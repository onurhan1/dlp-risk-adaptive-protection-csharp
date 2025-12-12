using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Options;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/settings/dlp")]
public class DlpConfigurationController : ControllerBase
{
    private readonly DlpConfigurationService _configurationService;
    private readonly InternalApiOptions _internalApiOptions;
    private readonly ILogger<DlpConfigurationController> _logger;

    public DlpConfigurationController(
        DlpConfigurationService configurationService,
        IOptions<InternalApiOptions> internalApiOptions,
        ILogger<DlpConfigurationController> logger)
    {
        _configurationService = configurationService;
        _internalApiOptions = internalApiOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DlpApiSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.GetAsync(false, cancellationToken);
        return Ok(settings);
    }

    [HttpPost]
    public async Task<ActionResult> SaveSettings([FromBody] DlpApiSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var saved = await _configurationService.SaveAsync(request, cancellationToken);
            return Ok(new { success = true, settings = saved });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save DLP API settings");
            return BadRequest(new { success = false, detail = "Failed to save DLP API settings. Please check your input and try again." });
        }
    }

    [HttpPost("test")]
    public async Task<ActionResult<DlpApiTestResult>> TestConnection([FromBody] DlpApiSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _configurationService.TestConnectionAsync(request, cancellationToken);
            if (result.Success)
            {
                return Ok(result);
            }

            return StatusCode(400, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test DLP API settings");
            return StatusCode(500, new DlpApiTestResult
            {
                Success = false,
                Message = "Connection test failed. Please verify your settings and try again.",
                TestedAt = DateTime.UtcNow
            });
        }
    }

    [HttpGet("runtime")]
    public async Task<ActionResult<DlpApiSensitiveSettingsResponse>> GetSensitiveSettings(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Secret", out var providedSecret) ||
            string.IsNullOrWhiteSpace(_internalApiOptions.SharedSecret) ||
            !string.Equals(providedSecret, _internalApiOptions.SharedSecret, StringComparison.Ordinal))
        {
            _logger.LogWarning("Unauthorized attempt to access runtime DLP config");
            return Unauthorized(new { detail = "Missing or invalid internal secret" });
        }

        try
        {
            var sensitive = await _configurationService.GetSensitiveConfigAsync(cancellationToken);
            return Ok(sensitive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sensitive DLP settings");
            return StatusCode(500, new { detail = "Failed to retrieve settings" });
        }
    }
}

