using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/incidents")]
public class RemediationController : ControllerBase
{
    private readonly RemediationService _remediationService;

    public RemediationController(RemediationService remediationService)
    {
        _remediationService = remediationService;
    }

    [HttpPost("{incidentId}/remediate")]
    public async Task<ActionResult<Dictionary<string, object>>> RemediateIncident(
        int incidentId,
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            var action = request.GetValueOrDefault("action", "investigating")?.ToString() ?? "investigating";
            var reason = request.GetValueOrDefault("reason")?.ToString();
            var notes = request.GetValueOrDefault("notes")?.ToString();

            var result = await _remediationService.RemediateIncidentAsync(
                incidentId.ToString(), action, reason, notes);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPut("{incidentId}")]
    public async Task<ActionResult<Dictionary<string, object>>> UpdateIncident(
        int incidentId,
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            var status = request.GetValueOrDefault("status")?.ToString();
            var severity = request.ContainsKey("severity") ? Convert.ToInt32(request["severity"]) : (int?)null;
            var assignedTo = request.GetValueOrDefault("assigned_to")?.ToString();
            var notes = request.GetValueOrDefault("notes")?.ToString();
            var reason = request.GetValueOrDefault("reason")?.ToString();

            var result = await _remediationService.UpdateIncidentAsync(
                incidentId.ToString(), status, severity, assignedTo, notes, reason);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

