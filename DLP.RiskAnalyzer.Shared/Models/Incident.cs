namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Incident model - DLP olay kaydı
/// </summary>
public class Incident
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int Severity { get; set; }
    public string? DataType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Policy { get; set; }
    public string? Channel { get; set; }
    public int? RiskScore { get; set; }
    public int RepeatCount { get; set; }
    public int DataSensitivity { get; set; }
    public int MaxMatches { get; set; }  // Maximum classifier matches from ViolationTriggers
    
    // New fields from Forcepoint DLP API
    public string? Action { get; set; }  // AUTHORIZED, BLOCK, QUARANTINE
    public string? Destination { get; set; }  // Target (printer, email, URL, etc.)
    public string? FileName { get; set; }  // File name with size
    public string? LoginName { get; set; }  // User login name (e.g., KUVEYTTURK\nyigit)
    public string? EmailAddress { get; set; }  // Email address (if available)
    public string? ViolationTriggers { get; set; }  // JSON: [{policy_name, rule_name, classifiers}]
    
    // Enriched fields
    public string? RiskLevel { get; set; }
    public string? RecommendedAction { get; set; }
    public List<string>? IOBs { get; set; }
}

/// <summary>
/// Incident response DTO
/// </summary>
public class IncidentResponse
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int Severity { get; set; }
    public string? DataType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Policy { get; set; }
    public string? Channel { get; set; }
    public int? RiskScore { get; set; }
    public int RepeatCount { get; set; }
    public int DataSensitivity { get; set; }
    public int MaxMatches { get; set; }  // Maximum classifier matches from ViolationTriggers
    
    // New fields from Forcepoint DLP API
    public string? Action { get; set; }
    public string? Destination { get; set; }
    public string? FileName { get; set; }
    public string? LoginName { get; set; }
    public string? EmailAddress { get; set; }
    public string? ViolationTriggers { get; set; }
    
    // Enriched fields
    public string? RiskLevel { get; set; }
    public string? RecommendedAction { get; set; }
    public List<string>? IOBs { get; set; }
}