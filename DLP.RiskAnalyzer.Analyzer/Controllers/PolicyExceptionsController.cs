using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Policy Exception yönetimi - DB'deki exception verilerini görüntüleme ve senkronizasyon
/// </summary>
[ApiController]
[Route("api/policy-exceptions")]
public class PolicyExceptionsController : ControllerBase
{
    private static readonly SemaphoreSlim SyncSemaphore = new(1, 1);
    private static readonly object SyncStatusLock = new();
    private static PolicyExceptionSyncState SyncState = new()
    {
        Status = "idle"
    };

    private readonly AnalyzerDbContext _context;
    private readonly IForcepointPolicyExceptionService _forcepointExceptionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PolicyExceptionsController> _logger;

    public PolicyExceptionsController(
        AnalyzerDbContext context,
        IForcepointPolicyExceptionService forcepointExceptionService,
        IServiceScopeFactory scopeFactory,
        ILogger<PolicyExceptionsController> logger)
    {
        _context = context;
        _forcepointExceptionService = forcepointExceptionService;
        _scopeFactory = scopeFactory;
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
            var lastSyncIso = FormatUtcIso(lastSync);

            return Ok(new
            {
                success = true,
                totalExceptions = exceptions.Count,
                totalPolicies = grouped.Count,
                lastSyncedAt = lastSyncIso,
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
        if (!await SyncSemaphore.WaitAsync(0))
        {
            return Ok(CreateSyncResponse("Policy exception sync is already running.", true));
        }

        var startedAt = DateTime.UtcNow;
        SetSyncState(new PolicyExceptionSyncState
        {
            Status = "running",
            StartedAt = startedAt,
            Message = "Policy exception sync started."
        });

        _ = Task.Run(RunSyncInBackgroundAsync);

        return Ok(CreateSyncResponse("Policy exception sync started.", true));
    }

    [HttpGet("sync/status")]
    public ActionResult GetSyncStatus()
    {
        return Ok(CreateSyncResponse());
    }

    private async Task RunSyncInBackgroundAsync()
    {
        try
        {
            _logger.LogInformation("Manual policy exception sync triggered");

            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IPolicyExceptionSyncService>();
            var context = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();

            var count = await syncService.SyncAsync();
            var lastSync = await GetLastSyncAsync(context);
            var completedAt = DateTime.UtcNow;

            SetSyncState(new PolicyExceptionSyncState
            {
                Status = "completed",
                StartedAt = GetSyncState().StartedAt,
                CompletedAt = completedAt,
                LastSyncedAt = lastSync ?? completedAt,
                SyncedCount = count,
                Message = $"Sync completed: {count} exceptions saved"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during policy exception sync");
            SetSyncState(new PolicyExceptionSyncState
            {
                Status = "failed",
                StartedAt = GetSyncState().StartedAt,
                CompletedAt = DateTime.UtcNow,
                Message = "Policy exception sync failed.",
                Error = ex.Message
            });
        }
        finally
        {
            SyncSemaphore.Release();
        }
    }

    [HttpPost("forcepoint-enabled/bulk")]
    public async Task<ActionResult> SetForcepointExceptionsEnabledBulk(
        [FromBody] PolicyExceptionBulkToggleRequest? request,
        CancellationToken cancellationToken)
    {
        return StatusCode(StatusCodes.Status423Locked, new
        {
            success = false,
            message = "Forcepoint exception update is temporarily disabled because the Forcepoint POST API can remove existing exceptions when the update payload is not accepted as a complete rule definition."
        });

#pragma warning disable CS0162
        try
        {
            var actor = User?.Identity?.Name ?? "System";
            var refs = request?.Exceptions?.Select(e => new ForcepointExceptionToggleReference(
                e.GetPolicyName(),
                e.GetRuleName(),
                e.GetExceptionName())) ?? Enumerable.Empty<ForcepointExceptionToggleReference>();

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
#pragma warning restore CS0162
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

    private static string? FormatUtcIso(DateTime? value)
        => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("O")
            : null;

    private static PolicyExceptionSyncState GetSyncState()
    {
        lock (SyncStatusLock)
        {
            return SyncState;
        }
    }

    private static void SetSyncState(PolicyExceptionSyncState state)
    {
        lock (SyncStatusLock)
        {
            SyncState = state;
        }
    }

    private object CreateSyncResponse(string? message = null, bool? inProgress = null)
    {
        var state = GetSyncState();
        var running = state.Status == "running";

        return new
        {
            success = state.Status != "failed",
            status = state.Status,
            syncInProgress = inProgress ?? running,
            message = message ?? state.Message,
            error = state.Error,
            syncedCount = state.SyncedCount,
            startedAt = FormatUtcIso(state.StartedAt),
            completedAt = FormatUtcIso(state.CompletedAt),
            syncedAt = FormatUtcIso(state.LastSyncedAt),
            lastSyncedAt = FormatUtcIso(state.LastSyncedAt)
        };
    }

    private static async Task<DateTime?> GetLastSyncAsync(AnalyzerDbContext context)
    {
        return await context.PolicyRuleExceptions
            .AsNoTracking()
            .Select(e => (DateTime?)e.SyncedAt)
            .OrderByDescending(e => e)
            .FirstOrDefaultAsync();
    }
}

public class PolicyExceptionSyncState
{
    public string Status { get; set; } = "idle";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public int? SyncedCount { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class PolicyExceptionBulkToggleRequest
{
    public List<PolicyExceptionBulkToggleItem> Exceptions { get; set; } = new();
    public bool Enabled { get; set; }
}

public class PolicyExceptionBulkToggleItem
{
    public string? PolicyName { get; set; }
    public string? RuleName { get; set; }
    public string? ExceptionName { get; set; }
    public string? ExceptionRuleName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public string? GetPolicyName() => FirstNonEmpty(PolicyName, GetAdditionalString("policyName", "policy_name"));
    public string GetRuleName() => FirstNonEmpty(RuleName, GetAdditionalString("ruleName", "rule_name")) ?? string.Empty;
    public string GetExceptionName() => FirstNonEmpty(
        ExceptionName,
        ExceptionRuleName,
        GetAdditionalString("exceptionName", "exception_name", "exceptionRuleName", "exception_rule_name")) ?? string.Empty;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private string? GetAdditionalString(params string[] names)
    {
        if (AdditionalData == null) return null;

        foreach (var name in names)
        {
            if (AdditionalData.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }
}
