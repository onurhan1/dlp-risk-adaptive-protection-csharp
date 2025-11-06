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
    public string? RiskLevel { get; set; }
    public string? RecommendedAction { get; set; }
    public List<string>? IOBs { get; set; }
}
