using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Extended Risk Analyzer Service with database operations
/// </summary>
public class RiskAnalyzerService
{
    private readonly AnalyzerDbContext _context;
    private readonly Shared.Services.RiskAnalyzer _riskAnalyzer;

    public RiskAnalyzerService(AnalyzerDbContext context)
    {
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

        var query = _context.Incidents
            .Where(i => i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue));

        if (!string.IsNullOrEmpty(user))
        {
            query = query.Where(i => i.UserEmail == user);
        }

        var trends = await query
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
            .ToListAsync();

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

        var summaries = await _context.Incidents
            .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue) &&
                       !string.IsNullOrEmpty(i.Department))
            .GroupBy(i => i.Department)
            .Select(g => new DepartmentSummary
            {
                Department = g.Key!,
                TotalIncidents = g.Count(),
                HighRiskCount = g.Count(i => (i.RiskScore ?? 0) >= 61),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count(),
                Date = endDate
            })
            .ToListAsync();

        return summaries;
    }

    /// <summary>
    /// Get daily summaries
    /// </summary>
    public async Task<List<DailySummary>> GetDailySummariesAsync(int days = 7)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var summaries = await _context.Incidents
            .Where(i => i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
            .Select(g => new DailySummary
            {
                Date = g.Key,
                TotalIncidents = g.Count(),
                HighRiskCount = g.Count(i => (i.RiskScore ?? 0) >= 61),
                AvgRiskScore = g.Average(i => (double)(i.RiskScore ?? 0)),
                UniqueUsers = g.Select(i => i.UserEmail).Distinct().Count(),
                DepartmentsAffected = g.Where(i => !string.IsNullOrEmpty(i.Department))
                                      .Select(i => i.Department!)
                                      .Distinct()
                                      .Count()
            })
            .OrderBy(s => s.Date)
            .ToListAsync();

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

        if (dimension == "department")
        {
            var deptData = await _context.Incidents
                .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue) &&
                           !string.IsNullOrEmpty(i.Department))
                .GroupBy(i => i.Department)
                .Select(g => new { Label = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            labels = deptData.Select(d => d.Label).ToList();
            values = deptData.Select(d => d.Count).ToList();
        }
        else if (dimension == "user")
        {
            var userData = await _context.Incidents
                .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue))
                .GroupBy(i => i.UserEmail)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            labels = userData.Select(d => d.Label).ToList();
            values = userData.Select(d => d.Count).ToList();
        }
        else // channel
        {
            var channelData = await _context.Incidents
                .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue) &&
                           !string.IsNullOrEmpty(i.Channel))
                .GroupBy(i => i.Channel)
                .Select(g => new { Label = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

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
        await dbService.ProcessRedisStreamAsync();
        return await CalculateRiskScoresAsync();
    }

    /// <summary>
    /// Calculate risk scores for incidents without scores
    /// </summary>
    public async Task<int> CalculateRiskScoresAsync()
    {
        var incidentsWithoutScores = await _context.Incidents
            .Where(i => i.RiskScore == null)
            .ToListAsync();

        var updatedCount = 0;
        foreach (var incident in incidentsWithoutScores)
        {
            // Calculate repeat count (how many times this user had similar incidents)
            var repeatCount = await _context.Incidents
                .CountAsync(i => i.UserEmail == incident.UserEmail &&
                                i.Timestamp < incident.Timestamp);

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

        await _context.SaveChangesAsync();
        return updatedCount;
    }

    private int CalculateDataSensitivity(string? dataType, int severity)
    {
        if (string.IsNullOrEmpty(dataType))
            return severity;

        var dataTypeLower = dataType.ToLower();
        
        if (dataTypeLower.Contains("pii") || dataTypeLower.Contains("personal"))
            return Math.Max(severity, 8);
        if (dataTypeLower.Contains("pci") || dataTypeLower.Contains("credit"))
            return Math.Max(severity, 9);
        if (dataTypeLower.Contains("confidential"))
            return Math.Max(severity, 7);

        return severity;
    }

    /// <summary>
    /// Get paginated user list with risk scores
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserListAsync(int page = 1, int pageSize = 15)
    {
        // This method is deprecated - use the endpoint in RiskController instead
        // Keeping for backward compatibility but should not be used
        throw new NotImplementedException("Use RiskController.GetUserList endpoint instead");
    }

    [Obsolete("Use RiskController.GetUserList endpoint instead")]
    private async Task<Dictionary<string, object>> GetUserListAsyncOld(int page = 1, int pageSize = 15)
    {
        var offset = (page - 1) * pageSize;

        var users = await _context.Incidents
            .Where(i => i.RiskScore != null)
            .GroupBy(i => i.UserEmail)
            .Select(g => new
            {
                UserEmail = g.Key,
                MaxRiskScore = g.Max(i => i.RiskScore ?? 0),
                AvgRiskScore = (int)g.Average(i => i.RiskScore ?? 0),
                TotalIncidents = g.Count()
            })
            .OrderByDescending(u => u.MaxRiskScore)
            .ThenByDescending(u => u.TotalIncidents)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync();

        var total = await _context.Incidents
            .Where(i => i.RiskScore != null)
            .Select(i => i.UserEmail)
            .Distinct()
            .CountAsync();

        return new Dictionary<string, object>
        {
            { "users", users },
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

        var channels = await _context.Incidents
            .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue) &&
                       !string.IsNullOrEmpty(i.Channel))
            .GroupBy(i => i.Channel)
            .Select(g => new
            {
                Channel = g.Key!,
                TotalIncidents = g.Count(),
                CriticalCount = g.Count(i => (i.RiskScore ?? 0) >= 91),
                HighCount = g.Count(i => (i.RiskScore ?? 0) >= 61 && (i.RiskScore ?? 0) < 91),
                MediumCount = g.Count(i => (i.RiskScore ?? 0) >= 41 && (i.RiskScore ?? 0) < 61),
                LowCount = g.Count(i => (i.RiskScore ?? 0) < 41)
            })
            .OrderByDescending(c => c.TotalIncidents)
            .ToListAsync();

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

        var incidents = await _context.Incidents
            .Where(i => i.Timestamp >= startDate.Value.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.Value.ToDateTime(TimeOnly.MaxValue))
            .Take(1000)
            .ToListAsync();

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
}

