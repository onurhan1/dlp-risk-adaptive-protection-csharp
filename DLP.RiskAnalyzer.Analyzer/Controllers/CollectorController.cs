using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/collector")]
public class CollectorController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CollectorController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CollectorController(
        IConnectionMultiplexer redis,
        ILogger<CollectorController> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a manual incident collection.
    /// Supports two modes:
    ///   - Date-based: specify startDate and endDate
    ///   - Hour-based: specify lookbackHours
    /// </summary>
    [HttpPost("manual-collect")]
    public async Task<ActionResult> StartManualCollect([FromBody] ManualCollectRequest request)
    {
        try
        {
            DateTime startDate;
            DateTime endDate;

            // Determine mode and calculate date range
            if (request.LookbackHours.HasValue && request.LookbackHours.Value > 0)
            {
                // Hour-based mode
                if (request.LookbackHours.Value > 2160) // 90 days max
                {
                    return BadRequest(new { success = false, detail = "Lookback hours cannot exceed 2160 (90 days)." });
                }

                endDate = DateTime.Now;
                startDate = endDate.AddHours(-request.LookbackHours.Value);
                _logger.LogInformation("Manual collect requested: hour-based, lookback {Hours}h ({Start} to {End})",
                    request.LookbackHours.Value, startDate, endDate);
            }
            else if (!string.IsNullOrEmpty(request.StartDate) && !string.IsNullOrEmpty(request.EndDate))
            {
                // Date-based mode
                if (!TryParseDate(request.StartDate, out startDate))
                {
                    return BadRequest(new { success = false, detail = "Invalid startDate format. Use yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss." });
                }

                if (!TryParseDate(request.EndDate, out endDate))
                {
                    return BadRequest(new { success = false, detail = "Invalid endDate format. Use yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss." });
                }

                // If only date given (no time component), set endDate to end of day
                if (request.EndDate.Length <= 10)
                {
                    endDate = endDate.Date.AddDays(1).AddSeconds(-1); // 23:59:59
                }

                if (startDate >= endDate)
                {
                    return BadRequest(new { success = false, detail = "startDate must be before endDate." });
                }

                var daysDiff = (endDate - startDate).TotalDays;
                if (daysDiff > 730)
                {
                    return BadRequest(new { success = false, detail = "Date range cannot exceed 730 days (2 years)." });
                }

                _logger.LogInformation("Manual collect requested: date-based, {Start} to {End} ({Days} days)",
                    startDate, endDate, daysDiff);
            }
            else
            {
                return BadRequest(new { success = false, detail = "Specify either lookbackHours or both startDate and endDate." });
            }

            // Generate unique job ID
            var jobId = Guid.NewGuid().ToString("N")[..12]; // Short, readable ID

            // Create initial status in Redis
            var db = _redis.GetDatabase();
            var status = new ManualCollectStatus
            {
                JobId = jobId,
                Status = ManualCollectStatusValues.Queued,
                Progress = 0,
                Message = "Çekim kuyruğa eklendi, sıra bekleniyor...",
                StartedAt = DateTime.UtcNow
            };

            var statusKey = $"{DlpConstants.ManualCollectJobKeyPrefix}{jobId}";
            await db.StringSetAsync(statusKey, JsonSerializer.Serialize(status, _jsonOptions), TimeSpan.FromHours(24));

            // Publish command to Redis pub/sub for Collector to pick up
            var command = new ManualCollectCommand
            {
                JobId = jobId,
                StartDate = startDate,
                EndDate = endDate
            };

            var commandJson = JsonSerializer.Serialize(command, _jsonOptions);
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(DlpConstants.ManualCollectChannel), commandJson);

            _logger.LogInformation("Manual collect job {JobId} published to Redis channel. Range: {Start} to {End}",
                jobId, startDate, endDate);

            return Accepted(new { success = true, jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start manual collection");
            return StatusCode(500, new { success = false, detail = "Failed to start manual collection: " + ex.Message });
        }
    }

    /// <summary>
    /// Gets the current status of a manual collection job.
    /// </summary>
    [HttpGet("manual-collect/status/{jobId}")]
    public async Task<ActionResult> GetManualCollectStatus(string jobId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var statusKey = $"{DlpConstants.ManualCollectJobKeyPrefix}{jobId}";
            var statusJson = await db.StringGetAsync(statusKey);

            if (statusJson.IsNullOrEmpty)
            {
                return NotFound(new { success = false, detail = "Job not found or expired." });
            }

            var status = JsonSerializer.Deserialize<ManualCollectStatus>(statusJson!, _jsonOptions);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get manual collect status for job {JobId}", jobId);
            return StatusCode(500, new { success = false, detail = "Failed to get status: " + ex.Message });
        }
    }

    private static bool TryParseDate(string input, out DateTime result)
    {
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss"
        };

        return DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out result);
    }
}
