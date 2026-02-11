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
    private readonly PolicyExceptionSyncService _syncService;
    private readonly ILogger<PolicyExceptionsController> _logger;

    public PolicyExceptionsController(
        AnalyzerDbContext context,
        PolicyExceptionSyncService syncService,
        ILogger<PolicyExceptionsController> logger)
    {
        _context = context;
        _syncService = syncService;
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
                            exceptions = ruleGroup.Select(e => e.ExceptionName).ToList()
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

            return Ok(new
            {
                success = true,
                message = $"Sync completed: {count} exceptions saved",
                syncedCount = count,
                syncedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during policy exception sync");
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
