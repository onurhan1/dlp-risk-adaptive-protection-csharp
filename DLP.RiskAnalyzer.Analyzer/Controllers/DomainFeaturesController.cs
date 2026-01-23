using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Domain Features API - Domain özelliklerini yönetir (Statik + Dinamik)
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
    /// Get paginated domain list with all features (Static + Dynamic)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDomains(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 150,
        [FromQuery] string? search = null,
        [FromQuery] bool onlyFlagged = false)
    {
        var query = _context.NdaDomains.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d => d.Domain.Contains(search.ToLower()));
        }
        else if (onlyFlagged)
        {
            // Only show domains that have at least one static feature enable OR exist in dynamic values
            query = query.Where(d => 
                d.HasNda || d.IsPersonal || d.IstirakDomain || 
                d.Egitim || d.Noter || d.Hukuk || d.Denetim || d.Banka ||
                _context.DomainFeatureValues.Any(v => v.DomainId == d.Id && v.IsEnabled));
        }

        var total = await query.CountAsync();
        
        // 1. Fetch domains
        var domains = await query
            .OrderBy(d => d.Domain)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            
        var domainIds = domains.Select(d => d.Id).ToList();

        // 2. Fetch dynamic values for these domains
        var dynamicValues = await _context.DomainFeatureValues
            .Include(v => v.Feature)
            .Where(v => domainIds.Contains(v.DomainId))
            .ToListAsync();

        // 3. Map to DTO
        var result = domains.Select(d =>
        {
            var features = dynamicValues
                .Where(v => v.DomainId == d.Id && v.Feature != null)
                .ToDictionary(v => v.Feature!.KeyName, v => v.IsEnabled);

            return new
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
                d.Banka,
                CustomFeatures = features
            };
        });

        // 4. Get all column definitions (Static + Dynamic)
        var staticColumns = new List<object>
        {
            new { name = "has_nda", displayName = "Gizlilik Sözleşmesi", key = "hasNda", isStatic = true },
            new { name = "is_personal", displayName = "Kişisel", key = "isPersonal", isStatic = true },
            new { name = "istirak_domain", displayName = "İştirak", key = "istirakDomain", isStatic = true },
            new { name = "egitim", displayName = "Eğitim", key = "egitim", isStatic = true },
            new { name = "noter", displayName = "Noter", key = "noter", isStatic = true },
            new { name = "hukuk", displayName = "Hukuk", key = "hukuk", isStatic = true },
            new { name = "denetim", displayName = "Denetim", key = "denetim", isStatic = true },
            new { name = "banka", displayName = "Banka", key = "banka", isStatic = true }
        };

        var dynamicColumns = await _context.DomainFeatureDefinitions
            .Where(f => f.IsActive)
            .OrderBy(f => f.DisplayName)
            .Select(f => new { name = f.KeyName, displayName = f.DisplayName, key = f.KeyName, isStatic = false, id = f.Id })
            .ToListAsync();

        return Ok(new
        {
            domains = result,
            columns = staticColumns.Concat(dynamicColumns),
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
    /// Get top used domains based on incident count
    /// </summary>
    [HttpGet("top")]
    public async Task<IActionResult> GetTopDomains([FromQuery] int limit = 100)
    {
        // 1. Get top domains from Incidents
        // Note: Destination column might contain multiple emails or just domain
        // This is a simplified approach assuming clean data or post-processing
        var topDomains = await _context.Incidents
            .Where(i => !string.IsNullOrEmpty(i.Destination))
            .GroupBy(i => i.Destination) // Group by raw destination for now
            .Select(g => new { Destination = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(limit * 2) // Take more to process
            .ToListAsync();

        // Process top domains (extract domain from email)
        var domainCounts = new Dictionary<string, int>();
        foreach (var item in topDomains)
        {
            var parts = item.Destination.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Contains("@"))
                {
                    var domain = part.Split('@').Last().Trim().ToLower();
                    if (!domainCounts.ContainsKey(domain)) domainCounts[domain] = 0;
                    domainCounts[domain] += item.Count;
                }
            }
        }

        var topDomainList = domainCounts
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .Select(x => x.Key)
            .ToList();

        // 2. Fetch NdaDomain details for these domains
        var domains = await _context.NdaDomains
            .Where(d => topDomainList.Contains(d.Domain))
            .ToListAsync();

        // Sort by frequency
        domains = domains.OrderBy(d => topDomainList.IndexOf(d.Domain)).ToList();
        var domainIds = domains.Select(d => d.Id).ToList();

        // 3. Fetch dynamic values
        var dynamicValues = await _context.DomainFeatureValues
            .Include(v => v.Feature)
            .Where(v => domainIds.Contains(v.DomainId))
            .ToListAsync();

        var result = domains.Select(d =>
        {
            var features = dynamicValues
                .Where(v => v.DomainId == d.Id && v.Feature != null)
                .ToDictionary(v => v.Feature!.KeyName, v => v.IsEnabled);

            return new
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
                d.Banka,
                CustomFeatures = features,
                IncidentCount = domainCounts.ContainsKey(d.Domain) ? domainCounts[d.Domain] : 0
            };
        });

        return Ok(new { domains = result });
    }

    /// <summary>
    /// Get column definitions only
    /// </summary>
    [HttpGet("columns")]
    public async Task<IActionResult> GetColumns()
    {
        var staticColumns = new List<object>
        {
            new { name = "has_nda", displayName = "Gizlilik Sözleşmesi", key = "hasNda", isStatic = true },
            new { name = "is_personal", displayName = "Kişisel", key = "isPersonal", isStatic = true },
            new { name = "istirak_domain", displayName = "İştirak", key = "istirakDomain", isStatic = true },
            new { name = "egitim", displayName = "Eğitim", key = "egitim", isStatic = true },
            new { name = "noter", displayName = "Noter", key = "noter", isStatic = true },
            new { name = "hukuk", displayName = "Hukuk", key = "hukuk", isStatic = true },
            new { name = "denetim", displayName = "Denetim", key = "denetim", isStatic = true },
            new { name = "banka", displayName = "Banka", key = "banka", isStatic = true }
        };

        var dynamicColumns = await _context.DomainFeatureDefinitions
            .Where(f => f.IsActive)
            .OrderBy(f => f.DisplayName)
            .Select(f => new { name = f.KeyName, displayName = f.DisplayName, key = f.KeyName, isStatic = false, id = f.Id })
            .ToListAsync();

        return Ok(staticColumns.Concat(dynamicColumns));
    }

    /// <summary>
    /// Add new dynamic column
    /// </summary>
    [HttpPost("columns")]
    public async Task<IActionResult> AddColumn([FromBody] AddColumnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Display name is required");

        // Generate key from display name (e.g., "Sağlık Sektörü" -> "saglik_sektoru")
        var key = request.DisplayName.ToLower()
            .Replace(" ", "_")
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Trim();
            
        // Ensure uniqueness
        if (await _context.DomainFeatureDefinitions.AnyAsync(f => f.KeyName == key))
            key += "_" + Guid.NewGuid().ToString().Substring(0, 4);

        var definition = new DomainFeatureDefinition
        {
            KeyName = key,
            DisplayName = request.DisplayName,
            IsActive = true
        };

        _context.DomainFeatureDefinitions.Add(definition);
        await _context.SaveChangesAsync();

        return Ok(new { name = definition.KeyName, displayName = definition.DisplayName, key = definition.KeyName, isStatic = false, id = definition.Id });
    }

    /// <summary>
    /// Update existing dynamic column (Rename)
    /// </summary>
    [HttpPut("columns/{id}")]
    public async Task<IActionResult> UpdateColumn(int id, [FromBody] UpdateColumnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Display name is required");

        var definition = await _context.DomainFeatureDefinitions.FindAsync(id);
        if (definition == null)
            return NotFound();

        definition.DisplayName = request.DisplayName;
        await _context.SaveChangesAsync();

        return Ok(new { name = definition.KeyName, displayName = definition.DisplayName, key = definition.KeyName, isStatic = false, id = definition.Id });
    }

    /// <summary>
    /// Delete dynamic column
    /// </summary>
    [HttpDelete("columns/{id}")]
    public async Task<IActionResult> DeleteColumn(int id)
    {
        var definition = await _context.DomainFeatureDefinitions.FindAsync(id);
        if (definition == null)
            return NotFound();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Delete all values associated with this feature
            var values = await _context.DomainFeatureValues
                .Where(v => v.FeatureId == id)
                .ToListAsync();
            
            _context.DomainFeatureValues.RemoveRange(values);

            // 2. Delete the definition
            _context.DomainFeatureDefinitions.Remove(definition);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting column {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk save domain features (Static + Dynamic)
    /// </summary>
    [HttpPost("bulk-save")]
    public async Task<IActionResult> BulkSave([FromBody] List<DomainFeatureUpdate> updates)
    {
        if (updates == null || !updates.Any())
            return BadRequest("No updates provided");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var domainIds = updates.Select(u => u.Id).ToList();
            var domains = await _context.NdaDomains
                .Where(d => domainIds.Contains(d.Id))
                .ToListAsync();

            // Cache feature definitions
            var featureDefs = await _context.DomainFeatureDefinitions.ToDictionaryAsync(f => f.KeyName, f => f.Id);

            foreach (var update in updates)
            {
                var domain = domains.FirstOrDefault(d => d.Id == update.Id);
                if (domain == null) continue;

                // Update static properties
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

                // Update dynamic values
                if (update.CustomFeatures != null)
                {
                    foreach (var kvp in update.CustomFeatures)
                    {
                        if (featureDefs.TryGetValue(kvp.Key, out int featureId))
                        {
                            // Check if value exists
                            var val = await _context.DomainFeatureValues
                                .FirstOrDefaultAsync(v => v.DomainId == domain.Id && v.FeatureId == featureId);

                            if (val == null)
                            {
                                if (kvp.Value) // Only insert if true to save space
                                {
                                    _context.DomainFeatureValues.Add(new DomainFeatureValue
                                    {
                                        DomainId = domain.Id,
                                        FeatureId = featureId,
                                        IsEnabled = true
                                    });
                                }
                            }
                            else
                            {
                                val.IsEnabled = kvp.Value;
                                val.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Bulk saved {Count} domain features", updates.Count);

            return Ok(new { success = true, updated = updates.Count });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error bulk saving domain features");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract domains from incidents and add missing ones
    /// </summary>
    [HttpPost("extract-from-incidents")]
    public async Task<IActionResult> ExtractFromIncidents()
    {
        try
        {
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

            return Ok(new { added = newDomains.Count, domains = newDomains.Take(20).Select(d => d.Domain) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting domains from incidents");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

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
    public Dictionary<string, bool>? CustomFeatures { get; set; }
}

public class AddColumnRequest
{
    public string DisplayName { get; set; } = string.Empty;
}

public class UpdateColumnRequest
{
    public string DisplayName { get; set; } = string.Empty;
}
