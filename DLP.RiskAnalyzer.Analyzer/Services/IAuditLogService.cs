using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IAuditLogService
{
    Task LogAsync(
        string eventType, string userName, string? userRole, string action,
        string? resource = null, string? details = null, string? ipAddress = null,
        string? userAgent = null, bool success = true, string? errorMessage = null,
        int? statusCode = null, long? durationMs = null,
        CancellationToken cancellationToken = default);

    Task<List<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null, DateTime? endDate = null,
        string? eventType = null, string? userName = null,
        int page = 1, int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<int> GetAuditLogsCountAsync(
        DateTime? startDate = null, DateTime? endDate = null,
        string? eventType = null, string? userName = null,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetDistinctEventTypesAsync(CancellationToken cancellationToken = default);

    Task SaveAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
