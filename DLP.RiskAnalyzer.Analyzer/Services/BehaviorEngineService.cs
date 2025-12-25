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
    /// Uses adaptive baseline selection - expands baseline window if insufficient data
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
            
            // Get current period incidents
            _logger.LogDebug("Fetching current period incidents for {EntityType}: {EntityId} from {StartDate} to {EndDate}", 
                entityType, entityId, startDate, endDate);
            var currentIncidents = await GetIncidentsForEntityAsync(entityType, entityId, startDate, endDate);
            _logger.LogInformation("Found {Count} current period incidents for {EntityType}: {EntityId}", 
                currentIncidents.Count, entityType, entityId);
            
            // ADAPTIVE BASELINE SELECTION
            // Try to find baseline data, expanding the window up to 4x lookback if needed
            List<Incident> baselineIncidents = new();
            DateTime baselineStartDate;
            DateTime baselineEndDate = startDate;
            int actualBaselineDays = lookbackDays;
            int maxMultiplier = 4; // Look back up to 4x the lookback period
            
            for (int multiplier = 1; multiplier <= maxMultiplier; multiplier++)
            {
                actualBaselineDays = lookbackDays * multiplier;
                baselineStartDate = startDate.AddDays(-actualBaselineDays);
                
                _logger.LogDebug("Trying baseline period: {BaselineStartDate} to {BaselineEndDate} ({Days} days)", 
                    baselineStartDate, baselineEndDate, actualBaselineDays);
                
                baselineIncidents = await GetIncidentsForEntityAsync(entityType, entityId, baselineStartDate, baselineEndDate);
                
                // If we have at least 30% of current incidents or at least 5 incidents, use this baseline
                var minRequired = Math.Max(1, (int)(currentIncidents.Count * 0.3));
                if (baselineIncidents.Count >= minRequired || baselineIncidents.Count >= 5)
                {
                    _logger.LogInformation("Found sufficient baseline data: {Count} incidents in {Days}-day window", 
                        baselineIncidents.Count, actualBaselineDays);
                    break;
                }
                
                // If we've reached max multiplier, use whatever we found
                if (multiplier == maxMultiplier)
                {
                    _logger.LogWarning("Baseline data insufficient even after expanding to {Days} days. Using available {Count} incidents.", 
                        actualBaselineDays, baselineIncidents.Count);
                }
            }
            
            // Calculate actual baseline start date
            baselineStartDate = startDate.AddDays(-actualBaselineDays);

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
                    AnalysisMetadata = new Dictionary<string, object>
                    {
                        { "analysis_note", "No data available for analysis" }
                    },
                    AnalysisDate = endDate
                };
            }
            
            // Handle case where we have current data but no baseline
            bool isBaselineInsufficient = baselineIncidents.Count == 0;
            if (isBaselineInsufficient && currentIncidents.Count > 0)
            {
                // Use current period split in half as pseudo-baseline
                var halfIndex = currentIncidents.Count / 2;
                baselineIncidents = currentIncidents.Take(halfIndex).ToList();
                currentIncidents = currentIncidents.Skip(halfIndex).ToList();
                _logger.LogInformation("No baseline data available. Using split-period analysis with {Current} current and {Baseline} baseline incidents", 
                    currentIncidents.Count, baselineIncidents.Count);
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
                { "current_period_days", lookbackDays },
                { "baseline_period_days", actualBaselineDays },
                { "baseline_mode", isBaselineInsufficient ? "split_period" : "historical" },
                { "current_incident_count", currentMetrics.TotalIncidents },
                { "baseline_incident_count", baselineMetrics.TotalIncidents },
                { "z_score_incident_count", Math.Round(anomalyResults.IncidentCountZScore, 2) },
                { "z_score_severity", Math.Round(anomalyResults.SeverityZScore, 2) },
                { "z_score_channel_email", Math.Round(anomalyResults.ChannelEmailZScore, 2) },
                { "z_score_channel_web", Math.Round(anomalyResults.ChannelWebZScore, 2) },
                { "z_score_channel_endpoint", Math.Round(anomalyResults.ChannelEndpointZScore, 2) },
                { "baseline_mean_incidents", Math.Round(baselineMetrics.MeanIncidentsPerDay, 2) },
                { "baseline_std_incidents", Math.Round(baselineMetrics.StdDevIncidentsPerDay, 2) },
                { "current_mean_incidents", Math.Round(currentMetrics.MeanIncidentsPerDay, 2) },
                { "current_avg_severity", Math.Round(currentMetrics.AvgSeverity, 2) },
                { "baseline_avg_severity", Math.Round(baselineMetrics.AvgSeverity, 2) },
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
        foreach (var user in users) // Analyze all users
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

        // Analyze destinations
        var destinations = await _context.Incidents
            .Where(i => i.Timestamp >= startDate && !string.IsNullOrEmpty(i.Destination))
            .Select(i => i.Destination!)
            .Distinct()
            .ToListAsync();

        var destinationAnalyses = new List<AIBehavioralAnalysisResponse>();
        foreach (var destination in destinations)
        {
            try
            {
                var analysis = await AnalyzeEntityAsync("destination", destination, lookbackDays);
                destinationAnalyses.Add(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze destination {Destination}", destination);
            }
        }

        // Analyze rules (extracted from ViolationTriggers JSON)
        var allViolationTriggers = await _context.Incidents
            .Where(i => i.Timestamp >= startDate && !string.IsNullOrEmpty(i.ViolationTriggers))
            .Select(i => i.ViolationTriggers!)
            .ToListAsync();
        
        var ruleNames = new HashSet<string>();
        foreach (var triggersJson in allViolationTriggers)
        {
            try
            {
                var triggers = System.Text.Json.JsonSerializer.Deserialize<List<ViolationTriggerDto>>(triggersJson, JsonOptions);
                if (triggers != null)
                {
                    foreach (var trigger in triggers.Where(t => !string.IsNullOrEmpty(t.RuleName)))
                    {
                        ruleNames.Add(trigger.RuleName!);
                    }
                }
            }
            catch { /* Skip invalid JSON */ }
        }

        var ruleAnalyses = new List<AIBehavioralAnalysisResponse>();
        foreach (var ruleName in ruleNames) // Analyze all rules
        {
            try
            {
                var analysis = await AnalyzeEntityAsync("rule", ruleName, lookbackDays);
                ruleAnalyses.Add(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze rule {Rule}", ruleName);
            }
        }

        var allAnalyses = userAnalyses
            .Concat(channelAnalyses)
            .Concat(departmentAnalyses)
            .Concat(destinationAnalyses)
            .Concat(ruleAnalyses)
            .ToList();

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
            
            // Entity-specific arrays (sorted by risk score descending)
            UserAnomalies = userAnalyses.OrderByDescending(a => a.RiskScore).ToList(),
            ChannelAnomalies = channelAnalyses.OrderByDescending(a => a.RiskScore).ToList(),
            DepartmentAnomalies = departmentAnalyses.OrderByDescending(a => a.RiskScore).ToList(),
            DestinationAnomalies = destinationAnalyses.OrderByDescending(a => a.RiskScore).ToList(),
            RuleAnomalies = ruleAnalyses.OrderByDescending(a => a.RiskScore).ToList(),
            
            // Unique values for autocomplete (sorted alphabetically)
            UniqueUsers = users.Where(u => !string.IsNullOrEmpty(u)).OrderBy(u => u).ToList(),
            UniqueChannels = channels.OrderBy(c => c).ToList(),
            UniqueDepartments = departments.OrderBy(d => d).ToList(),
            UniqueDestinations = destinations.OrderBy(d => d).ToList(),
            UniqueRules = ruleNames.OrderBy(r => r).ToList(),
            
            // Backward compatibility
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

            List<Incident> result;
            
            switch (entityType.ToLower())
            {
                case "user":
                    result = await query.Where(i => i.UserEmail == entityId).ToListAsync();
                    break;
                    
                case "channel":
                    result = await query.Where(i => i.Channel == entityId).ToListAsync();
                    break;
                    
                case "department":
                    result = await query.Where(i => i.Department == entityId).ToListAsync();
                    break;
                    
                case "destination":
                    result = await query.Where(i => i.Destination == entityId).ToListAsync();
                    break;
                    
                case "policy":
                    result = await query.Where(i => i.Policy == entityId).ToListAsync();
                    break;
                    
                case "datatype":
                    result = await query.Where(i => i.DataType == entityId).ToListAsync();
                    break;
                    
                case "rule":
                    // Rule is stored in ViolationTriggers JSON field
                    // Search for rule_name in the JSON string
                    var allIncidents = await query
                        .Where(i => !string.IsNullOrEmpty(i.ViolationTriggers) && i.ViolationTriggers.Contains(entityId))
                        .ToListAsync();
                    
                    // Filter more precisely by parsing JSON
                    result = allIncidents.Where(i => 
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(i.ViolationTriggers)) return false;
                            var triggers = System.Text.Json.JsonSerializer.Deserialize<List<ViolationTriggerDto>>(i.ViolationTriggers, JsonOptions);
                            return triggers?.Any(t => t.RuleName == entityId) == true;
                        }
                        catch
                        {
                            return false;
                        }
                    }).ToList();
                    break;
                    
                default:
                    result = new List<Incident>();
                    break;
            }
            
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
    
    // DTO for parsing ViolationTriggers JSON (supports PascalCase from DB)
    private class ViolationTriggerDto
    {
        public string? PolicyName { get; set; }
        public string? RuleName { get; set; }
    }
    
    // JSON options for case-insensitive parsing
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        // Get current and baseline counts for THIS SPECIFIC channel
        var currentCount = current.ChannelCounts.GetValueOrDefault(channel, 0);
        var baselineCount = baseline.ChannelCounts.GetValueOrDefault(channel, 0);
        
        // If no baseline data for this channel, can't calculate meaningful z-score
        if (baselineCount == 0)
        {
            // If current has activity but baseline doesn't, this is notable
            return currentCount > 0 ? 2.0 : 0; // Default anomaly score for new activity
        }
        
        // Calculate std dev from all channels as a measure of typical variation
        var baselineStd = baseline.ChannelCounts.Count > 1
            ? Math.Sqrt(baseline.ChannelCounts.Values.Sum(x => Math.Pow(x - baseline.ChannelCounts.Values.Average(), 2)) / (baseline.ChannelCounts.Count - 1))
            : Math.Max(1, baselineCount * 0.3); // Use 30% of baseline as estimated std if only one channel
        
        // Ensure minimum std dev to avoid division issues
        baselineStd = Math.Max(baselineStd, 1);
        
        // Z-score: compare current channel against ITS OWN baseline
        return (currentCount - baselineCount) / baselineStd;
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

