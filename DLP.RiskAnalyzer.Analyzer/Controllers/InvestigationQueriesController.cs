using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/investigation/queries")]
public class InvestigationQueriesController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<InvestigationQueriesController> _logger;

    public InvestigationQueriesController(
        AnalyzerDbContext context,
        ILogger<InvestigationQueriesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] string? status = null, [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var query = _context.InvestigationQueries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.QueryStatus == status);

        var rows = await query
            .OrderByDescending(q => q.QueryDate ?? q.CreatedAt)
            .Take(Math.Clamp(limit, 1, 2000))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult> SaveBulk([FromBody] BulkQueryRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);

        var now = DateTime.UtcNow;
        var actor = User?.Identity?.Name ?? "System";
        var saved = 0;

        foreach (var row in request.Rows)
        {
            InvestigationQueryRecord entity;
            if (row.Id.HasValue && row.Id.Value > 0)
            {
                entity = await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.Id == row.Id.Value, ct)
                    ?? new InvestigationQueryRecord { CreatedAt = now, CreatedBy = actor };
            }
            else
            {
                entity = new InvestigationQueryRecord { CreatedAt = now, CreatedBy = actor };
                _context.InvestigationQueries.Add(entity);
            }

            Apply(row, entity, now, actor);
            saved++;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, saved });
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
        await _context.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] QueryRecordRequest request, CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var entity = await _context.InvestigationQueries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new KeyNotFoundException($"Sorgu kaydı bulunamadı: {id}");

        Apply(request, entity, DateTime.UtcNow, User?.Identity?.Name ?? "System");
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

    private static void Apply(QueryRecordRequest row, InvestigationQueryRecord entity, DateTime now, string actor)
    {
        var mail = row.MailAddress?.Trim() ?? string.Empty;
        entity.FullName = string.IsNullOrWhiteSpace(row.FullName)
            ? TurkishNameHelper.FromEmailLocalPart(mail)
            : TurkishNameHelper.ToTurkishTitle(row.FullName);
        entity.MailAddress = mail;
        entity.Subject = row.Subject?.Trim() ?? string.Empty;
        entity.QueryDate = row.QueryDate;
        entity.ResponseStatus = row.ResponseStatus?.Trim() ?? string.Empty;
        entity.Action = row.Action?.Trim() ?? string.Empty;
        entity.QueryStatus = string.IsNullOrWhiteSpace(row.QueryStatus) ? InvestigationQueryStatus.Pending : row.QueryStatus.Trim();
        entity.Source = row.Source?.Trim();
        entity.Team = row.Team?.Trim();
        entity.Notes = row.Notes?.Trim();
        entity.PlaybookMailLogId = row.PlaybookMailLogId;
        entity.ExtraJson = string.IsNullOrWhiteSpace(row.ExtraJson) ? "{}" : row.ExtraJson;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actor;
    }
}

public class BulkQueryRequest
{
    public List<QueryRecordRequest> Rows { get; set; } = [];
}

public class QueryRecordRequest
{
    public int? Id { get; set; }
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
