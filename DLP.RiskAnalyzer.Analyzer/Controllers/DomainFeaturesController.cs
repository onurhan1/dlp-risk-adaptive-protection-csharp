using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Domain Features API - Domain özelliklerini yönetir
/// </summary>
[ApiController]
[Route("api/domain-features")]
public class DomainFeaturesController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<DomainFeaturesController> _logger;

    public DomainFeaturesController(AnalyzerDbContext context, ILogger<DomainFeaturesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated domain list with all features
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDomains(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 150,
        [FromQuery] string? search = null)
    {
        var query = _context.NdaDomains.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d => d.Domain.Contains(search.ToLower()));
        }

        var total = await query.CountAsync();
        var domains = await query
            .OrderBy(d => d.Domain)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.Domain,
                d.HasNda,
                d.IsUnknown,
                d.IsPersonal,
                d.IstirakDomain,
                d.Egitim,
                d.Noter,
                d.Hukuk,
                d.Denetim,
                d.Banka
            })
            .ToListAsync();

        return Ok(new
        {
            domains,
            columns = new[] { "has_nda", "is_personal", "istirak_domain", "egitim", "noter", "hukuk", "denetim", "banka" },
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }

    /// <summary>
    /// Get column definitions
    /// </summary>
    [HttpGet("columns")]
    public IActionResult GetColumns()
    {
        var columns = new[]
        {
            new { name = "has_nda", displayName = "Gizlilik Sözleşmesi", key = "HasNda" },
            new { name = "is_personal", displayName = "Kişisel", key = "IsPersonal" },
            new { name = "istirak_domain", displayName = "İştirak", key = "IstirakDomain" },
            new { name = "egitim", displayName = "Eğitim", key = "Egitim" },
            new { name = "noter", displayName = "Noter", key = "Noter" },
            new { name = "hukuk", displayName = "Hukuk", key = "Hukuk" },
            new { name = "denetim", displayName = "Denetim", key = "Denetim" },
            new { name = "banka", displayName = "Banka", key = "Banka" }
        };
        return Ok(columns);
    }

    /// <summary>
    /// Bulk save domain features
    /// </summary>
    [HttpPost("bulk-save")]
    public async Task<IActionResult> BulkSave([FromBody] List<DomainFeatureUpdate> updates)
    {
        if (updates == null || !updates.Any())
            return BadRequest("No updates provided");

        try
        {
            var domainIds = updates.Select(u => u.Id).ToList();
            var domains = await _context.NdaDomains
                .Where(d => domainIds.Contains(d.Id))
                .ToListAsync();

            foreach (var update in updates)
            {
                var domain = domains.FirstOrDefault(d => d.Id == update.Id);
                if (domain == null) continue;

                domain.HasNda = update.HasNda;
                domain.IsPersonal = update.IsPersonal;
                domain.IstirakDomain = update.IstirakDomain;
                domain.Egitim = update.Egitim;
                domain.Noter = update.Noter;
                domain.Hukuk = update.Hukuk;
                domain.Denetim = update.Denetim;
                domain.Banka = update.Banka;
                domain.IsUnknown = false; // Mark as reviewed
                domain.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Bulk saved {Count} domain features", updates.Count);

            return Ok(new { success = true, updated = updates.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk saving domain features");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update single domain
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDomain(int id, [FromBody] DomainFeatureUpdate update)
    {
        var domain = await _context.NdaDomains.FindAsync(id);
        if (domain == null)
            return NotFound();

        domain.HasNda = update.HasNda;
        domain.IsPersonal = update.IsPersonal;
        domain.IstirakDomain = update.IstirakDomain;
        domain.Egitim = update.Egitim;
        domain.Noter = update.Noter;
        domain.Hukuk = update.Hukuk;
        domain.Denetim = update.Denetim;
        domain.Banka = update.Banka;
        domain.IsUnknown = false;
        domain.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(domain);
    }

    /// <summary>
    /// Extract domains from incidents and add missing ones
    /// </summary>
    [HttpPost("extract-from-incidents")]
    public async Task<IActionResult> ExtractFromIncidents()
    {
        try
        {
            // Get all unique destinations from incidents
            var destinations = await _context.Incidents
                .Where(i => !string.IsNullOrEmpty(i.Destination))
                .Select(i => i.Destination)
                .Distinct()
                .ToListAsync();

            var existingDomains = await _context.NdaDomains
                .Select(d => d.Domain.ToLower())
                .ToListAsync();
            var existingSet = new HashSet<string>(existingDomains);

            var newDomains = new List<NdaDomain>();
            var personalDomains = new HashSet<string> { "gmail.com", "hotmail.com", "outlook.com", "yahoo.com", "icloud.com", "mynet.com", "windowslive.com" };

            foreach (var dest in destinations)
            {
                if (string.IsNullOrEmpty(dest)) continue;

                // Split by ; for multiple emails
                var parts = dest.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    string? domain = null;

                    if (trimmed.Contains('@'))
                    {
                        domain = trimmed.Split('@').LastOrDefault()?.ToLower();
                    }

                    if (!string.IsNullOrEmpty(domain) && !existingSet.Contains(domain))
                    {
                        existingSet.Add(domain);
                        newDomains.Add(new NdaDomain
                        {
                            Domain = domain,
                            HasNda = false,
                            IsUnknown = true,
                            IsPersonal = personalDomains.Contains(domain)
                        });
                    }
                }
            }

            if (newDomains.Any())
            {
                await _context.NdaDomains.AddRangeAsync(newDomains);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Extracted {Count} new domains from incidents", newDomains.Count);
            return Ok(new { added = newDomains.Count, domains = newDomains.Take(20).Select(d => d.Domain) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting domains from incidents");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get unknown (unreviewed) domains
    /// </summary>
    [HttpGet("unknown")]
    public async Task<IActionResult> GetUnknownDomains([FromQuery] int limit = 100)
    {
        var domains = await _context.NdaDomains
            .Where(d => d.IsUnknown)
            .OrderBy(d => d.Domain)
            .Take(limit)
            .ToListAsync();

        return Ok(new { count = domains.Count, domains });
    }
}

/// <summary>
/// DTO for domain feature updates
/// </summary>
public class DomainFeatureUpdate
{
    public int Id { get; set; }
    public bool HasNda { get; set; }
    public bool IsPersonal { get; set; }
    public bool IstirakDomain { get; set; }
    public bool Egitim { get; set; }
    public bool Noter { get; set; }
    public bool Hukuk { get; set; }
    public bool Denetim { get; set; }
    public bool Banka { get; set; }
}
