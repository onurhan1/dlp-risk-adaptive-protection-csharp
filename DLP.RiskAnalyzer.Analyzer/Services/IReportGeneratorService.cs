namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Interface for PDF/Excel report generation.
/// </summary>
public interface IReportGeneratorService
{
    Task<byte[]> GenerateDailySummaryReportAsync(DateTime reportDate);
}
