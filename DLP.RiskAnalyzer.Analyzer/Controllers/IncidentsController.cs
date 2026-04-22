using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Filters;
using DLP.RiskAnalyzer.Analyzer.Helpers;
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
    private readonly IDatabaseService _dbService;
    private readonly DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer _riskAnalyzer;
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<IncidentsController> _logger;

    public IncidentsController(
        IDatabaseService dbService,
        DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer riskAnalyzer,
        AnalyzerDbContext context,
        ILogger<IncidentsController> logger)
    {
        _dbService    = dbService;
        _riskAnalyzer = riskAnalyzer;
        _context      = context;
        _logger       = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Seed
    // ─────────────────────────────────────────────────────────────────────────

    [DevelopmentOnly]
    [HttpPost("seed-sample-data")]
    public async Task<ActionResult> SeedSampleData()
    {
        try
        {
            // Clear existing incidents to allow re-seeding
            var existingCount = await _context.Incidents.CountAsync();
            if (existingCount > 0)
            {
                _context.Incidents.RemoveRange(_context.Incidents);
                await _context.SaveChangesAsync();
            }

            var random      = new Random();
            var users       = new[] { "john.doe@company.com", "jane.smith@company.com", "bob.wilson@company.com", "alice.brown@company.com", "charlie.davis@company.com" };
            var departments = new[] { "IT", "Finance", "HR", "Sales", "Marketing", "Operations" };
            var dataTypes   = new[] { "PII", "Financial", "Health", "Intellectual Property", "Credentials" };
            var policies    = new[] { "Data Loss Prevention", "Email Security", "File Transfer", "Cloud Storage" };
            var channels    = new[] { "Email", "USB", "Cloud", "Network", "Print" };
            var severities  = new[] { 1, 2, 3, 4, 5 };

            var incidents       = new List<Incident>();
            var baseDate        = DateTime.UtcNow.AddDays(-30);
            var maxId           = await _context.Incidents.MaxAsync(i => (int?)i.Id) ?? 0;
            var usedTimestamps  = new HashSet<DateTime>();

            for (int i = 0; i < 50; i++)
            {
                var timestamp = baseDate
                    .AddDays(random.Next(0, 30))
                    .AddHours(random.Next(0, 24))
                    .AddMinutes(random.Next(0, 60));

                while (usedTimestamps.Contains(timestamp))
                    timestamp = timestamp.AddMinutes(1);

                usedTimestamps.Add(timestamp);

                incidents.Add(new Incident
                {
                    Id              = maxId + i + 1,
                    UserEmail       = users[random.Next(users.Length)],
                    Department      = departments[random.Next(departments.Length)],
                    Severity        = severities[random.Next(severities.Length)],
                    DataType        = dataTypes[random.Next(dataTypes.Length)],
                    Timestamp       = timestamp,
                    Policy          = policies[random.Next(policies.Length)],
                    Channel         = channels[random.Next(channels.Length)],
                    RiskScore       = random.Next(20, 95),
                    RepeatCount     = random.Next(0, 5),
                    DataSensitivity = random.Next(0, 10)
                });
            }

            await _context.Incidents.AddRangeAsync(incidents);
            var savedCount = await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Successfully created {savedCount} sample incidents", count = savedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding sample data");
            return StatusCode(500, new { detail = "An error occurred while seeding sample data" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/incidents
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string?   user,
        [FromQuery] string?   department,
        [FromQuery] int       limit   = 100,
        [FromQuery] string    orderBy = "timestamp_desc")
    {
        try
        {
            // Cap limit to prevent OOM on large datasets
            var safeLimitValue = Math.Min(limit, 100000);

            var incidents = await _dbService.GetIncidentsAsync(
                startDate, endDate, user, department, safeLimitValue, orderBy);

            // M-01: enrichment + mapping delegated to the single factory method
            var enrichedIncidents = incidents.Select(incident => EnrichAndMap(incident)).ToList();

            return Ok(enrichedIncidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching incidents");
            return StatusCode(500, new { detail = "An error occurred while fetching incidents" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/incidents/{id}
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id}")]
    public async Task<ActionResult<IncidentResponse>> GetIncident(int id)
    {
        try
        {
            var incident = await _dbService.GetIncidentByIdAsync(id);
            if (incident == null)
                return NotFound();

            return Ok(EnrichAndMap(incident));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching incident {Id}", id);
            return StatusCode(500, new { detail = "An error occurred while fetching incidents" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/incidents/exception-stats
    // Returns aggregated incident counts per policy_name + rule_name
    // by parsing ViolationTriggers JSON directly in PostgreSQL
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("exception-stats")]
    public async Task<ActionResult<List<ExceptionIncidentStats>>> GetExceptionIncidentStats(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var stats = await _dbService.GetExceptionIncidentStatsAsync(startDate, endDate);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exception incident stats");
            return StatusCode(500, new { detail = "An error occurred while fetching exception incident stats" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes enrichment values and delegates mapping to <see cref="IncidentResponseMapper"/>.
    /// Previously this block was duplicated verbatim in both GetIncidents and GetIncident.
    /// </summary>
    private IncidentResponse EnrichAndMap(Incident incident)
    {
        var riskLevel         = _riskAnalyzer.GetRiskLevel(incident.RiskScore ?? 0);
        var recommendedAction = _riskAnalyzer.GetPolicyAction(riskLevel, incident.Channel ?? string.Empty);
        var iobs              = _riskAnalyzer.DetectIOB(incident);

        return IncidentResponseMapper.Map(incident, riskLevel, recommendedAction, iobs);
    }
}