using Microsoft.AspNetCore.Mvc;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/isolation-forest")]
public class IsolationForestController : ControllerBase
{
    private readonly IIsolationForestService _service;
    private readonly ILogger<IsolationForestController> _logger;

    public IsolationForestController(IIsolationForestService service, ILogger<IsolationForestController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get current run status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<IsolationForestStatusDto>> GetStatus()
    {
        var status = await _service.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Get latest scored user list + department risks
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<IsolationForestOverviewDto>> GetOverview()
    {
        try
        {
            var overview = await _service.GetOverviewAsync();
            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching IF overview");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger a scoring run
    /// </summary>    [HttpPost("trigger")]
    public async Task<ActionResult<IsolationForestStatusDto>> Trigger([FromQuery] int lookbackDays = 180)
    {
        if (lookbackDays < 7 || lookbackDays > 365)
            return BadRequest(new { detail = "lookbackDays must be between 7 and 365" });

        var status = await _service.TriggerRunAsync(lookbackDays);
        return Ok(status);
    }
}
