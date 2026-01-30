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
    /// Get paginated user list with risk scores from user_daily_risk_scores (last 30 days average)
    /// Uses same algorithm as Dashboard Top Risky Users for consistency
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserListAsync(int page = 1, int pageSize = 15, string? search = null)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30); // Last 30 days

        // Get daily scores from user_daily_risk_scores table
        var dailyScores = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        // Calculate average risk score per user (same as Dashboard)
        var userGroups = dailyScores
            .GroupBy(s => s.UserEmail)
            .Select(g => {
                var scores = g.ToList();
                var avgScore = scores.Average(s => s.DailyRiskScore);
                var totalIncidents = scores.Sum(s => s.IncidentCount);
                var lastDate = scores.Max(s => s.Date);
                var fullName = scores.FirstOrDefault(s => !string.IsNullOrEmpty(s.FullName))?.FullName;
                var team = scores.FirstOrDefault(s => !string.IsNullOrEmpty(s.Team))?.Team;
                
                return new {
                    user_email = g.Key,
                    risk_score = Math.Round(avgScore, 1),
                    total_incidents = totalIncidents,
                    last_incident_date = lastDate,
                    full_name = fullName ?? "",
                    team = team ?? ""
                };
            })
            .OrderByDescending(u => u.risk_score)
            .ThenByDescending(u => u.last_incident_date)
            .ToList();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower().Trim();
            userGroups = userGroups
                .Where(u => u.user_email.ToLower().Contains(searchLower) ||
                           u.full_name.ToLower().Contains(searchLower) ||
                           u.team.ToLower().Contains(searchLower))
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
                { "last_incident_date", u.last_incident_date.ToString("yyyy-MM-dd") },
                { "full_name", u.full_name },
                { "team", u.team }
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
            
            var sumRiskScore = dailyIncidents.Sum(i => (double)(i.RiskScore ?? 0));
            var maxRiskScore = dailyIncidents.Max(i => i.RiskScore ?? 0);
            var incidentCount = dailyIncidents.Count;
            var avgRiskScore = incidentCount > 0 ? sumRiskScore / incidentCount : 0;
            
            // Calculate action counts
            var blockCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("BLOCK", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("BLOCKED", StringComparison.OrdinalIgnoreCase)));
            var permitCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("PERMIT", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("PERMITTED", StringComparison.OrdinalIgnoreCase) ||
                                      i.Action.Equals("AUTHORIZED", StringComparison.OrdinalIgnoreCase)));
            var quarantineCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("QUARANTINE", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("QUARANTINED", StringComparison.OrdinalIgnoreCase)));
            var releasedCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("RELEASE", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("RELEASED", StringComparison.OrdinalIgnoreCase)));
            
            // Calculate max matches stats
            var maxMaxMatches = dailyIncidents.Max(i => i.MaxMatches);
            var avgMaxMatches = incidentCount > 0 ? dailyIncidents.Average(i => (double)i.MaxMatches) : 0;
            
            // Get team and full_name from first incident that has them
            var firstWithTeam = dailyIncidents.FirstOrDefault(i => !string.IsNullOrEmpty(i.Team) || !string.IsNullOrEmpty(i.Department));
            var firstWithName = dailyIncidents.FirstOrDefault(i => !string.IsNullOrEmpty(i.FullName));
            var team = firstWithTeam?.Team ?? firstWithTeam?.Department;
            var fullName = firstWithName?.FullName;
            
            // Normalized daily score (1-100 scale)
            // Formula: MIN(100, (Avg/500*50) + (Max/500*30) + MIN(20, LOG10(Count+1)*10))
            var normalizedScore = Math.Min(100,
                (avgRiskScore / 500.0 * 50) +
                (maxRiskScore / 500.0 * 30) +
                Math.Min(20, Math.Log10(incidentCount + 1) * 10)
            );
            var totalRiskScore = Math.Round(normalizedScore, 2);

            // Check if record exists
            var existingRecord = await _context.UserDailyRiskScores
                .FirstOrDefaultAsync(r => r.UserEmail == userEmail && r.Date == targetDate);

            if (existingRecord != null)
            {
                existingRecord.DailyRiskScore = totalRiskScore;
                existingRecord.IncidentCount = incidentCount;
                existingRecord.MaxRiskScore = maxRiskScore;
                existingRecord.AvgRiskScore = avgRiskScore;
                existingRecord.BlockCount = blockCount;
                existingRecord.PermitCount = permitCount;
                existingRecord.QuarantineCount = quarantineCount;
                existingRecord.ReleasedCount = releasedCount;
                existingRecord.MaxMaxMatches = maxMaxMatches;
                existingRecord.AvgMaxMatches = avgMaxMatches;
                if (!string.IsNullOrEmpty(team)) existingRecord.Team = team;
                if (!string.IsNullOrEmpty(fullName)) existingRecord.FullName = fullName;
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
                    BlockCount = blockCount,
                    PermitCount = permitCount,
                    QuarantineCount = quarantineCount,
                    ReleasedCount = releasedCount,
                    MaxMaxMatches = maxMaxMatches,
                    AvgMaxMatches = avgMaxMatches,
                    Team = team,
                    FullName = fullName,
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
    /// Get comprehensive user insights with daily scores, action breakdown, and period averages
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserComprehensiveInsightsAsync(string userEmail, string period)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly startDate;
        
        switch (period.ToLower())
        {
            case "daily":
                startDate = endDate.AddDays(-7);
                break;
            case "weekly":
                startDate = endDate.AddDays(-14); // 2 weeks
                break;
            case "monthly":
                startDate = endDate.AddDays(-30);
                break;
            case "quarterly":
                startDate = endDate.AddDays(-90);
                break;
            default:
                startDate = endDate.AddDays(-30);
                break;
        }
        
        var dailyScores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);
        
        // Calculate period averages (all-time windows for comparison)
        var weeklyScores = await GetUserDailyScoresAsync(userEmail, endDate.AddDays(-7), endDate);
        var monthlyScores = await GetUserDailyScoresAsync(userEmail, endDate.AddDays(-30), endDate);
        var quarterlyScores = await GetUserDailyScoresAsync(userEmail, endDate.AddDays(-90), endDate);
        
        var periodAverages = new Dictionary<string, object>
        {
            { "weekly", new {
                avgScore = weeklyScores.Any() ? Math.Round(weeklyScores.Average(s => s.DailyRiskScore), 2) : 0,
                totalIncidents = weeklyScores.Sum(s => s.IncidentCount),
                totalBlocks = weeklyScores.Sum(s => s.BlockCount),
                totalQuarantines = weeklyScores.Sum(s => s.QuarantineCount)
            }},
            { "monthly", new {
                avgScore = monthlyScores.Any() ? Math.Round(monthlyScores.Average(s => s.DailyRiskScore), 2) : 0,
                totalIncidents = monthlyScores.Sum(s => s.IncidentCount),
                totalBlocks = monthlyScores.Sum(s => s.BlockCount),
                totalQuarantines = monthlyScores.Sum(s => s.QuarantineCount)
            }},
            { "quarterly", new {
                avgScore = quarterlyScores.Any() ? Math.Round(quarterlyScores.Average(s => s.DailyRiskScore), 2) : 0,
                totalIncidents = quarterlyScores.Sum(s => s.IncidentCount),
                totalBlocks = quarterlyScores.Sum(s => s.BlockCount),
                totalQuarantines = quarterlyScores.Sum(s => s.QuarantineCount)
            }}
        };
        
        // Summary for selected period
        var summary = new Dictionary<string, object>
        {
            { "totalIncidents", dailyScores.Sum(s => s.IncidentCount) },
            { "avgDailyScore", dailyScores.Any() ? Math.Round(dailyScores.Average(s => s.DailyRiskScore), 2) : 0 },
            { "maxDailyScore", dailyScores.Any() ? Math.Round(dailyScores.Max(s => s.DailyRiskScore), 2) : 0 },
            { "minDailyScore", dailyScores.Any() ? Math.Round(dailyScores.Min(s => s.DailyRiskScore), 2) : 0 },
            { "totalBlockCount", dailyScores.Sum(s => s.BlockCount) },
            { "totalPermitCount", dailyScores.Sum(s => s.PermitCount) },
            { "totalQuarantineCount", dailyScores.Sum(s => s.QuarantineCount) },
            { "totalReleasedCount", dailyScores.Sum(s => s.ReleasedCount) },
            { "maxMaxMatches", dailyScores.Any() ? dailyScores.Max(s => s.MaxMaxMatches) : 0 },
            { "avgMaxMatches", dailyScores.Any() ? Math.Round(dailyScores.Average(s => s.AvgMaxMatches), 2) : 0 }
        };
        
        // Get user info from latest score
        var latestScore = dailyScores.OrderByDescending(s => s.Date).FirstOrDefault();
        
        return new Dictionary<string, object>
        {
            { "userEmail", userEmail },
            { "fullName", latestScore?.FullName ?? "" },
            { "team", latestScore?.Team ?? "" },
            { "period", period },
            { "startDate", startDate.ToString("yyyy-MM-dd") },
            { "endDate", endDate.ToString("yyyy-MM-dd") },
            { "summary", summary },
            { "periodAverages", periodAverages },
            { "dailyScores", dailyScores.Select(s => new {
                date = s.Date.ToString("yyyy-MM-dd"),
                dailyRiskScore = Math.Round(s.DailyRiskScore, 2),
                incidentCount = s.IncidentCount,
                maxRiskScore = s.MaxRiskScore,
                avgRiskScore = Math.Round(s.AvgRiskScore, 2),
                blockCount = s.BlockCount,
                permitCount = s.PermitCount,
                quarantineCount = s.QuarantineCount,
                releasedCount = s.ReleasedCount,
                maxMaxMatches = s.MaxMaxMatches,
                avgMaxMatches = Math.Round(s.AvgMaxMatches, 2)
            }).ToList() }
        };
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

    /// <summary>
    /// Get top risky users from user_daily_risk_scores table
    /// period: 24h, weekly, monthly, quarterly
    /// Uses normalized daily score formula
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTopRiskyUsersFromDailyScoresAsync(string period, int limit = 10)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly startDate;
        int minDaysRequired = 1; // Minimum days with activity required for multi-day periods
        
        switch (period.ToLower())
        {
            case "24h":
            case "daily":
                startDate = endDate; // Today only
                minDaysRequired = 1;
                break;
            case "weekly":
                startDate = endDate.AddDays(-7);
                minDaysRequired = 2; // At least 2 days in a week
                break;
            case "monthly":
            case "1month":
                startDate = endDate.AddDays(-30);
                minDaysRequired = 3; // At least 3 days in a month
                break;
            case "quarterly":
            case "3month":
                startDate = endDate.AddDays(-90);
                minDaysRequired = 5; // At least 5 days in 3 months
                break;
            case "6month":
                startDate = endDate.AddDays(-180);
                minDaysRequired = 7; // At least 7 days in 6 months
                break;
            case "yearly":
            case "12month":
                startDate = endDate.AddDays(-365);
                minDaysRequired = 10; // At least 10 days in a year
                break;
            default:
                startDate = endDate.AddDays(-1);
                minDaysRequired = 1;
                break;
        }

        // Get all scores in range
        var scores = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        // Group by user and calculate aggregated metrics with consistency factor
        var userGroups = scores
            .GroupBy(s => s.UserEmail)
            .Select(g => {
                var userScores = g.ToList();
                var daysWithActivity = userScores.Count;
                var totalIncidents = userScores.Sum(s => s.IncidentCount);
                var avgDailyScore = userScores.Average(s => s.DailyRiskScore);
                var maxDailyScore = userScores.Max(s => s.DailyRiskScore);
                var totalBlocks = userScores.Sum(s => s.BlockCount);
                var totalQuarantines = userScores.Sum(s => s.QuarantineCount);
                var latestScore = userScores.OrderByDescending(s => s.Date).FirstOrDefault();
                
                // For multi-day periods, recalculate aggregated score using same formula
                // Formula: MIN(100, (Avg/500*50) + (Max/500*30) + MIN(20, LOG10(Count+1)*10))
                var avgRiskScore = userScores.Average(s => s.AvgRiskScore);
                var maxRiskScore = userScores.Max(s => s.MaxRiskScore);
                
                var baseScore = Math.Min(100,
                    (avgRiskScore / 500.0 * 50) +
                    (maxRiskScore / 500.0 * 30) +
                    Math.Min(20, Math.Log10(totalIncidents + 1) * 10)
                );
                
                // HYBRID APPROACH: Apply consistency factor for multi-day periods
                // Single-day events get penalized, persistent behavior gets full score
                // consistency_factor = MIN(1, days_with_activity / minDaysRequired)
                // For 24h/daily period, no penalty applied (consistencyFactor = 1)
                double consistencyFactor = period.ToLower() == "24h" || period.ToLower() == "daily" 
                    ? 1.0 
                    : Math.Min(1.0, (double)daysWithActivity / minDaysRequired);
                
                var adjustedScore = baseScore * consistencyFactor;
                
                return new {
                    UserEmail = g.Key,
                    FullName = latestScore?.FullName,
                    Team = latestScore?.Team,
                    BaseScore = Math.Round(baseScore, 1),
                    AdjustedScore = Math.Round(adjustedScore, 1),
                    ConsistencyFactor = Math.Round(consistencyFactor, 2),
                    AvgDailyScore = Math.Round(avgDailyScore, 1),
                    MaxDailyScore = Math.Round(maxDailyScore, 1),
                    TotalIncidents = totalIncidents,
                    TotalBlocks = totalBlocks,
                    TotalQuarantines = totalQuarantines,
                    DaysWithActivity = daysWithActivity,
                    MinDaysRequired = minDaysRequired
                };
            })
            .Where(u => u.TotalIncidents > 0)
            .OrderByDescending(u => u.AdjustedScore) // Sort by adjusted score (with consistency factor)
            .Take(limit)
            .ToList();

        return userGroups.Select(u => new Dictionary<string, object>
        {
            { "user_email", u.UserEmail },
            { "full_name", u.FullName ?? "" },
            { "team", u.Team ?? "" },
            { "risk_score", u.AdjustedScore }, // Use adjusted score as main score
            { "base_score", u.BaseScore },
            { "consistency_factor", u.ConsistencyFactor },
            { "avg_daily_score", u.AvgDailyScore },
            { "max_daily_score", u.MaxDailyScore },
            { "total_incidents", u.TotalIncidents },
            { "total_blocks", u.TotalBlocks },
            { "total_quarantines", u.TotalQuarantines },
            { "days_with_activity", u.DaysWithActivity },
            { "min_days_required", u.MinDaysRequired },
            { "period", period }
        }).ToList();
    }

    /// <summary>
    /// Get daily summary aggregated from user_daily_risk_scores
    /// Returns total incidents, avg risk score, unique users per day
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetDailySummaryFromDailyScoresAsync(DateOnly startDate, DateOnly endDate)
    {
        var scores = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        // Group by date
        var dailyGroups = scores
            .GroupBy(s => s.Date)
            .Select(g => {
                var dayScores = g.ToList();
                return new Dictionary<string, object>
                {
                    { "date", g.Key.ToString("yyyy-MM-dd") },
                    { "total_incidents", dayScores.Sum(s => s.IncidentCount) },
                    { "unique_users", dayScores.Count },
                    { "avg_risk_score", Math.Round(dayScores.Average(s => s.DailyRiskScore), 1) },
                    { "max_risk_score", Math.Round(dayScores.Max(s => s.DailyRiskScore), 1) },
                    { "high_risk_count", dayScores.Count(s => s.DailyRiskScore >= 50) },
                    { "critical_risk_count", dayScores.Count(s => s.DailyRiskScore >= 75) },
                    { "total_blocks", dayScores.Sum(s => s.BlockCount) },
                    { "total_quarantines", dayScores.Sum(s => s.QuarantineCount) }
                };
            })
            .OrderBy(d => d["date"])
            .ToList();

        return dailyGroups;
    }

    /// <summary>
    /// Get high impact alerts - single-day events with unusually high max_matches
    /// These are potential data exfiltration attempts that would be penalized by consistency factor
    /// </summary>
    public async Task<object> GetHighImpactAlertsAsync(int days = 7, int minMaxMatches = 100, int minDailyRiskScore = 0, int page = 1, int pageSize = 20)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        // Get all scores in range with filters
        var scores = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .Where(r => r.MaxMaxMatches >= minMaxMatches)
            .Where(r => r.DailyRiskScore >= minDailyRiskScore)
            .ToListAsync();

        // Find high-impact events and enrich with incident details
        var highImpactAlertsRaw = scores
            .GroupBy(s => s.UserEmail)
            .Select(g => {
                var userScores = g.ToList();
                var highestDay = userScores.OrderByDescending(s => s.MaxMaxMatches).First();
                var daysWithActivity = userScores.Count;
                
                // Calculate impact score based on max matches and severity
                var impactScore = Math.Min(100, (highestDay.MaxMaxMatches / 10.0) + (highestDay.DailyRiskScore * 0.5));
                
                return new {
                    UserEmail = g.Key,
                    FullName = highestDay.FullName,
                    Team = highestDay.Team,
                    ImpactScore = Math.Round(impactScore, 1),
                    MaxMaxMatches = highestDay.MaxMaxMatches,
                    HighestRiskDate = highestDay.Date,
                    DailyRiskScore = Math.Round(highestDay.DailyRiskScore, 1),
                    IncidentCount = highestDay.IncidentCount,
                    BlockCount = highestDay.BlockCount,
                    QuarantineCount = highestDay.QuarantineCount,
                    DaysWithActivity = daysWithActivity,
                    TotalIncidentsInPeriod = userScores.Sum(s => s.IncidentCount),
                    IsSingleDayEvent = daysWithActivity == 1,
                    SeverityLevel = highestDay.MaxMaxMatches >= 500 ? "Critical" :
                                   highestDay.MaxMaxMatches >= 200 ? "High" :
                                   highestDay.MaxMaxMatches >= 100 ? "Medium" : "Low"
                };
            })
            .OrderByDescending(a => a.HighestRiskDate) // Sort by date (newest first)
            .ThenByDescending(a => a.ImpactScore)
            .ToList();

        // Total count for pagination
        var totalCount = highImpactAlertsRaw.Count;

        // Apply pagination
        var paginatedAlerts = highImpactAlertsRaw
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Fetch incident details for each alert
        var alertsWithDetails = new List<Dictionary<string, object>>();
        
        foreach (var alert in paginatedAlerts)
        {
            // Get incident details from incidents table for the highest risk date
            var startOfDay = alert.HighestRiskDate.ToDateTime(TimeOnly.MinValue);
            var endOfDay = alert.HighestRiskDate.ToDateTime(TimeOnly.MaxValue);
            
            var incidentDetails = await _context.Incidents
                .Where(i => i.UserEmail == alert.UserEmail)
                .Where(i => i.Timestamp >= startOfDay && i.Timestamp <= endOfDay)
                .OrderByDescending(i => i.MaxMatches)
                .Take(5) // Top 5 incidents for this day
                .Select(i => new {
                    i.FileName,
                    Destination = i.Destination,
                    i.Channel,
                    i.Action,
                    i.Policy,
                    i.MaxMatches,
                    EventTimestamp = i.Timestamp
                })
                .ToListAsync();

            var alertDict = new Dictionary<string, object>
            {
                { "user_email", alert.UserEmail },
                { "full_name", alert.FullName ?? "" },
                { "team", alert.Team ?? "" },
                { "impact_score", alert.ImpactScore },
                { "max_max_matches", alert.MaxMaxMatches },
                { "highest_risk_date", alert.HighestRiskDate.ToString("yyyy-MM-dd") },
                { "daily_risk_score", alert.DailyRiskScore },
                { "incident_count", alert.IncidentCount },
                { "block_count", alert.BlockCount },
                { "quarantine_count", alert.QuarantineCount },
                { "days_with_activity", alert.DaysWithActivity },
                { "total_incidents_in_period", alert.TotalIncidentsInPeriod },
                { "is_single_day_event", alert.IsSingleDayEvent },
                { "severity_level", alert.SeverityLevel },
                { "incident_details", incidentDetails.Select(i => new Dictionary<string, object>
                    {
                        { "file_name", i.FileName ?? "" },
                        { "destination", i.Destination ?? "" },
                        { "channel", i.Channel ?? "" },
                        { "action", i.Action ?? "" },
                        { "policy", i.Policy ?? "" },
                        { "max_matches", i.MaxMatches },
                        { "timestamp", i.EventTimestamp.ToString("yyyy-MM-dd HH:mm:ss") }
                    }).ToList()
                }
            };
            
            alertsWithDetails.Add(alertDict);
        }

        return new {
            data = alertsWithDetails,
            pagination = new {
                page = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        };
    }
}