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
            // Get settings from database - force refresh from database with AsNoTracking
            _context.ChangeTracker.Clear();
            var settings = await _context.SystemSettings.AsNoTracking().ToListAsync();
            var settingsDict = new Dictionary<string, string>();
            foreach (var s in settings)
            {
                settingsDict[s.Key] = s.Value;
                _logger.LogInformation("Loaded setting from DB: {Key} = {Value}", s.Key, s.Value);
            }

            _logger.LogInformation("Found {Count} settings in database", settings.Count);

            // Return settings with defaults if not found
            int low = 10, medium = 30, high = 50;
            bool emailNotif = true;
            string reportTime = "06:00", adminEmail = "";

            if (settingsDict.TryGetValue("risk_threshold_low", out var lowStr) && !string.IsNullOrEmpty(lowStr))
            {
                if (int.TryParse(lowStr, out var parsedLow))
                    low = parsedLow;
            }
            if (settingsDict.TryGetValue("risk_threshold_medium", out var mediumStr) && !string.IsNullOrEmpty(mediumStr))
            {
                if (int.TryParse(mediumStr, out var parsedMedium))
                    medium = parsedMedium;
            }
            if (settingsDict.TryGetValue("risk_threshold_high", out var highStr) && !string.IsNullOrEmpty(highStr))
            {
                if (int.TryParse(highStr, out var parsedHigh))
                    high = parsedHigh;
            }
            if (settingsDict.TryGetValue("email_notifications", out var emailNotifStr) && !string.IsNullOrEmpty(emailNotifStr))
            {
                bool.TryParse(emailNotifStr, out emailNotif);
            }
            if (settingsDict.TryGetValue("daily_report_time", out var reportTimeStr) && !string.IsNullOrEmpty(reportTimeStr))
            {
                reportTime = reportTimeStr;
            }
            if (settingsDict.TryGetValue("admin_email", out var adminEmailStr))
            {
                adminEmail = adminEmailStr ?? "";
            }

            _logger.LogInformation("Returning settings: Low={Low}, Medium={Medium}, High={High}, Email={Email}", low, medium, high, adminEmail);

            return Ok(new
            {
                risk_threshold_low = low,
                risk_threshold_medium = medium,
                risk_threshold_high = high,
                email_notifications = emailNotif,
                daily_report_time = reportTime,
                admin_email = adminEmail
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
            // Save settings to database - always save all values from request
            _logger.LogInformation("Saving settings. Request keys: {Keys}", string.Join(", ", request.Keys));
            
            var settingsToSave = new Dictionary<string, string>();
            
            // Always save all settings from request
            settingsToSave["risk_threshold_low"] = request.ContainsKey("risk_threshold_low") 
                ? request["risk_threshold_low"]?.ToString() ?? "10" 
                : "10";
            settingsToSave["risk_threshold_medium"] = request.ContainsKey("risk_threshold_medium") 
                ? request["risk_threshold_medium"]?.ToString() ?? "30" 
                : "30";
            settingsToSave["risk_threshold_high"] = request.ContainsKey("risk_threshold_high") 
                ? request["risk_threshold_high"]?.ToString() ?? "50" 
                : "50";
            settingsToSave["email_notifications"] = request.ContainsKey("email_notifications") 
                ? request["email_notifications"]?.ToString() ?? "true" 
                : "true";
            settingsToSave["daily_report_time"] = request.ContainsKey("daily_report_time") 
                ? request["daily_report_time"]?.ToString() ?? "06:00" 
                : "06:00";
            settingsToSave["admin_email"] = request.ContainsKey("admin_email") 
                ? request["admin_email"]?.ToString() ?? "" 
                : "";
            
            _logger.LogInformation("Settings to save: Low={Low}, Medium={Medium}, High={High}, Email={Email}", 
                settingsToSave["risk_threshold_low"], 
                settingsToSave["risk_threshold_medium"], 
                settingsToSave["risk_threshold_high"], 
                settingsToSave["admin_email"]);

            // Clear change tracker and save settings
            _context.ChangeTracker.Clear();
            
            foreach (var setting in settingsToSave)
            {
                // Check if setting exists (with tracking to allow update)
                var existing = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == setting.Key);
                
                if (existing != null)
                {
                    // Update existing setting
                    existing.Value = setting.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Updating setting: {Key} = {Value}", setting.Key, setting.Value);
                }
                else
                {
                    // Add new setting
                    var newSetting = new Data.SystemSetting
                    {
                        Key = setting.Key,
                        Value = setting.Value,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemSettings.Add(newSetting);
                    _logger.LogInformation("Adding new setting: {Key} = {Value}", setting.Key, setting.Value);
                }
            }

            try
            {
                var savedCount = await _context.SaveChangesAsync();
                _logger.LogInformation("Settings saved successfully. {Count} records affected", savedCount);
                
                if (savedCount == 0)
                {
                    _logger.LogWarning("No records were saved! This might indicate a problem.");
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Error during SaveChangesAsync: {Message}", saveEx.Message);
                throw;
            }

            // Force refresh from database to verify - use a new query
            _context.ChangeTracker.Clear();
            var savedSettings = await _context.SystemSettings.AsNoTracking().ToListAsync();
            _logger.LogInformation("Verification: {Count} settings in database after save", savedSettings.Count);
            
            // Build response with actual saved values
            var savedDict = savedSettings.ToDictionary(s => s.Key, s => s.Value);
            int savedLow = 10, savedMedium = 30, savedHigh = 50;
            bool savedEmailNotif = true;
            string savedReportTime = "06:00", savedAdminEmail = "";

            if (savedDict.TryGetValue("risk_threshold_low", out var savedLowStr) && !string.IsNullOrEmpty(savedLowStr))
                int.TryParse(savedLowStr, out savedLow);
            if (savedDict.TryGetValue("risk_threshold_medium", out var savedMediumStr) && !string.IsNullOrEmpty(savedMediumStr))
                int.TryParse(savedMediumStr, out savedMedium);
            if (savedDict.TryGetValue("risk_threshold_high", out var savedHighStr) && !string.IsNullOrEmpty(savedHighStr))
                int.TryParse(savedHighStr, out savedHigh);
            if (savedDict.TryGetValue("email_notifications", out var savedEmailNotifStr) && !string.IsNullOrEmpty(savedEmailNotifStr))
                bool.TryParse(savedEmailNotifStr, out savedEmailNotif);
            if (savedDict.TryGetValue("daily_report_time", out var savedReportTimeStr) && !string.IsNullOrEmpty(savedReportTimeStr))
                savedReportTime = savedReportTimeStr;
            if (savedDict.TryGetValue("admin_email", out var savedAdminEmailStr))
                savedAdminEmail = savedAdminEmailStr ?? "";

            foreach (var s in savedSettings)
            {
                _logger.LogInformation("  Verified: {Key} = {Value}", s.Key, s.Value);
            }

            return Ok(new 
            { 
                success = true, 
                message = "Settings saved successfully",
                settings = new
                {
                    risk_threshold_low = savedLow,
                    risk_threshold_medium = savedMedium,
                    risk_threshold_high = savedHigh,
                    email_notifications = savedEmailNotif,
                    daily_report_time = savedReportTime,
                    admin_email = savedAdminEmail
                }
            });
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

