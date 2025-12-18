using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Extended Risk Analyzer Service with database operations
/// </summary>
public class RiskAnalyzerService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly AnalyzerDbContext _context;
    private readonly Shared.Services.RiskAnalyzer _riskAnalyzer;

    public RiskAnalyzerService(
        IIncidentRepository incidentRepository,
        AnalyzerDbContext context)
    {
        _incidentRepository = incidentRepository;
        _context = context;
        _riskAnalyzer = new Shared.Services.RiskAnalyzer();
    }

    /// <summary>
    /// Get user risk trends
    /// </summary>
    public async Task<List<UserRiskTrend>> GetUserRiskTrendsAsync(int days = 30, string? user = null)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var incidents = await _incidentRepository.GetIncidentsAsync(startDate, endDate);
        
        var filteredIncidents = !string.IsNullOrEmpty(user)
            ? incidents.Where(i => i.UserEmail == user).ToList()
            : incidents;

        var trends = filteredIncidents
            .GroupBy(i => new { i.UserEmail, Date = DateOnly.FromDateTime(i.Timestamp.Date) })
            .Select(g => new UserRiskTrend
            {
                UserEmail = g.Key.UserEmail,
                Date = g.Key.Date,
                TotalIncidents = g.Count(),
                RiskScore = g.Max(i => i.RiskScore ?? 0),
                TrendDirection = "stable" // Calculate trend direction based on previous days
            })
            .OrderBy(t => t.UserEmail)
            .ThenBy(t => t.Date)
            .ToList();

        return trends;
    }

    /// <summary>
    /// Get department summaries
    /// </summary>
    public async Task<List<DepartmentSummary>> GetDepartmentSummariesAsync(
        DateOnly? startDate,
        DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            startDate = endDate.Value.AddDays(-30);
        }

        var incidents = await _incidentRepository.GetIncidentsByDepartmentAsync(
            startDate.Value, endDate.Value);
        
        var summaries = incidents
            .GroupBy(i => i.Department)
            .Select(g => new DepartmentSummary
            {
                Department = g.Key!,
                TotalIncidents = g.Count(),
                HighRiskCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count(),
                Date = endDate
            })
            .ToList();

        return summaries;
    }

    /// <summary>
    /// Get daily summaries
    /// </summary>
    public async Task<List<DailySummary>> GetDailySummariesAsync(int days = 7)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var incidents = await _incidentRepository.GetIncidentsAsync(startDate, endDate);
        
        var summaries = incidents
            .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
            .Select(g => new DailySummary
            {
                Date = g.Key,
                TotalIncidents = g.Count(),
                HighRiskCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count(),
                DepartmentsAffected = g.Where(i => !string.IsNullOrEmpty(i.Department))
                                      .Select(i => i.Department!)
                                      .Distinct()
                                      .Count()
            })
            .OrderBy(s => s.Date)
            .ToList();

        return summaries;
    }

    /// <summary>
    /// Get risk heatmap data
    /// </summary>
    public async Task<RiskHeatmapData> GetRiskHeatmapAsync(
        string dimension,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            startDate = endDate.Value.AddDays(-30);
        }

        var labels = new List<string>();
        var values = new List<int>();
        var incidents = await _incidentRepository.GetIncidentsAsync(startDate.Value, endDate.Value);

        if (dimension == "department")
        {
            var deptData = incidents
                .Where(i => !string.IsNullOrEmpty(i.Department))
                .GroupBy(i => i.Department)
                .Select(g => new { Label = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            labels = deptData.Select(d => d.Label).ToList();
            values = deptData.Select(d => d.Count).ToList();
        }
        else if (dimension == "user")
        {
            var userData = incidents
                .GroupBy(i => i.UserEmail)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            labels = userData.Select(d => d.Label).ToList();
            values = userData.Select(d => d.Count).ToList();
        }
        else // channel
        {
            var channelData = incidents
                .Where(i => !string.IsNullOrEmpty(i.Channel))
                .GroupBy(i => i.Channel)
                .Select(g => new { Label = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            labels = channelData.Select(d => d.Label).ToList();
            values = channelData.Select(d => d.Count).ToList();
        }

        return new RiskHeatmapData
        {
            Labels = labels,
            Values = values,
            Dimension = dimension,
            DateRange = new Dictionary<string, string>
            {
                { "start", startDate.Value.ToString("yyyy-MM-dd") },
                { "end", endDate.Value.ToString("yyyy-MM-dd") }
            }
        };
    }

    /// <summary>
    /// Process Redis stream and calculate risk scores
    /// </summary>
    public async Task<int> ProcessRedisStreamAsync(DatabaseService dbService)
    {
        var processedCount = await dbService.ProcessRedisStreamAsync();
        if (processedCount > 0)
        {
            await CalculateRiskScoresAsync();
        }
        return processedCount;
    }

    /// <summary>
    /// Calculate risk scores for incidents without scores
    /// </summary>
    public async Task<int> CalculateRiskScoresAsync()
    {
        var incidentsWithoutScores = await _incidentRepository.GetIncidentsWithoutRiskScoreAsync();

        var updatedCount = 0;
        foreach (var incident in incidentsWithoutScores)
        {
            // Calculate repeat count (how many times this user had similar incidents)
            var repeatCount = await _incidentRepository.GetPreviousIncidentsCountAsync(
                incident.UserEmail, incident.Timestamp);

            // Calculate data sensitivity (based on data type and severity)
            var dataSensitivity = CalculateDataSensitivity(incident.DataType, incident.Severity);

            // Calculate risk score
            incident.RiskScore = _riskAnalyzer.CalculateRiskScore(
                incident.Severity,
                repeatCount,
                dataSensitivity);
            incident.RepeatCount = repeatCount;
            incident.DataSensitivity = dataSensitivity;

            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await _incidentRepository.UpdateIncidentsAsync(incidentsWithoutScores);
        }

        return updatedCount;
    }

    private int CalculateDataSensitivity(string? dataType, int severity)
    {
        if (string.IsNullOrEmpty(dataType))
            return severity;

        var dataTypeLower = dataType.ToLower();
        
        // Use RiskConstants for data sensitivity thresholds
        if (dataTypeLower.Contains(RiskConstants.DataSensitivity.PII) || 
            dataTypeLower.Contains(RiskConstants.DataSensitivity.Personal))
            return Math.Max(severity, RiskConstants.DataSensitivity.PIIThreshold);
        if (dataTypeLower.Contains(RiskConstants.DataSensitivity.PCI) || 
            dataTypeLower.Contains(RiskConstants.DataSensitivity.Credit))
            return Math.Max(severity, RiskConstants.DataSensitivity.PCIThreshold);
        if (dataTypeLower.Contains(RiskConstants.DataSensitivity.Confidential))
            return Math.Max(severity, RiskConstants.DataSensitivity.ConfidentialThreshold);

        return severity;
    }

    /// <summary>
    /// Get paginated user list with risk scores
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserListAsync(int page = 1, int pageSize = 15)
    {
        // This method is deprecated - use the endpoint in RiskController instead
        // Return format compatible with frontend expectations
        // Get all incidents (no date filter for user list)
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-365); // Last year
        var incidents = await _incidentRepository.GetIncidentsAsync(startDate, endDate);
        
        var userGroups = incidents
            .GroupBy(i => i.UserEmail)
            .Select(g => new
            {
                user_email = g.Key,
                risk_score = g.Max(i => i.RiskScore ?? 0),
                total_incidents = g.Count(),
                department = g.Where(i => !string.IsNullOrEmpty(i.Department))
                             .Select(i => i.Department)
                             .FirstOrDefault() ?? null
            })
            .OrderByDescending(u => u.risk_score)
            .ToList();

        var total = userGroups.Count;
        var offset = (page - 1) * pageSize;
        var pagedUsers = userGroups.Skip(offset).Take(pageSize).ToList();

        return new Dictionary<string, object>
        {
            { "users", pagedUsers.Select(u => new Dictionary<string, object>
            {
                { "user_email", u.user_email },
                { "risk_score", u.risk_score },
                { "total_incidents", u.total_incidents },
                { "department", u.department ?? "" }
            }) },
            { "total", total },
            { "page", page },
            { "page_size", pageSize }
        };
    }

    /// <summary>
    /// Get channel activity breakdown
    /// </summary>
    public async Task<Dictionary<string, object>> GetChannelActivityAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        int days = 30)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            startDate = endDate.Value.AddDays(-days);
        }

        var incidents = await _incidentRepository.GetIncidentsByChannelAsync(
            startDate.Value, endDate.Value);
        
        var channels = incidents
            .GroupBy(i => i.Channel)
            .Select(g => new
            {
                Channel = g.Key!,
                TotalIncidents = g.Count(),
                CriticalCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.CriticalThreshold),
                HighCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold && 
                                         (i.RiskScore ?? 0) < RiskConstants.RiskScores.CriticalThreshold),
                MediumCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.MediumThreshold && 
                                          (i.RiskScore ?? 0) < RiskConstants.RiskScores.HighThreshold),
                LowCount = g.Count(i => (i.RiskScore ?? 0) < RiskConstants.RiskScores.MediumThreshold)
            })
            .OrderByDescending(c => c.TotalIncidents)
            .ToList();

        var total = channels.Sum(c => c.TotalIncidents);

        var channelList = channels.Select(c => new Dictionary<string, object>
        {
            { "channel", c.Channel },
            { "total_incidents", c.TotalIncidents },
            { "percentage", total > 0 ? Math.Round((c.TotalIncidents / (double)total) * 100, 1) : 0 },
            { "critical_count", c.CriticalCount },
            { "high_count", c.HighCount },
            { "medium_count", c.MediumCount },
            { "low_count", c.LowCount }
        }).ToList();

        return new Dictionary<string, object>
        {
            { "channels", channelList },
            { "total", total },
            { "date_range", new Dictionary<string, string>
                {
                    { "start", startDate.Value.ToString("yyyy-MM-dd") },
                    { "end", endDate.Value.ToString("yyyy-MM-dd") }
                }
            }
        };
    }

    /// <summary>
    /// Get IOB detections
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetIOBDetectionsAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        string? category = null)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            startDate = endDate.Value.AddDays(-30);
        }

        var incidents = await _incidentRepository.GetIncidentsAsync(
            startDate.Value, endDate.Value);
        
        // Limit to 1000 for performance
        incidents = incidents.Take(1000).ToList();

        var iobCounts = new Dictionary<string, Dictionary<string, object>>();

        foreach (var incident in incidents)
        {
            var iobs = _riskAnalyzer.DetectIOB(incident);

            foreach (var iob in iobs)
            {
                if (!iobCounts.ContainsKey(iob))
                {
                    iobCounts[iob] = new Dictionary<string, object>
                    {
                        { "code", iob },
                        { "count", 0 },
                        { "users_affected", new HashSet<string>() }
                    };
                }

                var iobData = iobCounts[iob];
                iobData["count"] = (int)iobData["count"] + 1;
                ((HashSet<string>)iobData["users_affected"]).Add(incident.UserEmail);
            }
        }

        return iobCounts.Values.Select(iob => new Dictionary<string, object>
        {
            { "code", iob["code"] },
            { "count", iob["count"] },
            { "users_affected", ((HashSet<string>)iob["users_affected"]).Count }
        }).OrderByDescending(i => (int)i["count"]).ToList();
    }

    /// <summary>
    /// Get top users by day with their daily alert counts
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTopUsersByDayAsync(int days = 30, int limit = 20)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var incidents = await _incidentRepository.GetIncidentsAsync(startDate, endDate);

        // Group by user, calculate stats
        var userStats = incidents
            .GroupBy(i => i.UserEmail)
            .Select(g => new
            {
                UserEmail = g.Key,
                TotalAlerts = g.Count(),
                RiskScore = g.Max(i => i.RiskScore ?? 0),
                Department = g.Where(i => !string.IsNullOrEmpty(i.Department))
                             .Select(i => i.Department)
                             .FirstOrDefault() ?? "",
                LoginName = g.Where(i => !string.IsNullOrEmpty(i.LoginName))
                            .Select(i => i.LoginName)
                            .FirstOrDefault() ?? "",
                EmailAddress = g.Where(i => !string.IsNullOrEmpty(i.EmailAddress))
                              .Select(i => i.EmailAddress)
                              .FirstOrDefault() ?? ""
            })
            .OrderByDescending(u => u.TotalAlerts)
            .Take(limit)
            .ToList();

        return userStats.Select(u => new Dictionary<string, object>
        {
            { "user_email", u.UserEmail },
            { "login_name", u.LoginName },
            { "email_address", !string.IsNullOrEmpty(u.EmailAddress) ? u.EmailAddress : u.UserEmail },
            { "total_alerts", u.TotalAlerts },
            { "risk_score", u.RiskScore },
            { "department", u.Department },
            { "risk_level", GetRiskLevelFromScore(u.RiskScore) }
        }).ToList();
    }

    /// <summary>
    /// Get top rules by day with their daily alert counts
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTopRulesByDayAsync(int days = 30, int limit = 10)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var incidents = await _incidentRepository.GetIncidentsAsync(startDate, endDate);

        // Group by policy (rule), calculate stats
        var ruleStats = incidents
            .Where(i => !string.IsNullOrEmpty(i.Policy))
            .GroupBy(i => i.Policy!)
            .Select(g => new
            {
                RuleName = g.Key,
                TotalAlerts = g.Count(),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count()
            })
            .OrderByDescending(r => r.TotalAlerts)
            .Take(limit)
            .ToList();

        return ruleStats.Select(r => new Dictionary<string, object>
        {
            { "rule_name", r.RuleName },
            { "total_alerts", r.TotalAlerts },
            { "avg_risk_score", Math.Round(r.AvgRiskScore, 1) },
            { "unique_users", r.UniqueUsers }
        }).ToList();
    }

    /// <summary>
    /// Get comprehensive daily report data for a specific date
    /// </summary>
    public async Task<Dictionary<string, object>> GetDailyReportDataAsync(DateTime date)
    {
        var targetDate = DateOnly.FromDateTime(date);
        var incidents = await _incidentRepository.GetIncidentsAsync(targetDate, targetDate);

        // Action Summary
        var actionSummary = incidents
            .GroupBy(i => i.Action?.ToUpper() ?? "UNKNOWN")
            .ToDictionary(g => g.Key, g => g.Count());

        var authorized = actionSummary.GetValueOrDefault("AUTHORIZED", 0);
        var block = actionSummary.GetValueOrDefault("BLOCK", 0) + actionSummary.GetValueOrDefault("BLOCKED", 0);
        var quarantine = actionSummary.GetValueOrDefault("QUARANTINE", 0) + actionSummary.GetValueOrDefault("QUARANTINED", 0);
        var total = incidents.Count;

        // Top 10 Users
        var topUsers = incidents
            .GroupBy(i => i.UserEmail)
            .Select(g => new
            {
                UserEmail = g.Key,
                LoginName = g.Where(i => !string.IsNullOrEmpty(i.LoginName))
                            .Select(i => i.LoginName)
                            .FirstOrDefault() ?? "",
                TotalAlerts = g.Count(),
                RiskScore = g.Max(i => i.RiskScore ?? 0)
            })
            .OrderByDescending(u => u.TotalAlerts)
            .Take(10)
            .Select(u => new Dictionary<string, object>
            {
                { "user_email", u.UserEmail },
                { "login_name", u.LoginName },
                { "total_alerts", u.TotalAlerts },
                { "risk_score", u.RiskScore },
                { "risk_level", GetRiskLevelFromScore(u.RiskScore) }
            })
            .ToList();

        // Top 10 Policies with Top 3 Rules each
        var topPolicies = await GetTopPoliciesWithRulesAsync(incidents);

        // Channel Breakdown
        var channelBreakdown = incidents
            .Where(i => !string.IsNullOrEmpty(i.Channel))
            .GroupBy(i => i.Channel!)
            .Select(g => new Dictionary<string, object>
            {
                { "channel", g.Key },
                { "total_alerts", g.Count() },
                { "percentage", total > 0 ? Math.Round((g.Count() / (double)total) * 100, 1) : 0 }
            })
            .OrderByDescending(c => (int)c["total_alerts"])
            .ToList();

        // Top 10 Destinations
        var topDestinations = await GetDestinationSummaryAsync(incidents, 10);

        return new Dictionary<string, object>
        {
            { "date", date.ToString("yyyy-MM-dd") },
            { "action_summary", new Dictionary<string, object>
                {
                    { "authorized", authorized },
                    { "block", block },
                    { "quarantine", quarantine },
                    { "total", total }
                }
            },
            { "top_users", topUsers },
            { "top_policies", topPolicies },
            { "channel_breakdown", channelBreakdown },
            { "top_destinations", topDestinations }
        };
    }

    /// <summary>
    /// Get top policies with their top 3 rules
    /// </summary>
    private async Task<List<Dictionary<string, object>>> GetTopPoliciesWithRulesAsync(List<Incident> incidents)
    {
        // Parse ViolationTriggers to extract policy and rule combinations
        var policyRuleData = new Dictionary<string, Dictionary<string, int>>();

        foreach (var incident in incidents)
        {
            var policyName = incident.Policy ?? "Unknown Policy";
            
            if (!policyRuleData.ContainsKey(policyName))
            {
                policyRuleData[policyName] = new Dictionary<string, int>();
            }

            // Try to parse ViolationTriggers for rule names
            // ViolationTriggers format: [{"policy_name": "...", "rule_name": "...", "classifiers": [...]}]
            if (!string.IsNullOrEmpty(incident.ViolationTriggers))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(incident.ViolationTriggers);
                    var root = doc.RootElement;
                    
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var trigger in root.EnumerateArray())
                        {
                            string ruleName = policyName; // Default to policy name
                            
                            // Try to get rule_name from the trigger object
                            if (trigger.TryGetProperty("rule_name", out var ruleNameElement) && 
                                ruleNameElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var ruleValue = ruleNameElement.GetString();
                                if (!string.IsNullOrEmpty(ruleValue))
                                {
                                    ruleName = ruleValue;
                                }
                            }
                            // Also try RuleName (camelCase)
                            else if (trigger.TryGetProperty("RuleName", out var ruleNameCamelElement) && 
                                ruleNameCamelElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var ruleValue = ruleNameCamelElement.GetString();
                                if (!string.IsNullOrEmpty(ruleValue))
                                {
                                    ruleName = ruleValue;
                                }
                            }
                            
                            policyRuleData[policyName][ruleName] = policyRuleData[policyName].GetValueOrDefault(ruleName, 0) + 1;
                        }
                        continue;
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // JSON parse failed, fall through to use policy name as rule
                }
            }
            
            // If no ViolationTriggers or parsing failed, use policy name as the rule
            policyRuleData[policyName][policyName] = policyRuleData[policyName].GetValueOrDefault(policyName, 0) + 1;
        }

        return policyRuleData
            .Select(p => new Dictionary<string, object>
            {
                { "policy_name", p.Key },
                { "total_alerts", p.Value.Values.Sum() },
                { "top_rules", p.Value
                    .OrderByDescending(r => r.Value)
                    .Take(3)
                    .Select(r => new Dictionary<string, object>
                    {
                        { "rule_name", r.Key },
                        { "alert_count", r.Value }
                    })
                    .ToList()
                }
            })
            .OrderByDescending(p => (int)p["total_alerts"])
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Get destination summary
    /// </summary>
    private Task<List<Dictionary<string, object>>> GetDestinationSummaryAsync(List<Incident> incidents, int limit = 10)
    {
        var destinations = incidents
            .Where(i => !string.IsNullOrEmpty(i.Destination))
            .GroupBy(i => i.Destination!)
            .Select(g => new Dictionary<string, object>
            {
                { "destination", g.Key },
                { "total_alerts", g.Count() }
            })
            .OrderByDescending(d => (int)d["total_alerts"])
            .Take(limit)
            .ToList();

        return Task.FromResult(destinations);
    }

    /// <summary>
    /// Helper method to get risk level from score
    /// </summary>
    private string GetRiskLevelFromScore(int score)
    {
        if (score >= 91) return "Critical";
        if (score >= 61) return "High";
        if (score >= 41) return "Medium";
        return "Low";
    }
}