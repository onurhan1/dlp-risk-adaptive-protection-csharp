using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<DatabaseController> _logger;

    public DatabaseController(AnalyzerDbContext context, ILogger<DatabaseController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetDatabaseStatus()
    {
        try
        {
            // Test database connection
            var canConnect = await _context.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                return StatusCode(500, new { 
                    connected = false, 
                    message = "Cannot connect to database" 
                });
            }

            // Check tables
            var systemSettingsCount = await _context.SystemSettings.CountAsync();
            var incidentsCount = await _context.Incidents.CountAsync();
            
            return Ok(new
            {
                connected = true,
                database = _context.Database.GetDbConnection().Database,
                tables = new
                {
                    system_settings = new
                    {
                        exists = true,
                        record_count = systemSettingsCount,
                        columns = new[] { "key", "value", "updated_at" }
                    },
                    incidents = new
                    {
                        exists = true,
                        record_count = incidentsCount,
                        columns = new[] { "id", "timestamp", "user_email", "department", "severity", "data_type", "policy", "channel", "risk_score", "repeat_count", "data_sensitivity" }
                    }
                },
                settings = await _context.SystemSettings
                    .Select(s => new { s.Key, s.Value })
                    .ToListAsync()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database status");
            return StatusCode(500, new { 
                connected = false, 
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    [HttpPost("init-settings")]
    public async Task<ActionResult> InitializeSettings()
    {
        try
        {
            var defaultSettings = new Dictionary<string, string>
            {
                { "risk_threshold_low", "10" },
                { "risk_threshold_medium", "30" },
                { "risk_threshold_high", "50" },
                { "email_notifications", "true" },
                { "daily_report_time", "06:00" },
                { "admin_email", "" }
            };

            foreach (var setting in defaultSettings)
            {
                var existing = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == setting.Key);
                
                if (existing == null)
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

            return Ok(new { 
                success = true, 
                message = "Default settings initialized",
                settings = defaultSettings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing settings");
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message 
            });
        }
    }
}

