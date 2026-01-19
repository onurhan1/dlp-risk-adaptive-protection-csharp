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
        var allIncidents = await _context.Incidents
            .Where(i => i.Timestamp >= baselineStartDate && i.Timestamp < endDate)
            .AsNoTracking()
            .ToListAsync();
        
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
            .Take(20) // Limit AI calls to top 20
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
                    
                    var (explanation, recommendation) = await GenerateAIAnalysisAsync(
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
            .Take(20)
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

                // Calculate metrics
                var currentMetrics = CalculateMetrics(currentIncidents);
                var baselineMetrics = CalculateMetrics(baselineIncidents);

                // Detect anomalies
                var anomalyResults = DetectAnomalies(currentMetrics, baselineMetrics);

                // Calculate risk score
                var riskScore = CalculateRiskScore(anomalyResults);
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
                    { "current_incident_count", currentMetrics.TotalIncidents },
                    { "baseline_incident_count", baselineMetrics.TotalIncidents },
                    { "z_score_incident_count", Math.Round(anomalyResults.IncidentCountZScore, 2) },
                    { "z_score_severity", Math.Round(anomalyResults.SeverityZScore, 2) },
                    { "z_score_channel_email", Math.Round(anomalyResults.ChannelEmailZScore, 2) },
                    { "z_score_channel_web", Math.Round(anomalyResults.ChannelWebZScore, 2) },
                    { "z_score_channel_endpoint", Math.Round(anomalyResults.ChannelEndpointZScore, 2) }
                };

                // Generate static explanation (AI will be added later for HIGH/MEDIUM)
                var explanation = GenerateExplanation(entityType, entityId, currentMetrics, baselineMetrics, anomalyResults);
                var recommendation = GenerateRecommendation(anomalyResults, entityType);

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
        // Use more granular scoring based on actual z-score values
        if (maxZ >= 4) return 100;
        if (maxZ >= 3) return 85;
        if (maxZ >= 2.5) return 75;
        if (maxZ >= 2) return 65;
        if (maxZ >= 1.5) return 50;
        if (maxZ >= 1) return 40;
        if (maxZ >= 0.5) return 30;
        return 20; // Minimum score for entities with activity
    }

    private string DetermineAnomalyLevel(int riskScore)
    {
        return riskScore switch
        {
            >= 85 => "critical",
            >= 65 => "high",
            >= 40 => "medium",
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
        // New metrics
        public Dictionary<string, int> ActionCounts { get; set; } = new();
        public int TotalMatches { get; set; }
        public double AvgMatchesPerIncident { get; set; }
        public double StdDevMatches { get; set; }
    }

    private class AnomalyResults
    {
        public double IncidentCountZScore { get; set; }
        public double SeverityZScore { get; set; }
        public double ChannelEmailZScore { get; set; }
        public double ChannelWebZScore { get; set; }
        public double ChannelEndpointZScore { get; set; }
        // New action-based Z-scores
        public double ActionBlockZScore { get; set; }
        public double ActionQuarantineZScore { get; set; }
        public double ActionAuthorizedZScore { get; set; }
        public double ActionReleasedZScore { get; set; }
        public double MaxMatchesZScore { get; set; }
        public double WeeklyTrendZScore { get; set; }
    }

    #endregion

    #region Detailed Analysis

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
        var allIncidents = await GetIncidentsForEntityAsync(entityType, entityId, baselineStartDate, endDate);
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
        var currentMetrics = CalculateEnhancedMetrics(currentIncidents);
        var baselineMetrics = CalculateEnhancedMetrics(baselineIncidents);

        // Calculate all Z-scores
        var zScores = CalculateAllZScores(currentMetrics, baselineMetrics, currentIncidents, baselineIncidents);

        // Calculate base risk score from all Z-scores
        var baseRiskScore = CalculateEnhancedRiskScore(zScores);
        
        // Apply threat profile multiplier based on action distribution
        // If most actions are AUTHORIZED/RELEASED (non-threatening), reduce the risk score
        var threatMultiplier = CalculateThreatProfileMultiplier(currentMetrics.ActionCounts);
        var riskScore = (int)Math.Round(baseRiskScore * threatMultiplier);
        riskScore = Math.Clamp(riskScore, 0, 100); // Ensure within bounds
        
        var anomalyLevel = DetermineAnomalyLevel(riskScore);

        // Generate weekly trends
        var weeklyTrends = GenerateWeeklyTrends(allIncidents, lookbackDays);

        // Generate monthly trends
        var monthlyTrends = GenerateMonthlyTrends(allIncidents);

        // Get destination patterns (for users)
        var destinationPatterns = GetDestinationPatterns(currentIncidents, baselineIncidents);

        // Calculate destination diversity
        var uniqueDestinations = currentIncidents.Where(i => !string.IsNullOrEmpty(i.Destination))
            .Select(i => i.Destination).Distinct().Count();
        var destinationDiversity = currentIncidents.Count > 0 
            ? (double)uniqueDestinations / currentIncidents.Count 
            : 0;

        // Get top incidents
        var topIncidents = currentIncidents
            .OrderByDescending(i => i.MaxMatches)
            .ThenByDescending(i => i.Severity)
            .Take(20)
            .Select(i => new IncidentSummary
            {
                Id = i.Id,
                LoginName = i.LoginName ?? i.UserEmail ?? "N/A",
                Destination = i.Destination ?? "N/A",
                Channel = i.Channel ?? "N/A",
                Action = i.Action ?? "N/A",
                MaxMatches = i.MaxMatches,
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
            (explanation, recommendation) = await GenerateAIAnalysisAsync(entityType, entityId, metadata, anomalyResults);
        }
        catch
        {
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
            ReferenceIncidentIds = currentIncidents.Take(10).Select(i => i.Id).ToList(),
            AnalysisDate = endDate,
            ZScores = zScores,
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

    private BehaviorMetrics CalculateEnhancedMetrics(List<Incident> incidents)
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

        // Action counts
        var actionCounts = incidents
            .Where(i => !string.IsNullOrEmpty(i.Action))
            .GroupBy(i => i.Action!.ToUpper())
            .ToDictionary(g => g.Key, g => g.Count());

        // Max matches stats
        var totalMatches = incidents.Sum(i => i.MaxMatches);
        var avgMatches = incidents.Average(i => (double)i.MaxMatches);
        var stdDevMatches = incidents.Count > 1
            ? Math.Sqrt(incidents.Sum(i => Math.Pow(i.MaxMatches - avgMatches, 2)) / (incidents.Count - 1))
            : 0;

        return new BehaviorMetrics
        {
            TotalIncidents = incidents.Count,
            MeanIncidentsPerDay = mean,
            StdDevIncidentsPerDay = stdDev,
            AvgSeverity = avgSeverity,
            StdDevSeverity = severityStdDev,
            ChannelCounts = channelCounts,
            ActionCounts = actionCounts,
            TotalMatches = totalMatches,
            AvgMatchesPerIncident = avgMatches,
            StdDevMatches = stdDevMatches
        };
    }

    private Dictionary<string, double> CalculateAllZScores(
        BehaviorMetrics current, 
        BehaviorMetrics baseline,
        List<Incident> currentIncidents,
        List<Incident> baselineIncidents)
    {
        var zScores = new Dictionary<string, double>();

        // Incident count Z-score
        zScores["incident_count"] = baseline.StdDevIncidentsPerDay > 0
            ? Math.Round((current.MeanIncidentsPerDay - baseline.MeanIncidentsPerDay) / baseline.StdDevIncidentsPerDay, 2)
            : 0;

        // Severity Z-score
        zScores["severity"] = baseline.StdDevSeverity > 0
            ? Math.Round((current.AvgSeverity - baseline.AvgSeverity) / baseline.StdDevSeverity, 2)
            : 0;

        // Channel Z-scores
        zScores["channel_email"] = CalculateChannelZScore("Email", current, baseline);
        zScores["channel_web"] = CalculateChannelZScore("Web", current, baseline);
        zScores["channel_endpoint"] = CalculateChannelZScore("Endpoint", current, baseline);

        // Action Z-scores with impact weights
        // BLOCK and QUARANTINE are high-threat actions (weight: 1.0)
        // AUTHORIZED and RELEASED are low-threat actions (weight: 0.2)
        const double HIGH_THREAT_WEIGHT = 1.0;
        const double LOW_THREAT_WEIGHT = 0.2;
        
        zScores["action_block"] = CalculateActionZScore("BLOCK", current, baseline) * HIGH_THREAT_WEIGHT;
        zScores["action_quarantine"] = CalculateActionZScore("QUARANTINE", current, baseline) * HIGH_THREAT_WEIGHT;
        zScores["action_authorized"] = CalculateActionZScore("AUTHORIZED", current, baseline) * LOW_THREAT_WEIGHT;
        zScores["action_released"] = CalculateActionZScore("RELEASED", current, baseline) * LOW_THREAT_WEIGHT;

        // MaxMatches Z-score
        zScores["max_matches"] = baseline.StdDevMatches > 0
            ? Math.Round((current.AvgMatchesPerIncident - baseline.AvgMatchesPerIncident) / baseline.StdDevMatches, 2)
            : 0;

        // Weekly trend Z-score
        zScores["weekly_trend"] = CalculateWeeklyTrendZScore(currentIncidents);

        return zScores;
    }

    private double CalculateActionZScore(string action, BehaviorMetrics current, BehaviorMetrics baseline)
    {
        var currentCount = current.ActionCounts.GetValueOrDefault(action, 0) +
                          current.ActionCounts.GetValueOrDefault(action + "ED", 0); // BLOCK/BLOCKED
        var baselineCount = baseline.ActionCounts.GetValueOrDefault(action, 0) +
                           baseline.ActionCounts.GetValueOrDefault(action + "ED", 0);

        if (baselineCount == 0) return currentCount > 0 ? 2.0 : 0;

        var baselineStd = Math.Max(1, baselineCount * 0.3);
        return Math.Round((currentCount - baselineCount) / baselineStd, 2);
    }

    private double CalculateWeeklyTrendZScore(List<Incident> incidents)
    {
        if (incidents.Count < 7) return 0;

        var lastWeek = incidents.Where(i => i.Timestamp >= DateTime.UtcNow.AddDays(-7)).Count();
        var prevWeek = incidents.Where(i => i.Timestamp >= DateTime.UtcNow.AddDays(-14) && i.Timestamp < DateTime.UtcNow.AddDays(-7)).Count();

        // No previous week data - can't calculate trend
        if (prevWeek == 0)
        {
            // New activity: moderate signal based on volume
            if (lastWeek >= 20) return 2.5;     // High new activity
            if (lastWeek >= 10) return 1.5;     // Moderate new activity
            if (lastWeek >= 5) return 1.0;      // Some new activity
            return 0;
        }

        // Calculate growth rate with log-scale for large changes
        var rawGrowthRate = (double)(lastWeek - prevWeek) / prevWeek;
        
        // Use logarithmic scaling to prevent extreme Z-scores
        // log(1 + x) compresses large values: 650% -> ~2.0, 100% -> ~0.7
        double scaledGrowth;
        if (rawGrowthRate > 0)
        {
            scaledGrowth = Math.Log(1 + rawGrowthRate); // Compress positive growth
        }
        else
        {
            scaledGrowth = rawGrowthRate; // Keep negative as-is (decreasing incidents)
        }

        // Expected growth = 0 (stable), std = 0.5 (50% variation is normal)
        var zScore = scaledGrowth / 0.5;

        // Cap Z-score to reasonable range [-5, 5]
        return Math.Round(Math.Clamp(zScore, -5.0, 5.0), 2);
    }

    /// <summary>
    /// Enhanced risk scoring with tier-based thresholds
    /// LOW: 0-39, MEDIUM: 40-64, HIGH: 65-84, CRITICAL: 85-100
    /// 
    /// To reach CRITICAL (85+):
    /// - Must have at least 2 high Z-scores (>= 2.5) OR
    /// - One extremely high Z-score (>= 4.0) with another >= 1.5 OR
    /// - Multiple moderate signals adding up
    /// 
    /// To reach 100:
    /// - Must have 3+ high Z-scores (>= 3.0) OR
    /// - At least 2 Z-scores >= 4.0
    /// </summary>
    private int CalculateEnhancedRiskScore(Dictionary<string, double> zScores)
    {
        var absScores = zScores.Values.Select(Math.Abs).OrderByDescending(x => x).ToList();
        
        if (absScores.Count == 0) return 10;

        // Count significant anomalies at different thresholds
        var extremeCount = absScores.Count(z => z >= 4.0);    // Extreme anomaly
        var highCount = absScores.Count(z => z >= 3.0);       // High anomaly
        var mediumCount = absScores.Count(z => z >= 2.0);     // Medium anomaly
        var lowCount = absScores.Count(z => z >= 1.5);        // Low anomaly

        var maxZ = absScores.First();
        var secondZ = absScores.Count > 1 ? absScores[1] : 0;
        var thirdZ = absScores.Count > 2 ? absScores[2] : 0;

        // CRITICAL TIER (85-100): Requires strong evidence
        // Score 100: Multiple extreme anomalies
        if (extremeCount >= 2 || (highCount >= 3 && mediumCount >= 4))
        {
            return 100;
        }
        
        // Score 95: One extreme + one high
        if (extremeCount >= 1 && highCount >= 2)
        {
            return 95;
        }
        
        // Score 90: Multiple high anomalies
        if (highCount >= 2 && mediumCount >= 3)
        {
            return 90;
        }
        
        // Score 85: Strong pattern
        if (maxZ >= 4.0 || (highCount >= 2))
        {
            return 85;
        }

        // HIGH TIER (65-84): Clear anomaly signals
        if (maxZ >= 3.0 && secondZ >= 1.5)
        {
            return 80;
        }
        
        if (maxZ >= 3.0 || (mediumCount >= 3))
        {
            return 75;
        }
        
        if (maxZ >= 2.5 && mediumCount >= 2)
        {
            return 70;
        }
        
        if (maxZ >= 2.5 || (mediumCount >= 2 && lowCount >= 3))
        {
            return 65;
        }

        // MEDIUM TIER (40-64): Some anomaly signals
        if (maxZ >= 2.0 && secondZ >= 1.0)
        {
            return 60;
        }
        
        if (maxZ >= 2.0 || lowCount >= 3)
        {
            return 55;
        }
        
        if (mediumCount >= 1 && lowCount >= 2)
        {
            return 50;
        }
        
        if (maxZ >= 1.5)
        {
            return 45;
        }
        
        if (lowCount >= 2)
        {
            return 40;
        }

        // LOW TIER (0-39): Normal or minor variations
        if (maxZ >= 1.0)
        {
            return 35;
        }
        
        if (maxZ >= 0.5)
        {
            return 25;
        }

        return 15; // Baseline for entities with activity
    }

    /// <summary>
    /// Calculate threat profile multiplier based on action distribution.
    /// If most actions are AUTHORIZED/RELEASED (non-threatening), the multiplier reduces risk score.
    /// If most actions are BLOCK/QUARANTINE (threatening), multiplier stays at 1.0.
    /// 
    /// Examples:
    /// - 100% BLOCK/QUARANTINE: multiplier = 1.0 (full risk)
    /// - 100% AUTHORIZED/RELEASED: multiplier = 0.3 (70% reduction)
    /// - 50/50 mix: multiplier = 0.65
    /// </summary>
    private double CalculateThreatProfileMultiplier(Dictionary<string, int> actionCounts)
    {
        if (actionCounts == null || actionCounts.Count == 0)
            return 1.0;

        // Count high-threat actions (BLOCK, QUARANTINE, BLOCKED, QUARANTINED)
        var highThreatCount = 
            actionCounts.GetValueOrDefault("BLOCK", 0) +
            actionCounts.GetValueOrDefault("BLOCKED", 0) +
            actionCounts.GetValueOrDefault("QUARANTINE", 0) +
            actionCounts.GetValueOrDefault("QUARANTINED", 0);

        // Count low-threat actions (AUTHORIZED, RELEASED)
        var lowThreatCount = 
            actionCounts.GetValueOrDefault("AUTHORIZED", 0) +
            actionCounts.GetValueOrDefault("RELEASED", 0);

        var totalCount = highThreatCount + lowThreatCount;
        
        if (totalCount == 0)
            return 1.0;

        // Calculate the ratio of high-threat actions
        var highThreatRatio = (double)highThreatCount / totalCount;
        
        // Multiplier formula:
        // - 100% high-threat (ratio=1.0) -> multiplier = 1.0
        // - 0% high-threat (ratio=0.0) -> multiplier = 0.3 (minimum)
        // Linear interpolation between these extremes
        const double MIN_MULTIPLIER = 0.3;
        const double MAX_MULTIPLIER = 1.0;
        
        var multiplier = MIN_MULTIPLIER + (highThreatRatio * (MAX_MULTIPLIER - MIN_MULTIPLIER));
        
        return Math.Round(multiplier, 2);
    }

    private List<TrendDataPoint> GenerateWeeklyTrends(List<Incident> incidents, int lookbackDays)
    {
        var weeks = new List<TrendDataPoint>();
        var startDate = DateTime.UtcNow.AddDays(-lookbackDays);

        for (int i = 0; i < (lookbackDays / 7) + 1; i++)
        {
            var weekStart = startDate.AddDays(i * 7);
            var weekEnd = weekStart.AddDays(7);
            var weekIncidents = incidents.Where(inc => inc.Timestamp >= weekStart && inc.Timestamp < weekEnd).ToList();

            var weekNumber = System.Globalization.CultureInfo.CurrentCulture.Calendar
                .GetWeekOfYear(weekStart, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            weeks.Add(new TrendDataPoint
            {
                Label = $"{weekStart.Year}-W{weekNumber:D2}",
                Count = weekIncidents.Count,
                BlockCount = weekIncidents.Count(i => i.Action?.ToUpper() == "BLOCK" || i.Action?.ToUpper() == "BLOCKED"),
                QuarantineCount = weekIncidents.Count(i => i.Action?.ToUpper() == "QUARANTINE" || i.Action?.ToUpper() == "QUARANTINED"),
                AuthorizedCount = weekIncidents.Count(i => i.Action?.ToUpper() == "AUTHORIZED"),
                ReleasedCount = weekIncidents.Count(i => i.Action?.ToUpper() == "RELEASED"),
                TotalMatches = weekIncidents.Sum(i => i.MaxMatches)
            });
        }

        return weeks.Where(w => w.Count > 0).ToList();
    }

    private List<TrendDataPoint> GenerateMonthlyTrends(List<Incident> incidents)
    {
        return incidents
            .GroupBy(i => new { i.Timestamp.Year, i.Timestamp.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new TrendDataPoint
            {
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Count = g.Count(),
                BlockCount = g.Count(i => i.Action?.ToUpper() == "BLOCK" || i.Action?.ToUpper() == "BLOCKED"),
                QuarantineCount = g.Count(i => i.Action?.ToUpper() == "QUARANTINE" || i.Action?.ToUpper() == "QUARANTINED"),
                AuthorizedCount = g.Count(i => i.Action?.ToUpper() == "AUTHORIZED"),
                ReleasedCount = g.Count(i => i.Action?.ToUpper() == "RELEASED"),
                TotalMatches = g.Sum(i => i.MaxMatches)
            })
            .ToList();
    }

    private List<DestinationPattern> GetDestinationPatterns(List<Incident> currentIncidents, List<Incident> baselineIncidents)
    {
        var baselineDestinations = baselineIncidents
            .Where(i => !string.IsNullOrEmpty(i.Destination))
            .Select(i => i.Destination)
            .Distinct()
            .ToHashSet();

        return currentIncidents
            .Where(i => !string.IsNullOrEmpty(i.Destination))
            .GroupBy(i => i.Destination)
            .OrderByDescending(g => g.Sum(i => i.MaxMatches))
            .Take(20)
            .Select(g => new DestinationPattern
            {
                Destination = g.Key!,
                IncidentCount = g.Count(),
                TotalMatches = g.Sum(i => i.MaxMatches),
                IsNew = !baselineDestinations.Contains(g.Key)
            })
            .ToList();
    }

    #endregion
}

