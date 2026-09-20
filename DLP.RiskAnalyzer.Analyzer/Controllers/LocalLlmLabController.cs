using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Authorize]
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
        try
        {
            var health = await _service.TestConnectionAsync(settings, ct);
            return Ok(health);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot([FromQuery] int lookbackDays = 30, [FromQuery] int sampleSize = 40, [FromQuery] bool maskIdentifiers = true, [FromQuery] DateTime? startUtc = null, [FromQuery] DateTime? endUtc = null, [FromQuery] bool comprehensive = false, CancellationToken ct = default) =>
        Ok(await _service.GetIncidentSnapshotAsync(new LocalLlmSnapshotRequest(lookbackDays, sampleSize, maskIdentifiers, startUtc, endUtc, comprehensive), ct));

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

    [HttpGet("conversations/{conversationId:guid}/mail-proposals")]
    public async Task<IActionResult> GetMailProposals(Guid conversationId, CancellationToken ct) =>
        Ok(await _service.GetMailProposalsAsync(conversationId, GetOwnerUsername(), ct));

    [HttpPut("mail-proposals/{proposalId:guid}")]
    public async Task<IActionResult> UpdateMailProposal(Guid proposalId, [FromBody] LocalLlmMailProposalUpdateRequest request, CancellationToken ct)
    {
        var proposal = await _service.UpdateMailProposalAsync(proposalId, GetOwnerUsername(), request, ct);
        return proposal == null ? NotFound(new { detail = "Taslak bulunamadı veya artık düzenlenemez." }) : Ok(proposal);
    }

    [HttpPost("mail-proposals/{proposalId:guid}/approve")]
    public async Task<IActionResult> ApproveMailProposal(Guid proposalId, CancellationToken ct)
    {
        var proposal = await _service.ApproveMailProposalAsync(proposalId, GetOwnerUsername(), ct);
        return proposal == null ? BadRequest(new { detail = "Taslak gönderime uygun değil." }) : Ok(proposal);
    }

    [HttpPost("mail-proposals/{proposalId:guid}/reject")]
    public async Task<IActionResult> RejectMailProposal(Guid proposalId, CancellationToken ct)
    {
        var proposal = await _service.RejectMailProposalAsync(proposalId, GetOwnerUsername(), ct);
        return proposal == null ? BadRequest(new { detail = "Taslak reddedilemedi." }) : Ok(proposal);
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
        catch (LocalLlmConnectionException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                code = ex.Code,
                detail = ex.Detail,
                retryable = ex.Retryable,
                suggested_action = ex.SuggestedAction
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local LLM laboratory chat failed");
            return StatusCode(502, new { detail = "Yerel LLM yanıtı alınamadı." });
        }
    }

    private string GetOwnerUsername()
    {
        var username = User.Identity?.Name
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst("unique_name")?.Value
            ?? User.FindFirst("sub")?.Value;

        username = username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new UnauthorizedAccessException("Sohbet geçmişi için oturum açmanız gerekir.");
        return username;
    }
}
