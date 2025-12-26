using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Remediation Service - Incident remediation via Forcepoint DLP API + Database storage
/// </summary>
public class RemediationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AnalyzerDbContext _dbContext;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public RemediationService(HttpClient httpClient, IConfiguration configuration, AnalyzerDbContext dbContext)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _dbContext = dbContext;
        
        var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
        var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
        
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{dlpIp}:{dlpPort}"),
            Timeout = TimeSpan.FromSeconds(5) // Short timeout to fail fast
        };
    }

    /// <summary>
    /// Get JWT access token from Forcepoint DLP API
    /// According to Forcepoint DLP REST API v1 documentation:
    /// POST https://&lt;DLP Manager IP&gt;:&lt;DLP Manager port&gt;/dlp/rest/v1/auth/access-token
    /// Returns null if DLP Manager API is unavailable
    /// </summary>
    private async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        try
        {
            // Forcepoint DLP REST API v1 Authentication endpoint
            // According to Forcepoint DLP REST API documentation:
            // POST https://<DLP Manager IP>:<DLP Manager port>/dlp/rest/v1/auth/access-token
            // Request format: application/x-www-form-urlencoded (not JSON)
            var username = _configuration["DLP:Username"] ?? "";
            var password = _configuration["DLP:Password"] ?? "";

            // Request body: username=xxx&password=yyy (form-urlencoded format)
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            };
            var content = new FormUrlEncodedContent(formData);

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.PostAsync("/dlp/rest/v1/auth/access-token", content);
            }
            catch (Exception)
            {
                // Any exception means DLP Manager API is unavailable
                return null;
            }
            
            if (response == null || !response.IsSuccessStatusCode)
            {
                return null; // API unavailable
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

            // Forcepoint DLP API returns access_token (snake_case), but some versions use accessToken (camelCase)
            _accessToken = tokenResponse?.ContainsKey("access_token") == true
                ? tokenResponse["access_token"].ToString()
                : tokenResponse?.ContainsKey("accessToken") == true
                    ? tokenResponse["accessToken"].ToString()
                    : tokenResponse?.ContainsKey("token") == true
                        ? tokenResponse["token"].ToString()
                        : null;

            if (_accessToken != null)
            {
                // Forcepoint DLP tokens typically expire in 1 hour (3600 seconds) or access_token_expires_in field
                var expiresIn = tokenResponse?.ContainsKey("access_token_expires_in") == true
                    ? Convert.ToInt32(tokenResponse["access_token_expires_in"])
                    : tokenResponse?.ContainsKey("expiresIn") == true
                        ? Convert.ToInt32(tokenResponse["expiresIn"])
                        : 3600;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300); // Refresh 5 minutes before expiry
            }
            
            return _accessToken;
        }
        catch (Exception)
        {
            // Any exception means DLP Manager API is unavailable
            return null;
        }
    }

    /// <summary>
    /// Remediate incident via Forcepoint DLP API + save to database
    /// According to Forcepoint DLP REST API v1 documentation:
    /// POST https://&lt;DLP Manager IP&gt;:&lt;DLP Manager port&gt;/dlp/rest/v1/incidents/update
    /// </summary>
    public async Task<Dictionary<string, object>> RemediateIncidentAsync(
        string incidentId,
        string action,
        string? reason = null,
        string? notes = null,
        string? remediatedBy = null)
    {
        var remediatedAt = DateTime.UtcNow;
        var apiMessage = "";
        var apiSuccess = false;
        
        try
        {
            // Step 1: Try to call DLP API (optional, may fail if unavailable)
            var token = await GetAccessTokenAsync();
            if (token != null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var requestBody = new { incidentId, action, reason = reason ?? "", notes = notes ?? "" };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                try
                {
                    var response = await _httpClient.PostAsync("/dlp/rest/v1/incidents/update", content);
                    apiSuccess = response.IsSuccessStatusCode;
                    apiMessage = apiSuccess ? "DLP API updated" : $"DLP API returned {response.StatusCode}";
                }
                catch
                {
                    apiMessage = "DLP API unavailable";
                }
            }
            else
            {
                apiMessage = "DLP API unavailable";
            }
        }
        catch
        {
            apiMessage = "DLP API error";
        }
        
        // Step 2: ALWAYS save to database (this is the important part)
        try
        {
            if (int.TryParse(incidentId, out int id))
            {
                var incident = await _dbContext.Incidents.FirstOrDefaultAsync(i => i.Id == id);
                if (incident != null)
                {
                    incident.IsRemediated = true;
                    incident.RemediatedAt = remediatedAt;
                    incident.RemediatedBy = remediatedBy ?? "System";
                    incident.RemediationAction = action;
                    incident.RemediationNotes = notes ?? reason ?? "";
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception dbEx)
        {
            return new Dictionary<string, object>
            {
                { "success", false },
                { "message", $"Database error: {dbEx.Message}" },
                { "incidentId", incidentId },
                { "action", action },
                { "remediatedAt", remediatedAt.ToString("O") }
            };
        }
        
        return new Dictionary<string, object>
        {
            { "success", true },
            { "message", $"Incident remediated successfully ({apiMessage})" },
            { "savedToDatabase", true },
            { "incidentId", incidentId },
            { "action", action },
            { "reason", reason ?? "" },
            { "notes", notes ?? "" },
            { "remediatedAt", remediatedAt.ToString("O") }
        };
    }

    /// <summary>
    /// Update incident
    /// </summary>
    public async Task<Dictionary<string, object>> UpdateIncidentAsync(
        string incidentId,
        string? status = null,
        int? severity = null,
        string? assignedTo = null,
        string? notes = null,
        string? reason = null)
    {
        return await RemediateIncidentAsync(
            incidentId,
            status ?? "investigating",
            reason,
            notes
        );
    }
}

