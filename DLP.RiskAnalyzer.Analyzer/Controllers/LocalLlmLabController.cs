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

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken ct) =>
        Ok(await _service.GetConversationsAsync(GetOwnerUsername(), ct));

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] LocalLlmConversationCreateRequest? request, CancellationToken ct) =>
        Ok(await _service.CreateConversationAsync(GetOwnerUsername(), request?.Title, ct));

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(Guid conversationId, CancellationToken ct)
    {
        var conversation = await _service.GetConversationAsync(conversationId, GetOwnerUsername(), ct);
        return conversation == null ? NotFound(new { detail = "Sohbet bulunamadı." }) : Ok(conversation);
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct)
    {
        var deleted = await _service.DeleteConversationAsync(conversationId, GetOwnerUsername(), ct);
        return deleted ? NoContent() : NotFound(new { detail = "Sohbet bulunamadı." });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] LocalLlmChatRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ChatAsync(request, GetOwnerUsername(), ct));
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

    private string GetOwnerUsername()
    {
        var username = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new UnauthorizedAccessException("Sohbet geçmişi için oturum açmanız gerekir.");
        return username;
    }
}
