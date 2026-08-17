using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ScheduledJobService : IScheduledJobService
{
    private readonly AnalyzerDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledJobService> _logger;

    public ScheduledJobService(
        AnalyzerDbContext context,
        IServiceProvider serviceProvider,
        ILogger<ScheduledJobService> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<object>> GetJobsAsync(CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);

        var jobs = await _context.ScheduledJobs
            .AsNoTracking()
            .OrderBy(j => j.Name)
            .ToListAsync(ct);

        var jobIds = jobs.Select(j => j.Id).ToList();
        var lastRuns = await _context.ScheduledJobRuns
            .AsNoTracking()
            .Where(r => jobIds.Contains(r.ScheduledJobId))
            .GroupBy(r => r.ScheduledJobId)
            .Select(g => g.OrderByDescending(r => r.StartedAt).First())
            .ToListAsync(ct);

        return jobs.Select(j => ToDto(j, lastRuns.FirstOrDefault(r => r.ScheduledJobId == j.Id))).ToList();
    }

    public Task<object> GetCatalogAsync()
    {
        var handlers = ScheduledJobHandlerKeys.Labels.Select(kvp => new
        {
            key = kvp.Key,
            label = kvp.Value
        });

        return Task.FromResult<object>(new { handlers });
    }

    public async Task<object> CreateAsync(ScheduledJobRequest request, CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);
        Validate(request);

        var now = DateTime.UtcNow;
        var job = new ScheduledJob
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            HandlerKey = request.HandlerKey.Trim(),
            HandlerPayloadJson = BuildPayload(request),
            CronExpression = request.CronExpression.Trim(),
            Enabled = request.Enabled,
            NextRunAt = request.Enabled ? CronSchedule.Next(request.CronExpression, now) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ScheduledJobs.Add(job);
        await _context.SaveChangesAsync(ct);

        return ToDto(job, null);
    }

    public async Task<object> UpdateAsync(int id, ScheduledJobRequest request, CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);
        Validate(request);

        var job = await _context.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
                  ?? throw new KeyNotFoundException("Zamanlanmis is bulunamadi");

        var now = DateTime.UtcNow;
        job.Name = request.Name.Trim();
        job.Description = request.Description?.Trim();
        job.HandlerKey = request.HandlerKey.Trim();
        job.HandlerPayloadJson = BuildPayload(request);
        job.CronExpression = request.CronExpression.Trim();
        job.Enabled = request.Enabled;
        job.NextRunAt = request.Enabled ? CronSchedule.Next(request.CronExpression, now) : null;
        job.UpdatedAt = now;

        await _context.SaveChangesAsync(ct);
        return ToDto(job, null);
    }

    public async Task<object> ToggleAsync(int id, CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);

        var job = await _context.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
                  ?? throw new KeyNotFoundException("Zamanlanmis is bulunamadi");

        job.Enabled = !job.Enabled;
        job.NextRunAt = job.Enabled ? CronSchedule.Next(job.CronExpression, DateTime.UtcNow) : null;
        job.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(job, null);
    }

    public async Task<object> RunNowAsync(int id, CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);

        var job = await _context.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
                  ?? throw new KeyNotFoundException("Zamanlanmis is bulunamadi");

        var run = await ExecuteAsync(job, ScheduledJobTriggerType.Manual, ct);
        return ToRunDto(run);
    }

    public async Task<IReadOnlyList<object>> GetRunsAsync(int? jobId, int limit, CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);

        var take = limit <= 0 ? 50 : Math.Min(limit, 500);
        var query = _context.ScheduledJobRuns.AsNoTracking();
        if (jobId.HasValue) query = query.Where(r => r.ScheduledJobId == jobId.Value);

        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(ct);

        return runs.Select(ToRunDto).ToList();
    }

    public async Task RunDueJobsAsync(CancellationToken ct = default)
    {
        await ScheduledJobSchema.EnsureAsync(_context, _logger, ct);

        var now = DateTime.UtcNow;
        var dueJobs = await _context.ScheduledJobs
            .Where(j => j.Enabled && j.NextRunAt != null && j.NextRunAt <= now)
            .OrderBy(j => j.NextRunAt)
            .Take(10)
            .ToListAsync(ct);

        foreach (var job in dueJobs)
        {
            job.NextRunAt = CronSchedule.Next(job.CronExpression, now);
            job.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);
            await ExecuteAsync(job, ScheduledJobTriggerType.Schedule, ct);
        }
    }

    private async Task<ScheduledJobRun> ExecuteAsync(ScheduledJob job, string triggerType, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var run = new ScheduledJobRun
        {
            ScheduledJobId = job.Id,
            StartedAt = started,
            TriggerType = triggerType,
            Status = ScheduledJobRunStatus.Running
        };

        _context.ScheduledJobRuns.Add(run);
        job.LastRunAt = started;
        job.LastStatus = ScheduledJobRunStatus.Running;
        job.LastMessage = null;
        await _context.SaveChangesAsync(ct);

        try
        {
            var result = await ExecuteHandlerAsync(job, ct);
            var finished = DateTime.UtcNow;
            run.FinishedAt = finished;
            run.Status = ScheduledJobRunStatus.Success;
            run.Message = result.message;
            run.ResultJson = result.resultJson;

            job.LastRunAt = started;
            job.LastStatus = ScheduledJobRunStatus.Success;
            job.LastMessage = result.message;
            job.UpdatedAt = finished;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled job {JobId} failed", job.Id);
            var finished = DateTime.UtcNow;
            run.FinishedAt = finished;
            run.Status = ScheduledJobRunStatus.Failed;
            run.Message = ex.Message;

            job.LastRunAt = started;
            job.LastStatus = ScheduledJobRunStatus.Failed;
            job.LastMessage = ex.Message;
            job.UpdatedAt = finished;
        }

        await _context.SaveChangesAsync(ct);
        return run;
    }

    private async Task<(string message, string resultJson)> ExecuteHandlerAsync(ScheduledJob job, CancellationToken ct)
    {
        var payload = ParsePayload(job.HandlerPayloadJson);

        switch (job.HandlerKey)
        {
            case ScheduledJobHandlerKeys.ReleasedIncidentSync:
            {
                var lookbackHours = payload.TryGetValue("lookback_hours", out var value) && value.ValueKind == JsonValueKind.Number
                    ? value.GetInt32()
                    : 24;
                var sync = _serviceProvider.GetRequiredService<IReleasedIncidentSyncService>();
                var result = await sync.SyncAsync(lookbackHours);
                return ($"Released incident sync tamamlandi: {result.Inserted} eklendi, {result.Skipped} atlandi",
                    JsonSerializer.Serialize(result));
            }
            case ScheduledJobHandlerKeys.PolicyExceptionSync:
            {
                var sync = _serviceProvider.GetRequiredService<IPolicyExceptionSyncService>();
                var count = await sync.SyncAsync(force: true);
                return ($"Policy exception sync tamamlandi: {count} kayit", JsonSerializer.Serialize(new { count }));
            }
            case ScheduledJobHandlerKeys.IsolationForest:
            {
                var service = _serviceProvider.GetRequiredService<IIsolationForestService>();
                await service.RunAsync();
                return ("Isolation Forest analizi tamamlandi", "{}");
            }
            case ScheduledJobHandlerKeys.LogCleanup:
            {
                var retentionDays = payload.TryGetValue("retention_days", out var value) && value.ValueKind == JsonValueKind.Number
                    ? value.GetInt32()
                    : 90;
                var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
                var auditDeleted = await _context.AuditLogs.Where(l => l.Timestamp < cutoff).ExecuteDeleteAsync(ct);
                var activityDeleted = await _context.UserActivityLogs.Where(l => l.Timestamp < cutoff).ExecuteDeleteAsync(ct);
                return ($"Log cleanup tamamlandi: {auditDeleted + activityDeleted} kayit silindi",
                    JsonSerializer.Serialize(new { retention_days = retentionDays, audit_deleted = auditDeleted, activity_deleted = activityDeleted }));
            }
            case ScheduledJobHandlerKeys.WeeklyHighScoreUsersReport:
            case ScheduledJobHandlerKeys.TopPermitUsersReport:
            case ScheduledJobHandlerKeys.TopBlockUsersReport:
            case ScheduledJobHandlerKeys.HighMaxMatchTransfersReport:
            {
                var reportService = _serviceProvider.GetRequiredService<IScheduledReportService>();
                var options = new ScheduledReportOptions
                {
                    RecipientEmail = GetString(payload, "recipient_email"),
                    CcEmail = GetString(payload, "cc_email"),
                    LookbackDays = GetInt(payload, "lookback_days", 7),
                    TopLimit = GetInt(payload, "top_limit", 25),
                    MinRiskScore = GetInt(payload, "min_risk_score", 80),
                    MaxMatchThreshold = GetInt(payload, "max_match_threshold", 300)
                };
                var result = await reportService.SendReportAsync(job.HandlerKey, options, ct);
                return (result.Message, JsonSerializer.Serialize(result));
            }
            default:
                throw new InvalidOperationException($"Desteklenmeyen is tipi: {job.HandlerKey}");
        }
    }

    private static Dictionary<string, JsonElement> ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return new Dictionary<string, JsonElement>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson) ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static string BuildPayload(ScheduledJobRequest request)
    {
        var payload = new Dictionary<string, object>();
        if (request.LookbackHours.HasValue) payload["lookback_hours"] = Math.Max(1, request.LookbackHours.Value);
        if (request.RetentionDays.HasValue) payload["retention_days"] = Math.Max(1, request.RetentionDays.Value);
        if (request.LookbackDays.HasValue) payload["lookback_days"] = Math.Max(1, request.LookbackDays.Value);
        if (request.TopLimit.HasValue) payload["top_limit"] = Math.Max(1, request.TopLimit.Value);
        if (request.MinRiskScore.HasValue) payload["min_risk_score"] = Math.Clamp(request.MinRiskScore.Value, 0, 100);
        if (request.MaxMatchThreshold.HasValue) payload["max_match_threshold"] = Math.Max(1, request.MaxMatchThreshold.Value);
        if (!string.IsNullOrWhiteSpace(request.RecipientEmail)) payload["recipient_email"] = request.RecipientEmail.Trim();
        if (!string.IsNullOrWhiteSpace(request.CcEmail)) payload["cc_email"] = request.CcEmail.Trim();
        return JsonSerializer.Serialize(payload);
    }

    private static void Validate(ScheduledJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Is adi zorunludur");
        if (!ScheduledJobHandlerKeys.Labels.ContainsKey(request.HandlerKey)) throw new ArgumentException("Gecersiz is tipi");
        if (!CronSchedule.TryParse(request.CronExpression, out _, out var cronError))
            throw new ArgumentException(cronError ?? "Gecersiz cron ifadesi");
    }

    private static object ToDto(ScheduledJob job, ScheduledJobRun? lastRun) => new
    {
        id = job.Id,
        name = job.Name,
        description = job.Description,
        handler_key = job.HandlerKey,
        handler_label = ScheduledJobHandlerKeys.Labels.GetValueOrDefault(job.HandlerKey, job.HandlerKey),
        handler_payload = ParsePayload(job.HandlerPayloadJson),
        cron_expression = job.CronExpression,
        schedule_summary = CronSchedule.Describe(job.CronExpression),
        enabled = job.Enabled,
        last_run_at = job.LastRunAt,
        next_run_at = job.NextRunAt,
        last_status = job.LastStatus,
        last_message = job.LastMessage,
        last_run = lastRun == null ? null : ToRunDto(lastRun),
        created_at = job.CreatedAt,
        updated_at = job.UpdatedAt
    };

    private static object ToRunDto(ScheduledJobRun run) => new
    {
        id = run.Id,
        scheduled_job_id = run.ScheduledJobId,
        started_at = run.StartedAt,
        finished_at = run.FinishedAt,
        trigger_type = run.TriggerType,
        status = run.Status,
        message = run.Message,
        result = ParsePayload(run.ResultJson)
    };

    private static int GetInt(Dictionary<string, JsonElement> payload, string key, int fallback) =>
        payload.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;

    private static string? GetString(Dictionary<string, JsonElement> payload, string key) =>
        payload.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
