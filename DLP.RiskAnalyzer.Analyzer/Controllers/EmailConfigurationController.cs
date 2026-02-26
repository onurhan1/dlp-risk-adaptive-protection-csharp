using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/settings/email")]
public class EmailConfigurationController : ControllerBase
{
    private readonly EmailConfigurationService _configurationService;
    private readonly ILogger<EmailConfigurationController> _logger;

    public EmailConfigurationController(EmailConfigurationService configurationService, ILogger<EmailConfigurationController> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<EmailSettingsResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var response = await _configurationService.GetAsync(false, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<EmailSettingsResponse>> SaveAsync([FromBody] EmailSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var saved = await _configurationService.SaveAsync(request, cancellationToken);
            return Ok(new { success = true, settings = saved });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while saving SMTP settings");
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save SMTP settings");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost("test")]
    public async Task<ActionResult<EmailConfigTestResult>> TestAsync([FromBody] EmailSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _configurationService.TestAsync(request, cancellationToken);
            if (result.Success)
            {
                return Ok(result);
            }

            return StatusCode(400, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test SMTP settings");
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

