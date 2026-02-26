using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Implementations;

/// <summary>
/// Repository implementation for Incident data access operations
/// </summary>
public class IncidentRepository : IIncidentRepository
{
    private readonly AnalyzerDbContext _context;

    public IncidentRepository(AnalyzerDbContext context)
    {
        _context = context;
    }

    private IQueryable<Incident> DateRangeQuery(DateOnly startDate, DateOnly endDate) =>
        _context.Incidents.Where(i =>
            i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
            i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue));

    public async Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate, int maxRows = 10_000)
    {
        return await DateRangeQuery(startDate, endDate)
            .OrderByDescending(i => i.Timestamp)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate, int page, int pageSize)
    {
        return await DateRangeQuery(startDate, endDate)
            .OrderByDescending(i => i.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsByUserAsync(string userEmail, DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.UserEmail == userEmail &&
                       i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsByDepartmentAsync(DateOnly startDate, DateOnly endDate)
    {
        return await DateRangeQuery(startDate, endDate)
            .Where(i => i.Department != null && i.Department != "")
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsWithoutRiskScoreAsync(int batchSize = 2000)
    {
        return await _context.Incidents
            .Where(i => i.RiskScore == null)
            .OrderBy(i => i.Timestamp)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<int> GetPreviousIncidentsCountAsync(string userEmail, DateTime beforeDate)
    {
        return await _context.Incidents
            .CountAsync(i => i.UserEmail == userEmail && i.Timestamp < beforeDate);
    }

    public async Task<int> GetWeeklyIncidentsCountAsync(string userEmail, DateTime beforeDate)
    {
        var weekAgo = beforeDate.AddDays(-7);
        return await _context.Incidents
            .CountAsync(i => i.UserEmail == userEmail && 
                            i.Timestamp >= weekAgo && 
                            i.Timestamp < beforeDate);
    }

    public async Task<Dictionary<string, int>> GetPolicyRepeatCountsAsync(string userEmail, DateTime beforeDate)
    {
        var counts = await _context.Incidents
            .Where(i => i.UserEmail == userEmail && 
                        i.Timestamp < beforeDate &&
                        i.Policy != null && i.Policy != "")
            .GroupBy(i => i.Policy!)
            .Select(g => new { Policy = g.Key, Count = g.Count() })
            .ToListAsync();
        
        return counts.ToDictionary(x => x.Policy, x => x.Count);
    }

    public async Task<int> UpdateIncidentsAsync(IEnumerable<Incident> incidents)
    {
        var list = incidents.ToList();
        foreach (var incident in list)
        {
            _context.Incidents.Update(incident);
        }

        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            foreach (var inc in list)
                _context.Entry(inc).State = EntityState.Detached;

            var saved = 0;
            foreach (var inc in list)
            {
                try
                {
                    _context.Incidents.Update(inc);
                    await _context.SaveChangesAsync();
                    saved++;
                }
                catch (DbUpdateException)
                {
                    _context.Entry(inc).State = EntityState.Detached;
                }
            }
            return saved;
        }
    }

    public async Task<List<Incident>> GetIncidentsByChannelAsync(DateOnly startDate, DateOnly endDate)
    {
        return await DateRangeQuery(startDate, endDate)
            .Where(i => i.Channel != null && i.Channel != "")
            .ToListAsync();
    }

    public async Task<List<Incident>> GetRecentIncidentsAsync(int count)
    {
        return await _context.Incidents
            .OrderByDescending(i => i.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsForAnomalyDetectionAsync(string userEmail, DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.UserEmail == userEmail &&
                       i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
    }

    public async Task SaveAnomalyAsync(AnomalyDetection anomaly)
    {
        _context.AnomalyDetections.Add(anomaly);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AnomalyDetection>> GetAnomaliesAsync(DateOnly startDate, DateOnly endDate, string? severity)
    {
        var query = _context.AnomalyDetections
            .Where(a => a.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       a.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue));

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(a => a.Severity == severity);
        }

        return await query.OrderByDescending(a => a.Timestamp).ToListAsync();
    }

    // ── Aggregation methods ─────────────────────────────────────────────────
    // Pattern: DB-side WHERE filtering + bounded materialize + in-memory aggregation.
    // The date range filter bounds the data set; aggregation happens on the filtered results.
    // This pattern works with all EF Core providers (PostgreSQL, InMemory, SQLite).

    public async Task<List<UserRiskTrendDto>> GetUserRiskTrendsAggregatedAsync(
        DateOnly startDate, DateOnly endDate, string? user = null)
    {
        var query = DateRangeQuery(startDate, endDate);
        if (!string.IsNullOrEmpty(user))
            query = query.Where(i => i.UserEmail == user);

        var incidents = await query.ToListAsync();

        return incidents
            .GroupBy(i => new { i.UserEmail, Date = DateOnly.FromDateTime(i.Timestamp.Date) })
            .Select(g => new UserRiskTrendDto(
                g.Key.UserEmail,
                g.Key.Date,
                g.Count(),
                g.Max(i => i.RiskScore ?? 0)))
            .OrderBy(t => t.UserEmail)
            .ThenBy(t => t.Date)
            .ToList();
    }

    public async Task<List<DailySummaryDto>> GetDailySummariesAggregatedAsync(
        DateOnly startDate, DateOnly endDate)
    {
        var incidents = await DateRangeQuery(startDate, endDate).ToListAsync();

        return incidents
            .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
            .Select(g => new DailySummaryDto(
                g.Key,
                g.Count(),
                g.Average(i => (double)(i.RiskScore ?? 0)),
                g.Select(i => i.UserEmail).Distinct().Count(),
                g.GroupBy(i => i.UserEmail)
                 .Count(ug => ug.Max(i => i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold),
                g.Where(i => !string.IsNullOrEmpty(i.Department))
                 .Select(i => i.Department!).Distinct().Count()))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<List<ChannelBreakdownDto>> GetChannelBreakdownAggregatedAsync(
        DateOnly startDate, DateOnly endDate)
    {
        var incidents = await DateRangeQuery(startDate, endDate)
            .Where(i => i.Channel != null && i.Channel != "")
            .ToListAsync();

        return incidents
            .GroupBy(i => i.Channel!)
            .Select(g => new ChannelBreakdownDto(
                g.Key,
                g.Count(),
                g.Count(i => (i.RiskScore ?? 0) >= 75),
                g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.HighThreshold &&
                             (i.RiskScore ?? 0) < 75),
                g.Count(i => (i.RiskScore ?? 0) >= RiskConstants.RiskScores.MediumThreshold &&
                             (i.RiskScore ?? 0) < RiskConstants.RiskScores.HighThreshold),
                g.Count(i => (i.RiskScore ?? 0) < RiskConstants.RiskScores.MediumThreshold)))
            .OrderByDescending(c => c.TotalIncidents)
            .ToList();
    }

    public async Task<List<HeatmapItemDto>> GetHeatmapAggregatedAsync(
        DateOnly startDate, DateOnly endDate, string dimension, int limit = 10)
    {
        IQueryable<Incident> query = dimension switch
        {
            "department" => DateRangeQuery(startDate, endDate)
                .Where(i => i.Department != null && i.Department != ""),
            "channel" => DateRangeQuery(startDate, endDate)
                .Where(i => i.Channel != null && i.Channel != ""),
            _ => DateRangeQuery(startDate, endDate)
        };

        var incidents = await query.ToListAsync();

        Func<Incident, string> keySelector = dimension switch
        {
            "department" => i => i.Department!,
            "user" => i => i.UserEmail,
            _ => i => i.Channel!
        };

        return incidents
            .GroupBy(keySelector)
            .Select(g => new HeatmapItemDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToList();
    }

    public async Task<List<TopUserDto>> GetTopUsersAggregatedAsync(
        DateOnly startDate, DateOnly endDate, int minRiskScore = 35, int limit = 20)
    {
        var incidents = await DateRangeQuery(startDate, endDate).ToListAsync();

        return incidents
            .GroupBy(i => i.UserEmail)
            .Select(g => new TopUserDto(
                g.Key,
                g.Count(),
                g.Max(i => i.RiskScore ?? 0),
                g.FirstOrDefault(i => !string.IsNullOrEmpty(i.Department))?.Department,
                g.FirstOrDefault(i => !string.IsNullOrEmpty(i.LoginName))?.LoginName,
                g.FirstOrDefault(i => !string.IsNullOrEmpty(i.EmailAddress))?.EmailAddress))
            .Where(u => u.MaxRiskScore >= minRiskScore)
            .OrderByDescending(u => u.MaxRiskScore)
            .ThenByDescending(u => u.TotalAlerts)
            .Take(limit)
            .ToList();
    }
}