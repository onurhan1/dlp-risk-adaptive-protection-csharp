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
                total_incidents = g.Count()
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
                { "total_incidents", u.total_incidents }
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
}

