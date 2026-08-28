using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Interface for PDF/Excel report generation.
/// </summary>
public interface IReportGeneratorService
{
    byte[] GenerateDailyReport(DateTime reportDate, object? data);

    Task<byte[]> GenerateDailySummaryReportAsync(DateTime reportDate);

    byte[] GenerateDashboardSummaryReport(DashboardReportRequest request);

    byte[] GenerateWorkflowTableReport(WorkflowTableReport report);
}

public sealed record WorkflowTableReport(
    string Title,
    string? Intro,
    DateTime GeneratedAt,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);
