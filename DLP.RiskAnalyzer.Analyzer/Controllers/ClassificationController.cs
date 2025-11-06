using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api")]
public class ClassificationController : ControllerBase
{
    private readonly ClassificationService _classificationService;

    public ClassificationController(ClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    [HttpGet("incidents/{incidentId}/classification")]
    public async Task<ActionResult<Dictionary<string, object>>> GetIncidentClassification(int incidentId)
    {
        try
        {
            var classification = await _classificationService.GetIncidentClassificationAsync(incidentId);
            return Ok(classification);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("incidents/{incidentId}/files")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetIncidentFiles(int incidentId)
    {
        try
        {
            var files = await _classificationService.GetIncidentFilesAsync(incidentId);
            return Ok(files);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("users/{userEmail}/classification")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserClassificationSummary(string userEmail)
    {
        try
        {
            var summary = await _classificationService.GetUserClassificationSummaryAsync(userEmail);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

