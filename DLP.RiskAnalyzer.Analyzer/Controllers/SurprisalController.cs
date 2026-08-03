using System.Text;
using DLP.RiskAnalyzer.Analyzer.Services.Surprisal;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Development surface for the behavioural surprisal model. The model is unsupervised, so there is
/// no accuracy metric to optimise — tuning happens by reading what it actually says about real
/// traffic. This endpoint produces that document.
/// </summary>
[ApiController]
[Route("api/surprisal")]
public class SurprisalController : ControllerBase
{
    private readonly ISurprisalRiskService _service;
    private readonly ILogger<SurprisalController> _logger;

    public SurprisalController(ISurprisalRiskService service, ILogger<SurprisalController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Fits the model on the trailing baseline, scores the recent window, and returns a markdown
    /// tuning report. Add <c>?download=true</c> to get it as a file.
    /// </summary>
    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics([FromQuery] bool download = false, CancellationToken ct = default)
    {
        try
        {
            var markdown = await _service.BuildDiagnosticReportAsync(ct);

            if (download)
                return File(Encoding.UTF8.GetBytes(markdown), "text/markdown",
                    $"surprisal-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmm}.md");

            return Content(markdown, "text/markdown; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Surprisal diagnostics failed");
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}
