using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Azure OpenAI API Service for testing connections and validating API keys
/// </summary>
public class AzureOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(HttpClient httpClient, ILogger<AzureOpenAIService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DLP-RiskAnalyzer/1.0");
        _logger = logger;
    }

    /// <summary>
    /// Test Azure OpenAI API connection with provided endpoint and API key
    /// </summary>
    public async Task<bool> TestConnectionAsync(string endpoint, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogWarning("Azure OpenAI endpoint is empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Azure OpenAI API key is empty");
            return false;
        }

        try
        {
            // Normalize endpoint URL
            var baseUrl = endpoint.TrimEnd('/');
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
            {
                baseUrl = "https://" + baseUrl;
            }

            // Clear previous headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DLP-RiskAnalyzer/1.0");

            // Test by listing available models
            // Azure OpenAI endpoint format: https://{resource}.openai.azure.com/openai/models?api-version=2024-02-15-preview
            var modelsUrl = $"{baseUrl}/openai/models?api-version=2024-02-15-preview";
            
            var response = await _httpClient.GetAsync(modelsUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Azure OpenAI API test failed. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Azure OpenAI API key is invalid");
                    return false;
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Azure OpenAI endpoint not found. Check the endpoint URL.");
                    return false;
                }
                
                return false;
            }

            // Verify response contains model data
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Azure OpenAI API returned empty response");
                return false;
            }

            // Try to parse as JSON to verify it's a valid response
            try
            {
                var modelsResponse = JsonSerializer.Deserialize<JsonElement>(content);
                if (!modelsResponse.TryGetProperty("data", out _))
                {
                    _logger.LogWarning("Azure OpenAI API response does not contain expected model data");
                    // Still consider it successful if we got a 200 response
                    return true;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Azure OpenAI API response as JSON, but got 200 status");
                // If we got 200, the connection is working even if we can't parse the response
                return true;
            }

            _logger.LogInformation("Azure OpenAI API connection test successful");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error testing Azure OpenAI API connection");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout testing Azure OpenAI API connection");
            return false;
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex, "Invalid Azure OpenAI endpoint URL format");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Azure OpenAI API connection");
            return false;
        }
    }

    /// <summary>
    /// Generate AI explanation and recommendation for behavioral analysis
    /// </summary>
    public async Task<(string Explanation, string Recommendation)> GenerateAnalysisAsync(
        string endpoint,
        string apiKey,
        string model,
        string entityType,
        string entityId,
        Dictionary<string, object> analysisData,
        double? temperature = null,
        int? maxTokens = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint is required", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        try
        {
            // Normalize endpoint URL
            var baseUrl = endpoint.TrimEnd('/');
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
            {
                baseUrl = "https://" + baseUrl;
            }

            // Clear previous headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DLP-RiskAnalyzer/1.0");

            // Build prompt from analysis data
            var prompt = BuildAnalysisPrompt(entityType, entityId, analysisData);

            // Create chat completion request
            var requestBody = new
            {
                model = model ?? "gpt-4",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a cybersecurity analyst specializing in Data Loss Prevention (DLP) behavioral analysis. Analyze the provided metrics and provide clear, actionable insights."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature = temperature ?? 0.7,
                max_tokens = maxTokens ?? 500
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Azure OpenAI endpoint format: https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview
            var chatUrl = $"{baseUrl}/openai/deployments/{model ?? "gpt-4"}/chat/completions?api-version=2024-02-15-preview";
            
            var response = await _httpClient.PostAsync(chatUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Azure OpenAI API call failed. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Azure OpenAI API call failed: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<OpenAIChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                throw new InvalidOperationException("Azure OpenAI API returned empty response");
            }

            var aiResponse = chatResponse.Choices[0].Message?.Content ?? "";
            
            // Parse explanation and recommendation from AI response
            var (explanation, recommendation) = ParseAIResponse(aiResponse);

            return (explanation, recommendation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI analysis");
            throw;
        }
    }

    private string BuildAnalysisPrompt(string entityType, string entityId, Dictionary<string, object> analysisData)
    {
        var prompt = $@"Analyze the following DLP behavioral metrics for {entityType} '{entityId}':

Current Period Metrics:
- Total Incidents: {analysisData.GetValueOrDefault("current_incident_count", 0)}
- Mean Incidents per Day: {analysisData.GetValueOrDefault("current_mean_incidents", 0):F2}
- Average Severity: {analysisData.GetValueOrDefault("current_avg_severity", 0):F2}

Baseline Period Metrics:
- Total Incidents: {analysisData.GetValueOrDefault("baseline_incident_count", 0)}
- Mean Incidents per Day: {analysisData.GetValueOrDefault("baseline_mean_incidents", 0):F2}
- Average Severity: {analysisData.GetValueOrDefault("baseline_avg_severity", 0):F2}

Anomaly Detection (Z-scores):
- Incident Count Z-score: {analysisData.GetValueOrDefault("z_score_incident_count", 0):F2}
- Severity Z-score: {analysisData.GetValueOrDefault("z_score_severity", 0):F2}
- Email Channel Z-score: {analysisData.GetValueOrDefault("z_score_channel_email", 0):F2}
- Web Channel Z-score: {analysisData.GetValueOrDefault("z_score_channel_web", 0):F2}
- Endpoint Channel Z-score: {analysisData.GetValueOrDefault("z_score_channel_endpoint", 0):F2}

Risk Score: {analysisData.GetValueOrDefault("risk_score", 0)}/100

Please provide:
1. EXPLANATION: A clear explanation of the behavioral anomalies detected (2-3 sentences)
2. RECOMMENDATION: A specific, actionable recommendation (1-2 sentences)

Format your response as:
EXPLANATION: [your explanation]
RECOMMENDATION: [your recommendation]";

        return prompt;
    }

    private (string Explanation, string Recommendation) ParseAIResponse(string aiResponse)
    {
        var explanation = "";
        var recommendation = "";

        // Try to parse structured response
        var explanationMatch = Regex.Match(
            aiResponse, 
            @"EXPLANATION:\s*(.+?)(?=RECOMMENDATION:|$)", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        var recommendationMatch = Regex.Match(
            aiResponse, 
            @"RECOMMENDATION:\s*(.+?)(?=$)", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (explanationMatch.Success)
        {
            explanation = explanationMatch.Groups[1].Value.Trim();
        }

        if (recommendationMatch.Success)
        {
            recommendation = recommendationMatch.Groups[1].Value.Trim();
        }

        // Fallback: if parsing failed, split response in half
        if (string.IsNullOrWhiteSpace(explanation) || string.IsNullOrWhiteSpace(recommendation))
        {
            var parts = aiResponse.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                explanation = string.Join(" ", parts.Take(parts.Length / 2));
                recommendation = string.Join(" ", parts.Skip(parts.Length / 2));
            }
            else
            {
                explanation = aiResponse;
                recommendation = "Please review the analysis above and take appropriate action.";
            }
        }

        return (explanation, recommendation);
    }
}

