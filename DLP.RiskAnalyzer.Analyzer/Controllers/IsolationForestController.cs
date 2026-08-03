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
    /// The incidents behind one reason on a user's card, so the analyst can verify the score
    /// instead of taking it on trust. Ids are recorded at scoring time, not re-derived here.
    /// </summary>
    [HttpGet("user/{email}/evidence")]
    public async Task<ActionResult<ReasonEvidenceDto>> GetReasonEvidence(
        string email,
        [FromQuery] string family,
        [FromQuery] string? dimension = null)
    {
        if (string.IsNullOrWhiteSpace(family))
            return BadRequest(new { detail = "family is required" });

        try
        {
            return Ok(await _service.GetReasonEvidenceAsync(email, family, dimension));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reason evidence for {Email}/{Family}", email, family);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger the fixed seven-day scoring run.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<IsolationForestStatusDto>> Trigger()
    {
        var status = await _service.TriggerRunAsync();
        return Ok(status);
    }
}
