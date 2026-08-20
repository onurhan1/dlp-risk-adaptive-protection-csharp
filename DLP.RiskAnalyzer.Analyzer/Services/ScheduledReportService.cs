using System.Net;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ScheduledReportService : IScheduledReportService
{
    private const int ReportRowCap = 500_000;

    private readonly AnalyzerDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IExternalUserDirectoryService _externalUserDirectory;
    private readonly ILogger<ScheduledReportService> _logger;

    public ScheduledReportService(
        AnalyzerDbContext context,
        IEmailService emailService,
        IExternalUserDirectoryService externalUserDirectory,
        ILogger<ScheduledReportService> logger)
    {
        _context = context;
        _emailService = emailService;
        _externalUserDirectory = externalUserDirectory;
        _logger = logger;
    }

    public async Task<ScheduledReportSendResult> SendReportAsync(string reportType, ScheduledReportOptions options, CancellationToken ct = default)
    {
        var recipient = await ResolveRecipientAsync(options.RecipientEmail, ct);
        if (string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("Rapor alıcısı bulunamadı. Job üzerinde alıcı girin veya Ayarlar > Yönetici E-postası alanını doldurun.");

        var nowTurkey = RadarTimeZone.NowTurkey();
        var startTurkey = nowTurkey.Date.AddDays(-Math.Max(1, options.LookbackDays));
        var endTurkey = nowTurkey;
        var start = startTurkey;
        var end = endTurkey;

        var report = reportType switch
        {
            ScheduledReportTypes.WeeklyHighScoreUsers => await BuildWeeklyHighScoreUsersAsync(start, end, options, ct),
            ScheduledReportTypes.TopPermitUsers => await BuildTopActionUsersAsync(start, end, options, "permit", ct),
            ScheduledReportTypes.TopBlockUsers => await BuildTopActionUsersAsync(start, end, options, "block", ct),
            ScheduledReportTypes.HighMaxMatchTransfers => await BuildHighMaxMatchTransfersAsync(start, end, options, ct),
            _ => throw new InvalidOperationException($"Desteklenmeyen rapor tipi: {reportType}")
        };

        var subject = $"{report.Title} - {startTurkey:dd.MM.yyyy} / {endTurkey:dd.MM.yyyy}";
        var body = BuildMailHtml(report.Title, report.Description, startTurkey, endTurkey, report.Headers, report.Rows);
        var sent = await _emailService.SendEmailAsync(recipient, subject, body, isHtml: true, ccEmail: options.CcEmail);

        if (!sent)
            throw new InvalidOperationException("Rapor maili gönderilemedi. SMTP ayarlarını ve servis hesabı bilgilerini kontrol edin.");

        _logger.LogInformation("Scheduled report {ReportType} sent to {Recipient} ({Rows} rows)", reportType, recipient, report.Rows.Count);

        return new ScheduledReportSendResult
        {
            Sent = true,
            ReportType = reportType,
            RecipientEmail = recipient,
            Subject = subject,
            RowCount = report.Rows.Count,
            Message = $"{report.Title} gönderildi ({report.Rows.Count} kayıt)"
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

        var reportRows = new List<string[]>(rows.Count);
        foreach (var x in rows)
        {
            var user = await ResolveReportUserAsync(x.UserEmail, x.UserEmail, x.Team, ct);
            reportRows.Add([
                DisplayName(user.FullName, x.UserEmail),
                user.Email,
                user.Team ?? "-",
                x.MaxScore.ToString("N1"),
                x.AvgScore.ToString("N1"),
                x.IncidentCount.ToString("N0"),
                x.BlockCount.ToString("N0"),
                x.PermitCount.ToString("N0"),
                x.MaxMatches.ToString("N0")
            ]);
        }

        return new ScheduledReportData(
            "Haftalık İncelenmesi Tavsiye Edilen Yüksek Skorlu Kullanıcılar",
            $"Haftalık bazda maksimum risk skoru {options.MinRiskScore} ve üzeri olan kullanıcılar.",
            ["Kullanıcı", "E-posta", "Ekip", "Maksimum Skor", "Ortalama Skor", "Olay Kaydı", "Block", "Permit", "Maksimum Eşleşme"],
            reportRows);
    }

    private async Task<ScheduledReportData> BuildTopActionUsersAsync(DateTime start, DateTime end, ScheduledReportOptions options, string actionKind, CancellationToken ct)
    {
        var query = _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end);

        query = actionKind == "permit"
            ? query.Where(i => i.Action != null && (i.Action.ToUpper().Contains("PERMIT") || i.Action.ToUpper().Contains("AUTHORIZE") || i.Action.ToUpper().Contains("ALLOW")))
            : query.Where(i => i.Action != null && i.Action.ToUpper().Contains("BLOCK"));

        var incidents = await query
            .OrderByDescending(i => i.Timestamp)
            .Take(ReportRowCap)
            .ToListAsync(ct);

        var rows = incidents
            .GroupBy(i => i.UserEmail, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var topIncident = g.OrderByDescending(EffectiveMaxMatches).ThenByDescending(i => i.Timestamp).First();
                return new
                {
                    UserEmail = g.Key,
                    Team = g.Max(i => i.Team ?? i.Department),
                    Destination = topIncident.Destination,
                    MaxRiskScore = g.Max(i => i.RiskScore ?? 0),
                    MaxMatches = EffectiveMaxMatches(topIncident),
                    LastIncident = g.Max(i => i.Timestamp)
                };
            })
            .OrderByDescending(x => x.MaxMatches)
            .ThenByDescending(x => x.LastIncident)
            .Take(Math.Clamp(options.TopLimit, 1, 200))
            .Select(x => new TopActionReportRow(x.UserEmail, x.Team, x.Destination, x.MaxRiskScore, x.MaxMatches, x.LastIncident))
            .ToList();

        var title = actionKind == "permit"
            ? "Haftalık En Çok Permit Olay Kaydı Üreten Kullanıcılar"
            : "Haftalık En Çok Block Olay Kaydı Üreten Kullanıcılar";

        return new ScheduledReportData(
            title,
            "Haftalık maksimum eşleşme sayısına göre kullanıcı sıralaması.",
            ["Kullanıcı", "E-posta", "Ekip", "Hedef", "Maksimum Risk", "Maksimum Eşleşme", "Son Olay"],
            await BuildTopActionReportRowsAsync(rows, ct));
    }

    private async Task<ScheduledReportData> BuildHighMaxMatchTransfersAsync(DateTime start, DateTime end, ScheduledReportOptions options, CancellationToken ct)
    {
        var incidents = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= start && i.Timestamp <= end)
            .OrderByDescending(i => i.Timestamp)
            .Take(ReportRowCap)
            .ToListAsync(ct);
        var rows = incidents
            .Select(i => new
            {
                Incident = i,
                MaxMatches = EffectiveMaxMatches(i)
            })
            .Where(x => x.MaxMatches >= options.MaxMatchThreshold)
            .OrderByDescending(x => x.MaxMatches)
            .ThenByDescending(x => x.Incident.Timestamp)
            .Take(Math.Clamp(options.TopLimit, 1, 200))
            .Select(x => new HighMaxMatchReportRow(
                x.Incident.Timestamp,
                x.Incident.UserEmail,
                x.Incident.Team ?? x.Incident.Department,
                x.Incident.Action,
                x.Incident.Channel,
                x.Incident.Policy,
                x.Incident.RuleName,
                x.Incident.Destination,
                x.Incident.FileName,
                x.MaxMatches,
                x.Incident.RiskScore ?? 0))
            .ToList();

        return new ScheduledReportData(
            "Haftalık Tek Seferde Yüksek Eşleşmeli Veri Gönderimleri",
            $"Maksimum eşleşme alt sınırı {options.MaxMatchThreshold} ve üzeri olan tekil olaylar.",
            ["Tarih", "Kullanıcı", "E-posta", "Aksiyon", "Kanal", "Politika / Kural", "Hedef", "Dosya Adı", "Maksimum Eşleşme", "Risk Skoru"],
            await BuildHighMaxMatchReportRowsAsync(rows, ct));
    }

    private async Task<List<string[]>> BuildTopActionReportRowsAsync(IReadOnlyList<TopActionReportRow> rows, CancellationToken ct)
    {
        var result = new List<string[]>(rows.Count);
        foreach (var x in rows)
        {
            var user = await ResolveReportUserAsync(x.UserEmail, x.UserEmail, x.Team, ct);
            result.Add([
                DisplayName(user.FullName, x.UserEmail),
                user.Email,
                user.Team ?? "-",
                x.Destination ?? "-",
                x.MaxRiskScore.ToString("N0"),
                x.MaxMatches.ToString("N0"),
                x.LastIncident.ToString("dd.MM.yyyy HH:mm")
            ]);
        }

        return result;
    }

    private async Task<List<string[]>> BuildHighMaxMatchReportRowsAsync(IReadOnlyList<HighMaxMatchReportRow> rows, CancellationToken ct)
    {
        var result = new List<string[]>(rows.Count);
        foreach (var x in rows)
        {
            var user = await ResolveReportUserAsync(x.UserEmail, x.UserEmail, x.Team, ct);
            result.Add([
                x.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                DisplayName(user.FullName, x.UserEmail),
                user.Email,
                x.Action ?? "-",
                x.Channel ?? "-",
                $"{x.Policy ?? "-"} / {x.RuleName ?? "-"}",
                x.Destination ?? "-",
                x.FileName ?? "-",
                x.MaxMatches.ToString("N0"),
                x.RiskScore.ToString("N0")
            ]);
        }

        return result;
    }

    private async Task<(string? FullName, string Email, string? Team)> ResolveReportUserAsync(
        string userEmail,
        string? fallbackEmail,
        string? fallbackTeam,
        CancellationToken ct)
    {
        var profile = await _externalUserDirectory.ResolveUserAsync(userEmail, ct);
        return (
            profile?.FullName,
            FirstNonEmpty(profile?.Email, fallbackEmail, userEmail) ?? userEmail,
            FirstNonEmpty(profile?.Department, fallbackTeam));
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
            ? $"<tr><td colspan=\"{headers.Count}\" class=\"empty\">Kayıt bulunamadı.</td></tr>"
            : string.Join("", rows.Select(row => $"<tr>{string.Join("", row.Select(cell => $"<td>{Encode(cell)}</td>"))}</tr>"));
        var introHtml = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $@"<div class=""intro"">{Encode(description)}</div>";

        return $@"
<html>
<head>
  <style>
    body {{ margin: 0; font-family: Arial, sans-serif; color: #0f172a; background: #ffffff; }}
    .wrap {{ width: 100%; max-width: none; margin: 0; padding: 0; }}
    .header {{ background: #eef4ff; color: #111827; padding: 14px 16px; border-bottom: 2px solid #bfdbfe; }}
    .content {{ background: #fff; padding: 18px 16px 20px; border: 0; }}
    h1 {{ margin: 0; font-size: 20px; }}
    .intro {{ color: #334155; margin: 0 0 12px; line-height: 1.55; }}
    .meta {{ color: #475569; margin: 0 0 18px; line-height: 1.55; background: #f8fafc; border-left: 3px solid #bfdbfe; padding: 8px 10px; }}
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
      {introHtml}
      <div class=""meta"">Dönem: {start:dd.MM.yyyy HH:mm} - {end:dd.MM.yyyy HH:mm} ({RadarTimeZone.DisplayName})</div>
      <table>
        <thead><tr>{headerCells}</tr></thead>
        <tbody>{bodyRows}</tbody>
      </table>
      <div class=""footer"">Bu rapor DLP Risk Radar zamanlanmış işleri tarafından otomatik üretilmiştir.</div>
    </div>
  </div>
</body>
</html>";
    }

    private static string DisplayName(string? fullName, string fallback) =>
        string.IsNullOrWhiteSpace(fullName) ? fallback : fullName;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int EffectiveMaxMatches(DLP.RiskAnalyzer.Shared.Models.Incident incident)
    {
        if (incident.MaxMatches > 0) return incident.MaxMatches;
        return ViolationTriggerParser.ExtractMaxMatches(incident.ViolationTriggers);
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}

public record TopActionReportRow(
    string UserEmail,
    string? Team,
    string? Destination,
    int MaxRiskScore,
    int MaxMatches,
    DateTime LastIncident);

public record HighMaxMatchReportRow(
    DateTime Timestamp,
    string UserEmail,
    string? Team,
    string? Action,
    string? Channel,
    string? Policy,
    string? RuleName,
    string? Destination,
    string? FileName,
    int MaxMatches,
    int RiskScore);

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
