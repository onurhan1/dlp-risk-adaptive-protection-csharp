using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Controller for managing Mercek (Help Desk) Incidents
/// Provides endpoints for retrieving, filtering, and analyzing incident data
/// </summary>
[ApiController]
[Route("api/mercek")]
public class MercekController : ControllerBase
{
    private readonly AnalyzerDbContext _dbContext;
    private readonly ILogger<MercekController> _logger;

    public MercekController(AnalyzerDbContext dbContext, ILogger<MercekController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated Mercek incidents with optional filtering
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (default: 10, max: 1000)</param>
    /// <param name="userName">Filter by user name (partial match)</param>
    /// <param name="assignedUserCode">Filter by assigned user code</param>
    /// <param name="statusId">Filter by status ID</param>
    /// <param name="startDate">Filter by incidents opened after this date</param>
    /// <param name="endDate">Filter by incidents opened before this date</param>
    /// <param name="searchTerm">Search in summary and incident description</param>
    /// <returns>Paginated list of Mercek incidents</returns>
    [HttpGet]
    public async Task<ActionResult<MercekIncidentResponse>> GetIncidents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? userName = null,
        [FromQuery] string? assignedUserCode = null,
        [FromQuery] string? statusId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 1000) pageSize = 1000;

            // Build query
            var query = _dbContext.MercekIncidents.AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(userName))
            {
                query = query.Where(m => m.UserName != null && m.UserName.Contains(userName));
            }

            if (!string.IsNullOrWhiteSpace(assignedUserCode))
            {
                query = query.Where(m => m.AssignedUserCode == assignedUserCode);
            }

            if (!string.IsNullOrWhiteSpace(statusId))
            {
                query = query.Where(m => m.StatusId == statusId);
            }

            if (startDate.HasValue)
            {
                query = query.Where(m => m.OpenDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.OpenDate <= endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Search by incident_id (numeric), user_name, assigned_user_code
                if (int.TryParse(searchTerm, out var incidentIdSearch))
                {
                    query = query.Where(m => 
                        m.IncidentId == incidentIdSearch ||
                        (m.UserName != null && m.UserName.Contains(searchTerm)) ||
                        (m.AssignedUserCode != null && m.AssignedUserCode.Contains(searchTerm))
                    );
                }
                else
                {
                    query = query.Where(m => 
                        (m.UserName != null && m.UserName.Contains(searchTerm)) ||
                        (m.AssignedUserCode != null && m.AssignedUserCode.Contains(searchTerm))
                    );
                }
            }

            // Get total count
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Get paginated data
            var items = await query
                .OrderByDescending(m => m.OpenDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new MercekIncidentResponse
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = page < totalPages,
                HasPreviousPage = page > 1
            };

            _logger.LogInformation(
                "Retrieved {Count} Mercek incidents (Page {Page} of {TotalPages})",
                items.Count, page, totalPages);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Mercek incidents");
            return StatusCode(500, new { error = "An error occurred while retrieving incidents" });
        }
    }

    /// <summary>
    /// Get a specific Mercek incident by ID
    /// </summary>
    [HttpGet("{incidentId}")]
    public async Task<ActionResult<MercekIncident>> GetIncident(int incidentId)
    {
        try
        {
            var incident = await _dbContext.MercekIncidents
                .FirstOrDefaultAsync(m => m.IncidentId == incidentId);

            if (incident == null)
            {
                return NotFound(new { error = $"Incident {incidentId} not found" });
            }

            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incident {IncidentId}", incidentId);
            return StatusCode(500, new { error = "An error occurred while retrieving the incident" });
        }
    }

    /// <summary>
    /// Get filter options (unique values for dropdowns)
    /// </summary>
    [HttpGet("filters")]
    public async Task<ActionResult<MercekFilterOptions>> GetFilterOptions()
    {
        try
        {
            var filters = new MercekFilterOptions
            {
                Users = await _dbContext.MercekIncidents
                    .Where(m => m.UserName != null)
                    .Select(m => m.UserName!)
                    .Distinct()
                    .OrderBy(u => u)
                    .ToListAsync(),

                AssignedUsers = await _dbContext.MercekIncidents
                    .Where(m => m.AssignedUserCode != null)
                    .Select(m => m.AssignedUserCode!)
                    .Distinct()
                    .OrderBy(u => u)
                    .ToListAsync(),

                StatusIds = await _dbContext.MercekIncidents
                    .Where(m => m.StatusId != null)
                    .Select(m => m.StatusId!)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync(),

                CategoryIds = await _dbContext.MercekIncidents
                    .Where(m => m.CategoryId.HasValue)
                    .Select(m => m.CategoryId!.Value)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync()
            };

            // Get date range
            var dateRange = await _dbContext.MercekIncidents
                .Where(m => m.OpenDate.HasValue)
                .GroupBy(m => 1)
                .Select(g => new 
                {
                    Min = g.Min(m => m.OpenDate),
                    Max = g.Max(m => m.OpenDate)
                })
                .FirstOrDefaultAsync();

            if (dateRange != null)
            {
                filters.MinDate = dateRange.Min;
                filters.MaxDate = dateRange.Max;
            }

            return Ok(filters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving filter options");
            return StatusCode(500, new { error = "An error occurred while retrieving filter options" });
        }
    }

    /// <summary>
    /// Get comprehensive statistics for Mercek incidents (for charts and cards)
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<object>> GetStatistics(
        [FromQuery] string? userName = null,
        [FromQuery] string? assignedUserCode = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var query = _dbContext.MercekIncidents.AsQueryable();

            // Apply same filters as main list
            if (!string.IsNullOrWhiteSpace(userName))
                query = query.Where(m => m.UserName != null && m.UserName.Contains(userName));
            if (!string.IsNullOrWhiteSpace(assignedUserCode))
                query = query.Where(m => m.AssignedUserCode == assignedUserCode);
            if (startDate.HasValue)
                query = query.Where(m => m.OpenDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(m => m.OpenDate <= endDate.Value);

            var totalIncidents = await query.CountAsync();
            var openIncidents = await query.Where(m => m.CloseDate == null).CountAsync();
            var closedIncidents = totalIncidents - openIncidents;

            // Average resolution time (client-side calculation for dates)
            var closedWithDates = await query
                .Where(m => m.OpenDate.HasValue && m.CloseDate.HasValue)
                .Select(m => new { m.OpenDate, m.CloseDate })
                .ToListAsync();

            var avgResolutionDays = closedWithDates.Any()
                ? closedWithDates.Average(m => (m.CloseDate!.Value - m.OpenDate!.Value).TotalDays)
                : 0;

            // Weekly comparison
            var now = DateTime.UtcNow;
            var oneWeekAgo = now.AddDays(-7);
            var twoWeeksAgo = now.AddDays(-14);

            var lastWeekCount = await query
                .Where(m => m.OpenDate.HasValue && m.OpenDate >= oneWeekAgo && m.OpenDate <= now)
                .CountAsync();
            var previousWeekCount = await query
                .Where(m => m.OpenDate.HasValue && m.OpenDate >= twoWeeksAgo && m.OpenDate < oneWeekAgo)
                .CountAsync();

            // Daily counts for timeline chart (last 90 days or all data)
            var dailyCounts = await query
                .Where(m => m.OpenDate.HasValue)
                .GroupBy(m => m.OpenDate!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // User distribution (all users, sorted by count)
            var userDistribution = await query
                .Where(m => m.UserName != null)
                .GroupBy(m => m.UserName)
                .Select(g => new { User = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            // Assigned user distribution (all users, sorted by count)
            var assignedUserDistribution = await query
                .Where(m => m.AssignedUserCode != null)
                .GroupBy(m => m.AssignedUserCode)
                .Select(g => new { User = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var stats = new
            {
                TotalIncidents = totalIncidents,
                OpenIncidents = openIncidents,
                ClosedIncidents = closedIncidents,
                AverageResolutionDays = Math.Round(avgResolutionDays, 2),
                LastWeekCount = lastWeekCount,
                PreviousWeekCount = previousWeekCount,
                DailyCounts = dailyCounts.Select(d => new { date = d.Date.ToString("yyyy-MM-dd"), count = d.Count }),
                UserDistribution = userDistribution.Select(u => new { label = u.User ?? "Bilinmiyor", count = u.Count }),
                AssignedUserDistribution = assignedUserDistribution.Select(u => new { label = u.User ?? "Bilinmiyor", count = u.Count })
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            return StatusCode(500, new { error = "An error occurred while retrieving statistics" });
        }
    }
}
