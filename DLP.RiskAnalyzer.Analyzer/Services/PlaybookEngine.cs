using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly IReportGeneratorService _reportGenerator;
    private readonly IInvestigationQueryRemediationSyncService _queryRemediationSync;
    private readonly IDirectorySettingsService _directorySettings;
    private readonly ILogger<PlaybookEngine> _logger;

    public PlaybookEngine(
        AnalyzerDbContext context,
        IWeeklyFlagService weeklyFlagService,
        IIncidentRepository incidentRepository,
        IEmailService emailService,
        IReportGeneratorService reportGenerator,
        IInvestigationQueryRemediationSyncService queryRemediationSync,
        IDirectorySettingsService directorySettings,
        ILogger<PlaybookEngine> logger)
    {
        _context = context;
        _weeklyFlagService = weeklyFlagService;
        _incidentRepository = incidentRepository;
        _emailService = emailService;
        _reportGenerator = reportGenerator;
        _queryRemediationSync = queryRemediationSync;
        _directorySettings = directorySettings;
        _logger = logger;
    }

    // ── Run ──────────────────────────────────────────────────────────────────

    public async Task<PlaybookRun> RunAsync(
        int playbookId,
        string triggerType,
        bool? forceDryRun,
        string? reportRecipientEmail = null,
        bool requestPdfAttachment = false,
        CancellationToken ct = default)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.FirstOrDefaultAsync(p => p.Id == playbookId, ct)
            ?? throw new KeyNotFoundException($"Agentic Workflow bulunamadı: {playbookId}");

        var alreadyRunning = await _context.PlaybookRuns
            .AnyAsync(r => r.PlaybookId == playbookId && r.Status == PlaybookRunStatus.Running, ct);
        if (alreadyRunning)
            throw new InvalidOperationException("Bu workflow için hâlâ çalışan bir akış var. Bitmesini bekleyin.");

        // Only an explicit test run is a dry run. Regular and scheduled runs may
        // send report mails while user-query mails wait for manual approval.
        var dryRun = forceDryRun == true;

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

            await ExecuteGraphAsync(graph, playbook, run, dryRun, reportRecipientEmail, requestPdfAttachment, nodeLogs, ct);

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
            run.ErrorMessage = DescribeFailure(ex);
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

    private static string DescribeFailure(Exception exception)
    {
        var messages = new List<string>();
        Exception? current = exception;

        while (current != null && messages.Count < 4)
        {
            var message = string.IsNullOrWhiteSpace(current.Message)
                ? "Açıklama bulunamadı"
                : current.Message.Trim();
            messages.Add($"{current.GetType().Name}: {message}");
            current = current.InnerException;
        }

        return string.Join(" | İç hata: ", messages.Distinct(StringComparer.Ordinal));
    }

    private Task<int> CountAsync(int runId, string status, CancellationToken ct) =>
        _context.PlaybookMailLogs.CountAsync(m => m.RunId == runId && m.Status == status, ct);

    private async Task ExecuteGraphAsync(
        PlaybookGraph graph,
        Playbook playbook,
        PlaybookRun run,
        bool dryRun,
        string? reportRecipientEmail,
        bool requestPdfAttachment,
        List<PlaybookNodeLog> nodeLogs,
        CancellationToken ct)
    {
        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var trigger = graph.Nodes.First(n => PlaybookNodeType.IsTrigger(n.Type));

        var order = TopologicalOrderFrom(trigger.Id, graph, nodesById);

        // nodeId → output handle → payload
        var outputs = new Dictionary<string, Dictionary<string, PlaybookPayload>>(StringComparer.Ordinal);

        // Run-wide guards shared by every send-mail node.
        var context = new SendContext(dryRun, playbook.AutoSend, reportRecipientEmail, requestPdfAttachment);

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
                log.Message = DescribeFailure(ex);
                log.DurationMs = sw.ElapsedMilliseconds;
                nodeLogs.Add(log);
                await PersistRunProgressAsync(run, nodeLogs, ct);
                throw new InvalidOperationException($"'{log.Label}' adımı başarısız: {DescribeFailure(ex)}", ex);
            }

            log.DurationMs = sw.ElapsedMilliseconds;
            nodeLogs.Add(log);
            await PersistRunProgressAsync(run, nodeLogs, ct);
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

    private async Task PersistRunProgressAsync(
        PlaybookRun run,
        List<PlaybookNodeLog> nodeLogs,
        CancellationToken ct)
    {
        run.NodeLogJson = PlaybookJson.Serialize(nodeLogs);
        await _context.SaveChangesAsync(ct);
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

            case PlaybookNodeType.SourceIncidentUsers:
                return SingleOutput(PlaybookPayload.OfItems(await LoadIncidentUsersAsync(node, context, ct)));

            case PlaybookNodeType.SourceHighRiskUsers:
                return SingleOutput(PlaybookPayload.OfItems(await LoadHighRiskUsersAsync(node, context, ct)));

            case PlaybookNodeType.SourceTopActionUsers:
                return SingleOutput(PlaybookPayload.OfItems(await LoadTopActionUsersAsync(node, context, ct)));

            case PlaybookNodeType.SourceHighMaxMatchTransfers:
                return SingleOutput(PlaybookPayload.OfItems(await LoadHighMaxMatchTransfersAsync(node, context, ct)));

            case PlaybookNodeType.SourcePendingQueryReminders:
                return SingleOutput(PlaybookPayload.OfItems(await LoadPendingQueryRemindersAsync(context, ct)));

            case PlaybookNodeType.SourceQueryTracking:
                return SingleOutput(PlaybookPayload.OfItems(await LoadQueryTrackingAsync(node, context, ct)));

            case PlaybookNodeType.TransformFilter:
                return SingleOutput(PlaybookPayload.OfItems(ApplyFilter(node, input.Items, context)));

            case PlaybookNodeType.LogicCondition:
                return ApplyCondition(node, input.Items, context);

            case PlaybookNodeType.LogicMetricThreshold:
                return ApplyMetricThreshold(node, input, context);

            case PlaybookNodeType.ActionSendMail:
                return SingleOutput(await SendMailsAsync(node, input, playbook, run, context, ct));

            case PlaybookNodeType.ActionSendReportMail:
                return SingleOutput(await SendReportMailAsync(node, input, playbook, run, context, ct));

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

    private async Task<List<PlaybookItem>> LoadHighRiskUsersAsync(PlaybookNode node, SendContext context, CancellationToken ct)
    {
        var days = Math.Max(1, node.GetInt("days") ?? 7);
        var minRiskScore = Math.Clamp(node.GetInt("min_risk_score") ?? 80, 0, 100);
        var topLimit = Math.Clamp(node.GetInt("top_limit") ?? 25, 1, 200);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var scores = await _context.UserDailyRiskScores
            .AsNoTracking()
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .ToListAsync(ct);

        var rows = scores
            .GroupBy(s => s.UserEmail, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var list = g.ToList();
                var maxScore = list.Max(s => s.DailyRiskScore);
                var first = list.Min(s => s.Date).ToDateTime(TimeOnly.MinValue);
                var last = list.Max(s => s.Date).ToDateTime(TimeOnly.MinValue);
                var best = list.OrderByDescending(s => s.DailyRiskScore).First();
                var incidentCount = list.Sum(s => s.IncidentCount);
                var blockCount = list.Sum(s => s.BlockCount);
                var permitCount = list.Sum(s => s.PermitCount);
                var maxMatches = list.Max(s => s.MaxMaxMatches);

                return new
                {
                    UserEmail = g.Key,
                    FullName = (string?)null,
                    Team = best.Team,
                    ContactEmail = string.IsNullOrWhiteSpace(best.EmailAddress) ? g.Key : best.EmailAddress!,
                    MaxScore = maxScore,
                    IncidentCount = incidentCount,
                    BlockCount = blockCount,
                    PermitCount = permitCount,
                    MaxMatches = maxMatches,
                    FirstSeen = first,
                    LastSeen = last
                };
            })
            .Where(x => x.MaxScore >= minRiskScore)
            .OrderByDescending(x => x.MaxScore)
            .ThenByDescending(x => x.IncidentCount)
            .Take(topLimit)
            .ToList();

        var items = rows.Select(x => new PlaybookItem(
            new WeeklyFlagUserDto(
                x.UserEmail,
                x.FullName,
                x.Team,
                x.ContactEmail,
                (int)Math.Round(x.MaxScore),
                x.FirstSeen,
                x.LastSeen,
                new List<WeeklyFlagIncidentDto>
                {
                    new(
                        x.LastSeen,
                        $"Maksimum risk: {x.MaxScore:0.#} | Olay kaydı: {x.IncidentCount} | Block: {x.BlockCount} | Permit: {x.PermitCount}",
                        x.MaxMatches,
                        null,
                        "Risk")
                }),
            PlaybookNodeType.SourceHighRiskUsers)).ToList();
        items = await EnrichItemsAsync(items, ct);

        context.SetMessage($"Son {days} günde risk skoru {minRiskScore}+ olan {items.Count} kullanıcı listelendi");
        return items;
    }

    private async Task<List<PlaybookItem>> LoadTopActionUsersAsync(PlaybookNode node, SendContext context, CancellationToken ct)
    {
        var days = Math.Max(1, node.GetInt("days") ?? 7);
        var topLimit = Math.Clamp(node.GetInt("top_limit") ?? 25, 1, 200);
        var actionKind = (node.GetString("action_kind") ?? "permit").Trim().ToLowerInvariant();
        var end = DateTime.UtcNow;
        var start = end.Date.AddDays(-days);

        var incidents = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end && i.Action != null)
            .OrderByDescending(i => i.Timestamp)
            .Take(MetricRowCap)
            .ToListAsync(ct);

        var filtered = incidents
            .Where(i => actionKind == "block" ? IsBlockAction(i.Action) : IsPermitAction(i.Action))
            .ToList();

        var items = filtered
            .GroupBy(i => i.UserEmail, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var list = g.OrderByDescending(i => i.Timestamp).ToList();
                var top5Policies = list
                    .Select(i => ViolationTriggerParser.ExtractMaxMatchPolicyAndRule(i.ViolationTriggers))
                    .Where(p => p.PolicyName != null)
                    .GroupBy(p => $"{p.PolicyName} / {p.RuleName}")
                    .OrderByDescending(grp => grp.Count())
                    .Take(5)
                    .Select(grp => $"{grp.Key} ({grp.Count()})")
                    .ToList();
                
                var top5Str = top5Policies.Count > 0 ? string.Join(", ", top5Policies) : "-";
                var best = list.First();
                
                var topSamples = new List<WeeklyFlagIncidentDto> 
                {
                    new(
                        best.Timestamp,
                        top5Str,
                        EffectiveMaxMatches(best),
                        best.Destination,
                        best.Channel,
                        best.RiskScore,
                        FirstNonEmpty(best.Action, best.RemediationAction),
                        best.DataType,
                        best.Severity
                    )
                };

                return new PlaybookItem(
                    new WeeklyFlagUserDto(
                        g.Key,
                        null,
                        FirstNonEmpty(list.Select(i => i.Team ?? i.Department)),
                        ResolveContactEmail(best),
                        list.Count,
                        list.Min(i => i.Timestamp),
                        list.Max(i => i.Timestamp),
                        topSamples),
                    actionKind == "block" ? "top_block_users" : "top_permit_users");
            })
            .OrderByDescending(i => i.User.TriggerCount)
            .ThenByDescending(i => i.User.SampleIncidents.Count > 0 ? i.User.SampleIncidents.Max(s => s.MaxMatches) : 0)
            .Take(topLimit)
            .ToList();
        items = await EnrichItemsAsync(items, ct);

        context.SetMessage($"Son {days} günde en çok {actionKind} incident üreten {items.Count} kullanıcı listelendi");
        return items;
    }

    /// <summary>
    /// Configurable incident aggregation source. Report definitions belong to the playbook graph:
    /// action, thresholds and ordering are node settings rather than named backend reports.
    /// </summary>
    private async Task<List<PlaybookItem>> LoadIncidentUsersAsync(PlaybookNode node, SendContext context, CancellationToken ct)
    {
        var days = Math.Max(1, node.GetInt("days") ?? 7);
        var topLimit = Math.Clamp(node.GetInt("top_limit") ?? 25, 1, 200);
        var minRiskScore = node.GetInt("min_risk_score");
        var minMatches = node.GetInt("min_matches");
        var actions = node.GetStringList("actions");
        var channels = node.GetStringList("channels");
        var dataTypes = node.GetStringList("data_types");
        var severities = node.GetStringList("severities");
        var minSeverity = node.GetInt("min_severity");
        var destinationContains = node.GetString("destination_contains")?.Trim();
        var policyContains = node.GetString("policy_contains")?.Trim();
        var teamContains = node.GetString("team_contains")?.Trim();
        var summarizeTopPolicies = actions.Any(a =>
            a.Equals("permit", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("block", StringComparison.OrdinalIgnoreCase));
        var sortBy = node.GetString("sort_by") ?? "incident_count";
        var sortDirection = node.GetString("sort_direction") ?? "desc";
        var end = DateTime.UtcNow;
        var start = end.Date.AddDays(-days);

        var incidents = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end)
            .OrderByDescending(i => i.Timestamp)
            .Take(MetricRowCap)
            .ToListAsync(ct);

        var filtered = incidents.Where(i =>
            (actions.Count == 0 || actions.Any(a => ActionMatches(i.Action, a))) &&
            MatchesAny(channels, i.Channel) &&
            MatchesAny(dataTypes, i.DataType) &&
            (severities.Count == 0 || severities.Contains(i.Severity.ToString())) &&
            (!minSeverity.HasValue || i.Severity >= minSeverity.Value) &&
            (!minRiskScore.HasValue || (i.RiskScore ?? 0) >= minRiskScore.Value) &&
            (!minMatches.HasValue || EffectiveMaxMatches(i) >= minMatches.Value) &&
            Contains(i.Destination, destinationContains) &&
            (Contains(i.Policy, policyContains) || Contains(i.RuleName, policyContains)) &&
            Contains(i.Team ?? i.Department, teamContains)).ToList();

        var items = filtered
            .Where(i => !string.IsNullOrWhiteSpace(i.UserEmail) && !i.UserEmail.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            .GroupBy(i => i.UserEmail, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var list = g.OrderByDescending(i => i.Timestamp).ToList();
                var best = list.OrderByDescending(EffectiveMaxMatches).ThenByDescending(i => i.Timestamp).First();
                var sampleIncidents = sortBy == "max_risk_score"
                    ? list.OrderByDescending(i => i.RiskScore ?? 0).ThenByDescending(i => i.Timestamp).Take(3)
                    : list.OrderByDescending(EffectiveMaxMatches).ThenByDescending(i => i.Timestamp).Take(3);
                var samples = sampleIncidents.Select(ToWorkflowReportIncident).ToList();

                if (summarizeTopPolicies && samples.Count > 0)
                {
                    var policySummary = list
                        .Select(PolicyRuleLabel)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(group => group.Count())
                        .ThenBy(group => group.Key)
                        .Take(5)
                        .Select(group => $"{group.Key} ({group.Count()} olay kaydı)");

                    samples[0] = samples[0] with { Policy = string.Join(", ", policySummary) };
                }
                return new PlaybookItem(
                    new WeeklyFlagUserDto(
                        g.Key,
                        null,
                        FirstNonEmpty(list.Select(i => i.Team ?? i.Department)),
                        ResolveContactEmail(best),
                        list.Count,
                        list.Min(i => i.Timestamp),
                        list.Max(i => i.Timestamp),
                        samples),
                    PlaybookNodeType.SourceIncidentUsers);
            })
            .ToList();

        items = sortBy switch
        {
            "max_risk_score" => sortDirection == "asc"
                ? items.OrderBy(i => MaxRiskScoreOf(i.User)).ToList()
                : items.OrderByDescending(i => MaxRiskScoreOf(i.User)).ToList(),
            "max_matches" => sortDirection == "asc"
                ? items.OrderBy(i => MaxMatchesOf(i.User)).ToList()
                : items.OrderByDescending(i => MaxMatchesOf(i.User)).ToList(),
            "last_seen" => sortDirection == "asc"
                ? items.OrderBy(i => i.User.LastSeen).ToList()
                : items.OrderByDescending(i => i.User.LastSeen).ToList(),
            _ => sortDirection == "asc"
                ? items.OrderBy(i => i.User.TriggerCount).ToList()
                : items.OrderByDescending(i => i.User.TriggerCount).ToList()
        };

        items = await EnrichItemsAsync(items.Take(topLimit).ToList(), ct);
        context.SetMessage($"Son {days} günde {filtered.Count} olay kaydından {items.Count} kullanıcı listelendi");
        return items;
    }

    private async Task<List<PlaybookItem>> LoadHighMaxMatchTransfersAsync(PlaybookNode node, SendContext context, CancellationToken ct)
    {
        var days = Math.Max(1, node.GetInt("days") ?? 7);
        var threshold = Math.Max(1, node.GetInt("min_matches") ?? 300);
        var topLimit = Math.Clamp(node.GetInt("top_limit") ?? 25, 1, 200);
        var end = DateTime.UtcNow;
        var start = end.Date.AddDays(-days);

        var incidents = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end)
            .OrderByDescending(i => i.Timestamp)
            .Take(MetricRowCap)
            .ToListAsync(ct);

        var items = incidents
            .Where(i => EffectiveMaxMatches(i) >= threshold)
            .OrderByDescending(EffectiveMaxMatches)
            .ThenByDescending(i => i.Timestamp)
            .GroupBy(i => i.UserEmail, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var list = g.OrderByDescending(EffectiveMaxMatches).ThenByDescending(i => i.Timestamp).ToList();
                var best = list.First();
                return new PlaybookItem(
                    new WeeklyFlagUserDto(
                        g.Key,
                        null,
                        FirstNonEmpty(list.Select(i => i.Team ?? i.Department)),
                        ResolveContactEmail(best),
                        EffectiveMaxMatches(best),
                        list.Min(i => i.Timestamp),
                        list.Max(i => i.Timestamp),
                        list.Take(3).Select(ToWorkflowReportIncident).ToList()),
                    PlaybookNodeType.SourceHighMaxMatchTransfers);
            })
            .OrderByDescending(i => i.User.TriggerCount)
            .Take(topLimit)
            .ToList();
        items = await EnrichItemsAsync(items, ct);

        context.SetMessage($"Maksimum eşleşme {threshold}+ olan tekil gönderimlerden {items.Count} kullanıcı listelendi");
        return items;
    }

    private async Task<List<PlaybookItem>> LoadPendingQueryRemindersAsync(SendContext context, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var due = await _context.InvestigationQueries
            .Where(q => q.QueryStatus == InvestigationQueryStatus.Queried &&
                        q.ReplyReceivedAt == null &&
                        q.ReminderCount == 0 &&
                        (q.FirstSentAt ?? q.QueryDate) != null &&
                        (q.FirstSentAt ?? q.QueryDate) <= cutoff &&
                        q.MailAddress.Contains("@"))
            .OrderBy(q => q.FirstSentAt ?? q.QueryDate)
            .Take(MaxRecipientsPerRun)
            .ToListAsync(ct);

        // Older/manual query rows predate correlation codes. Stamp one before preparing the
        // reminder so the sent mail updates this same row rather than creating a second query.
        var changedCorrelations = false;
        foreach (var query in due.Where(q => string.IsNullOrWhiteSpace(q.CorrelationCode)))
        {
            query.CorrelationCode = NewCorrelationCode();
            changedCorrelations = true;
        }
        if (changedCorrelations) await _context.SaveChangesAsync(ct);

        var items = due.Select(q => new PlaybookItem(
            new WeeklyFlagUserDto(
                q.UserCode,
                q.FullName,
                q.Team,
                q.MailAddress,
                1,
                q.QueryDate ?? q.FirstSentAt ?? q.CreatedAt,
                q.QueryDate ?? q.FirstSentAt ?? q.CreatedAt,
                new List<WeeklyFlagIncidentDto>()),
            PlaybookNodeType.SourcePendingQueryReminders,
            q.Id,
            q.CorrelationCode,
            q)).ToList();

        context.SetMessage($"Ilk gonderim veya sorgu tarihinden en az 7 gun sonra cevap gelmeyen {items.Count} sorgu listelendi");
        return items;
    }

    private async Task<List<PlaybookItem>> LoadQueryTrackingAsync(PlaybookNode node, SendContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var periodMode = node.GetString("period_mode") ?? "this_week";
        var dateBasis = node.GetString("date_basis") ?? "first_sent";
        var (from, to) = ResolveTrackingWindow(node, periodMode, now);
        var requestedStatuses = node.GetStringList("statuses");
        if (requestedStatuses.Count == 0) requestedStatuses = QueryTrackingStatuses.All.ToList();

        var records = await _context.InvestigationQueries
            .OrderByDescending(query => query.UpdatedAt)
            .Take(2_000)
            .ToListAsync(ct);

        var correlationCodes = records
            .Where(query => !string.IsNullOrWhiteSpace(query.CorrelationCode))
            .Select(query => query.CorrelationCode!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var legacyMailIds = records.Where(query => query.PlaybookMailLogId.HasValue).Select(query => query.PlaybookMailLogId!.Value).ToList();
        var logs = await _context.PlaybookMailLogs
            .Where(mail => (correlationCodes.Contains(mail.CorrelationCode!) || legacyMailIds.Contains(mail.Id)) &&
                           !string.Equals(mail.UserEmail, "(rapor)", StringComparison.OrdinalIgnoreCase))
            .OrderBy(mail => mail.CreatedAt)
            .ToListAsync(ct);

        var items = new List<PlaybookItem>();
        foreach (var query in records)
        {
            var related = logs.Where(mail =>
                (!string.IsNullOrWhiteSpace(query.CorrelationCode) && mail.CorrelationCode == query.CorrelationCode) ||
                (query.PlaybookMailLogId.HasValue && mail.Id == query.PlaybookMailLogId.Value)).ToList();
            var firstMail = related.FirstOrDefault(mail => mail.SourceCriterion != PlaybookNodeType.SourcePendingQueryReminders);
            var reminderMail = related.LastOrDefault(mail => mail.SourceCriterion == PlaybookNodeType.SourcePendingQueryReminders);
            var lifecycle = GetTrackingStatus(query, reminderMail, now);
            if (!requestedStatuses.Contains(lifecycle, StringComparer.Ordinal)) continue;

            var trackingDate = dateBasis switch
            {
                "prepared" => firstMail?.CreatedAt ?? query.CreatedAt,
                "reminder" => reminderMail?.SentAt ?? reminderMail?.CreatedAt ?? query.ReminderSentAt,
                "updated" => query.UpdatedAt,
                _ => firstMail?.SentAt ?? query.FirstSentAt ?? query.QueryDate ?? query.CreatedAt
            };
            if (trackingDate < from || trackingDate > to) continue;

            var firstMailStatus = firstMail == null ? "Kayit yok" : MailStatusLabel(firstMail.Status);
            var reminderStatus = GetReminderStatus(query, reminderMail, now);
            var subject = firstMail?.Subject ?? query.Subject;
            items.Add(new PlaybookItem(
                new WeeklyFlagUserDto(query.UserCode, query.FullName, query.Team, query.MailAddress, 1,
                    query.QueryDate ?? query.FirstSentAt ?? query.CreatedAt,
                    query.UpdatedAt,
                    new List<WeeklyFlagIncidentDto>()),
                PlaybookNodeType.SourceQueryTracking,
                query.Id,
                query.CorrelationCode,
                query,
                new QueryTrackingDetails(lifecycle, firstMailStatus, firstMail?.SentAt ?? firstMail?.CreatedAt,
                    reminderStatus, query.ReplyReceivedAt, subject)));
        }

        var result = items.Take(MaxRecipientsPerRun).ToList();
        context.SetMessage($"Sorgu ve hatirlatma takibinde {result.Count} kayit listelendi");
        return result;
    }

    private static (DateTime From, DateTime To) ResolveTrackingWindow(PlaybookNode node, string mode, DateTime nowUtc)
    {
        var turkeyNow = RadarTimeZone.ToTurkeyTime(nowUtc);
        DateTime localFrom;
        DateTime localTo;
        if (mode == "custom" && DateTime.TryParse(node.GetString("from_date"), out var customFrom) &&
            DateTime.TryParse(node.GetString("to_date"), out var customTo))
        {
            localFrom = customFrom.Date;
            localTo = customTo.Date.AddDays(1).AddTicks(-1);
        }
        else if (mode == "last_n_days")
        {
            var days = Math.Clamp(node.GetInt("days") ?? 7, 1, 365);
            localFrom = turkeyNow.Date.AddDays(-(days - 1));
            localTo = turkeyNow;
        }
        else if (mode == "last_7_days")
        {
            localFrom = turkeyNow.Date.AddDays(-6);
            localTo = turkeyNow;
        }
        else
        {
            var mondayOffset = ((int)turkeyNow.DayOfWeek + 6) % 7;
            localFrom = turkeyNow.Date.AddDays(-mondayOffset);
            localTo = turkeyNow;
        }

        return (TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localFrom, DateTimeKind.Unspecified), RadarTimeZone.Turkey),
                TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTo, DateTimeKind.Unspecified), RadarTimeZone.Turkey));
    }

    private static string GetTrackingStatus(InvestigationQueryRecord query, PlaybookMailLog? reminderMail, DateTime now)
    {
        if (query.QueryStatus == InvestigationQueryStatus.Completed) return QueryTrackingStatuses.Completed;
        if (query.QueryStatus == InvestigationQueryStatus.ReplyReview) return QueryTrackingStatuses.ReplyReview;
        if (query.QueryStatus == InvestigationQueryStatus.ReminderUnanswered) return QueryTrackingStatuses.ReminderUnanswered;
        if (reminderMail?.Status == PlaybookMailStatus.Pending) return QueryTrackingStatuses.ReminderPending;
        if (query.ReminderCount > 0) return QueryTrackingStatuses.ReminderSent;
        if (query.QueryStatus == InvestigationQueryStatus.Pending || query.FirstSentAt == null) return QueryTrackingStatuses.QueryPending;
        var firstSent = query.FirstSentAt ?? query.QueryDate ?? query.CreatedAt;
        return firstSent <= now.AddDays(-7) ? QueryTrackingStatuses.ReminderDue : QueryTrackingStatuses.AwaitingReply;
    }

    private static string GetReminderStatus(InvestigationQueryRecord query, PlaybookMailLog? reminderMail, DateTime now)
    {
        var lifecycle = GetTrackingStatus(query, reminderMail, now);
        return lifecycle switch
        {
            QueryTrackingStatuses.ReminderPending => "Hazirlandi - onay bekliyor",
            QueryTrackingStatuses.ReminderSent => "Gonderildi",
            QueryTrackingStatuses.ReminderUnanswered => "Yanitsiz",
            QueryTrackingStatuses.ReminderDue => "Hatirlatmaya uygun",
            _ => "-"
        };
    }

    private static string MailStatusLabel(string status) => status switch
    {
        PlaybookMailStatus.Pending => "Hazirlandi - onay bekliyor",
        PlaybookMailStatus.Sent => "Gonderildi",
        PlaybookMailStatus.Failed => "Gonderilemedi",
        PlaybookMailStatus.Skipped => "Atlandi",
        _ => status
    };

    private async Task<List<PlaybookItem>> EnrichItemsAsync(List<PlaybookItem> items, CancellationToken ct)
    {
        var enriched = new List<PlaybookItem>(items.Count);
        foreach (var item in items)
        {
            var profile = await _directorySettings.LookupLdapUserAsync(item.User.UserEmail, ct);
            if (!profile.Success)
            {
                enriched.Add(item);
                continue;
            }

            enriched.Add(item with
            {
                User = item.User with
                {
                    FullName = FirstNonEmpty(profile.FullName, item.User.FullName),
                    Team = FirstNonEmpty(profile.Department, item.User.Team),
                    ContactEmail = FirstNonEmpty(profile.Email, item.User.ContactEmail, item.User.UserEmail) ?? item.User.ContactEmail,
                    Gender = FirstNonEmpty(profile.Gender, item.User.Gender)
                }
            });
        }

        return enriched;
    }

    private static bool IsPermitAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;
        var value = action.Trim().ToUpperInvariant();
        return value.Contains("PERMIT") || value.Contains("ALLOW") || value.Contains("AUTHORIZE") || value.Contains("AUTHORIZED");
    }

    private static bool IsBlockAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;
        return action.Trim().ToUpperInvariant().Contains("BLOCK");
    }

    private static bool ActionMatches(string? action, string requested) => requested.Trim().ToLowerInvariant() switch
    {
        "permit" => IsPermitAction(action),
        "block" => IsBlockAction(action),
        _ => !string.IsNullOrWhiteSpace(action) && action.Equals(requested, StringComparison.OrdinalIgnoreCase)
    };

    private static int MaxMatchesOf(WeeklyFlagUserDto user) => user.SampleIncidents?.Count > 0
        ? user.SampleIncidents.Max(i => i.MaxMatches)
        : 0;

    private static int MaxRiskScoreOf(WeeklyFlagUserDto user) => user.SampleIncidents?.Count > 0
        ? user.SampleIncidents.Max(i => i.RiskScore ?? 0)
        : 0;

    private static string ResolveContactEmail(Incident incident)
    {
        if (!string.IsNullOrWhiteSpace(incident.EmailAddress)) return incident.EmailAddress.Trim();
        if (!string.IsNullOrWhiteSpace(incident.UserEmail)) return incident.UserEmail.Trim();
        if (!string.IsNullOrWhiteSpace(incident.LoginName)) return incident.LoginName.Trim();
        return "unknown";
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static WeeklyFlagIncidentDto ToWeeklyFlagIncident(Incident incident)
    {
        var maxMatchInfo = ViolationTriggerParser.ExtractMaxMatchPolicyAndRule(incident.ViolationTriggers);
        var policyStr = maxMatchInfo.PolicyName != null 
            ? $"{maxMatchInfo.PolicyName} / {maxMatchInfo.RuleName}"
            : (string.IsNullOrWhiteSpace(incident.Policy)
                ? incident.RuleName
                : string.IsNullOrWhiteSpace(incident.RuleName)
                    ? incident.Policy
                    : $"{incident.Policy} / {incident.RuleName}");

        return new(
            incident.Timestamp,
            policyStr,
            EffectiveMaxMatches(incident),
            incident.Destination,
            string.IsNullOrWhiteSpace(incident.Action)
                ? incident.Channel
                : $"{incident.Channel ?? "-"} · {incident.Action}");
    }

    private static WeeklyFlagIncidentDto ToWorkflowReportIncident(Incident incident) => new(
        incident.Timestamp,
        FirstNonEmpty(incident.Policy, incident.RuleName),
        EffectiveMaxMatches(incident),
        incident.Destination,
        incident.Channel,
        incident.RiskScore,
        FirstNonEmpty(incident.Action, incident.RemediationAction),
        incident.DataType,
        incident.Severity);

    private static string? PolicyRuleLabel(Incident incident)
    {
        var parsed = ViolationTriggerParser.ExtractMaxMatchPolicyAndRule(incident.ViolationTriggers);
        if (!string.IsNullOrWhiteSpace(parsed.PolicyName))
            return string.IsNullOrWhiteSpace(parsed.RuleName) ? parsed.PolicyName : $"{parsed.PolicyName} / {parsed.RuleName}";

        if (!string.IsNullOrWhiteSpace(incident.Policy))
            return string.IsNullOrWhiteSpace(incident.RuleName) ? incident.Policy : $"{incident.Policy} / {incident.RuleName}";

        return incident.RuleName;
    }

    private static int EffectiveMaxMatches(Incident incident)
    {
        if (incident.MaxMatches > 0) return incident.MaxMatches;
        return ViolationTriggerParser.ExtractMaxMatches(incident.ViolationTriggers);
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
            (!minMatches.HasValue || EffectiveMaxMatches(i) >= minMatches.Value) &&
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
        var templateCatalog = await _context.MailTemplates.AsNoTracking().ToListAsync(ct);
        var defaultTemplate = ResolveNodeTemplate(node, templateCatalog);
        var templateRules = ResolveTemplateMatchRules(node, templateCatalog);

        var recipientMode = node.GetString("recipient_mode") ?? "user";
        var fixedRecipient = context.ReportRecipientEmail ?? node.GetString("fixed_recipient")?.Trim();
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
            var metricTemplate = RequireTemplate(defaultTemplate, "Metrik maili icin bir sabit sablon veya konu gerekir.");
            await SendMetricMailAsync(
                node, payload.Metric!, metricTemplate.Subject, metricTemplate.Body,
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
            var decision = ResolveTemplateForUser(node, item, defaultTemplate, templateCatalog, templateRules);
            var renderUser = WithPrimaryIncident(user, decision.Incident);
            var correlationCode = item.ExistingCorrelationCode ?? NewCorrelationCode();

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
                Subject = PlaybookMailRenderer.ApplyPlaceholders(decision.Template.Subject, renderUser, now),
                BodyHtml = PlaybookMailRenderer.ToEmailHtml(
                    PlaybookMailRenderer.ApplyPlaceholders(decision.Template.Body, renderUser, now)),
                TemplateId = decision.TemplateId,
                TemplateName = decision.TemplateName,
                TemplateMatchReason = decision.MatchReason,
                IncidentSummaryJson = BuildIncidentSummaryJson(decision.Incident),
                CorrelationCode = correlationCode,
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
            else if (context.DryRun || !context.AutoSend)
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
        await SyncQueryRecordsSafelyAsync(queryLogEntries, ct);

        var verb = (context.DryRun || !context.AutoSend) ? "onay için hazırlandı" : "gönderildi";
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

        context.SetMessage(entry.Status switch
        {
            PlaybookMailStatus.Pending => $"Özet mail onay için hazırlandı → {fixedRecipient}",
            PlaybookMailStatus.Sent => $"Özet mail gönderildi → {fixedRecipient}",
            PlaybookMailStatus.Skipped => entry.ErrorMessage ?? "Özet mail atlandı",
            _ => "Özet mail gönderilemedi"
        });
    }

    private async Task<PlaybookPayload> SendReportMailAsync(
        PlaybookNode node,
        PlaybookPayload payload,
        Playbook playbook,
        PlaybookRun run,
        SendContext context,
        CancellationToken ct)
    {
        var fixedRecipient = context.ReportRecipientEmail ?? node.GetString("fixed_recipient")?.Trim();
        if (string.IsNullOrWhiteSpace(fixedRecipient))
            fixedRecipient = await ResolveAdminEmailAsync(ct);

        if (string.IsNullOrWhiteSpace(fixedRecipient))
            throw new InvalidOperationException("Rapor maili için alıcı bulunamadı. Node üzerinde alıcı girin veya Ayarlar > Yönetici E-postası alanını doldurun.");
        if (fixedRecipient.Contains('@') && !IsValidEmail(fixedRecipient))
            throw new InvalidOperationException("Rapor alıcısı geçerli değil.");

        var ccEmail = node.GetString("cc_email")?.Trim();
        if (string.IsNullOrWhiteSpace(ccEmail)) ccEmail = null;
        if (!string.IsNullOrWhiteSpace(ccEmail) && !IsValidEmail(ccEmail))
            throw new InvalidOperationException("Rapor CC adresi geçerli değil.");

        var now = RadarTimeZone.NowTurkey();
        var title = node.GetString("title")?.Trim();
        if (string.IsNullOrWhiteSpace(title)) title = "Agentic Workflow Raporu";
        title = ApplyReportPlaceholders(title, payload, now);

        var subject = node.GetString("subject_override")?.Trim();
        if (string.IsNullOrWhiteSpace(subject)) subject = $"{title} - {now:dd.MM.yyyy}";
        subject = ApplyReportPlaceholders(subject, payload, now);

        var intro = ApplyReportPlaceholders(node.GetString("intro"), payload, now);
        var selectedColumns = node.GetStringList("columns");
        var bodyHtml = BuildConfiguredReportMailHtml(title, intro, payload, now, selectedColumns);

        var entry = new PlaybookMailLog
        {
            RunId = run.Id,
            PlaybookId = playbook.Id,
            NodeId = node.Id,
            UserEmail = "(rapor)",
            FullName = title,
            Team = null,
            ToEmail = fixedRecipient!,
            CcEmail = ccEmail,
            Subject = subject,
            BodyHtml = bodyHtml,
            SourceCriterion = payload.HasMetric
                ? PlaybookNodeType.SourceIncidentMetric
                : payload.Items.FirstOrDefault()?.SourceCriterion ?? node.Type,
            TriggerCount = payload.HasMetric ? (int)Math.Round(payload.Metric!.Value) : payload.Items.Count,
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
            entry.ErrorMessage = "Aynı alıcıya aynı rapor son 30 dakika içinde hazırlanmış veya gönderilmiş.";
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
            var attachPdf = node.GetBool("attach_pdf") || context.RequestPdfAttachment;
            var success = attachPdf
                ? await _emailService.SendEmailWithAttachmentsAsync(
                    toEmail: fixedRecipient!,
                    subject: entry.Subject,
                    body: entry.BodyHtml,
                    attachments:
                    [
                        new EmailAttachment(
                            $"workflow_raporu_{now:yyyyMMdd_HHmm}.pdf",
                            _reportGenerator.GenerateWorkflowTableReport(BuildWorkflowTableReport(title, intro, payload, now, selectedColumns)),
                            "application/pdf")
                    ],
                    isHtml: true,
                    toName: null,
                    ccEmail: ccEmail)
                : await _emailService.SendEmailAsync(
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

        context.SetMessage(entry.Status switch
        {
            PlaybookMailStatus.Pending => $"Rapor maili onay için hazırlandı -> {fixedRecipient}",
            PlaybookMailStatus.Sent => $"Rapor maili gönderildi -> {fixedRecipient}",
            PlaybookMailStatus.Skipped => entry.ErrorMessage ?? "Rapor maili atlandı",
            _ => "Rapor maili gönderilemedi"
        });

        return payload;
    }

    private async Task<string?> ResolveAdminEmailAsync(CancellationToken ct)
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "admin_email", ct);

        return setting?.Value;
    }

    /// <summary>
    /// Mail delivery is the primary action. A later failure while mirroring that result into the
    /// investigation-query view must not turn an already-sent mail into a failed workflow run.
    /// </summary>
    private async Task SyncQueryRecordsSafelyAsync(IEnumerable<PlaybookMailLog> mailLogs, CancellationToken ct)
    {
        // Report and metric mail are workflow artefacts, not user investigation queries.
        var entries = mailLogs.Where(IsInvestigationQueryMail).ToList();
        if (entries.Count == 0) return;

        try
        {
            await UpsertQueryRecordsForMailLogsAsync(entries, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Playbook mail logs were saved but investigation query synchronization failed for {Count} entries",
                entries.Count);
        }
    }

    private static bool IsInvestigationQueryMail(PlaybookMailLog mail) =>
        !string.Equals(mail.UserEmail, "(rapor)", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(mail.UserEmail, MetricSubjectLabel, StringComparison.OrdinalIgnoreCase);

    private static string BuildReportMailHtml(string title, string? intro, PlaybookPayload payload, DateTime now)
    {
        var introHtml = string.IsNullOrWhiteSpace(intro)
            ? string.Empty
            : $@"<div class=""intro"">{Encode(intro)}</div>";

        if (payload.HasMetric)
        {
            var metric = payload.Metric!;
            var breakdownRows = metric.Breakdown.Count == 0
                ? "<tr><td colspan=\"2\" class=\"empty\">Kırılım yok.</td></tr>"
                : string.Join("", metric.Breakdown.Select(b =>
                    $"<tr><td>{Encode(b.Label)}</td><td>{b.Count:N0}</td></tr>"));

            return $@"
<html>
<head>{ReportMailStyles()}</head>
<body>
  <div class=""wrap"">
    <div class=""header""><h1>{Encode(title)}</h1></div>
    <div class=""content"">
      {introHtml}
      <div class=""meta"">Üretim tarihi: {now:dd.MM.yyyy HH:mm} ({RadarTimeZone.DisplayName})</div>
      <table>
        <tbody>
          <tr><th>Metrik</th><td>{Encode(metric.Label)}</td></tr>
          <tr><th>Değer</th><td>{metric.Value:0.##}</td></tr>
          <tr><th>Dönem</th><td>{metric.WindowStart:dd.MM.yyyy} - {metric.WindowEnd:dd.MM.yyyy}</td></tr>
          <tr><th>Toplam Olay Kaydı</th><td>{metric.TotalIncidents:N0}</td></tr>
          <tr><th>Kullanıcı Sayısı</th><td>{metric.UniqueUsers:N0}</td></tr>
          <tr><th>Filtreler</th><td>{Encode(metric.FilterSummary)}</td></tr>
        </tbody>
      </table>
      <h2>Kırılım</h2>
      <table><thead><tr><th>Alan</th><th>Adet</th></tr></thead><tbody>{breakdownRows}</tbody></table>
      <div class=""footer"">Bu rapor agentic workflow tarafından servis hesabı üzerinden üretilmiştir.</div>
    </div>
  </div>
</body>
</html>";
        }

        var isTopAction = payload.Items.Any(i => i.SourceCriterion == "top_block_users" || i.SourceCriterion == "top_permit_users");

        var rows = payload.Items.Count == 0
            ? "<tr><td colspan=\"9\" class=\"empty\">Kayıt bulunamadı.</td></tr>"
            : string.Join("", payload.Items.Select((item, index) =>
            {
                var user = item.User;
                var sample = user.SampleIncidents?.OrderByDescending(i => i.MaxMatches).FirstOrDefault();
                
                var dateTd = isTopAction ? "" : $"<td>{(sample == null ? "-" : sample.Timestamp.ToString("dd.MM.yyyy HH:mm"))}</td>";
                var maxMatchTd = isTopAction ? "" : $"<td>{sample?.MaxMatches.ToString("N0") ?? "-"}</td>";

                return "<tr>" +
                       $"<td>{index + 1}</td>" +
                       $"<td>{Encode(user.FullName ?? user.UserEmail)}</td>" +
                       $"<td>{Encode(user.UserEmail)}</td>" +
                       $"<td>{Encode(user.Team ?? "-")}</td>" +
                       $"<td>{Encode(ReportCriterionLabel(item.SourceCriterion))}</td>" +
                       dateTd +
                       $"<td>{user.TriggerCount.ToString("N0")}</td>" +
                       maxMatchTd +
                       $"<td>{Encode(sample?.Policy ?? "-")}</td>" +
                       "</tr>";
            }));

        return $@"
<html>
<head>{ReportMailStyles()}</head>
<body>
  <div class=""wrap"">
    <div class=""header""><h1>{Encode(title)}</h1></div>
    <div class=""content"">
      {introHtml}
      <div class=""meta"">Üretim tarihi: {now:dd.MM.yyyy HH:mm} ({RadarTimeZone.DisplayName})<br/>Satır sayısı: {payload.Items.Count:N0}</div>
      <table>
        <thead>
          <tr>
            <th>#</th><th>Kullanıcı</th><th>Kullanıcı Adı</th><th>Ekip</th><th>Kaynak</th>
            {(isTopAction ? "" : "<th>Olay Tarihi</th>")}
            <th>Olay Kaydı Sayısı</th>
            {(isTopAction ? "" : "<th>Maksimum Eşleşme</th>")}
            <th>Örnek Politika / Kural</th>
          </tr>
        </thead>
        <tbody>{rows}</tbody>
      </table>
      <div class=""footer"">Bu rapor agentic workflow tarafından servis hesabı üzerinden üretilmiştir.</div>
    </div>
  </div>
</body>
</html>";
    }

    private static string BuildConfiguredReportMailHtml(string title, string? intro, PlaybookPayload payload, DateTime now, List<string> columns)
    {
        if (payload.HasMetric)
            return BuildReportMailHtml(title, intro, payload, now);
        if (payload.Items.Any(item => item.Tracking != null))
            return BuildQueryTrackingReportHtml(title, intro, payload, now);

        var selectedColumns = columns.Where(IsReportColumn).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selectedColumns.Count == 0)
            selectedColumns = DefaultReportColumns.ToList();

        var introHtml = string.IsNullOrWhiteSpace(intro)
            ? string.Empty
            : $@"<div class=""intro"">{Encode(intro)}</div>";
        var headerCells = string.Join("", selectedColumns.Select(column => $"<th>{ReportColumnLabel(column)}</th>"));
        var rows = payload.Items.Count == 0
            ? $"<tr><td colspan=\"{selectedColumns.Count + 1}\" class=\"empty\">Kay\u0131t bulunamad\u0131.</td></tr>"
            : string.Join("", payload.Items.Select((item, index) =>
            {
                var user = item.User;
                var sample = user.SampleIncidents?.OrderByDescending(i => i.MaxMatches).FirstOrDefault();
                var cells = string.Join("", selectedColumns.Select(column =>
                    $"<td>{ReportColumnValue(column, user, item.SourceCriterion, sample, item.InvestigationQuery)}</td>"));
                return $"<tr><td>{index + 1}</td>{cells}</tr>";
            }));

        return $@"
<html>
<head>{ReportMailStyles()}</head>
<body>
  <div class=""wrap"">
    <div class=""header""><h1>{Encode(title)}</h1></div>
    <div class=""content"">
      {introHtml}
      <div class=""meta"">Üretim tarihi: {now:dd.MM.yyyy HH:mm} ({RadarTimeZone.DisplayName})<br/>Satır sayısı: {payload.Items.Count:N0}</div>
      <table><thead><tr><th>#</th>{headerCells}</tr></thead><tbody>{rows}</tbody></table>
      <div class=""footer"">Bu rapor agentic workflow tarafından servis hesabı üzerinden üretilmiştir.</div>
    </div>
  </div>
</body>
</html>";
    }

    private static WorkflowTableReport BuildWorkflowTableReport(
        string title,
        string? intro,
        PlaybookPayload payload,
        DateTime now,
        List<string> columns)
    {
        if (payload.HasMetric)
        {
            var metric = payload.Metric!;
            return new WorkflowTableReport(
                title,
                intro,
                now,
                ["Metrik", "Değer"],
                [
                    [metric.Label, metric.Value.ToString("N0")],
                    ["Dönem", $"{RadarTimeZone.ToTurkeyTime(metric.WindowStart):dd.MM.yyyy} - {RadarTimeZone.ToTurkeyTime(metric.WindowEnd):dd.MM.yyyy}"],
                    ["Toplam olay kaydı", metric.TotalIncidents.ToString("N0")],
                    ["Kullanıcı sayısı", metric.UniqueUsers.ToString("N0")],
                    ["Filtreler", string.IsNullOrWhiteSpace(metric.FilterSummary) ? "-" : metric.FilterSummary]
                ]);
        }

        if (payload.Items.Any(item => item.Tracking != null))
        {
            var headers = new[]
            {
                "Kullanıcı adı", "Ad Soyad", "Birim", "Alıcı e-posta", "Sorgu durumu", "İlk mail durumu",
                "İlk mail tarihi", "Geçen gün", "Hatırlatma durumu", "Hatırlatma tarihi", "Adet",
                "Gelen cevap", "Cevap tarihi", "Şablon / konu", "Son işlem notu"
            };
            var rows = payload.Items.Select(item =>
            {
                var query = item.InvestigationQuery;
                var tracking = item.Tracking!;
                var firstSent = tracking.FirstMailAt ?? query?.FirstSentAt ?? query?.QueryDate;
                var days = firstSent.HasValue
                    ? Math.Max(0, (int)Math.Floor((DateTime.UtcNow - firstSent.Value.ToUniversalTime()).TotalDays))
                    : 0;
                string FormatTurkey(DateTime? value) => value.HasValue
                    ? RadarTimeZone.ToTurkeyTime(value.Value).ToString("dd.MM.yyyy HH:mm")
                    : "-";
                return (IReadOnlyList<string>)new[]
                {
                    item.User.UserEmail,
                    item.User.FullName ?? "-",
                    item.User.Team ?? "-",
                    item.User.ContactEmail ?? "-",
                    TrackingStatusLabel(tracking.LifecycleStatus),
                    tracking.FirstMailStatus,
                    FormatTurkey(firstSent),
                    days.ToString(),
                    tracking.ReminderStatus,
                    FormatTurkey(query?.ReminderSentAt),
                    (query?.ReminderCount ?? 0).ToString(),
                    query?.ResponseStatus ?? "-",
                    FormatTurkey(tracking.ReplyAt),
                    tracking.TemplateOrSubject ?? "-",
                    query?.Action ?? query?.Notes ?? "-"
                };
            }).ToList();
            return new WorkflowTableReport(title, intro, now, headers, rows);
        }

        var selectedColumns = columns.Where(IsReportColumn).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selectedColumns.Count == 0) selectedColumns = DefaultReportColumns.ToList();
        var tableRows = payload.Items.Select(item =>
        {
            var sample = item.User.SampleIncidents?.OrderByDescending(incident => incident.MaxMatches).FirstOrDefault();
            return (IReadOnlyList<string>)selectedColumns.Select(column =>
                WebUtility.HtmlDecode(ReportColumnValue(column, item.User, item.SourceCriterion, sample, item.InvestigationQuery))).ToList();
        }).ToList();
        return new WorkflowTableReport(
            title,
            intro,
            now,
            selectedColumns.Select(ReportColumnLabel).ToList(),
            tableRows);
    }

    private static string BuildQueryTrackingReportHtml(string title, string? intro, PlaybookPayload payload, DateTime now)
    {
        var introHtml = string.IsNullOrWhiteSpace(intro) ? string.Empty : $@"<div class=""intro"">{Encode(intro)}</div>";
        var rows = payload.Items.Count == 0
            ? "<tr><td colspan=\"16\" class=\"empty\">Kayit bulunamadi.</td></tr>"
            : string.Join("", payload.Items.Select((item, index) =>
            {
                var query = item.InvestigationQuery;
                var tracking = item.Tracking!;
                var firstSent = tracking.FirstMailAt ?? query?.FirstSentAt ?? query?.QueryDate;
                var days = firstSent.HasValue ? Math.Max(0, (int)Math.Floor((DateTime.UtcNow - firstSent.Value.ToUniversalTime()).TotalDays)) : 0;
                string FormatTurkey(DateTime? value) => value.HasValue ? RadarTimeZone.ToTurkeyTime(value.Value).ToString("dd.MM.yyyy HH:mm") : "-";
                return $"<tr><td>{index + 1}</td><td>{Encode(item.User.UserEmail)}</td><td>{Encode(item.User.FullName)}</td>" +
                       $"<td>{Encode(item.User.Team ?? "-")}</td><td>{Encode(item.User.ContactEmail ?? "-")}</td>" +
                       $"<td>{Encode(TrackingStatusLabel(tracking.LifecycleStatus))}</td><td>{Encode(tracking.FirstMailStatus)}</td>" +
                       $"<td>{FormatTurkey(firstSent)}</td><td>{days}</td>" +
                       $"<td>{Encode(tracking.ReminderStatus)}</td><td>{FormatTurkey(query?.ReminderSentAt)}</td>" +
                       $"<td>{query?.ReminderCount ?? 0}</td><td>{Encode(query?.ResponseStatus ?? "-")}</td>" +
                       $"<td>{FormatTurkey(tracking.ReplyAt)}</td><td>{Encode(tracking.TemplateOrSubject ?? "-")}</td>" +
                       $"<td>{Encode(query?.Action ?? query?.Notes ?? "-")}</td></tr>";
            }));

        return $@"
<html><head>{ReportMailStyles()}</head><body><div class=""wrap""><div class=""header""><h1>{Encode(title)}</h1></div>
<div class=""content"">{introHtml}<div class=""meta"">Uretim tarihi: {now:dd.MM.yyyy HH:mm} ({RadarTimeZone.DisplayName})<br/>Satir sayisi: {payload.Items.Count:N0}</div>
<table><thead><tr><th>#</th><th>Kullanici adi</th><th>Ad Soyad</th><th>Birim</th><th>Alici e-posta</th><th>Sorgu durumu</th><th>Ilk mail durumu</th><th>Ilk mail tarihi</th><th>Gecen gun</th><th>Hatirlatma durumu</th><th>Hatirlatma tarihi</th><th>Adet</th><th>Gelen cevap</th><th>Cevap tarihi</th><th>Sablon / konu</th><th>Son islem notu</th></tr></thead><tbody>{rows}</tbody></table>
<div class=""footer"">Bu rapor agentic workflow tarafindan servis hesabi uzerinden uretilmistir.</div></div></div></body></html>";
    }

    private static string TrackingStatusLabel(string status) => status switch
    {
        QueryTrackingStatuses.QueryPending => "Sorgulanmayi bekliyor",
        QueryTrackingStatuses.AwaitingReply => "Cevap bekliyor",
        QueryTrackingStatuses.ReminderDue => "Hatirlatmaya uygun",
        QueryTrackingStatuses.ReminderPending => "Hatirlatma onay bekliyor",
        QueryTrackingStatuses.ReminderSent => "Hatirlatma gonderildi",
        QueryTrackingStatuses.ReplyReview => "Cevap geldi - inceleme bekliyor",
        QueryTrackingStatuses.Completed => "Tamamlandi",
        QueryTrackingStatuses.ReminderUnanswered => "Ilk hatirlatmaya cevap yok",
        _ => status
    };

    private static readonly string[] DefaultReportColumns =
    {
        "full_name", "user_name", "team", "source", "incident_count",
        "max_matches", "action", "last_seen", "policy"
    };

    private static bool IsReportColumn(string column) => column is "full_name" or "user_name" or "team" or "source" or "incident_count" or "max_risk_score" or "max_matches" or "last_seen" or "policy" or "destination" or "channel" or "action" or "data_type" or "severity" or "query_date" or "query_status" or "response_status" or "reminder_date" or "reminder_count";

    private static string ReportColumnLabel(string column) => column switch
    {
        "full_name" => "Kullan\u0131c\u0131",
        "user_name" => "Kullan\u0131c\u0131 Ad\u0131",
        "team" => "Ekip",
        "source" => "Kaynak",
        "incident_count" => "Olay Kayd\u0131 Say\u0131s\u0131",
        "max_risk_score" => "Maksimum Risk Skoru",
        "max_matches" => "Maksimum E\u015fle\u015fme",
        "last_seen" => "Son Olay Tarihi",
        "policy" => "\u00d6rnek Politika / Kural",
        "destination" => "Hedef",
        "channel" => "Kanal",
        "action" => "Aksiyon",
        "data_type" => "Veri Tipi",
        "severity" => "Siddet",
        "query_date" => "Sorgu Tarihi",
        "query_status" => "Durum",
        "response_status" => "Son Islem",
        "reminder_date" => "Hatirlatma Tarihi",
        "reminder_count" => "Hatirlatma Adedi",
        _ => column
    };

    private static string ReportColumnValue(string column, WeeklyFlagUserDto user, string source, WeeklyFlagIncidentDto? sample, InvestigationQueryRecord? query) => column switch
    {
        "full_name" => Encode(user.FullName ?? user.UserEmail),
        "user_name" => Encode(user.UserEmail),
        "team" => Encode(user.Team ?? "-"),
        "source" => Encode(ReportCriterionLabel(source)),
        "incident_count" => user.TriggerCount.ToString("N0"),
        "max_risk_score" => sample?.RiskScore?.ToString("N0") ?? "-",
        "max_matches" => sample?.MaxMatches.ToString("N0") ?? "-",
        "last_seen" => user.LastSeen.ToString("dd.MM.yyyy HH:mm"),
        "policy" => Encode(sample?.Policy ?? "-"),
        "destination" => Encode(sample?.Destination ?? "-"),
        "channel" => Encode(sample?.Channel ?? "-"),
        "action" => Encode(sample?.Action ?? "-"),
        "data_type" => Encode(sample?.DataType ?? "-"),
        "severity" => sample?.Severity?.ToString() ?? "-",
        "query_date" => query?.QueryDate?.ToString("dd.MM.yyyy") ?? "-",
        "query_status" => Encode(QueryStatusLabel(query?.QueryStatus)),
        "response_status" => Encode(query?.ResponseStatus ?? "-"),
        "reminder_date" => query?.ReminderSentAt?.ToString("dd.MM.yyyy HH:mm") ?? "-",
        "reminder_count" => query?.ReminderCount.ToString() ?? "0",
        _ => "-"
    };

    private static string QueryStatusLabel(string? status) => status switch
    {
        InvestigationQueryStatus.Pending => "Sorgu bekliyor",
        InvestigationQueryStatus.Queried => "Sorgulandi",
        InvestigationQueryStatus.ReplyReview => "Cevap geldi - inceleme bekliyor",
        InvestigationQueryStatus.ReminderUnanswered => "Hatirlatma sonrasi yanitsiz",
        InvestigationQueryStatus.Completed => "Tamamlandi",
        _ => status ?? "-"
    };

    private static string ApplyReportPlaceholders(string? text, PlaybookPayload payload, DateTime now)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var windowStart = payload.HasMetric
            ? payload.Metric!.WindowStart
            : payload.Items.Select(i => (DateTime?)i.User.FirstSeen).Min() ?? now;
        var windowEnd = payload.HasMetric
            ? payload.Metric!.WindowEnd
            : payload.Items.Select(i => (DateTime?)i.User.LastSeen).Max() ?? now;

        return text
            .Replace("{{tarih}}", now.ToString("dd.MM.yyyy"))
            .Replace("{{uretim_tarihi}}", now.ToString("dd.MM.yyyy HH:mm"))
            .Replace("{{donem}}", $"{windowStart:dd.MM.yyyy} - {windowEnd:dd.MM.yyyy}")
            .Replace("{{satir}}", payload.HasMetric ? "1" : payload.Items.Count.ToString("N0"));
    }

    private static string ReportMailStyles() => @"
  <style>
    body { margin: 0; font-family: Arial, Helvetica, sans-serif; color: #0f172a; background: #ffffff; }
    .wrap { width: 100%; max-width: none; margin: 0; padding: 0; }
    .header { background: #eef4ff; color: #111827; padding: 14px 16px; border-bottom: 2px solid #bfdbfe; }
    .content { background: #fff; padding: 18px 16px 20px; border: 0; }
    h1 { margin: 0; font-size: 20px; }
    h2 { margin: 18px 0 8px; font-size: 15px; }
    .intro { color: #334155; margin: 0 0 12px; line-height: 1.55; }
    .meta { color: #475569; margin: 0 0 18px; line-height: 1.55; background: #f8fafc; border-left: 3px solid #bfdbfe; padding: 8px 10px; }
    table { width: 100%; border-collapse: collapse; font-size: 12px; }
    th { text-align: left; background: #f1f5f9; border-bottom: 1px solid #cbd5e1; padding: 8px; }
    td { border-bottom: 1px solid #e2e8f0; padding: 8px; vertical-align: top; }
    .empty { text-align: center; color: #64748b; padding: 24px; }
    .footer { color: #64748b; font-size: 11px; margin-top: 16px; }
  </style>";

    private static string ReportCriterionLabel(string? criterion) => criterion switch
    {
        PlaybookNodeType.SourceHighRiskUsers => "Haftalık yüksek skorlu kullanıcı",
        PlaybookNodeType.SourceHighMaxMatchTransfers => "Yüksek maksimum eşleşmeli gönderim",
        PlaybookNodeType.SourcePendingQueryReminders => "Hatırlatma için cevap bekleyen sorgu",
        "top_permit_users" => "En çok Permit olay kaydı",
        "top_block_users" => "En çok Block olay kaydı",
        WeeklyFlagCriterion.PersonalEmailSenders => WeeklyFlagCriterion.Label(WeeklyFlagCriterion.PersonalEmailSenders),
        WeeklyFlagCriterion.HighVolume => WeeklyFlagCriterion.Label(WeeklyFlagCriterion.HighVolume),
        WeeklyFlagCriterion.MassiveMatches => WeeklyFlagCriterion.Label(WeeklyFlagCriterion.MassiveMatches),
        PlaybookNodeType.SourceQueryTracking => "Sorgu ve hatırlatma takibi",
        _ => criterion ?? "-"
    };

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

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
            var isReminder = mail.SourceCriterion == PlaybookNodeType.SourcePendingQueryReminders;
            var query = isReminder && !string.IsNullOrWhiteSpace(mail.CorrelationCode)
                ? await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.CorrelationCode == mail.CorrelationCode, ct)
                : await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.PlaybookMailLogId == mail.Id, ct);

            if (isReminder && query != null)
            {
                if (mail.Status == PlaybookMailStatus.Sent)
                {
                    query.ReminderCount++;
                    query.ReminderSentAt = mail.SentAt ?? now;
                    query.ResponseStatus = "Hatirlatma gonderildi";
                    query.Action = "Ilk hatirlatma maili gonderildi";
                }
                else
                {
                    query.ResponseStatus = "Hatirlatma gonderim onayi bekliyor";
                    query.Action = mail.ErrorMessage ?? "Hatirlatma maili hazirlandi";
                }

                query.UpdatedAt = now;
                query.UpdatedBy = "Agentic Workflow";
                changedRows.Add(query);
                continue;
            }

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
            query.CorrelationCode = mail.CorrelationCode;
            query.FirstSentAt ??= mail.SentAt;
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

    private static string NewCorrelationCode() => $"RADAR-Q-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    /// <summary>
    /// Subject/body come from a saved mail template; per-node overrides win when filled in,
    /// which is how the analyst tweaks one playbook without editing the shared template.
    /// </summary>
    private enum MailTemplateRoute
    {
        Personal,
        GitHub,
        Destination
    }

    private readonly record struct MailTemplateContent(string Subject, string Body);
    private readonly record struct MailTemplateDecision(
        MailTemplateContent Template,
        WeeklyFlagIncidentDto? Incident,
        int? TemplateId,
        string? TemplateName,
        string MatchReason);
    private sealed record TemplateMatchRule(string Pattern, int TemplateId, int Index);

    private MailTemplateContent ResolveNodeTemplate(PlaybookNode node, IReadOnlyCollection<MailTemplate> templates)
    {
        var subject = node.GetString("subject_override")?.Trim();
        var body = node.GetString("body_override");

        var templateId = node.GetInt("template_id");
        if (templateId.HasValue && templateId.Value > 0)
        {
            var template = templates.FirstOrDefault(t => t.Id == templateId.Value)
                ?? throw new InvalidOperationException($"Mail sablonu bulunamadi (id: {templateId.Value})");

            if (string.IsNullOrWhiteSpace(subject)) subject = template.Subject;
            if (string.IsNullOrWhiteSpace(body)) body = template.Body;
        }

        return new MailTemplateContent(subject ?? string.Empty, body ?? string.Empty);
    }

    private MailTemplateDecision ResolveTemplateForUser(
        PlaybookNode node,
        PlaybookItem item,
        MailTemplateContent defaultTemplate,
        IReadOnlyCollection<MailTemplate> templates,
        IReadOnlyCollection<TemplateMatchRule> rules)
    {
        var fallbackIncident = SelectTemplateIncident(item);
        var fallbackTemplate = FindFallbackTemplate(node, templates);

        // A reminder starts from an existing query, not a fresh incident. It intentionally has
        // no destination to match, so its explicitly selected template must win over the
        // destination/general-template routing used by incident workflows.
        if (item.SourceCriterion == PlaybookNodeType.SourcePendingQueryReminders ||
            !node.GetBool("auto_template_by_destination", true) ||
            HasNodeTemplateOverride(node))
            return new MailTemplateDecision(
                RequireTemplate(defaultTemplate, "Mail konusu bos - bir sablon secin ya da konu yazin."),
                fallbackIncident,
                node.GetInt("template_id"),
                fallbackTemplate?.Name ?? "Node sablonu",
                "Node uzerinde secilen sablon veya icerik");

        var ruleMatch = SelectRuleMatchedTemplate(item.User, rules, templates);
        if (ruleMatch != null) return ruleMatch.Value;

        var incident = SelectTemplateIncidentByTemplateMatch(item, templates, node.GetInt("template_id")) ?? fallbackIncident;
        var route = DetermineTemplateRoute(item.SourceCriterion, incident?.Destination);
        var configured = ResolveConfiguredRouteTemplate(node, route, templates);
        if (configured != null) return DecisionFromTemplate(configured, incident, "Destination icin tanimli sablon eslesmesi");

        var guessed = GuessRouteTemplate(route, incident, templates, node.GetInt("template_id"));
        if (guessed != null) return DecisionFromTemplate(guessed, incident, "Sablon adi ve olay hedefi benzerligi");

        if (fallbackTemplate != null)
            return DecisionFromTemplate(fallbackTemplate, fallbackIncident, "Genel sablon fallback");

        return new MailTemplateDecision(
            RequireTemplate(defaultTemplate, "Destination icin uygun mail sablonu bulunamadi."),
            fallbackIncident,
            node.GetInt("template_id"),
            "Node sablonu",
            "Node uzerinde secilen fallback sablon");
    }

    private static MailTemplateContent RequireTemplate(MailTemplateContent template, string message)
    {
        if (string.IsNullOrWhiteSpace(template.Subject))
            throw new InvalidOperationException(message);

        return template;
    }

    private static bool HasNodeTemplateOverride(PlaybookNode node) =>
        !string.IsNullOrWhiteSpace(node.GetString("subject_override")) ||
        !string.IsNullOrWhiteSpace(node.GetString("body_override"));

    private static MailTemplateContent FromTemplate(MailTemplate template) =>
        new(template.Subject, template.Body ?? string.Empty);

    private static MailTemplateDecision DecisionFromTemplate(MailTemplate template, WeeklyFlagIncidentDto? incident, string reason) =>
        new(FromTemplate(template), incident, template.Id, template.Name, reason);

    private static MailTemplate? FindFallbackTemplate(PlaybookNode node, IReadOnlyCollection<MailTemplate> templates)
    {
        var selectedId = node.GetInt("template_id");
        if (selectedId is > 0)
            return templates.FirstOrDefault(template => template.Id == selectedId.Value);

        return templates
            .OrderBy(template => template.Id)
            .FirstOrDefault(template =>
            {
                var name = Fold(template.Name);
                return name.Contains("genel") || name.Contains("generic") || name.Contains("default");
            });
    }

    private List<TemplateMatchRule> ResolveTemplateMatchRules(
        PlaybookNode node,
        IReadOnlyCollection<MailTemplate> templates)
    {
        var result = new List<TemplateMatchRule>();
        if (!node.Config.TryGetValue("template_match_rules", out var element) ||
            element.ValueKind != JsonValueKind.Array)
            return result;

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object) continue;

            var pattern = ReadString(item, "pattern") ?? ReadString(item, "destination") ?? ReadString(item, "match");
            var templateId = ReadInt(item, "template_id") ?? ReadInt(item, "templateId");
            if (string.IsNullOrWhiteSpace(pattern) || templateId is null or <= 0) continue;

            if (!templates.Any(t => t.Id == templateId.Value))
                throw new InvalidOperationException($"Mail sablonu bulunamadi (id: {templateId.Value})");

            result.Add(new TemplateMatchRule(pattern.Trim(), templateId.Value, index));
        }

        return result;
    }

    private static string? ReadString(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static IEnumerable<int> ReadTemplateMatchRuleIds(PlaybookNode node)
    {
        if (!node.Config.TryGetValue("template_match_rules", out var element) ||
            element.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var templateId = ReadInt(item, "template_id") ?? ReadInt(item, "templateId");
            if (templateId is > 0) yield return templateId.Value;
        }
    }

    private static MailTemplateDecision? SelectRuleMatchedTemplate(
        WeeklyFlagUserDto user,
        IReadOnlyCollection<TemplateMatchRule> rules,
        IReadOnlyCollection<MailTemplate> templates)
    {
        if (rules.Count == 0 || user.SampleIncidents.Count == 0) return null;

        var match = user.SampleIncidents
            .SelectMany(incident => rules.Select(rule => new
            {
                Incident = incident,
                Rule = rule,
                Score = ScoreRuleMatch(rule.Pattern, incident)
            }))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Incident.MaxMatches)
            .ThenByDescending(x => x.Incident.Timestamp)
            .ThenBy(x => x.Rule.Index)
            .FirstOrDefault();

        if (match == null) return null;

        var template = templates.First(t => t.Id == match.Rule.TemplateId);
        return DecisionFromTemplate(template, match.Incident, $"Tanimli eslesme kurali: {match.Rule.Pattern}");
    }

    private static int ScoreRuleMatch(string pattern, WeeklyFlagIncidentDto incident)
    {
        var destination = Fold(incident.Destination);
        var combined = Fold($"{incident.Destination} {incident.Channel} {incident.Policy}");
        var best = 0;

        foreach (var token in SplitPattern(pattern))
        {
            if (token.Length == 0) continue;

            if (destination == token) best = Math.Max(best, 120 + token.Length);
            else if (WildcardMatches(token, destination)) best = Math.Max(best, 100 + LiteralLength(token));
            else if (WildcardMatches(token, combined)) best = Math.Max(best, 80 + LiteralLength(token));
            else if (destination.Contains(token)) best = Math.Max(best, 70 + token.Length);
            else if (combined.Contains(token)) best = Math.Max(best, 45 + token.Length);
        }

        return best;
    }

    private static IEnumerable<string> SplitPattern(string pattern)
    {
        var folded = Fold(pattern);
        var separators = folded.Contains("://")
            ? new[] { ',', ';', '\n', '\r' }
            : new[] { ',', ';', '\n', '\r', '/' };

        return folded
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);
    }

    private static bool WildcardMatches(string pattern, string target)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(target)) return false;
        if (!pattern.Contains('*')) return target.Contains(pattern);

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        if (Regex.IsMatch(target, regex, RegexOptions.IgnoreCase)) return true;
        return Regex.IsMatch(target, ".*" + regex.Trim('^', '$') + ".*", RegexOptions.IgnoreCase);
    }

    private static int LiteralLength(string pattern) => pattern.Count(c => c != '*');

    private static WeeklyFlagIncidentDto? SelectTemplateIncidentByTemplateMatch(
        PlaybookItem item,
        IReadOnlyCollection<MailTemplate> templates,
        int? fallbackTemplateId)
    {
        if (templates.Count == 0 || item.User.SampleIncidents.Count == 0) return null;

        var match = item.User.SampleIncidents
            .Select(incident => new
            {
                Incident = incident,
                Score = templates.Max(t => ScoreTemplateForIncident(t, incident, fallbackTemplateId))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Incident.MaxMatches)
            .ThenByDescending(x => x.Incident.Timestamp)
            .FirstOrDefault();

        return match?.Incident;
    }

    private static WeeklyFlagIncidentDto? SelectTemplateIncident(PlaybookItem item)
    {
        var incidents = item.User.SampleIncidents ?? new List<WeeklyFlagIncidentDto>();
        if (incidents.Count == 0) return null;

        var github = incidents
            .Where(i => IsGitHubDestination(i.Destination))
            .OrderByDescending(i => i.MaxMatches)
            .ThenByDescending(i => i.Timestamp)
            .FirstOrDefault();
        if (github != null) return github;

        if (item.SourceCriterion == WeeklyFlagCriterion.PersonalEmailSenders)
        {
            return incidents
                .OrderByDescending(i => i.Timestamp)
                .ThenByDescending(i => i.MaxMatches)
                .FirstOrDefault();
        }

        var personal = incidents
            .Where(i => LooksLikePersonalDestination(i.Destination))
            .OrderByDescending(i => i.MaxMatches)
            .ThenByDescending(i => i.Timestamp)
            .FirstOrDefault();
        if (personal != null) return personal;

        return incidents
            .OrderByDescending(i => i.MaxMatches)
            .ThenByDescending(i => i.Timestamp)
            .FirstOrDefault();
    }

    private static string? BuildIncidentSummaryJson(WeeklyFlagIncidentDto? incident)
    {
        if (incident == null) return null;

        return JsonSerializer.Serialize(new
        {
            timestamp = incident.Timestamp,
            policy = incident.Policy,
            max_matches = incident.MaxMatches,
            destination = incident.Destination,
            channel = incident.Channel,
            action = incident.Action,
            data_type = incident.DataType,
            severity = incident.Severity,
            risk_score = incident.RiskScore
        });
    }

    private static WeeklyFlagUserDto WithPrimaryIncident(WeeklyFlagUserDto user, WeeklyFlagIncidentDto? primary)
    {
        if (primary == null || user.SampleIncidents.Count == 0) return user;

        var samples = new List<WeeklyFlagIncidentDto> { primary };
        samples.AddRange(user.SampleIncidents.Where(i => !Equals(i, primary)));
        return user with { SampleIncidents = samples };
    }

    private static MailTemplate? ResolveConfiguredRouteTemplate(
        PlaybookNode node,
        MailTemplateRoute route,
        IReadOnlyCollection<MailTemplate> templates)
    {
        var templateId = route switch
        {
            MailTemplateRoute.Personal => node.GetInt("personal_template_id"),
            MailTemplateRoute.GitHub => node.GetInt("github_template_id"),
            _ => node.GetInt("destination_template_id")
        };

        if (templateId is null or <= 0) return null;

        return templates.FirstOrDefault(t => t.Id == templateId.Value)
            ?? throw new InvalidOperationException($"Mail sablonu bulunamadi (id: {templateId.Value})");
    }

    private static MailTemplateRoute DetermineTemplateRoute(string? criterion, string? destination)
    {
        if (IsGitHubDestination(destination)) return MailTemplateRoute.GitHub;
        if (criterion == WeeklyFlagCriterion.PersonalEmailSenders || LooksLikePersonalDestination(destination))
            return MailTemplateRoute.Personal;

        return MailTemplateRoute.Destination;
    }

    private static bool IsGitHubDestination(string? destination)
    {
        var value = Fold(destination);
        return value.Contains("business.github") || value.Contains("github");
    }

    private static bool LooksLikePersonalDestination(string? destination)
    {
        var value = Fold(destination);
        if (!value.Contains('@')) return false;

        var personalDomains = new[]
        {
            "@gmail.", "@hotmail.", "@outlook.", "@yahoo.", "@icloud.",
            "@live.", "@msn.", "@yandex.", "@proton.", "@me.com"
        };
        return personalDomains.Any(value.Contains);
    }

    private static MailTemplate? GuessRouteTemplate(
        MailTemplateRoute route,
        WeeklyFlagIncidentDto? incident,
        IReadOnlyCollection<MailTemplate> templates,
        int? fallbackTemplateId)
    {
        return templates
            .Select(t => new { Template = t, Score = ScoreRouteTemplate(t, route, incident, fallbackTemplateId) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Template.Id)
            .Select(x => x.Template)
            .FirstOrDefault();
    }

    private static int ScoreRouteTemplate(
        MailTemplate template,
        MailTemplateRoute route,
        WeeklyFlagIncidentDto? incident,
        int? fallbackTemplateId)
    {
        var haystack = Fold($"{template.Name} {template.Subject} {template.Body}");
        var score = 0;
        var destination = incident?.Destination;

        if (route == MailTemplateRoute.GitHub)
        {
            if (haystack.Contains("business.github")) score += 50;
            if (haystack.Contains("github")) score += 35;
            if (ContainsDestinationToken(haystack)) score += 5;
        }
        else if (route == MailTemplateRoute.Personal)
        {
            if (haystack.Contains("sahsi")) score += 35;
            if (haystack.Contains("kisisel")) score += 30;
            if (haystack.Contains("personal")) score += 30;
            if (ContainsDestinationToken(haystack)) score += 5;
        }
        else
        {
            var foldedDestination = Fold(destination);
            if (!string.IsNullOrWhiteSpace(foldedDestination) && haystack.Contains(foldedDestination)) score += 45;
            if (ContainsDestinationToken(haystack)) score += 30;
            if (haystack.Contains("destination")) score += 12;
            if (haystack.Contains("hedef")) score += 12;
            if (haystack.Contains("generic") || haystack.Contains("genel")) score += 8;
        }

        score += ScoreTemplateForIncident(template, incident, fallbackTemplateId);
        if (fallbackTemplateId.HasValue && template.Id == fallbackTemplateId.Value) score += 1;
        return score;
    }

    private static int ScoreTemplateForIncident(
        MailTemplate template,
        WeeklyFlagIncidentDto? incident,
        int? fallbackTemplateId)
    {
        if (incident == null) return fallbackTemplateId.HasValue && template.Id == fallbackTemplateId.Value ? 1 : 0;

        var haystack = Fold($"{template.Name} {template.Subject} {template.Body}");
        var destination = Fold(incident.Destination);
        var channel = Fold(incident.Channel);
        var policy = Fold(incident.Policy);
        var score = 0;

        if (!string.IsNullOrWhiteSpace(destination) && haystack.Contains(destination)) score += 60;
        if (!string.IsNullOrWhiteSpace(channel) && haystack.Contains(channel)) score += 35;

        var tokens = DestinationTokens(incident)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        score += tokens.Where(haystack.Contains).Sum(token => Math.Min(16, token.Length + 4));

        if (!string.IsNullOrWhiteSpace(policy))
        {
            var policyTokens = Regex.Split(policy, @"[^a-z0-9]+")
                .Where(token => token.Length >= 4 && !CommonTemplateTokens.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            score += policyTokens.Where(haystack.Contains).Sum(_ => 4);
        }

        if (ContainsDestinationToken(haystack)) score += 4;
        return score;
    }

    private static IEnumerable<string> DestinationTokens(WeeklyFlagIncidentDto incident)
    {
        var text = Fold($"{incident.Destination} {incident.Channel}");
        return Regex.Split(text, @"[^a-z0-9]+")
            .Where(token => token.Length > 0 && !CommonTemplateTokens.Contains(token));
    }

    private static readonly HashSet<string> CommonTemplateTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "net", "org", "www", "mail", "email", "http", "https", "the", "and", "ve", "icin", "ile"
    };

    private static bool ContainsDestinationToken(string haystack) =>
        haystack.Contains("{{destination}}") || haystack.Contains("{{hedef}}");

    private static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim()
            .ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('İ', 'i')
            .Replace('ş', 's')
            .Replace('Ş', 's')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'g')
            .Replace('ü', 'u')
            .Replace('Ü', 'u')
            .Replace('ö', 'o')
            .Replace('Ö', 'o')
            .Replace('ç', 'c')
            .Replace('Ç', 'c');
    }

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
        await SyncQueryRecordsSafelyAsync(queryUpdates, ct);

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

                case PlaybookNodeType.SourceHighRiskUsers:
                {
                    if (node.GetInt("days") is <= 0)
                        result.Errors.Add($"'{node.Label}' gün sayısı 0'dan büyük olmalı.");
                    if (node.GetInt("top_limit") is <= 0)
                        result.Errors.Add($"'{node.Label}' top limit 0'dan büyük olmalı.");
                    var minRisk = node.GetInt("min_risk_score");
                    if (minRisk is < 0 or > 100)
                        result.Errors.Add($"'{node.Label}' min risk skoru 0-100 arasinda olmali.");
                    break;
                }

                case PlaybookNodeType.SourceTopActionUsers:
                {
                    if (node.GetInt("days") is <= 0)
                        result.Errors.Add($"'{node.Label}' gün sayısı 0'dan büyük olmalı.");
                    if (node.GetInt("top_limit") is <= 0)
                        result.Errors.Add($"'{node.Label}' top limit 0'dan büyük olmalı.");
                    var actionKind = node.GetString("action_kind") ?? "permit";
                    if (actionKind is not ("permit" or "block"))
                        result.Errors.Add($"'{node.Label}' aksiyon turu permit veya block olmali.");
                    break;
                }

                case PlaybookNodeType.SourceHighMaxMatchTransfers:
                {
                    if (node.GetInt("days") is <= 0)
                        result.Errors.Add($"'{node.Label}' gün sayısı 0'dan büyük olmalı.");
                    if (node.GetInt("top_limit") is <= 0)
                        result.Errors.Add($"'{node.Label}' top limit 0'dan büyük olmalı.");
                    if (node.GetInt("min_matches") is <= 0)
                        result.Errors.Add($"'{node.Label}' maksimum eşleşme alt sınırı 0'dan büyük olmalı.");
                    break;
                }

                case PlaybookNodeType.SourcePendingQueryReminders:
                    break;

                case PlaybookNodeType.SourceQueryTracking:
                {
                    var mode = node.GetString("period_mode") ?? "this_week";
                    if (mode is not ("this_week" or "last_7_days" or "last_n_days" or "custom"))
                        result.Errors.Add($"'{node.Label}' bilinmeyen bir zaman araligi iceriyor.");
                    if (mode == "last_n_days" && node.GetInt("days") is <= 0)
                        result.Errors.Add($"'{node.Label}' gun sayisi 0'dan buyuk olmali.");
                    if (mode == "custom" &&
                        (!DateTime.TryParse(node.GetString("from_date"), out _) ||
                         !DateTime.TryParse(node.GetString("to_date"), out _)))
                        result.Errors.Add($"'{node.Label}' icin baslangic ve bitis tarihi girin.");
                    var statuses = node.GetStringList("statuses");
                    if (statuses.Any(status => !QueryTrackingStatuses.All.Contains(status)))
                        result.Errors.Add($"'{node.Label}' bilinmeyen bir sorgu durumu iceriyor.");
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
                    var routeTemplateIds = new List<int?>
                    {
                        node.GetInt("personal_template_id"),
                        node.GetInt("github_template_id"),
                        node.GetInt("destination_template_id")
                    };
                    routeTemplateIds.AddRange(ReadTemplateMatchRuleIds(node).Select(id => (int?)id));
                    var hasRouteTemplate = routeTemplateIds.Any(id => id is > 0);
                    var hasOverride = !string.IsNullOrWhiteSpace(node.GetString("subject_override"));
                    var autoTemplateByDestination = node.GetBool("auto_template_by_destination", true);
                    if (!autoTemplateByDestination &&
                        (templateId is null or <= 0) && !hasRouteTemplate && !hasOverride)
                        result.Errors.Add($"'{node.Label}' için bir şablon seçin ya da konu girin.");
                    else if (templateId is > 0 &&
                             !await _context.MailTemplates.AnyAsync(t => t.Id == templateId.Value, ct))
                        result.Errors.Add($"'{node.Label}' seçili şablon artık mevcut değil.");

                    foreach (var routeTemplateId in routeTemplateIds.Where(id => id is > 0).Select(id => id!.Value).Distinct())
                    {
                        if (!await _context.MailTemplates.AnyAsync(t => t.Id == routeTemplateId, ct))
                            result.Errors.Add($"'{node.Label}' rota sablonu artik mevcut degil (id: {routeTemplateId}).");
                    }

                    if (node.GetString("recipient_mode") == "fixed" &&
                        !IsValidEmail(node.GetString("fixed_recipient")))
                        result.Errors.Add($"'{node.Label}' sabit alıcı adresi geçerli değil.");

                    var cc = node.GetString("cc_email");
                    if (!string.IsNullOrWhiteSpace(cc) && !IsValidEmail(cc))
                        result.Errors.Add($"'{node.Label}' CC adresi geçerli değil.");
                    break;
                }

                case PlaybookNodeType.ActionSendReportMail:
                {
                    var fixedRecipient = node.GetString("fixed_recipient");
                    if (!string.IsNullOrWhiteSpace(fixedRecipient) && !IsValidEmail(fixedRecipient))
                        result.Errors.Add($"'{node.Label}' rapor alıcısı geçerli değil.");

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

        var trackingReach = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in graph.Nodes.Where(n => n.Type == PlaybookNodeType.SourceQueryTracking))
            foreach (var id in Downstream(source.Id, graph, nodesById))
                trackingReach.Add(id);

        foreach (var node in graph.Nodes.Where(n => n.Type == PlaybookNodeType.LogicMetricThreshold))
        {
            if (!metricReach.Contains(node.Id))
                result.Errors.Add(
                    $"'{node.Label}' bir Incident Metriği node'una bağlı değil; " +
                    "metrik eşiği yalnızca metrik girdisiyle çalışır.");
        }

        foreach (var node in graph.Nodes.Where(n => n.Type == PlaybookNodeType.ActionSendMail))
        {
            if (metricReach.Contains(node.Id) && node.GetString("recipient_mode") != "fixed")
                result.Errors.Add(
                    $"'{node.Label}' bir metrik akışında olduğu için Alıcı \"Sabit bir adres\" olmalı " +
                    "(kurum toplamının kişisel bir adresi yok).");

            if (trackingReach.Contains(node.Id))
                result.Errors.Add(
                    $"'{node.Label}' Sorgu ve Hatırlatma Takibi kaynağından besleniyor. " +
                    "Bu kaynak yalnızca ekip raporu içindir; Rapor Maili Gönder node'unu kullanın.");
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
                                             or PlaybookNodeType.SourceIncidentMetric
                                             or PlaybookNodeType.SourceHighRiskUsers
                                             or PlaybookNodeType.SourceTopActionUsers
                                             or PlaybookNodeType.SourceHighMaxMatchTransfers
                                             or PlaybookNodeType.SourcePendingQueryReminders
                                             or PlaybookNodeType.SourceQueryTracking))
                result.Warnings.Add("Akışta veri kaynağı yok; hiçbir kullanıcı ya da metrik hesaplanmayacak.");

            foreach (var node in metricSources.Where(n => !metricReach.Any(id =>
                         id != n.Id && nodesById[id].Type == PlaybookNodeType.LogicMetricThreshold)))
                result.Warnings.Add(
                    $"'{node.Label}' bir Metrik Eşiği node'una bağlı değil; mail eşik kontrolü olmadan " +
                    "her çalıştırmada gönderilir.");
            if (!graph.Nodes.Any(n => n.Type is PlaybookNodeType.ActionSendMail
                                             or PlaybookNodeType.ActionSendReportMail))
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
            "daily" => $"{minute} {hour} * * {(node.GetBool("weekdays_only") ? "1-5" : "*")}",
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

        public SendContext(bool dryRun, bool autoSend, string? reportRecipientEmail = null, bool requestPdfAttachment = false)
        {
            DryRun = dryRun;
            AutoSend = autoSend;
            ReportRecipientEmail = string.IsNullOrWhiteSpace(reportRecipientEmail) ? null : reportRecipientEmail.Trim();
            RequestPdfAttachment = requestPdfAttachment;
        }

        public bool DryRun { get; }
        public bool AutoSend { get; }
        public string? ReportRecipientEmail { get; }
        public bool RequestPdfAttachment { get; }
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
