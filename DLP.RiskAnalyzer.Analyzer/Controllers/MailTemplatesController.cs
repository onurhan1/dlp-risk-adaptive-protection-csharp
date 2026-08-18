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
    private static readonly DefaultMailTemplate[] DefaultTemplates =
    {
        new(
            "Workflow - Standart DLP Aktivite Sorgusu",
            "DLP Aktivite Doğrulama Talebi - {{tarih}}",
            """
            <p>Merhaba {{tam_ad}},</p>
            <p>DLP izleme kapsamında aşağıdaki aktivite kayıtları inceleme için tarafınıza iletilmektedir.</p>
            <p>Lütfen bu işlemlerin bilginiz dahilinde ve iş amacıyla gerçekleşip gerçekleşmediğini yanıtlayınız.</p>
            <p><strong>Kullanıcı:</strong> {{kullanici}}<br />
            <strong>Ekip:</strong> {{takim}}<br />
            <strong>İnceleme tarihi:</strong> {{tarih}}</p>
            <p><strong>Örnek olaylar</strong></p>
            <pre style="font-family: Arial, Helvetica, sans-serif; white-space: pre-wrap; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px;">{{olaylar}}</pre>
            <p>Yanıtınız güvenlik ve uyumluluk değerlendirmesi için kayıt altına alınacaktır.</p>
            <p>Teşekkürler,<br />Bilgi Güvenliği Ekibi</p>
            """),
        new(
            "Workflow - Yüksek Risk Skoru İnceleme",
            "Haftalık Yüksek Risk Skoru İncelemesi - {{kullanici}}",
            """
            <p>Merhaba {{tam_ad}},</p>
            <p>Haftalık DLP risk değerlendirmesinde hesabınız için yüksek risk sinyali oluşmuştur.</p>
            <p>Aşağıdaki örnek aktiviteleri kontrol ederek işlemlerin size ait olup olmadığını ve iş gerekçesini paylaşmanızı rica ederiz.</p>
            <p><strong>Kullanıcı:</strong> {{kullanici}}<br />
            <strong>Ekip:</strong> {{takim}}<br />
            <strong>Tarih:</strong> {{tarih}}</p>
            <pre style="font-family: Arial, Helvetica, sans-serif; white-space: pre-wrap; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px;">{{olaylar}}</pre>
            <p>İşlemler size ait değilse veya şüpheli görünüyorsa lütfen bu e-postayı acil olarak yanıtlayınız.</p>
            <p>Teşekkürler,<br />Bilgi Güvenliği Ekibi</p>
            """),
        new(
            "Workflow - Permit Incident İncelemesi",
            "Permit Edilen DLP Olayı Hakkında Bilgi Talebi - {{tarih}}",
            """
            <p>Merhaba {{tam_ad}},</p>
            <p>DLP politikaları kapsamında permit edilen fakat inceleme gerektiren aktiviteleriniz tespit edilmiştir.</p>
            <p>Bu işlemlerin iş amacı, alıcı/hedef bilgisi ve veri paylaşım gerekçesini kısaca iletmenizi rica ederiz.</p>
            <p><strong>Kullanıcı:</strong> {{kullanici}}<br />
            <strong>Ekip:</strong> {{takim}}</p>
            <pre style="font-family: Arial, Helvetica, sans-serif; white-space: pre-wrap; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px;">{{olaylar}}</pre>
            <p>Yanıtınız sonrasında olay uygunluk açısından kapatılacak veya ek incelemeye alınacaktır.</p>
            <p>Teşekkürler,<br />Bilgi Güvenliği Ekibi</p>
            """),
        new(
            "Workflow - Block Incident Bilgilendirme",
            "Bloklanan DLP Olayı İncelemesi - {{kullanici}}",
            """
            <p>Merhaba {{tam_ad}},</p>
            <p>DLP güvenlik kontrolleri aşağıdaki aktiviteyi engellemiş veya blok aksiyonu üretmiştir.</p>
            <p>İşlemin iş ihtiyacı kapsamında yapıldığını düşünüyorsanız, lütfen gerekçenizi ve gerekiyorsa alternatif güvenli paylaşım yöntemini belirtiniz.</p>
            <p><strong>Kullanıcı:</strong> {{kullanici}}<br />
            <strong>Ekip:</strong> {{takim}}<br />
            <strong>İnceleme tarihi:</strong> {{tarih}}</p>
            <pre style="font-family: Arial, Helvetica, sans-serif; white-space: pre-wrap; background: #fff7ed; border: 1px solid #fed7aa; border-radius: 6px; padding: 12px;">{{olaylar}}</pre>
            <p>Güvenli paylaşım ihtiyacı varsa Bilgi Güvenliği ekibi yönlendirme sağlayacaktır.</p>
            <p>Teşekkürler,<br />Bilgi Güvenliği Ekibi</p>
            """),
        new(
            "Workflow - Yüksek Max Match Veri Gönderimi",
            "Yüksek Eşleşme Sayılı Veri Gönderimi İncelemesi - {{tarih}}",
            """
            <p>Merhaba {{tam_ad}},</p>
            <p>Tek seferde yüksek sayıda hassas veri eşleşmesi içeren bir aktarım tespit edilmiştir.</p>
            <p>Mevcut alt sınır 300 Max Match ve üzeri olaylar için kullanılmaktadır. Lütfen aşağıdaki aktivitenin iş gerekçesini, hedef/alıcı bilgisini ve verinin paylaşım zorunluluğunu açıklayınız.</p>
            <p><strong>Kullanıcı:</strong> {{kullanici}}<br />
            <strong>Ekip:</strong> {{takim}}</p>
            <pre style="font-family: Arial, Helvetica, sans-serif; white-space: pre-wrap; background: #fef2f2; border: 1px solid #fecaca; border-radius: 6px; padding: 12px;">{{olaylar}}</pre>
            <p>Gerekçesi olmayan veya hatalı veri paylaşımı için ek aksiyon alınabilir.</p>
            <p>Teşekkürler,<br />Bilgi Güvenliği Ekibi</p>
            """)
    };

    public MailTemplatesController(AnalyzerDbContext context, ILogger<MailTemplatesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public record MailTemplateRequest(string Name, string Subject, string Body);
    private sealed record DefaultMailTemplate(string Name, string Subject, string Body);

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

    private async Task SeedDefaultTemplatesAsync(CancellationToken ct)
    {
        await EnsureTableAsync(ct);

        var defaultNames = DefaultTemplates.Select(t => t.Name).ToList();
        var existingNames = await _context.MailTemplates
            .AsNoTracking()
            .Where(t => defaultNames.Contains(t.Name))
            .Select(t => t.Name)
            .ToListAsync(ct);
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var template in DefaultTemplates.Where(t => !existing.Contains(t.Name)))
        {
            _context.MailTemplates.Add(new MailTemplate
            {
                Name = template.Name,
                Subject = template.Subject,
                Body = template.Body,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(ct);
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
        await SeedDefaultTemplatesAsync(ct);
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
