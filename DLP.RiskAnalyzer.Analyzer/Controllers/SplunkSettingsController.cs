using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/settings/splunk")]
public class SplunkSettingsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly SplunkService? _splunkService;
    private readonly ILogger<SplunkSettingsController> _logger;

    private const string EnabledKey = "splunk_enabled";
    private const string HecUrlKey = "splunk_hec_url";
    private const string HecTokenKey = "splunk_hec_token_protected";
    private const string IndexKey = "splunk_index";
    private const string SourceKey = "splunk_source";
    private const string SourcetypeKey = "splunk_sourcetype";

    public SplunkSettingsController(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SplunkSettingsController> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("Splunk.SettingsProtector");
        _logger = logger;
        
        try
        {
            _splunkService = serviceProvider.GetService<SplunkService>();
        }
        catch
        {
            _splunkService = null;
        }
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetSplunkSettings()
    {
        try
        {
            _context.ChangeTracker.Clear();
            
            var settings = await _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.Key.StartsWith("splunk_"))
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            var hasToken = settings.ContainsKey(HecTokenKey) && !string.IsNullOrEmpty(settings[HecTokenKey]);

            return Ok(new
            {
                enabled = bool.TryParse(settings.GetValueOrDefault(EnabledKey, "false"), out var enabled) ? enabled : false,
                hec_url = settings.GetValueOrDefault(HecUrlKey, ""),
                hec_token_set = hasToken,
                index = settings.GetValueOrDefault(IndexKey, "dlp_risk_analyzer"),
                source = settings.GetValueOrDefault(SourceKey, "dlp-risk-analyzer"),
                sourcetype = settings.GetValueOrDefault(SourcetypeKey, "dlp:audit")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Splunk settings");
            return StatusCode(500, new { detail = "Failed to get Splunk settings" });
        }
    }

    [HttpPost]
    public async Task<ActionResult> SaveSplunkSettings([FromBody] SplunkSettingsRequest request)
    {
        try
        {
            await SaveSettingAsync(EnabledKey, request.Enabled?.ToString() ?? "false", encrypt: false);
            await SaveSettingAsync(HecUrlKey, request.HecUrl?.Trim() ?? "", encrypt: false);
            await SaveSettingAsync(IndexKey, request.Index?.Trim() ?? "dlp_risk_analyzer", encrypt: false);
            await SaveSettingAsync(SourceKey, request.Source?.Trim() ?? "dlp-risk-analyzer", encrypt: false);
            await SaveSettingAsync(SourcetypeKey, request.Sourcetype?.Trim() ?? "dlp:audit", encrypt: false);

            if (!string.IsNullOrWhiteSpace(request.HecToken))
            {
                await SaveSettingAsync(HecTokenKey, request.HecToken, encrypt: true);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Splunk settings saved successfully");

            return Ok(new { success = true, message = "Splunk settings saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Splunk settings");
            return StatusCode(500, new { detail = "Failed to save Splunk settings", message = ex.Message });
        }
    }

    [HttpPost("test")]
    public async Task<ActionResult> TestConnection([FromBody] SplunkTestRequest request)
    {
        try
        {
            string? hecUrl = null;
            string? hecToken = null;

            // Get from request or stored settings
            if (!string.IsNullOrWhiteSpace(request.HecUrl))
            {
                hecUrl = request.HecUrl;
            }
            else
            {
                var urlSetting = await _context.SystemSettings
                    .Where(s => s.Key == HecUrlKey)
                    .FirstOrDefaultAsync();
                hecUrl = urlSetting?.Value ?? "";
            }

            if (!string.IsNullOrWhiteSpace(request.HecToken))
            {
                hecToken = request.HecToken;
            }
            else
            {
                var tokenSetting = await _context.SystemSettings
                    .Where(s => s.Key == HecTokenKey)
                    .FirstOrDefaultAsync();

                if (tokenSetting != null && !string.IsNullOrEmpty(tokenSetting.Value))
                {
                    try
                    {
                        hecToken = _protector.Unprotect(tokenSetting.Value);
                    }
                    catch
                    {
                        return BadRequest(new { success = false, message = "Failed to decrypt stored HEC token" });
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(hecUrl))
            {
                return BadRequest(new { success = false, message = "HEC URL is required" });
            }

            if (string.IsNullOrWhiteSpace(hecToken))
            {
                return BadRequest(new { success = false, message = "HEC Token is required" });
            }

            // Test connection by sending a test event
            if (_splunkService != null)
            {
                var testEvent = new AuditLogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "TestConnection",
                    UserName = "System",
                    Action = "Test Splunk Connection",
                    Success = true
                };

                await _splunkService.SendAuditLogAsync(testEvent);

                return Ok(new { success = true, message = "Splunk connection test successful" });
            }
            else
            {
                // Manual test using HttpClient
                using var httpClient = new HttpClient();
                var testEvent = new
                {
                    time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    host = Environment.MachineName,
                    source = request.Source ?? "dlp-risk-analyzer",
                    sourcetype = request.Sourcetype ?? "dlp:audit",
                    index = request.Index ?? "dlp_risk_analyzer",
                    @event = new
                    {
                        message = "Test connection from DLP Risk Analyzer",
                        timestamp = DateTime.UtcNow
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(testEvent);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var httpRequest = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, hecUrl)
                {
                    Content = content
                };

                httpRequest.Headers.Add("Authorization", $"Splunk {hecToken}");

                var response = await httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new { success = true, message = "Splunk connection test successful" });
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return BadRequest(new { success = false, message = $"Connection test failed: {response.StatusCode}", details = errorBody });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Splunk connection");
            return StatusCode(500, new { detail = "Failed to test Splunk connection", message = ex.Message });
        }
    }

    private async Task SaveSettingAsync(string key, string? value, bool encrypt)
    {
        if (value == null || string.IsNullOrWhiteSpace(value))
        {
            // Don't save empty values for optional settings
            if (key == HecTokenKey)
            {
                return; // Token is optional (can be kept existing)
            }
            value = "";
        }

        var finalValue = encrypt ? _protector.Protect(value) : value;

        _context.ChangeTracker.Clear();

        var existing = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (existing == null)
        {
            var newSetting = new SystemSetting
            {
                Key = key,
                Value = finalValue,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(newSetting);
        }
        else
        {
            existing.Value = finalValue;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(existing);
        }
    }
}

public class SplunkSettingsRequest
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }
    
    [JsonPropertyName("hec_url")]
    public string? HecUrl { get; set; }
    
    [JsonPropertyName("hec_token")]
    public string? HecToken { get; set; }
    
    [JsonPropertyName("index")]
    public string? Index { get; set; }
    
    [JsonPropertyName("source")]
    public string? Source { get; set; }
    
    [JsonPropertyName("sourcetype")]
    public string? Sourcetype { get; set; }
}

public class SplunkTestRequest
{
    public string? HecUrl { get; set; }
    public string? HecToken { get; set; }
    public string? Index { get; set; }
    public string? Source { get; set; }
    public string? Sourcetype { get; set; }
}

