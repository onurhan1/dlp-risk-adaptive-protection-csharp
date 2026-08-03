using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

internal sealed record SurprisalRunResult
{
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<EventToken> BaselineTokens { get; init; } = Array.Empty<EventToken>();
    public IReadOnlyList<EventSurprisal> ScoredEvents { get; init; } = Array.Empty<EventSurprisal>();
    public IReadOnlyList<UserRisk> UserRisks { get; init; } = Array.Empty<UserRisk>();
    public IReadOnlyDictionary<string, int> BaselineCounts { get; init; } = new Dictionary<string, int>();
    public ClusteringResult Clustering { get; init; } =
        new(new Dictionary<string, string>(), Array.Empty<BehaviorCluster>(), Array.Empty<string>(), 0, 0);
    public ExcitationModel Excitation { get; init; } = null!;
    public IReadOnlyList<string>? IsolationForestComparison { get; init; }
}

public interface ISurprisalRiskService
{
    /// <summary>Fits the model on history, scores the recent window, and renders the tuning report.</summary>
    Task<string> BuildDiagnosticReportAsync(CancellationToken ct = default);
}

/// <summary>
/// Orchestrates the behavioural surprisal model: fit on the trailing baseline, score the recent
/// window, accumulate with decay, and render diagnostics.
///
/// The model is deliberately fitted fresh on every run rather than persisted. At this data volume
/// fitting is counting — cheap — and a stateless fit means the baseline always reflects the current
/// trailing window, which is what stops a stale profile from anchoring somebody who changed roles.
/// </summary>
internal sealed class SurprisalRiskService : ISurprisalRiskService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SurprisalRiskService> _logger;
    private readonly SurprisalOptions _options;

    public SurprisalRiskService(
        IServiceProvider serviceProvider,
        ILogger<SurprisalRiskService> logger,
        IOptions<SurprisalOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> BuildDiagnosticReportAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(ct);
        return SurprisalDiagnostics.Render(result, _options);
    }

    internal async Task<SurprisalRunResult> RunAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();

        var now = DateTime.UtcNow;
        var baselineStart = now.AddDays(-_options.BaselineWindowDays);
        var scoreStart = now.AddDays(-_options.ScoreWindowDays);

        var incidents = await db.Incidents
            .AsNoTracking()
            .Where(i => i.Timestamp >= baselineStart && i.Timestamp <= now)
            .OrderBy(i => i.Timestamp)
            .ToListAsync(ct);

        _logger.LogInformation(
            "SurprisalRiskService: loaded {Count} incidents over {Days} days",
            incidents.Count, _options.BaselineWindowDays);

        return Run(incidents, now, await LoadIsolationForestTopAsync(db, ct));
    }

    /// <summary>Pure, DB-free core — this is what the tests exercise.</summary>
    internal SurprisalRunResult Run(
        IReadOnlyList<Incident> incidents,
        DateTime asOf,
        IReadOnlyList<string>? isolationForestTop = null)
    {
        var tokenizer = new EventTokenizer(_options);
        var all = tokenizer.Tokenize(incidents.Where(i => !string.IsNullOrWhiteSpace(i.UserEmail ?? i.LoginName)));

        var scoreStart = asOf.AddDays(-_options.ScoreWindowDays);

        // Everything in the window feeds the baseline; only the recent slice is scored. An event
        // must not be part of the distribution it is being judged against.
        var baseline = all.Where(t => t.Timestamp < scoreStart).ToList();
        var scoring = all.Where(t => t.Timestamp >= scoreStart).ToList();

        // With a short history there is nothing to compare against, so fall back to fitting on
        // everything and report it rather than silently producing noise.
        if (baseline.Count < scoring.Count / 4)
        {
            _logger.LogWarning(
                "SurprisalRiskService: baseline ({Baseline}) is thin next to the scoring window ({Scoring}); fitting on all events",
                baseline.Count, scoring.Count);
            baseline = all;
        }

        var profiler = new BehaviorProfiler(_options);
        var clustering = profiler.Build(baseline);

        var model = new SurprisalModel(_options);
        model.Fit(baseline, clustering.ClusterOf, asOf);

        var excitation = new ExcitationModel(_options);
        excitation.Fit(baseline);

        var scored = ScoreWindow(scoring, model, excitation);

        var baselineCounts = baseline
            .GroupBy(t => t.User, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var user in scoring.Select(t => t.User).Distinct(StringComparer.OrdinalIgnoreCase))
            baselineCounts.TryAdd(user, 0);

        var userRisks = new RiskAccumulator(_options)
            .Accumulate(scored, clustering.ClusterOf, baselineCounts, asOf);

        return new SurprisalRunResult
        {
            GeneratedAt = asOf,
            BaselineTokens = baseline,
            ScoredEvents = scored,
            UserRisks = userRisks,
            BaselineCounts = baselineCounts,
            Clustering = clustering,
            Excitation = excitation,
            IsolationForestComparison = CompareWithIsolationForest(userRisks, isolationForestTop)
        };
    }

    /// <summary>
    /// Scores each event against the model, letting recent predecessors excite it. The predecessor
    /// list is kept per user and trimmed to the excitation window, so this stays linear.
    /// </summary>
    private List<EventSurprisal> ScoreWindow(
        IReadOnlyList<EventToken> scoring, SurprisalModel model, ExcitationModel excitation)
    {
        var scored = new List<EventSurprisal>(scoring.Count);

        foreach (var perUser in scoring.GroupBy(t => t.User, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = perUser.OrderBy(t => t.Timestamp).ToList();
            var recent = new List<EventToken>();

            foreach (var token in ordered)
            {
                recent.RemoveAll(p => (token.Timestamp - p.Timestamp).TotalMinutes > _options.ExcitationWindowMinutes);
                var (multiplier, by) = excitation.MultiplierFor(token, recent);
                scored.Add(model.Score(token, multiplier, by));
                recent.Add(token);
            }
        }

        return scored.OrderByDescending(s => s.Score).ToList();
    }

    // ── Comparison against the incumbent model ───────────────────────────────

    private async Task<IReadOnlyList<string>?> LoadIsolationForestTopAsync(AnalyzerDbContext db, CancellationToken ct)
    {
        try
        {
            var latestJob = await db.IsolationForestScores
                .OrderByDescending(s => s.CalculatedAt)
                .Select(s => s.JobId)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrEmpty(latestJob)) return null;

            return await db.IsolationForestScores
                .AsNoTracking()
                .Where(s => s.JobId == latestJob)
                .OrderByDescending(s => s.IFScore)
                .Take(50)
                .Select(s => s.UserEmail)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SurprisalRiskService: could not load isolation-forest scores for comparison");
            return null;
        }
    }

    private static IReadOnlyList<string>? CompareWithIsolationForest(
        IReadOnlyList<UserRisk> risks, IReadOnlyList<string>? isolationForestTop)
    {
        if (isolationForestTop is not { Count: > 0 }) return null;

        var ifTop = isolationForestTop.Take(50).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mine = risks.Take(50).Select(r => r.User).ToList();
        var overlap = mine.Count(u => ifTop.Contains(u));

        var lines = new List<string>
        {
            $"İlk 50'de örtüşme: **{overlap} / 50** (%{overlap * 2})",
            $"Yalnızca sürpriz modelinin işaretledikleri: {50 - overlap} kullanıcı",
        };

        var onlyMine = mine.Where(u => !ifTop.Contains(u)).Take(10).ToList();
        if (onlyMine.Count > 0)
            lines.Add("Örnek (yalnızca yeni model): " + string.Join(", ", onlyMine.Select(MaskShort)));

        lines.Add("Düşük örtüşme beklenir ve iyiye işarettir — iki model farklı sorulara bakıyor. " +
                  "Ama %0 örtüşme bir tarafta hata olduğunu düşündürür.");

        return lines;
    }

    private static string MaskShort(string email) => SurprisalDiagnostics.Mask(email);
}
