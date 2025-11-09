using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly DatabaseService _dbService;
    private readonly DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer _riskAnalyzer;
    private readonly AnalyzerDbContext _context;

    public IncidentsController(
        DatabaseService dbService, 
        DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer riskAnalyzer,
        AnalyzerDbContext context)
    {
        _dbService = dbService;
        _riskAnalyzer = riskAnalyzer;
        _context = context;
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

    [HttpPost]
    [Route("seed-sample-data")]
    public async Task<ActionResult> SeedSampleData()
    {
        try
        {
            var random = new Random();
            var users = new[] { "john.doe@company.com", "jane.smith@company.com", "bob.wilson@company.com", "alice.brown@company.com", "charlie.davis@company.com" };
            var departments = new[] { "IT", "Finance", "HR", "Sales", "Marketing", "Operations" };
            var dataTypes = new[] { "PII", "Financial", "Health", "Intellectual Property", "Credentials" };
            var policies = new[] { "Data Loss Prevention", "Email Security", "File Transfer", "Cloud Storage" };
            var channels = new[] { "Email", "USB", "Cloud", "Network", "Print" };
            var severities = new[] { 1, 2, 3, 4, 5 };

            var incidents = new List<Incident>();
            var baseDate = DateTime.UtcNow.AddDays(-30);

            for (int i = 0; i < 50; i++)
            {
                var timestamp = baseDate.AddDays(random.Next(0, 30)).AddHours(random.Next(0, 24)).AddMinutes(random.Next(0, 60));
                var riskScore = random.Next(20, 95);
                
                incidents.Add(new Incident
                {
                    UserEmail = users[random.Next(users.Length)],
                    Department = departments[random.Next(departments.Length)],
                    Severity = severities[random.Next(severities.Length)],
                    DataType = dataTypes[random.Next(dataTypes.Length)],
                    Timestamp = timestamp,
                    Policy = policies[random.Next(policies.Length)],
                    Channel = channels[random.Next(channels.Length)],
                    RiskScore = riskScore,
                    RepeatCount = random.Next(0, 5),
                    DataSensitivity = random.Next(0, 10)
                });
            }

            await _context.Incidents.AddRangeAsync(incidents);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Successfully created {incidents.Count} sample incidents" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

