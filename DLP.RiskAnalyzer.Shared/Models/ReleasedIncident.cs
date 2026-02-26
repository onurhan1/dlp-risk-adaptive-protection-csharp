namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Represents a released quarantined incident from the DLP system.
/// These are incidents where an admin released a quarantined message.
/// </summary>
public class ReleasedIncident
{
    /// <summary>
    /// Auto-generated unique ID for the record
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Original incident ID from DLP system
    /// </summary>
    public long IncidentId { get; set; }
    
    /// <summary>
    /// When the incident originally occurred (incident_time from API)
    /// </summary>
    public DateTime IncidentTimestamp { get; set; }
    
    /// <summary>
    /// The action taken on the incident (e.g., RELEASED)
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Task name from history (e.g., "Released quarantined message")
    /// </summary>
    public string TaskName { get; set; } = string.Empty;
    
    /// <summary>
    /// Admin who released the incident
    /// </summary>
    public string? AdminName { get; set; }
    
    /// <summary>
    /// Comments added during release (often contains ticket number like INC1310908)
    /// </summary>
    public string? Comments { get; set; }
    
    /// <summary>
    /// When the release action was taken
    /// </summary>
    public DateTime? UpdateTime { get; set; }
    
    /// <summary>
    /// When this record was created in our database
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
