using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using DLP.RiskAnalyzer.Shared.Services;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Policy Management Service - Forcepoint DLP Policy operations
/// </summary>
public class PolicyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public PolicyService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
        var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
        _httpClient.BaseAddress = new Uri($"https://{dlpIp}:{dlpPort}");
        
        // SSL certificate bypass for self-signed certs
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{dlpIp}:{dlpPort}")
        };
    }

    /// <summary>
    /// Get JWT access token from Forcepoint DLP API
    /// </summary>
    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

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

        var response = await _httpClient.PostAsync("/dlp/rest/v1/auth/access-token", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

        // Forcepoint DLP API returns access_token (snake_case), but some versions use accessToken (camelCase)
        _accessToken = tokenResponse?.ContainsKey("access_token") == true 
            ? tokenResponse["access_token"].ToString()
            : tokenResponse?.ContainsKey("accessToken") == true
                ? tokenResponse["accessToken"].ToString()
                : tokenResponse?.ContainsKey("token") == true
                    ? tokenResponse["token"].ToString()
                    : throw new Exception("No token received from DLP API");

        // Forcepoint DLP tokens typically expire in 1 hour (3600 seconds) or access_token_expires_in field
        var expiresIn = tokenResponse?.ContainsKey("access_token_expires_in") == true
            ? Convert.ToInt32(tokenResponse["access_token_expires_in"])
            : tokenResponse?.ContainsKey("expiresIn") == true
                ? Convert.ToInt32(tokenResponse["expiresIn"])
                : 3600;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300); // Refresh 5 minutes before expiry
        return _accessToken!;
    }

    /// <summary>
    /// Fetch all policies from Forcepoint DLP API
    /// According to Forcepoint DLP REST API v1 documentation:
    /// POST https://<DLP Manager IP>:<DLP Manager port>/dlp/rest/v1/policies
    /// </summary>
    public async Task<List<Dictionary<string, object>>> FetchPoliciesAsync()
    {
        var token = await GetAccessTokenAsync();
        
        // Forcepoint DLP API uses POST for all operations
        var request = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/policies");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Some DLP API versions may require a body, even if empty
        var requestBody = new { type = "POLICIES" };
        var jsonBody = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var policies = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(responseContent) 
            ?? new List<Dictionary<string, object>>();

        return policies;
    }

    /// <summary>
    /// Fetch a specific policy by ID
    /// According to Forcepoint DLP REST API v1 documentation:
    /// POST https://<DLP Manager IP>:<DLP Manager port>/dlp/rest/v1/policies/{policyId}
    /// </summary>
    public async Task<Dictionary<string, object>> FetchPolicyAsync(string policyId)
    {
        var token = await GetAccessTokenAsync();
        
        // Forcepoint DLP API uses POST for all operations
        var request = new HttpRequestMessage(HttpMethod.Post, $"/dlp/rest/v1/policies/{policyId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Some DLP API versions may require a body with policy ID
        var requestBody = new { policy_id = policyId };
        var jsonBody = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var policy = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent)
            ?? new Dictionary<string, object>();

        return policy;
    }

    /// <summary>
    /// Get risk-adaptive policy recommendation
    /// </summary>
    public Dictionary<string, object> GetPolicyRecommendation(
        int riskScore,
        string riskLevel,
        string channel)
    {
        var riskAnalyzer = new Shared.Services.RiskAnalyzer();
        var recommendedAction = riskAnalyzer.GetPolicyAction(riskLevel, channel);

        var priority = riskLevel is "Critical" or "High" ? "High" : "Medium";

        return new Dictionary<string, object>
        {
            { "risk_score", riskScore },
            { "risk_level", riskLevel },
            { "channel", channel },
            { "recommended_action", recommendedAction },
            { "priority", priority },
            { "description", $"Recommended {recommendedAction} action for {riskLevel} risk user on {channel} channel" }
        };
    }
}

