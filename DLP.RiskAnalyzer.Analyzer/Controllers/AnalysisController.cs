using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IRiskAnalyzerService _riskAnalyzerService;
    private readonly IRedisStreamProcessor _redisStreamProcessor;

    public AnalysisController(
        IRiskAnalyzerService riskAnalyzerService,
        IRedisStreamProcessor redisStreamProcessor)
    {
        _riskAnalyzerService = riskAnalyzerService;
        _redisStreamProcessor = redisStreamProcessor;
    }

    [HttpPost("daily")]
    public async Task<ActionResult<Dictionary<string, object>>> AnalyzeDaily()
    {
        try
        {
            // Process Redis stream and calculate risk scores
            var processedCount = await _riskAnalyzerService.ProcessRedisStreamAsync(_redisStreamProcessor);

            return Ok(new
            {
                message = "Daily analysis completed",
                processed_incidents = processedCount,
                status = "success"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost("process/redis-stream")]
    public async Task<ActionResult<Dictionary<string, object>>> ProcessRedisStream()
    {
        try
        {
            await _redisStreamProcessor.ProcessRedisStreamAsync();
            return Ok(new { message = "Redis stream processed successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}
