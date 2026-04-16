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
public class RiskAnalyzerService : IRiskAnalyzerService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly AnalyzerDbContext _context;
    private readonly Shared.Services.RiskAnalyzer _riskAnalyzer;
    private readonly IUserInsightsService _userInsights;

    public RiskAnalyzerService(
        IIncidentRepository incidentRepository,
        AnalyzerDbContext context,
        IUserInsightsService userInsights)
    {
        _incidentRepository = incidentRepository;
        _context = context;
        _riskAnalyzer = new Shared.Services.RiskAnalyzer();
        _userInsights = userInsights;
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
    public async Task<int> ProcessRedisStreamAsync(IRedisStreamProcessor redisProcessor)
    {
        var processedCount = await redisProcessor.ProcessRedisStreamAsync();
        
        // Note: Risk scoring is now handled sequentially in AnalyzerBackgroundService
        // via IRiskScoringService
        
        return processedCount;
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
                EmailAddress = g.Where(r => r.EmailAddress != null && r.EmailAddress != string.Empty)
                            .OrderByDescending(r => r.Date)
                            .Select(r => r.EmailAddress)
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
                (u.EmailAddress != null && u.EmailAddress.ToLower().Contains(s)) ||
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
                UserEmail        = GetValidUserIdentifier(u.UserEmail, u.EmailAddress, u.FullName),
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
            UserEmail    = GetValidUserIdentifier(u.UserEmail, u.EmailAddress, null),
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
                catch (System.Text.Json.JsonException) { /* Intentionally empty — malformed JSON returns safe default */ }
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