using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IScheduledJobService
{
    Task<IReadOnlyList<object>> GetJobsAsync(CancellationToken ct = default);
    Task<object> GetCatalogAsync();
    Task<object> CreateAsync(ScheduledJobRequest request, CancellationToken ct = default);
    Task<object> UpdateAsync(int id, ScheduledJobRequest request, CancellationToken ct = default);
    Task<object> ToggleAsync(int id, CancellationToken ct = default);
    Task<object> RunNowAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<object>> GetRunsAsync(int? jobId, int limit, CancellationToken ct = default);
    Task RunDueJobsAsync(CancellationToken ct = default);
}

public record ScheduledJobRequest(
    string Name,
    string? Description,
    string HandlerKey,
    string CronExpression,
    bool Enabled,
    int? LookbackHours,
    int? RetentionDays);
