using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class IsolationForestService : IIsolationForestService
{
    public const int ScoreWindowDays = 7;

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
            .Select(s => new { s.JobId, s.CalculatedAt, s.LookbackDays })
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
            .CountAsync(s => s.JobId == latestJob.JobId);

        var anomalyCount = await db.IsolationForestScores
            .CountAsync(s => s.JobId == latestJob.JobId && s.IsAnomaly);

        // Load only anomalies + top-scoring non-anomalies (max 200 rows)
        var anomalyScores = await db.IsolationForestScores
            .Where(s => s.JobId == latestJob.JobId && s.IsAnomaly)
            .OrderByDescending(s => s.IFScore)
            .ToListAsync();

        var topNonAnomaly = await db.IsolationForestScores
            .Where(s => s.JobId == latestJob.JobId && !s.IsAnomaly)
            .OrderByDescending(s => s.IFScore)
            .Take(Math.Max(0, 200 - anomalyScores.Count))
            .ToListAsync();

        var displayScores = anomalyScores.Concat(topNonAnomaly)
            .OrderByDescending(s => s.IFScore)
            .ToList();

        var scoreDtos = displayScores.Select(ToDto).ToList();

        // Department risks — load aggregates from all rows (not just display set)
        var allScores = await db.IsolationForestScores
            .Where(s => s.JobId == latestJob.JobId)
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

        var persistedStatus = _status.IsRunning
            ? _status
            : new IsolationForestStatusDto
            {
                Status = "completed",
                LastRunAt = latestJob.CalculatedAt,
                LastUserCount = totalUsers,
                IsRunning = false
            };

        return new IsolationForestOverviewDto
        {
            Status = persistedStatus,
            UserScores = scoreDtos,
            TotalUsers = totalUsers,
            AnomalyCount = anomalyCount,
            ScoreWindowDays = latestJob.LookbackDays,
            ScoreWindowStart = latestJob.CalculatedAt.AddDays(-latestJob.LookbackDays),
            ScoreWindowEnd = latestJob.CalculatedAt,
            BaselineStrategy = "all_time_before_score_window",
            DepartmentRisks = deptRisks
        };
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    public async Task<IsolationForestStatusDto> TriggerRunAsync()
    {
        if (_status.IsRunning)
            return _status;

        // Fire & forget – return immediately
        _ = Task.Run(RunAsync);
        await Task.Delay(50); // let the task start
        return _status;
    }

    // ── Core run (called by trigger + background scheduler) ──────────────────

    public async Task RunAsync()
    {
        if (!await _lock.WaitAsync(0))
        {
            _logger.LogInformation("IsolationForestService: run already in progress, skipping");
            return;
        }

        _status = new IsolationForestStatusDto { Status = "running", IsRunning = true };
        _logger.LogInformation(
            "IsolationForestService: starting run (scoreWindowDays={Days}, baseline=all-time)",
            ScoreWindowDays);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();
            var engine = scope.ServiceProvider.GetRequiredService<IsolationForestEngine>();

            var calculatedAt = DateTime.UtcNow;
            var scoreWindowEnd = calculatedAt;
            var scoreWindowStart = scoreWindowEnd.AddDays(-ScoreWindowDays);

            var scoringIncidents = await db.Incidents
                .AsNoTracking()
                .Where(i => i.Timestamp >= scoreWindowStart && i.Timestamp <= scoreWindowEnd)
                .ToListAsync();

            if (scoringIncidents.Count == 0)
            {
                _logger.LogWarning("IsolationForestService: no incidents found for last {Days} days", ScoreWindowDays);
                _status = new IsolationForestStatusDto
                {
                    Status = "completed",
                    LastRunAt = calculatedAt,
                    LastUserCount = 0,
                    IsRunning = false
                };
                return;
            }

            var scoredUsers = scoringIncidents
                .Select(i => i.UserEmail)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Only load the all-time history of users who are active in the score window.
            // This keeps the baseline complete without pulling unrelated users into memory.
            var historicalIncidents = await db.Incidents
                .AsNoTracking()
                .Where(i => scoredUsers.Contains(i.UserEmail) && i.Timestamp < scoreWindowStart)
                .ToListAsync();

            var results = engine.Run(scoringIncidents, historicalIncidents);
            var jobId = Guid.NewGuid().ToString("N")[..12];

            // Persist
            var entities = results.Select(r => new IsolationForestScore
            {
                UserEmail = r.UserEmail,
                Department = r.Department,
                CalculatedAt = calculatedAt,
                LookbackDays = ScoreWindowDays,
                IFScore = r.IFScore,
                AnomalyRaw = r.IFScore,
                IsAnomaly = r.IsAnomaly,
                IncidentCount = r.IncidentCount,
                BaselineIncidentCount = r.BaselineIncidentCount,
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
                LastRunAt = calculatedAt,
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
            BaselineIncidentCount = s.BaselineIncidentCount,
            TopFeatures = features,
            GroupBreakdown = groups
        };
    }
}
