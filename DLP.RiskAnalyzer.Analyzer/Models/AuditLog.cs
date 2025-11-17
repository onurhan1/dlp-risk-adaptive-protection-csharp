namespace DLP.RiskAnalyzer.Analyzer.Models;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // "Login", "Logout", "SettingsChange", "IncidentView", "UserCreate", etc.
    public string UserName { get; set; } = string.Empty;
    public string? UserRole { get; set; }
    public string Action { get; set; } = string.Empty; // "GET /api/incidents", "POST /api/users", etc.
    public string? Resource { get; set; } // "Incident:123", "User:admin", etc.
    public string? Details { get; set; } // JSON string with additional details
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }
    public long? DurationMs { get; set; } // Request duration in milliseconds
}

