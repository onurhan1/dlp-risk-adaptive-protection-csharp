using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Data Classification Service
/// </summary>
public class ClassificationService
{
    private readonly AnalyzerDbContext _context;

    public ClassificationService(AnalyzerDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get incident classification details
    /// </summary>
    public async Task<Dictionary<string, object>> GetIncidentClassificationAsync(int incidentId)
    {
        var incident = await _context.Incidents
            .FirstOrDefaultAsync(i => i.Id == incidentId);

        if (incident == null)
            throw new Exception("Incident not found");

        // Determine classification based on data type and severity
        var classification = DetermineClassification(incident.DataType, incident.Severity);

        return new Dictionary<string, object>
        {
            { "incident_id", incident.Id },
            { "data_type", incident.DataType ?? "Unknown" },
            { "classification", classification },
            { "severity", incident.Severity },
            { "confidence_score", CalculateConfidenceScore(incident) },
            { "matched_patterns", GetMatchedPatterns(incident) },
            { "file_count", 1 }, // Placeholder
            { "total_size", 0 } // Placeholder
        };
    }

    /// <summary>
    /// Get incident file details
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetIncidentFilesAsync(int incidentId)
    {
        var incident = await _context.Incidents
            .FirstOrDefaultAsync(i => i.Id == incidentId);

        if (incident == null)
            throw new Exception("Incident not found");

        // Placeholder - in real implementation, fetch from DLP API or file metadata
        return new List<Dictionary<string, object>>
        {
            new()
            {
                { "file_name", "example.pdf" },
                { "file_size", 1024 },
                { "file_type", incident.DataType ?? "Unknown" },
                { "classification", DetermineClassification(incident.DataType, incident.Severity) },
                { "hash", "placeholder_hash" }
            }
        };
    }

    /// <summary>
    /// Get user classification summary
    /// </summary>
    public async Task<Dictionary<string, object>> GetUserClassificationSummaryAsync(string userEmail)
    {
        var incidents = await _context.Incidents
            .Where(i => i.UserEmail == userEmail)
            .ToListAsync();

        var classifications = incidents
            .GroupBy(i => DetermineClassification(i.DataType, i.Severity))
            .Select(g => new { Classification = g.Key, Count = g.Count() })
            .ToList();

        return new Dictionary<string, object>
        {
            { "user_email", userEmail },
            { "total_incidents", incidents.Count },
            { "classifications", classifications.ToDictionary(c => c.Classification, c => c.Count) },
            { "most_common_classification", classifications.OrderByDescending(c => c.Count).FirstOrDefault()?.Classification ?? "Unknown" }
        };
    }

    private string DetermineClassification(string? dataType, int severity)
    {
        if (string.IsNullOrEmpty(dataType))
            return "Unknown";

        var dataTypeLower = dataType.ToLower();

        if (dataTypeLower.Contains("pii") || dataTypeLower.Contains("personal"))
            return "PII";
        if (dataTypeLower.Contains("pci") || dataTypeLower.Contains("credit") || dataTypeLower.Contains("card"))
            return "PCI";
        if (dataTypeLower.Contains("hipaa") || dataTypeLower.Contains("health"))
            return "HIPAA";
        if (dataTypeLower.Contains("confidential") && severity >= 7)
            return "Confidential";
        if (severity >= 8)
            return "Restricted";

        return "Internal";
    }

    private int CalculateConfidenceScore(Shared.Models.Incident incident)
    {
        // Confidence based on severity and data type
        var baseScore = incident.Severity * 10;
        if (!string.IsNullOrEmpty(incident.DataType))
            baseScore += 10;
        return Math.Min(100, baseScore);
    }

    private List<string> GetMatchedPatterns(Shared.Models.Incident incident)
    {
        var patterns = new List<string>();
        
        if (!string.IsNullOrEmpty(incident.DataType))
        {
            patterns.Add($"Data Type: {incident.DataType}");
        }

        if (!string.IsNullOrEmpty(incident.Policy))
        {
            patterns.Add($"Policy: {incident.Policy}");
        }

        if (incident.Severity >= 8)
        {
            patterns.Add("High Severity Pattern");
        }

        return patterns;
    }
}

