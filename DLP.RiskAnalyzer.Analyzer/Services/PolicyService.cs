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

        var username = _configuration["DLP:Username"] ?? "";
        var password = _configuration["DLP:Password"] ?? "";

        var requestBody = new
        {
            username,
            password
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/dlp/rest/v1/auth/access-token", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

        _accessToken = tokenResponse?.ContainsKey("accessToken") == true 
            ? tokenResponse["accessToken"].ToString()
            : tokenResponse?.ContainsKey("token") == true
                ? tokenResponse["token"].ToString()
                : throw new Exception("No token received from DLP API");

        _tokenExpiry = DateTime.UtcNow.AddMinutes(55);
        return _accessToken!;
    }

    /// <summary>
    /// Fetch all policies from Forcepoint DLP API
    /// </summary>
    public async Task<List<Dictionary<string, object>>> FetchPoliciesAsync()
    {
        var token = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetAsync("/dlp/rest/v1/policies");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var policies = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content) 
            ?? new List<Dictionary<string, object>>();

        return policies;
    }

    /// <summary>
    /// Fetch a specific policy by ID
    /// </summary>
    public async Task<Dictionary<string, object>> FetchPolicyAsync(string policyId)
    {
        var token = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetAsync($"/dlp/rest/v1/policies/{policyId}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var policy = JsonSerializer.Deserialize<Dictionary<string, object>>(content)
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

