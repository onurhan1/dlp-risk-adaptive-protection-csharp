namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// AI Behavioral Analysis result model
/// </summary>
public class AIBehavioralAnalysis
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty; // "user", "channel", "department"
    public string EntityId { get; set; } = string.Empty; // user_email, channel_name, department_name
    public DateTime AnalysisDate { get; set; }
    public int RiskScore { get; set; } // 0-100
    public string AnomalyLevel { get; set; } = string.Empty; // "low", "medium", "high"
    public string AIExplanation { get; set; } = string.Empty;
    public string AIRecommendation { get; set; } = string.Empty;
    public string ReferenceIncidentIds { get; set; } = string.Empty; // JSON array of incident IDs
    public string AnalysisMetadata { get; set; } = string.Empty; // JSON with z-score, baseline, etc.
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// AI Behavioral Analysis request/response DTOs
/// </summary>
public class AIBehavioralAnalysisRequest
{
    public string? EntityType { get; set; } // "user", "channel", "department", "all"
    public string? EntityId { get; set; }
    public int LookbackDays { get; set; } = 7;
}

public class AIBehavioralAnalysisResponse
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string AnomalyLevel { get; set; } = string.Empty;
    public string AIExplanation { get; set; } = string.Empty;
    public string AIRecommendation { get; set; } = string.Empty;
    public List<int> ReferenceIncidentIds { get; set; } = new();
    public Dictionary<string, object> AnalysisMetadata { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
}

public class AIBehavioralOverviewResponse
{
    public int TotalAnalyzed { get; set; }
    public int HighAnomalyCount { get; set; }
    public int MediumAnomalyCount { get; set; }
    public int LowAnomalyCount { get; set; }
    
    // Entity-specific anomaly lists
    public List<AIBehavioralAnalysisResponse> UserAnomalies { get; set; } = new();
    public List<AIBehavioralAnalysisResponse> ChannelAnomalies { get; set; } = new();
    public List<AIBehavioralAnalysisResponse> DepartmentAnomalies { get; set; } = new();
    public List<AIBehavioralAnalysisResponse> DestinationAnomalies { get; set; } = new();
    public List<AIBehavioralAnalysisResponse> RuleAnomalies { get; set; } = new();
    
    // Unique values for autocomplete/dropdown
    public List<string> UniqueUsers { get; set; } = new();
    public List<string> UniqueChannels { get; set; } = new();
    public List<string> UniqueDepartments { get; set; } = new();
    public List<string> UniqueDestinations { get; set; } = new();
    public List<string> UniqueRules { get; set; } = new();
    
    // Keep for backward compatibility
    public List<AIBehavioralAnalysisResponse> TopAnomalies { get; set; } = new();
    public Dictionary<string, int> AnomalyByChannel { get; set; } = new();
    public Dictionary<string, int> AnomalyByDepartment { get; set; } = new();
}

