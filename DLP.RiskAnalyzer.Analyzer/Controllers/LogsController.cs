using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        AuditLogService auditLogService,
        ILogger<LogsController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
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
            var eventTypes = await _auditLogService.GetAuditLogsAsync(
                null, null, null, null, 1, 10000, cancellationToken);

            var distinctTypes = eventTypes
                .Select(l => l.EventType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

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
            // For now, return a message that application logs are read from log files
            // In production, you might want to read from a log file or database
            return Ok(new ApplicationLogsResponse
            {
                Message = "Application logs are available in log files. For Splunk integration, logs are automatically sent to Splunk when configured.",
                Logs = new List<ApplicationLogEntry>(),
                Total = 0,
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching application logs");
            return StatusCode(500, new { detail = "An error occurred while fetching application logs" });
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

