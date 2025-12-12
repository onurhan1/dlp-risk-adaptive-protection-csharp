using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<LogsController> _logger;
    private readonly InternalApiOptions _internalApiOptions;

    public LogsController(
        AuditLogService auditLogService,
        ILogger<LogsController> logger,
        IOptions<InternalApiOptions> internalApiOptions)
    {
        _auditLogService = auditLogService;
        _logger = logger;
        _internalApiOptions = internalApiOptions.Value;
    }

    [HttpGet("audit")]
    public async Task<ActionResult<AuditLogsResponse>> GetAuditLogs(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? eventType,
        [FromQuery] string? userName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 1000) pageSize = 100;

            var logs = await _auditLogService.GetAuditLogsAsync(
                startDate, endDate, eventType, userName, page, pageSize, cancellationToken);

            var total = await _auditLogService.GetAuditLogsCountAsync(
                startDate, endDate, eventType, userName, cancellationToken);

            return Ok(new AuditLogsResponse
            {
                Logs = logs,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching audit logs");
            return StatusCode(500, new { detail = "An error occurred while fetching audit logs" });
        }
    }

    [HttpGet("audit/event-types")]
    public async Task<ActionResult<List<string>>> GetEventTypes(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get distinct event types from audit logs
            var distinctTypes = await _auditLogService.GetDistinctEventTypesAsync(cancellationToken);
            return Ok(distinctTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching event types");
            return StatusCode(500, new { detail = "An error occurred while fetching event types" });
        }
    }

    [HttpGet("application")]
    public async Task<ActionResult<ApplicationLogsResponse>> GetApplicationLogs(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? level,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 1000) pageSize = 100;

            // Get Collector service logs from audit_logs table (EventType = "CollectorService")
            var collectorLogs = await _auditLogService.GetAuditLogsAsync(
                startDate, endDate, "CollectorService", null, page, pageSize, cancellationToken);

            var total = await _auditLogService.GetAuditLogsCountAsync(
                startDate, endDate, "CollectorService", null, cancellationToken);

            // Convert AuditLog to ApplicationLogEntry
            var applicationLogs = collectorLogs.Select(log => new ApplicationLogEntry
            {
                Timestamp = log.Timestamp,
                Level = log.Success ? "Information" : "Error",
                Category = "DLP.RiskAnalyzer.Collector",
                Message = log.Action,
                Exception = log.ErrorMessage
            }).ToList();

            var response = new ApplicationLogsResponse
            {
                Logs = applicationLogs,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching application logs");
            return StatusCode(500, new { detail = "An error occurred while fetching application logs" });
        }
    }

    [HttpPost("application/collector")]
    public async Task<ActionResult> LogCollectorEvent(
        [FromBody] CollectorLogRequest request,
        [FromHeader(Name = "X-Internal-Secret")] string? internalSecret,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify internal secret
            if (string.IsNullOrWhiteSpace(_internalApiOptions.SharedSecret) ||
                !string.Equals(internalSecret, _internalApiOptions.SharedSecret, StringComparison.Ordinal))
            {
                return Unauthorized(new { detail = "Invalid or missing internal secret" });
            }

            // Save collector log as audit log
            var auditLog = new AuditLog
            {
                Timestamp = request.Timestamp,
                EventType = "CollectorService",
                UserName = "CollectorService",
                Action = request.Message,
                Details = request.Details,
                Success = request.Success,
                ErrorMessage = request.ErrorMessage
            };

            await _auditLogService.SaveAuditLogAsync(auditLog, cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving collector log");
            return StatusCode(500, new { detail = "An error occurred while saving collector log" });
        }
    }
}

public class AuditLogsResponse
{
    public List<AuditLog> Logs { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ApplicationLogsResponse
{
    public string? Message { get; set; }
    public List<ApplicationLogEntry> Logs { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ApplicationLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

public class CollectorLogRequest
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}