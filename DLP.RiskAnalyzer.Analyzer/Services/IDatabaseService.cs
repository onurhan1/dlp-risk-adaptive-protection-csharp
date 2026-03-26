using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IDatabaseService
{
    Task<List<Incident>> GetIncidentsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? user,
        string? department,
        int limit = 100,
        string orderBy = "timestamp_desc");

    Task<List<ExceptionIncidentStats>> GetExceptionIncidentStatsAsync(
        DateTime? startDate, DateTime? endDate);

    Task<Incident?> GetIncidentByIdAsync(int id);

    Task<int> InsertIncidentAsync(Incident incident);

    Task<int> ProcessRedisStreamAsync();

    Task<int> ProcessReleasedIncidentsStreamAsync();
}
