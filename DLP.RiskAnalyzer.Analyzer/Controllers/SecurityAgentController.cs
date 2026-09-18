using System.Security.Claims;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/security-agent")]
public sealed class SecurityAgentController : ControllerBase
{
    private readonly ISecurityAgentService _service;
    private readonly ILogger<SecurityAgentController> _logger;

    public SecurityAgentController(ISecurityAgentService service, ILogger<SecurityAgentController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext([FromQuery] DateTime? startUtc, [FromQuery] DateTime? endUtc, CancellationToken ct) =>
        Ok(await _service.GetContextAsync(new SecurityAgentContextRequest(startUtc, endUtc), ct));

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] SecurityAgentChatRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.ChatAsync(request, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security agent chat failed for {User}", User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "unknown");
            return StatusCode(502, new { detail = "Güvenlik Agentından yanıt alınamadı." });
        }
    }

    [HttpPost("workflow-drafts")]
    public async Task<IActionResult> CreateWorkflowDraft([FromBody] SecurityAgentWorkflowDraftRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.CreateWorkflowDraftAsync(request, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security agent workflow draft failed");
            return StatusCode(502, new { detail = "Workflow taslağı oluşturulamadı." });
        }
    }
}
