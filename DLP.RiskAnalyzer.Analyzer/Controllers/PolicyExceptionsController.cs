using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Policy Exception yönetimi - DB'deki exception verilerini görüntüleme ve senkronizasyon
/// </summary>
[ApiController]
[Route("api/policy-exceptions")]
public class PolicyExceptionsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly IPolicyExceptionSyncService _syncService;
    private readonly IForcepointPolicyExceptionService _forcepointExceptionService;
    private readonly ILogger<PolicyExceptionsController> _logger;

    public PolicyExceptionsController(
        AnalyzerDbContext context,
        IPolicyExceptionSyncService syncService,
        IForcepointPolicyExceptionService forcepointExceptionService,
        ILogger<PolicyExceptionsController> logger)
    {
        _context = context;
        _syncService = syncService;
        _forcepointExceptionService = forcepointExceptionService;
        _logger = logger;
    }

    /// <summary>
    /// Tüm policy rule exception kayıtlarını listeler (Policy → Rule → Exception hiyerarşisi)
    /// GET /api/policy-exceptions
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            var exceptions = await _context.PolicyRuleExceptions
                .AsNoTracking()
                .OrderBy(e => e.PolicyName)
                .ThenBy(e => e.RuleName)
                .ThenBy(e => e.ExceptionName)
                .ToListAsync();



            // Policy → Rule → Exceptions hiyerarşisi olarak grupla
            var grouped = exceptions
                .GroupBy(e => e.PolicyName)
                .Select(policyGroup => new
                {
                    policyName = policyGroup.Key,
                    rules = policyGroup
                        .GroupBy(e => e.RuleName)
                        .Select(ruleGroup => new
                        {
                            ruleName = ruleGroup.Key,
                            exceptions = ruleGroup.Select(e => new
                            {
                                exceptionName = e.ExceptionName,
                                enabled = e.Enabled
                            }).ToList()
                        })
                        .ToList()
                })
                .ToList();

            var lastSync = exceptions.Any()
                ? exceptions.Max(e => e.SyncedAt)
                : (DateTime?)null;

            return Ok(new
            {
                success = true,
                totalExceptions = exceptions.Count,
                totalPolicies = grouped.Count,
                lastSyncedAt = lastSync,
                data = grouped
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching policy exceptions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// DLP API'den exception bilgilerini senkronize eder
    /// POST /api/policy-exceptions/sync
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult> Sync()
    {
        try
        {
            _logger.LogInformation("Manual policy exception sync triggered");
            var count = await _syncService.SyncAsync();
            var lastSync = await _context.PolicyRuleExceptions
                .AsNoTracking()
                .MaxAsync(e => (DateTime?)e.SyncedAt);

            return Ok(new
            {
                success = true,
                message = $"Sync completed: {count} exceptions saved",
                syncedCount = count,
                syncedAt = lastSync ?? DateTime.UtcNow,
                lastSyncedAt = lastSync
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during policy exception sync");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("forcepoint-enabled/bulk")]
    public async Task<ActionResult> SetForcepointExceptionsEnabledBulk(
        [FromBody] PolicyExceptionBulkToggleRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = User?.Identity?.Name ?? "System";
            var refs = request?.Exceptions?.Select(e => new ForcepointExceptionToggleReference(
                e.PolicyName,
                e.RuleName,
                e.ExceptionName)) ?? Enumerable.Empty<ForcepointExceptionToggleReference>();

            var result = await _forcepointExceptionService.SetExceptionReferencesEnabledAsync(
                refs,
                request?.Enabled ?? false,
                actor,
                cancellationToken);

            var response = new
            {
                success = result.Success,
                message = result.Message,
                data = result
            };

            return result.UpdatedCount > 0 || result.Success
                ? Ok(response)
                : BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk Forcepoint policy exception update");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Belirli bir rule name'in exception olup olmadığını kontrol eder
    /// GET /api/policy-exceptions/check/{ruleName}
    /// </summary>
    [HttpGet("check/{ruleName}")]
    public async Task<ActionResult> CheckRule(string ruleName)
    {
        try
        {
            var exception = await _context.PolicyRuleExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ExceptionName == ruleName);

            return Ok(new
            {
                success = true,
                ruleName = ruleName,
                isException = exception != null,
                parentRuleName = exception?.RuleName,
                policyName = exception?.PolicyName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rule exception status");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public class PolicyExceptionBulkToggleRequest
{
    public List<PolicyExceptionBulkToggleItem> Exceptions { get; set; } = new();
    public bool Enabled { get; set; }
}

public class PolicyExceptionBulkToggleItem
{
    public string? PolicyName { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string ExceptionName { get; set; } = string.Empty;
}
