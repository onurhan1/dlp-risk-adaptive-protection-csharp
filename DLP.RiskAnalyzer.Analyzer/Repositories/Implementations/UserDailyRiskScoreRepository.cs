using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Implementations;

public class UserDailyRiskScoreRepository : IUserDailyRiskScoreRepository
{
    private readonly AnalyzerDbContext _context;

    public UserDailyRiskScoreRepository(AnalyzerDbContext context)
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

    public async Task<List<UserDailyRiskScore>> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        return await _context.UserDailyRiskScores
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();
    }

    public async Task<(List<TopRiskyUserItem> Items, int TotalCount, int TotalPages)> GetTopRiskyUsersAsync(
        DateOnly startDate, DateOnly endDate, int minDaysRequired, double minScore, string period, int page, int pageSize)
    {
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

        var items = pagedUsers.Select(u => new TopRiskyUserItem
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

        return (items, totalCount, totalPages);
    }

    public async Task<List<DailySummaryScoreItem>> GetDailySummariesAsync(DateOnly startDate, DateOnly endDate)
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

    public async Task<(List<UserDailyRiskScore> Items, int TotalCount)> GetHighImpactScoresAsync(
        DateOnly startDate, DateOnly endDate, int minMaxMatches, int minDailyRiskScore, int page, int pageSize)
    {
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

        return (paginatedHits, totalCount);
    }

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
