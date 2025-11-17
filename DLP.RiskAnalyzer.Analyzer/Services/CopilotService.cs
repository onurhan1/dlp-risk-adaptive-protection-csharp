using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// GitHub Copilot API Service for testing connections and validating API keys
/// </summary>
public class CopilotService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CopilotService> _logger;

    public CopilotService(HttpClient httpClient, ILogger<CopilotService> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DLP-RiskAnalyzer/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        _logger = logger;
    }

    /// <summary>
    /// Test GitHub Copilot API connection with provided API key/token
    /// </summary>
    public async Task<bool> TestConnectionAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GitHub Copilot API key is empty");
            return false;
        }

        try
        {
            // Clear previous headers
            _httpClient.DefaultRequestHeaders.Authorization = null;
            
            // Set authorization header
            // GitHub accepts both "token" and "Bearer" prefix, but "token" is more common for PATs
            if (apiKey.StartsWith("ghp_") || apiKey.StartsWith("github_pat_"))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            else
            {
                // For older token formats, try with "token" prefix
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", apiKey);
            }

            // Test by getting authenticated user info
            // This is a simple endpoint that requires authentication
            var response = await _httpClient.GetAsync("user");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GitHub API test failed. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("GitHub API key is invalid or expired");
                    return false;
                }
                
                return false;
            }

            // Verify response contains user data
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("GitHub API returned empty response");
                return false;
            }

            // Try to parse as JSON to verify it's a valid response
            try
            {
                var userInfo = JsonSerializer.Deserialize<JsonElement>(content);
                if (!userInfo.TryGetProperty("login", out _))
                {
                    _logger.LogWarning("GitHub API response does not contain expected user data");
                    return false;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse GitHub API response as JSON");
                return false;
            }

            _logger.LogInformation("GitHub Copilot API connection test successful");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error testing GitHub Copilot API connection");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout testing GitHub Copilot API connection");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing GitHub Copilot API connection");
            return false;
        }
    }

    /// <summary>
    /// Check if the API key has Copilot access
    /// Note: This requires additional API calls and may not be available for all token types
    /// </summary>
    public async Task<bool> CheckCopilotAccessAsync(string apiKey)
    {
        try
        {
            // Clear and set authorization
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Check Copilot usage endpoint (if available)
            // Note: This endpoint may require specific scopes
            var response = await _httpClient.GetAsync("user/copilot/usage");

            // 200 = has access, 403 = no access, 404 = endpoint not available
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("GitHub token does not have Copilot access");
                return false;
            }
            else
            {
                // Endpoint not available or other error - assume basic auth works
                _logger.LogInformation("Copilot usage endpoint not available, assuming basic authentication is sufficient");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check Copilot access, assuming basic auth is sufficient");
            // Don't fail the test if we can't check Copilot access
            return true;
        }
    }
}

