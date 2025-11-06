using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly DatabaseService _dbService;
    private readonly DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer _riskAnalyzer;

    public IncidentsController(
        DatabaseService dbService, 
        DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer riskAnalyzer)
    {
        _dbService = dbService;
        _riskAnalyzer = riskAnalyzer;
    }

    [HttpGet]
    public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? user,
        [FromQuery] string? department,
        [FromQuery] int limit = 100,
        [FromQuery] string orderBy = "timestamp_desc")
    {
        try
        {
            var incidents = await _dbService.GetIncidentsAsync(
                startDate, endDate, user, department, limit, orderBy);

            // Enrich with risk level and IOBs
            var enrichedIncidents = incidents.Select(incident =>
            {
                var riskLevel = _riskAnalyzer.GetRiskLevel(incident.RiskScore ?? 0);
                var policyAction = _riskAnalyzer.GetPolicyAction(riskLevel, incident.Channel ?? "");
                var iobs = _riskAnalyzer.DetectIOB(incident);

                return new IncidentResponse
                {
                    Id = incident.Id,
                    UserEmail = incident.UserEmail,
                    Department = incident.Department,
                    Severity = incident.Severity,
                    DataType = incident.DataType,
                    Timestamp = incident.Timestamp,
                    Policy = incident.Policy,
                    Channel = incident.Channel,
                    RiskScore = incident.RiskScore,
                    RepeatCount = incident.RepeatCount,
                    DataSensitivity = incident.DataSensitivity,
                    RiskLevel = riskLevel,
                    RecommendedAction = policyAction,
                    IOBs = iobs
                };
            }).ToList();

            return Ok(enrichedIncidents);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IncidentResponse>> GetIncident(int id)
    {
        try
        {
            var incident = await _dbService.GetIncidentByIdAsync(id);
            if (incident == null)
                return NotFound();

            var riskLevel = _riskAnalyzer.GetRiskLevel(incident.RiskScore ?? 0);
            var policyAction = _riskAnalyzer.GetPolicyAction(riskLevel, incident.Channel ?? "");
            var iobs = _riskAnalyzer.DetectIOB(incident);

            var response = new IncidentResponse
            {
                Id = incident.Id,
                UserEmail = incident.UserEmail,
                Department = incident.Department,
                Severity = incident.Severity,
                DataType = incident.DataType,
                Timestamp = incident.Timestamp,
                Policy = incident.Policy,
                Channel = incident.Channel,
                RiskScore = incident.RiskScore,
                RepeatCount = incident.RepeatCount,
                DataSensitivity = incident.DataSensitivity,
                RiskLevel = riskLevel,
                RecommendedAction = policyAction,
                IOBs = iobs
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

