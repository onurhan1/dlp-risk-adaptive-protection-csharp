using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/released-incidents")]
public class ReleasedIncidentsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly IReleasedIncidentSyncService _syncService;
    private readonly ILogger<ReleasedIncidentsController> _logger;

    public ReleasedIncidentsController(
        AnalyzerDbContext context,
        IReleasedIncidentSyncService syncService,
        ILogger<ReleasedIncidentsController> logger)
    {
        _context = context;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Tüm released incident'ları listeler (filtreleme + sayfalama destekli)
    /// GET /api/released-incidents?admin=&startDate=&endDate=&page=1&pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll(
        [FromQuery] string? admin,
        [FromQuery] long? incidentId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string orderBy = "update_time_desc")
    {
        try
        {
            var query = _context.ReleasedIncidents.AsQueryable();

            // Filters
            if (!string.IsNullOrEmpty(admin))
            {
                var adminLower = admin.ToLower();
                query = query.Where(r => r.AdminName != null && r.AdminName.ToLower() == adminLower);
            }

            if (incidentId.HasValue)
            {
                query = query.Where(r => r.IncidentId == incidentId.Value);
            }

            if (startDate.HasValue)
            {
                var utcStart = startDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                    : startDate.Value.ToUniversalTime();
                query = query.Where(r => r.UpdateTime >= utcStart || r.IncidentTimestamp >= utcStart);
            }

            if (endDate.HasValue)
            {
                var utcEnd = endDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                    : endDate.Value.ToUniversalTime();
                query = query.Where(r => r.UpdateTime <= utcEnd || r.IncidentTimestamp <= utcEnd);
            }

            // Total count for pagination
            var totalCount = await query.CountAsync();

            // Ordering
            query = orderBy switch
            {
                "update_time_asc" => query.OrderBy(r => r.UpdateTime),
                "incident_time_desc" => query.OrderByDescending(r => r.IncidentTimestamp),
                "incident_time_asc" => query.OrderBy(r => r.IncidentTimestamp),
                "incident_id_desc" => query.OrderByDescending(r => r.IncidentId),
                "incident_id_asc" => query.OrderBy(r => r.IncidentId),
                _ => query.OrderByDescending(r => r.UpdateTime)
            };

            // Pagination
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                data = items.Select(r => new
                {
                    id = r.Id,
                    incident_id = r.IncidentId,
                    incident_timestamp = r.IncidentTimestamp,
                    action = r.Action,
                    task_name = r.TaskName,
                    admin_name = r.AdminName,
                    comments = r.Comments,
                    update_time = r.UpdateTime,
                    created_at = r.CreatedAt
                }),
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching released incidents");
            return StatusCode(500, new { detail = "An error occurred while fetching released incidents" });
        }
    }

    /// <summary>
    /// Belirli bir incident ID'ye ait tüm release geçmişini döndürür
    /// GET /api/released-incidents/by-incident/12345
    /// </summary>
    [HttpGet("by-incident/{incidentId}")]
    public async Task<ActionResult> GetByIncidentId(long incidentId)
    {
        try
        {
            var releases = await _context.ReleasedIncidents
                .Where(r => r.IncidentId == incidentId)
                .OrderByDescending(r => r.UpdateTime)
                .ToListAsync();

            return Ok(releases.Select(r => new
            {
                id = r.Id,
                incident_id = r.IncidentId,
                incident_timestamp = r.IncidentTimestamp,
                action = r.Action,
                task_name = r.TaskName,
                admin_name = r.AdminName,
                comments = r.Comments,
                update_time = r.UpdateTime,
                created_at = r.CreatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching released incidents for incident {IncidentId}", incidentId);
            return StatusCode(500, new { detail = "An error occurred while fetching released incidents" });
        }
    }

    /// <summary>
    /// Tek bir released incident kaydını döndürür
    /// GET /api/released-incidents/5
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(int id)
    {
        try
        {
            var release = await _context.ReleasedIncidents.FindAsync(id);
            if (release == null)
                return NotFound(new { detail = $"Released incident with id {id} not found" });

            return Ok(new
            {
                id = release.Id,
                incident_id = release.IncidentId,
                incident_timestamp = release.IncidentTimestamp,
                action = release.Action,
                task_name = release.TaskName,
                admin_name = release.AdminName,
                comments = release.Comments,
                update_time = release.UpdateTime,
                created_at = release.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching released incident {Id}", id);
            return StatusCode(500, new { detail = "An error occurred while fetching released incident" });
        }
    }

    /// <summary>
    /// Özet istatistikleri döndürür
    /// GET /api/released-incidents/summary?days=30
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary([FromQuery] int days = 30)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-days);

            var allReleases = await _context.ReleasedIncidents
                .Where(r => r.UpdateTime >= since || r.IncidentTimestamp >= since)
                .ToListAsync();

            var totalCount = allReleases.Count;
            var uniqueIncidents = allReleases.Select(r => r.IncidentId).Distinct().Count();
            var uniqueAdmins = allReleases.Where(r => r.AdminName != null).Select(r => r.AdminName).Distinct().Count();

            // Admin breakdown
            var byAdmin = allReleases
                .Where(r => !string.IsNullOrEmpty(r.AdminName))
                .GroupBy(r => r.AdminName!)
                .Select(g => new { admin = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            // Daily trend
            var dailyTrend = allReleases
                .Where(r => r.UpdateTime.HasValue)
                .GroupBy(r => r.UpdateTime!.Value.Date)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count() })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                days,
                total_count = totalCount,
                unique_incidents = uniqueIncidents,
                unique_admins = uniqueAdmins,
                by_admin = byAdmin,
                daily_trend = dailyTrend
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching released incidents summary");
            return StatusCode(500, new { detail = "An error occurred while fetching summary" });
        }
    }

    /// <summary>
    /// Admin listesini döndürür (frontend dropdown için)
    /// GET /api/released-incidents/admins
    /// </summary>
    [HttpGet("admins")]
    public async Task<ActionResult> GetAdmins()
    {
        try
        {
            var admins = await _context.ReleasedIncidents
                .Where(r => r.AdminName != null && r.AdminName != "")
                .Select(r => r.AdminName!)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            return Ok(admins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching admin list");
            return StatusCode(500, new { detail = "An error occurred while fetching admins" });
        }
    }

    /// <summary>
    /// DLP API'den released incident verilerini çekip veritabanına kaydeder (manuel tetikleme).
    /// POST /api/released-incidents/sync?lookbackHours=24
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult> SyncFromDlpApi([FromQuery] int lookbackHours = 24)
    {
        try
        {
            _logger.LogInformation("Manual released incident sync triggered: {LookbackHours}h lookback", lookbackHours);

            var result = await _syncService.SyncAsync(lookbackHours);

            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            return Ok(new
            {
                success = result.Success,
                total_fetched = result.TotalFetched,
                released_found = result.ReleasedFound,
                inserted = result.Inserted,
                skipped = result.Skipped,
                message = result.Success
                    ? $"{result.Inserted} yeni released incident eklendi ({result.Skipped} zaten mevcuttu)"
                    : "İşlem tamamlandı ancak released incident bulunamadı"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual released incident sync failed");
            return StatusCode(500, new { detail = "Released incident sync sırasında hata oluştu", error = ex.Message });
        }
    }
}
