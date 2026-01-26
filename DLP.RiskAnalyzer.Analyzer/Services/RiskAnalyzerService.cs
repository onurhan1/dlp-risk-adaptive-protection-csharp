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
                // Use legacy thresholds for backward compatibility with existing 0-100 scale data
                HighRiskCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.LegacyHighThreshold),
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
                // Count unique HIGH RISK USERS (users whose max risk score >= 500, i.e., 50+ when normalized)
                HighRiskCount = g.GroupBy(i => i.UserEmail)
                                 .Count(userGroup => userGroup.Max(i => i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold),
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
    /// GÜNCEL FORMÜL: MaxMatchesTier * ChannelMultiplier * DestinationScore
    /// </summary>
    public async Task<int> CalculateRiskScoresAsync()
    {
        var incidentsWithoutScores = await _incidentRepository.GetIncidentsWithoutRiskScoreAsync();
        if (!incidentsWithoutScores.Any())
            return 0;

        // Load NDA domains for fast lookup
        var ndaDomains = await _context.NdaDomains
            .ToDictionaryAsync(d => d.Domain.ToLower(), d => d);

        var updatedCount = 0;
        foreach (var incident in incidentsWithoutScores)
        {
            // 1. Calculate Destination Score
            int destinationScore = RiskConstants.DestinationScores.Unknown; // Default 5
            
            if (!string.IsNullOrEmpty(incident.Destination))
            {
                var dest = incident.Destination.Trim();
                
                // SPL Check (Hardcoded for now as per request)
                // "SPL bizim ortamımız 1 olsun"
                if (dest.Contains("SPL", StringComparison.OrdinalIgnoreCase))
                {
                    destinationScore = RiskConstants.DestinationScores.Spl; // 1
                }
                // Printer Check
                else if (incident.Channel?.Contains("Printer", StringComparison.OrdinalIgnoreCase) == true || 
                         incident.Channel?.Contains("Printing", StringComparison.OrdinalIgnoreCase) == true ||
                         dest.Contains("Print", StringComparison.OrdinalIgnoreCase))
                {
                    destinationScore = RiskConstants.DestinationScores.Printer; // 3
                }
                else 
                {
                    // Domain extraction logic could be complex (URL vs Email vs IP)
                    // Simplified: Assume destination is somewhat domain-like or contains domain
                    
                    var lowerDest = dest.ToLowerInvariant();
                    
                    // Try exact match first
                    if (ndaDomains.TryGetValue(lowerDest, out var domainInfo))
                    {
                        if (domainInfo.IsPersonal) destinationScore = RiskConstants.DestinationScores.Personal; // 10
                        else if (domainInfo.HasNda) destinationScore = RiskConstants.DestinationScores.NdaPresent; // 1
                        else destinationScore = RiskConstants.DestinationScores.NdaAbsent; // 5
                    }
                    else
                    {
                        // Partial match check (e.g. user@gmail.com contains gmail.com)
                        // This is computationally expensive O(N*M), but N (domains) is small (~900)
                        
                        // Check explicit personal domains first (optimization)
                        if (lowerDest.Contains("gmail.com") || lowerDest.Contains("hotmail.com") || 
                            lowerDest.Contains("outlook.com") || lowerDest.Contains("windowslive.com") || 
                            lowerDest.Contains("icloud.com") || lowerDest.Contains("yahoo.com") ||
                            lowerDest.Contains("mynet.com"))
                        {
                            destinationScore = RiskConstants.DestinationScores.Personal; // 10
                        }
                        else
                        {
                            // Check DB domains
                            var matchedDomain = ndaDomains.Values
                                .FirstOrDefault(d => lowerDest.Contains(d.Domain));
                                
                            if (matchedDomain != null)
                            {
                                if (matchedDomain.IsPersonal) destinationScore = RiskConstants.DestinationScores.Personal; // 10
                                else if (matchedDomain.HasNda) destinationScore = RiskConstants.DestinationScores.NdaPresent; // 1
                                else destinationScore = RiskConstants.DestinationScores.NdaAbsent; // 5
                            }
                            else
                            {
                                // Unknown/New domain
                                // Automatically add to DB as Unknown if it looks like a domain (contains dot)
                                // But don't block the thread
                                // Future improvement: Queue for addition
                                destinationScore = RiskConstants.DestinationScores.Unknown; // 5
                            }
                        }
                    }
                }
            }
            
            // Channel is already in incident.Channel
            // MaxMatches is in incident.MaxMatches

            // Calculate risk score with new formula
            // Calculate risk score with new formula
            incident.RiskScore = _riskAnalyzer.CalculateRiskScoreV2(
                maxMatches: incident.MaxMatches,
                channel: incident.Channel,
                destinationScore: destinationScore,
                action: incident.Action);
            
            // We still keep other fields populated for reference, even if not used in formula
            // Calculate data sensitivity (based on data type and severity)
            incident.DataSensitivity = CalculateDataSensitivity(incident.DataType, incident.Severity);

            // Policy Repeat Count (keeping legacy logic for population)
            var policyRepeatCounts = await _incidentRepository.GetPolicyRepeatCountsAsync(incident.UserEmail, incident.Timestamp);
            incident.RepeatCount = policyRepeatCounts.Values.DefaultIfEmpty(0).Max();

            incident.RiskLevel = _riskAnalyzer.GetRiskLevel(incident.RiskScore.Value);

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
    public async Task<Dictionary<string, object>> GetUserListAsync(int page = 1, int pageSize = 15, string? search = null)
    {
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
                last_incident_date = g.Max(i => i.Timestamp),
                department = g.Where(i => !string.IsNullOrEmpty(i.Department))
                             .Select(i => i.Department)
                             .FirstOrDefault() ?? null
            })
            // Sort by: 1) Risk score desc, 2) Last incident date desc (most recent first)
            .OrderByDescending(u => u.risk_score)
            .ThenByDescending(u => u.last_incident_date)
            .ToList();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower().Trim();
            userGroups = userGroups
                .Where(u => u.user_email.ToLower().Contains(searchLower) ||
                           (u.department != null && u.department.ToLower().Contains(searchLower)))
                .ToList();
        }

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
                { "last_incident_date", u.last_incident_date.ToString("O") },
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
                CriticalCount = g.Count(i => (i.RiskScore ?? 0) >= 750), // High risk in 1000-scale (75+ on dashboard)
                HighCount = g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold && 
                                         (i.RiskScore ?? 0) < 750),
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
    public async Task<List<Dictionary<string, object>>> GetTopUsersByDayAsync(
        int days = 30, 
        int limit = 20,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        DateOnly start, end;
        
        if (startDate.HasValue && endDate.HasValue)
        {
            start = DateOnly.FromDateTime(startDate.Value);
            end = DateOnly.FromDateTime(endDate.Value);
        }
        else
        {
            end = DateOnly.FromDateTime(DateTime.UtcNow);
            start = end.AddDays(-days);
        }

        var incidents = await _incidentRepository.GetIncidentsAsync(start, end);

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
            .Where(u => u.RiskScore >= 700)  // Only show high-risk users (70+ normalized)
            .OrderByDescending(u => u.RiskScore)  // Sort by risk score first
            .ThenByDescending(u => u.TotalAlerts)  // Then by incident count
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
    public async Task<List<Dictionary<string, object>>> GetTopRulesByDayAsync(
        int days = 30, 
        int limit = 10,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        DateOnly start, end;
        
        if (startDate.HasValue && endDate.HasValue)
        {
            start = DateOnly.FromDateTime(startDate.Value);
            end = DateOnly.FromDateTime(endDate.Value);
        }
        else
        {
            end = DateOnly.FromDateTime(DateTime.UtcNow);
            start = end.AddDays(-days);
        }

        var incidents = await _incidentRepository.GetIncidentsAsync(start, end);

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
        var released = actionSummary.GetValueOrDefault("RELEASED", 0);
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
                Department = g.Where(i => !string.IsNullOrEmpty(i.Department))
                             .Select(i => i.Department)
                             .FirstOrDefault() ?? "",
                TotalAlerts = g.Count(),
                // Convert 1000-scale risk score to 100-scale for display
                RiskScore = (int)Math.Round(g.Max(i => i.RiskScore ?? 0) / 10.0)
            })
            .OrderByDescending(u => u.TotalAlerts)
            .Take(10)
            .Select(u => new Dictionary<string, object>
            {
                { "user_email", u.UserEmail },
                { "login_name", u.LoginName },
                { "department", u.Department },
                { "total_alerts", u.TotalAlerts },
                { "risk_score", u.RiskScore },
                { "risk_level", GetRiskLevelFromScore(u.RiskScore * 10) } // Convert back to 1000 scale for level
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
                    { "released", released },
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
    /// Helper method to get risk level from score (1000 scale)
    /// </summary>
    private string GetRiskLevelFromScore(int score)
    {
        if (score >= 500) return "High";
        if (score >= 250) return "Medium";
        return "Low";
    }
    
    /// <summary>
    /// Get display score for dashboard (1000 -> 100 scale)
    /// </summary>
    public double GetDisplayScore(int riskScore)
    {
        return _riskAnalyzer.GetDisplayScore(riskScore);
    }

    /// <summary>
    /// Calculate and store daily risk scores for all users for a specific date
    /// Should be run at the end of the day (e.g. 23:59)
    /// </summary>
    public async Task<int> CalculateDailyScoresAsync(DateOnly? date = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        
        // Convert DateOnly to UTC DateTime range for the full day
        var startDateTime = targetDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var endDateTime = targetDate.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

        // Get incidents for the day
        var incidents = await _incidentRepository.GetIncidentsAsync(targetDate, targetDate);
        if (!incidents.Any()) return 0;

        var userGroups = incidents
            .GroupBy(i => i.UserEmail)
            .ToList();

        var updatedCount = 0;

        foreach (var group in userGroups)
        {
            var userEmail = group.Key;
            var dailyIncidents = group.ToList();
            
            var totalRiskScore = dailyIncidents.Sum(i => (double)(i.RiskScore ?? 0));
            var maxRiskScore = dailyIncidents.Max(i => i.RiskScore ?? 0);
            var incidentCount = dailyIncidents.Count;
            var avgRiskScore = incidentCount > 0 ? totalRiskScore / incidentCount : 0;

            // Check if record exists
            var existingRecord = await _context.UserDailyRiskScores
                .FirstOrDefaultAsync(r => r.UserEmail == userEmail && r.Date == targetDate);

            if (existingRecord != null)
            {
                existingRecord.DailyRiskScore = totalRiskScore;
                existingRecord.IncidentCount = incidentCount;
                existingRecord.MaxRiskScore = maxRiskScore;
                existingRecord.AvgRiskScore = avgRiskScore;
                // CreatedAt remains original
            }
            else
            {
                var newRecord = new UserDailyRiskScore
                {
                    UserEmail = userEmail,
                    Date = targetDate,
                    DailyRiskScore = totalRiskScore,
                    IncidentCount = incidentCount,
                    MaxRiskScore = maxRiskScore,
                    AvgRiskScore = avgRiskScore,
                    CreatedAt = DateTime.UtcNow
                };
                
                await _context.UserDailyRiskScores.AddAsync(newRecord);
            }
            
            updatedCount++;
        }

        await _context.SaveChangesAsync();
        return updatedCount;
    }

    /// <summary>
    /// Get user daily scores for a date range
    /// </summary>
    public async Task<List<UserDailyRiskScore>> GetUserDailyScoresAsync(string userEmail, DateOnly startDate, DateOnly endDate)
    {
        return await _context.UserDailyRiskScores
            .Where(r => r.UserEmail == userEmail && r.Date >= startDate && r.Date <= endDate)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    /// <summary>
    /// Get weekly trend metrics
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserWeeklyTrendAsync(string userEmail)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-7);
        
        var scores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);
        
        return new Dictionary<string, object>
        {
            { "period", "weekly" },
            { "average_daily_score", scores.Any() ? scores.Average(s => s.DailyRiskScore) : 0 },
            { "total_score", scores.Sum(s => s.DailyRiskScore) },
            { "max_score", scores.Any() ? scores.Max(s => s.DailyRiskScore) : 0 },
            { "incident_count", scores.Sum(s => s.IncidentCount) },
            { "scores", scores }
        };
    }

    /// <summary>
    /// Get monthly trend metrics
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserMonthlyTrendAsync(string userEmail)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30);
        
        var scores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);
        
        return new Dictionary<string, object>
        {
            { "period", "monthly" },
            { "average_daily_score", scores.Any() ? scores.Average(s => s.DailyRiskScore) : 0 },
            { "total_score", scores.Sum(s => s.DailyRiskScore) },
            { "max_score", scores.Any() ? scores.Max(s => s.DailyRiskScore) : 0 },
            { "incident_count", scores.Sum(s => s.IncidentCount) },
            { "scores", scores }
        };
    }

    /// <summary>
    /// Get quarterly (3-month) trend metrics
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserQuarterlyTrendAsync(string userEmail)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-90);
        
        var scores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);
        
        return new Dictionary<string, object>
        {
            { "period", "quarterly" },
            { "average_daily_score", scores.Any() ? scores.Average(s => s.DailyRiskScore) : 0 },
            { "total_score", scores.Sum(s => s.DailyRiskScore) },
            { "max_score", scores.Any() ? scores.Max(s => s.DailyRiskScore) : 0 },
            { "incident_count", scores.Sum(s => s.IncidentCount) },
            { "scores", scores }
        };
    }

    /// <summary>
    /// Detect anomalies for a user based on their history
    /// </summary>
    public async Task<List<string>> DetectUserAnomaliesAsync(string userEmail)
    {
        var anomalies = new List<string>();
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30);
        
        var scores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);
        if (scores.Count < 5) return anomalies; // Not enough data
        
        // Calculate baseline stats (excluding today)
        var history = scores.Where(s => s.Date < endDate).ToList();
        if (!history.Any()) return anomalies;
        
        var avgScore = history.Average(s => s.DailyRiskScore);
        var stdDev = CalculateStdDev(history.Select(s => s.DailyRiskScore));
        
        // Check recent scores (last 3 days)
        var recent = scores.Where(s => s.Date >= endDate.AddDays(-3)).ToList();
        
        foreach (var score in recent)
        {
            // If score is > Mean + 3*StdDev => Anomaly
            if (score.DailyRiskScore > avgScore + (3 * stdDev) && score.DailyRiskScore > 100) // Minimum threshold
            {
                anomalies.Add($"High Risk Score Anomaly on {score.Date}: {score.DailyRiskScore} (Baseline: {avgScore:F1})");
            }
        }
        
        return anomalies;
    }
    
    private double CalculateStdDev(IEnumerable<double> values)
    {
        double ret = 0;
        if (values.Any())
        {
            int count = values.Count();
            if (count > 1)
            {
                double avg = values.Average();
                double sum = values.Sum(d => Math.Pow(d - avg, 2));
                ret = Math.Sqrt((sum) / (count - 1));
            }
        }
        return ret;
    }

    /// <summary>
    /// Get risky users report with trend analysis
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetRiskyUsersReportAsync(string period)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly startDate;
        
        switch (period.ToLower())
        {
            case "weekly": startDate = endDate.AddDays(-7); break;
            case "monthly": startDate = endDate.AddDays(-30); break;
            case "quarterly": startDate = endDate.AddDays(-90); break;
            default: startDate = endDate.AddDays(-30); break;
        }

        // Get all scores in range
        var scores = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        var userGroups = scores.GroupBy(s => s.UserEmail);
        var result = new List<Dictionary<string, object>>();

        foreach (var group in userGroups)
        {
            var userScores = group.OrderBy(s => s.Date).ToList();
            if (!userScores.Any()) continue;

            var latest = userScores.Last();
            var first = userScores.First();
            
            // Calculate trend (Simple difference for now)
            var trendChange = latest.DailyRiskScore - first.DailyRiskScore;
            
            // Calculate average score over the period
            var avgScore = userScores.Average(s => s.DailyRiskScore);
            
            // Max score in period
            var maxScore = userScores.Max(s => s.MaxRiskScore);
            
            // Total incidents
            var totalIncidents = userScores.Sum(s => s.IncidentCount);
            
            // Filter out low risk users (noise reduction) - keep if significant activity or risk
            if (maxScore < 20 && userScores.Count < 3) continue; 

            result.Add(new Dictionary<string, object>
            {
                { "user_email", group.Key },
                { "current_score", latest.DailyRiskScore },
                { "avg_score", avgScore },
                { "max_score", maxScore },
                { "incident_count", totalIncidents },
                { "trend_change", trendChange },
                { "trend_direction", trendChange > 0 ? "increasing" : trendChange < 0 ? "decreasing" : "stable" },
                { "period", period }
            });
        }

        return result.OrderByDescending(r => (double)r["current_score"]).ToList();
    }
}