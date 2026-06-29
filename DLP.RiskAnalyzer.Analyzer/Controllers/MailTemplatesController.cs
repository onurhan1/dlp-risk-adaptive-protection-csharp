using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/mail-templates")]
public class MailTemplatesController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<MailTemplatesController> _logger;

    public MailTemplatesController(AnalyzerDbContext context, ILogger<MailTemplatesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public record MailTemplateRequest(string Name, string Subject, string Body);

    /// <summary>
    /// Ensure the mail_templates table exists (mirrors the runtime provisioning used for
    /// system_settings in SettingsController, so the feature works even before EF migrations
    /// are applied on the server).
    /// </summary>
    private async Task EnsureTableAsync(CancellationToken ct)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                CREATE SCHEMA IF NOT EXISTS dlp;
                CREATE TABLE IF NOT EXISTS dlp.mail_templates (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    subject VARCHAR(500) NOT NULL,
                    body TEXT NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure mail_templates table (may already exist)");
        }
    }

    private static object ToDto(MailTemplate t) => new
    {
        id = t.Id,
        name = t.Name,
        subject = t.Subject,
        body = t.Body,
        created_at = t.CreatedAt,
        updated_at = t.UpdatedAt
    };

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var templates = await _context.MailTemplates
            .AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);
        return Ok(templates.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var template = await _context.MailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template == null) return NotFound(new { detail = "Şablon bulunamadı" });
        return Ok(ToDto(template));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MailTemplateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { detail = "İsim ve konu zorunludur" });

        await EnsureTableAsync(ct);
        var now = DateTime.UtcNow;
        var template = new MailTemplate
        {
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            Body = request.Body ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.MailTemplates.Add(template);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(template));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] MailTemplateRequest request, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var template = await _context.MailTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template == null) return NotFound(new { detail = "Şablon bulunamadı" });

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { detail = "İsim ve konu zorunludur" });

        template.Name = request.Name.Trim();
        template.Subject = request.Subject.Trim();
        template.Body = request.Body ?? string.Empty;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(template));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var template = await _context.MailTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template == null) return NotFound(new { detail = "Şablon bulunamadı" });

        _context.MailTemplates.Remove(template);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}
