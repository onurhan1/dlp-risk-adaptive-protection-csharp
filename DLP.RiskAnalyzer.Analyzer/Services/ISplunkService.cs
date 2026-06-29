namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Splunk integration service interface - Sends audit and application logs to Splunk HEC
/// </summary>
public interface ISplunkService
{
    Task SendAuditLogAsync(AuditLogEvent logEvent, CancellationToken cancellationToken = default);
    Task SendApplicationLogAsync(string level, string message, string? category = null, Exception? exception = null, CancellationToken cancellationToken = default);
}
