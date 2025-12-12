using System.Text;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class SplunkService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SplunkService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SplunkService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SplunkService> logger,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;

    }

    private async Task<(bool enabled, string? hecUrl, string? hecToken, string index, string source, string sourcetype)> GetSettingsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DLP.RiskAnalyzer.Analyzer.Data.AnalyzerDbContext>();
            var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
            var protector = dataProtectionProvider.CreateProtector("Splunk.SettingsProtector");

            var settings = await context.SystemSettings
                .AsNoTracking()
                .Where(s => s.Key.StartsWith("splunk_"))
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            var enabled = bool.TryParse(settings.GetValueOrDefault("splunk_enabled", "false"), out var en) ? en : false;
            var hecUrl = settings.GetValueOrDefault("splunk_hec_url", "");
            var index = settings.GetValueOrDefault("splunk_index", "dlp_risk_analyzer");
            var source = settings.GetValueOrDefault("splunk_source", "dlp-risk-analyzer");
            var sourcetype = settings.GetValueOrDefault("splunk_sourcetype", "dlp:audit");

            string? hecToken = null;
            if (settings.TryGetValue("splunk_hec_token_protected", out var tokenValue) && !string.IsNullOrEmpty(tokenValue))
            {
                try
                {
                    hecToken = protector.Unprotect(tokenValue);
                }
                catch
                {
                    _logger.LogWarning("Failed to decrypt Splunk HEC token");
                }
            }

            return (enabled, hecUrl, hecToken, index, source, sourcetype);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Splunk settings from database");
            // Fallback to configuration
            var enabled = _configuration.GetValue<bool>("Splunk:Enabled", false);
            var hecUrl = _configuration["Splunk:HecUrl"];
            var hecToken = _configuration["Splunk:HecToken"];
            var index = _configuration["Splunk:Index"] ?? "dlp_risk_analyzer";
            var source = _configuration["Splunk:Source"] ?? "dlp-risk-analyzer";
            var sourcetype = _configuration["Splunk:Sourcetype"] ?? "dlp:audit";
            return (enabled, hecUrl, hecToken, index, source, sourcetype);
        }
    }

    public async Task SendAuditLogAsync(AuditLogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var (enabled, hecUrl, hecToken, index, source, sourcetype) = await GetSettingsAsync();

        if (!enabled || string.IsNullOrWhiteSpace(hecUrl) || string.IsNullOrWhiteSpace(hecToken))
        {
            return;
        }

        try
        {
            var splunkEvent = new
            {
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                host = Environment.MachineName,
                source = source,
                sourcetype = sourcetype,
                index = index,
                @event = logEvent
            };

            var json = JsonSerializer.Serialize(splunkEvent);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, hecUrl)
            {
                Content = content
            };

            request.Headers.Add("Authorization", $"Splunk {hecToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to send audit log to Splunk. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audit log to Splunk");
        }
    }

    public async Task SendApplicationLogAsync(string level, string message, string? category = null, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        var (enabled, hecUrl, hecToken, index, source, sourcetype) = await GetSettingsAsync();

        if (!enabled || string.IsNullOrWhiteSpace(hecUrl) || string.IsNullOrWhiteSpace(hecToken))
        {
            return;
        }

        try
        {
            var logEvent = new
            {
                level = level,
                message = message,
                category = category ?? "Application",
                exception = exception?.ToString(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                host = Environment.MachineName,
                application = "DLP-RiskAnalyzer"
            };

            var splunkEvent = new
            {
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                host = Environment.MachineName,
                source = source,
                sourcetype = "dlp:application",
                index = index,
                @event = logEvent
            };

            var json = JsonSerializer.Serialize(splunkEvent);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, hecUrl)
            {
                Content = content
            };

            request.Headers.Add("Authorization", $"Splunk {hecToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to send application log to Splunk. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending application log to Splunk");
        }
    }
}

public class AuditLogEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Resource { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }
    public long? DurationMs { get; set; }
}

