using Microsoft.AspNetCore.Mvc;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/ai-behavioral")]
public class AIBehavioralController : ControllerBase
{
    private readonly BehaviorEngineService _behaviorEngine;
    private readonly ILogger<AIBehavioralController> _logger;

    public AIBehavioralController(
        BehaviorEngineService behaviorEngine,
        ILogger<AIBehavioralController> logger)
    {
        _behaviorEngine = behaviorEngine;
        _logger = logger;
    }

    /// <summary>
    /// Get AI behavioral analysis overview
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AIBehavioralOverviewResponse>> GetOverview([FromQuery] int lookbackDays = 7)
    {
        try
        {
            if (lookbackDays < 1 || lookbackDays > 30)
            {
                return BadRequest(new { detail = "lookbackDays must be between 1 and 30" });
            }

            var overview = await _behaviorEngine.AnalyzeOverviewAsync(lookbackDays);
            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI behavioral overview");
            return StatusCode(500, new { detail = "Failed to get AI behavioral overview" });
        }
    }

    /// <summary>
    /// Analyze specific entity (user/channel/department)
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AIBehavioralAnalysisResponse>> AnalyzeEntity([FromBody] AIBehavioralAnalysisRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EntityType))
            {
                return BadRequest(new { detail = "EntityType is required" });
            }

            if (string.IsNullOrEmpty(request.EntityId))
            {
                return BadRequest(new { detail = "EntityId is required" });
            }

            if (request.LookbackDays < 1 || request.LookbackDays > 30)
            {
                return BadRequest(new { detail = "lookbackDays must be between 1 and 30" });
            }

            var validEntityTypes = new[] { "user", "channel", "department" };
            if (!validEntityTypes.Contains(request.EntityType.ToLower()))
            {
                return BadRequest(new { detail = $"EntityType must be one of: {string.Join(", ", validEntityTypes)}" });
            }

            var analysis = await _behaviorEngine.AnalyzeEntityAsync(
                request.EntityType.ToLower(),
                request.EntityId,
                request.LookbackDays);

            // Save to database
            await _behaviorEngine.SaveAnalysisAsync(analysis);

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing entity {EntityType}:{EntityId}", request.EntityType, request.EntityId);
            return StatusCode(500, new { detail = "Failed to analyze entity" });
        }
    }

    /// <summary>
    /// Get analysis for specific entity
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<ActionResult<AIBehavioralAnalysisResponse>> GetEntityAnalysis(
        string entityType,
        string entityId,
        [FromQuery] int lookbackDays = 7)
    {
        try
        {
            if (lookbackDays < 1 || lookbackDays > 30)
            {
                return BadRequest(new { detail = "lookbackDays must be between 1 and 30" });
            }

            var validEntityTypes = new[] { "user", "channel", "department" };
            if (!validEntityTypes.Contains(entityType.ToLower()))
            {
                return BadRequest(new { detail = $"EntityType must be one of: {string.Join(", ", validEntityTypes)}" });
            }

            var analysis = await _behaviorEngine.AnalyzeEntityAsync(
                entityType.ToLower(),
                entityId,
                lookbackDays);

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity analysis {EntityType}:{EntityId}", entityType, entityId);
            return StatusCode(500, new { detail = "Failed to get entity analysis" });
        }
    }

    /// <summary>
    /// Get top anomalies
    /// </summary>
    [HttpGet("anomalies")]
    public async Task<ActionResult<List<AIBehavioralAnalysisResponse>>> GetTopAnomalies(
        [FromQuery] string? entityType = null,
        [FromQuery] string? anomalyLevel = null,
        [FromQuery] int limit = 20,
        [FromQuery] int lookbackDays = 7)
    {
        try
        {
            var overview = await _behaviorEngine.AnalyzeOverviewAsync(lookbackDays);
            
            var anomalies = overview.TopAnomalies.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
            {
                anomalies = anomalies.Where(a => a.EntityType.ToLower() == entityType.ToLower());
            }

            if (!string.IsNullOrEmpty(anomalyLevel))
            {
                anomalies = anomalies.Where(a => a.AnomalyLevel.ToLower() == anomalyLevel.ToLower());
            }

            var result = anomalies
                .OrderByDescending(a => a.RiskScore)
                .Take(limit)
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top anomalies");
            return StatusCode(500, new { detail = "Failed to get top anomalies" });
        }
    }
}

