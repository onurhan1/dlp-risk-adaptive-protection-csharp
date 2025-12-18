using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ReportGeneratorService
{
    private readonly RiskAnalyzerService _riskAnalyzerService;

    public ReportGeneratorService(RiskAnalyzerService riskAnalyzerService)
    {
        _riskAnalyzerService = riskAnalyzerService;
    }

    public byte[] GenerateDailyReport(DateTime reportDate, object? data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Header
                page.Header().Element(ComposeHeader);

                // Content
                page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(15);
                    
                    col.Item().Text($"Report Date: {reportDate:yyyy-MM-dd}")
                        .FontSize(14).SemiBold();
                    
                    if (data is Dictionary<string, object> reportData)
                    {
                        // Action Summary Section
                        if (reportData.TryGetValue("action_summary", out var actionSummaryObj) && 
                            actionSummaryObj is Dictionary<string, object> actionSummary)
                        {
                            col.Item().Element(c => ComposeActionSummary(c, actionSummary));
                        }

                        // Top Users Section
                        if (reportData.TryGetValue("top_users", out var topUsersObj) && 
                            topUsersObj is List<Dictionary<string, object>> topUsers)
                        {
                            col.Item().Element(c => ComposeTopUsers(c, topUsers));
                        }

                        // Top Policies Section
                        if (reportData.TryGetValue("top_policies", out var topPoliciesObj) && 
                            topPoliciesObj is List<Dictionary<string, object>> topPolicies)
                        {
                            col.Item().Element(c => ComposeTopPolicies(c, topPolicies));
                        }

                        // Channel Breakdown Section
                        if (reportData.TryGetValue("channel_breakdown", out var channelObj) && 
                            channelObj is List<Dictionary<string, object>> channels)
                        {
                            col.Item().Element(c => ComposeChannelBreakdown(c, channels));
                        }

                        // Top Destinations Section
                        if (reportData.TryGetValue("top_destinations", out var destObj) && 
                            destObj is List<Dictionary<string, object>> destinations)
                        {
                            col.Item().Element(c => ComposeTopDestinations(c, destinations));
                        }
                    }
                    else
                    {
                        col.Item().Text("No data available for this report date.")
                            .FontSize(12).Italic();
                    }
                });

                // Footer
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    public async Task<byte[]> GenerateDailySummaryReportAsync(DateTime reportDate)
    {
        var reportData = await _riskAnalyzerService.GetDailyReportDataAsync(reportDate);
        return GenerateDailyReport(reportDate, reportData);
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Forcepoint DLP Risk Adaptive Protection")
                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().Text("Daily Security Report")
                    .FontSize(14).SemiBold().FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated on ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.Span($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(9).Bold();
            text.Span(" | Page ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(9);
            text.Span(" of ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(9);
        });
    }

    private void ComposeActionSummary(IContainer container, Dictionary<string, object> actionSummary)
    {
        container.Column(col =>
        {
            col.Item().Text("Action Summary").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(10).Row(row =>
            {
                var authorized = Convert.ToInt32(actionSummary.GetValueOrDefault("authorized", 0));
                var block = Convert.ToInt32(actionSummary.GetValueOrDefault("block", 0));
                var quarantine = Convert.ToInt32(actionSummary.GetValueOrDefault("quarantine", 0));
                var total = Convert.ToInt32(actionSummary.GetValueOrDefault("total", 0));

                row.RelativeItem().Element(c => ComposeActionCard(c, "AUTHORIZED", authorized, Colors.Green.Darken1));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => ComposeActionCard(c, "BLOCK", block, Colors.Red.Darken1));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => ComposeActionCard(c, "QUARANTINE", quarantine, Colors.Purple.Darken1));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => ComposeActionCard(c, "TOTAL", total, Colors.Blue.Darken2));
            });
        });
    }

    private void ComposeActionCard(IContainer container, string label, int value, string color)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4)
            .Padding(10).Column(col =>
            {
                col.Item().AlignCenter().Text(label).FontSize(10).FontColor(Colors.Grey.Darken2);
                col.Item().AlignCenter().Text(value.ToString()).FontSize(20).Bold().FontColor(color);
            });
    }

    private void ComposeTopUsers(IContainer container, List<Dictionary<string, object>> topUsers)
    {
        container.Column(col =>
        {
            col.Item().Text("Top 10 Users by Incident Count").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#").Bold();
                    header.Cell().Element(CellStyle).Text("User").Bold();
                    header.Cell().Element(CellStyle).Text("Department").Bold();
                    header.Cell().Element(CellStyle).Text("Risk Score").Bold();
                    header.Cell().Element(CellStyle).Text("Incidents").Bold();
                });

                // Data rows
                for (int i = 0; i < topUsers.Count; i++)
                {
                    var user = topUsers[i];
                    var riskScore = Convert.ToInt32(user.GetValueOrDefault("risk_score", 0));
                    var riskColor = GetRiskColor(riskScore);

                    table.Cell().Element(CellStyle).Text((i + 1).ToString());
                    table.Cell().Element(CellStyle).Text(user.GetValueOrDefault("login_name", "")?.ToString() ?? "");
                    table.Cell().Element(CellStyle).Text(user.GetValueOrDefault("department", "")?.ToString() ?? "");
                    table.Cell().Element(CellStyle).Text(riskScore.ToString()).FontColor(riskColor).Bold();
                    table.Cell().Element(CellStyle).Text(user.GetValueOrDefault("total_alerts", 0)?.ToString() ?? "0");
                }
            });
        });
    }

    private void ComposeTopPolicies(IContainer container, List<Dictionary<string, object>> topPolicies)
    {
        container.Column(col =>
        {
            col.Item().Text("Top 10 Policies with Top 3 Rules").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(10);

            foreach (var policy in topPolicies)
            {
                var policyName = policy.GetValueOrDefault("policy_name", "")?.ToString() ?? "";
                var totalAlerts = Convert.ToInt32(policy.GetValueOrDefault("total_alerts", 0));
                var topRules = policy.GetValueOrDefault("top_rules") as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();

                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(policyCol =>
                {
                    policyCol.Item().Row(r =>
                    {
                        r.RelativeItem().Text(policyName).FontSize(11).SemiBold();
                        r.ConstantItem(80).AlignRight().Text($"{totalAlerts} alerts").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    if (topRules.Count > 0)
                    {
                        policyCol.Item().PaddingLeft(15).PaddingTop(5).Column(rulesCol =>
                        {
                            foreach (var rule in topRules)
                            {
                                var ruleName = rule.GetValueOrDefault("rule_name", "")?.ToString() ?? "";
                                var alertCount = Convert.ToInt32(rule.GetValueOrDefault("alert_count", 0));
                                rulesCol.Item().Row(r =>
                                {
                                    r.ConstantItem(10).Text("•").FontColor(Colors.Grey.Medium);
                                    r.RelativeItem().Text(ruleName).FontSize(9);
                                    r.ConstantItem(50).AlignRight().Text($"{alertCount}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                    }
                });
                col.Item().PaddingTop(5);
            }
        });
    }

    private void ComposeChannelBreakdown(IContainer container, List<Dictionary<string, object>> channels)
    {
        container.Column(col =>
        {
            col.Item().Text("Channel Breakdown").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("Channel").Bold();
                    header.Cell().Element(CellStyle).Text("Alerts").Bold();
                    header.Cell().Element(CellStyle).Text("Percentage").Bold();
                });

                // Data rows
                foreach (var channel in channels)
                {
                    table.Cell().Element(CellStyle).Text(channel.GetValueOrDefault("channel", "")?.ToString() ?? "");
                    table.Cell().Element(CellStyle).Text(channel.GetValueOrDefault("total_alerts", 0)?.ToString() ?? "0");
                    table.Cell().Element(CellStyle).Text($"{channel.GetValueOrDefault("percentage", 0)}%");
                }
            });
        });
    }

    private void ComposeTopDestinations(IContainer container, List<Dictionary<string, object>> destinations)
    {
        container.Column(col =>
        {
            col.Item().Text("Top 10 Destinations").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#").Bold();
                    header.Cell().Element(CellStyle).Text("Destination").Bold();
                    header.Cell().Element(CellStyle).Text("Alerts").Bold();
                });

                // Data rows
                for (int i = 0; i < destinations.Count; i++)
                {
                    var dest = destinations[i];
                    table.Cell().Element(CellStyle).Text((i + 1).ToString());
                    table.Cell().Element(CellStyle).Text(dest.GetValueOrDefault("destination", "")?.ToString() ?? "");
                    table.Cell().Element(CellStyle).Text(dest.GetValueOrDefault("total_alerts", 0)?.ToString() ?? "0");
                }
            });
        });
    }

    private IContainer CellStyle(IContainer container)
    {
        return container.Padding(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
    }

    private string GetRiskColor(int score)
    {
        if (score >= 91) return Colors.Red.Darken1;
        if (score >= 61) return Colors.Orange.Darken1;
        if (score >= 41) return Colors.Yellow.Darken2;
        return Colors.Green.Darken1;
    }
}
