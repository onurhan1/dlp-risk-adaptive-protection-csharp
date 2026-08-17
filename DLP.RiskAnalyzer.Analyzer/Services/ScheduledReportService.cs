using System.Net;
using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ScheduledReportService : IScheduledReportService
{
    private readonly AnalyzerDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ScheduledReportService> _logger;

    public ScheduledReportService(
        AnalyzerDbContext context,
        IEmailService emailService,
        ILogger<ScheduledReportService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ScheduledReportSendResult> SendReportAsync(string reportType, ScheduledReportOptions options, CancellationToken ct = default)
    {
        var recipient = await ResolveRecipientAsync(options.RecipientEmail, ct);
        if (string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("Rapor alicisi bulunamadi. Job uzerinde alici girin veya Ayarlar > Yonetici E-postasi alanini doldurun.");

        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(-Math.Max(1, options.LookbackDays));
        var end = now;

        var report = reportType switch
        {
            ScheduledReportTypes.WeeklyHighScoreUsers => await BuildWeeklyHighScoreUsersAsync(start, end, options, ct),
            ScheduledReportTypes.TopPermitUsers => await BuildTopActionUsersAsync(start, end, options, "permit", ct),
            ScheduledReportTypes.TopBlockUsers => await BuildTopActionUsersAsync(start, end, options, "block", ct),
            ScheduledReportTypes.HighMaxMatchTransfers => await BuildHighMaxMatchTransfersAsync(start, end, options, ct),
            _ => throw new InvalidOperationException($"Desteklenmeyen rapor tipi: {reportType}")
        };

        var subject = $"{report.Title} - {start:dd.MM.yyyy} / {end:dd.MM.yyyy}";
        var body = BuildMailHtml(report.Title, report.Description, start, end, report.Headers, report.Rows);
        var sent = await _emailService.SendEmailAsync(recipient, subject, body, isHtml: true, ccEmail: options.CcEmail);

        if (!sent)
            throw new InvalidOperationException("Rapor maili gonderilemedi. SMTP ayarlarini ve servis hesabi bilgilerini kontrol edin.");

        _logger.LogInformation("Scheduled report {ReportType} sent to {Recipient} ({Rows} rows)", reportType, recipient, report.Rows.Count);

        return new ScheduledReportSendResult
        {
            Sent = true,
            ReportType = reportType,
            RecipientEmail = recipient,
            Subject = subject,
            RowCount = report.Rows.Count,
            Message = $"{report.Title} gonderildi ({report.Rows.Count} kayit)"
        };
    }

    private async Task<ScheduledReportData> BuildWeeklyHighScoreUsersAsync(DateTime start, DateTime end, ScheduledReportOptions options, CancellationToken ct)
    {
        var startDate = DateOnly.FromDateTime(start);
        var endDate = DateOnly.FromDateTime(end);

        var rows = await _context.UserDailyRiskScores
            .AsNoTracking()
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .GroupBy(s => s.UserEmail)
            .Select(g => new
            {
                UserEmail = g.Key,
                FullName = g.Max(s => s.FullName),
                Team = g.Max(s => s.Team),
                MaxScore = g.Max(s => s.DailyRiskScore),
                AvgScore = g.Average(s => s.DailyRiskScore),
                IncidentCount = g.Sum(s => s.IncidentCount),
                BlockCount = g.Sum(s => s.BlockCount),
                PermitCount = g.Sum(s => s.PermitCount),
                MaxMatches = g.Max(s => s.MaxMaxMatches)
            })
            .Where(x => x.MaxScore >= options.MinRiskScore)
            .OrderByDescending(x => x.MaxScore)
            .ThenByDescending(x => x.IncidentCount)
            .Take(Math.Clamp(options.TopLimit, 1, 200))
            .ToListAsync(ct);

        return new ScheduledReportData(
            "Haftalik Incelenmesi Tavsiye Edilen Yuksek Skorlu Kullanicilar",
            $"Haftalik bazda maksimum risk skoru {options.MinRiskScore} ve uzeri olan kullanicilar.",
            ["Kullanici", "E-posta", "Ekip", "Max Skor", "Ort. Skor", "Incident", "Block", "Permit", "Max Match"],
            rows.Select(x => new[]
            {
                DisplayName(x.FullName, x.UserEmail),
                x.UserEmail,
                x.Team ?? "-",
                x.MaxScore.ToString("N1"),
                x.AvgScore.ToString("N1"),
                x.IncidentCount.ToString("N0"),
                x.BlockCount.ToString("N0"),
                x.PermitCount.ToString("N0"),
                x.MaxMatches.ToString("N0")
            }).ToList());
    }

    private async Task<ScheduledReportData> BuildTopActionUsersAsync(DateTime start, DateTime end, ScheduledReportOptions options, string actionKind, CancellationToken ct)
    {
        var query = _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end);

        query = actionKind == "permit"
            ? query.Where(i => i.Action != null && (i.Action.ToUpper().Contains("PERMIT") || i.Action.ToUpper().Contains("AUTHORIZE") || i.Action.ToUpper().Contains("ALLOW")))
            : query.Where(i => i.Action != null && i.Action.ToUpper().Contains("BLOCK"));

        var rows = await query
            .GroupBy(i => i.UserEmail)
            .Select(g => new
            {
                UserEmail = g.Key,
                FullName = g.Max(i => i.FullName),
                Team = g.Max(i => i.Team ?? i.Department),
                Count = g.Count(),
                MaxRiskScore = g.Max(i => i.RiskScore ?? 0),
                MaxMatches = g.Max(i => i.MaxMatches),
                LastIncident = g.Max(i => i.Timestamp)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.MaxMatches)
            .Take(Math.Clamp(options.TopLimit, 1, 200))
            .ToListAsync(ct);

        var title = actionKind == "permit"
            ? "Haftalik En Cok Permit Incident Ureten Kullanicilar"
            : "Haftalik En Cok Block Incident Ureten Kullanicilar";

        return new ScheduledReportData(
            title,
            "Haftalik incident adedine gore kullanici siralamasi.",
            ["Kullanici", "E-posta", "Ekip", "Incident", "Max Risk", "Max Match", "Son Olay"],
            rows.Select(x => new[]
            {
                DisplayName(x.FullName, x.UserEmail),
                x.UserEmail,
                x.Team ?? "-",
                x.Count.ToString("N0"),
                x.MaxRiskScore.ToString("N0"),
                x.MaxMatches.ToString("N0"),
                x.LastIncident.ToString("dd.MM.yyyy HH:mm")
            }).ToList());
    }

    private async Task<ScheduledReportData> BuildHighMaxMatchTransfersAsync(DateTime start, DateTime end, ScheduledReportOptions options, CancellationToken ct)
    {
        var rows = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end && i.MaxMatches >= options.MaxMatchThreshold)
            .OrderByDescending(i => i.MaxMatches)
            .ThenByDescending(i => i.Timestamp)
            .Take(Math.Clamp(options.TopLimit, 1, 200))
            .Select(i => new
            {
                i.Timestamp,
                i.UserEmail,
                i.FullName,
                Team = i.Team ?? i.Department,
                i.Action,
                i.Channel,
                i.Policy,
                i.RuleName,
                i.Destination,
                i.FileName,
                i.MaxMatches,
                RiskScore = i.RiskScore ?? 0
            })
            .ToListAsync(ct);

        return new ScheduledReportData(
            "Haftalik Tek Seferde Yuksek Max Match Veri Gonderimleri",
            $"Max Match alt siniri {options.MaxMatchThreshold} ve uzeri olan tekil olaylar.",
            ["Tarih", "Kullanici", "E-posta", "Aksiyon", "Kanal", "Policy / Rule", "Hedef", "Dosya", "Max Match", "Risk"],
            rows.Select(x => new[]
            {
                x.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                DisplayName(x.FullName, x.UserEmail),
                x.UserEmail,
                x.Action ?? "-",
                x.Channel ?? "-",
                $"{x.Policy ?? "-"} / {x.RuleName ?? "-"}",
                x.Destination ?? "-",
                x.FileName ?? "-",
                x.MaxMatches.ToString("N0"),
                x.RiskScore.ToString("N0")
            }).ToList());
    }

    private async Task<string?> ResolveRecipientAsync(string? requested, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return requested.Trim();

        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "admin_email", ct);

        return setting?.Value;
    }

    private static string BuildMailHtml(string title, string description, DateTime start, DateTime end, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var headerCells = string.Join("", headers.Select(h => $"<th>{Encode(h)}</th>"));
        var bodyRows = rows.Count == 0
            ? $"<tr><td colspan=\"{headers.Count}\" class=\"empty\">Kayit bulunamadi.</td></tr>"
            : string.Join("", rows.Select(row => $"<tr>{string.Join("", row.Select(cell => $"<td>{Encode(cell)}</td>"))}</tr>"));

        return $@"
<html>
<head>
  <style>
    body {{ font-family: Arial, sans-serif; color: #0f172a; background: #f8fafc; }}
    .wrap {{ max-width: 1100px; margin: 0 auto; padding: 20px; }}
    .header {{ background: #0f172a; color: #fff; padding: 18px 20px; border-radius: 8px 8px 0 0; }}
    .content {{ background: #fff; padding: 18px 20px; border: 1px solid #e2e8f0; border-top: 0; border-radius: 0 0 8px 8px; }}
    h1 {{ margin: 0; font-size: 20px; }}
    .meta {{ color: #64748b; margin: 8px 0 16px; }}
    table {{ width: 100%; border-collapse: collapse; font-size: 12px; }}
    th {{ text-align: left; background: #f1f5f9; border-bottom: 1px solid #cbd5e1; padding: 8px; }}
    td {{ border-bottom: 1px solid #e2e8f0; padding: 8px; vertical-align: top; }}
    .empty {{ text-align: center; color: #64748b; padding: 24px; }}
    .footer {{ color: #64748b; font-size: 11px; margin-top: 16px; }}
  </style>
</head>
<body>
  <div class=""wrap"">
    <div class=""header""><h1>{Encode(title)}</h1></div>
    <div class=""content"">
      <div class=""meta"">{Encode(description)}<br/>Donem: {start:dd.MM.yyyy HH:mm} - {end:dd.MM.yyyy HH:mm}</div>
      <table>
        <thead><tr>{headerCells}</tr></thead>
        <tbody>{bodyRows}</tbody>
      </table>
      <div class=""footer"">Bu rapor DLP Risk Radar zamanlanmis isleri tarafindan otomatik uretilmistir.</div>
    </div>
  </div>
</body>
</html>";
    }

    private static string DisplayName(string? fullName, string fallback) =>
        string.IsNullOrWhiteSpace(fullName) ? fallback : fullName;

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}

public static class ScheduledReportTypes
{
    public const string WeeklyHighScoreUsers = "report_weekly_high_score_users";
    public const string TopPermitUsers = "report_top_permit_users";
    public const string TopBlockUsers = "report_top_block_users";
    public const string HighMaxMatchTransfers = "report_high_max_match_transfers";
}

public record ScheduledReportData(
    string Title,
    string Description,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> Rows);
