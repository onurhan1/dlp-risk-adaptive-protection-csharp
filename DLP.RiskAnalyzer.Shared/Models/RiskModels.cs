namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// User risk trend modeli
/// </summary>
public class UserRiskTrend
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int TotalIncidents { get; set; }
    public int RiskScore { get; set; }
    public string TrendDirection { get; set; } = "stable"; // "up", "down", "stable"
}

/// <summary>
/// Department summary modeli
/// </summary>
public class DepartmentSummary
{
    public int Id { get; set; }
    public string Department { get; set; } = string.Empty;
    public int TotalIncidents { get; set; }
    public int HighRiskCount { get; set; }
    public double AvgRiskScore { get; set; }
    public int UniqueUsers { get; set; }
    public DateOnly? Date { get; set; }
}

/// <summary>
/// Daily summary modeli
/// </summary>
public class DailySummary
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int TotalIncidents { get; set; }
    public int HighRiskCount { get; set; }
    public double AvgRiskScore { get; set; }
    public int UniqueUsers { get; set; }
    public int DepartmentsAffected { get; set; }
}

/// <summary>
/// Risk heatmap data
/// </summary>
public class RiskHeatmapData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Values { get; set; } = new();
    public string Dimension { get; set; } = string.Empty;
    public Dictionary<string, string> DateRange { get; set; } = new();
}
