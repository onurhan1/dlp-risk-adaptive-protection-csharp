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
            
            // Apply date filters only if explicitly provided
            if (startDate.HasValue)
                query = query.Where(i => i.Timestamp >= startDate.Value);
            
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
                .Where(u => u.MaxRiskScore >= 500)  // High risk threshold in 1000-scale (50+ when normalized to 0-100)
                .OrderByDescending(u => u.MaxRiskScore)
                .ThenByDescending(u => u.IncidentCount)
                .ToList();

            var result = highRiskUsers.Select(u => new Dictionary<string, object>
            {
                { "user_email", u.UserEmail },
                { "login_name", u.LoginName },
                { "department", u.Department },
                { "max_risk_score", u.MaxRiskScore },
                { "incident_count", u.IncidentCount }
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
        [FromQuery] int page_size = 15,
        [FromQuery] string? search = null)
    {
        try
        {
            // Use RiskAnalyzerService which now returns correct format
            var result = await _riskAnalyzerService.GetUserListAsync(page, page_size, search);
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
    /// Get filter options for Action Incidents modal (unique values for dropdowns)
    /// </summary>
    [HttpGet("incidents/filter-options")]
    public async Task<ActionResult<object>> GetFilterOptions(
        [FromQuery] string? action = null)
    {
        try
        {
            // Get all incidents (optionally filtered by action)
            var query = _context.Incidents.AsQueryable();
            
            if (!string.IsNullOrEmpty(action) && action.ToUpper() != "TOTAL")
            {
                var normalizedAction = action.ToUpper();
                query = query.Where(i => i.Action != null && 
                           (i.Action.ToUpper() == normalizedAction || 
                            (normalizedAction == "BLOCK" && i.Action.ToUpper() == "BLOCKED") ||
                            (normalizedAction == "QUARANTINE" && i.Action.ToUpper() == "QUARANTINED")));
            }

            // Get unique values efficiently
            var users = await query
                .Where(i => i.LoginName != null && i.LoginName != "")
                .Select(i => i.LoginName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var destinations = await query
                .Where(i => i.Destination != null && i.Destination != "")
                .Select(i => i.Destination!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var channels = await query
                .Where(i => i.Channel != null && i.Channel != "")
                .Select(i => i.Channel!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var policies = await query
                .Where(i => i.Policy != null && i.Policy != "")
                .Select(i => i.Policy!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            // Get rules from ViolationTriggers - this requires in-memory processing
            var incidentsWithTriggers = await query
                .Where(i => i.ViolationTriggers != null && i.ViolationTriggers != "")
                .Select(i => i.ViolationTriggers!)
                .Distinct()
                .ToListAsync();

            var rules = new HashSet<string>();
            foreach (var triggerJson in incidentsWithTriggers)
            {
                try
                {
                    var triggers = System.Text.Json.JsonDocument.Parse(triggerJson);
                    if (triggers.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var trigger in triggers.RootElement.EnumerateArray())
                        {
                            if (trigger.TryGetProperty("RuleName", out var ruleNameElement))
                            {
                                var ruleName = ruleNameElement.GetString();
                                if (!string.IsNullOrEmpty(ruleName))
                                {
                                    rules.Add(ruleName);
                                }
                            }
                        }
                    }
                }
                catch { /* Skip invalid JSON */ }
            }

            // Get date range
            var minDate = await _context.Incidents.MinAsync(i => (DateTime?)i.Timestamp);
            var maxDate = await _context.Incidents.MaxAsync(i => (DateTime?)i.Timestamp);

            return Ok(new
            {
                users = users,
                destinations = destinations,
                channels = channels,
                policies = policies,
                rules = rules.OrderBy(r => r).ToList(),
                dateRange = new
                {
                    minDate = minDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"),
                    maxDate = maxDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Get incidents filtered by action type for Action Summary modal
    /// Supports pagination and server-side filtering for performance
    /// </summary>
    [HttpGet("incidents/by-action")]
    public async Task<ActionResult<object>> GetIncidentsByAction(
        [FromQuery] string action,
        [FromQuery] string? date = null,
        [FromQuery] string? start_date = null,
        [FromQuery] string? end_date = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? search = null,
        [FromQuery] string? user = null,
        [FromQuery] string? destination = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? policy = null,
        [FromQuery] string? rule = null)
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

            // Ensure valid pagination
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;
            if (pageSize > 500) pageSize = 500; // Max limit

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
                // Default to last 30 days
                startOfRange = DateTime.UtcNow.Date.AddDays(-30);
                endOfRange = DateTime.UtcNow.Date.AddDays(1);
            }

            // Build query
            var query = _context.Incidents
                .Where(i => i.Timestamp >= startOfRange && i.Timestamp < endOfRange);

            // Action filter
            if (normalizedAction != "TOTAL")
            {
                query = query.Where(i => i.Action != null && 
                           (i.Action.ToUpper() == normalizedAction || 
                            (normalizedAction == "BLOCK" && i.Action.ToUpper() == "BLOCKED") ||
                            (normalizedAction == "QUARANTINE" && i.Action.ToUpper() == "QUARANTINED")));
            }

            // Server-side search filter
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(i => 
                    (i.LoginName != null && i.LoginName.ToLower().Contains(searchLower)) ||
                    (i.UserEmail != null && i.UserEmail.ToLower().Contains(searchLower)) ||
                    (i.Destination != null && i.Destination.ToLower().Contains(searchLower)));
            }

            // User filter
            if (!string.IsNullOrEmpty(user))
            {
                var userLower = user.ToLower();
                query = query.Where(i => i.LoginName != null && i.LoginName.ToLower().Contains(userLower));
            }

            // Destination filter
            if (!string.IsNullOrEmpty(destination))
            {
                var destLower = destination.ToLower();
                query = query.Where(i => i.Destination != null && i.Destination.ToLower().Contains(destLower));
            }

            // Channel filter
            if (!string.IsNullOrEmpty(channel))
            {
                query = query.Where(i => i.Channel != null && i.Channel.ToLower().Contains(channel.ToLower()));
            }

            // Policy filter
            if (!string.IsNullOrEmpty(policy))
            {
                query = query.Where(i => i.Policy != null && i.Policy.ToLower().Contains(policy.ToLower()));
            }

            // Rule filter - filter by ViolationTriggers containing the rule name
            if (!string.IsNullOrEmpty(rule))
            {
                query = query.Where(i => i.ViolationTriggers != null && i.ViolationTriggers.Contains(rule));
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Apply pagination
            var incidents = await query
                .OrderByDescending(i => i.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Format response
            var items = incidents.Select(i =>
            {
                // Extract rule name and max matches from ViolationTriggers
                // Extract rule name and max matches from ViolationTriggers
                string ruleName = i.RuleName ?? "N/A"; // Use the dedicated column first
                int maxMatches = i.MaxMatches;
                
                if (ruleName == "N/A" && !string.IsNullOrEmpty(i.ViolationTriggers))
                {
                    try
                    {
                        var triggers = System.Text.Json.JsonDocument.Parse(i.ViolationTriggers);
                        if (triggers.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                            triggers.RootElement.GetArrayLength() > 0)
                        {
                            var firstTrigger = triggers.RootElement[0];
                            // Try multiple casing formats
                            if (firstTrigger.TryGetProperty("RuleName", out var ruleNameElement) ||
                                firstTrigger.TryGetProperty("rule_name", out ruleNameElement) ||
                                firstTrigger.TryGetProperty("ruleName", out ruleNameElement))
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
                                            int matches = 0;
                                            if (classifier.TryGetProperty("NumberMatches", out var matchesElement) ||
                                                classifier.TryGetProperty("number_matches", out matchesElement) ||
                                                classifier.TryGetProperty("numberMatches", out matchesElement))
                                            {
                                                if (matchesElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                                                {
                                                    matches = matchesElement.GetInt32();
                                                }
                                            }
                                            
                                            if (matches > maxMatches) maxMatches = matches;
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

            // Return paginated response
            return Ok(new {
                items = items,
                page = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = totalPages,
                hasNextPage = page < totalPages,
                hasPreviousPage = page > 1
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}