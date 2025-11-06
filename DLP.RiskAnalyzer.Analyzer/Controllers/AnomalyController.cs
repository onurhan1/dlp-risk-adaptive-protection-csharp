using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/risk/[controller]")]
public class AnomalyController : ControllerBase
{
    private readonly AnomalyDetector _anomalyDetector;
    private readonly AnalyzerDbContext _context;
    private readonly DatabaseService _dbService;

    public AnomalyController(
        AnomalyDetector anomalyDetector,
        AnalyzerDbContext context,
        DatabaseService dbService)
    {
        _anomalyDetector = anomalyDetector;
        _context = context;
        _dbService = dbService;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<Dictionary<string, object>>> CalculateAnomalies(
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            var userEmail = request.GetValueOrDefault("user_email")?.ToString();
            var metricType = request.GetValueOrDefault("metric_type", "cloud_upload")?.ToString() ?? "cloud_upload";

            if (string.IsNullOrEmpty(userEmail))
            {
                return BadRequest(new { detail = "user_email is required" });
            }

            // Get current value
            var currentIncidents = await _dbService.GetIncidentsAsync(
                null, null, userEmail, null, 100);

            // Calculate current metric value
            var currentValue = metricType switch
            {
                "cloud_upload" => currentIncidents.Count(i => 
                    (i.Channel ?? "").ToLower().Contains("cloud")),
                "email_count" => currentIncidents.Count(i => 
                    (i.Channel ?? "").ToLower() == "email"),
                _ => currentIncidents.Count
            };

            // Detect anomaly
            var detection = await _anomalyDetector.DetectAnomaliesAsync(
                userEmail, currentValue, metricType);

            // Save if anomaly detected
            if ((bool)(detection.GetValueOrDefault("is_anomaly") ?? false))
            {
                await _anomalyDetector.SaveAnomalyDetectionAsync(
                    userEmail,
                    metricType,
                    currentValue,
                    (double)detection.GetValueOrDefault("baseline_mean", 0.0),
                    (int)detection.GetValueOrDefault("anomaly_score", 0),
                    detection.GetValueOrDefault("severity")?.ToString() ?? "Low");
            }

            return Ok(detection);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("detections")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetAnomalyDetections(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? severity = null)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var detections = await _anomalyDetector.GetAnomalyDetectionsAsync(start, end, severity);
            return Ok(detections);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

