using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/investigation/queries")]
public class InvestigationQueriesController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly IInvestigationQueryRemediationSyncService _remediationSync;
    private readonly IInvestigationMailAutomationService _mailAutomation;
    private readonly ILogger<InvestigationQueriesController> _logger;

    public InvestigationQueriesController(
        AnalyzerDbContext context,
        IInvestigationQueryRemediationSyncService remediationSync,
        IInvestigationMailAutomationService mailAutomation,
        ILogger<InvestigationQueriesController> logger)
    {
        _context = context;
        _remediationSync = remediationSync;
        _mailAutomation = mailAutomation;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] string? status = null, [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var query = _context.InvestigationQueries
            .AsNoTracking()
            .Where(q => !(q.Source == "agentic_workflow" && q.Notes == PlaybookNodeType.SourceIncidentMetric));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.QueryStatus == status);

        var rows = await query
            .OrderByDescending(q => q.QueryDate ?? q.CreatedAt)
            .Take(Math.Clamp(limit, 1, 2000))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("workflow-mails")]
    public async Task<ActionResult> GetWorkflowMails([FromQuery] int limit = 500, CancellationToken ct = default)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var rows = await (
            from mail in _context.PlaybookMailLogs.AsNoTracking()
            join playbook in _context.Playbooks.AsNoTracking()
                on mail.PlaybookId equals playbook.Id into playbooks
            from playbook in playbooks.DefaultIfEmpty()
            where mail.UserEmail == "(rapor)" || mail.SourceCriterion == PlaybookNodeType.SourceIncidentMetric
            orderby (mail.SentAt ?? mail.CreatedAt) descending
            select new
            {
                id = mail.Id,
                run_id = mail.RunId,
                playbook_id = mail.PlaybookId,
                playbook_name = playbook != null ? playbook.Name : $"Workflow #{mail.PlaybookId}",
                to_email = mail.ToEmail,
                cc_email = mail.CcEmail,
                subject = mail.Subject,
                mail_date = mail.SentAt ?? mail.CreatedAt,
                status = mail.Status,
                source = "Incident metriği (kurum toplamı)",
                trigger_count = mail.TriggerCount,
                error_message = mail.ErrorMessage
            })
            .Take(Math.Clamp(limit, 1, 2000))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("automation/process-inbox")]
    public async Task<ActionResult> ProcessInbox(CancellationToken ct = default) =>
        Ok(await _mailAutomation.ProcessInboxAsync(ct));

    [HttpGet("reply-review-count")]
    public async Task<ActionResult> GetReplyReviewCount(CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var count = await _context.InvestigationQueries
            .CountAsync(q => q.QueryStatus == InvestigationQueryStatus.ReplyReview, ct);
        return Ok(new { count });
    }

    [HttpPost("bulk")]
    public async Task<ActionResult> SaveBulk([FromBody] BulkQueryRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var rows = request.Rows ?? [];
        if (rows.Count > 2_000)
            return BadRequest(new { detail = "Tek seferde en fazla 2000 sorgu kaydi kaydedilebilir." });

        var now = DateTime.UtcNow;
        var actor = User?.Identity?.Name ?? "System";
        var saved = 0;
        var skippedDuplicates = 0;
        var changedRows = new List<InvestigationQueryRecord>();
        var requestIds = new HashSet<int>();
        var existingKeys = (await _context.InvestigationQueries
                .AsNoTracking()
                .Select(q => new { q.UserCode, q.FullName, q.QueryDate })
                .ToListAsync(ct))
            .Select(q => DuplicateKey(q.UserCode, q.FullName, q.QueryDate))
            .Where(key => key != null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
        var requestKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var duplicateKey = DuplicateKey(row.UserCode, row.FullName, row.QueryDate);
            var existingId = row.Id.GetValueOrDefault();
            InvestigationQueryRecord? entity = null;

            if (existingId > 0)
            {
                if (!requestIds.Add(existingId))
                {
                    skippedDuplicates++;
                    continue;
                }

                entity = await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.Id == existingId, ct);
            }

            if (entity == null && duplicateKey != null &&
                (!requestKeys.Add(duplicateKey) || existingKeys.Contains(duplicateKey)))
            {
                skippedDuplicates++;
                continue;
            }

            if (entity == null)
            {
                entity = new InvestigationQueryRecord { CreatedAt = now, CreatedBy = actor };
                _context.InvestigationQueries.Add(entity);
            }

            Apply(row, entity, now, actor);
            changedRows.Add(entity);
            if (duplicateKey != null) existingKeys.Add(duplicateKey);
            saved++;
        }

        await _context.SaveChangesAsync(ct);
        var remediationsSynced = await _remediationSync.SyncAsync(changedRows, actor, now, ct);
        if (remediationsSynced > 0)
            await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, saved, skippedDuplicates, remediationsSynced });
    }

    [HttpPost("bulk-delete")]
    public async Task<ActionResult> DeleteBulk([FromBody] BulkDeleteQueryRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var ids = (request.Ids ?? [])
            .Where(id => id > 0)
            .Distinct()
            .Take(2_000)
            .ToList();
        if (ids.Count == 0)
            return BadRequest(new { detail = "Silinecek kayit secilmedi." });

        var records = await _context.InvestigationQueries
            .Where(query => ids.Contains(query.Id))
            .ToListAsync(ct);
        _context.InvestigationQueries.RemoveRange(records);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, deleted = records.Count });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] QueryRecordRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var now = DateTime.UtcNow;
        var actor = User?.Identity?.Name ?? "System";
        var entity = new InvestigationQueryRecord { CreatedAt = now, CreatedBy = actor };
        Apply(request, entity, now, actor);
        _context.InvestigationQueries.Add(entity);
        await _remediationSync.SyncAsync([entity], actor, now, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] QueryRecordRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var entity = await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new KeyNotFoundException($"Sorgu kaydı bulunamadı: {id}");

        var now = DateTime.UtcNow;
        var actor = User?.Identity?.Name ?? "System";
        Apply(request, entity, now, actor);
        await _remediationSync.SyncAsync([entity], actor, now, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var entity = await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity == null) return NotFound(new { detail = "Sorgu kaydı bulunamadı" });

        _context.InvestigationQueries.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/review-reply")]
    public async Task<ActionResult> ReviewReply(int id, [FromBody] ReplyReviewRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var entity = await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity == null) return NotFound(new { detail = "Sorgu kaydi bulunamadi" });
        if (entity.ReplyReceivedAt == null)
            return BadRequest(new { detail = "Bu sorgu icin incelenecek bir cevap bulunamadi" });

        var now = DateTime.UtcNow;
        entity.ReviewNote = request.Note?.Trim();
        entity.QueryStatus = request.IsSufficient
            ? InvestigationQueryStatus.Completed
            : InvestigationQueryStatus.Queried;
        entity.ResponseStatus = request.IsSufficient
            ? "Cevap yeterli bulundu"
            : "Cevap yetersiz bulundu";
        entity.Action = request.IsSufficient
            ? "Analist cevabi yeterli buldu ve sorguyu kapatti"
            : "Analist ek aksiyon veya tekrar sorgulama bekliyor";
        entity.UpdatedAt = now;
        entity.UpdatedBy = User?.Identity?.Name ?? "Analist";
        await _remediationSync.SyncAsync([entity], entity.UpdatedBy, now, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(entity);
    }

    private static void Apply(QueryRecordRequest row, InvestigationQueryRecord entity, DateTime now, string actor)
    {
        var mail = row.MailAddress?.Trim() ?? string.Empty;
        entity.UserCode = row.UserCode?.Trim() ?? string.Empty;
        entity.FullName = string.IsNullOrWhiteSpace(row.FullName)
            ? TurkishNameHelper.FromEmailLocalPart(mail)
            : TurkishNameHelper.ToTurkishTitle(row.FullName);
        entity.MailAddress = mail;
        entity.Subject = row.Subject?.Trim() ?? string.Empty;
        entity.QueryDate = row.QueryDate;
        entity.ResponseStatus = row.ResponseStatus?.Trim() ?? string.Empty;
        entity.Action = row.Action?.Trim() ?? string.Empty;
        entity.QueryStatus = InvestigationQueryRemediationSyncService.NormalizeQueryStatus(row.QueryStatus);
        entity.Source = row.Source?.Trim();
        entity.Team = row.Team?.Trim();
        entity.Notes = row.Notes?.Trim();
        entity.PlaybookMailLogId = row.PlaybookMailLogId;
        entity.ExtraJson = string.IsNullOrWhiteSpace(row.ExtraJson) ? "{}" : row.ExtraJson;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actor;
    }

    private static string? DuplicateKey(string? userCode, string? fullName, DateTime? queryDate)
    {
        if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(fullName) || !queryDate.HasValue)
            return null;

        return string.Join('|',
            userCode.Trim().ToUpperInvariant(),
            fullName.Trim().ToUpperInvariant(),
            queryDate.Value.Date.ToString("yyyy-MM-dd"));
    }
}

public class BulkQueryRequest
{
    public List<QueryRecordRequest> Rows { get; set; } = [];
}

public class BulkDeleteQueryRequest
{
    public List<int> Ids { get; set; } = [];
}

public class QueryRecordRequest
{
    public int? Id { get; set; }
    public string? UserCode { get; set; }
    public string? FullName { get; set; }
    public string? MailAddress { get; set; }
    public string? Subject { get; set; }
    public DateTime? QueryDate { get; set; }
    public string? ResponseStatus { get; set; }
    public string? Action { get; set; }
    public string? QueryStatus { get; set; }
    public string? Source { get; set; }
    public string? Team { get; set; }
    public string? Notes { get; set; }
    public int? PlaybookMailLogId { get; set; }
    public string? ExtraJson { get; set; }
}

public class ReplyReviewRequest
{
    public bool IsSufficient { get; set; }
    public string? Note { get; set; }
}
