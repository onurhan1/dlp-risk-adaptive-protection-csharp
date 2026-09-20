using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public sealed class RiskShadowService(AnalyzerDbContext context) : IRiskShadowService
{
    public async Task<RiskShadowReviewSummary> RecordReviewAsync(string userEmail, string verdict, string reviewer, string? note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) throw new ArgumentException("Kullanıcı zorunludur.");
        if (verdict is not ("confirmed" or "false_positive" or "needs_review")) throw new ArgumentException("Geçersiz değerlendirme.");
        await RiskShadowSchema.EnsureAsync(context, ct);
        context.RiskShadowReviews.Add(new RiskShadowReview { UserEmail = userEmail.Trim(), Verdict = verdict, Reviewer = reviewer, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 1000)], CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(ct);
        return await GetReviewSummaryAsync(ct);
    }
    public async Task<RiskShadowSnapshot> GetSnapshotAsync(int days = 30, int take = 20, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 90); take = Math.Clamp(take, 1, 50);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-days + 1);
        var current = await context.UserDailyRiskScores.AsNoTracking().Where(x => x.Date >= start && x.Date <= today)
            .GroupBy(x => new { x.UserEmail, x.Team }).Select(g => new { g.Key.UserEmail, g.Key.Team, Score = g.Average(x => x.DailyRiskScore), Incidents = g.Sum(x => x.IncidentCount) })
            .OrderByDescending(x => x.Score).Take(take).ToListAsync(ct);
        var latestAt = await context.IsolationForestScores.AsNoTracking().MaxAsync(x => (DateTime?)x.CalculatedAt, ct);
        var ifByUser = latestAt == null ? new Dictionary<string, (double Score, int Baseline, bool Anomaly)>() :
            (await context.IsolationForestScores.AsNoTracking().Where(x => x.CalculatedAt == latestAt).Select(x => new { x.UserEmail, x.IFScore, x.BaselineIncidentCount, x.IsAnomaly }).ToListAsync(ct))
            .ToDictionary(x => x.UserEmail, x => (Score: x.IFScore, Baseline: x.BaselineIncidentCount, Anomaly: x.IsAnomaly), StringComparer.OrdinalIgnoreCase);
        var results = new List<RiskShadowCandidate>();
        foreach (var user in current)
        {
            var baselineStart = start.AddDays(-60);
            var baseline = await context.UserDailyRiskScores.AsNoTracking().Where(x => x.UserEmail == user.UserEmail && x.Date >= baselineStart && x.Date < start)
                .Select(x => (double?)x.DailyRiskScore).AverageAsync(ct) ?? 0;
            var delta = user.Score - baseline;
            ifByUser.TryGetValue(user.UserEmail, out var forest);
            var shadow = Math.Clamp(user.Score * .55 + forest.Score * .30 + Math.Clamp(delta, 0, 30) * .50, 0, 100);
            var evidence = new List<string> { $"Günlük risk ortalaması: {user.Score:F1}", $"Kişisel baz çizgisi farkı: {delta:+0.0;-0.0;0.0}" };
            if (latestAt != null) evidence.Add($"Isolation Forest: {forest.Score:F1}" + (forest.Anomaly ? " (anomali)" : string.Empty));
            results.Add(new(user.UserEmail, user.Team, Math.Round(shadow, 1), Math.Round(user.Score, 1), Math.Round(forest.Score, 1), Math.Round(delta, 1), user.Incidents,
                forest.Baseline >= 10 ? "high" : forest.Baseline > 0 ? "medium" : "low", evidence));
        }
        return new(DateTime.UtcNow, days, results.Count, results.OrderByDescending(x => x.ShadowScore).ToList(), await GetReviewSummaryAsync(ct));
    }

    private async Task<RiskShadowReviewSummary> GetReviewSummaryAsync(CancellationToken ct)
    {
        if (!context.Database.IsRelational()) return new(0, 0, 0, 0, 0);
        await RiskShadowSchema.EnsureAsync(context, ct);
        var counts = await context.RiskShadowReviews.AsNoTracking().GroupBy(x => x.Verdict).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var confirmed = counts.FirstOrDefault(x => x.Key == "confirmed")?.Count ?? 0;
        var falsePositive = counts.FirstOrDefault(x => x.Key == "false_positive")?.Count ?? 0;
        var needsReview = counts.FirstOrDefault(x => x.Key == "needs_review")?.Count ?? 0;
        var reviewed = confirmed + falsePositive;
        return new(confirmed, falsePositive, needsReview, reviewed, reviewed == 0 ? 0 : Math.Round(confirmed * 100d / reviewed, 1));
    }
}
