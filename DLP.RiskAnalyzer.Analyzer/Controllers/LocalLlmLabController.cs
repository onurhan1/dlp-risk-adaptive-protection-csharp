using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/local-llm-lab")]
public sealed class LocalLlmLabController : ControllerBase
{
    private readonly ILocalLlmLabService _service;
    private readonly ILogger<LocalLlmLabController> _logger;

    public LocalLlmLabController(ILocalLlmLabService service, ILogger<LocalLlmLabController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct) => Ok(await _service.GetSettingsAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] LocalLlmLabSettings settings, CancellationToken ct)
    {
        await _service.SaveSettingsAsync(settings, ct);
        return Ok(new { success = true });
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] LocalLlmLabSettings settings, CancellationToken ct)
    {
        var reply = await _service.TestConnectionAsync(settings, ct);
        return Ok(new { success = true, reply });
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot([FromQuery] int lookbackDays = 30, [FromQuery] int sampleSize = 40, [FromQuery] bool maskIdentifiers = true, CancellationToken ct = default) =>
        Ok(await _service.GetIncidentSnapshotAsync(new LocalLlmSnapshotRequest(lookbackDays, sampleSize, maskIdentifiers), ct));

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] LocalLlmChatRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ChatAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local LLM laboratory chat failed");
            return StatusCode(502, new { detail = "Yerel LLM yanıtı alınamadı." });
        }
    }
}
