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
        [FromQuery] bool onlyFlagged = false,
        [FromQuery] string? filterColumns = null)
    {
        var query = _context.NdaDomains.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d => d.Domain.Contains(search.ToLower()));
        }
        else if (!string.IsNullOrEmpty(filterColumns))
        {
            // Filter by specific columns (AND logic - all selected columns must be true)
            var columns = filterColumns.Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var col in columns)
            {
                var colKey = col.Trim().ToLower();
                
                // Static columns
                if (colKey == "hasnda") query = query.Where(d => d.HasNda);
                else if (colKey == "ispersonal") query = query.Where(d => d.IsPersonal);
                else if (colKey == "istirakdomain") query = query.Where(d => d.IstirakDomain);
                else if (colKey == "egitim") query = query.Where(d => d.Egitim);
                else if (colKey == "noter") query = query.Where(d => d.Noter);
                else if (colKey == "hukuk") query = query.Where(d => d.Hukuk);
                else if (colKey == "denetim") query = query.Where(d => d.Denetim);
                else if (colKey == "banka") query = query.Where(d => d.Banka);
                else
                {
                    // Dynamic column - check in DomainFeatureValues
                    var dynamicKey = colKey;
                    query = query.Where(d => 
                        _context.DomainFeatureValues.Any(v => 
                            v.DomainId == d.Id && 
                            v.Feature.KeyName == dynamicKey && 
                            v.IsEnabled));
                }
            }
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
    /// Get top used domains based on incident count with detailed stats
    /// </summary>
    [HttpGet("top")]
    public async Task<IActionResult> GetTopDomains([FromQuery] int limit = 100)
    {
        // 1. Get top domains from Incidents
        var topDomains = await _context.Incidents
            .Where(i => !string.IsNullOrEmpty(i.Destination))
            .GroupBy(i => i.Destination)
            .Select(g => new { Destination = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(limit * 2) 
            .ToListAsync();

        // Process top domains and prepare aggregation containers
        var domainCounts = new Dictionary<string, int>();
        var domainActions = new Dictionary<string, Dictionary<string, int>>();
        var domainTeams = new Dictionary<string, Dictionary<string, int>>();

        // Helper to add stats
        void AddStat(Dictionary<string, Dictionary<string, int>> target, string domain, string? key)
        {
            if (string.IsNullOrEmpty(key)) key = "Unknown";
            if (!target.ContainsKey(domain)) target[domain] = new Dictionary<string, int>();
            if (!target[domain].ContainsKey(key)) target[domain][key] = 0;
            target[domain][key]++;
        }

        // 2. Fetch detailed data for stats (Action, Department)
        // Since we need to parse destinations (split ;), we need to fetch raw data for these top destinations
        var destinations = topDomains.Select(x => x.Destination).ToList();
        
        var details = await _context.Incidents
            .Where(i => destinations.Contains(i.Destination))
            .Select(i => new { i.Destination, i.Action, i.Department })
            .ToListAsync();

        foreach (var item in details)
        {
            var parts = item.Destination.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Contains("@"))
                {
                    var domain = part.Split('@').Last().Trim().ToLower();
                    
                    // Count
                    if (!domainCounts.ContainsKey(domain)) domainCounts[domain] = 0;
                    domainCounts[domain]++;

                    // Stats
                    AddStat(domainActions, domain, item.Action);
                    AddStat(domainTeams, domain, item.Department);
                }
            }
        }

        var topDomainList = domainCounts
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .Select(x => x.Key)
            .ToList();

        // 3. Fetch NdaDomain details for these domains
        var domains = await _context.NdaDomains
            .Where(d => topDomainList.Contains(d.Domain))
            .ToListAsync();

        // Sort by frequency
        domains = domains.OrderBy(d => topDomainList.IndexOf(d.Domain)).ToList();
        var domainIds = domains.Select(d => d.Id).ToList();

        // 4. Fetch dynamic values
        var featureDefs = await _context.DomainFeatureDefinitions.ToDictionaryAsync(f => f.KeyName, f => f.Id);
        var existingValues = await _context.DomainFeatureValues
            .Where(v => domainIds.Contains(v.DomainId))
            .ToListAsync();
            
        var valueLookup = existingValues.ToDictionary(v => (v.DomainId, v.FeatureId), v => v);

        var result = domains.Select(d =>
        {
            // Reconstruct features dictionary
            var features = new Dictionary<string, bool>();
            foreach(var def in featureDefs)
            {
                if (valueLookup.TryGetValue((d.Id, def.Value), out var val) && val.IsEnabled)
                {
                    features[def.Key] = true;
                }
            }

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
                IncidentCount = domainCounts.ContainsKey(d.Domain) ? domainCounts[d.Domain] : 0,
                IncidentStats = new 
                {
                    Actions = domainActions.ContainsKey(d.Domain) ? domainActions[d.Domain] : new Dictionary<string, int>(),
                    Teams = domainTeams.ContainsKey(d.Domain) ? domainTeams[d.Domain] : new Dictionary<string, int>()
                }
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

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
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

        try
        {
            var domainIds = updates.Select(u => u.Id).ToList();
            var domains = await _context.NdaDomains
                .Where(d => domainIds.Contains(d.Id))
                .ToListAsync();

            // Cache feature definitions
            var featureDefs = await _context.DomainFeatureDefinitions.ToDictionaryAsync(f => f.KeyName, f => f.Id);

            // Fetch ALL existing values for these domains to avoid DB calls in loop
            var existingValues = await _context.DomainFeatureValues
                .Where(v => domainIds.Contains(v.DomainId))
                .ToListAsync();

            // Create a lookup for performance: (DomainId, FeatureId) -> DomainFeatureValue
            var valueLookup = existingValues
                .ToDictionary(v => (v.DomainId, v.FeatureId), v => v);

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
                            // Check in memory lookup
                            if (valueLookup.TryGetValue((domain.Id, featureId), out var val))
                            {
                                // Update existing
                                if (val.IsEnabled != kvp.Value)
                                {
                                    val.IsEnabled = kvp.Value;
                                    val.UpdatedAt = DateTime.UtcNow;
                                }
                            }
                            else if (kvp.Value) // Only insert if true and not exists
                            {
                                // Create new
                                var newVal = new DomainFeatureValue
                                {
                                    DomainId = domain.Id,
                                    FeatureId = featureId,
                                    IsEnabled = true
                                };
                                _context.DomainFeatureValues.Add(newVal);
                                
                                // Add to lookup to prevent duplicates if key repeats (unlikely)
                                valueLookup[(domain.Id, featureId)] = newVal;
                            }
                        }
                    }
                }
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
