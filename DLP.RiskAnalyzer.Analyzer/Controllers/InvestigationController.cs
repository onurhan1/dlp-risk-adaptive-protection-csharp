using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/investigation")]
public class InvestigationController : ControllerBase
{
    private readonly IWeeklyFlagService _weeklyFlagService;
    private readonly IEmailService _emailService;
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<InvestigationController> _logger;

    public InvestigationController(
        IWeeklyFlagService weeklyFlagService,
        IEmailService emailService,
        AnalyzerDbContext context,
        ILogger<InvestigationController> logger)
    {
        _weeklyFlagService = weeklyFlagService;
        _emailService = emailService;
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

    public record SendMailRequest(string ToEmail, string? ToName, bool CcAdmin, string Subject, string BodyHtml);

    [HttpPost("send-mail")]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            return BadRequest(new { detail = "Alıcı e-posta adresi zorunludur" });
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { detail = "Konu zorunludur" });

        if (!await _emailService.IsConfiguredAsync())
            return BadRequest(new { detail = "E-posta servisi yapılandırılmamış. Lütfen Ayarlar'dan SMTP bilgilerini girin." });

        string? ccEmail = null;
        if (request.CcAdmin)
        {
            ccEmail = await _context.SystemSettings
                .Where(s => s.Key == "admin_email")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(ccEmail))
                _logger.LogWarning("CC admin requested but admin_email is not configured in system_settings");
        }

        var success = await _emailService.SendEmailAsync(
            toEmail: request.ToEmail,
            subject: request.Subject,
            body: request.BodyHtml ?? string.Empty,
            isHtml: true,
            toName: request.ToName,
            ccEmail: string.IsNullOrWhiteSpace(ccEmail) ? null : ccEmail);

        if (success)
        {
            _logger.LogInformation("Investigation mail sent to {ToEmail} (cc: {Cc})", request.ToEmail, ccEmail ?? "-");
            return Ok(new { success = true, message = $"Mail gönderildi: {request.ToEmail}" });
        }

        return StatusCode(500, new { detail = "Mail gönderilemedi. SMTP yapılandırmasını ve logları kontrol edin." });
    }
}
