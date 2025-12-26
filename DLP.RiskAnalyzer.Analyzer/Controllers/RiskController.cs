using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DLP.RiskAnalyzer.Analyzer.Data;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RiskController : ControllerBase
{
    private readonly RiskAnalyzerService _riskAnalyzerService;
    private readonly AnalyzerDbContext _context;

    public RiskController(RiskAnalyzerService riskAnalyzerService, AnalyzerDbContext context)
    {
        _riskAnalyzerService = riskAnalyzerService;
        _context = context;
    }

    /// <summary>
    /// Get action summary (Block/Quarantine/Authorized counts)
    /// </summary>
    [HttpGet("action-summary")]
    public async Task<ActionResult<Dictionary<string, object>>> GetActionSummary(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int days = 30)
    {
        try
        {
            var query = _context.Incidents.AsQueryable();
            
            // Apply date filters
            if (startDate.HasValue)
                query = query.Where(i => i.Timestamp >= startDate.Value);
            else if (!endDate.HasValue)
                query = query.Where(i => i.Timestamp >= DateTime.UtcNow.AddDays(-days));
            
            if (endDate.HasValue)
                query = query.Where(i => i.Timestamp <= endDate.Value);

            // Count by action type
            var actionCounts = await query
                .GroupBy(i => i.Action ?? "UNKNOWN")
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .ToListAsync();

            var authorized = actionCounts.FirstOrDefault(a => a.Action.ToUpper() == "AUTHORIZED")?.Count ?? 0;
            var block = actionCounts.FirstOrDefault(a => a.Action.ToUpper() == "BLOCK" || a.Action.ToUpper() == "BLOCKED")?.Count ?? 0;
            var quarantine = actionCounts.FirstOrDefault(a => a.Action.ToUpper() == "QUARANTINE" || a.Action.ToUpper() == "QUARANTINED")?.Count ?? 0;
            var released = actionCounts.FirstOrDefault(a => a.Action.ToUpper() == "RELEASED")?.Count ?? 0;
            var unknown = actionCounts.FirstOrDefault(a => a.Action.ToUpper() == "UNKNOWN" || string.IsNullOrEmpty(a.Action))?.Count ?? 0;
            var total = authorized + block + quarantine + released + unknown;

            return Ok(new Dictionary<string, object>
            {
                { "authorized", authorized },
                { "block", block },
                { "quarantine", quarantine },
                { "released", released },
                { "unknown", unknown },
                { "total", total },
                { "actions", actionCounts.Select(a => new { action = a.Action, count = a.Count }).ToList() }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("trends")]
    public async Task<ActionResult<List<UserRiskTrend>>> GetUserRiskTrends(
        [FromQuery] int days = 30,
        [FromQuery] string? user = null)
    {
        try
        {
            var trends = await _riskAnalyzerService.GetUserRiskTrendsAsync(days, user);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("daily-summary")]
    public async Task<ActionResult<List<DailySummary>>> GetDailySummaries(
        [FromQuery] int days = 7)
    {
        try
        {
            var summaries = await _riskAnalyzerService.GetDailySummariesAsync(days);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Get high-risk users for a specific date (for Daily Trends popup)
    /// </summary>
    [HttpGet("high-risk-users")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetHighRiskUsers(
        [FromQuery] string date)
    {
        try
        {
            if (!DateTime.TryParse(date, out var parsedDate))
            {
                return BadRequest(new { detail = "Invalid date format. Use yyyy-MM-dd" });
            }

            var startOfDay = parsedDate.Date;
            var endOfDay = parsedDate.Date.AddDays(1);

            // Get all incidents for that day
            var incidents = await _context.Incidents
                .Where(i => i.Timestamp >= startOfDay && i.Timestamp < endOfDay)
                .ToListAsync();

            // Group by user and calculate max risk score
            var highRiskUsers = incidents
                .GroupBy(i => i.UserEmail)
                .Select(g => new
                {
                    UserEmail = g.Key,
                    LoginName = g.Where(i => !string.IsNullOrEmpty(i.LoginName))
                                 .Select(i => i.LoginName)
                                 .FirstOrDefault() ?? g.Key,
                    Department = g.Where(i => !string.IsNullOrEmpty(i.Department))
                                  .Select(i => i.Department)
                                  .FirstOrDefault() ?? "",
                    MaxRiskScore = g.Max(i => i.RiskScore ?? 0),
                    IncidentCount = g.Count()
                })
                .Where(u => u.MaxRiskScore >= 61)  // Legacy high threshold
                .OrderByDescending(u => u.MaxRiskScore)
                .ThenByDescending(u => u.IncidentCount)
                .ToList();

            var result = highRiskUsers.Select(u => new Dictionary<string, object>
            {
                { "user_email", u.UserEmail },
                { "login_name", u.LoginName },
                { "department", u.Department },
                { "max_risk_score", u.MaxRiskScore },
                { "incident_count", u.IncidentCount },
                { "risk_level", u.MaxRiskScore >= 91 ? "Critical" : u.MaxRiskScore >= 61 ? "High" : "Medium" }
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("department-summary")]
    public async Task<ActionResult<List<DepartmentSummary>>> GetDepartmentSummaries(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var summaries = await _riskAnalyzerService.GetDepartmentSummariesAsync(start, end);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("heatmap")]
    public async Task<ActionResult<RiskHeatmapData>> GetRiskHeatmap(
        [FromQuery] string dimension = "department",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var heatmap = await _riskAnalyzerService.GetRiskHeatmapAsync(dimension, start, end);
            return Ok(heatmap);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("channel-activity")]
    public async Task<ActionResult<Dictionary<string, object>>> GetChannelActivity(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int days = 30)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var activity = await _riskAnalyzerService.GetChannelActivityAsync(start, end, days);
            return Ok(activity);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("iob-detections")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetIOBDetections(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? category = null)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var detections = await _riskAnalyzerService.GetIOBDetectionsAsync(start, end, category);
            return Ok(detections);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("user-list")]
    [HttpGet("user-list/{page}/{page_size}")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserList(
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 15)
    {
        try
        {
            // Use RiskAnalyzerService which now returns correct format
            var result = await _riskAnalyzerService.GetUserListAsync(page, page_size);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("decay/simulation")]
    public Task<ActionResult<Dictionary<string, object>>> GetRiskDecaySimulation(
        [FromQuery] string userEmail,
        [FromQuery] int days = 30)
    {
        try
        {
            // Simulate risk score decay over time
            var currentScore = 80; // Placeholder - get from database
            var decayRate = 2; // 2 points per day
            
            var simulation = new List<Dictionary<string, object>>();
            for (int day = 0; day <= days; day++)
            {
                var decayedScore = Math.Max(0, currentScore - (day * decayRate));
                var riskLevel = decayedScore >= 91 ? "Critical" :
                               decayedScore >= 61 ? "High" :
                               decayedScore >= 41 ? "Medium" : "Low";
                
                simulation.Add(new Dictionary<string, object>
                {
                    { "day", day },
                    { "risk_score", decayedScore },
                    { "risk_level", riskLevel },
                    { "decay_percentage", (day * decayRate * 100.0 / currentScore) }
                });
            }

            return Task.FromResult<ActionResult<Dictionary<string, object>>>(Ok(new Dictionary<string, object>
            {
                { "user_email", userEmail },
                { "initial_score", currentScore },
                { "decay_rate", decayRate },
                { "simulation", simulation }
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ActionResult<Dictionary<string, object>>>(StatusCode(500, new { detail = ex.Message }));
        }
    }

    /// <summary>
    /// Get top users by day with their daily alert counts (for Investigation 30 days - Risky Users tab)
    /// </summary>
    [HttpGet("top-users-daily")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetTopUsersByDay(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 20,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _riskAnalyzerService.GetTopUsersByDayAsync(days, limit, startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Get top rules by day with their daily alert counts (for Investigation 30 days - Alerts tab)
    /// </summary>
    [HttpGet("top-rules-daily")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetTopRulesByDay(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _riskAnalyzerService.GetTopRulesByDayAsync(days, limit, startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Get comprehensive daily report data for Reports page
    /// </summary>
    [HttpGet("daily-report-data")]
    public async Task<ActionResult<Dictionary<string, object>>> GetDailyReportData(
        [FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow.Date;
            var result = await _riskAnalyzerService.GetDailyReportDataAsync(targetDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Get incidents filtered by action type for Action Summary modal
    /// </summary>
    [HttpGet("incidents/by-action")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetIncidentsByAction(
        [FromQuery] string action,
        [FromQuery] string? date = null,
        [FromQuery] string? start_date = null,
        [FromQuery] string? end_date = null)
    {
        try
        {
            // Validate action parameter
            var validActions = new[] { "BLOCK", "BLOCKED", "QUARANTINE", "QUARANTINED", "AUTHORIZED", "RELEASED", "TOTAL" };
            var normalizedAction = action?.ToUpper();
            
            if (string.IsNullOrEmpty(normalizedAction) || !validActions.Contains(normalizedAction))
            {
                return BadRequest(new { detail = "Invalid action parameter. Must be one of: BLOCK, QUARANTINE, AUTHORIZED, RELEASED, TOTAL" });
            }

            // Parse date range - support both single date and date range
            DateTime startOfRange;
            DateTime endOfRange;
            
            if (!string.IsNullOrEmpty(start_date) && !string.IsNullOrEmpty(end_date))
            {
                // Use date range if both provided
                if (DateTime.TryParse(start_date, out var parsedStart) && DateTime.TryParse(end_date, out var parsedEnd))
                {
                    startOfRange = parsedStart.Date;
                    endOfRange = parsedEnd.Date.AddDays(1); // End of that day
                }
                else
                {
                    return BadRequest(new { detail = "Invalid date format. Use yyyy-MM-dd" });
                }
            }
            else if (!string.IsNullOrEmpty(date))
            {
                // Single date (backward compatible)
                if (DateTime.TryParse(date, out var parsedDate))
                {
                    startOfRange = parsedDate.Date;
                    endOfRange = parsedDate.Date.AddDays(1);
                }
                else
                {
                    return BadRequest(new { detail = "Invalid date format. Use yyyy-MM-dd" });
                }
            }
            else
            {
                // Default to today
                startOfRange = DateTime.UtcNow.Date;
                endOfRange = startOfRange.AddDays(1);
            }

            // Query incidents - TOTAL returns all incidents, others filter by action
            List<Incident> incidents;
            
            if (normalizedAction == "TOTAL")
            {
                // Return all incidents for the date range
                incidents = await _context.Incidents
                    .Where(i => i.Timestamp >= startOfRange && i.Timestamp < endOfRange)
                    .OrderByDescending(i => i.Timestamp)
                    .ToListAsync();
            }
            else
            {
                // Filter by specific action
                incidents = await _context.Incidents
                    .Where(i => i.Timestamp >= startOfRange && i.Timestamp < endOfRange)
                    .Where(i => i.Action != null && 
                               (i.Action.ToUpper() == normalizedAction || 
                                (normalizedAction == "BLOCK" && i.Action.ToUpper() == "BLOCKED") ||
                                (normalizedAction == "QUARANTINE" && i.Action.ToUpper() == "QUARANTINED")))
                    .OrderByDescending(i => i.Timestamp)
                    .ToListAsync();
            }

            // Format response
            var result = incidents.Select(i =>
            {
                // Extract rule name and max matches from ViolationTriggers
                string ruleName = "N/A";
                int maxMatches = i.MaxMatches;
                
                if (!string.IsNullOrEmpty(i.ViolationTriggers))
                {
                    try
                    {
                        var triggers = System.Text.Json.JsonDocument.Parse(i.ViolationTriggers);
                        if (triggers.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                            triggers.RootElement.GetArrayLength() > 0)
                        {
                            var firstTrigger = triggers.RootElement[0];
                            if (firstTrigger.TryGetProperty("RuleName", out var ruleNameElement))
                            {
                                ruleName = ruleNameElement.GetString() ?? "N/A";
                            }
                            
                            // Extract max matches from all classifiers if not already set
                            if (maxMatches == 0)
                            {
                                foreach (var trigger in triggers.RootElement.EnumerateArray())
                                {
                                    if (trigger.TryGetProperty("Classifiers", out var classifiers) &&
                                        classifiers.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var classifier in classifiers.EnumerateArray())
                                        {
                                            if (classifier.TryGetProperty("NumberMatches", out var matchesElement))
                                            {
                                                var matches = matchesElement.GetInt32();
                                                if (matches > maxMatches) maxMatches = matches;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If parsing fails, use policy name as fallback
                        ruleName = i.Policy ?? "N/A";
                    }
                }
                
                return new Dictionary<string, object>
                {
                    { "login_name", i.LoginName ?? i.UserEmail ?? "N/A" },
                    { "destination", i.Destination ?? "N/A" },
                    { "channel", i.Channel ?? "N/A" },
                    { "policy", i.Policy ?? "N/A" },
                    { "rule_name", ruleName },
                    { "action", i.Action ?? "N/A" },
                    { "timestamp", i.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") },
                    { "max_matches", maxMatches },
                    { "violation_triggers", i.ViolationTriggers ?? "" }
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}