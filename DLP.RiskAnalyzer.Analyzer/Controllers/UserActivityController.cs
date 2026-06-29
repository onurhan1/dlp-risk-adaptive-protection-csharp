using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// User Activity Tracking API - Kullanıcı aktivitelerini takip eder
/// LDAP entegrasyonu geldiğinde AuthSource alanı ile LDAP/Local ayrımı yapılabilir
/// </summary>
[ApiController]
[Route("api/activity")]
public class UserActivityController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<UserActivityController> _logger;

    public UserActivityController(AnalyzerDbContext context, ILogger<UserActivityController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Track a user activity (page visit, action, etc.)
    /// </summary>
    [HttpPost("track")]
    public async Task<IActionResult> TrackActivity([FromBody] TrackActivityRequest request)
    {
        try
        {
            var activity = new UserActivityLog
            {
                Timestamp = DateTime.UtcNow,
                UserName = request.UserName ?? "Anonymous",
                AuthSource = request.AuthSource ?? "Local",
                ActivityType = request.ActivityType ?? "PageVisit",
                PagePath = request.PagePath,
                PageTitle = request.PageTitle,
                ActionDetail = request.ActionDetail,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].FirstOrDefault()
            };

            _context.UserActivityLogs.Add(activity);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = activity.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking user activity");
            return StatusCode(500, new { error = "Failed to track activity" });
        }
    }

    /// <summary>
    /// Update page leave (session duration)
    /// </summary>
    [HttpPost("page-leave")]
    public async Task<IActionResult> PageLeave([FromBody] PageLeaveRequest request)
    {
        try
        {
            if (request.ActivityId > 0)
            {
                var activity = await _context.UserActivityLogs.FindAsync(request.ActivityId);
                if (activity != null)
                {
                    activity.SessionDurationSeconds = request.DurationSeconds;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating page leave");
            return StatusCode(500, new { error = "Failed to update page leave" });
        }
    }

    /// <summary>
    /// Get activity logs with filtering and pagination
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetActivityLogs(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? userName,
        [FromQuery] string? activityType,
        [FromQuery] string? authSource,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;

            var query = _context.UserActivityLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(l => l.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(l => l.Timestamp <= endDate.Value);
            if (!string.IsNullOrWhiteSpace(userName))
                query = query.Where(l => l.UserName.Contains(userName));
            if (!string.IsNullOrWhiteSpace(activityType))
                query = query.Where(l => l.ActivityType == activityType);
            if (!string.IsNullOrWhiteSpace(authSource))
                query = query.Where(l => l.AuthSource == authSource);

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                logs,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activity logs");
            return StatusCode(500, new { detail = "An error occurred while fetching activity logs" });
        }
    }

    /// <summary>
    /// Get activity summary (most visited pages, active users, etc.)
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetActivitySummary(
        [FromQuery] int days = 7)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-days);

            var recentLogs = await _context.UserActivityLogs
                .Where(l => l.Timestamp >= since)
                .ToListAsync();

            // Most visited pages
            var topPages = recentLogs
                .Where(l => l.ActivityType == "PageVisit" && !string.IsNullOrEmpty(l.PagePath))
                .GroupBy(l => l.PagePath!)
                .Select(g => new { page = g.Key, title = g.First().PageTitle ?? g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToList();

            // Most active users
            var topUsers = recentLogs
                .GroupBy(l => l.UserName)
                .Select(g => new { user = g.Key, authSource = g.First().AuthSource, actions = g.Count() })
                .OrderByDescending(x => x.actions)
                .Take(10)
                .ToList();

            // Activity type breakdown
            var activityTypes = recentLogs
                .GroupBy(l => l.ActivityType)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            // Daily activity trend
            var dailyTrend = recentLogs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count() })
                .OrderBy(x => x.date)
                .ToList();

            // Auth source breakdown (prepared for LDAP)
            var authSources = recentLogs
                .GroupBy(l => l.AuthSource)
                .Select(g => new { source = g.Key, count = g.Count() })
                .ToList();

            return Ok(new
            {
                period = $"Last {days} days",
                totalActivities = recentLogs.Count,
                uniqueUsers = recentLogs.Select(l => l.UserName).Distinct().Count(),
                topPages,
                topUsers,
                activityTypes,
                dailyTrend,
                authSources
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activity summary");
            return StatusCode(500, new { detail = "An error occurred while fetching activity summary" });
        }
    }

    /// <summary>
    /// Get distinct activity types for filter dropdown
    /// </summary>
    [HttpGet("activity-types")]
    public async Task<IActionResult> GetActivityTypes()
    {
        try
        {
            var types = await _context.UserActivityLogs
                .Select(l => l.ActivityType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activity types");
            return StatusCode(500, new { detail = "An error occurred" });
        }
    }
}

public class TrackActivityRequest
{
    public string? UserName { get; set; }
    public string? AuthSource { get; set; }
    public string? ActivityType { get; set; }
    public string? PagePath { get; set; }
    public string? PageTitle { get; set; }
    public string? ActionDetail { get; set; }
}

public class PageLeaveRequest
{
    public int ActivityId { get; set; }
    public int DurationSeconds { get; set; }
}
