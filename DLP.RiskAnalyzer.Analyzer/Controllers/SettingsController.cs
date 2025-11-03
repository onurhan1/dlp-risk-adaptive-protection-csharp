using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;

    public SettingsController(AnalyzerDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, object>>> GetSettings()
    {
        try
        {
            // Get settings from database (system_settings table)
            // Placeholder - implement based on your schema
            return Ok(new
            {
                risk_threshold_low = 10,
                risk_threshold_medium = 30,
                risk_threshold_high = 50,
                email_notifications = true,
                daily_report_time = "06:00",
                admin_email = ""
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Dictionary<string, object>>> SaveSettings(
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            // Save settings to database
            // Placeholder - implement based on your schema
            
            return Ok(new { success = true, message = "Settings saved successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

