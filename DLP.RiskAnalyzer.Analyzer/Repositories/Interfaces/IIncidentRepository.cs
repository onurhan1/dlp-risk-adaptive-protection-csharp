using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;

public record UserRiskTrendDto(string UserEmail, DateOnly Date, int TotalIncidents, int MaxRiskScore);
public record ChannelBreakdownDto(string Channel, int TotalIncidents, int CriticalCount, int HighCount, int MediumCount, int LowCount);
public record HeatmapItemDto(string Label, int Count);
public record DailySummaryDto(DateOnly Date, int TotalIncidents, double AvgRiskScore, int UniqueUsers, int HighRiskUserCount, int DepartmentsAffected);
public record TopUserDto(string UserEmail, int TotalAlerts, int MaxRiskScore, string? Department, string? LoginName, string? EmailAddress, string? FullName);

/// <summary>
/// Repository interface for Incident data access operations
/// </summary>
public interface IIncidentRepository
{
    Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate, int maxRows = 10_000);
    Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate, int page, int pageSize);
    Task<List<Incident>> GetIncidentsByUserAsync(string userEmail, DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetIncidentsByDepartmentAsync(DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetIncidentsWithoutRiskScoreAsync(int batchSize = 2000);
    Task<int> GetPreviousIncidentsCountAsync(string userEmail, DateTime beforeDate);
    Task<int> GetWeeklyIncidentsCountAsync(string userEmail, DateTime beforeDate);
    Task<Dictionary<string, int>> GetPolicyRepeatCountsAsync(string userEmail, DateTime beforeDate);
    Task<int> UpdateIncidentsAsync(IEnumerable<Incident> incidents);
    Task<List<Incident>> GetIncidentsByChannelAsync(DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetRecentIncidentsAsync(int count);
    Task<List<Incident>> GetIncidentsForAnomalyDetectionAsync(string userEmail, DateOnly startDate, DateOnly endDate);
    Task SaveAnomalyAsync(AnomalyDetection anomaly);
    Task<List<AnomalyDetection>> GetAnomaliesAsync(DateOnly startDate, DateOnly endDate, string? severity);

    // DB-side aggregation methods
    Task<List<UserRiskTrendDto>> GetUserRiskTrendsAggregatedAsync(DateOnly startDate, DateOnly endDate, string? user = null);
    Task<List<DailySummaryDto>> GetDailySummariesAggregatedAsync(DateOnly startDate, DateOnly endDate);
    Task<List<ChannelBreakdownDto>> GetChannelBreakdownAggregatedAsync(DateOnly startDate, DateOnly endDate);
    Task<List<HeatmapItemDto>> GetHeatmapAggregatedAsync(DateOnly startDate, DateOnly endDate, string dimension, int limit = 10);
    Task<List<TopUserDto>> GetTopUsersAggregatedAsync(DateOnly startDate, DateOnly endDate, int minRiskScore = 35, int limit = 20);
}

