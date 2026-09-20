using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/risk-shadow")]
public sealed class RiskShadowController(IRiskShadowService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int days = 30, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await service.GetSnapshotAsync(days, take, ct));

    [HttpPost("reviews")]
    public async Task<IActionResult> Review([FromBody] RiskShadowReviewRequest request, CancellationToken ct)
    {
        var reviewer = User.Identity?.Name ?? "admin";
        try { return Ok(await service.RecordReviewAsync(request.UserEmail, request.Verdict, reviewer, request.Note, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { detail = ex.Message }); }
    }
}

public sealed record RiskShadowReviewRequest(string UserEmail, string Verdict, string? Note = null);
