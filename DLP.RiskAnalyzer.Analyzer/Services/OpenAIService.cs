using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// OpenAI API Service for testing connections and validating API keys
/// </summary>
public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(HttpClient httpClient, ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    /// <summary>
    /// Test OpenAI API connection with provided API key
    /// </summary>
    public async Task<bool> TestConnectionAsync(string apiKey, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OpenAI API key is empty");
            return false;
        }

        try
        {
            // Clear previous headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DLP-RiskAnalyzer/1.0");

            // Test by listing available models
            var response = await _httpClient.GetAsync("models");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("OpenAI API test failed. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                return false;
            }

            // If model is specified, verify it's available
            if (!string.IsNullOrWhiteSpace(model))
            {
                var content = await response.Content.ReadAsStringAsync();
                var modelsResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (modelsResponse?.Data != null)
                {
                    var modelExists = modelsResponse.Data.Any(m => m.Id == model);
                    if (!modelExists)
                    {
                        _logger.LogWarning("Specified model {Model} not found in available models", model);
                        // Don't fail the test, just log a warning - model might be available but not in list
                    }
                }
            }

            _logger.LogInformation("OpenAI API connection test successful");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error testing OpenAI API connection");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout testing OpenAI API connection");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing OpenAI API connection");
            return false;
        }
    }

    /// <summary>
    /// Generate AI explanation and recommendation for behavioral analysis
    /// </summary>
    public async Task<(string Explanation, string Recommendation)> GenerateAnalysisAsync(
        string apiKey,
        string model,
        string entityType,
        string entityId,
        Dictionary<string, object> analysisData,
        double? temperature = null,
        int? maxTokens = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        try
        {
            // Clear previous headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DLP-RiskAnalyzer/1.0");

            // Build prompt from analysis data
            var prompt = BuildAnalysisPrompt(entityType, entityId, analysisData);

            // Create chat completion request
            var requestBody = new
            {
                model = model ?? "gpt-4o-mini",
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

            var response = await _httpClient.PostAsync("chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API call failed. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"OpenAI API call failed: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<OpenAIChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                throw new InvalidOperationException("OpenAI API returned empty response");
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

/// <summary>
/// OpenAI Chat Completion API Response
/// </summary>
public class OpenAIChatResponse
{
    public List<OpenAIChatChoice>? Choices { get; set; }
}

public class OpenAIChatChoice
{
    public OpenAIChatMessage? Message { get; set; }
}

public class OpenAIChatMessage
{
    public string? Content { get; set; }
}

/// <summary>
/// OpenAI Models API Response
/// </summary>
public class OpenAIModelsResponse
{
    public List<OpenAIModel>? Data { get; set; }
}

public class OpenAIModel
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public long? Created { get; set; }
    public string? OwnedBy { get; set; }
}

