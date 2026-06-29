using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class RiskScoringService : IRiskScoringService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly AnalyzerDbContext _context;
    private readonly Shared.Services.RiskAnalyzer _riskAnalyzer;

    public RiskScoringService(
        IIncidentRepository incidentRepository,
        AnalyzerDbContext context)
    {
        _incidentRepository = incidentRepository;
        _context = context;
        _riskAnalyzer = new Shared.Services.RiskAnalyzer();
    }

    /// <summary>
    /// Get display score for dashboard (already 0-100 scale)
    /// </summary>
    public double GetDisplayScore(int riskScore)
    {
        return _riskAnalyzer.GetDisplayScore(riskScore);
    }

    /// <summary>
    /// Calculate and store daily risk scores for all users for a specific date
    /// Should be run at the end of the day (e.g. 23:59)
    /// </summary>
    public async Task<int> CalculateDailyScoresAsync(DateOnly? date = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        
        // Convert DateOnly to UTC DateTime range for the full day
        var startDateTime = targetDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var endDateTime = targetDate.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

        // Get incidents for the day
        var incidents = await _incidentRepository.GetIncidentsAsync(targetDate, targetDate);
        if (!incidents.Any()) return 0;

        var userGroups = incidents
            .GroupBy(i => i.UserEmail)
            .ToList();

        var updatedCount = 0;

        foreach (var group in userGroups)
        {
            var userEmail = group.Key;
            var dailyIncidents = group.ToList();
            
            var sumRiskScore = dailyIncidents.Sum(i => (double)(i.RiskScore ?? 0));
            var maxRiskScore = dailyIncidents.Max(i => i.RiskScore ?? 0);
            var incidentCount = dailyIncidents.Count;
            var avgRiskScore = incidentCount > 0 ? sumRiskScore / incidentCount : 0;
            
            // Calculate action counts
            var blockCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("BLOCK", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("BLOCKED", StringComparison.OrdinalIgnoreCase)));
            var permitCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("PERMIT", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("PERMITTED", StringComparison.OrdinalIgnoreCase) ||
                                      i.Action.Equals("AUTHORIZED", StringComparison.OrdinalIgnoreCase)));
            var quarantineCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("QUARANTINE", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("QUARANTINED", StringComparison.OrdinalIgnoreCase)));
            var releasedCount = dailyIncidents.Count(i => 
                i.Action != null && (i.Action.Equals("RELEASE", StringComparison.OrdinalIgnoreCase) || 
                                      i.Action.Equals("RELEASED", StringComparison.OrdinalIgnoreCase)));
            
            // Calculate max matches stats
            var maxMaxMatches = dailyIncidents.Max(i => i.MaxMatches);
            var avgMaxMatches = incidentCount > 0 ? dailyIncidents.Average(i => (double)i.MaxMatches) : 0;
            
            // Get team, full_name and email_address from first incident that has them
            var firstWithTeam = dailyIncidents.FirstOrDefault(i => !string.IsNullOrEmpty(i.Team) || !string.IsNullOrEmpty(i.Department));
            var firstWithName = dailyIncidents.FirstOrDefault(i => !string.IsNullOrEmpty(i.FullName));
            var firstWithEmail = dailyIncidents.FirstOrDefault(i => !string.IsNullOrEmpty(i.EmailAddress));
            var team = firstWithTeam?.Team ?? firstWithTeam?.Department;
            var fullName = firstWithName?.FullName;
            var incidentEmailAddress = firstWithEmail?.EmailAddress;
            
            // Normalized daily score (0-100 scale)
            // Now incident scores are directly 0-100, so we use direct multipliers
            // Formula: (Avg * 0.50) + (Max * 0.30) + MIN(20, LOG10(Count+1) * 10)
            var normalizedScore = Math.Min(100,
                (avgRiskScore * 0.50) +
                (maxRiskScore * 0.30) +
                Math.Min(20, Math.Log10(incidentCount + 1) * 10)
            );
            var totalRiskScore = Math.Round(normalizedScore, 2);

            // Check if record exists
            var existingRecord = await _context.UserDailyRiskScores
                .FirstOrDefaultAsync(r => r.UserEmail == userEmail && r.Date == targetDate);

            if (existingRecord != null)
            {
                existingRecord.DailyRiskScore = totalRiskScore;
                existingRecord.IncidentCount = incidentCount;
                existingRecord.MaxRiskScore = maxRiskScore;
                existingRecord.AvgRiskScore = avgRiskScore;
                existingRecord.BlockCount = blockCount;
                existingRecord.PermitCount = permitCount;
                existingRecord.QuarantineCount = quarantineCount;
                existingRecord.ReleasedCount = releasedCount;
                existingRecord.MaxMaxMatches = maxMaxMatches;
                existingRecord.AvgMaxMatches = avgMaxMatches;
                if (!string.IsNullOrEmpty(team)) existingRecord.Team = team;
                if (!string.IsNullOrEmpty(fullName)) existingRecord.FullName = fullName;
                if (!string.IsNullOrEmpty(incidentEmailAddress)) existingRecord.EmailAddress = incidentEmailAddress;
                // CreatedAt remains original
            }
            else
            {
                var newRecord = new UserDailyRiskScore
                {
                    UserEmail = userEmail,
                    Date = targetDate,
                    DailyRiskScore = totalRiskScore,
                    IncidentCount = incidentCount,
                    MaxRiskScore = maxRiskScore,
                    AvgRiskScore = avgRiskScore,
                    BlockCount = blockCount,
                    PermitCount = permitCount,
                    QuarantineCount = quarantineCount,
                    ReleasedCount = releasedCount,
                    MaxMaxMatches = maxMaxMatches,
                    AvgMaxMatches = avgMaxMatches,
                    Team = team,
                    FullName = fullName,
                    EmailAddress = incidentEmailAddress,
                    CreatedAt = DateTime.UtcNow
                };
                
                await _context.UserDailyRiskScores.AddAsync(newRecord);
            }
            
            updatedCount++;
        }

        await _context.SaveChangesAsync();
        return updatedCount;
    }

    /// <summary>
    /// Calculate risk scores for incidents without scores
    /// GÜNCEL FORMÜL: MaxMatchesTier * ChannelMultiplier * DestinationScore
    /// </summary>
    public async Task<int> CalculateRiskScoresAsync()
    {
        var incidentsWithoutScores = await _incidentRepository.GetIncidentsWithoutRiskScoreAsync();
        if (!incidentsWithoutScores.Any())
            return 0;

        // Load NDA domains for fast lookup
        var ndaDomains = await _context.NdaDomains
            .ToDictionaryAsync(d => d.Domain.ToLower(), d => d);

        var updatedCount = 0;
        foreach (var incident in incidentsWithoutScores)
        {
            // 1. Calculate Destination Score
            int destinationScore = RiskConstants.DestinationScores.Unknown; // Default 5
            
            if (!string.IsNullOrEmpty(incident.Destination))
            {
                var dest = incident.Destination.Trim();
                
                // SPL Check (Hardcoded for now as per request)
                // "SPL bizim ortamımız 1 olsun"
                if (dest.Contains("SPL", StringComparison.OrdinalIgnoreCase))
                {
                    destinationScore = RiskConstants.DestinationScores.Spl; // 1
                }
                // Printer Check
                else if (incident.Channel?.Contains("Printer", StringComparison.OrdinalIgnoreCase) == true || 
                         incident.Channel?.Contains("Printing", StringComparison.OrdinalIgnoreCase) == true ||
                         dest.Contains("Print", StringComparison.OrdinalIgnoreCase))
                {
                    destinationScore = RiskConstants.DestinationScores.Printer; // 3
                }
                else 
                {
                    // Domain extraction logic could be complex (URL vs Email vs IP)
                    // Simplified: Assume destination is somewhat domain-like or contains domain
                    
                    var lowerDest = dest.ToLowerInvariant();
                    
                    // Try exact match first
                    if (ndaDomains.TryGetValue(lowerDest, out var domainInfo))
                    {
                        if (domainInfo.IsPersonal) destinationScore = RiskConstants.DestinationScores.Personal; // 10
                        else if (domainInfo.HasNda) destinationScore = RiskConstants.DestinationScores.NdaPresent; // 1
                        else destinationScore = RiskConstants.DestinationScores.NdaAbsent; // 5
                    }
                    else
                    {
                        // Partial match check (e.g. user@gmail.com contains gmail.com)
                        // This is computationally expensive O(N*M), but N (domains) is small (~900)
                        
                        // Check explicit personal domains first (optimization)
                        if (lowerDest.Contains("gmail.com") || lowerDest.Contains("hotmail.com") || 
                            lowerDest.Contains("outlook.com") || lowerDest.Contains("windowslive.com") || 
                            lowerDest.Contains("icloud.com") || lowerDest.Contains("yahoo.com") ||
                            lowerDest.Contains("mynet.com"))
                        {
                            destinationScore = RiskConstants.DestinationScores.Personal; // 10
                        }
                        else
                        {
                            // Check DB domains
                            var matchedDomain = ndaDomains.Values
                                .FirstOrDefault(d => lowerDest.Contains(d.Domain));
                                
                            if (matchedDomain != null)
                            {
                                if (matchedDomain.IsPersonal) destinationScore = RiskConstants.DestinationScores.Personal; // 10
                                else if (matchedDomain.HasNda) destinationScore = RiskConstants.DestinationScores.NdaPresent; // 1
                                else destinationScore = RiskConstants.DestinationScores.NdaAbsent; // 5
                            }
                            else
                            {
                                // Unknown/New domain
                                // Automatically add to DB as Unknown if it looks like a domain (contains dot)
                                // But don't block the thread
                                // Future improvement: Queue for addition
                                destinationScore = RiskConstants.DestinationScores.Unknown; // 5
                            }
                        }
                    }
                }
            }
            
            // Channel is already in incident.Channel
            // MaxMatches is in incident.MaxMatches

            // Calculate risk score with new formula
            incident.RiskScore = _riskAnalyzer.CalculateRiskScoreV2(
                maxMatches: incident.MaxMatches,
                channel: incident.Channel,
                destinationScore: destinationScore,
                action: incident.Action);
            
            // We still keep other fields populated for reference, even if not used in formula
            // Calculate data sensitivity (based on data type and severity)
            incident.DataSensitivity = CalculateDataSensitivity(incident.DataType, incident.Severity);

            // Policy Repeat Count (keeping legacy logic for population)
            var policyRepeatCounts = await _incidentRepository.GetPolicyRepeatCountsAsync(incident.UserEmail, incident.Timestamp);
            incident.RepeatCount = policyRepeatCounts.Values.DefaultIfEmpty(0).Max();

            incident.RiskLevel = _riskAnalyzer.GetRiskLevel(incident.RiskScore.Value);

            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await _incidentRepository.UpdateIncidentsAsync(incidentsWithoutScores);
        }

        return updatedCount;
    }

    private int CalculateDataSensitivity(string? dataType, int severity)
    {
        if (string.IsNullOrEmpty(dataType))
            return severity;

        var dataTypeLower = dataType.ToLower();
        
        // Use RiskConstants for data sensitivity thresholds
        if (dataTypeLower.Contains(RiskConstants.DataSensitivity.PII) || 
            dataTypeLower.Contains(RiskConstants.DataSensitivity.Personal))
            return Math.Max(severity, RiskConstants.DataSensitivity.PIIThreshold);
        if (dataTypeLower.Contains(RiskConstants.DataSensitivity.PCI) || 
            dataTypeLower.Contains(RiskConstants.DataSensitivity.Credit))
            return Math.Max(severity, RiskConstants.DataSensitivity.PCIThreshold);
        if (dataTypeLower.Contains(RiskConstants.DataSensitivity.Confidential))
            return Math.Max(severity, RiskConstants.DataSensitivity.ConfidentialThreshold);

        return severity;
    }
}
