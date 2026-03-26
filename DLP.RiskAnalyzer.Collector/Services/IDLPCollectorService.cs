using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Interface for the DLP API collector service. Handles token acquisition, incident fetching, and Redis publishing.
/// </summary>
public interface IDLPCollectorService
{
    Task<string> GetAccessTokenAsync();
    Task<List<DLPIncident>> FetchIncidentsAsync(DateTime startTime, DateTime endTime, int page = 1, int pageSize = 100);
    Task PushToRedisStreamAsync(Incident incident);
    Task PushReleasedIncidentToRedisStreamAsync(long incidentId, string incidentTime, string action, string taskName, string adminName, string comments, string updateTime);
}
