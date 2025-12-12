using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class AuditLogService
{
    private readonly AnalyzerDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        AnalyzerDbContext context,
        ILogger<AuditLogService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task LogAsync(
        string eventType,
        string userName,
        string? userRole,
        string action,
        string? resource = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool success = true,
        string? errorMessage = null,
        int? statusCode = null,
        long? durationMs = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                UserName = userName,
                UserRole = userRole,
                Action = action,
                Resource = resource,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Success = success,
                ErrorMessage = errorMessage,
                StatusCode = statusCode,
                DurationMs = durationMs
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);

            // Send to Splunk if enabled
            try
            {
                var splunkService = _serviceProvider.GetService<SplunkService>();
                if (splunkService != null)
                {
                    var splunkEvent = new AuditLogEvent
                    {
                        Timestamp = auditLog.Timestamp,
                        EventType = auditLog.EventType,
                        UserName = auditLog.UserName,
                        UserRole = auditLog.UserRole,
                        Action = auditLog.Action,
                        Resource = auditLog.Resource,
                        IpAddress = auditLog.IpAddress,
                        UserAgent = auditLog.UserAgent,
                        Success = auditLog.Success,
                        ErrorMessage = auditLog.ErrorMessage,
                        StatusCode = auditLog.StatusCode,
                        DurationMs = auditLog.DurationMs
                    };

                    if (!string.IsNullOrWhiteSpace(auditLog.Details))
                    {
                        try
                        {
                            splunkEvent.Details = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Details);
                        }
                        catch
                        {
                            splunkEvent.Details = new Dictionary<string, object> { { "raw", auditLog.Details } };
                        }
                    }

                    await splunkService.SendAuditLogAsync(splunkEvent, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send audit log to Splunk");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audit log");
        }
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? eventType = null,
        string? userName = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.Timestamp <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(l => l.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(l => l.UserName.Contains(userName));
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetAuditLogsCountAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? eventType = null,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.Timestamp <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(l => l.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(l => l.UserName.Contains(userName));
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<string>> GetDistinctEventTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Select(l => l.EventType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);

            // Send to Splunk if enabled
            try
            {
                var splunkService = _serviceProvider.GetService<SplunkService>();
                if (splunkService != null)
                {
                    var splunkEvent = new AuditLogEvent
                    {
                        Timestamp = auditLog.Timestamp,
                        EventType = auditLog.EventType,
                        UserName = auditLog.UserName,
                        UserRole = auditLog.UserRole,
                        Action = auditLog.Action,
                        Resource = auditLog.Resource,
                        IpAddress = auditLog.IpAddress,
                        UserAgent = auditLog.UserAgent,
                        Success = auditLog.Success,
                        ErrorMessage = auditLog.ErrorMessage,
                        StatusCode = auditLog.StatusCode,
                        DurationMs = auditLog.DurationMs
                    };

                    if (!string.IsNullOrWhiteSpace(auditLog.Details))
                    {
                        try
                        {
                            splunkEvent.Details = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Details);
                        }
                        catch
                        {
                            splunkEvent.Details = new Dictionary<string, object> { { "raw", auditLog.Details } };
                        }
                    }

                    await splunkService.SendAuditLogAsync(splunkEvent, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send audit log to Splunk");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audit log");
            throw;
        }
    }
}