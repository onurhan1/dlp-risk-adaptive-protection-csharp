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
public partial class BehaviorEngineService : IBehaviorEngineService
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

}
