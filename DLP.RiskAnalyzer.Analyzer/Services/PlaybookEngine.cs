using System.Diagnostics;
using System.Net.Mail;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
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

    /// <summary>Output port key used by every node except the condition node.</summary>
    private const string MainHandle = "main";

    private readonly AnalyzerDbContext _context;
    private readonly IWeeklyFlagService _weeklyFlagService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PlaybookEngine> _logger;

    public PlaybookEngine(
        AnalyzerDbContext context,
        IWeeklyFlagService weeklyFlagService,
        IEmailService emailService,
        ILogger<PlaybookEngine> logger)
    {
        _context = context;
        _weeklyFlagService = weeklyFlagService;
        _emailService = emailService;
        _logger = logger;
    }

    // ── Run ──────────────────────────────────────────────────────────────────

    public async Task<PlaybookRun> RunAsync(int playbookId, string triggerType, bool? forceDryRun, CancellationToken ct = default)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.FirstOrDefaultAsync(p => p.Id == playbookId, ct)
            ?? throw new KeyNotFoundException($"Playbook bulunamadı: {playbookId}");

        var alreadyRunning = await _context.PlaybookRuns
            .AnyAsync(r => r.PlaybookId == playbookId && r.Status == PlaybookRunStatus.Running, ct);
        if (alreadyRunning)
            throw new InvalidOperationException("Bu playbook için hâlâ çalışan bir akış var. Bitmesini bekleyin.");

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
                        ?? throw new InvalidOperationException("Playbook akışı okunamadı (bozuk graph verisi).");

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

        // nodeId → output handle → items
        var outputs = new Dictionary<string, Dictionary<string, List<PlaybookItem>>>(StringComparer.Ordinal);

        // Run-wide guards shared by every send-mail node.
        var context = new SendContext(dryRun);

        foreach (var nodeId in order)
        {
            var node = nodesById[nodeId];
            var sw = Stopwatch.StartNew();

            var input = graph.Edges
                .Where(e => e.Target == nodeId)
                .SelectMany(e => outputs.TryGetValue(e.Source, out var byHandle) &&
                                 byHandle.TryGetValue(HandleOf(e), out var items)
                    ? items
                    : Enumerable.Empty<PlaybookItem>())
                .ToList();

            var log = new PlaybookNodeLog
            {
                NodeId = node.Id,
                NodeType = node.Type,
                Label = string.IsNullOrWhiteSpace(node.Label) ? node.Type : node.Label,
                ItemsIn = input.Count
            };

            try
            {
                var produced = await ExecuteNodeAsync(node, input, playbook, run, context, ct);
                outputs[nodeId] = produced;
                log.ItemsOut = produced.Values.Sum(v => v.Count);
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

    /// <summary>Wraps a node's items as its single default output port.</summary>
    private static Dictionary<string, List<PlaybookItem>> SingleOutput(List<PlaybookItem> items) =>
        new(StringComparer.Ordinal) { [MainHandle] = items };

    private async Task<Dictionary<string, List<PlaybookItem>>> ExecuteNodeAsync(
        PlaybookNode node,
        List<PlaybookItem> input,
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
                return SingleOutput(new List<PlaybookItem>());

            case PlaybookNodeType.SourceWeeklyFlags:
                return SingleOutput(await LoadWeeklyFlagsAsync(node, context, ct));

            case PlaybookNodeType.TransformFilter:
                return SingleOutput(ApplyFilter(node, input, context));

            case PlaybookNodeType.LogicCondition:
                return ApplyCondition(node, input, context);

            case PlaybookNodeType.ActionSendMail:
                return SingleOutput(await SendMailsAsync(node, input, playbook, run, context, ct));

            case PlaybookNodeType.OutputReport:
                context.SetMessage($"{input.Count} satır rapora yazıldı");
                return new Dictionary<string, List<PlaybookItem>>(StringComparer.Ordinal);

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

    private static Dictionary<string, List<PlaybookItem>> ApplyCondition(
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

        return new Dictionary<string, List<PlaybookItem>>(StringComparer.Ordinal)
        {
            ["true"] = onTrue,
            ["false"] = onFalse
        };
    }

    private async Task<List<PlaybookItem>> SendMailsAsync(
        PlaybookNode node,
        List<PlaybookItem> input,
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

        if (recipientMode == "fixed" && !IsValidEmail(fixedRecipient))
            throw new InvalidOperationException("Sabit alıcı adresi geçerli değil.");

        var now = DateTime.UtcNow;
        var processed = new List<PlaybookItem>();
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

            if (!IsValidEmail(toEmail))
            {
                entry.Status = PlaybookMailStatus.Skipped;
                entry.ErrorMessage = "Geçerli bir alıcı adresi yok";
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

        var verb = context.DryRun ? "onay için hazırlandı" : "gönderildi";
        var message = $"{processed.Count} mail {verb}";
        if (capped > 0) message += $" · {capped} alıcı üst sınır ({MaxRecipientsPerRun}) nedeniyle atlandı";
        context.SetMessage(message);

        return processed;
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
        foreach (var entry in pending)
        {
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

        // Advisory checks — these do not block saving a half-built draft.
        if (triggers.Count == 1)
        {
            var reachable = TopologicalOrderFrom(triggers[0].Id, graph, nodesById);
            var orphans = graph.Nodes.Where(n => !reachable.Contains(n.Id)).ToList();
            if (orphans.Count > 0)
                result.Warnings.Add(
                    $"Tetikleyiciye bağlı olmayan {orphans.Count} node çalıştırılmayacak: " +
                    string.Join(", ", orphans.Select(o => o.Label)));

            if (!graph.Nodes.Any(n => n.Type == PlaybookNodeType.SourceWeeklyFlags))
                result.Warnings.Add("Akışta veri kaynağı yok; hiçbir kullanıcı listelenmeyecek.");
            if (!graph.Nodes.Any(n => n.Type == PlaybookNodeType.ActionSendMail))
                result.Warnings.Add("Akışta mail gönderme adımı yok.");
            if (!graph.Nodes.Any(n => n.Type == PlaybookNodeType.OutputReport))
                result.Warnings.Add("Akışta rapor çıktısı yok; sonuçlar yine de mail kaydına yazılır.");
        }

        return result;
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
