using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;


public partial class BehaviorEngineService
{
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

}
