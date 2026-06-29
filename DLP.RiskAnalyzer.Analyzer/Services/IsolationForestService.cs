using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class IsolationForestService : IIsolationForestService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IsolationForestService> _logger;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    // In-memory status (survives within process lifetime)
    private static IsolationForestStatusDto _status = new() { Status = "idle" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public IsolationForestService(IServiceProvider serviceProvider, ILogger<IsolationForestService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // ── Status ────────────────────────────────────────────────────────────────

    public Task<IsolationForestStatusDto> GetStatusAsync() => Task.FromResult(_status);

    // ── Overview ─────────────────────────────────────────────────────────────

    public async Task<IsolationForestOverviewDto> GetOverviewAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();

        // Get the latest job's scores
        var latestJob = await db.IsolationForestScores
            .OrderByDescending(s => s.CalculatedAt)
            .Select(s => s.JobId)
            .FirstOrDefaultAsync();

        if (latestJob == null)
        {
            return new IsolationForestOverviewDto
            {
                Status = _status,
                UserScores = new(),
                TotalUsers = 0,
                AnomalyCount = 0,
                DepartmentRisks = new()
            };
        }        // Aggregate counts without loading all rows into memory
        var totalUsers = await db.IsolationForestScores
            .CountAsync(s => s.JobId == latestJob);

        var anomalyCount = await db.IsolationForestScores
            .CountAsync(s => s.JobId == latestJob && s.IsAnomaly);

        // Load only anomalies + top-scoring non-anomalies (max 200 rows)
        var anomalyScores = await db.IsolationForestScores
            .Where(s => s.JobId == latestJob && s.IsAnomaly)
            .OrderByDescending(s => s.IFScore)
            .ToListAsync();

        var topNonAnomaly = await db.IsolationForestScores
            .Where(s => s.JobId == latestJob && !s.IsAnomaly)
            .OrderByDescending(s => s.IFScore)
            .Take(Math.Max(0, 200 - anomalyScores.Count))
            .ToListAsync();

        var displayScores = anomalyScores.Concat(topNonAnomaly)
            .OrderByDescending(s => s.IFScore)
            .ToList();

        var scoreDtos = displayScores.Select(ToDto).ToList();

        // Department risks — load aggregates from all rows (not just display set)
        var allScores = await db.IsolationForestScores
            .Where(s => s.JobId == latestJob)
            .Select(s => new { s.Department, s.IFScore, s.IsAnomaly })
            .ToListAsync();

        var deptRisks = allScores
            .GroupBy(s => s.Department ?? "Unknown")
            .Select(g => new DepartmentIFRiskDto
            {
                Department = g.Key,
                UserCount = g.Count(),
                AnomalyCount = g.Count(s => s.IsAnomaly),
                MeanScore = Math.Round(g.Average(s => s.IFScore), 1),
                MaxScore = Math.Round(g.Max(s => s.IFScore), 1)
            })
            .OrderByDescending(d => d.AnomalyCount)
            .ThenByDescending(d => d.MeanScore)
            .ToList();

        return new IsolationForestOverviewDto
        {
            Status = _status,
            UserScores = scoreDtos,
            TotalUsers = totalUsers,
            AnomalyCount = anomalyCount,
            DepartmentRisks = deptRisks
        };
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    public async Task<IsolationForestStatusDto> TriggerRunAsync(int lookbackDays = 30)
    {
        if (_status.IsRunning)
            return _status;

        // Fire & forget – return immediately
        _ = Task.Run(() => RunAsync(lookbackDays));
        await Task.Delay(50); // let the task start
        return _status;
    }

    // ── Core run (called by trigger + background scheduler) ──────────────────

    public async Task RunAsync(int lookbackDays = 30)
    {
        if (!await _lock.WaitAsync(0))
        {
            _logger.LogInformation("IsolationForestService: run already in progress, skipping");
            return;
        }

        _status = new IsolationForestStatusDto { Status = "running", IsRunning = true };
        _logger.LogInformation("IsolationForestService: starting run (lookbackDays={Days})", lookbackDays);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();
            var engine = scope.ServiceProvider.GetRequiredService<IsolationForestEngine>();

            var since = DateTime.UtcNow.AddDays(-lookbackDays);
            var incidents = await db.Incidents
                .Where(i => i.Timestamp >= since)
                .ToListAsync();

            if (incidents.Count == 0)
            {
                _logger.LogWarning("IsolationForestService: no incidents found for last {Days} days", lookbackDays);
                _status = new IsolationForestStatusDto
                {
                    Status = "completed",
                    LastRunAt = DateTime.UtcNow,
                    LastUserCount = 0,
                    IsRunning = false
                };
                return;
            }

            var results = engine.Run(incidents);
            var jobId = Guid.NewGuid().ToString("N")[..12];

            // Persist
            var entities = results.Select(r => new IsolationForestScore
            {
                UserEmail = r.UserEmail,
                Department = r.Department,
                CalculatedAt = DateTime.UtcNow,
                LookbackDays = lookbackDays,
                IFScore = r.IFScore,
                AnomalyRaw = r.IFScore,
                IsAnomaly = r.IsAnomaly,
                IncidentCount = r.IncidentCount,
                FeatureContributions = JsonSerializer.Serialize(r.TopFeatures, JsonOpts),
                GroupBreakdown = JsonSerializer.Serialize(r.GroupBreakdown, JsonOpts),
                JobId = jobId
            }).ToList();

            await db.IsolationForestScores.AddRangeAsync(entities);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "IsolationForestService: completed. {Users} users scored, {Anomalies} anomalies, jobId={JobId}",
                results.Count, results.Count(r => r.IsAnomaly), jobId);

            _status = new IsolationForestStatusDto
            {
                Status = "completed",
                LastRunAt = DateTime.UtcNow,
                LastUserCount = results.Count,
                IsRunning = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IsolationForestService: run failed");
            _status = new IsolationForestStatusDto
            {
                Status = "failed",
                IsRunning = false,
                LastError = ex.Message
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IsolationForestScoreDto ToDto(IsolationForestScore s)
    {
        List<FeatureContributionDto> features;
        Dictionary<string, double> groups;

        try { features = JsonSerializer.Deserialize<List<FeatureContributionDto>>(s.FeatureContributions, JsonOpts) ?? new(); }
        catch { features = new(); }

        try { groups = JsonSerializer.Deserialize<Dictionary<string, double>>(s.GroupBreakdown, JsonOpts) ?? new(); }
        catch { groups = new(); }

        return new IsolationForestScoreDto
        {
            UserEmail = s.UserEmail,
            Department = s.Department,
            CalculatedAt = s.CalculatedAt,
            IFScore = s.IFScore,
            IsAnomaly = s.IsAnomaly,
            IncidentCount = s.IncidentCount,
            TopFeatures = features,
            GroupBreakdown = groups
        };
    }
}
