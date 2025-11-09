using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DLP.RiskAnalyzer.Analyzer.Data;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RiskController : ControllerBase
{
    private readonly RiskAnalyzerService _riskAnalyzerService;
    private readonly AnalyzerDbContext _context;

    public RiskController(RiskAnalyzerService riskAnalyzerService, AnalyzerDbContext context)
    {
        _riskAnalyzerService = riskAnalyzerService;
        _context = context;
    }

    [HttpGet("trends")]
    public async Task<ActionResult<List<UserRiskTrend>>> GetUserRiskTrends(
        [FromQuery] int days = 30,
        [FromQuery] string? user = null)
    {
        try
        {
            var trends = await _riskAnalyzerService.GetUserRiskTrendsAsync(days, user);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("daily-summary")]
    public async Task<ActionResult<List<DailySummary>>> GetDailySummaries(
        [FromQuery] int days = 7)
    {
        try
        {
            var summaries = await _riskAnalyzerService.GetDailySummariesAsync(days);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("department-summary")]
    public async Task<ActionResult<List<DepartmentSummary>>> GetDepartmentSummaries(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var summaries = await _riskAnalyzerService.GetDepartmentSummariesAsync(start, end);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("heatmap")]
    public async Task<ActionResult<RiskHeatmapData>> GetRiskHeatmap(
        [FromQuery] string dimension = "department",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var heatmap = await _riskAnalyzerService.GetRiskHeatmapAsync(dimension, start, end);
            return Ok(heatmap);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("channel-activity")]
    public async Task<ActionResult<Dictionary<string, object>>> GetChannelActivity(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int days = 30)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var activity = await _riskAnalyzerService.GetChannelActivityAsync(start, end, days);
            return Ok(activity);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("iob-detections")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetIOBDetections(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? category = null)
    {
        try
        {
            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;
            
            var detections = await _riskAnalyzerService.GetIOBDetectionsAsync(start, end, category);
            return Ok(detections);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("user-list")]
    public async Task<ActionResult<Dictionary<string, object>>> GetUserList(
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 15)
    {
        try
        {
            // Get all incidents and group by user
            var incidents = await _context.Incidents.ToListAsync();
            
            // Group by user email and calculate risk scores
            var userGroups = incidents
                .GroupBy(i => i.UserEmail)
                .Select(g => new
                {
                    user_email = g.Key,
                    risk_score = g.Max(i => i.RiskScore ?? 0),
                    total_incidents = g.Count()
                })
                .OrderByDescending(u => u.risk_score)
                .ToList();

            // Apply pagination
            var total = userGroups.Count;
            var skip = (page - 1) * page_size;
            var pagedUsers = userGroups.Skip(skip).Take(page_size).ToList();

            return Ok(new
            {
                users = pagedUsers.Select(u => new
                {
                    user_email = u.user_email,
                    risk_score = u.risk_score,
                    total_incidents = u.total_incidents
                }),
                total = total,
                page = page,
                page_size = page_size
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("decay/simulation")]
    public Task<ActionResult<Dictionary<string, object>>> GetRiskDecaySimulation(
        [FromQuery] string userEmail,
        [FromQuery] int days = 30)
    {
        try
        {
            // Simulate risk score decay over time
            var currentScore = 80; // Placeholder - get from database
            var decayRate = 2; // 2 points per day
            
            var simulation = new List<Dictionary<string, object>>();
            for (int day = 0; day <= days; day++)
            {
                var decayedScore = Math.Max(0, currentScore - (day * decayRate));
                var riskLevel = decayedScore >= 91 ? "Critical" :
                               decayedScore >= 61 ? "High" :
                               decayedScore >= 41 ? "Medium" : "Low";
                
                simulation.Add(new Dictionary<string, object>
                {
                    { "day", day },
                    { "risk_score", decayedScore },
                    { "risk_level", riskLevel },
                    { "decay_percentage", (day * decayRate * 100.0 / currentScore) }
                });
            }

            return Task.FromResult<ActionResult<Dictionary<string, object>>>(Ok(new Dictionary<string, object>
            {
                { "user_email", userEmail },
                { "initial_score", currentScore },
                { "decay_rate", decayRate },
                { "simulation", simulation }
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ActionResult<Dictionary<string, object>>>(StatusCode(500, new { detail = ex.Message }));
        }
    }
}

