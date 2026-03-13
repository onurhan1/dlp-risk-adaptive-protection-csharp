using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Helpers;
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
    private readonly UserInsightsService _userInsights;

    public RiskAnalyzerService(
        IIncidentRepository incidentRepository,
        AnalyzerDbContext context,
        UserInsightsService? userInsights = null)
    {
        _incidentRepository = incidentRepository;
        _context = context;
        _riskAnalyzer = new Shared.Services.RiskAnalyzer();
        _userInsights = userInsights ?? new UserInsightsService(context);
    }

    /// <summary>
    /// Get user risk trends (DB-side aggregation)
    /// </summary>
    public async Task<List<UserRiskTrend>> GetUserRiskTrendsAsync(int days = 30, string? user = null)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var dtos = await _incidentRepository.GetUserRiskTrendsAggregatedAsync(startDate, endDate, user);

        return dtos.Select(d => new UserRiskTrend
        {
            UserEmail = d.UserEmail,
            Date = d.Date,
            TotalIncidents = d.TotalIncidents,
            RiskScore = d.MaxRiskScore,
            TrendDirection = "stable"
        }).ToList();
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

        // Optimized: Group in database
        var summaries = await _context.Incidents
            .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) && 
                        i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue))
            .GroupBy(i => i.Department)
            .Select(g => new DepartmentSummary
            {
                Department = g.Key ?? "Unknown",
                TotalIncidents = g.Count(),
                // Use legacy thresholds (50+) - SQL translated
                HighRiskCount = g.Count(i => (i.RiskScore ?? 0) >= 50),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count(),
                Date = endDate
            })
            .ToListAsync();

        return summaries;
    }
    /// <summary>
    /// Get daily summaries (DB-side aggregation)
    /// </summary>
    public async Task<List<DailySummary>> GetDailySummariesAsync(int days = 7)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var dtos = await _incidentRepository.GetDailySummariesAggregatedAsync(startDate, endDate);

        return dtos.Select(d => new DailySummary
        {
            Date = d.Date,
            TotalIncidents = d.TotalIncidents,
            HighRiskCount = d.HighRiskUserCount,
            AvgRiskScore = d.AvgRiskScore,
            UniqueUsers = d.UniqueUsers,
            DepartmentsAffected = d.DepartmentsAffected
        }).ToList();
    }

    /// <summary>
    /// Get risk heatmap data (DB-side aggregation)
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

        var items = await _incidentRepository.GetHeatmapAggregatedAsync(
            startDate.Value, endDate.Value, dimension);

        return new RiskHeatmapData
        {
            Labels = items.Select(i => i.Label).ToList(),
            Values = items.Select(i => i.Count).ToList(),
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
            try
            {
                await CalculateRiskScoresAsync();
            }
            catch (Exception ex)
            {
                // CRITICAL: Log exception but don't fail the entire process
                // This prevents incidents from being created without risk scores
                _logger.LogError(ex, "CRITICAL: Failed to calculate risk scores after processing {Count} incidents. Incidents may have NULL risk_score!", processedCount);
                // Re-throw so AnalyzerBackgroundService can handle it
                throw;
            }
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
        Dictionary<string, NdaDomain> ndaDomains;
        try
        {
            ndaDomains = await _context.NdaDomains
                .AsNoTracking()
                .ToDictionaryAsync(d => d.Domain.ToLower(), d => d);
            
            if (ndaDomains.Count == 0)
            {
                _logger.LogWarning("WARNING: NdaDomains table is empty! Risk calculation may be incomplete.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Failed to load NdaDomains table. This is likely the cause of NULL risk_scores!");
            throw new InvalidOperationException("NdaDomains table load failed - risk score calculation aborted", ex);
        }

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
    /// Get paginated user list with risk scores from user_daily_risk_scores (last 30 days average).
    /// P-01 fix: Grouping, search, and pagination are now pushed to PostgreSQL instead of
    /// loading all rows into memory (previously up to 300 K+ rows per call).
    /// </summary>
    public async Task<UserListResponse> GetUserListAsync(int page = 1, int pageSize = 15, string? search = null)
    {
        var endDate   = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30);

        var grouped = _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .GroupBy(r => r.UserEmail)
            .Select(g => new
            {
                UserEmail      = g.Key,
                AvgScore       = g.Average(r => r.DailyRiskScore),
                TotalIncidents = g.Sum(r => r.IncidentCount),
                LastDate       = g.Max(r => r.Date),
                FullName = g.Where(r => r.FullName != null && r.FullName != string.Empty)
                            .OrderByDescending(r => r.Date)
                            .Select(r => r.FullName)
                            .FirstOrDefault(),
                Team = g.Where(r => r.Team != null && r.Team != string.Empty)
                        .OrderByDescending(r => r.Date)
                        .Select(r => r.Team)
                        .FirstOrDefault()
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower().Trim();
            grouped = grouped.Where(u =>
                u.UserEmail.ToLower().Contains(s) ||
                (u.FullName != null && u.FullName.ToLower().Contains(s)) ||
                (u.Team     != null && u.Team.ToLower().Contains(s)));
        }

        var total  = await grouped.CountAsync();
        var offset = (page - 1) * pageSize;

        var pagedUsers = await grouped
            .OrderByDescending(u => u.AvgScore)
            .ThenByDescending(u => u.LastDate)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync();

        return new UserListResponse
        {
            Users = pagedUsers.Select(u => new UserListItem
            {
                UserEmail        = GetValidUserIdentifier(u.UserEmail, null, u.FullName),
                RiskScore        = Math.Round(u.AvgScore, 1),
                TotalIncidents   = u.TotalIncidents,
                LastIncidentDate = u.LastDate.ToString("yyyy-MM-dd"),
                FullName         = u.FullName ?? string.Empty,
                Team             = u.Team     ?? string.Empty
            }).ToList(),
            Total    = total,
            Page     = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Get channel activity breakdown (DB-side aggregation)
    /// </summary>
    public async Task<ChannelActivityResponse> GetChannelActivityAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        int days = 30)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            startDate = endDate.Value.AddDays(-days);
        }

        var channels = await _incidentRepository.GetChannelBreakdownAggregatedAsync(
            startDate.Value, endDate.Value);

        var total = channels.Sum(c => c.TotalIncidents);

        return new ChannelActivityResponse
        {
            Channels = channels.Select(c => new ChannelActivityItem
            {
                Channel        = c.Channel,
                TotalIncidents = c.TotalIncidents,
                Percentage     = total > 0 ? Math.Round((c.TotalIncidents / (double)total) * 100, 1) : 0,
                CriticalCount  = c.CriticalCount,
                HighCount      = c.HighCount,
                MediumCount    = c.MediumCount,
                LowCount       = c.LowCount
            }).ToList(),
            Total = total,
            DateRange = new DateRangeInfo
            {
                Start = startDate.Value.ToString("yyyy-MM-dd"),
                End   = endDate.Value.ToString("yyyy-MM-dd")
            }
        };
    }

    /// <summary>
    /// Get IOB detections (limited fetch from DB with explicit cap)
    /// </summary>
    public async Task<List<IOBDetectionItem>> GetIOBDetectionsAsync(
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
            startDate.Value, endDate.Value, maxRows: 1000);

        var iobCounts = new Dictionary<string, (int Count, HashSet<string> Users)>();

        foreach (var incident in incidents)
        {
            var iobs = _riskAnalyzer.DetectIOB(incident);
            foreach (var iob in iobs)
            {
                if (!iobCounts.TryGetValue(iob, out var data))
                {
                    data = (0, new HashSet<string>());
                    iobCounts[iob] = data;
                }
                iobCounts[iob] = (data.Count + 1, data.Users);
                data.Users.Add(incident.UserEmail);
            }
        }

        return iobCounts
            .Select(kv => new IOBDetectionItem
            {
                Code          = kv.Key,
                Count         = kv.Value.Count,
                UsersAffected = kv.Value.Users.Count
            })
            .OrderByDescending(i => i.Count)
            .ToList();
    }

    /// <summary>
    /// Get top users by day with their daily alert counts (DB-side aggregation)
    /// </summary>
    public async Task<List<TopUserItem>> GetTopUsersByDayAsync(
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

        var topUsers = await _incidentRepository.GetTopUsersAggregatedAsync(start, end, 35, limit);

        return topUsers.Select(u => new TopUserItem
        {
            UserEmail    = GetValidUserIdentifier(u.UserEmail, u.EmailAddress, u.FullName),
            LoginName    = u.LoginName ?? "",
            EmailAddress = !string.IsNullOrEmpty(u.EmailAddress) ? u.EmailAddress : u.UserEmail,
            TotalAlerts  = u.TotalAlerts,
            RiskScore    = u.MaxRiskScore,
            Department   = u.Department ?? "",
            RiskLevel    = GetRiskLevelFromScore(u.MaxRiskScore)
        }).ToList();
    }

    /// <summary>
    /// Get top rules by day with their daily alert counts
    /// </summary>
    public async Task<List<TopRuleItem>> GetTopRulesByDayAsync(
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

        var ruleStats = await _context.Incidents
            .Where(i => i.Timestamp >= start.ToDateTime(TimeOnly.MinValue) && 
                        i.Timestamp <= end.ToDateTime(TimeOnly.MaxValue))
            .Where(i => i.Policy != null && i.Policy != "")
            .GroupBy(i => i.Policy)
            .Select(g => new
            {
                RuleName = g.Key!,
                TotalAlerts = g.Count(),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count()
            })
            .OrderByDescending(r => r.TotalAlerts)
            .Take(limit)
            .ToListAsync();

        return ruleStats.Select(r => new TopRuleItem
        {
            RuleName     = r.RuleName,
            TotalAlerts  = r.TotalAlerts,
            AvgRiskScore = Math.Round(r.AvgRiskScore, 1),
            UniqueUsers  = r.UniqueUsers
        }).ToList();
    }

    /// <summary>
    /// Get comprehensive daily report data for a specific date
    /// </summary>
    public async Task<DailyReportResponse> GetDailyReportDataAsync(DateTime date)
    {
        var targetDate = DateOnly.FromDateTime(date);
        var incidents = await _incidentRepository.GetIncidentsAsync(targetDate, targetDate);

        var actionSummary = incidents
            .GroupBy(i => i.Action?.ToUpper() ?? "UNKNOWN")
            .ToDictionary(g => g.Key, g => g.Count());

        var authorized = actionSummary.GetValueOrDefault("AUTHORIZED", 0);
        var block = actionSummary.GetValueOrDefault("BLOCK", 0) + actionSummary.GetValueOrDefault("BLOCKED", 0);
        var quarantine = actionSummary.GetValueOrDefault("QUARANTINE", 0) + actionSummary.GetValueOrDefault("QUARANTINED", 0);
        var released = actionSummary.GetValueOrDefault("RELEASED", 0);
        var total = incidents.Count;

        var topUsers = incidents
            .GroupBy(i => new { i.UserEmail, i.EmailAddress, i.FullName })
            .Select(g => new
            {
                UserEmail = g.Key.UserEmail,
                EmailAddress = g.Key.EmailAddress,
                FullName = g.Key.FullName,
                LoginName = g.Where(i => !string.IsNullOrEmpty(i.LoginName))
                            .Select(i => i.LoginName)
                            .FirstOrDefault() ?? "",
                Department = g.Where(i => !string.IsNullOrEmpty(i.Department))
                             .Select(i => i.Department)
                             .FirstOrDefault() ?? "",
                TotalAlerts = g.Count(),
                RiskScore = (int)Math.Round(g.Max(i => i.RiskScore ?? 0) * 1.0)
            })
            .OrderByDescending(u => u.TotalAlerts)
            .Take(10)
            .Select(u => new DailyReportTopUser
            {
                UserEmail   = GetValidUserIdentifier(u.UserEmail, u.EmailAddress, u.FullName),
                LoginName   = u.LoginName,
                Department  = u.Department,
                TotalAlerts = u.TotalAlerts,
                RiskScore   = u.RiskScore,
                RiskLevel   = GetRiskLevelFromScore(u.RiskScore)
            })
            .ToList();

        var topPolicies = await GetTopPoliciesWithRulesAsync(incidents);

        var channelBreakdown = incidents
            .Where(i => !string.IsNullOrEmpty(i.Channel))
            .GroupBy(i => i.Channel!)
            .Select(g => new DailyReportChannel
            {
                Channel     = g.Key,
                TotalAlerts = g.Count(),
                Percentage  = total > 0 ? Math.Round((g.Count() / (double)total) * 100, 1) : 0
            })
            .OrderByDescending(c => c.TotalAlerts)
            .ToList();

        var topDestinations = GetDestinationSummary(incidents, 10);

        return new DailyReportResponse
        {
            Date = date.ToString("yyyy-MM-dd"),
            ActionSummary = new ActionSummary
            {
                Authorized = authorized,
                Block      = block,
                Quarantine = quarantine,
                Released   = released,
                Total      = total
            },
            TopUsers         = topUsers,
            TopPolicies      = topPolicies,
            ChannelBreakdown = channelBreakdown,
            TopDestinations  = topDestinations
        };
    }

    /// <summary>
    /// Get top policies with their top 3 rules
    /// </summary>
    private async Task<List<DailyReportPolicy>> GetTopPoliciesWithRulesAsync(List<Incident> incidents)
    {
        var policyRuleData = new Dictionary<string, Dictionary<string, int>>();

        foreach (var incident in incidents)
        {
            var policyName = incident.Policy ?? "Unknown Policy";
            
            if (!policyRuleData.ContainsKey(policyName))
                policyRuleData[policyName] = new Dictionary<string, int>();

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
                            string ruleName = policyName;
                            
                            if (trigger.TryGetProperty("rule_name", out var ruleNameElement) && 
                                ruleNameElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var ruleValue = ruleNameElement.GetString();
                                if (!string.IsNullOrEmpty(ruleValue))
                                    ruleName = ruleValue;
                            }
                            else if (trigger.TryGetProperty("RuleName", out var ruleNameCamelElement) && 
                                ruleNameCamelElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var ruleValue = ruleNameCamelElement.GetString();
                                if (!string.IsNullOrEmpty(ruleValue))
                                    ruleName = ruleValue;
                            }
                            
                            policyRuleData[policyName][ruleName] = policyRuleData[policyName].GetValueOrDefault(ruleName, 0) + 1;
                        }
                        continue;
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
            
            policyRuleData[policyName][policyName] = policyRuleData[policyName].GetValueOrDefault(policyName, 0) + 1;
        }

        await Task.CompletedTask;

        return policyRuleData
            .Select(p => new DailyReportPolicy
            {
                PolicyName  = p.Key,
                TotalAlerts = p.Value.Values.Sum(),
                TopRules    = p.Value
                    .OrderByDescending(r => r.Value)
                    .Take(3)
                    .Select(r => new DailyReportRule { RuleName = r.Key, AlertCount = r.Value })
                    .ToList()
            })
            .OrderByDescending(p => p.TotalAlerts)
            .Take(10)
            .ToList();
    }

    private static List<DailyReportDestination> GetDestinationSummary(List<Incident> incidents, int limit = 10)
    {
        var total = incidents.Count;
        return incidents
            .Where(i => !string.IsNullOrEmpty(i.Destination))
            .GroupBy(i => i.Destination!)
            .Select(g => new DailyReportDestination
            {
                Destination = g.Key,
                TotalAlerts = g.Count(),
                Percentage  = total > 0 ? Math.Round((g.Count() / (double)total) * 100, 1) : 0
            })
            .OrderByDescending(d => d.TotalAlerts)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Helper method to get risk level from score (0-100 scale)
    /// </summary>
    private string GetRiskLevelFromScore(int score)
    {
        if (score >= 50) return "High";
        if (score >= 25) return "Medium";
        return "Low";
    }
    
    /// <summary>
    /// Get display score for dashboard (already 0-100 scale)
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
            
            // Normalized daily score (0-100 scale)
            // Now incident scores are directly 0-100, so we use direct multipliers
            // Formula: (Avg * 0.50) + (Max * 0.30) + MIN(20, LOG10(Count+1) * 10)
            var normalizedScore = Math.Min(100,
                (avgRiskScore * 0.50) +
                (maxRiskScore * 0.30) +
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

    // ── Delegated to UserInsightsService ──────────────────────────────────

    public Task<List<UserDailyRiskScore>> GetUserDailyScoresAsync(string userEmail, DateOnly startDate, DateOnly endDate) =>
        _userInsights.GetUserDailyScoresAsync(userEmail, startDate, endDate);

    public Task<UserComprehensiveInsightsResponse> GetUserComprehensiveInsightsAsync(string userEmail, string period) =>
        _userInsights.GetUserComprehensiveInsightsAsync(userEmail, period);

    public Task<UserTrendResponse> GetUserWeeklyTrendAsync(string userEmail) =>
        _userInsights.GetUserWeeklyTrendAsync(userEmail);

    public Task<UserTrendResponse> GetUserMonthlyTrendAsync(string userEmail) =>
        _userInsights.GetUserMonthlyTrendAsync(userEmail);

    public Task<UserTrendResponse> GetUserQuarterlyTrendAsync(string userEmail) =>
        _userInsights.GetUserQuarterlyTrendAsync(userEmail);

    public Task<List<string>> DetectUserAnomaliesAsync(string userEmail) =>
        _userInsights.DetectUserAnomaliesAsync(userEmail);

    public Task<List<RiskyUserReportItem>> GetRiskyUsersReportAsync(string period) =>
        _userInsights.GetRiskyUsersReportAsync(period);

    public Task<List<TopRiskyUserItem>> GetTopRiskyUsersFromDailyScoresAsync(string period, int limit = 10, int page = 1, int pageSize = 20) =>
        _userInsights.GetTopRiskyUsersFromDailyScoresAsync(period, limit, page, pageSize);

    public Task<List<DailySummaryScoreItem>> GetDailySummaryFromDailyScoresAsync(DateOnly startDate, DateOnly endDate) =>
        _userInsights.GetDailySummaryFromDailyScoresAsync(startDate, endDate);

    public Task<object> GetHighImpactAlertsAsync(int days = 7, int minMaxMatches = 100, int minDailyRiskScore = 0, int page = 1, int pageSize = 20) =>
        _userInsights.GetHighImpactAlertsAsync(days, minMaxMatches, minDailyRiskScore, page, pageSize);

    private static double CalculateStdDev(IEnumerable<double> source) =>
        UserInsightsService.CalculateStdDev(source);

    /// <summary>
    /// Returns the best available user identifier. If the primary value is null, empty, 
    /// or "unknown", it falls back to emailAddress, then fullName.
    /// </summary>
    private static string GetValidUserIdentifier(string? primary, string? emailAddress, string? fullName)
    {
        bool isInvalid = string.IsNullOrWhiteSpace(primary) || 
                         primary.Equals("unknown", StringComparison.OrdinalIgnoreCase);
        
        if (!isInvalid) return primary!;

        if (!string.IsNullOrWhiteSpace(emailAddress)) return emailAddress;
        if (!string.IsNullOrWhiteSpace(fullName)) return fullName;
        
        return primary ?? "unknown";
    }
}