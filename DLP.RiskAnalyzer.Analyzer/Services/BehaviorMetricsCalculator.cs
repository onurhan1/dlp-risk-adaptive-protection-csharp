using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IBehaviorMetricsCalculator
{
    BehaviorMetrics CalculateEnhancedMetrics(List<Incident> incidents);
    Dictionary<string, double> CalculateAllZScores(BehaviorMetrics current, BehaviorMetrics baseline, List<Incident> currentIncidents, List<Incident> baselineIncidents);
    int CalculateEnhancedRiskScore(Dictionary<string, double> zScores);
    string DetermineAnomalyLevel(int riskScore);
    double CalculateThreatProfileMultiplier(List<Incident> incidents);
    List<TrendDataPoint> GenerateWeeklyTrends(List<Incident> incidents, int lookbackDays);
    List<TrendDataPoint> GenerateMonthlyTrends(List<Incident> incidents);
    List<DestinationPattern> GetDestinationPatterns(List<Incident> currentIncidents, List<Incident> baselineIncidents);
    int GetEffectiveMaxMatches(Incident incident);
}

public class BehaviorMetricsCalculator : IBehaviorMetricsCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<BehaviorMetricsCalculator> _logger;

    public BehaviorMetricsCalculator(ILogger<BehaviorMetricsCalculator> logger)
    {
        _logger = logger;
    }

    public BehaviorMetrics CalculateEnhancedMetrics(List<Incident> incidents)
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

    public Dictionary<string, double> CalculateAllZScores(
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
        zScores["action_block"] = CalculateActionZScore("BLOCK", current, baseline) * BehaviorThresholds.HighThreatZScoreWeight;
        zScores["action_quarantine"] = CalculateActionZScore("QUARANTINE", current, baseline) * BehaviorThresholds.HighThreatZScoreWeight;
        zScores["action_authorized"] = CalculateActionZScore("AUTHORIZED", current, baseline) * BehaviorThresholds.LowThreatZScoreWeight;
        zScores["action_released"] = CalculateActionZScore("RELEASED", current, baseline) * BehaviorThresholds.LowThreatZScoreWeight;

        // MaxMatches Z-score
        zScores["max_matches"] = baseline.StdDevMatches > 0
            ? Math.Round((current.AvgMatchesPerIncident - baseline.AvgMatchesPerIncident) / baseline.StdDevMatches, 2)
            : 0;

        // Weekly trend Z-score - needs all incidents to compare this week vs last week
        var allIncidentsForTrend = currentIncidents.Concat(baselineIncidents).ToList();
        zScores["weekly_trend"] = CalculateWeeklyTrendZScore(allIncidentsForTrend);

        return zScores;
    }

    public double CalculateActionZScore(string action, BehaviorMetrics current, BehaviorMetrics baseline)
    {
        var currentCount = current.ActionCounts.GetValueOrDefault(action, 0) +
                          current.ActionCounts.GetValueOrDefault(action + "ED", 0); // BLOCK/BLOCKED
        var baselineCount = baseline.ActionCounts.GetValueOrDefault(action, 0) +
                           baseline.ActionCounts.GetValueOrDefault(action + "ED", 0);

        if (baselineCount == 0) return currentCount > 0 ? 2.0 : 0;

        var baselineStd = Math.Max(1, baselineCount * 0.3);
        return Math.Round((currentCount - baselineCount) / baselineStd, 2);
    }

    public double CalculateWeeklyTrendZScore(List<Incident> incidents)
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
    public int CalculateEnhancedRiskScore(Dictionary<string, double> zScores)
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
    /// Calculate threat profile multiplier based on per-incident analysis.
    /// Each incident contributes a weighted threat score based on:
    /// - Action type: BLOCK=1.0, QUARANTINE=0.8, AUTHORIZED=0.2, RELEASED=0.2
    /// - Severity: 1-3 scale (higher = more severe)
    /// - MaxMatches: log(1 + matches) to prevent extreme values
    /// 
    /// The multiplier ranges from 0.2 (all low-threat) to 1.0 (all high-threat)
    /// </summary>
    public double CalculateThreatProfileMultiplier(List<Incident> incidents)
    {
        if (incidents == null || incidents.Count == 0)
            return 1.0;

        // Action weights
        var actionWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "BLOCK", BehaviorThresholds.ActionWeightBlock },
            { "BLOCKED", BehaviorThresholds.ActionWeightBlock },
            { "QUARANTINE", BehaviorThresholds.ActionWeightQuarantine },
            { "QUARANTINED", BehaviorThresholds.ActionWeightQuarantine },
            { "AUTHORIZED", BehaviorThresholds.ActionWeightAuthorized },
            { "RELEASED", BehaviorThresholds.ActionWeightReleased }
        };

        double totalWeightedThreat = 0;
        double maxPossibleThreat = 0;

        foreach (var incident in incidents)
        {
            // Get action weight (default 0.5 for unknown actions)
            var actionWeight = actionWeights.GetValueOrDefault(incident.Action ?? "", BehaviorThresholds.ActionWeightDefault);
            
            // Normalize severity (1-3) to 0.33-1.0 range
            var severityFactor = Math.Max(0.33, incident.Severity / 3.0);
            
            // MaxMatches factor using log scale (1 match = 0.69, 10 matches = 2.4, 100 matches = 4.6)
            var matchesFactor = Math.Log(1 + Math.Max(1, incident.MaxMatches));
            
            // Calculate weighted threat for this incident
            var incidentThreat = actionWeight * severityFactor * matchesFactor;
            totalWeightedThreat += incidentThreat;
            
            // Max possible threat (if this were a BLOCK with max severity)
            maxPossibleThreat += 1.0 * 1.0 * matchesFactor;
        }

        if (maxPossibleThreat == 0)
            return 1.0;

        // Calculate threat ratio (0 to 1)
        var threatRatio = totalWeightedThreat / maxPossibleThreat;
        
        // Map to multiplier range [0.2, 1.0]
        // threatRatio 0.0 -> multiplier 0.2
        // threatRatio 1.0 -> multiplier 1.0
        const double MIN_MULT = 0.2;
        const double MAX_MULT = 1.0;
        
        var multiplier = MIN_MULT + (threatRatio * (MAX_MULT - MIN_MULT));
        
        return Math.Round(Math.Clamp(multiplier, MIN_MULT, MAX_MULT), 2);
    }

    public List<TrendDataPoint> GenerateWeeklyTrends(List<Incident> incidents, int lookbackDays)
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

            // Generate daily breakdown for this week
            var dailyBreakdown = new List<DailyData>();
            for (int d = 0; d < 7; d++)
            {
                var dayStart = weekStart.AddDays(d);
                var dayEnd = dayStart.AddDays(1);
                var dayIncidents = weekIncidents.Where(inc => inc.Timestamp >= dayStart && inc.Timestamp < dayEnd).ToList();
                
                if (dayIncidents.Count > 0 || dayStart <= DateTime.UtcNow)
                {
                    dailyBreakdown.Add(new DailyData
                    {
                        Date = dayStart.ToString("yyyy-MM-dd"),
                        Count = dayIncidents.Count,
                        BlockCount = dayIncidents.Count(x => x.Action?.ToUpper() == "BLOCK" || x.Action?.ToUpper() == "BLOCKED"),
                        QuarantineCount = dayIncidents.Count(x => x.Action?.ToUpper() == "QUARANTINE" || x.Action?.ToUpper() == "QUARANTINED"),
                        AuthorizedCount = dayIncidents.Count(x => x.Action?.ToUpper() == "AUTHORIZED"),
                        ReleasedCount = dayIncidents.Count(x => x.Action?.ToUpper() == "RELEASED"),
                        TotalMatches = dayIncidents.Sum(x => x.MaxMatches)
                    });
                }
            }

            weeks.Add(new TrendDataPoint
            {
                Label = $"{weekStart.Year}-W{weekNumber:D2}",
                StartDate = weekStart.ToString("yyyy-MM-dd"),
                EndDate = weekEnd.AddDays(-1).ToString("yyyy-MM-dd"),
                Count = weekIncidents.Count,
                BlockCount = weekIncidents.Count(x => x.Action?.ToUpper() == "BLOCK" || x.Action?.ToUpper() == "BLOCKED"),
                QuarantineCount = weekIncidents.Count(x => x.Action?.ToUpper() == "QUARANTINE" || x.Action?.ToUpper() == "QUARANTINED"),
                AuthorizedCount = weekIncidents.Count(x => x.Action?.ToUpper() == "AUTHORIZED"),
                ReleasedCount = weekIncidents.Count(x => x.Action?.ToUpper() == "RELEASED"),
                TotalMatches = weekIncidents.Sum(x => x.MaxMatches),
                DailyBreakdown = dailyBreakdown
            });
        }

        return weeks.Where(w => w.Count > 0).ToList();
    }

    public List<TrendDataPoint> GenerateMonthlyTrends(List<Incident> incidents)
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

    public List<DestinationPattern> GetDestinationPatterns(List<Incident> currentIncidents, List<Incident> baselineIncidents)
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
            .Take(BehaviorThresholds.DestinationPatternLimit)
            .Select(g => new DestinationPattern
            {
                Destination = g.Key!,
                IncidentCount = g.Count(),
                TotalMatches = g.Sum(i => i.MaxMatches),
                IsNew = !baselineDestinations.Contains(g.Key)
            })
            .ToList();
    }

    public string DetermineAnomalyLevel(int riskScore)
    {
        return riskScore switch
        {
            >= BehaviorThresholds.CriticalRiskThreshold => "critical",
            >= BehaviorThresholds.HighRiskThreshold => "high",
            >= BehaviorThresholds.MediumRiskThreshold => "medium",
            _ => "low"
        };
    }

    /// <summary>
    /// Get effective MaxMatches value - calculates from ViolationTriggers JSON if database value is 0
    /// </summary>
    public int GetEffectiveMaxMatches(Incident incident)
    {
        // If database has a value, use it
        if (incident.MaxMatches > 0)
            return incident.MaxMatches;

        // Otherwise, calculate from ViolationTriggers JSON
        if (string.IsNullOrEmpty(incident.ViolationTriggers))
            return 0;

        try
        {
            var triggers = System.Text.Json.JsonSerializer.Deserialize<List<ViolationTriggerDto>>(incident.ViolationTriggers, JsonOptions);
            if (triggers == null || triggers.Count == 0)
                return 0;

            // Get max MatchCount from all triggers
            int maxMatches = 0;
            foreach (var trigger in triggers)
            {
                if (trigger.MatchCount > maxMatches)
                    maxMatches = trigger.MatchCount;
            }
            return maxMatches;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse ViolationTriggers JSON for incident {Id}", incident.Id);
            return 0;
        }
    }

    /// <summary>
    /// Calculate channel-specific Z-score comparing current vs baseline activity
    /// </summary>
    private double CalculateChannelZScore(string channel, BehaviorMetrics current, BehaviorMetrics baseline)
    {
        var currentCount = current.ChannelCounts.GetValueOrDefault(channel, 0);
        var baselineCount = baseline.ChannelCounts.GetValueOrDefault(channel, 0);

        if (baselineCount == 0)
        {
            return 0;
        }

        var baselineStd = baseline.ChannelCounts.Count > 1
            ? Math.Sqrt(baseline.ChannelCounts.Values.Sum(x => Math.Pow(x - baseline.ChannelCounts.Values.Average(), 2)) / (baseline.ChannelCounts.Count - 1))
            : Math.Max(1, baselineCount * 0.3);

        baselineStd = Math.Max(baselineStd, 1);

        return (currentCount - baselineCount) / baselineStd;
    }
}

