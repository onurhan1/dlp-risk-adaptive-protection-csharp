using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// CRUD, execution and reporting for investigation playbooks — the n8n-style flows built on the
/// Investigation → Playbook screen.
/// </summary>
[ApiController]
[Route("api/playbooks")]
public class PlaybooksController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly IPlaybookEngine _engine;
    private readonly ILogger<PlaybooksController> _logger;

    public PlaybooksController(
        AnalyzerDbContext context,
        IPlaybookEngine engine,
        ILogger<PlaybooksController> logger)
    {
        _context = context;
        _engine = engine;
        _logger = logger;
    }

    public record PlaybookRequest(
        string Name,
        string? Description,
        PlaybookGraph? Graph,
        bool Enabled,
        bool AutoSend);

    // ── Node catalog ─────────────────────────────────────────────────────────

    /// <summary>
    /// The node palette, served from the backend so the UI and the engine cannot drift apart on
    /// node types, port counts or criterion names.
    /// </summary>
    [HttpGet("node-types")]
    public IActionResult GetNodeTypes()
    {
        var types = PlaybookNodeType.All.Select(type => new
        {
            type,
            inputs = PlaybookNodeType.InputCount(type),
            outputs = PlaybookNodeType.OutputCount(type),
            is_trigger = PlaybookNodeType.IsTrigger(type)
        });

        return Ok(new
        {
            node_types = types,
            criteria = WeeklyFlagCriterion.All.Select(c => new { value = c, label = WeeklyFlagCriterion.Label(c) }),
            incident_metrics = IncidentMetricKind.All.Select(m => new { value = m, label = IncidentMetricKind.Label(m) }),
            breakdown_dimensions = IncidentBreakdownDimension.All,
            max_recipients_per_run = PlaybookEngine.MaxRecipientsPerRun
        });
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var playbooks = await _context.Playbooks
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);

        var ids = playbooks.Select(p => p.Id).ToList();

        // Last run per playbook, plus how many mails are still waiting for approval.
        var runs = await _context.PlaybookRuns
            .AsNoTracking()
            .Where(r => ids.Contains(r.PlaybookId))
            .GroupBy(r => r.PlaybookId)
            .Select(g => g.OrderByDescending(r => r.StartedAt).First())
            .ToListAsync(ct);

        var pendingCounts = await _context.PlaybookMailLogs
            .AsNoTracking()
            .Where(m => ids.Contains(m.PlaybookId) && m.Status == PlaybookMailStatus.Pending)
            .GroupBy(m => m.PlaybookId)
            .Select(g => new { PlaybookId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = playbooks.Select(p =>
        {
            var lastRun = runs.FirstOrDefault(r => r.PlaybookId == p.Id);
            var pending = pendingCounts.FirstOrDefault(c => c.PlaybookId == p.Id)?.Count ?? 0;
            return ToListDto(p, lastRun, pending);
        });

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playbook == null) return NotFound(new { detail = "Agentic Workflow bulunamadı" });

        return Ok(ToDetailDto(playbook));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlaybookRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { detail = "Workflow adı zorunludur" });

        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var graph = request.Graph ?? new PlaybookGraph();
        var validation = await _engine.ValidateAsync(graph, ct);

        var now = DateTime.UtcNow;
        var playbook = new Playbook
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            GraphJson = PlaybookJson.Serialize(graph),
            AutoSend = request.AutoSend,
            CreatedAt = now,
            UpdatedAt = now
        };

        // A playbook can only be enabled once its graph actually runs.
        ApplySchedule(playbook, graph, request.Enabled && validation.IsValid, now);

        _context.Playbooks.Add(playbook);
        await _context.SaveChangesAsync(ct);

        return Ok(ToDetailDto(playbook, validation));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PlaybookRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { detail = "Workflow adı zorunludur" });

        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playbook == null) return NotFound(new { detail = "Agentic Workflow bulunamadı" });

        var graph = request.Graph ?? new PlaybookGraph();
        var validation = await _engine.ValidateAsync(graph, ct);

        // Drafts are saved freely; enabling a schedule is what requires a valid graph.
        if (request.Enabled && !validation.IsValid)
            return BadRequest(new { detail = string.Join(" · ", validation.Errors) });

        var now = DateTime.UtcNow;
        playbook.Name = request.Name.Trim();
        playbook.Description = request.Description?.Trim();
        playbook.GraphJson = PlaybookJson.Serialize(graph);
        playbook.AutoSend = request.AutoSend;
        playbook.UpdatedAt = now;
        ApplySchedule(playbook, graph, request.Enabled, now);

        await _context.SaveChangesAsync(ct);
        return Ok(ToDetailDto(playbook, validation));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playbook == null) return NotFound(new { detail = "Agentic Workflow bulunamadı" });

        // Keep the mail log — it is the audit trail — but drop the runs and the playbook itself.
        var runs = await _context.PlaybookRuns.Where(r => r.PlaybookId == id).ToListAsync(ct);
        _context.PlaybookRuns.RemoveRange(runs);
        _context.Playbooks.Remove(playbook);
        await _context.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var playbook = await _context.Playbooks.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playbook == null) return NotFound(new { detail = "Agentic Workflow bulunamadı" });

        var graph = PlaybookJson.Deserialize<PlaybookGraph>(playbook.GraphJson) ?? new PlaybookGraph();

        if (!playbook.Enabled)
        {
            var validation = await _engine.ValidateAsync(graph, ct);
            if (!validation.IsValid)
                return BadRequest(new { detail = string.Join(" · ", validation.Errors) });
            if (string.IsNullOrWhiteSpace(BuildCronFor(graph)))
                return BadRequest(new { detail = "Zamanlanmış çalıştırma için akışa Zamanlama tetikleyicisi ekleyin." });
        }

        var now = DateTime.UtcNow;
        playbook.UpdatedAt = now;
        ApplySchedule(playbook, graph, !playbook.Enabled, now);
        await _context.SaveChangesAsync(ct);

        return Ok(ToDetailDto(playbook));
    }

    // ── Execution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a playbook now. <c>dry_run=true</c> forces a preview; omitting it falls back to the
    /// playbook's own AutoSend setting, which defaults to a dry run.
    /// </summary>
    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> Run(int id, [FromQuery(Name = "dry_run")] bool? dryRun, CancellationToken ct)
    {
        try
        {
            // A manual run can send mail and persist a run history. Do not abort that work just
            // because the browser/proxy closes a long-running HTTP request while LDAP or reports run.
            var run = await _engine.RunAsync(id, PlaybookTriggerType.Manual, dryRun, ct: CancellationToken.None);
            return Ok(ToRunDto(run, includeLog: true));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("{id:int}/runs")]
    public async Task<IActionResult> GetRuns(int id, [FromQuery] int limit, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var take = limit <= 0 ? 50 : Math.Min(limit, 200);
        var runs = await _context.PlaybookRuns
            .AsNoTracking()
            .Where(r => r.PlaybookId == id)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(runs.Select(r => ToRunDto(r, includeLog: false)));
    }

    [HttpGet("runs/{runId:int}")]
    public async Task<IActionResult> GetRun(int runId, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var run = await _context.PlaybookRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run == null) return NotFound(new { detail = "Çalıştırma bulunamadı" });

        var mails = await _context.PlaybookMailLogs
            .AsNoTracking()
            .Where(m => m.RunId == runId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        return Ok(new
        {
            run = ToRunDto(run, includeLog: true),
            mails = mails.Select(ToMailDto)
        });
    }

    // ── Report ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The reportable audit trail: which user was queried, when, with which subject, and whether
    /// the mail actually went out. Feeds the exportable report table in the UI.
    /// </summary>
    [HttpGet("{id:int}/report")]
    public async Task<IActionResult> GetReport(
        int id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var take = limit <= 0 ? 1000 : Math.Min(limit, 5000);

        var query = _context.PlaybookMailLogs.AsNoTracking().Where(m => m.PlaybookId == id);
        if (from.HasValue) query = query.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(m => m.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") query = query.Where(m => m.Status == status);

        var rows = await query.OrderByDescending(m => m.CreatedAt).Take(take).ToListAsync(ct);

        return Ok(new
        {
            total = rows.Count,
            sent = rows.Count(r => r.Status == PlaybookMailStatus.Sent),
            pending = rows.Count(r => r.Status == PlaybookMailStatus.Pending),
            failed = rows.Count(r => r.Status == PlaybookMailStatus.Failed),
            skipped = rows.Count(r => r.Status == PlaybookMailStatus.Skipped),
            rows = rows.Select(ToMailDto)
        });
    }

    // ── Approval of dry-run mails ────────────────────────────────────────────

    [HttpPost("runs/{runId:int}/approve")]
    public async Task<IActionResult> ApproveRun(int runId, CancellationToken ct) =>
        await ApproveAsync(runId, null, ct);

    [HttpPost("mail-log/{mailLogId:int}/approve")]
    public async Task<IActionResult> ApproveMail(int mailLogId, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var entry = await _context.PlaybookMailLogs.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mailLogId, ct);
        if (entry == null) return NotFound(new { detail = "Mail kaydı bulunamadı" });

        return await ApproveAsync(entry.RunId, mailLogId, ct);
    }

    [HttpPost("mail-log/{mailLogId:int}/skip")]
    public async Task<IActionResult> SkipMail(int mailLogId, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var entry = await _context.PlaybookMailLogs.FirstOrDefaultAsync(m => m.Id == mailLogId, ct);
        if (entry == null) return NotFound(new { detail = "Mail kaydı bulunamadı" });
        if (entry.Status != PlaybookMailStatus.Pending)
            return BadRequest(new { detail = "Yalnızca onay bekleyen kayıtlar atlanabilir" });

        entry.Status = PlaybookMailStatus.Skipped;
        entry.ErrorMessage = "Kullanıcı tarafından atlandı";
        await _context.SaveChangesAsync(ct);

        var run = await _context.PlaybookRuns.FirstOrDefaultAsync(r => r.Id == entry.RunId, ct);
        if (run != null)
        {
            run.MailsPending = await _context.PlaybookMailLogs
                .CountAsync(m => m.RunId == run.Id && m.Status == PlaybookMailStatus.Pending, ct);
            run.MailsSkipped = await _context.PlaybookMailLogs
                .CountAsync(m => m.RunId == run.Id && m.Status == PlaybookMailStatus.Skipped, ct);
            if (run.MailsPending == 0 && run.Status == PlaybookRunStatus.AwaitingApproval)
                run.Status = PlaybookRunStatus.Success;
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { success = true, status = entry.Status });
    }

    private async Task<IActionResult> ApproveAsync(int runId, int? mailLogId, CancellationToken ct)
    {
        try
        {
            var (sent, failed) = await _engine.ApprovePendingAsync(runId, mailLogId, ct);
            return Ok(new
            {
                success = failed == 0,
                sent,
                failed,
                message = failed == 0
                    ? $"{sent} mail gönderildi"
                    : $"{sent} mail gönderildi, {failed} mail gönderilemedi"
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes the denormalised scheduling columns the background service reads. Enabling
    /// without a schedule trigger is a no-op: there would be nothing for the scheduler to fire.
    /// </summary>
    private static void ApplySchedule(Playbook playbook, PlaybookGraph graph, bool enabled, DateTime nowUtc)
    {
        var cron = BuildCronFor(graph);
        playbook.ScheduleCron = cron;
        playbook.Enabled = enabled && !string.IsNullOrWhiteSpace(cron);
        playbook.NextRunAt = playbook.Enabled ? CronSchedule.Next(cron, nowUtc, RadarTimeZone.Turkey) : null;
    }

    private static string? BuildCronFor(PlaybookGraph graph)
    {
        var trigger = graph.Nodes.FirstOrDefault(n => n.Type == PlaybookNodeType.TriggerSchedule);
        return trigger == null ? null : PlaybookEngine.BuildCron(trigger);
    }

    private static object ToListDto(Playbook p, PlaybookRun? lastRun, int pendingMails) => new
    {
        id = p.Id,
        name = p.Name,
        description = p.Description,
        enabled = p.Enabled,
        auto_send = p.AutoSend,
        schedule_cron = p.ScheduleCron,
        schedule_summary = CronSchedule.Describe(p.ScheduleCron),
        last_run_at = p.LastRunAt,
        next_run_at = p.NextRunAt,
        created_at = p.CreatedAt,
        updated_at = p.UpdatedAt,
        pending_mails = pendingMails,
        last_run = lastRun == null ? null : ToRunDto(lastRun, includeLog: false)
    };

    private static object ToDetailDto(Playbook p, PlaybookValidationResult? validation = null) => new
    {
        id = p.Id,
        name = p.Name,
        description = p.Description,
        enabled = p.Enabled,
        auto_send = p.AutoSend,
        schedule_cron = p.ScheduleCron,
        schedule_summary = CronSchedule.Describe(p.ScheduleCron),
        last_run_at = p.LastRunAt,
        next_run_at = p.NextRunAt,
        created_at = p.CreatedAt,
        updated_at = p.UpdatedAt,
        graph = PlaybookJson.Deserialize<PlaybookGraph>(p.GraphJson) ?? new PlaybookGraph(),
        validation = validation == null ? null : new
        {
            is_valid = validation.IsValid,
            errors = validation.Errors,
            warnings = validation.Warnings
        }
    };

    private static object ToRunDto(PlaybookRun r, bool includeLog) => new
    {
        id = r.Id,
        playbook_id = r.PlaybookId,
        started_at = r.StartedAt,
        finished_at = r.FinishedAt,
        status = r.Status,
        trigger_type = r.TriggerType,
        dry_run = r.DryRun,
        mails_sent = r.MailsSent,
        mails_pending = r.MailsPending,
        mails_failed = r.MailsFailed,
        mails_skipped = r.MailsSkipped,
        error_message = r.ErrorMessage,
        node_log = includeLog
            ? PlaybookJson.Deserialize<List<PlaybookNodeLog>>(r.NodeLogJson) ?? new List<PlaybookNodeLog>()
            : null
    };

    /// <summary>
    /// Report label for a mail row's origin. Metric mails record the node type rather than a
    /// weekly-flag criterion, so they get their own wording instead of a raw identifier.
    /// </summary>
    private static string? CriterionLabel(string? criterion) => criterion switch
    {
        null => null,
        PlaybookNodeType.SourceIncidentMetric => "Olay kaydı metriği (kurum toplamı)",
        PlaybookNodeType.SourceHighRiskUsers => "Haftalık yüksek skorlu kullanıcılar",
        PlaybookNodeType.SourceHighMaxMatchTransfers => "Yüksek maksimum eşleşmeli transferler",
        "top_permit_users" => "En çok Permit olay kaydı üretenler",
        "top_block_users" => "En çok Block olay kaydı üretenler",
        _ => WeeklyFlagCriterion.Label(criterion)
    };

    private static object ToMailDto(PlaybookMailLog m) => new
    {
        id = m.Id,
        run_id = m.RunId,
        playbook_id = m.PlaybookId,
        node_id = m.NodeId,
        user_email = m.UserEmail,
        full_name = m.FullName,
        team = m.Team,
        to_email = m.ToEmail,
        cc_email = m.CcEmail,
        subject = m.Subject,
        body_html = m.BodyHtml,
        correlation_code = m.CorrelationCode,
        template_id = m.TemplateId,
        template_name = m.TemplateName,
        template_match_reason = m.TemplateMatchReason,
        incident_summary_json = m.IncidentSummaryJson,
        source_criterion = m.SourceCriterion,
        source_criterion_label = CriterionLabel(m.SourceCriterion),
        trigger_count = m.TriggerCount,
        status = m.Status,
        created_at = m.CreatedAt,
        sent_at = m.SentAt,
        error_message = m.ErrorMessage
    };
}
