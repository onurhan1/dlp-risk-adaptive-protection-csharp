namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IScheduledReportService
{
    Task<ScheduledReportSendResult> SendReportAsync(string reportType, ScheduledReportOptions options, CancellationToken ct = default);
}

public class ScheduledReportOptions
{
    public string? RecipientEmail { get; set; }
    public string? CcEmail { get; set; }
    public int LookbackDays { get; set; } = 7;
    public int TopLimit { get; set; } = 25;
    public int MinRiskScore { get; set; } = 80;
    public int MaxMatchThreshold { get; set; } = 300;
}

public class ScheduledReportSendResult
{
    public bool Sent { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
