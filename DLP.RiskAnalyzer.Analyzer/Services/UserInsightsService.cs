using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Helpers;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class UserInsightsService
{
    private readonly AnalyzerDbContext _context;

    public UserInsightsService(AnalyzerDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserDailyRiskScore>> GetUserDailyScoresAsync(string userEmail, DateOnly startDate, DateOnly endDate)
    {
        return await _context.UserDailyRiskScores
            .Where(r => r.UserEmail == userEmail && r.Date >= startDate && r.Date <= endDate)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    public async Task<UserComprehensiveInsightsResponse> GetUserComprehensiveInsightsAsync(string userEmail, string period)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = period.ToLower() switch
        {
            "daily"  => endDate.AddDays(-7),
            "weekly" => endDate.AddDays(-14),
            _        => PeriodHelper.GetStartDate(endDate, period)
        };

        var dailyScores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);

        var weeklyScores = await GetUserDailyScoresAsync(userEmail, endDate.AddDays(-RiskConstants.Periods.WeeklyDays), endDate);
        var monthlyScores = await GetUserDailyScoresAsync(userEmail, endDate.AddDays(-RiskConstants.Periods.MonthlyDays), endDate);
        var quarterlyScores = await GetUserDailyScoresAsync(userEmail, endDate.AddDays(-RiskConstants.Periods.QuarterlyDays), endDate);

        return new UserComprehensiveInsightsResponse
        {
            DailyScores = dailyScores,
            Period = period,
            Summary = new UserInsightsSummary
            {
                TotalIncidents = dailyScores.Sum(s => s.IncidentCount),
                AvgDailyScore  = dailyScores.Any() ? Math.Round(dailyScores.Average(s => s.DailyRiskScore), 2) : 0,
                MaxDailyScore  = dailyScores.Any() ? Math.Round(dailyScores.Max(s => s.DailyRiskScore), 2) : 0,
                DaysActive     = dailyScores.Count
            },
            PeriodAverages = new Dictionary<string, PeriodAverage>
            {
                ["weekly"]    = BuildPeriodAverage(weeklyScores),
                ["monthly"]   = BuildPeriodAverage(monthlyScores),
                ["quarterly"] = BuildPeriodAverage(quarterlyScores)
            },
            ActionBreakdown = new ActionBreakdown
            {
                TotalBlocks      = dailyScores.Sum(s => s.BlockCount),
                TotalPermits     = dailyScores.Sum(s => s.PermitCount),
                TotalQuarantines = dailyScores.Sum(s => s.QuarantineCount),
                TotalReleased    = dailyScores.Sum(s => s.ReleasedCount)
            }
        };
    }

    public async Task<UserTrendResponse> GetUserWeeklyTrendAsync(string userEmail) =>
        await BuildUserTrendAsync(userEmail, "weekly", -RiskConstants.Periods.WeeklyDays);

    public async Task<UserTrendResponse> GetUserMonthlyTrendAsync(string userEmail) =>
        await BuildUserTrendAsync(userEmail, "monthly", -RiskConstants.Periods.MonthlyDays);

    public async Task<UserTrendResponse> GetUserQuarterlyTrendAsync(string userEmail) =>
        await BuildUserTrendAsync(userEmail, "quarterly", -RiskConstants.Periods.QuarterlyDays);

    public async Task<List<string>> DetectUserAnomaliesAsync(string userEmail)
    {
        var anomalies = new List<string>();
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-RiskConstants.Periods.MonthlyDays);

        var scores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);
        if (scores.Count < RiskConstants.Thresholds.MinDataPointsForAnomaly)
            return anomalies;

        var history = scores.Where(s => s.Date < endDate).ToList();
        if (!history.Any()) return anomalies;

        var avgScore = history.Average(s => s.DailyRiskScore);
        var stdDev = CalculateStdDev(history.Select(s => s.DailyRiskScore));
        var recent = scores.Where(s => s.Date >= endDate.AddDays(-3)).ToList();

        foreach (var score in recent)
        {
            if (score.DailyRiskScore > avgScore + (RiskConstants.Thresholds.AnomalySigma * stdDev) &&
                score.DailyRiskScore > RiskConstants.Thresholds.AnomalyMinScore)
            {
                anomalies.Add($"High Risk Score Anomaly on {score.Date}: {score.DailyRiskScore} (Baseline: {avgScore:F1})");
            }
        }

        return anomalies;
    }

    public async Task<List<RiskyUserReportItem>> GetRiskyUsersReportAsync(string period)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = PeriodHelper.GetStartDate(endDate, period);

        var scores = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        var result = new List<RiskyUserReportItem>();

        foreach (var group in scores.GroupBy(s => s.UserEmail))
        {
            var userScores = group.OrderBy(s => s.Date).ToList();
            if (!userScores.Any()) continue;

            var latest = userScores.Last();
            var first = userScores.First();
            var trendChange = latest.DailyRiskScore - first.DailyRiskScore;
            var maxScore = userScores.Max(s => s.MaxRiskScore);

            if (maxScore < RiskConstants.Thresholds.LowRiskNoiseMax && userScores.Count < RiskConstants.Thresholds.MinDaysForTrend)
                continue;

            result.Add(new RiskyUserReportItem
            {
                UserEmail      = group.Key,
                CurrentScore   = latest.DailyRiskScore,
                AvgScore       = userScores.Average(s => s.DailyRiskScore),
                MaxScore       = maxScore,
                IncidentCount  = userScores.Sum(s => s.IncidentCount),
                TrendChange    = trendChange,
                TrendDirection = trendChange > 0 ? "increasing" : trendChange < 0 ? "decreasing" : "stable",
                Period         = period
            });
        }

        return result.OrderByDescending(r => r.CurrentScore).ToList();
    }

    public async Task<List<TopRiskyUserItem>> GetTopRiskyUsersFromDailyScoresAsync(string period, int limit = 10, int page = 1, int pageSize = 20)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        const int minDaysRequired = RiskConstants.Thresholds.MinDaysForTrend;
        const double minScore = RiskConstants.Thresholds.MinRiskScoreForTopUsers;

        if (period.ToLower() is "24h" or "daily")
            return await GetTopUsersFromIncidentsRealTimeAsync(limit);

        var startDate = PeriodHelper.GetStartDate(endDate, period);

        var query = _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .GroupBy(r => new { r.UserEmail, r.EmailAddress })
            .Select(g => new {
                UserEmail = g.Key.UserEmail,
                EmailAddress = g.Key.EmailAddress,
                FullName = g.Max(s => s.FullName),
                Team = g.Max(s => s.Team),
                RiskScore = g.Average(s => s.DailyRiskScore),
                MaxDailyScore = g.Max(s => s.DailyRiskScore),
                TotalIncidents = g.Sum(s => s.IncidentCount),
                TotalBlocks = g.Sum(s => s.BlockCount),
                TotalQuarantines = g.Sum(s => s.QuarantineCount),
                DaysWithActivity = g.Count()
            })
            .Where(u => u.DaysWithActivity >= minDaysRequired && u.RiskScore >= minScore);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedUsers = await query
            .OrderByDescending(u => u.RiskScore)
            .ThenByDescending(u => u.DaysWithActivity)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return pagedUsers.Select(u => new TopRiskyUserItem
        {
            UserEmail        = GetValidUserIdentifier(u.UserEmail, u.EmailAddress, u.FullName),
            FullName         = u.FullName ?? "",
            Team             = u.Team ?? "",
            RiskScore        = u.RiskScore,
            MaxDailyScore    = u.MaxDailyScore,
            TotalIncidents   = u.TotalIncidents,
            TotalBlocks      = u.TotalBlocks,
            TotalQuarantines = u.TotalQuarantines,
            DaysWithActivity = u.DaysWithActivity,
            MinDaysRequired  = minDaysRequired,
            Period           = period,
            Page             = page,
            PageSize         = pageSize,
            TotalCount       = totalCount,
            TotalPages       = totalPages
        }).ToList();
    }

    public async Task<List<DailySummaryScoreItem>> GetDailySummaryFromDailyScoresAsync(DateOnly startDate, DateOnly endDate)
    {
        var dailySummaries = await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .GroupBy(r => r.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalIncidents = g.Sum(s => s.IncidentCount),
                UniqueUsers = g.Count(),
                AvgRiskScore = g.Average(s => s.DailyRiskScore),
                MaxRiskScore = g.Max(s => s.DailyRiskScore),
                HighRiskCount = g.Count(s => s.DailyRiskScore >= RiskConstants.Thresholds.HighDailyScore),
                CriticalRiskCount = g.Count(s => s.DailyRiskScore >= RiskConstants.Thresholds.CriticalDailyScore),
                TotalBlocks = g.Sum(s => s.BlockCount),
                TotalQuarantines = g.Sum(s => s.QuarantineCount)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        return dailySummaries.Select(d => new DailySummaryScoreItem
        {
            Date              = d.Date.ToString("yyyy-MM-dd"),
            TotalIncidents    = d.TotalIncidents,
            UniqueUsers       = d.UniqueUsers,
            AvgRiskScore      = Math.Round(d.AvgRiskScore, 1),
            MaxRiskScore      = Math.Round(d.MaxRiskScore, 1),
            HighRiskCount     = d.HighRiskCount,
            CriticalRiskCount = d.CriticalRiskCount,
            TotalBlocks       = d.TotalBlocks,
            TotalQuarantines  = d.TotalQuarantines
        }).ToList();
    }

    private async Task<UserTrendResponse> BuildUserTrendAsync(string userEmail, string period, int daysBack)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(daysBack);
        var scores = await GetUserDailyScoresAsync(userEmail, startDate, endDate);

        return new UserTrendResponse
        {
            Period            = period,
            AverageDailyScore = scores.Any() ? scores.Average(s => s.DailyRiskScore) : 0,
            TotalScore        = scores.Sum(s => s.DailyRiskScore),
            MaxScore          = scores.Any() ? scores.Max(s => s.DailyRiskScore) : 0,
            IncidentCount     = scores.Sum(s => s.IncidentCount),
            Scores            = scores
        };
    }

    private async Task<List<TopRiskyUserItem>> GetTopUsersFromIncidentsRealTimeAsync(int limit = 10)
    {
        var startTime = DateTime.UtcNow.AddHours(-24);

        var userGroups = await _context.Incidents
            .Where(i => i.Timestamp >= startTime)
            .GroupBy(i => new { i.UserEmail, i.EmailAddress, i.FullName })
            .Select(g => new {
                UserEmail = g.Key.UserEmail,
                EmailAddress = g.Key.EmailAddress,
                FullName = g.Key.FullName,
                TotalIncidents = g.Count(),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                MaxRiskScore = g.Max(i => (double)(i.RiskScore ?? 0)),
                TotalBlocks = g.Count(i => i.Action == "BLOCK" || i.Action == "BLOCKED"),
                TotalQuarantines = g.Count(i => i.Action == "QUARANTINE" || i.Action == "QUARANTINED"),
            })
            .OrderByDescending(u => u.AvgRiskScore)
            .Take(limit)
            .ToListAsync();

        return userGroups.Select(u => {
            double riskScore = Math.Min(100.0,
                (u.AvgRiskScore * 0.50) +
                (u.MaxRiskScore * 0.30) +
                Math.Min(20.0, Math.Log10(u.TotalIncidents + 1) * 10.0));

            return new TopRiskyUserItem
            {
                UserEmail        = GetValidUserIdentifier(u.UserEmail, u.EmailAddress, u.FullName),
                RiskScore        = Math.Round(riskScore, 1),
                MaxDailyScore    = Math.Round(u.MaxRiskScore, 1),
                TotalIncidents   = u.TotalIncidents,
                TotalBlocks      = u.TotalBlocks,
                TotalQuarantines = u.TotalQuarantines,
                DaysWithActivity = 1,
                Period           = "24h"
            };
        }).ToList();
    }

    private static PeriodAverage BuildPeriodAverage(List<UserDailyRiskScore> scores) => new()
    {
        AvgScore         = scores.Any() ? Math.Round(scores.Average(s => s.DailyRiskScore), 2) : 0,
        TotalIncidents   = scores.Sum(s => s.IncidentCount),
        TotalBlocks      = scores.Sum(s => s.BlockCount),
        TotalQuarantines = scores.Sum(s => s.QuarantineCount)
    };

    public async Task<object> GetHighImpactAlertsAsync(int days = 7, int minMaxMatches = RiskConstants.Thresholds.HighImpactMinMatches, int minDailyRiskScore = 0, int page = 1, int pageSize = 20)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var query = _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .Where(r => r.MaxMaxMatches >= minMaxMatches)
            .Where(r => r.DailyRiskScore >= minDailyRiskScore);

        var totalCount = await query.CountAsync();

        var paginatedHits = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.MaxMaxMatches)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var alertsWithDetails = new List<object>();
        
        foreach (var alert in paginatedHits)
        {
            var startOfDay = alert.Date.ToDateTime(TimeOnly.MinValue);
            var endOfDay = alert.Date.ToDateTime(TimeOnly.MaxValue);
            
            var incidentDetails = await _context.Incidents
                .Where(i => i.UserEmail == alert.UserEmail)
                .Where(i => i.Timestamp >= startOfDay && i.Timestamp <= endOfDay)
                .OrderByDescending(i => i.MaxMatches)
                .Take(5)
                .Select(i => new {
                    FileName = i.FileName ?? "",
                    Destination = i.Destination ?? "",
                    Channel = i.Channel ?? "",
                    Action = i.Action ?? "",
                    Policy = i.Policy ?? "",
                    i.MaxMatches,
                    Timestamp = i.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            var impactScore = Math.Min(100.0, (alert.MaxMaxMatches / 10.0) + (alert.DailyRiskScore * 0.5));

            alertsWithDetails.Add(new {
                user_email = alert.UserEmail,
                full_name = alert.FullName ?? "",
                team = alert.Team ?? "",
                impact_score = Math.Round(impactScore, 1),
                max_max_matches = alert.MaxMaxMatches,
                highest_risk_date = alert.Date.ToString("yyyy-MM-dd"),
                daily_risk_score = Math.Round(alert.DailyRiskScore, 1),
                incident_count = alert.IncidentCount,
                block_count = alert.BlockCount,
                quarantine_count = alert.QuarantineCount,
                severity_level = alert.MaxMaxMatches >= RiskConstants.Thresholds.SeverityCriticalMatches ? "Critical" :
                                 alert.MaxMaxMatches >= RiskConstants.Thresholds.SeverityHighMatches ? "High" :
                                 alert.MaxMaxMatches >= RiskConstants.Thresholds.HighImpactMinMatches ? "Medium" : "Low",
                incident_details = incidentDetails
            });
        }

        return new {
            data = alertsWithDetails,
            pagination = new {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        };
    }

    internal static double CalculateStdDev(IEnumerable<double> source)
    {
        var values = source as IReadOnlyList<double> ?? source.ToList();
        if (values.Count < 2) return 0;

        double avg = values.Average();
        double sumSquares = values.Sum(d => (d - avg) * (d - avg));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }

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
