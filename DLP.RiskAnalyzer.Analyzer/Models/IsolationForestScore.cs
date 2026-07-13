namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// Isolation Forest scoring result stored per user per batch run
/// </summary>
public class IsolationForestScore
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime CalculatedAt { get; set; }
    public int LookbackDays { get; set; }
    public double IFScore { get; set; }        // 0-100 normalized
    public double AnomalyRaw { get; set; }     // raw anomaly score (higher = more anomalous)
    public bool IsAnomaly { get; set; }
    public int IncidentCount { get; set; }
    public int BaselineIncidentCount { get; set; }
    public string FeatureContributions { get; set; } = "[]"; // JSON: List<FeatureContributionDto>
    public string GroupBreakdown { get; set; } = "{}";       // JSON: Dict<group, abs_sum>
    public string JobId { get; set; } = string.Empty;
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class IsolationForestScoreDto
{
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime CalculatedAt { get; set; }
    public double IFScore { get; set; }
    public bool IsAnomaly { get; set; }
    public int IncidentCount { get; set; }
    public int BaselineIncidentCount { get; set; }
    public List<FeatureContributionDto> TopFeatures { get; set; } = new();
    public Dictionary<string, double> GroupBreakdown { get; set; } = new();
}

public class FeatureContributionDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty; // raw, self, peer, dept_ctx
    public double ShapValue { get; set; }              // positive = risk ↑
    public string Direction { get; set; } = string.Empty;
    public double ActualValue { get; set; }
}

public class IsolationForestStatusDto
{
    public DateTime? LastRunAt { get; set; }
    public int LastUserCount { get; set; }
    public string Status { get; set; } = "idle"; // idle, running, completed, failed
    public string? LastError { get; set; }
    public bool IsRunning { get; set; }
}

public class IsolationForestOverviewDto
{
    public IsolationForestStatusDto Status { get; set; } = new();
    public List<IsolationForestScoreDto> UserScores { get; set; } = new();
    public int TotalUsers { get; set; }
    public int AnomalyCount { get; set; }
    public int ScoreWindowDays { get; set; } = 7;
    public DateTime? ScoreWindowStart { get; set; }
    public DateTime? ScoreWindowEnd { get; set; }
    public string BaselineStrategy { get; set; } = "all_time_before_score_window";
    public List<DepartmentIFRiskDto> DepartmentRisks { get; set; } = new();
}

public class DepartmentIFRiskDto
{
    public string Department { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int AnomalyCount { get; set; }
    public double MeanScore { get; set; }
    public double MaxScore { get; set; }
}
