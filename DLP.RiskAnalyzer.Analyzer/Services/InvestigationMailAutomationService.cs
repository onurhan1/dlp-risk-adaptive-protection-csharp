using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IInvestigationMailAutomationService
{
    Task<InvestigationMailAutomationResult> ProcessInboxAsync(CancellationToken ct = default);
    Task<InvestigationMailAutomationResult> MarkUnansweredRemindersAsync(CancellationToken ct = default);
}

public sealed class InvestigationMailAutomationResult
{
    public int Processed { get; set; }
    public int AlreadyProcessed { get; set; }
    public int Retried { get; set; }
    public int RepliesMatched { get; set; }
    public int RemindersSent { get; set; }
    public int MarkedUnanswered { get; set; }
    public List<string> ProcessingResults { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public class InvestigationMailAutomationService : IInvestigationMailAutomationService
{
    private static readonly Regex CorrelationPattern = new(@"\[(RADAR-Q-[A-Z0-9]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"(?<email>[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AnalyzerDbContext _context;
    private readonly IDirectorySettingsService _directorySettings;
    private readonly IEmailService _emailService;
    private readonly IScheduledReportService _scheduledReports;
    private readonly ILogger<InvestigationMailAutomationService> _logger;

    public InvestigationMailAutomationService(
        AnalyzerDbContext context,
        IDirectorySettingsService directorySettings,
        IEmailService emailService,
        IScheduledReportService scheduledReports,
        ILogger<InvestigationMailAutomationService> logger)
    {
        _context = context;
        _directorySettings = directorySettings;
        _emailService = emailService;
        _scheduledReports = scheduledReports;
        _logger = logger;
    }

    public async Task<InvestigationMailAutomationResult> ProcessInboxAsync(CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var result = new InvestigationMailAutomationResult();
        var settings = await _directorySettings.GetImapAsync(ct);
        if (!settings.Enabled || !settings.IsConfigured)
        {
            result.Message = "IMAP otomasyonu yapilandirilmamis";
            return result;
        }

        var inbox = await _directorySettings.PreviewInboxAsync(new ImapInboxRequest
        {
            Host = settings.Host,
            Port = settings.Port,
            EnableSsl = settings.EnableSsl,
            Username = settings.Username,
            Folder = settings.Folder,
            UnreadOnly = false,
            LookbackDays = Math.Max(settings.LookbackDays, 14),
            PreviewCount = Math.Min(settings.MaxMessages, 100)
        }, ct);

        if (!inbox.Success)
        {
            result.Message = inbox.Message;
            return result;
        }

        foreach (var item in inbox.Messages)
        {
            var messageKey = string.IsNullOrWhiteSpace(item.MessageId)
                ? $"{inbox.Folder}:{item.Id}:{item.Date}"
                : item.MessageId.Trim();

            var existingInbound = await _context.InvestigationInboundMails
                .FirstOrDefaultAsync(m => m.MessageKey == messageKey, ct);
            if (existingInbound != null)
            {
                result.AlreadyProcessed++;
                if (CanRetry(existingInbound.ProcessingResult))
                {
                    var retriedResult = await ProcessReportRequestAsync(
                        existingInbound.FromEmail,
                        existingInbound.Subject,
                        existingInbound.BodyPreview ?? string.Empty,
                        ct);
                    existingInbound.ProcessingResult = retriedResult;
                    existingInbound.ProcessedAt = DateTime.UtcNow;
                    result.ProcessingResults.Add($"retry:{retriedResult}");
                    result.Retried++;
                    await _context.SaveChangesAsync(ct);
                }
                continue;
            }

            var content = await _directorySettings.GetInboxMessageAsync(new ImapMessageContentRequest
            {
                Host = settings.Host,
                Port = settings.Port,
                EnableSsl = settings.EnableSsl,
                Username = settings.Username,
                Folder = inbox.Folder,
                MessageId = item.Id
            }, ct);
            if (!content.Success) continue;

            // Some IMAP servers omit the From header in a full-message preview even
            // though it is present in the header-only inbox listing.
            var sender = ExtractEmail(content.From);
            if (!sender.Contains('@'))
                sender = ExtractEmail(item.From);
            var isReportRequest = IsReportRequest(content.Subject, content.BodyText);
            var query = isReportRequest
                ? null
                : await FindQueryAsync(sender, content.Subject, content.BodyText, ct);
            var receivedAt = ParseDate(content.Date);
            var preview = Shorten(content.BodyText, 2000);

            var processingResult = query == null
                ? await ProcessReportRequestAsync(sender, content.Subject, content.BodyText, ct)
                : "reply_matched";
            result.ProcessingResults.Add(processingResult);

            var inbound = new InvestigationInboundMail
            {
                MessageKey = messageKey,
                RfcMessageId = NullIfEmpty(content.MessageId),
                FromEmail = sender,
                Subject = content.Subject ?? string.Empty,
                ReceivedAt = receivedAt,
                BodyPreview = preview,
                InvestigationQueryId = query?.Id,
                ProcessingResult = processingResult,
                ProcessedAt = DateTime.UtcNow
            };
            _context.InvestigationInboundMails.Add(inbound);

            if (query != null)
            {
                query.ReplyReceivedAt = receivedAt ?? DateTime.UtcNow;
                query.ReplyMessageId = NullIfEmpty(content.MessageId) ?? messageKey;
                query.ReplyPreview = preview;
                query.ResponseStatus = "Cevap geldi - inceleme bekliyor";
                query.Action = "Kullanici cevabi analist incelemesini bekliyor";
                query.QueryStatus = InvestigationQueryStatus.ReplyReview;
                query.UpdatedAt = DateTime.UtcNow;
                query.UpdatedBy = "IMAP Otomasyonu";
                result.RepliesMatched++;
            }

            result.Processed++;
            await _context.SaveChangesAsync(ct);
        }

        var outcomes = result.ProcessingResults.Count == 0
            ? "bu calistirmada yeni islem yapilmadi"
            : string.Join(", ", result.ProcessingResults
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(DescribeProcessingResult));
        result.Message = $"{result.Processed} yeni mail islendi, {result.AlreadyProcessed} onceki mail goruldu, " +
                         $"{result.Retried} tekrar denendi, {result.RepliesMatched} cevap sorguyla eslestirildi: {outcomes}";
        return result;
    }

    /// <summary>
    /// Delivery itself belongs to the configurable reminder workflow. The mailbox worker only
    /// advances an already-reminded query when its second seven-day window expires.
    /// </summary>
    public async Task<InvestigationMailAutomationResult> MarkUnansweredRemindersAsync(CancellationToken ct = default)
    {
        await InvestigationQuerySchema.EnsureAsync(_context, _logger, ct);
        var result = new InvestigationMailAutomationResult();
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);
        var unanswered = await _context.InvestigationQueries
            .Where(q => q.QueryStatus == InvestigationQueryStatus.Queried &&
                        q.ReplyReceivedAt == null &&
                        q.ReminderCount > 0 &&
                        q.ReminderSentAt != null &&
                        q.ReminderSentAt <= cutoff)
            .ToListAsync(ct);

        foreach (var query in unanswered)
        {
            query.QueryStatus = InvestigationQueryStatus.ReminderUnanswered;
            query.ResponseStatus = "Hatirlatma sonrasi cevap gelmedi";
            query.Action = "Analist takip veya eskalasyon aksiyonu bekliyor";
            query.UpdatedAt = now;
            query.UpdatedBy = "Hatirlatma Takibi";
            result.MarkedUnanswered++;
        }

        if (unanswered.Count > 0) await _context.SaveChangesAsync(ct);
        result.Message = $"{result.MarkedUnanswered} kayit yanitsiz isaretlendi";
        return result;
    }

    private async Task<InvestigationQueryRecord?> FindQueryAsync(string sender, string subject, string body, CancellationToken ct)
    {
        var correlation = CorrelationPattern.Match($"{subject}\n{body}").Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(correlation))
        {
            var byCode = await _context.InvestigationQueries
                .FirstOrDefaultAsync(q => q.CorrelationCode == correlation, ct);
            if (byCode != null && SenderMatches(sender, byCode.MailAddress)) return byCode;
        }

        var candidates = await _context.InvestigationQueries
            .Where(q => q.ReplyReceivedAt == null &&
                        (q.QueryStatus == InvestigationQueryStatus.Queried || q.QueryStatus == InvestigationQueryStatus.ReminderUnanswered) &&
                        q.MailAddress.ToLower() == sender.ToLower())
            .OrderByDescending(q => q.FirstSentAt ?? q.QueryDate ?? q.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var normalizedSubject = NormalizeSubject(subject);
        var matched = candidates.Where(q => NormalizeSubject(q.Subject) == normalizedSubject).ToList();
        if (matched.Count == 1) return matched[0];

        // Reminder templates may use a different stable subject. If this sender has exactly one
        // unresolved query, it is still safe to associate the reply without showing an internal
        // correlation code in the subject line.
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private async Task<string> ProcessReportRequestAsync(string sender, string subject, string body, CancellationToken ct)
    {
        if (!sender.Contains('@')) return "unmatched";

        // Pass the full address so LDAP can match mail/userPrincipalName as well as
        // the local-part based sAMAccountName lookup.
        var directoryUser = await _directorySettings.LookupLdapUserAsync(sender, ct);
        if (!directoryUser.Success) return "ldap_unverified";
        if (!string.IsNullOrWhiteSpace(directoryUser.Email) && !SenderMatches(sender, directoryUser.Email))
            return "ldap_email_mismatch";

        var username = directoryUser.Username.Trim();
        var approvedUser = await _context.Users.AsNoTracking().AnyAsync(user =>
            user.IsActive &&
            (user.Username.ToLower() == username.ToLower() ||
             user.Email.ToLower() == sender.ToLower()), ct);
        if (!approvedUser) return "user_not_authorized";

        var request = FoldRequest($"{subject}\n{ExtractReplyRequest(body)}");
        if (request.Contains("SORGU RAPORU"))
        {
            var html = request.Contains("HTML");
            var sent = html
                ? await SendQueryReportHtmlAsync(sender, ct)
                : await SendQueryReportExcelAsync(sender, ct);
            return sent ? "query_report_sent" : "query_report_failed";
        }

        var reportType = request.Contains("HAFTALIK PERMIT")
            ? ScheduledReportTypes.TopPermitUsers
            : request.Contains("HAFTALIK BLOCK")
                ? ScheduledReportTypes.TopBlockUsers
                : request.Contains("YUKSEK ESLESME") || request.Contains("MAX MATCH")
                    ? ScheduledReportTypes.HighMaxMatchTransfers
                    : request.Contains("YUKSEK SKOR")
                        ? ScheduledReportTypes.WeeklyHighScoreUsers
                        : null;

        if (reportType != null)
        {
            try
            {
                await _scheduledReports.SendReportAsync(reportType, new ScheduledReportOptions { RecipientEmail = sender }, ct);
                return "report_sent";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Requested report could not be sent to {Sender}", sender);
                return "report_failed";
            }
        }

        var catalogSent = await _emailService.SendEmailAsync(sender, "RADAR Rapor Talep Katalogu", ReportCatalogHtml(), isHtml: true);
        return catalogSent ? "catalog_sent" : "catalog_failed";
    }

    private async Task<bool> SendQueryReportHtmlAsync(string recipient, CancellationToken ct)
    {
        var rows = await _context.InvestigationQueries.AsNoTracking()
            .OrderByDescending(q => q.QueryDate ?? q.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);
        var table = string.Join(string.Empty, rows.Select(q =>
            $"<tr><td>{WebUtility.HtmlEncode(q.UserCode)}</td><td>{WebUtility.HtmlEncode(q.FullName)}</td>" +
            $"<td>{WebUtility.HtmlEncode(q.MailAddress)}</td><td>{WebUtility.HtmlEncode(q.Subject)}</td>" +
            $"<td>{q.QueryDate?.ToString("dd.MM.yyyy HH:mm") ?? "-"}</td><td>{WebUtility.HtmlEncode(q.QueryStatus)}</td></tr>"));
        var body = "<h2>Sorgu Raporu</h2><p>Sorgulamalar ekranindaki tum kayitlar listelenmektedir.</p>" +
                   "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\"><tr><th>Kullanici</th><th>Ad Soyad</th><th>Mail</th><th>Konu</th><th>Tarih</th><th>Durum</th></tr>" +
                   table + "</table>";
        return await _emailService.SendEmailAsync(recipient, "RADAR Sorgu Raporu", body, isHtml: true);
    }

    private async Task<bool> SendQueryReportExcelAsync(string recipient, CancellationToken ct)
    {
        var rows = await _context.InvestigationQueries.AsNoTracking()
            .OrderByDescending(q => q.QueryDate ?? q.CreatedAt)
            .Take(10000)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sorgulamalar");
        var headers = new[] { "Kullanici Kodu", "Ad Soyad", "Mail Adresi", "Konu", "Sorgu Tarihi", "Geri Donus", "Aksiyon", "Durum", "Ekip", "Kaynak", "Notlar" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var excelRow = index + 2;
            sheet.Cell(excelRow, 1).Value = row.UserCode;
            sheet.Cell(excelRow, 2).Value = row.FullName;
            sheet.Cell(excelRow, 3).Value = row.MailAddress;
            sheet.Cell(excelRow, 4).Value = row.Subject;
            sheet.Cell(excelRow, 5).Value = row.QueryDate?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
            sheet.Cell(excelRow, 6).Value = row.ResponseStatus;
            sheet.Cell(excelRow, 7).Value = row.Action;
            sheet.Cell(excelRow, 8).Value = row.QueryStatus;
            sheet.Cell(excelRow, 9).Value = row.Team ?? string.Empty;
            sheet.Cell(excelRow, 10).Value = row.Source ?? string.Empty;
            sheet.Cell(excelRow, 11).Value = row.Notes ?? string.Empty;
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Min(rows.Count + 1, 100));

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await _emailService.SendEmailWithAttachmentsAsync(
            recipient,
            "RADAR Sorgu Raporu",
            "<p>Sorgu raporu Excel eki olarak iletilmistir.</p>",
            [new EmailAttachment("sorgu_raporu.xlsx", stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")],
            isHtml: true);
    }

    private static string ReportCatalogHtml() => """
        <p>Merhaba, kullanilabilir RADAR raporlari asagidadir:</p>
        <ul>
          <li>HAFTALIK PERMIT</li>
          <li>HAFTALIK BLOCK</li>
          <li>YUKSEK SKOR</li>
          <li>YUKSEK ESLESME</li>
          <li>SORGU RAPORU EXCEL</li>
          <li>SORGU RAPORU HTML</li>
        </ul>
        <p>Isteginizi konuya veya mail govdesine bu ifadelerden biriyle yazabilirsiniz.</p>
        """;

    private static bool CanRetry(string result) => result is
        "catalog_failed" or "query_report_failed" or "report_failed";

    private static string DescribeProcessingResult(string result)
    {
        var retry = result.StartsWith("retry:", StringComparison.OrdinalIgnoreCase);
        var already = result.StartsWith("already:", StringComparison.OrdinalIgnoreCase);
        var code = (retry || already) ? result[(result.IndexOf(':') + 1)..] : result;
        var label = code switch
        {
            "reply_matched" => "sorgu cevabi eslestirildi",
            "catalog_sent" => "rapor katalogu gonderildi",
            "catalog_failed" => "rapor katalogu SMTP ile gonderilemedi",
            "query_report_sent" => "sorgu raporu gonderildi",
            "query_report_failed" => "sorgu raporu SMTP ile gonderilemedi",
            "report_sent" => "rapor gonderildi",
            "report_failed" => "rapor SMTP ile gonderilemedi",
            "ldap_unverified" => "gonderen LDAP ile dogrulanamadi",
            "ldap_email_mismatch" => "gonderen e-posta LDAP kaydiyla eslesmedi",
            "user_not_authorized" => "gonderen Kullanici Yonetimi listesinde aktif degil",
            "unmatched" => "gonderen e-posta adresi okunamadi",
            _ => code
        };

        return retry ? $"tekrar deneme: {label}" : already ? $"daha once islendi: {label}" : label;
    }

    private static string ExtractEmail(string value)
    {
        var match = EmailPattern.Match(value ?? string.Empty);
        return match.Success ? match.Groups["email"].Value.Trim() : value?.Trim() ?? string.Empty;
    }

    private static bool SenderMatches(string sender, string recipient) =>
        string.Equals(sender.Trim(), recipient.Trim(), StringComparison.OrdinalIgnoreCase);

    private static DateTime? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed.UtcDateTime : null;

    private static string NormalizeSubject(string? value) => Regex.Replace(
        Regex.Replace(value ?? string.Empty, @"\[(RADAR-Q-[A-Z0-9]+)\]", string.Empty, RegexOptions.IgnoreCase),
        @"^\s*((re|fw|fwd)\s*:\s*)+", string.Empty, RegexOptions.IgnoreCase).Trim();

    private static string ExtractReplyRequest(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        var reply = Regex.Replace(body, @"(?m)^>.*$", string.Empty);
        var quotedMessage = Regex.Match(reply,
            @"(?im)^\s*(-----Original Message-----|_{5,}|From:|Kimden:|Gonderen:|Gönderen:|On .+ wrote:)\s*.*$");
        if (quotedMessage.Success) reply = reply[..quotedMessage.Index];

        return reply.Trim();
    }

    private static bool IsReportRequest(string? subject, string? body)
    {
        if (NormalizeSubject(subject).Contains("RADAR RAPOR TALEP KATALOGU", StringComparison.OrdinalIgnoreCase))
            return true;

        var request = FoldRequest($"{subject}\n{ExtractReplyRequest(body)}");
        return request.Contains("SORGU RAPORU") ||
               request.Contains("HAFTALIK PERMIT") ||
               request.Contains("HAFTALIK BLOCK") ||
               request.Contains("YUKSEK ESLESME") ||
               request.Contains("MAX MATCH") ||
               request.Contains("YUKSEK SKOR");
    }

    /*
    private static string FoldRequest(string value) => value
        .Replace("\u0131", "i")
        .ToUpperInvariant()
        .Replace('Ä°', 'I')
        .Replace('I', 'I')
        .Replace('Åž', 'S')
        .Replace('Ã‡', 'C')
        .Replace('Äž', 'G')
        .Replace('Ãœ', 'U')
        .Replace('Ã–', 'O');

    */
    private static string FoldRequest(string value) => value
        .ToUpperInvariant()
        .Replace("\u0130", "I")
        .Replace("\u015e", "S")
        .Replace("\u00c7", "C")
        .Replace("\u011e", "G")
        .Replace("\u00dc", "U")
        .Replace("\u00d6", "O");

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Shorten(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
