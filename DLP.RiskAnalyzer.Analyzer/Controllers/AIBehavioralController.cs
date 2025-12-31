using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/ai-behavioral")]
public class AIBehavioralController : ControllerBase
{
    private readonly BehaviorEngineService _behaviorEngine;
    private readonly ILogger<AIBehavioralController> _logger;
    private readonly IMemoryCache _cache;
    
    // Cache settings
    private const string CacheKeyPrefix = "ai-behavioral-overview";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public AIBehavioralController(
        BehaviorEngineService behaviorEngine,
        ILogger<AIBehavioralController> logger,
        IMemoryCache cache)
    {
        _behaviorEngine = behaviorEngine;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Get AI behavioral analysis overview (cached for 5 minutes)
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AIBehavioralOverviewResponse>> GetOverview(
        [FromQuery] int lookbackDays = 7,
        [FromQuery] bool forceRefresh = false)
    {
        try
        {
            if (lookbackDays < 1 || lookbackDays > 30)
            {
                return BadRequest(new { detail = "lookbackDays must be between 1 and 30" });
            }

            var cacheKey = $"{CacheKeyPrefix}-{lookbackDays}";
            
            // Check if force refresh is requested
            if (forceRefresh)
            {
                _cache.Remove(cacheKey);
                _logger.LogInformation("Cache cleared for AI behavioral overview (forceRefresh=true)");
            }

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out AIBehavioralOverviewResponse? cachedOverview) && cachedOverview != null)
            {
                _logger.LogDebug("Returning cached AI behavioral overview for {LookbackDays} days", lookbackDays);
                return Ok(cachedOverview);
            }

            // Calculate and cache
            _logger.LogInformation("Calculating AI behavioral overview for {LookbackDays} days (cache miss)", lookbackDays);
            var overview = await _behaviorEngine.AnalyzeOverviewAsync(lookbackDays);
            
            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration)
                .SetSlidingExpiration(TimeSpan.FromMinutes(2));
            
            _cache.Set(cacheKey, overview, cacheOptions);
            _logger.LogInformation("AI behavioral overview cached for {Duration} minutes", CacheDuration.TotalMinutes);

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
            _logger.LogError(ex, "Error getting entity analysis {EntityType}:{EntityId}. Error: {Error}", entityType, entityId, ex.Message);
            // Return more detailed error message for debugging
            var errorMessage = ex.InnerException != null 
                ? $"Failed to get entity analysis: {ex.InnerException.Message}" 
                : $"Failed to get entity analysis: {ex.Message}";
            return StatusCode(500, new { detail = errorMessage });
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

