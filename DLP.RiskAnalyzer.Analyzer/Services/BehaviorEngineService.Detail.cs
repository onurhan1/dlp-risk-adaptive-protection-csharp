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

    // DTOs moved to DLP.RiskAnalyzer.Analyzer.Models
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
