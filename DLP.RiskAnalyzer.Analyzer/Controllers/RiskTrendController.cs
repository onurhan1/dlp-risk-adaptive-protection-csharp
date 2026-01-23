using Microsoft.AspNetCore.Mvc;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/risk-trends")]
public class RiskTrendController : ControllerBase
{
    private readonly RiskAnalyzerService _riskService;
    private readonly ILogger<RiskTrendController> _logger;

    public RiskTrendController(
        RiskAnalyzerService riskService,
        ILogger<RiskTrendController> logger)
    {
        _riskService = riskService;
        _logger = logger;
    }

    /// <summary>
    /// Get daily risk scores for a user within a date range
    /// </summary>
    [HttpGet("user/{email}/daily")]
    public async Task<ActionResult<List<UserDailyRiskScore>>> GetUserDailyScores(
        string email, 
        [FromQuery] DateOnly? startDate, 
        [FromQuery] DateOnly? endDate)
    {
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startDate ?? end.AddDays(-30);

        var scores = await _riskService.GetUserDailyScoresAsync(email, start, end);
        return Ok(scores);
    }

    /// <summary>
    /// Get weekly trend analysis for a user
    /// </summary>
    [HttpGet("user/{email}/weekly-trend")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserWeeklyTrend(string email)
    {
        var result = await _riskService.GetUserWeeklyTrendAsync(email);
        return Ok(result);
    }

    /// <summary>
    /// Get monthly trend analysis for a user
    /// </summary>
    [HttpGet("user/{email}/monthly-trend")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserMonthlyTrend(string email)
    {
        var result = await _riskService.GetUserMonthlyTrendAsync(email);
        return Ok(result);
    }

    /// <summary>
    /// Get quarterly trend analysis for a user
    /// </summary>
    [HttpGet("user/{email}/quarterly-trend")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserQuarterlyTrend(string email)
    {
        var result = await _riskService.GetUserQuarterlyTrendAsync(email);
        return Ok(result);
    }

    /// <summary>
    /// Get anomaly detection results for a user
    /// </summary>
    [HttpGet("user/{email}/anomalies")]
    public async Task<ActionResult<List<string>>> GetUserAnomalies(string email)
    {
        var anomalies = await _riskService.DetectUserAnomaliesAsync(email);
        return Ok(anomalies);
    }

    /// <summary>
    /// Trigger daily risk score calculation manually
    /// Useful for testing or forcing an update
    /// </summary>
    [HttpPost("calculate-daily")]
    public async Task<ActionResult<int>> CalculateDailyScores([FromQuery] DateOnly? date)
    {
        try
        {
            var count = await _riskService.CalculateDailyScoresAsync(date);
            return Ok(new { message = "Daily calculation completed", updated_users = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating daily scores");
            return StatusCode(500, new { message = "Error calculating daily scores", error = ex.Message });
        }
    }

    /// <summary>
    /// Trigger recalculation of all incident risk scores
    /// WARNING: This operation can be expensive
    /// </summary>
    [HttpPost("recalculate-all")]
    public async Task<ActionResult<int>> RecalculateAllRisks()
    {
        try
        {
            var count = await _riskService.CalculateRiskScoresAsync();
            return Ok(new { message = "Risk score recalculation completed", updated_incidents = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating risk scores");
            return StatusCode(500, new { message = "Error recalculating risk scores", error = ex.Message });
        }
    }

    /// <summary>
    /// Get risky users report for a specific period
    /// Period: weekly, monthly, quarterly
    /// </summary>
    [HttpGet("users/report")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetRiskyUsersReport([FromQuery] string period = "monthly")
    {
        var result = await _riskService.GetRiskyUsersReportAsync(period);
        return Ok(result);
    }
}
