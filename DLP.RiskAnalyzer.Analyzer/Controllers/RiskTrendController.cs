using Microsoft.AspNetCore.Mvc;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/risk-trends")]
public class RiskTrendController : ControllerBase
{
    private readonly IRiskAnalyzerService _riskService;
    private readonly IRiskScoringService _scoringService;
    private readonly IUserInsightsService _userInsights;
    private readonly ILogger<RiskTrendController> _logger;

    public RiskTrendController(
        IRiskAnalyzerService riskService,
        IRiskScoringService scoringService,
        IUserInsightsService userInsights,
        ILogger<RiskTrendController> logger)
    {
        _riskService = riskService;
        _scoringService = scoringService;
        _userInsights = userInsights;
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

        var scores = await _userInsights.GetUserDailyScoresAsync(email, start, end);
        return Ok(scores);
    }

    /// <summary>
    /// Get comprehensive user insights with daily scores, action counts, and period averages
    /// </summary>
    [HttpGet("user/{email}/comprehensive")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserComprehensiveInsights(
        string email,
        [FromQuery] string period = "monthly") // daily, weekly, monthly, quarterly
    {
        try
        {
            var result = await _userInsights.GetUserComprehensiveInsightsAsync(email, period);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comprehensive insights for {Email}", email);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get weekly trend analysis for a user
    /// </summary>
    [HttpGet("user/{email}/weekly-trend")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserWeeklyTrend(string email)
    {
        var result = await _userInsights.GetUserWeeklyTrendAsync(email);
        return Ok(result);
    }

    /// <summary>
    /// Get monthly trend analysis for a user
    /// </summary>
    [HttpGet("user/{email}/monthly-trend")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserMonthlyTrend(string email)
    {
        var result = await _userInsights.GetUserMonthlyTrendAsync(email);
        return Ok(result);
    }

    /// <summary>
    /// Get quarterly trend analysis for a user
    /// </summary>
    [HttpGet("user/{email}/quarterly-trend")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserQuarterlyTrend(string email)
    {
        var result = await _userInsights.GetUserQuarterlyTrendAsync(email);
        return Ok(result);
    }

    /// <summary>
    /// Get anomaly detection results for a user
    /// </summary>
    [HttpGet("user/{email}/anomalies")]
    public async Task<ActionResult<List<string>>> GetUserAnomalies(string email)
    {
        var anomalies = await _userInsights.DetectUserAnomaliesAsync(email);
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
            var count = await _scoringService.CalculateDailyScoresAsync(date);
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
            var count = await _scoringService.CalculateRiskScoresAsync();
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
        var result = await _userInsights.GetRiskyUsersReportAsync(period);
        return Ok(result);
    }

    /// <summary>
    /// Get top risky users based on user_daily_risk_scores table
    /// period: 24h, weekly, monthly, quarterly
    /// Uses simple average of DailyRiskScore (same as User Insights)
    /// Filters: minimum 3 days activity, score >= 35
    /// </summary>
    [HttpGet("top-users")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetTopRiskyUsers(
        [FromQuery] string period = "24h",
        [FromQuery] int limit = 10,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _userInsights.GetTopRiskyUsersFromDailyScoresAsync(period, limit, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching top risky users for period {Period}", period);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get daily risk summary aggregated from user_daily_risk_scores
    /// Returns total incidents, avg risk score, unique users per day
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetDailySummaryFromScores(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate)
    {
        try
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);
            var result = await _userInsights.GetDailySummaryFromDailyScoresAsync(start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily summary");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get high impact alerts - potential data exfiltration attempts
    /// Single-day events with unusually high max_matches that would be penalized by consistency factor
    /// </summary>
    [HttpGet("high-impact-alerts")]
    public async Task<ActionResult<object>> GetHighImpactAlerts(
        [FromQuery] int days = 7,
        [FromQuery] int minMaxMatches = 100,
        [FromQuery] int minDailyRiskScore = 0,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _userInsights.GetHighImpactAlertsAsync(days, minMaxMatches, minDailyRiskScore, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching high impact alerts");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get top most active rules within a period
    /// </summary>
    [HttpGet("top-rules")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetTopRules(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _riskService.GetTopRulesByDayAsync(days, limit, startDate, endDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching top rules");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
