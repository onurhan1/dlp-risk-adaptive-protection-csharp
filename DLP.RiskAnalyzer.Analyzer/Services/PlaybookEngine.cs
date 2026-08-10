using System.Diagnostics;
using System.Net.Mail;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Executes playbook graphs. Nodes are walked in topological order; each node consumes the
/// concatenated output of its incoming edges and produces a list of <see cref="PlaybookItem"/>.
/// Mails are written to <c>playbook_mail_log</c> either as "pending" (dry run, awaiting human
/// approval) or actually sent through <see cref="IEmailService"/>.
/// </summary>
public class PlaybookEngine : IPlaybookEngine
{
    /// <summary>
    /// Hard ceiling on recipients per run. A mis-configured filter must not be able to mail the
    /// entire company; anything past the cap is logged as "skipped" and reported.
    /// </summary>
    public const int MaxRecipientsPerRun = 200;

    /// <summary>Output port key used by every node except the two branching nodes.</summary>
    private const string MainHandle = "main";

    /// <summary>
    /// Row ceiling when loading incidents for a metric. Filtering happens in memory, matching how
    /// WeeklyFlagService already works; if the cap is reached the node says so in its run log
    /// rather than silently reporting a low count.
    /// </summary>
    private const int MetricRowCap = 500_000;

    private static readonly TimeSpan SameContentDuplicateWindow = TimeSpan.FromMinutes(30);

    /// <summary>Stand-in "user" recorded in the mail log for organisation-wide metric mails.</summary>
    private const string MetricSubjectLabel = "(kurum toplamı)";

    private readonly AnalyzerDbContext _context;
    private readonly IWeeklyFlagService _weeklyFlagService;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IEmailService _emailService;
    private readonly IInvestigationQueryRemediationSyncService _queryRemediationSync;
    private readonly ILogger<PlaybookEngine> _logger;

    public PlaybookEngine(
        AnalyzerDbContext context,
        IWeeklyFlagService weeklyFlagService,
        IIncidentRepository incidentRepository,
        IEmailService emailService,
        IInvestigationQueryRemediationSyncService queryRemediationSync,
        ILogger<PlaybookEngine> logger)
    {
        _context = context;
        _weeklyFlagService = weeklyFlagService;
        _incidentRepository = incidentRepository;
        _emailService = emailService;
        _queryRemediationSync = queryRemediationSync;
        _logger = logger;
    }

    // ── Run ──────────────────────────────────────────────────────────────────

    public async Task<PlaybookRun> RunAsync(int playbookId, string triggerType, bool? forceDryRun, CancellationToken ct = default)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.FirstOrDefaultAsync(p => p.Id == playbookId, ct)
            ?? throw new KeyNotFoundException($"Agentic Workflow bulunamadı: {playbookId}");

        var alreadyRunning = await _context.PlaybookRuns
            .AnyAsync(r => r.PlaybookId == playbookId && r.Status == PlaybookRunStatus.Running, ct);
        if (alreadyRunning)
            throw new InvalidOperationException("Bu workflow için hâlâ çalışan bir akış var. Bitmesini bekleyin.");

        // Dry run unless the playbook explicitly opted into automatic sending.
        var dryRun = forceDryRun ?? !playbook.AutoSend;

        var run = new PlaybookRun
        {
            PlaybookId = playbookId,
            StartedAt = DateTime.UtcNow,
            Status = PlaybookRunStatus.Running,
            TriggerType = triggerType,
            DryRun = dryRun
        };
        _context.PlaybookRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        var nodeLogs = new List<PlaybookNodeLog>();

        try
        {
            var graph = PlaybookJson.Deserialize<PlaybookGraph>(playbook.GraphJson)
                        ?? throw new InvalidOperationException("Workflow akışı okunamadı (bozuk graph verisi).");

            var validation = await ValidateAsync(graph, ct);
            if (!validation.IsValid)
                throw new InvalidOperationException("Akış geçerli değil: " + string.Join(" · ", validation.Errors));

            if (!dryRun && !await _emailService.IsConfiguredAsync())
                throw new InvalidOperationException(
                    "E-posta servisi yapılandırılmamış. Lütfen Ayarlar'dan SMTP bilgilerini girin.");

            await ExecuteGraphAsync(graph, playbook, run, dryRun, nodeLogs, ct);

            run.MailsSent = await CountAsync(run.Id, PlaybookMailStatus.Sent, ct);
            run.MailsPending = await CountAsync(run.Id, PlaybookMailStatus.Pending, ct);
            run.MailsFailed = await CountAsync(run.Id, PlaybookMailStatus.Failed, ct);
            run.MailsSkipped = await CountAsync(run.Id, PlaybookMailStatus.Skipped, ct);

            run.Status = run.MailsPending > 0
                ? PlaybookRunStatus.AwaitingApproval
                : PlaybookRunStatus.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playbook run failed (playbook {PlaybookId}, run {RunId})", playbookId, run.Id);
            run.Status = PlaybookRunStatus.Failed;
            run.ErrorMessage = ex.Message;
        }
        finally
        {
            run.FinishedAt = DateTime.UtcNow;
            run.NodeLogJson = PlaybookJson.Serialize(nodeLogs);

            playbook.LastRunAt = run.StartedAt;
            playbook.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }

        return run;
    }

    private Task<int> CountAsync(int runId, string status, CancellationToken ct) =>
        _context.PlaybookMailLogs.CountAsync(m => m.RunId == runId && m.Status == status, ct);

    private async Task ExecuteGraphAsync(
        PlaybookGraph graph,
        Playbook playbook,
        PlaybookRun run,
        bool dryRun,
        List<PlaybookNodeLog> nodeLogs,
        CancellationToken ct)
    {
        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var trigger = graph.Nodes.First(n => PlaybookNodeType.IsTrigger(n.Type));

        var order = TopologicalOrderFrom(trigger.Id, graph, nodesById);

        // nodeId → output handle → payload
        var outputs = new Dictionary<string, Dictionary<string, PlaybookPayload>>(StringComparer.Ordinal);

        // Run-wide guards shared by every send-mail node.
        var context = new SendContext(dryRun);

        foreach (var nodeId in order)
        {
            var node = nodesById[nodeId];
            var sw = Stopwatch.StartNew();

            var incoming = graph.Edges
                .Where(e => e.Target == nodeId)
                .Select(e => outputs.TryGetValue(e.Source, out var byHandle) &&
                             byHandle.TryGetValue(HandleOf(e), out var payload)
                    ? payload
                    : PlaybookPayload.Empty())
                .ToList();

            // Merging branches concatenates their user lists; a metric is a single value, so the
            // first branch that carries one wins.
            var input = new PlaybookPayload
            {
                Items = incoming.SelectMany(p => p.Items).ToList(),
                Metric = incoming.Select(p => p.Metric).FirstOrDefault(m => m != null)
            };

            var log = new PlaybookNodeLog
            {
                NodeId = node.Id,
                NodeType = node.Type,
                Label = string.IsNullOrWhiteSpace(node.Label) ? node.Type : node.Label,
                ItemsIn = input.Size
            };

            try
            {
                var produced = await ExecuteNodeAsync(node, input, playbook, run, context, ct);
                outputs[nodeId] = produced;
                log.ItemsOut = produced.Values.Sum(p => p.Size);
                log.Message = context.TakeMessage();
            }
            catch (Exception ex)
            {
                log.Status = "failed";
                log.Message = ex.Message;
                log.DurationMs = sw.ElapsedMilliseconds;
                nodeLogs.Add(log);
                throw new InvalidOperationException($"'{log.Label}' adımı başarısız: {ex.Message}", ex);
            }

            log.DurationMs = sw.ElapsedMilliseconds;
            nodeLogs.Add(log);
        }

        // Anything the trigger cannot reach never ran — say so instead of failing silently.
        foreach (var node in graph.Nodes.Where(n => !order.Contains(n.Id)))
        {
            nodeLogs.Add(new PlaybookNodeLog
            {
                NodeId = node.Id,
                NodeType = node.Type,
                Label = string.IsNullOrWhiteSpace(node.Label) ? node.Type : node.Label,
                Status = "skipped",
                Message = "Tetikleyiciye bağlı olmadığı için çalıştırılmadı"
            });
        }
    }

    private static string HandleOf(PlaybookEdge edge) =>
        string.IsNullOrWhiteSpace(edge.SourceHandle) ? MainHandle : edge.SourceHandle;

    /// <summary>Wraps a node's payload as its single default output port.</summary>
    private static Dictionary<string, PlaybookPayload> SingleOutput(PlaybookPayload payload) =>
        new(StringComparer.Ordinal) { [MainHandle] = payload };

    private async Task<Dictionary<string, PlaybookPayload>> ExecuteNodeAsync(
        PlaybookNode node,
        PlaybookPayload input,
        Playbook playbook,
        PlaybookRun run,
        SendContext context,
        CancellationToken ct)
    {
        switch (node.Type)
        {
            case PlaybookNodeType.TriggerSchedule:
            case PlaybookNodeType.TriggerManual:
                // A trigger emits a single empty tick; the source node fetches its own data.
                return SingleOutput(PlaybookPayload.Empty());

            case PlaybookNodeType.SourceWeeklyFlags:
                return SingleOutput(PlaybookPayload.OfItems(await LoadWeeklyFlagsAsync(node, context, ct)));

            case PlaybookNodeType.SourceIncidentMetric:
                return SingleOutput(PlaybookPayload.OfMetric(await LoadIncidentMetricAsync(node, context)));

            case PlaybookNodeType.TransformFilter:
                return SingleOutput(PlaybookPayload.OfItems(ApplyFilter(node, input.Items, context)));

            case PlaybookNodeType.LogicCondition:
                return ApplyCondition(node, input.Items, context);

            case PlaybookNodeType.LogicMetricThreshold:
                return ApplyMetricThreshold(node, input, context);

            case PlaybookNodeType.ActionSendMail:
                return SingleOutput(await SendMailsAsync(node, input, playbook, run, context, ct));

            case PlaybookNodeType.OutputReport:
                context.SetMessage(input.HasMetric
                    ? $"{input.Metric!.Label}: {input.Metric.Value:0.##} rapora yazıldı"
                    : $"{input.Items.Count} satır rapora yazıldı");
                return new Dictionary<string, PlaybookPayload>(StringComparer.Ordinal);

            default:
                throw new InvalidOperationException($"Bilinmeyen node tipi: {node.Type}");
        }
    }

    // ── Node implementations ─────────────────────────────────────────────────

    private async Task<List<PlaybookItem>> LoadWeeklyFlagsAsync(PlaybookNode node, SendContext context, CancellationToken ct)
    {
        var days = node.GetInt("days") ?? 7;
        if (days <= 0) days = 7;

        var criteria = node.GetStringList("criteria");
        if (criteria.Count == 0) criteria = WeeklyFlagCriterion.All.ToList();

        var flags = await _weeklyFlagService.GetWeeklyFlagsAsync(days);

        var collected = new List<PlaybookItem>();
        foreach (var criterion in criteria)
        {
            var users = criterion switch
            {
                WeeklyFlagCriterion.PersonalEmailSenders => flags.PersonalEmailSenders,
                WeeklyFlagCriterion.HighVolume => flags.HighVolume,
                WeeklyFlagCriterion.MassiveMatches => flags.MassiveMatches,
                _ => new List<WeeklyFlagUserDto>()
            };
            collected.AddRange(users.Select(u => new PlaybookItem(u, criterion)));
        }

        // The same person can trip several criteria; keep the strongest signal so nobody
        // receives two mails from one run.
        var deduped = collected
            .GroupBy(i => i.User.UserEmail, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => i.User.TriggerCount).First())
            .OrderByDescending(i => i.User.TriggerCount)
            .ToList();

        context.SetMessage(
            $"{string.Join(", ", criteria.Select(WeeklyFlagCriterion.Label))} · son {days} gün · " +
            $"{collected.Count} eşleşme → {deduped.Count} kullanıcı");

        return deduped;
    }

    private static List<PlaybookItem> ApplyFilter(PlaybookNode node, List<PlaybookItem> input, SendContext context)
    {
        var minTriggerCount = node.GetInt("min_trigger_count");
        var teamContains = node.GetString("team_contains");
        var domains = node.GetStringList("email_domain_in")
            .Select(d => d.TrimStart('@').ToLowerInvariant())
            .ToList();
        var excluded = new HashSet<string>(node.GetStringList("exclude_users"), StringComparer.OrdinalIgnoreCase);

        var result = input.Where(item =>
        {
            var user = item.User;

            if (minTriggerCount.HasValue && user.TriggerCount < minTriggerCount.Value) return false;

            if (!string.IsNullOrWhiteSpace(teamContains) &&
                (user.Team == null || !user.Team.Contains(teamContains, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (domains.Count > 0)
            {
                var address = RecipientOf(user);
                var at = address.LastIndexOf('@');
                var domain = at >= 0 ? address[(at + 1)..].ToLowerInvariant() : string.Empty;
                if (!domains.Contains(domain)) return false;
            }

            if (excluded.Count > 0 &&
                (excluded.Contains(user.UserEmail) || excluded.Contains(user.ContactEmail ?? string.Empty)))
                return false;

            return true;
        }).ToList();

        context.SetMessage($"{input.Count} kullanıcıdan {result.Count} tanesi filtreyi geçti");
        return result;
    }

    private static Dictionary<string, PlaybookPayload> ApplyCondition(
        PlaybookNode node, List<PlaybookItem> input, SendContext context)
    {
        var field = node.GetString("field") ?? "triggerCount";
        var op = node.GetString("op") ?? "gte";
        var threshold = node.GetInt("value") ?? 0;

        var onTrue = new List<PlaybookItem>();
        var onFalse = new List<PlaybookItem>();

        foreach (var item in input)
        {
            var actual = field switch
            {
                "maxMatches" => item.User.SampleIncidents?.Count > 0
                    ? item.User.SampleIncidents.Max(i => i.MaxMatches)
                    : 0,
                _ => item.User.TriggerCount
            };

            var matches = op switch
            {
                "gt" => actual > threshold,
                "lt" => actual < threshold,
                "lte" => actual <= threshold,
                "eq" => actual == threshold,
                _ => actual >= threshold
            };

            (matches ? onTrue : onFalse).Add(item);
        }

        context.SetMessage($"Evet: {onTrue.Count} · Hayır: {onFalse.Count}");

        return new Dictionary<string, PlaybookPayload>(StringComparer.Ordinal)
        {
            ["true"] = PlaybookPayload.OfItems(onTrue),
            ["false"] = PlaybookPayload.OfItems(onFalse)
        };
    }

    /// <summary>
    /// Counts incidents over a window and reduces them to a single organisation-wide number.
    /// Filters are applied in memory, the same approach WeeklyFlagService uses.
    /// </summary>
    private async Task<PlaybookMetric> LoadIncidentMetricAsync(PlaybookNode node, SendContext context)
    {
        var days = node.GetInt("days") ?? 7;
        if (days <= 0) days = 7;

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var incidents = await _incidentRepository.GetIncidentsAsync(startDate, endDate, MetricRowCap);

        // ── Filters ───────────────────────────────────────────────────────────
        var channels = node.GetStringList("channels");
        var dataTypes = node.GetStringList("data_types");
        var actions = node.GetStringList("actions");
        var severities = node.GetStringList("severities");
        var minSeverity = node.GetInt("min_severity");
        var minRiskScore = node.GetInt("min_risk_score");
        var minMatches = node.GetInt("min_matches");
        var policyContains = node.GetString("policy_contains")?.Trim();
        var teamContains = node.GetString("team_contains")?.Trim();
        var destinationContains = node.GetString("destination_contains")?.Trim();

        var filtered = incidents.Where(i =>
            MatchesAny(channels, i.Channel) &&
            MatchesAny(dataTypes, i.DataType) &&
            MatchesAny(actions, i.Action) &&
            (severities.Count == 0 || severities.Contains(i.Severity.ToString())) &&
            (!minSeverity.HasValue || i.Severity >= minSeverity.Value) &&
            (!minRiskScore.HasValue || (i.RiskScore ?? 0) >= minRiskScore.Value) &&
            (!minMatches.HasValue || i.MaxMatches >= minMatches.Value) &&
            Contains(i.Policy, policyContains) &&
            Contains(i.Team ?? i.Department, teamContains) &&
            Contains(i.Destination, destinationContains)
        ).ToList();

        var uniqueUsers = filtered
            .Select(i => i.UserEmail)
            .Where(e => !string.IsNullOrWhiteSpace(e) && !e.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var kind = node.GetString("metric") ?? IncidentMetricKind.TotalIncidents;
        var value = kind switch
        {
            IncidentMetricKind.UniqueUsers => uniqueUsers,
            IncidentMetricKind.MaxRiskScore => filtered.Count > 0 ? filtered.Max(i => i.RiskScore ?? 0) : 0,
            IncidentMetricKind.AvgRiskScore => filtered.Count > 0 ? filtered.Average(i => i.RiskScore ?? 0) : 0,
            _ => filtered.Count
        };

        var metric = new PlaybookMetric
        {
            Kind = kind,
            Label = IncidentMetricKind.Label(kind),
            Value = value,
            TotalIncidents = filtered.Count,
            UniqueUsers = uniqueUsers,
            Days = days,
            WindowStart = startDate.ToDateTime(TimeOnly.MinValue),
            WindowEnd = endDate.ToDateTime(TimeOnly.MinValue),
            FilterSummary = BuildFilterSummary(node),
            Breakdown = BuildBreakdown(filtered, node.GetString("breakdown_by") ?? IncidentBreakdownDimension.Channel)
        };

        var message = $"{metric.Label}: {metric.Value:0.##} · son {days} gün · " +
                      $"{filtered.Count}/{incidents.Count} incident filtreyi geçti · {uniqueUsers} kullanıcı";
        if (incidents.Count >= MetricRowCap)
            message += $" · UYARI: {MetricRowCap} satır üst sınırına ulaşıldı, sayım eksik olabilir";
        context.SetMessage(message);

        return metric;
    }

    /// <summary>True when no filter values are given, or the field matches one of them.</summary>
    private static bool MatchesAny(List<string> allowed, string? value)
    {
        if (allowed.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return allowed.Any(a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool Contains(string? value, string? needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return true;
        return value != null && value.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static List<PlaybookMetricBreakdown> BuildBreakdown(List<Incident> incidents, string dimension)
    {
        if (dimension == IncidentBreakdownDimension.None || incidents.Count == 0)
            return new List<PlaybookMetricBreakdown>();

        Func<Incident, string> selector = dimension switch
        {
            IncidentBreakdownDimension.Policy => i => i.Policy ?? "-",
            IncidentBreakdownDimension.DataType => i => i.DataType ?? "-",
            IncidentBreakdownDimension.Team => i => i.Team ?? i.Department ?? "-",
            IncidentBreakdownDimension.Severity => i => $"Severity {i.Severity}",
            _ => i => i.Channel ?? "-"
        };

        return incidents
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PlaybookMetricBreakdown(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .Take(15)
            .ToList();
    }

    /// <summary>Readable description of the active filters, so a metric mail states its own scope.</summary>
    private static string BuildFilterSummary(PlaybookNode node)
    {
        var parts = new List<string>();

        void AddList(string key, string label)
        {
            var values = node.GetStringList(key);
            if (values.Count > 0) parts.Add($"{label}: {string.Join(", ", values)}");
        }
        void AddInt(string key, string label)
        {
            var value = node.GetInt(key);
            if (value.HasValue) parts.Add($"{label}: {value.Value}");
        }
        void AddText(string key, string label)
        {
            var value = node.GetString(key)?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) parts.Add($"{label}: {value}");
        }

        AddList("channels", "Kanal");
        AddList("data_types", "Veri tipi");
        AddList("actions", "Aksiyon");
        AddList("severities", "Şiddet");
        AddInt("min_severity", "Min şiddet");
        AddInt("min_risk_score", "Min risk skoru");
        AddInt("min_matches", "Min eşleşme");
        AddText("policy_contains", "Politika içerir");
        AddText("team_contains", "Takım içerir");
        AddText("destination_contains", "Hedef içerir");

        return parts.Count == 0 ? "Filtre yok (tüm incident'lar)" : string.Join(" · ", parts);
    }

    /// <summary>
    /// Gates the flow on an aggregate metric: the payload continues down the "true" branch only
    /// when the threshold is met, so a downstream mail node fires just for a breach.
    /// </summary>
    private static Dictionary<string, PlaybookPayload> ApplyMetricThreshold(
        PlaybookNode node, PlaybookPayload input, SendContext context)
    {
        if (!input.HasMetric)
            throw new InvalidOperationException(
                "Metrik Eşiği node'una bir Incident Metriği bağlanmalı (kullanıcı listesi kabul etmez).");

        var metric = input.Metric!;
        var op = node.GetString("op") ?? "gt";
        var threshold = node.GetInt("value") ?? 0;

        var exceeded = op switch
        {
            "gte" => metric.Value >= threshold,
            "lt" => metric.Value < threshold,
            "lte" => metric.Value <= threshold,
            "eq" => Math.Abs(metric.Value - threshold) < 0.0001,
            _ => metric.Value > threshold
        };

        metric.Threshold = threshold;
        metric.ThresholdExceeded = exceeded;

        context.SetMessage(
            $"{metric.Label} = {metric.Value:0.##} · eşik {OpLabel(op)} {threshold} · " +
            (exceeded ? "AŞILDI, akış devam ediyor" : "aşılmadı, akış burada duruyor"));

        var carried = PlaybookPayload.OfMetric(metric);
        return new Dictionary<string, PlaybookPayload>(StringComparer.Ordinal)
        {
            ["true"] = exceeded ? carried : PlaybookPayload.Empty(),
            ["false"] = exceeded ? PlaybookPayload.Empty() : carried
        };
    }

    private static string OpLabel(string op) => op switch
    {
        "gte" => "≥",
        "lt" => "<",
        "lte" => "≤",
        "eq" => "=",
        _ => ">"
    };

    private async Task<PlaybookPayload> SendMailsAsync(
        PlaybookNode node,
        PlaybookPayload payload,
        Playbook playbook,
        PlaybookRun run,
        SendContext context,
        CancellationToken ct)
    {
        var (subjectTemplate, bodyTemplate) = await ResolveTemplateAsync(node, ct);

        var recipientMode = node.GetString("recipient_mode") ?? "user";
        var fixedRecipient = node.GetString("fixed_recipient")?.Trim();
        var ccEmail = node.GetString("cc_email")?.Trim();
        if (string.IsNullOrWhiteSpace(ccEmail)) ccEmail = null;

        if (recipientMode == "fixed" && string.IsNullOrWhiteSpace(fixedRecipient))
            throw new InvalidOperationException("Sabit alıcı adresi boş olamaz.");
        if (recipientMode == "fixed" && fixedRecipient!.Contains('@') && !IsValidEmail(fixedRecipient))
            throw new InvalidOperationException("Sabit alıcı adresi geçerli değil.");

        // A metric describes the whole organisation, so it produces one summary mail rather than
        // one mail per person — and there is no user address to fall back on.
        if (payload.HasMetric)
        {
            await SendMetricMailAsync(
                node, payload.Metric!, subjectTemplate, bodyTemplate,
                fixedRecipient, ccEmail, playbook, run, context, ct);
            return payload;
        }

        var input = payload.Items;
        var now = DateTime.UtcNow;
        var processed = new List<PlaybookItem>();
        var queryLogEntries = new List<PlaybookMailLog>();
        var capped = 0;

        foreach (var item in input)
        {
            var user = item.User;
            var toEmail = recipientMode == "fixed" ? fixedRecipient! : RecipientOf(user);

            var entry = new PlaybookMailLog
            {
                RunId = run.Id,
                PlaybookId = playbook.Id,
                NodeId = node.Id,
                UserEmail = user.UserEmail,
                FullName = user.FullName,
                Team = user.Team,
                ToEmail = toEmail,
                CcEmail = ccEmail,
                Subject = PlaybookMailRenderer.ApplyPlaceholders(subjectTemplate, user, now),
                BodyHtml = PlaybookMailRenderer.ToEmailHtml(
                    PlaybookMailRenderer.ApplyPlaceholders(bodyTemplate, user, now)),
                SourceCriterion = item.SourceCriterion,
                TriggerCount = user.TriggerCount,
                CreatedAt = now
            };

            if (!toEmail.Contains('@'))
            {
                entry.Status = PlaybookMailStatus.Pending;
                entry.ErrorMessage = "Alıcı adresinde @ yok; manuel gönderim için bekliyor.";
                queryLogEntries.Add(entry);
            }
            else if (!IsValidEmail(toEmail))
            {
                entry.Status = PlaybookMailStatus.Skipped;
                entry.ErrorMessage = "Geçerli bir alıcı adresi yok";
            }
            else if (await HasDuplicateMailAsync(toEmail, entry.Subject, entry.BodyHtml, null, ct))
            {
                entry.Status = PlaybookMailStatus.Skipped;
                entry.ErrorMessage = "Aynı alıcıya aynı içerikte mail son 30 dakika içinde hazırlanmış veya gönderilmiş.";
            }
            else if (!context.TryReserveRecipient(toEmail))
            {
                // Either a duplicate within this run or past the per-run recipient cap.
                entry.Status = PlaybookMailStatus.Skipped;
                entry.ErrorMessage = context.LastSkipReason;
                if (context.LastSkipReason?.Contains("üst sınır") == true) capped++;
            }
            else if (context.DryRun)
            {
                entry.Status = PlaybookMailStatus.Pending;
                queryLogEntries.Add(entry);
                processed.Add(item);
            }
            else
            {
                var success = await _emailService.SendEmailAsync(
                    toEmail: toEmail,
                    subject: entry.Subject,
                    body: entry.BodyHtml,
                    isHtml: true,
                    toName: recipientMode == "fixed" ? null : (user.FullName ?? user.UserEmail),
                    ccEmail: ccEmail);

                if (success)
                {
                    entry.Status = PlaybookMailStatus.Sent;
                    entry.SentAt = DateTime.UtcNow;
                    queryLogEntries.Add(entry);
                    processed.Add(item);
                }
                else
                {
                    entry.Status = PlaybookMailStatus.Failed;
                    entry.ErrorMessage = "SMTP gönderimi başarısız (yapılandırmayı ve logları kontrol edin)";
                }
            }

            _context.PlaybookMailLogs.Add(entry);
        }

        await _context.SaveChangesAsync(ct);
        await UpsertQueryRecordsForMailLogsAsync(queryLogEntries, ct);

        var verb = context.DryRun ? "onay için hazırlandı" : "gönderildi";
        var message = $"{processed.Count} mail {verb}";
        if (capped > 0) message += $" · {capped} alıcı üst sınır ({MaxRecipientsPerRun}) nedeniyle atlandı";
        context.SetMessage(message);

        return PlaybookPayload.OfItems(processed);
    }

    /// <summary>
    /// One summary mail for an organisation-wide metric. There is no per-user address here, so a
    /// fixed recipient is required — the UI surfaces this as soon as a metric node is connected.
    /// </summary>
    private async Task SendMetricMailAsync(
        PlaybookNode node,
        PlaybookMetric metric,
        string subjectTemplate,
        string bodyTemplate,
        string? fixedRecipient,
        string? ccEmail,
        Playbook playbook,
        PlaybookRun run,
        SendContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fixedRecipient))
            throw new InvalidOperationException(
                "Metrik maili için sabit bir alıcı adresi gerekir. Mail node'unda Alıcı'yı " +
                "\"Sabit bir adres\" yapıp adresi girin.");
        if (fixedRecipient.Contains('@') && !IsValidEmail(fixedRecipient))
            throw new InvalidOperationException("Sabit alıcı adresi geçerli değil.");

        var now = DateTime.UtcNow;
        var entry = new PlaybookMailLog
        {
            RunId = run.Id,
            PlaybookId = playbook.Id,
            NodeId = node.Id,
            UserEmail = MetricSubjectLabel,
            FullName = metric.Label,
            Team = null,
            ToEmail = fixedRecipient!,
            CcEmail = ccEmail,
            Subject = PlaybookMailRenderer.ApplyMetricPlaceholders(subjectTemplate, metric, now),
            BodyHtml = PlaybookMailRenderer.ToEmailHtml(
                PlaybookMailRenderer.ApplyMetricPlaceholders(bodyTemplate, metric, now)),
            SourceCriterion = PlaybookNodeType.SourceIncidentMetric,
            TriggerCount = (int)Math.Round(metric.Value),
            CreatedAt = now
        };

        if (!fixedRecipient.Contains('@'))
        {
            entry.Status = PlaybookMailStatus.Pending;
            entry.ErrorMessage = "Alıcı adresinde @ yok; manuel gönderim için bekliyor.";
        }
        else if (await HasDuplicateMailAsync(fixedRecipient!, entry.Subject, entry.BodyHtml, null, ct))
        {
            entry.Status = PlaybookMailStatus.Skipped;
            entry.ErrorMessage = "Aynı alıcıya aynı içerikte mail son 30 dakika içinde hazırlanmış veya gönderilmiş.";
        }
        else if (!context.TryReserveRecipient(fixedRecipient!))
        {
            entry.Status = PlaybookMailStatus.Skipped;
            entry.ErrorMessage = context.LastSkipReason;
        }
        else if (context.DryRun)
        {
            entry.Status = PlaybookMailStatus.Pending;
        }
        else
        {
            var success = await _emailService.SendEmailAsync(
                toEmail: fixedRecipient!,
                subject: entry.Subject,
                body: entry.BodyHtml,
                isHtml: true,
                toName: null,
                ccEmail: ccEmail);

            if (success)
            {
                entry.Status = PlaybookMailStatus.Sent;
                entry.SentAt = DateTime.UtcNow;
            }
            else
            {
                entry.Status = PlaybookMailStatus.Failed;
                entry.ErrorMessage = "SMTP gönderimi başarısız (yapılandırmayı ve logları kontrol edin)";
            }
        }

        _context.PlaybookMailLogs.Add(entry);
        await _context.SaveChangesAsync(ct);
        if (entry.Status == PlaybookMailStatus.Pending || entry.Status == PlaybookMailStatus.Sent)
            await UpsertQueryRecordsForMailLogsAsync(new[] { entry }, ct);

        context.SetMessage(entry.Status switch
        {
            PlaybookMailStatus.Pending => $"Özet mail onay için hazırlandı → {fixedRecipient}",
            PlaybookMailStatus.Sent => $"Özet mail gönderildi → {fixedRecipient}",
            PlaybookMailStatus.Skipped => entry.ErrorMessage ?? "Özet mail atlandı",
            _ => "Özet mail gönderilemedi"
        });
    }

    private async Task<bool> HasDuplicateMailAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        int? excludeMailLogId,
        CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.Subtract(SameContentDuplicateWindow);
        var normalized = toEmail.Trim().ToLower();

        return await _context.PlaybookMailLogs.AsNoTracking()
            .AnyAsync(m =>
                (!excludeMailLogId.HasValue || m.Id != excludeMailLogId.Value) &&
                m.CreatedAt >= cutoff &&
                m.ToEmail.ToLower() == normalized &&
                m.Subject == subject &&
                m.BodyHtml == bodyHtml &&
                (m.Status == PlaybookMailStatus.Pending || m.Status == PlaybookMailStatus.Sent),
                ct);
    }

    private async Task UpsertQueryRecordsForMailLogsAsync(IEnumerable<PlaybookMailLog> mailLogs, CancellationToken ct)
    {
        var changedRows = new List<InvestigationQueryRecord>();
        var now = DateTime.UtcNow;

        foreach (var mail in mailLogs.Where(m => m.Id > 0))
        {
            var query = await _context.InvestigationQueries
                .FirstOrDefaultAsync(q => q.PlaybookMailLogId == mail.Id, ct);

            if (query == null)
            {
                query = new InvestigationQueryRecord
                {
                    PlaybookMailLogId = mail.Id,
                    CreatedAt = mail.CreatedAt,
                    CreatedBy = "Agentic Workflow"
                };
                _context.InvestigationQueries.Add(query);
            }

            query.FullName = TurkishNameHelper.FromEmailLocalPart(mail.ToEmail, mail.FullName ?? mail.UserEmail);
            query.UserCode = ResolveQueryUserCode(mail.UserEmail, mail.ToEmail);
            query.MailAddress = mail.ToEmail;
            query.Subject = mail.Subject;
            query.QueryDate = mail.SentAt ?? mail.CreatedAt;
            query.ResponseStatus = mail.Status == PlaybookMailStatus.Sent ? "Mail gönderildi" : "Manuel gönderim bekliyor";
            query.Action = mail.ErrorMessage ?? (mail.Status == PlaybookMailStatus.Sent ? "Sorgu maili gönderildi" : "Sorgu maili hazırlandı");
            query.QueryStatus = mail.Status == PlaybookMailStatus.Sent
                ? InvestigationQueryStatus.Queried
                : InvestigationQueryStatus.Pending;
            query.Source = "agentic_workflow";
            query.Team = mail.Team;
            query.Notes = mail.SourceCriterion;
            query.UpdatedAt = now;
            query.UpdatedBy = "Agentic Workflow";
            changedRows.Add(query);
        }

        await _queryRemediationSync.SyncAsync(changedRows, "Agentic Workflow", now, ct);
        await _context.SaveChangesAsync(ct);
    }

    private static string ResolveQueryUserCode(string userEmail, string toEmail)
    {
        var candidate = string.IsNullOrWhiteSpace(userEmail) ? toEmail : userEmail;
        candidate = candidate.Trim();
        if (candidate.Contains('\\')) candidate = candidate.Split('\\').Last();
        if (candidate.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return candidate.Contains('@') ? string.Empty : candidate;
    }

    /// <summary>
    /// Subject/body come from a saved mail template; per-node overrides win when filled in,
    /// which is how the analyst tweaks one playbook without editing the shared template.
    /// </summary>
    private async Task<(string Subject, string Body)> ResolveTemplateAsync(PlaybookNode node, CancellationToken ct)
    {
        var subject = node.GetString("subject_override")?.Trim();
        var body = node.GetString("body_override");

        var templateId = node.GetInt("template_id");
        if (templateId.HasValue && templateId.Value > 0)
        {
            var template = await _context.MailTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId.Value, ct)
                ?? throw new InvalidOperationException($"Mail şablonu bulunamadı (id: {templateId.Value})");

            if (string.IsNullOrWhiteSpace(subject)) subject = template.Subject;
            if (string.IsNullOrWhiteSpace(body)) body = template.Body;
        }

        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("Mail konusu boş — bir şablon seçin ya da konu yazın.");

        return (subject, body ?? string.Empty);
    }

    private static string RecipientOf(WeeklyFlagUserDto user) =>
        string.IsNullOrWhiteSpace(user.ContactEmail) ? user.UserEmail : user.ContactEmail;

    // ── Approval of pending (dry-run) mails ──────────────────────────────────

    public async Task<(int Sent, int Failed)> ApprovePendingAsync(int runId, int? mailLogId, CancellationToken ct = default)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var run = await _context.PlaybookRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new KeyNotFoundException($"Çalıştırma bulunamadı: {runId}");

        if (!await _emailService.IsConfiguredAsync())
            throw new InvalidOperationException(
                "E-posta servisi yapılandırılmamış. Lütfen Ayarlar'dan SMTP bilgilerini girin.");

        var query = _context.PlaybookMailLogs
            .Where(m => m.RunId == runId && m.Status == PlaybookMailStatus.Pending);
        if (mailLogId.HasValue) query = query.Where(m => m.Id == mailLogId.Value);

        var pending = await query.OrderBy(m => m.Id).ToListAsync(ct);

        int sent = 0, failed = 0;
        var queryUpdates = new List<PlaybookMailLog>();
        foreach (var entry in pending)
        {
            if (!entry.ToEmail.Contains('@') || !IsValidEmail(entry.ToEmail))
            {
                entry.ErrorMessage = !entry.ToEmail.Contains('@')
                    ? "Alıcı adresinde @ yok; manuel gönderim için bekliyor."
                    : "Geçerli bir alıcı adresi yok; manuel düzeltme gerekiyor.";
                failed++;
                queryUpdates.Add(entry);
                continue;
            }

            if (await HasDuplicateMailAsync(entry.ToEmail, entry.Subject, entry.BodyHtml, entry.Id, ct))
            {
                entry.Status = PlaybookMailStatus.Skipped;
                entry.ErrorMessage = "Aynı alıcıya aynı içerikte mail son 30 dakika içinde hazırlanmış veya gönderilmiş.";
                failed++;
                continue;
            }

            var success = await _emailService.SendEmailAsync(
                toEmail: entry.ToEmail,
                subject: entry.Subject,
                body: entry.BodyHtml,
                isHtml: true,
                toName: entry.FullName ?? entry.UserEmail,
                ccEmail: entry.CcEmail);

            if (success)
            {
                entry.Status = PlaybookMailStatus.Sent;
                entry.SentAt = DateTime.UtcNow;
                entry.ErrorMessage = null;
                queryUpdates.Add(entry);
                sent++;
            }
            else
            {
                entry.Status = PlaybookMailStatus.Failed;
                entry.ErrorMessage = "SMTP gönderimi başarısız (yapılandırmayı ve logları kontrol edin)";
                failed++;
            }
        }

        await _context.SaveChangesAsync(ct);
        await UpsertQueryRecordsForMailLogsAsync(queryUpdates, ct);

        run.MailsSent = await CountAsync(runId, PlaybookMailStatus.Sent, ct);
        run.MailsPending = await CountAsync(runId, PlaybookMailStatus.Pending, ct);
        run.MailsFailed = await CountAsync(runId, PlaybookMailStatus.Failed, ct);
        run.MailsSkipped = await CountAsync(runId, PlaybookMailStatus.Skipped, ct);

        if (run.MailsPending == 0)
        {
            run.Status = run.MailsSent == 0 && run.MailsFailed > 0
                ? PlaybookRunStatus.Failed
                : PlaybookRunStatus.Success;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Playbook run {RunId} approval: {Sent} sent, {Failed} failed", runId, sent, failed);

        return (sent, failed);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    public async Task<PlaybookValidationResult> ValidateAsync(PlaybookGraph graph, CancellationToken ct = default)
    {
        var result = new PlaybookValidationResult();

        if (graph.Nodes.Count == 0)
        {
            result.Errors.Add("Akışta hiç node yok.");
            return result;
        }

        var duplicateIds = graph.Nodes
            .GroupBy(n => n.Id, StringComparer.Ordinal)
            .Where(g => g.Key is null || g.Key.Length == 0 || g.Count() > 1)
            .ToList();
        if (duplicateIds.Count > 0)
            result.Errors.Add("Node kimlikleri boş ya da tekrar ediyor.");

        foreach (var node in graph.Nodes.Where(n => !PlaybookNodeType.All.Contains(n.Type)))
            result.Errors.Add($"Bilinmeyen node tipi: {node.Type}");

        var triggers = graph.Nodes.Where(n => PlaybookNodeType.IsTrigger(n.Type)).ToList();
        if (triggers.Count == 0)
            result.Errors.Add("Akışta bir tetikleyici (zamanlama ya da manuel) olmalı.");
        else if (triggers.Count > 1)
            result.Errors.Add("Akışta yalnızca bir tetikleyici olabilir.");

        var nodesById = new Dictionary<string, PlaybookNode>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes) nodesById[node.Id] = node;

        foreach (var edge in graph.Edges)
        {
            if (!nodesById.ContainsKey(edge.Source) || !nodesById.ContainsKey(edge.Target))
            {
                result.Errors.Add("Bir bağlantı var olmayan node'a işaret ediyor.");
                continue;
            }
            if (edge.Source == edge.Target)
                result.Errors.Add("Bir node kendisine bağlanamaz.");
            if (PlaybookNodeType.InputCount(nodesById[edge.Target].Type) == 0)
                result.Errors.Add("Tetikleyici node'un girişi olamaz.");
            if (PlaybookNodeType.OutputCount(nodesById[edge.Source].Type) == 0)
                result.Errors.Add("Rapor node'unun çıkışı olamaz.");
        }

        if (HasCycle(graph, nodesById))
            result.Errors.Add("Akışta döngü var; node'lar bir çevrim oluşturamaz.");

        // Per-node configuration checks.
        foreach (var node in graph.Nodes)
        {
            switch (node.Type)
            {
                case PlaybookNodeType.TriggerSchedule:
                {
                    var cron = BuildCron(node);
                    if (cron == null || !CronSchedule.IsValid(cron))
                        result.Errors.Add($"'{node.Label}' zamanlaması geçersiz.");
                    break;
                }

                case PlaybookNodeType.SourceWeeklyFlags:
                {
                    var criteria = node.GetStringList("criteria");
                    if (criteria.Count == 0)
                        result.Errors.Add($"'{node.Label}' için en az bir kriter seçilmeli.");
                    else if (criteria.Except(WeeklyFlagCriterion.All).Any())
                        result.Errors.Add($"'{node.Label}' bilinmeyen bir kriter içeriyor.");
                    break;
                }

                case PlaybookNodeType.SourceIncidentMetric:
                {
                    var days = node.GetInt("days");
                    if (days is <= 0)
                        result.Errors.Add($"'{node.Label}' gün sayısı 0'dan büyük olmalı.");

                    var kind = node.GetString("metric");
                    if (!string.IsNullOrWhiteSpace(kind) && !IncidentMetricKind.All.Contains(kind))
                        result.Errors.Add($"'{node.Label}' bilinmeyen bir metrik içeriyor.");

                    var dimension = node.GetString("breakdown_by");
                    if (!string.IsNullOrWhiteSpace(dimension) && !IncidentBreakdownDimension.All.Contains(dimension))
                        result.Errors.Add($"'{node.Label}' bilinmeyen bir kırılım içeriyor.");
                    break;
                }

                case PlaybookNodeType.LogicMetricThreshold:
                {
                    if (node.GetInt("value") is null)
                        result.Errors.Add($"'{node.Label}' için bir eşik değeri girin.");
                    break;
                }

                case PlaybookNodeType.ActionSendMail:
                {
                    var templateId = node.GetInt("template_id");
                    var hasOverride = !string.IsNullOrWhiteSpace(node.GetString("subject_override"));
                    if ((templateId is null or <= 0) && !hasOverride)
                        result.Errors.Add($"'{node.Label}' için bir şablon seçin ya da konu girin.");
                    else if (templateId is > 0 &&
                             !await _context.MailTemplates.AnyAsync(t => t.Id == templateId.Value, ct))
                        result.Errors.Add($"'{node.Label}' seçili şablon artık mevcut değil.");

                    if (node.GetString("recipient_mode") == "fixed" &&
                        !IsValidEmail(node.GetString("fixed_recipient")))
                        result.Errors.Add($"'{node.Label}' sabit alıcı adresi geçerli değil.");

                    var cc = node.GetString("cc_email");
                    if (!string.IsNullOrWhiteSpace(cc) && !IsValidEmail(cc))
                        result.Errors.Add($"'{node.Label}' CC adresi geçerli değil.");
                    break;
                }
            }
        }

        // ── Metric path rules ────────────────────────────────────────────────
        // A metric is one organisation-wide number, so nodes downstream of a metric source behave
        // differently from the per-user path. Catching this here beats failing mid-run.
        var metricSources = graph.Nodes.Where(n => n.Type == PlaybookNodeType.SourceIncidentMetric).ToList();
        var metricReach = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in metricSources)
            foreach (var id in Downstream(source.Id, graph, nodesById))
                metricReach.Add(id);

        foreach (var node in graph.Nodes.Where(n => n.Type == PlaybookNodeType.LogicMetricThreshold))
        {
            if (!metricReach.Contains(node.Id))
                result.Errors.Add(
                    $"'{node.Label}' bir Incident Metriği node'una bağlı değil; " +
                    "metrik eşiği yalnızca metrik girdisiyle çalışır.");
        }

        foreach (var node in graph.Nodes.Where(n => n.Type == PlaybookNodeType.ActionSendMail))
        {
            if (!metricReach.Contains(node.Id)) continue;

            if (node.GetString("recipient_mode") != "fixed")
                result.Errors.Add(
                    $"'{node.Label}' bir metrik akışında olduğu için Alıcı \"Sabit bir adres\" olmalı " +
                    "(kurum toplamının kişisel bir adresi yok).");
        }

        // Advisory checks — these do not block saving a half-built draft.
        if (triggers.Count == 1)
        {
            var reachable = TopologicalOrderFrom(triggers[0].Id, graph, nodesById);
            var orphans = graph.Nodes.Where(n => !reachable.Contains(n.Id)).ToList();
            if (orphans.Count > 0)
                result.Warnings.Add(
                    $"Tetikleyiciye bağlı olmayan {orphans.Count} node çalıştırılmayacak: " +
                    string.Join(", ", orphans.Select(o => o.Label)));

            if (!graph.Nodes.Any(n => n.Type is PlaybookNodeType.SourceWeeklyFlags
                                             or PlaybookNodeType.SourceIncidentMetric))
                result.Warnings.Add("Akışta veri kaynağı yok; hiçbir kullanıcı ya da metrik hesaplanmayacak.");

            foreach (var node in metricSources.Where(n => !metricReach.Any(id =>
                         id != n.Id && nodesById[id].Type == PlaybookNodeType.LogicMetricThreshold)))
                result.Warnings.Add(
                    $"'{node.Label}' bir Metrik Eşiği node'una bağlı değil; mail eşik kontrolü olmadan " +
                    "her çalıştırmada gönderilir.");
            if (!graph.Nodes.Any(n => n.Type == PlaybookNodeType.ActionSendMail))
                result.Warnings.Add("Akışta mail gönderme adımı yok.");
            if (!graph.Nodes.Any(n => n.Type == PlaybookNodeType.OutputReport))
                result.Warnings.Add("Akışta rapor çıktısı yok; sonuçlar yine de mail kaydına yazılır.");
        }

        return result;
    }

    /// <summary>Every node reachable by following edges forward from <paramref name="startId"/>.</summary>
    private static HashSet<string> Downstream(
        string startId, PlaybookGraph graph, Dictionary<string, PlaybookNode> nodesById)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(startId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current)) continue;
            foreach (var edge in graph.Edges.Where(e => e.Source == current && nodesById.ContainsKey(e.Target)))
                stack.Push(edge.Target);
        }
        return seen;
    }

    /// <summary>
    /// Kahn's algorithm restricted to the subgraph reachable from the trigger, so a node with
    /// two incoming branches only runs after both have produced their items.
    /// </summary>
    private static List<string> TopologicalOrderFrom(
        string startId, PlaybookGraph graph, Dictionary<string, PlaybookNode> nodesById)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(startId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!reachable.Add(current)) continue;
            foreach (var edge in graph.Edges.Where(e => e.Source == current && nodesById.ContainsKey(e.Target)))
                stack.Push(edge.Target);
        }

        var edges = graph.Edges
            .Where(e => reachable.Contains(e.Source) && reachable.Contains(e.Target))
            .ToList();

        var inDegree = reachable.ToDictionary(id => id, id => edges.Count(e => e.Target == id), StringComparer.Ordinal);
        var ready = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();

        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            order.Add(current);
            foreach (var edge in edges.Where(e => e.Source == current))
            {
                inDegree[edge.Target]--;
                if (inDegree[edge.Target] == 0) ready.Enqueue(edge.Target);
            }
        }

        // Nodes left with a non-zero in-degree sit on a cycle; validation reports that
        // separately, and skipping them here keeps a broken graph from hanging the run.
        return order;
    }

    private static bool HasCycle(PlaybookGraph graph, Dictionary<string, PlaybookNode> nodesById)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            if (done.Contains(id)) return false;
            if (!visiting.Add(id)) return true;

            foreach (var edge in graph.Edges.Where(e => e.Source == id && nodesById.ContainsKey(e.Target)))
                if (Visit(edge.Target)) return true;

            visiting.Remove(id);
            done.Add(id);
            return false;
        }

        return nodesById.Keys.Any(Visit);
    }

    /// <summary>
    /// Compiles a schedule trigger's settings into a 5-field cron expression. The presets and
    /// the raw-cron mode share one code path so the scheduler only ever deals with cron.
    /// </summary>
    public static string? BuildCron(PlaybookNode node)
    {
        if (node.Type != PlaybookNodeType.TriggerSchedule) return null;

        var frequency = node.GetString("frequency") ?? "weekly";
        var hour = Math.Clamp(node.GetInt("hour") ?? 9, 0, 23);
        var minute = Math.Clamp(node.GetInt("minute") ?? 0, 0, 59);
        var dayOfWeek = Math.Clamp(node.GetInt("day_of_week") ?? 1, 0, 6);

        return frequency switch
        {
            "cron" => node.GetString("cron")?.Trim(),
            "hourly" => $"{minute} * * * *",
            "daily" => $"{minute} {hour} * * *",
            _ => $"{minute} {hour} * * {dayOfWeek}"
        };
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var normalized = email.Trim();
            return new MailAddress(normalized).Address.Equals(normalized, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Run-scoped guards for mail sending: one mail per address per run, and a hard recipient
    /// cap. Also carries the short status message each node contributes to the run log.
    /// </summary>
    private sealed class SendContext
    {
        private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);
        private string? _message;

        public SendContext(bool dryRun) => DryRun = dryRun;

        public bool DryRun { get; }
        public string? LastSkipReason { get; private set; }

        public bool TryReserveRecipient(string email)
        {
            if (_reserved.Contains(email))
            {
                LastSkipReason = "Bu adrese bu çalıştırmada zaten mail hazırlandı";
                return false;
            }
            if (_reserved.Count >= MaxRecipientsPerRun)
            {
                LastSkipReason = $"Çalıştırma başına alıcı üst sınırına ({MaxRecipientsPerRun}) ulaşıldı";
                return false;
            }
            _reserved.Add(email);
            return true;
        }

        public void SetMessage(string message) => _message = message;

        public string? TakeMessage()
        {
            var value = _message;
            _message = null;
            return value;
        }
    }
}
