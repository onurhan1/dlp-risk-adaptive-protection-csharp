using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ReportGeneratorService
{
    public byte[] GenerateDailyReport(DateTime reportDate, object? data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);

                page.Header().Text("Forcepoint DLP Risk Adaptive Protection - Daily Report")
                    .FontSize(20).Bold();

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    col.Item().Text($"Report Date: {reportDate:yyyy-MM-dd}");
                    col.Item().Text("This is a placeholder report.");
                    // Add actual report content here
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ").FontSize(10);
                    text.Span($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(10).Bold();
                });
            });
        }).GeneratePdf();
    }
}

