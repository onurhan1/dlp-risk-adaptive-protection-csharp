namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// Snapshot of the dashboard grids that the user is currently looking at.
/// The dashboard posts what it rendered — including each grid's own period and
/// action filter — so the printed record matches the screen exactly.
/// </summary>
public class DashboardReportRequest
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? GeneratedBy { get; set; }
    public List<DashboardReportSection> Sections { get; set; } = new();
}

public class DashboardReportSection
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Human readable filter description, e.g. "Dönem: Son 1 Ay | Aksiyon: BLOCK".</summary>
    public string? Subtitle { get; set; }

    public string LabelHeader { get; set; } = "Ad";
    public string ValueHeader { get; set; } = "Adet";
    public string PercentageHeader { get; set; } = "Oran (%)";

    public List<DashboardReportRow> Rows { get; set; } = new();
}

public class DashboardReportRow
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Percentage { get; set; }
}
