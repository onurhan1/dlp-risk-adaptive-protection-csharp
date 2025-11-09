using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly EmailService _emailService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(AnalyzerDbContext context, EmailService emailService, ILogger<SettingsController> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
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

    [HttpPost("send-test-email")]
    public async Task<ActionResult<Dictionary<string, object>>> SendTestEmail(
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            if (!request.ContainsKey("email") || string.IsNullOrWhiteSpace(request["email"]?.ToString()))
            {
                return BadRequest(new { detail = "Email address is required" });
            }

            var email = request["email"].ToString()!;

            if (!_emailService.IsConfigured())
            {
                return BadRequest(new 
                { 
                    detail = "Email service is not configured. Please configure SMTP settings in appsettings.json",
                    configured = false
                });
            }

            var success = await _emailService.SendTestEmailAsync(email);

            if (success)
            {
                _logger.LogInformation("Test email sent successfully to {Email}", email);
                return Ok(new 
                { 
                    success = true, 
                    message = $"Test email sent successfully to {email}",
                    configured = true
                });
            }
            else
            {
                return StatusCode(500, new 
                { 
                    detail = "Failed to send test email. Please check SMTP configuration and logs.",
                    configured = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email");
            return StatusCode(500, new { detail = $"Error sending test email: {ex.Message}" });
        }
    }
}

