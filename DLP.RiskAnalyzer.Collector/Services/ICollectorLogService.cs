namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Interface for sending Collector service logs to Analyzer service
/// </summary>
public interface ICollectorLogService
{
    Task LogCollectionAsync(
        string message,
        bool success,
        string? errorMessage = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
