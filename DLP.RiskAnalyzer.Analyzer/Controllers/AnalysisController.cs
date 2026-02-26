using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly RiskAnalyzerService _riskAnalyzerService;
    private readonly DatabaseService _dbService;

    public AnalysisController(
        RiskAnalyzerService riskAnalyzerService,
        DatabaseService dbService)
    {
        _riskAnalyzerService = riskAnalyzerService;
        _dbService = dbService;
    }

    [HttpPost("daily")]
    public async Task<ActionResult<Dictionary<string, object>>> AnalyzeDaily()
    {
        try
        {
            // Process Redis stream and calculate risk scores
            var processedCount = await _riskAnalyzerService.ProcessRedisStreamAsync(_dbService);

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
            await _dbService.ProcessRedisStreamAsync();
            return Ok(new { message = "Redis stream processed successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

