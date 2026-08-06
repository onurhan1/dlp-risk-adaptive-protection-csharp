using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/risk")]
public class RiskIncidentsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;

    public RiskIncidentsController(AnalyzerDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get filter options for Action Incidents modal (unique values for dropdowns)
    /// </summary>
    /// <summary>
    /// P-03 fix: all DB queries run in parallel via Task.WhenAll instead of sequentially.
    /// M-02 fix: ViolationTrigger parsing delegated to ViolationTriggerParser.
    /// </summary>
    [HttpGet("incidents/filter-options")]
    public async Task<ActionResult<object>> GetFilterOptions(
        [FromQuery] string? action = null)
    {
        try
        {
            var query = _context.Incidents.AsQueryable();

            if (!string.IsNullOrEmpty(action) && action.ToUpper() != "TOTAL")
            {
                var normalizedAction = action.ToUpper();
                query = query.Where(i => i.Action != null &&
                           (i.Action.ToUpper() == normalizedAction ||
                            (normalizedAction == "BLOCK"       && i.Action.ToUpper() == "BLOCKED") ||
                            (normalizedAction == "QUARANTINE"  && i.Action.ToUpper() == "QUARANTINED")));
            }

            // ── P-03 Fix Reverted: EF Core DbContext does not support concurrent execution. ────────────
            // We must run these sequentially, otherwise we get a 500 InvalidOperationException
            var users = await query
                .Where(i => i.LoginName != null && i.LoginName != "")
                .Select(i => i.LoginName!)
                .Distinct().OrderBy(x => x)
                .ToListAsync();

            var destinations = await query
                .Where(i => i.Destination != null && i.Destination != "")
                .Select(i => i.Destination!)
                .Distinct().OrderBy(x => x)
                .ToListAsync();

            var channels = await query
                .Where(i => i.Channel != null && i.Channel != "")
                .Select(i => i.Channel!)
                .Distinct().OrderBy(x => x)
                .ToListAsync();

            var policies = await query
                .Where(i => i.Policy != null && i.Policy != "")
                .Select(i => i.Policy!)
                .Distinct().OrderBy(x => x)
                .ToListAsync();

            var triggers = await query
                .Where(i => i.ViolationTriggers != null && i.ViolationTriggers != "")
                .Select(i => i.ViolationTriggers!)
                .Distinct()
                .ToListAsync();

            var minDate = await _context.Incidents.MinAsync(i => (DateTime?)i.Timestamp);
            var maxDate = await _context.Incidents.MaxAsync(i => (DateTime?)i.Timestamp);

            // ── M-02: Rule extraction via ViolationTriggerParser ─────────────
            var rules = new HashSet<string>();
            foreach (var triggerJson in triggers)
            {
                foreach (var ruleName in ViolationTriggerParser.ExtractAllRuleNames(triggerJson))
                    rules.Add(ruleName);
            }

            return Ok(new
            {
                users        = users,
                destinations = destinations,
                channels     = channels,
                policies     = policies,
                rules        = rules.OrderBy(r => r).ToList(),
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
    /// Top matched users / departments for the dashboard grids.
    /// Grouping happens in the database; the optional action filter uses the same
    /// BLOCK/BLOCKED and QUARANTINE/QUARANTINED normalisation as by-action.
    /// </summary>
    [HttpGet("incidents/top-breakdown")]
    public async Task<ActionResult<object>> GetTopBreakdown(
        [FromQuery] string dimension = "user",
        [FromQuery] string? action = null,
        [FromQuery] int days = 30,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 3)
    {
        try
        {
            var normalizedDimension = (dimension ?? "user").ToLowerInvariant();
            if (normalizedDimension != "user" && normalizedDimension != "department")
            {
                return BadRequest(new { detail = "Invalid dimension parameter. Must be one of: user, department" });
            }

            if (limit < 1) limit = 1;
            if (limit > 100) limit = 100;

            DateTime startOfRange, endOfRange;
            if (startDate.HasValue && endDate.HasValue)
            {
                startOfRange = startDate.Value.Date;
                endOfRange = endDate.Value.Date.AddDays(1);
            }
            else
            {
                if (days < 1) days = 30;
                endOfRange = DateTime.UtcNow.Date.AddDays(1);
                startOfRange = DateTime.UtcNow.Date.AddDays(-days);
            }

            var query = _context.Incidents
                .Where(i => i.Timestamp >= startOfRange && i.Timestamp < endOfRange);

            var normalizedAction = action?.ToUpper();
            if (!string.IsNullOrEmpty(normalizedAction) && normalizedAction != "TOTAL")
            {
                query = query.Where(i => i.Action != null &&
                           (i.Action.ToUpper() == normalizedAction ||
                            (normalizedAction == "BLOCK" && i.Action.ToUpper() == "BLOCKED") ||
                            (normalizedAction == "QUARANTINE" && i.Action.ToUpper() == "QUARANTINED")));
            }

            // Users are keyed on the email, departments fall back to the Team field
            // that the collector fills from the Manager column (same rule the
            // behavioural models use). Rows with no usable key are dropped before
            // grouping so the fallback stays a plain CASE in the GROUP BY.
            var grouped = normalizedDimension == "user"
                ? query
                    .Where(i => (i.UserEmail != null && i.UserEmail != "") || (i.LoginName != null && i.LoginName != ""))
                    .GroupBy(i => i.UserEmail != null && i.UserEmail != "" ? i.UserEmail : i.LoginName)
                : query
                    .Where(i => (i.Department != null && i.Department != "") || (i.Team != null && i.Team != ""))
                    .GroupBy(i => i.Department != null && i.Department != "" ? i.Department : i.Team);

            var items = await grouped
                .Select(g => new { Name = g.Key!, TotalAlerts = g.Count() })
                .OrderByDescending(x => x.TotalAlerts)
                .Take(limit)
                .ToListAsync();

            return Ok(items);
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

            // ── M-02: Use ViolationTriggerParser instead of inline JSON parsing ──
            var items = incidents.Select(i =>
            {
                // Prefer the stored RuleName column; fall back to parsing ViolationTriggers
                string ruleName  = i.RuleName ?? string.Empty;
                int    maxMatches = i.MaxMatches;

                if (string.IsNullOrEmpty(ruleName))
                {
                    var (parsedName, parsedMatches) = ViolationTriggerParser.ExtractSummary(i.ViolationTriggers);
                    ruleName   = parsedName  ?? i.Policy ?? "N/A";
                    if (maxMatches == 0) maxMatches = parsedMatches;
                }

                return new Dictionary<string, object>
                {
                    { "login_name",         i.LoginName ?? i.UserEmail ?? "N/A" },
                    { "destination",        i.Destination ?? "N/A" },
                    { "channel",            i.Channel ?? "N/A" },
                    { "policy",             i.Policy ?? "N/A" },
                    { "rule_name",          ruleName },
                    { "action",             i.Action ?? "N/A" },
                    { "timestamp",          i.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") },
                    { "max_matches",        maxMatches },
                    { "violation_triggers", i.ViolationTriggers ?? string.Empty }
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
