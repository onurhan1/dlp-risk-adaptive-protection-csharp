using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IBehaviorAIExplanationService
{
    Task<(string Explanation, string Recommendation)> GenerateAIAnalysisAsync(string entityType, string entityId, Dictionary<string, object> analysisData, AnomalyResults anomalyResults);
    string GenerateExplanation(string entityType, string entityId, BehaviorMetrics currentMetrics, BehaviorMetrics baselineMetrics, AnomalyResults anomalyResults);
    string GenerateRecommendation(AnomalyResults anomalyResults, string entityType);
}

public class BehaviorAIExplanationService : IBehaviorAIExplanationService
{
    // AI Settings keys
    private const string ModelProviderKey = "ai_model_provider";
    private const string ModelNameKey = "ai_model_name";
    private const string TemperatureKey = "ai_temperature";
    private const string MaxTokensKey = "ai_max_tokens";
    private const string OpenAIKeyKey = "openai_api_key";
    private const string AzureKeyKey = "azure_openai_api_key";
    private const string AzureEndpointKey = "azure_openai_endpoint";

    private readonly IAIAnalysisRepository _aiAnalysisRepository;
    private readonly ILogger<BehaviorAIExplanationService> _logger;
    private readonly IDataProtector _protector;
    private readonly IOpenAIService? _openAIService;
    private readonly IAzureOpenAIService? _azureOpenAIService;
    private readonly ICopilotService? _copilotService;

    public BehaviorAIExplanationService(
        IAIAnalysisRepository aiAnalysisRepository,
        ILogger<BehaviorAIExplanationService> logger,
        IDataProtectionProvider dataProtectionProvider,
        IServiceProvider serviceProvider)
    {
        _aiAnalysisRepository = aiAnalysisRepository;
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector("DLP.AISettings");
        try { _openAIService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IOpenAIService>(serviceProvider); }
        catch (Exception ex) { _logger.LogDebug(ex, "Optional OpenAIService not available"); }
        try { _azureOpenAIService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAzureOpenAIService>(serviceProvider); }
        catch (Exception ex) { _logger.LogDebug(ex, "Optional AzureOpenAIService not available"); }
        try { _copilotService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ICopilotService>(serviceProvider); }
        catch (Exception ex) { _logger.LogDebug(ex, "Optional CopilotService not available"); }
    }

    public async Task<(string Explanation, string Recommendation)> GenerateAIAnalysisAsync(
        string entityType,
        string entityId,
        Dictionary<string, object> analysisData,
        AnomalyResults anomalyResults)
    {
        try
        {
            // Get AI settings from database
            _logger.LogDebug("Fetching AI settings from database");
            var settings = await _aiAnalysisRepository.GetAISettingsAsync();
            
            _logger.LogDebug("Found {Count} AI settings in database", settings.Count);

        var provider = settings.GetValueOrDefault(ModelProviderKey, "local")?.ToLower() ?? "local";
        var modelName = settings.GetValueOrDefault(ModelNameKey, "");
        var temperatureStr = settings.GetValueOrDefault(TemperatureKey, "0.7");
        double? temperature = double.TryParse(temperatureStr, out var temp) ? temp : 0.7;
        var maxTokensStr = settings.GetValueOrDefault(MaxTokensKey, "1000");
        int? maxTokens = int.TryParse(maxTokensStr, out var tokens) ? tokens : 1000;

        // If provider is "local", use static explanation
        if (provider == "local")
        {
            throw new InvalidOperationException("Local provider - use static explanation");
        }

        // Try OpenAI
        if (provider == "openai" && _openAIService != null)
        {
            var apiKeySetting = settings.GetValueOrDefault(OpenAIKeyKey, "");
            if (string.IsNullOrEmpty(apiKeySetting))
            {
                throw new InvalidOperationException("OpenAI API key not configured");
            }

            try
            {
                var apiKey = _protector.Unprotect(apiKeySetting);
                var (explanation, recommendation) = await _openAIService.GenerateAnalysisAsync(
                    apiKey,
                    modelName ?? "gpt-4o-mini",
                    entityType,
                    entityId,
                    analysisData,
                    temperature,
                    maxTokens);

                _logger.LogInformation("Successfully generated AI analysis using OpenAI model: {Model}", modelName);
                return (explanation, recommendation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate AI analysis using OpenAI");
                throw;
            }
        }

        // Try Azure OpenAI
        if (provider == "azure" && _azureOpenAIService != null)
        {
            var apiKeySetting = settings.GetValueOrDefault(AzureKeyKey, "");
            var endpoint = settings.GetValueOrDefault(AzureEndpointKey, "");
            
            if (string.IsNullOrEmpty(apiKeySetting) || string.IsNullOrEmpty(endpoint))
            {
                throw new InvalidOperationException("Azure OpenAI API key or endpoint not configured");
            }

            try
            {
                var apiKey = _protector.Unprotect(apiKeySetting);
                var (explanation, recommendation) = await _azureOpenAIService.GenerateAnalysisAsync(
                    endpoint,
                    apiKey,
                    modelName ?? "gpt-4",
                    entityType,
                    entityId,
                    analysisData,
                    temperature,
                    maxTokens);

                _logger.LogInformation("Successfully generated AI analysis using Azure OpenAI model: {Model}", modelName);
                return (explanation, recommendation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate AI analysis using Azure OpenAI");
                throw;
            }
        }

        // Copilot is not supported for analysis generation (only for testing)
        if (provider == "copilot")
        {
            throw new InvalidOperationException("GitHub Copilot is not supported for behavioral analysis generation");
        }

            // Unknown provider or service not available
            throw new InvalidOperationException($"AI provider '{provider}' is not available or not configured");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateAIAnalysisAsync: {Error}", ex.Message);
            throw;
        }
    }

    public string GenerateExplanation(
        string entityType,
        string entityId,
        BehaviorMetrics current,
        BehaviorMetrics baseline,
        AnomalyResults results)
    {
        var parts = new List<string>();

        if (Math.Abs(results.IncidentCountZScore) > 2)
        {
            var change = current.MeanIncidentsPerDay > baseline.MeanIncidentsPerDay ? "increased" : "decreased";
            parts.Add($"Incident frequency {change} significantly (Z-score: {results.IncidentCountZScore:F2})");
        }

        if (Math.Abs(results.SeverityZScore) > 2)
        {
            var change = current.AvgSeverity > baseline.AvgSeverity ? "increased" : "decreased";
            parts.Add($"Average severity {change} (Z-score: {results.SeverityZScore:F2})");
        }

        if (Math.Abs(results.ChannelEmailZScore) > 2)
        {
            parts.Add($"Email channel activity anomaly detected (Z-score: {results.ChannelEmailZScore:F2})");
        }

        if (Math.Abs(results.ChannelWebZScore) > 2)
        {
            parts.Add($"Web channel activity anomaly detected (Z-score: {results.ChannelWebZScore:F2})");
        }

        if (Math.Abs(results.ChannelEndpointZScore) > 2)
        {
            parts.Add($"Endpoint channel activity anomaly detected (Z-score: {results.ChannelEndpointZScore:F2})");
        }

        if (parts.Count == 0)
        {
            return $"No significant behavioral anomalies detected for {entityType} '{entityId}' in the analyzed period.";
        }

        return string.Join(" ", parts);
    }

    public string GenerateRecommendation(AnomalyResults results, string entityType)
    {
        var maxZ = Math.Max(
            Math.Abs(results.IncidentCountZScore),
            Math.Max(
                Math.Abs(results.SeverityZScore),
                Math.Max(
                    Math.Abs(results.ChannelEmailZScore),
                    Math.Max(
                        Math.Abs(results.ChannelWebZScore),
                        Math.Abs(results.ChannelEndpointZScore)
                    )
                )
            )
        );

        if (maxZ >= 3)
        {
            return $"CRITICAL: High anomaly detected (Z-score: {maxZ:F2}). Immediate investigation recommended for {entityType}.";
        }
        if (maxZ >= 2)
        {
            return $"HIGH RISK: Significant anomaly detected (Z-score: {maxZ:F2}). Review {entityType} activity and consider enhanced monitoring.";
        }
        if (maxZ >= 1)
        {
            return $"MEDIUM RISK: Moderate anomaly detected (Z-score: {maxZ:F2}). Monitor {entityType} for continued unusual activity.";
        }

        return "Low risk: Behavior within normal parameters. Continue standard monitoring.";
    }


}
