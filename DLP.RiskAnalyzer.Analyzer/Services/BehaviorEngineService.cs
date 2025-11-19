using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// AI Behavioral Analysis Engine - Z-score based anomaly detection (Baseline PoC)
/// </summary>
public class BehaviorEngineService
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<BehaviorEngineService> _logger;
    private readonly IDataProtector _protector;
    private readonly OpenAIService? _openAIService;
    private readonly AzureOpenAIService? _azureOpenAIService;
    private readonly CopilotService? _copilotService;

    private const string OpenAIKeyKey = "ai_openai_api_key_protected";
    private const string CopilotKeyKey = "ai_copilot_api_key_protected";
    private const string AzureKeyKey = "ai_azure_openai_key_protected";
    private const string AzureEndpointKey = "ai_azure_openai_endpoint";
    private const string ModelProviderKey = "ai_model_provider";
    private const string ModelNameKey = "ai_model_name";
    private const string TemperatureKey = "ai_temperature";
    private const string MaxTokensKey = "ai_max_tokens";

    public BehaviorEngineService(
        AnalyzerDbContext context,
        ILogger<BehaviorEngineService> logger,
        IDataProtectionProvider dataProtectionProvider,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector("AI.SettingsProtector");
        
        // Get AI services if available (optional dependencies)
        try
        {
            _openAIService = serviceProvider.GetService<OpenAIService>();
        }
        catch
        {
            _openAIService = null;
        }

        try
        {
            _azureOpenAIService = serviceProvider.GetService<AzureOpenAIService>();
        }
        catch
        {
            _azureOpenAIService = null;
        }

        try
        {
            _copilotService = serviceProvider.GetService<CopilotService>();
        }
        catch
        {
            _copilotService = null;
        }
    }

    /// <summary>
    /// Analyze behavior for a specific entity (user/channel/department)
    /// </summary>
    public async Task<AIBehavioralAnalysisResponse> AnalyzeEntityAsync(
        string entityType,
        string entityId,
        int lookbackDays = 7)
    {
        try
        {
            _logger.LogInformation("Starting analysis for {EntityType}: {EntityId} (lookbackDays: {LookbackDays})", entityType, entityId, lookbackDays);
            
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-lookbackDays);
            var baselineStartDate = endDate.AddDays(-(lookbackDays * 2)); // Baseline: previous period

            // Get current period incidents
            _logger.LogDebug("Fetching current period incidents for {EntityType}: {EntityId} from {StartDate} to {EndDate}", 
                entityType, entityId, startDate, endDate);
            var currentIncidents = await GetIncidentsForEntityAsync(entityType, entityId, startDate, endDate);
            _logger.LogInformation("Found {Count} current period incidents for {EntityType}: {EntityId}", 
                currentIncidents.Count, entityType, entityId);
            
            // Get baseline period incidents (previous period)
            _logger.LogDebug("Fetching baseline period incidents for {EntityType}: {EntityId} from {BaselineStartDate} to {StartDate}", 
                entityType, entityId, baselineStartDate, startDate);
            var baselineIncidents = await GetIncidentsForEntityAsync(entityType, entityId, baselineStartDate, startDate);
            _logger.LogInformation("Found {Count} baseline period incidents for {EntityType}: {EntityId}", 
                baselineIncidents.Count, entityType, entityId);

            if (currentIncidents.Count == 0 && baselineIncidents.Count == 0)
            {
                _logger.LogInformation("No incidents found for {EntityType}: {EntityId}, returning empty analysis", entityType, entityId);
                return new AIBehavioralAnalysisResponse
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    RiskScore = 0,
                    AnomalyLevel = "low",
                    AIExplanation = $"No incidents found for {entityType} '{entityId}' in the analyzed period.",
                    AIRecommendation = "No action required.",
                    ReferenceIncidentIds = new List<int>(),
                    AnalysisMetadata = new Dictionary<string, object>(),
                    AnalysisDate = endDate
                };
            }

            // Calculate metrics
            _logger.LogDebug("Calculating metrics for {EntityType}: {EntityId}", entityType, entityId);
            var currentMetrics = CalculateMetrics(currentIncidents);
            var baselineMetrics = CalculateMetrics(baselineIncidents);

            // Z-score anomaly detection
            var anomalyResults = DetectAnomalies(currentMetrics, baselineMetrics);

            // Calculate risk score (0-100)
            var riskScore = CalculateRiskScore(anomalyResults);

            // Determine anomaly level
            var anomalyLevel = DetermineAnomalyLevel(riskScore);

            // Get reference incident IDs
            var referenceIncidentIds = currentIncidents
                .Where(i => i.RiskScore >= 50 || i.Severity >= 7)
                .Select(i => i.Id)
                .Distinct()
                .Take(10)
                .ToList();

            var metadata = new Dictionary<string, object>
            {
                { "current_incident_count", currentMetrics.TotalIncidents },
                { "baseline_incident_count", baselineMetrics.TotalIncidents },
                { "z_score_incident_count", anomalyResults.IncidentCountZScore },
                { "z_score_severity", anomalyResults.SeverityZScore },
                { "z_score_channel_email", anomalyResults.ChannelEmailZScore },
                { "z_score_channel_web", anomalyResults.ChannelWebZScore },
                { "z_score_channel_endpoint", anomalyResults.ChannelEndpointZScore },
                { "baseline_mean_incidents", baselineMetrics.MeanIncidentsPerDay },
                { "baseline_std_incidents", baselineMetrics.StdDevIncidentsPerDay },
                { "current_mean_incidents", currentMetrics.MeanIncidentsPerDay },
                { "current_avg_severity", currentMetrics.AvgSeverity },
                { "baseline_avg_severity", baselineMetrics.AvgSeverity },
                { "risk_score", riskScore }
            };

            // Generate AI explanation and recommendation using selected model (or fallback to static)
            string explanation;
            string recommendation;
            
            try
            {
                var (aiExplanation, aiRecommendation) = await GenerateAIAnalysisAsync(
                    entityType, 
                    entityId, 
                    metadata, 
                    anomalyResults);
                
                explanation = aiExplanation;
                recommendation = aiRecommendation;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI analysis, falling back to static explanation");
                // Fallback to static explanation
                explanation = GenerateExplanation(entityType, entityId, currentMetrics, baselineMetrics, anomalyResults);
                recommendation = GenerateRecommendation(anomalyResults, entityType);
            }

            _logger.LogInformation("Analysis completed for {EntityType}: {EntityId}. RiskScore: {RiskScore}, AnomalyLevel: {AnomalyLevel}", 
                entityType, entityId, riskScore, anomalyLevel);

            return new AIBehavioralAnalysisResponse
            {
                EntityType = entityType,
                EntityId = entityId,
                RiskScore = riskScore,
                AnomalyLevel = anomalyLevel,
                AIExplanation = explanation,
                AIRecommendation = recommendation,
                ReferenceIncidentIds = referenceIncidentIds,
                AnalysisMetadata = metadata,
                AnalysisDate = endDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in AnalyzeEntityAsync for {EntityType}: {EntityId}. Error: {Error}", 
                entityType, entityId, ex.Message);
            throw; // Re-throw to be handled by controller
        }
    }

    /// <summary>
    /// Analyze all entities and return overview
    /// </summary>
    public async Task<AIBehavioralOverviewResponse> AnalyzeOverviewAsync(int lookbackDays = 7)
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-lookbackDays);

        // Analyze users
        var users = await _context.Incidents
            .Where(i => i.Timestamp >= startDate)
            .Select(i => i.UserEmail)
            .Distinct()
            .ToListAsync();

        var userAnalyses = new List<AIBehavioralAnalysisResponse>();
        foreach (var user in users.Take(50)) // Limit to top 50 users
        {
            try
            {
                var analysis = await AnalyzeEntityAsync("user", user, lookbackDays);
                userAnalyses.Add(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze user {User}", user);
            }
        }

        // Analyze channels
        var channels = await _context.Incidents
            .Where(i => i.Timestamp >= startDate && !string.IsNullOrEmpty(i.Channel))
            .Select(i => i.Channel!)
            .Distinct()
            .ToListAsync();

        var channelAnalyses = new List<AIBehavioralAnalysisResponse>();
        foreach (var channel in channels)
        {
            try
            {
                var analysis = await AnalyzeEntityAsync("channel", channel, lookbackDays);
                channelAnalyses.Add(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze channel {Channel}", channel);
            }
        }

        // Analyze departments
        var departments = await _context.Incidents
            .Where(i => i.Timestamp >= startDate && !string.IsNullOrEmpty(i.Department))
            .Select(i => i.Department!)
            .Distinct()
            .ToListAsync();

        var departmentAnalyses = new List<AIBehavioralAnalysisResponse>();
        foreach (var department in departments)
        {
            try
            {
                var analysis = await AnalyzeEntityAsync("department", department, lookbackDays);
                departmentAnalyses.Add(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze department {Department}", department);
            }
        }

        var allAnalyses = userAnalyses.Concat(channelAnalyses).Concat(departmentAnalyses).ToList();

        var highAnomalies = allAnalyses.Count(a => a.AnomalyLevel == "high");
        var mediumAnomalies = allAnalyses.Count(a => a.AnomalyLevel == "medium");
        var lowAnomalies = allAnalyses.Count(a => a.AnomalyLevel == "low");

        var topAnomalies = allAnalyses
            .OrderByDescending(a => a.RiskScore)
            .Take(20)
            .ToList();

        var anomalyByChannel = channelAnalyses
            .GroupBy(a => a.EntityId)
            .ToDictionary(g => g.Key, g => g.Count(a => a.AnomalyLevel == "high" || a.AnomalyLevel == "medium"));

        var anomalyByDepartment = departmentAnalyses
            .GroupBy(a => a.EntityId)
            .ToDictionary(g => g.Key, g => g.Count(a => a.AnomalyLevel == "high" || a.AnomalyLevel == "medium"));

        return new AIBehavioralOverviewResponse
        {
            TotalAnalyzed = allAnalyses.Count,
            HighAnomalyCount = highAnomalies,
            MediumAnomalyCount = mediumAnomalies,
            LowAnomalyCount = lowAnomalies,
            TopAnomalies = topAnomalies,
            AnomalyByChannel = anomalyByChannel,
            AnomalyByDepartment = anomalyByDepartment
        };
    }

    /// <summary>
    /// Save analysis result to database
    /// </summary>
    public async Task SaveAnalysisAsync(AIBehavioralAnalysisResponse response)
    {
        var existing = await _context.AIBehavioralAnalyses
            .FirstOrDefaultAsync(a => 
                a.EntityType == response.EntityType &&
                a.EntityId == response.EntityId &&
                a.AnalysisDate.Date == response.AnalysisDate.Date);

        if (existing != null)
        {
            existing.RiskScore = response.RiskScore;
            existing.AnomalyLevel = response.AnomalyLevel;
            existing.AIExplanation = response.AIExplanation;
            existing.AIRecommendation = response.AIRecommendation;
            existing.ReferenceIncidentIds = JsonSerializer.Serialize(response.ReferenceIncidentIds);
            existing.AnalysisMetadata = JsonSerializer.Serialize(response.AnalysisMetadata);
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            var analysis = new AIBehavioralAnalysis
            {
                EntityType = response.EntityType,
                EntityId = response.EntityId,
                AnalysisDate = response.AnalysisDate,
                RiskScore = response.RiskScore,
                AnomalyLevel = response.AnomalyLevel,
                AIExplanation = response.AIExplanation,
                AIRecommendation = response.AIRecommendation,
                ReferenceIncidentIds = JsonSerializer.Serialize(response.ReferenceIncidentIds),
                AnalysisMetadata = JsonSerializer.Serialize(response.AnalysisMetadata),
                CreatedAt = DateTime.UtcNow
            };
            _context.AIBehavioralAnalyses.Add(analysis);
        }

        await _context.SaveChangesAsync();
    }

    #region Private Helper Methods

    private async Task<List<Incident>> GetIncidentsForEntityAsync(
        string entityType,
        string entityId,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            var query = _context.Incidents
                .Where(i => i.Timestamp >= startDate && i.Timestamp < endDate);

            var result = entityType.ToLower() switch
            {
                "user" => await query.Where(i => i.UserEmail == entityId).ToListAsync(),
                "channel" => await query.Where(i => i.Channel == entityId).ToListAsync(),
                "department" => await query.Where(i => i.Department == entityId).ToListAsync(),
                _ => new List<Incident>()
            };
            
            _logger.LogDebug("Query executed for {EntityType}: {EntityId}. Found {Count} incidents between {StartDate} and {EndDate}", 
                entityType, entityId, result.Count, startDate, endDate);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetIncidentsForEntityAsync for {EntityType}: {EntityId}. Error: {Error}", 
                entityType, entityId, ex.Message);
            throw;
        }
    }

    private BehaviorMetrics CalculateMetrics(List<Incident> incidents)
    {
        if (incidents.Count == 0)
        {
            return new BehaviorMetrics();
        }

        var incidentsPerDay = incidents
            .GroupBy(i => i.Timestamp.Date)
            .Select(g => g.Count())
            .ToList();

        var mean = incidentsPerDay.Count > 0 ? incidentsPerDay.Average() : 0;
        var stdDev = incidentsPerDay.Count > 1
            ? Math.Sqrt(incidentsPerDay.Sum(x => Math.Pow(x - mean, 2)) / (incidentsPerDay.Count - 1))
            : 0;

        var avgSeverity = incidents.Average(i => i.Severity);
        var severityStdDev = incidents.Count > 1
            ? Math.Sqrt(incidents.Sum(i => Math.Pow(i.Severity - avgSeverity, 2)) / (incidents.Count - 1))
            : 0;

        var channelCounts = incidents
            .Where(i => !string.IsNullOrEmpty(i.Channel))
            .GroupBy(i => i.Channel)
            .ToDictionary(g => g.Key!, g => g.Count());

        return new BehaviorMetrics
        {
            TotalIncidents = incidents.Count,
            MeanIncidentsPerDay = mean,
            StdDevIncidentsPerDay = stdDev,
            AvgSeverity = avgSeverity,
            StdDevSeverity = severityStdDev,
            ChannelCounts = channelCounts
        };
    }

    private AnomalyResults DetectAnomalies(BehaviorMetrics current, BehaviorMetrics baseline)
    {
        // Z-score calculation: z = (x - μ) / σ
        var incidentCountZ = baseline.StdDevIncidentsPerDay > 0
            ? (current.MeanIncidentsPerDay - baseline.MeanIncidentsPerDay) / baseline.StdDevIncidentsPerDay
            : 0;

        var severityZ = baseline.StdDevSeverity > 0
            ? (current.AvgSeverity - baseline.AvgSeverity) / baseline.StdDevSeverity
            : 0;

        // Channel-specific z-scores
        var emailZ = CalculateChannelZScore("Email", current, baseline);
        var webZ = CalculateChannelZScore("Web", current, baseline);
        var endpointZ = CalculateChannelZScore("Endpoint", current, baseline);

        return new AnomalyResults
        {
            IncidentCountZScore = incidentCountZ,
            SeverityZScore = severityZ,
            ChannelEmailZScore = emailZ,
            ChannelWebZScore = webZ,
            ChannelEndpointZScore = endpointZ
        };
    }

    private double CalculateChannelZScore(string channel, BehaviorMetrics current, BehaviorMetrics baseline)
    {
        var currentCount = current.ChannelCounts.GetValueOrDefault(channel, 0);
        var baselineCount = baseline.ChannelCounts.GetValueOrDefault(channel, 0);
        var baselineMean = baseline.ChannelCounts.Values.Any() ? baseline.ChannelCounts.Values.Average() : 0;
        var baselineStd = baseline.ChannelCounts.Count > 1
            ? Math.Sqrt(baseline.ChannelCounts.Values.Sum(x => Math.Pow(x - baselineMean, 2)) / (baseline.ChannelCounts.Count - 1))
            : 1;

        return baselineStd > 0 ? (currentCount - baselineMean) / baselineStd : 0;
    }

    private int CalculateRiskScore(AnomalyResults results)
    {
        // Risk score based on z-scores
        // Z-score > 2: high anomaly, 1-2: medium, <1: low
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

        // Convert z-score to risk score (0-100)
        // Z > 3: 100, Z > 2: 80, Z > 1: 50, Z <= 1: 30
        if (maxZ >= 3) return 100;
        if (maxZ >= 2) return 80;
        if (maxZ >= 1) return 50;
        return 30;
    }

    private string DetermineAnomalyLevel(int riskScore)
    {
        return riskScore switch
        {
            >= 80 => "high",
            >= 50 => "medium",
            _ => "low"
        };
    }

    /// <summary>
    /// Generate AI explanation and recommendation using the selected model provider
    /// </summary>
    private async Task<(string Explanation, string Recommendation)> GenerateAIAnalysisAsync(
        string entityType,
        string entityId,
        Dictionary<string, object> analysisData,
        AnomalyResults anomalyResults)
    {
        try
        {
            // Get AI settings from database
            _logger.LogDebug("Fetching AI settings from database");
            var settings = await _context.SystemSettings
                .Where(s => s.Key.StartsWith("ai_"))
                .ToDictionaryAsync(s => s.Key, s => s.Value);
            
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

    private string GenerateExplanation(
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

    private string GenerateRecommendation(AnomalyResults results, string entityType)
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

    #endregion

    #region Helper Classes

    private class BehaviorMetrics
    {
        public int TotalIncidents { get; set; }
        public double MeanIncidentsPerDay { get; set; }
        public double StdDevIncidentsPerDay { get; set; }
        public double AvgSeverity { get; set; }
        public double StdDevSeverity { get; set; }
        public Dictionary<string, int> ChannelCounts { get; set; } = new();
    }

    private class AnomalyResults
    {
        public double IncidentCountZScore { get; set; }
        public double SeverityZScore { get; set; }
        public double ChannelEmailZScore { get; set; }
        public double ChannelWebZScore { get; set; }
        public double ChannelEndpointZScore { get; set; }
    }

    #endregion
}

