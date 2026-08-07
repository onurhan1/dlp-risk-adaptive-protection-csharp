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
    private readonly IDlpConfigurationService _configurationService;
    private readonly InternalApiOptions _internalApiOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DlpConfigurationController> _logger;

    public DlpConfigurationController(
        IDlpConfigurationService configurationService,
        IOptions<InternalApiOptions> internalApiOptions,
        IConfiguration configuration,
        ILogger<DlpConfigurationController> logger)
    {
        _configurationService = configurationService;
        _internalApiOptions = internalApiOptions.Value;
        _configuration = configuration;
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
        catch (ArgumentException ex)
        {
            // Validation error — return the specific message so the user knows what to fix
            return BadRequest(new { success = false, detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Business rule error (e.g. password required on first save)
            return BadRequest(new { success = false, detail = ex.Message });
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
            // Always return 200 so the frontend can read result.success and result.message.
            // Returning 4xx causes axios to throw, making the camelCase message field inaccessible.
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test DLP API settings");
            return Ok(new DlpApiTestResult
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
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning("Unauthorized attempt to access runtime DLP config");
            return Unauthorized(new { detail = "Missing or invalid internal secret" });
        }

        try
        {
            var sensitive = await _configurationService.GetSensitiveConfigAsync(cancellationToken);
            return Ok(sensitive);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation("DLP API settings are not yet configured.");
            return NotFound(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sensitive DLP settings");
            return StatusCode(500, new { detail = "Failed to retrieve settings" });
        }
    }

    [HttpGet("/api/settings/collector/runtime")]
    public ActionResult GetCollectorRuntimeSettings()
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning("Unauthorized attempt to access collector runtime config");
            return Unauthorized(new { detail = "Missing or invalid internal secret" });
        }

        return Ok(new
        {
            redis = new
            {
                host = _configuration["Redis:Host"] ?? "localhost",
                port = _configuration.GetValue<int>("Redis:Port", 6379),
                password = _configuration["Redis:Password"] ?? string.Empty,
                stream_name = _configuration["Redis:StreamName"] ?? "dlp:incidents"
            }
        });
    }

    private bool IsInternalRequestAuthorized()
    {
        return Request.Headers.TryGetValue("X-Internal-Secret", out var providedSecret) &&
               !string.IsNullOrWhiteSpace(_internalApiOptions.SharedSecret) &&
               string.Equals(providedSecret, _internalApiOptions.SharedSecret, StringComparison.Ordinal);
    }
}
