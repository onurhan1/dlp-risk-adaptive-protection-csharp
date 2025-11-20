using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;

/// <summary>
/// Repository interface for Incident data access operations
/// </summary>
public interface IIncidentRepository
{
    Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate, int page, int pageSize);
    Task<List<Incident>> GetIncidentsByUserAsync(string userEmail, DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetIncidentsByDepartmentAsync(DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetIncidentsWithoutRiskScoreAsync();
    Task<int> GetPreviousIncidentsCountAsync(string userEmail, DateTime beforeDate);
    Task<int> UpdateIncidentsAsync(IEnumerable<Incident> incidents);
    Task<List<Incident>> GetIncidentsByChannelAsync(DateOnly startDate, DateOnly endDate);
    Task<List<Incident>> GetRecentIncidentsAsync(int count);
    Task<List<Incident>> GetIncidentsForAnomalyDetectionAsync(string userEmail, DateOnly startDate, DateOnly endDate);
    Task SaveAnomalyAsync(AnomalyDetection anomaly);
    Task<List<AnomalyDetection>> GetAnomaliesAsync(DateOnly startDate, DateOnly endDate, string? severity);
}

