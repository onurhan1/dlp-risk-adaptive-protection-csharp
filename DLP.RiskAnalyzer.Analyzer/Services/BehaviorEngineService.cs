using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
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
public class BehaviorEngineService : IBehaviorEngineService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IAIAnalysisRepository _aiAnalysisRepository;
    private readonly ILogger<BehaviorEngineService> _logger;
    private readonly IBehaviorMetricsCalculator _metricsCalculator;
    private readonly IBehaviorAIExplanationService _aiExplanationService;

    public BehaviorEngineService(
        IIncidentRepository incidentRepository,
        IAIAnalysisRepository aiAnalysisRepository,
        ILogger<BehaviorEngineService> logger,
        IBehaviorMetricsCalculator metricsCalculator,
        IBehaviorAIExplanationService aiExplanationService)
    {
        _incidentRepository = incidentRepository;
        _aiAnalysisRepository = aiAnalysisRepository;
        _logger = logger;
        _metricsCalculator = metricsCalculator;
        _aiExplanationService = aiExplanationService;
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
            var currentIncidents = await _incidentRepository.GetIncidentsForEntityAsync(entityType, entityId, startDate, endDate);
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
                
                baselineIncidents = await _incidentRepository.GetIncidentsForEntityAsync(entityType, entityId, baselineStartDate, baselineEndDate);
                
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

            // Calculate enhanced metrics (consistent with all analysis methods)
            _logger.LogDebug("Calculating metrics for {EntityType}: {EntityId}", entityType, entityId);
            var currentMetrics = _metricsCalculator.CalculateEnhancedMetrics(currentIncidents);
            var baselineMetrics = _metricsCalculator.CalculateEnhancedMetrics(baselineIncidents);

            // Calculate ALL Z-scores (consistent with detailed analysis)
            var zScores = _metricsCalculator.CalculateAllZScores(currentMetrics, baselineMetrics, currentIncidents, baselineIncidents);

            // Calculate enhanced risk score with threat profile multiplier
            var baseRiskScore = _metricsCalculator.CalculateEnhancedRiskScore(zScores);
            var threatMultiplier = _metricsCalculator.CalculateThreatProfileMultiplier(currentIncidents);
            var riskScore = (int)Math.Round(baseRiskScore * threatMultiplier);
            riskScore = Math.Clamp(riskScore, 0, 100);

            // Determine anomaly level
            var anomalyLevel = _metricsCalculator.DetermineAnomalyLevel(riskScore);

            // Get reference incident IDs
            var referenceIncidentIds = currentIncidents
                .Where(i => i.RiskScore >= BehaviorThresholds.HighRiskIncidentMinScore || i.Severity >= BehaviorThresholds.HighSeverityMin)
                .Select(i => i.Id)
                .Distinct()
                .Take(BehaviorThresholds.ReferenceIncidentLimit)
                .ToList();

            // Build metadata with all info
            var metadata = new Dictionary<string, object>
            {
                { "current_period_days", lookbackDays },
                { "baseline_period_days", actualBaselineDays },
                { "baseline_mode", isBaselineInsufficient ? "split_period" : "historical" },
                { "current_incident_count", currentMetrics.TotalIncidents },
                { "baseline_incident_count", baselineMetrics.TotalIncidents },
                { "baseline_mean_incidents", Math.Round(baselineMetrics.MeanIncidentsPerDay, 2) },
                { "baseline_std_incidents", Math.Round(baselineMetrics.StdDevIncidentsPerDay, 2) },
                { "current_mean_incidents", Math.Round(currentMetrics.MeanIncidentsPerDay, 2) },
                { "current_avg_severity", Math.Round(currentMetrics.AvgSeverity, 2) },
                { "baseline_avg_severity", Math.Round(baselineMetrics.AvgSeverity, 2) },
                { "threat_multiplier", Math.Round(threatMultiplier, 2) },
                { "risk_score", riskScore }
            };
            
            // Add all Z-scores to metadata
            foreach (var zs in zScores)
            {
                metadata[$"z_score_{zs.Key}"] = Math.Round(zs.Value, 2);
            }
            
            // Create anomaly results for explanation generation
            var anomalyResults = new AnomalyResults
            {
                IncidentCountZScore = zScores.GetValueOrDefault("incident_count", 0),
                SeverityZScore = zScores.GetValueOrDefault("severity", 0),
                ChannelEmailZScore = zScores.GetValueOrDefault("channel_email", 0),
                ChannelWebZScore = zScores.GetValueOrDefault("channel_web", 0),
                ChannelEndpointZScore = zScores.GetValueOrDefault("channel_endpoint", 0),
                ActionBlockZScore = zScores.GetValueOrDefault("action_block", 0),
                MaxMatchesZScore = zScores.GetValueOrDefault("max_matches", 0)
            };

            // Generate AI explanation and recommendation using selected model (or fallback to static)
            string explanation;
            string recommendation;
            
            try
            {
                var (aiExplanation, aiRecommendation) = await _aiExplanationService.GenerateAIAnalysisAsync(
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
                explanation = _aiExplanationService.GenerateExplanation(entityType, entityId, currentMetrics, baselineMetrics, anomalyResults);
                recommendation = _aiExplanationService.GenerateRecommendation(anomalyResults, entityType);
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
    /// Analyze all entities and return overview - OPTIMIZED with parallel processing and batch queries
    /// </summary>
    public async Task<AIBehavioralOverviewResponse> AnalyzeOverviewAsync(int lookbackDays = 7)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting AnalyzeOverviewAsync with lookbackDays={LookbackDays}", lookbackDays);
        
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-lookbackDays);
        var baselineStartDate = startDate.AddDays(-lookbackDays * 2); // 2x baseline period

        // BATCH QUERY: Get ALL incidents in one query
        _logger.LogDebug("Fetching all incidents from {StartDate} to {EndDate}", baselineStartDate, endDate);
        var allIncidents = await _incidentRepository.GetIncidentsByDateRangeAsync(baselineStartDate, endDate);
        
        _logger.LogInformation("Loaded {Count} incidents in {ElapsedMs}ms", allIncidents.Count, stopwatch.ElapsedMilliseconds);

        // Split into current and baseline periods
        var currentIncidents = allIncidents.Where(i => i.Timestamp >= startDate).ToList();
        var baselineIncidents = allIncidents.Where(i => i.Timestamp < startDate).ToList();

        // Extract unique entities
        var users = currentIncidents.Select(i => i.UserEmail).Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
        var channels = currentIncidents.Select(i => i.Channel).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList()!;
        var departments = currentIncidents.Select(i => i.Department).Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList()!;
        var destinations = currentIncidents.Select(i => i.Destination).Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList()!;
        
        // Extract rules from ViolationTriggers
        var ruleNames = new HashSet<string>();
        foreach (var incident in currentIncidents.Where(i => !string.IsNullOrEmpty(i.ViolationTriggers)))
        {
            try
            {
                var triggers = System.Text.Json.JsonSerializer.Deserialize<List<ViolationTriggerDto>>(incident.ViolationTriggers!, JsonOptions);
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

        _logger.LogInformation("Found entities: {Users} users, {Channels} channels, {Departments} depts, {Destinations} dests, {Rules} rules",
            users.Count, channels.Count, departments.Count, destinations.Count, ruleNames.Count);

        // PARALLEL ANALYSIS: Analyze all entities in parallel using in-memory data
        var userTasks = users.Select(user => AnalyzeEntityLightweightAsync("user", user, currentIncidents, baselineIncidents, lookbackDays));
        var channelTasks = channels.Select(channel => AnalyzeEntityLightweightAsync("channel", channel!, currentIncidents, baselineIncidents, lookbackDays));
        var departmentTasks = departments.Select(dept => AnalyzeEntityLightweightAsync("department", dept!, currentIncidents, baselineIncidents, lookbackDays));
        var destinationTasks = destinations.Select(dest => AnalyzeEntityLightweightAsync("destination", dest!, currentIncidents, baselineIncidents, lookbackDays));
        var ruleTasks = ruleNames.Select(rule => AnalyzeEntityLightweightAsync("rule", rule, currentIncidents, baselineIncidents, lookbackDays));

        // Wait for all analyses to complete in parallel
        var userResults = await Task.WhenAll(userTasks);
        var channelResults = await Task.WhenAll(channelTasks);
        var departmentResults = await Task.WhenAll(departmentTasks);
        var destinationResults = await Task.WhenAll(destinationTasks);
        var ruleResults = await Task.WhenAll(ruleTasks);

        var userAnalyses = userResults.Where(r => r != null).ToList()!;
        var channelAnalyses = channelResults.Where(r => r != null).ToList()!;
        var departmentAnalyses = departmentResults.Where(r => r != null).ToList()!;
        var destinationAnalyses = destinationResults.Where(r => r != null).ToList()!;
        var ruleAnalyses = ruleResults.Where(r => r != null).ToList()!;

        _logger.LogInformation("Parallel analysis completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        // GENERATE AI EXPLANATIONS for HIGH and MEDIUM risk entities only (parallel)
        var highMediumEntities = userAnalyses.Concat(channelAnalyses).Concat(departmentAnalyses)
            .Concat(destinationAnalyses).Concat(ruleAnalyses)
            .Where(a => a.AnomalyLevel == "high" || a.AnomalyLevel == "medium")
            .Take(BehaviorThresholds.MaxAICallEntities) // Limit AI calls
            .ToList();

        if (highMediumEntities.Count > 0)
        {
            _logger.LogInformation("Generating AI explanations for {Count} high/medium risk entities", highMediumEntities.Count);
            var aiTasks = highMediumEntities.Select(async entity =>
            {
                try
                {
                    var metadata = entity.AnalysisMetadata ?? new Dictionary<string, object>();
                    var anomalyResults = new AnomalyResults
                    {
                        IncidentCountZScore = metadata.TryGetValue("z_score_incident_count", out var zInc) ? Convert.ToDouble(zInc) : 0,
                        SeverityZScore = metadata.TryGetValue("z_score_severity", out var zSev) ? Convert.ToDouble(zSev) : 0,
                        ChannelEmailZScore = metadata.TryGetValue("z_score_channel_email", out var zEmail) ? Convert.ToDouble(zEmail) : 0,
                        ChannelWebZScore = metadata.TryGetValue("z_score_channel_web", out var zWeb) ? Convert.ToDouble(zWeb) : 0,
                        ChannelEndpointZScore = metadata.TryGetValue("z_score_channel_endpoint", out var zEnd) ? Convert.ToDouble(zEnd) : 0
                    };
                    
                    var (explanation, recommendation) = await _aiExplanationService.GenerateAIAnalysisAsync(
                        entity.EntityType, entity.EntityId, metadata, anomalyResults);
                    entity.AIExplanation = explanation;
                    entity.AIRecommendation = recommendation;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate AI for {EntityType}:{EntityId}", entity.EntityType, entity.EntityId);
                }
            });
            await Task.WhenAll(aiTasks);
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
            .Take(BehaviorThresholds.MaxAICallEntities)
            .ToList();

        var anomalyByChannel = channelAnalyses
            .GroupBy(a => a.EntityId)
            .ToDictionary(g => g.Key, g => g.Count(a => a.AnomalyLevel == "high" || a.AnomalyLevel == "medium"));

        var anomalyByDepartment = departmentAnalyses
            .GroupBy(a => a.EntityId)
            .ToDictionary(g => g.Key, g => g.Count(a => a.AnomalyLevel == "high" || a.AnomalyLevel == "medium"));

        stopwatch.Stop();
        _logger.LogInformation("AnalyzeOverviewAsync completed in {ElapsedMs}ms. Analyzed {Count} entities.", 
            stopwatch.ElapsedMilliseconds, allAnalyses.Count);

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
            UniqueUsers = users.Where(u => !string.IsNullOrEmpty(u)).OrderBy(u => u).ToList()!,
            UniqueChannels = channels.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).OrderBy(c => c).ToList(),
            UniqueDepartments = departments.Where(d => !string.IsNullOrEmpty(d)).Select(d => d!).OrderBy(d => d).ToList(),
            UniqueDestinations = destinations.Where(d => !string.IsNullOrEmpty(d)).Select(d => d!).OrderBy(d => d).ToList(),
            UniqueRules = ruleNames.OrderBy(r => r).ToList(),
            
            // Backward compatibility
            TopAnomalies = topAnomalies,
            AnomalyByChannel = anomalyByChannel,
            AnomalyByDepartment = anomalyByDepartment
        };
    }

    /// <summary>
    /// Lightweight entity analysis using pre-loaded in-memory data (no additional DB queries)
    /// Uses the same enhanced risk calculation as detailed analysis for consistency
    /// </summary>
    private Task<AIBehavioralAnalysisResponse> AnalyzeEntityLightweightAsync(
        string entityType,
        string entityId,
        List<Incident> allCurrentIncidents,
        List<Incident> allBaselineIncidents,
        int lookbackDays)
    {
        return Task.Run(() =>
        {
            try
            {
                // Filter incidents for this entity from in-memory data
                var currentIncidents = FilterIncidentsForEntity(entityType, entityId, allCurrentIncidents);
                var baselineIncidents = FilterIncidentsForEntity(entityType, entityId, allBaselineIncidents);

                if (currentIncidents.Count == 0 && baselineIncidents.Count == 0)
                {
                    return new AIBehavioralAnalysisResponse
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        RiskScore = 0,
                        AnomalyLevel = "low",
                        AIExplanation = $"No incidents found for {entityType} '{entityId}'.",
                        AIRecommendation = "No action required.",
                        ReferenceIncidentIds = new List<int>(),
                        AnalysisMetadata = new Dictionary<string, object>(),
                        AnalysisDate = DateTime.UtcNow
                    };
                }

                // Use enhanced metrics calculation (same as detailed analysis)
                var currentMetrics = _metricsCalculator.CalculateEnhancedMetrics(currentIncidents);
                var baselineMetrics = _metricsCalculator.CalculateEnhancedMetrics(baselineIncidents);

                // Calculate ALL Z-scores (same as detailed analysis)
                var zScores = _metricsCalculator.CalculateAllZScores(currentMetrics, baselineMetrics, currentIncidents, baselineIncidents);

                // Calculate enhanced risk score (same as detailed analysis)
                var baseRiskScore = _metricsCalculator.CalculateEnhancedRiskScore(zScores);
                var threatMultiplier = _metricsCalculator.CalculateThreatProfileMultiplier(currentIncidents);
                var riskScore = (int)Math.Round(baseRiskScore * threatMultiplier);
                riskScore = Math.Clamp(riskScore, 0, 100);
                var anomalyLevel = _metricsCalculator.DetermineAnomalyLevel(riskScore);

                // Get reference incident IDs
                var referenceIncidentIds = currentIncidents
                    .Where(i => i.RiskScore >= BehaviorThresholds.HighRiskIncidentMinScore || i.Severity >= BehaviorThresholds.HighSeverityMin)
                    .Select(i => i.Id)
                    .Distinct()
                    .Take(BehaviorThresholds.ReferenceIncidentLimit)
                    .ToList();

                // Build metadata with all Z-scores
                var metadata = new Dictionary<string, object>
                {
                    { "current_period_days", lookbackDays },
                    { "current_incident_count", currentMetrics.TotalIncidents },
                    { "baseline_incident_count", baselineMetrics.TotalIncidents },
                    { "threat_multiplier", Math.Round(threatMultiplier, 2) }
                };
                
                // Add all Z-scores to metadata
                foreach (var zs in zScores)
                {
                    metadata[$"z_score_{zs.Key}"] = Math.Round(zs.Value, 2);
                }

                // Generate explanation based on anomaly results
                var anomalyResults = new AnomalyResults
                {
                    IncidentCountZScore = zScores.GetValueOrDefault("incident_count", 0),
                    SeverityZScore = zScores.GetValueOrDefault("severity", 0),
                    ChannelEmailZScore = zScores.GetValueOrDefault("channel_email", 0),
                    ChannelWebZScore = zScores.GetValueOrDefault("channel_web", 0),
                    ChannelEndpointZScore = zScores.GetValueOrDefault("channel_endpoint", 0),
                    ActionBlockZScore = zScores.GetValueOrDefault("action_block", 0),
                    MaxMatchesZScore = zScores.GetValueOrDefault("max_matches", 0)
                };
                
                var explanation = _aiExplanationService.GenerateExplanation(entityType, entityId, currentMetrics, baselineMetrics, anomalyResults);
                var recommendation = _aiExplanationService.GenerateRecommendation(anomalyResults, entityType);

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
                    AnalysisDate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze {EntityType}: {EntityId}", entityType, entityId);
                return null!;
            }
        });
    }

    /// <summary>
    /// Filter incidents for a specific entity from in-memory list
    /// </summary>
    private List<Incident> FilterIncidentsForEntity(string entityType, string entityId, List<Incident> incidents)
    {
        return entityType.ToLower() switch
        {
            "user" => incidents.Where(i => i.UserEmail == entityId).ToList(),
            "channel" => incidents.Where(i => i.Channel == entityId).ToList(),
            "department" => incidents.Where(i => i.Department == entityId).ToList(),
            "destination" => incidents.Where(i => i.Destination == entityId).ToList(),
            "policy" => incidents.Where(i => i.Policy == entityId).ToList(),
            "rule" => incidents.Where(i =>
            {
                if (string.IsNullOrEmpty(i.ViolationTriggers)) return false;
                try
                {
                    var triggers = System.Text.Json.JsonSerializer.Deserialize<List<ViolationTriggerDto>>(i.ViolationTriggers, JsonOptions);
                    return triggers?.Any(t => t.RuleName == entityId) == true;
                }
                catch { return false; }
            }).ToList(),
            _ => new List<Incident>()
        };
    }

    /// <summary>
    /// Save analysis result to database
    /// </summary>
    public async Task SaveAnalysisAsync(AIBehavioralAnalysisResponse response)
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
            AnalysisMetadata = JsonSerializer.Serialize(response.AnalysisMetadata)
        };
        
        await _aiAnalysisRepository.SaveAnalysisAsync(analysis);
    }

    
    // DTO for parsing ViolationTriggers JSON (supports PascalCase from DB)
    private class ViolationTriggerDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("policy_name")]
        public string? PolicyNameSnake { get; set; }
        public string? PolicyName { get; set; } // Matches PascalCase or camelCase with options
        
        public string? EffectivePolicyName => PolicyNameSnake ?? PolicyName;

        [System.Text.Json.Serialization.JsonPropertyName("rule_name")]
        public string? RuleNameSnake { get; set; }
        public string? RuleName { get; set; }
        
        public string? EffectiveRuleName => RuleNameSnake ?? RuleName;

        public List<ClassifierDto>? Classifiers { get; set; }
    }

    private class ClassifierDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("classifier_name")]
        public string? ClassifierNameSnake { get; set; }
        public string? ClassifierName { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("number_matches")]
        public int NumberMatchesSnake { get; set; }
        public int NumberMatches { get; set; }
        
        public int EffectiveNumberMatches => NumberMatchesSnake > 0 ? NumberMatchesSnake : NumberMatches;
    }
    
    // JSON options for case-insensitive parsing
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private double CalculateChannelZScore(string channel, BehaviorMetrics current, BehaviorMetrics baseline)
    {
        // Get current and baseline counts for THIS SPECIFIC channel
        var currentCount = current.ChannelCounts.GetValueOrDefault(channel, 0);
        var baselineCount = baseline.ChannelCounts.GetValueOrDefault(channel, 0);
        
        // If no baseline data for this channel, return 0 (can't determine anomaly without baseline)
        if (baselineCount == 0)
        {
            // Only flag as anomaly if current has significant activity AND baseline has no data at all
            // This prevents false positives for new channels
            return 0; // Changed from 2.0 - don't assume anomaly without baseline
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


    /// <summary>
    /// Generate AI explanation and recommendation using the selected model provider
    /// </summary>






    /// <summary>
    /// Analyze entity with full detail including trends and action breakdown
    /// </summary>
    public async Task<AIBehavioralDetailResponse> AnalyzeEntityDetailAsync(
        string entityType,
        string entityId,
        int lookbackDays = 30)
    {
        _logger.LogInformation("Starting detailed analysis for {EntityType}: {EntityId}", entityType, entityId);

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-lookbackDays);
        var baselineStartDate = startDate.AddDays(-lookbackDays * 2);

        // Get all incidents for this entity
        var allIncidents = await _incidentRepository.GetIncidentsForEntityAsync(entityType, entityId, baselineStartDate, endDate);
        var currentIncidents = allIncidents.Where(i => i.Timestamp >= startDate).ToList();
        var baselineIncidents = allIncidents.Where(i => i.Timestamp < startDate).ToList();

        if (currentIncidents.Count == 0)
        {
            return new AIBehavioralDetailResponse
            {
                EntityType = entityType,
                EntityId = entityId,
                RiskScore = 0,
                AnomalyLevel = "low",
                AIExplanation = $"No incidents found for {entityType} '{entityId}' in the analyzed period.",
                AIRecommendation = "No action required.",
                AnalysisDate = endDate
            };
        }

        // Calculate enhanced metrics
        var currentMetrics = _metricsCalculator.CalculateEnhancedMetrics(currentIncidents);
        var baselineMetrics = _metricsCalculator.CalculateEnhancedMetrics(baselineIncidents);

        // Calculate all Z-scores
        var zScores = _metricsCalculator.CalculateAllZScores(currentMetrics, baselineMetrics, currentIncidents, baselineIncidents);

        // Calculate base risk score from all Z-scores
        var baseRiskScore = _metricsCalculator.CalculateEnhancedRiskScore(zScores);
        
        // Apply threat profile multiplier based on per-incident threat analysis
        // Each incident is weighted by: action_type × severity × log(1 + maxMatches)
        var threatMultiplier = _metricsCalculator.CalculateThreatProfileMultiplier(currentIncidents);
        var riskScore = (int)Math.Round(baseRiskScore * threatMultiplier);
        riskScore = Math.Clamp(riskScore, 0, 100); // Ensure within bounds
        
        var anomalyLevel = _metricsCalculator.DetermineAnomalyLevel(riskScore);

        // Generate weekly trends
        var weeklyTrends = _metricsCalculator.GenerateWeeklyTrends(allIncidents, lookbackDays);

        // Generate monthly trends
        var monthlyTrends = _metricsCalculator.GenerateMonthlyTrends(allIncidents);

        // Get destination patterns (for users)
        var destinationPatterns = _metricsCalculator.GetDestinationPatterns(currentIncidents, baselineIncidents);

        // Calculate destination diversity
        var uniqueDestinations = currentIncidents.Where(i => !string.IsNullOrEmpty(i.Destination))
            .Select(i => i.Destination).Distinct().Count();
        var destinationDiversity = currentIncidents.Count > 0 
            ? (double)uniqueDestinations / currentIncidents.Count 
            : 0;

        // Get top incidents - Calculate MaxMatches from ViolationTriggers if database value is 0
        var topIncidents = currentIncidents
            .OrderByDescending(i => _metricsCalculator.GetEffectiveMaxMatches(i))
            .ThenByDescending(i => i.Severity)
            .Take(20)
            .Select(i => new IncidentSummary
            {
                Id = i.Id,
                LoginName = i.LoginName ?? i.UserEmail ?? "N/A",
                Destination = i.Destination ?? "N/A",
                Channel = i.Channel ?? "N/A",
                Action = i.Action ?? "N/A",
                MaxMatches = _metricsCalculator.GetEffectiveMaxMatches(i),
                Timestamp = i.Timestamp
            })
            .ToList();

        // Generate AI explanation
        string explanation, recommendation;
        try
        {
            var metadata = new Dictionary<string, object>
            {
                { "current_incident_count", currentMetrics.TotalIncidents },
                { "baseline_incident_count", baselineMetrics.TotalIncidents },
                { "z_score_incident_count", zScores["incident_count"] },
                { "z_score_action_block", zScores["action_block"] },
                { "z_score_max_matches", zScores["max_matches"] }
            };
            var anomalyResults = new AnomalyResults
            {
                IncidentCountZScore = zScores["incident_count"],
                ActionBlockZScore = zScores["action_block"],
                MaxMatchesZScore = zScores["max_matches"]
            };
            (explanation, recommendation) = await _aiExplanationService.GenerateAIAnalysisAsync(entityType, entityId, metadata, anomalyResults);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI analysis for {EntityType}:{EntityId}, using fallback explanation", entityType, entityId);
            explanation = $"Analysis shows {anomalyLevel} risk level with {currentMetrics.TotalIncidents} incidents.";
            recommendation = anomalyLevel == "high" 
                ? "Immediate investigation recommended." 
                : "Continue monitoring.";
        }

        return new AIBehavioralDetailResponse
        {
            EntityType = entityType,
            EntityId = entityId,
            RiskScore = riskScore,
            AnomalyLevel = anomalyLevel,
            AIExplanation = explanation,
            AIRecommendation = recommendation,
            ReferenceIncidentIds = currentIncidents.Take(BehaviorThresholds.ReferenceIncidentLimit).Select(i => i.Id).ToList(),
            AnalysisDate = endDate,
            ZScores = zScores,
            ZScoreDetails = new Dictionary<string, ZScoreDetail>
            {
                ["incident_count"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("incident_count", 0),
                    CurrentValue = currentMetrics.MeanIncidentsPerDay,
                    Mean = baselineMetrics.MeanIncidentsPerDay,
                    StdDev = baselineMetrics.StdDevIncidentsPerDay,
                    BaselineValue = baselineMetrics.MeanIncidentsPerDay,
                    Formula = "Z = (Current - Mean) / StdDev"
                },
                ["severity"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("severity", 0),
                    CurrentValue = currentMetrics.AvgSeverity,
                    Mean = baselineMetrics.AvgSeverity,
                    StdDev = baselineMetrics.StdDevSeverity,
                    BaselineValue = baselineMetrics.AvgSeverity,
                    Formula = "Z = (Current - Mean) / StdDev"
                },
                ["channel_email"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("channel_email", 0),
                    CurrentValue = currentMetrics.ChannelCounts.GetValueOrDefault("Email", 0),
                    Mean = baselineMetrics.ChannelCounts.GetValueOrDefault("Email", 0),
                    StdDev = baselineMetrics.ChannelCounts.Count > 1 ? Math.Sqrt(baselineMetrics.ChannelCounts.Values.Sum(x => Math.Pow(x - baselineMetrics.ChannelCounts.Values.Average(), 2)) / (baselineMetrics.ChannelCounts.Count - 1)) : 1,
                    BaselineValue = baselineMetrics.ChannelCounts.GetValueOrDefault("Email", 0),
                    Formula = "Z = (Current - Baseline) / StdDev"
                },
                ["channel_web"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("channel_web", 0),
                    CurrentValue = currentMetrics.ChannelCounts.GetValueOrDefault("Web", 0),
                    Mean = baselineMetrics.ChannelCounts.GetValueOrDefault("Web", 0),
                    StdDev = baselineMetrics.ChannelCounts.Count > 1 ? Math.Sqrt(baselineMetrics.ChannelCounts.Values.Sum(x => Math.Pow(x - baselineMetrics.ChannelCounts.Values.Average(), 2)) / (baselineMetrics.ChannelCounts.Count - 1)) : 1,
                    BaselineValue = baselineMetrics.ChannelCounts.GetValueOrDefault("Web", 0),
                    Formula = "Z = (Current - Baseline) / StdDev"
                },
                ["channel_endpoint"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("channel_endpoint", 0),
                    CurrentValue = currentMetrics.ChannelCounts.GetValueOrDefault("Endpoint", 0),
                    Mean = baselineMetrics.ChannelCounts.GetValueOrDefault("Endpoint", 0),
                    StdDev = baselineMetrics.ChannelCounts.Count > 1 ? Math.Sqrt(baselineMetrics.ChannelCounts.Values.Sum(x => Math.Pow(x - baselineMetrics.ChannelCounts.Values.Average(), 2)) / (baselineMetrics.ChannelCounts.Count - 1)) : 1,
                    BaselineValue = baselineMetrics.ChannelCounts.GetValueOrDefault("Endpoint", 0),
                    Formula = "Z = (Current - Baseline) / StdDev"
                },
                ["action_block"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("action_block", 0),
                    CurrentValue = currentMetrics.ActionCounts.GetValueOrDefault("BLOCK", 0) + currentMetrics.ActionCounts.GetValueOrDefault("BLOCKED", 0),
                    Mean = baselineMetrics.ActionCounts.GetValueOrDefault("BLOCK", 0) + baselineMetrics.ActionCounts.GetValueOrDefault("BLOCKED", 0),
                    StdDev = Math.Max(1, (baselineMetrics.ActionCounts.GetValueOrDefault("BLOCK", 0) + baselineMetrics.ActionCounts.GetValueOrDefault("BLOCKED", 0)) * 0.3),
                    BaselineValue = baselineMetrics.ActionCounts.GetValueOrDefault("BLOCK", 0) + baselineMetrics.ActionCounts.GetValueOrDefault("BLOCKED", 0),
                    Formula = "Z = (Current - Baseline) / StdDev × 1.0 (high-threat weight)"
                },
                ["action_authorized"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("action_authorized", 0),
                    CurrentValue = currentMetrics.ActionCounts.GetValueOrDefault("AUTHORIZED", 0),
                    Mean = baselineMetrics.ActionCounts.GetValueOrDefault("AUTHORIZED", 0),
                    StdDev = Math.Max(1, baselineMetrics.ActionCounts.GetValueOrDefault("AUTHORIZED", 0) * 0.3),
                    BaselineValue = baselineMetrics.ActionCounts.GetValueOrDefault("AUTHORIZED", 0),
                    Formula = "Z = (Current - Baseline) / StdDev × 0.2 (low-threat weight)"
                },
                ["max_matches"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("max_matches", 0),
                    CurrentValue = currentMetrics.AvgMatchesPerIncident,
                    Mean = baselineMetrics.AvgMatchesPerIncident,
                    StdDev = baselineMetrics.StdDevMatches,
                    BaselineValue = baselineMetrics.AvgMatchesPerIncident,
                    Formula = "Z = (Current - Mean) / StdDev"
                },
                ["weekly_trend"] = new ZScoreDetail
                {
                    ZScore = zScores.GetValueOrDefault("weekly_trend", 0),
                    CurrentValue = currentIncidents.Count(i => i.Timestamp >= DateTime.UtcNow.AddDays(-7)),
                    Mean = currentIncidents.Count(i => i.Timestamp >= DateTime.UtcNow.AddDays(-14) && i.Timestamp < DateTime.UtcNow.AddDays(-7)),
                    StdDev = 0.5,
                    BaselineValue = currentIncidents.Count(i => i.Timestamp >= DateTime.UtcNow.AddDays(-14) && i.Timestamp < DateTime.UtcNow.AddDays(-7)),
                    Formula = "Z = log(1 + growthRate) / 0.5 (weekly comparison)"
                }
            },
            WeeklyTrends = weeklyTrends,
            MonthlyTrends = monthlyTrends,
            ActionCounts = currentMetrics.ActionCounts,
            ActionZScores = new Dictionary<string, double>
            {
                { "BLOCK", zScores.GetValueOrDefault("action_block", 0) },
                { "QUARANTINE", zScores.GetValueOrDefault("action_quarantine", 0) },
                { "AUTHORIZED", zScores.GetValueOrDefault("action_authorized", 0) },
                { "RELEASED", zScores.GetValueOrDefault("action_released", 0) }
            },
            TotalIncidents = currentMetrics.TotalIncidents,
            TotalMatches = currentMetrics.TotalMatches,
            AvgMatchesPerIncident = currentMetrics.AvgMatchesPerIncident,
            DestinationPatterns = destinationPatterns,
            DestinationDiversity = Math.Round(destinationDiversity, 2),
            TopIncidents = topIncidents
        };
    }





}

