using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/investigation")]
public class InvestigationController : ControllerBase
{
    private readonly IWeeklyFlagService _weeklyFlagService;
    private readonly IEmailService _emailService;
    private readonly IInvestigationQueryRemediationSyncService _queryRemediationSync;
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<InvestigationController> _logger;

    public InvestigationController(
        IWeeklyFlagService weeklyFlagService,
        IEmailService emailService,
        IInvestigationQueryRemediationSyncService queryRemediationSync,
        AnalyzerDbContext context,
        ILogger<InvestigationController> logger)
    {
        _weeklyFlagService = weeklyFlagService;
        _emailService = emailService;
        _queryRemediationSync = queryRemediationSync;
        _context = context;
        _logger = logger;
    }

    [HttpGet("weekly-flags")]
    public async Task<IActionResult> GetWeeklyFlags([FromQuery] int days = 7)
    {
        try
        {
            var result = await _weeklyFlagService.GetWeeklyFlagsAsync(days);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute weekly flags");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    public class SendMailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string? ToName { get; set; }
        public string? UserCode { get; set; }
        public bool CcAdmin { get; set; }
        public string? CcEmail { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public DateTime? IncidentTimestamp { get; set; }
    }

    [HttpPost("send-mail")]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            return BadRequest(new { detail = "Alıcı e-posta adresi zorunludur" });
        if (!IsValidEmail(request.ToEmail))
            return BadRequest(new { detail = "Geçerli bir alıcı e-posta adresi girin" });
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { detail = "Konu zorunludur" });
        if (!string.IsNullOrWhiteSpace(request.CcEmail) && !IsValidEmail(request.CcEmail))
            return BadRequest(new { detail = "Geçerli bir CC e-posta adresi girin" });

        if (!await _emailService.IsConfiguredAsync())
            return BadRequest(new { detail = "E-posta servisi yapılandırılmamış. Lütfen Ayarlar'dan SMTP bilgilerini girin." });

        string? ccEmail = string.IsNullOrWhiteSpace(request.CcEmail)
            ? null
            : request.CcEmail.Trim();

        // Backwards compatibility for callers still using the old cc_admin flag.
        if (ccEmail == null && request.CcAdmin)
        {
            ccEmail = await _context.SystemSettings
                .Where(s => s.Key == "admin_email")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(ccEmail))
                _logger.LogWarning("CC admin requested but admin_email is not configured in system_settings");
        }

        var success = await _emailService.SendEmailAsync(
            toEmail: request.ToEmail.Trim(),
            subject: request.Subject.Trim(),
            body: request.BodyHtml ?? string.Empty,
            isHtml: true,
            toName: request.ToName,
            ccEmail: string.IsNullOrWhiteSpace(ccEmail) ? null : ccEmail);

        if (success)
        {
            await InvestigationQuerySchema.EnsureAsync(_context, _logger);
            var now = DateTime.UtcNow;
            var actor = User?.Identity?.Name ?? "System";
            var queryRecord = new InvestigationQueryRecord
            {
                UserCode = request.UserCode?.Trim() ?? string.Empty,
                FullName = TurkishNameHelper.FromEmailLocalPart(request.ToEmail, request.ToName),
                MailAddress = request.ToEmail.Trim(),
                Subject = request.Subject.Trim(),
                QueryDate = now,
                ResponseStatus = "Mail gönderildi",
                Action = "Sorgu maili gönderildi",
                QueryStatus = InvestigationQueryStatus.Queried,
                Source = "manual_mail",
                Notes = request.IncidentTimestamp.HasValue
                    ? $"Olay tarihi: {request.IncidentTimestamp.Value:dd.MM.yyyy HH:mm}"
                    : null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actor,
                UpdatedBy = actor
            };
            _context.InvestigationQueries.Add(queryRecord);
            await _queryRemediationSync.SyncAsync([queryRecord], actor, now);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Investigation mail sent to {ToEmail} (cc: {Cc})", request.ToEmail, ccEmail ?? "-");
            return Ok(new { success = true, message = $"Mail gönderildi: {request.ToEmail}" });
        }

        return StatusCode(500, new { detail = "Mail gönderilemedi. SMTP yapılandırmasını ve logları kontrol edin." });
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var normalized = email.Trim();
            return new MailAddress(normalized).Address.Equals(normalized, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
