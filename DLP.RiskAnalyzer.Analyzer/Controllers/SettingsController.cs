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
            // Get settings from database
            var settings = await _context.SystemSettings.ToListAsync();
            var settingsDict = settings.ToDictionary(s => s.Key, s => s.Value);

            // Return settings with defaults if not found
            return Ok(new
            {
                risk_threshold_low = int.TryParse(settingsDict.GetValueOrDefault("risk_threshold_low"), out var low) ? low : 10,
                risk_threshold_medium = int.TryParse(settingsDict.GetValueOrDefault("risk_threshold_medium"), out var medium) ? medium : 30,
                risk_threshold_high = int.TryParse(settingsDict.GetValueOrDefault("risk_threshold_high"), out var high) ? high : 50,
                email_notifications = bool.TryParse(settingsDict.GetValueOrDefault("email_notifications"), out var emailNotif) ? emailNotif : true,
                daily_report_time = settingsDict.GetValueOrDefault("daily_report_time") ?? "06:00",
                admin_email = settingsDict.GetValueOrDefault("admin_email") ?? ""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings");
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
            var settingsToSave = new Dictionary<string, string>
            {
                { "risk_threshold_low", request.GetValueOrDefault("risk_threshold_low")?.ToString() ?? "10") },
                { "risk_threshold_medium", request.GetValueOrDefault("risk_threshold_medium")?.ToString() ?? "30") },
                { "risk_threshold_high", request.GetValueOrDefault("risk_threshold_high")?.ToString() ?? "50") },
                { "email_notifications", request.GetValueOrDefault("email_notifications")?.ToString() ?? "true") },
                { "daily_report_time", request.GetValueOrDefault("daily_report_time")?.ToString() ?? "06:00") },
                { "admin_email", request.GetValueOrDefault("admin_email")?.ToString() ?? "") }
            };

            foreach (var setting in settingsToSave)
            {
                var existing = await _context.SystemSettings.FindAsync(setting.Key);
                if (existing != null)
                {
                    existing.Value = setting.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _context.SystemSettings.Update(existing);
                }
                else
                {
                    _context.SystemSettings.Add(new Data.SystemSetting
                    {
                        Key = setting.Key,
                        Value = setting.Value,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Settings saved successfully");

            return Ok(new { success = true, message = "Settings saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
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

