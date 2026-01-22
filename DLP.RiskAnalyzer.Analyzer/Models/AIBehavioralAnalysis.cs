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

/// <summary>
/// Detailed analysis response with trends and charts data
/// </summary>
public class AIBehavioralDetailResponse
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string AnomalyLevel { get; set; } = string.Empty;
    public string AIExplanation { get; set; } = string.Empty;
    public string AIRecommendation { get; set; } = string.Empty;
    public List<int> ReferenceIncidentIds { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
    
    // Z-Score breakdown
    public Dictionary<string, double> ZScores { get; set; } = new();
    
    // Z-Score calculation details for clickable items
    public Dictionary<string, ZScoreDetail> ZScoreDetails { get; set; } = new();
    
    // Trend data for charts
    public List<TrendDataPoint> WeeklyTrends { get; set; } = new();
    public List<TrendDataPoint> MonthlyTrends { get; set; } = new();
    
    // Action breakdown for pie chart
    public Dictionary<string, int> ActionCounts { get; set; } = new();
    public Dictionary<string, double> ActionZScores { get; set; } = new();
    
    // Statistics
    public int TotalIncidents { get; set; }
    public int TotalMatches { get; set; }
    public double AvgMatchesPerIncident { get; set; }
    
    // Destination patterns (for users)
    public List<DestinationPattern> DestinationPatterns { get; set; } = new();
    public double DestinationDiversity { get; set; }
    
    // Top incidents for table
    public List<IncidentSummary> TopIncidents { get; set; } = new();
}

public class TrendDataPoint
{
    public string Label { get; set; } = string.Empty; // "2024-W01" or "2024-01"
    public int Count { get; set; }
    public int BlockCount { get; set; }
    public int QuarantineCount { get; set; }
    public int AuthorizedCount { get; set; }
    public int ReleasedCount { get; set; }
    public int TotalMatches { get; set; }
    public string StartDate { get; set; } = string.Empty; // For drill-down
    public string EndDate { get; set; } = string.Empty;   // For drill-down
    public List<DailyData> DailyBreakdown { get; set; } = new(); // Daily drill-down data
}

public class DailyData
{
    public string Date { get; set; } = string.Empty; // "2024-01-15"
    public int Count { get; set; }
    public int BlockCount { get; set; }
    public int QuarantineCount { get; set; }
    public int AuthorizedCount { get; set; }
    public int ReleasedCount { get; set; }
    public int TotalMatches { get; set; }
}

public class DestinationPattern
{
    public string Destination { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public int TotalMatches { get; set; }
    public bool IsNew { get; set; } // Not in baseline
}

public class IncidentSummary
{
    public int Id { get; set; }
    public string LoginName { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int MaxMatches { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Z-Score calculation detail for clickable breakdown
/// </summary>
public class ZScoreDetail
{
    public double ZScore { get; set; }
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double CurrentValue { get; set; }
    public double BaselineValue { get; set; }
    public string Formula { get; set; } = "Z = (Current - Mean) / StdDev";
}
