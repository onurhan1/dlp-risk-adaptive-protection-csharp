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
        var candidates = await BuildCandidatesAsync(days, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        return new(DateTime.UtcNow, days, candidates.Count, candidates.OrderByDescending(x => x.ShadowScore).Take(take).ToList(), await GetReviewSummaryAsync(ct));
    }

    public async Task<RiskShadowListResult> GetHighRiskCandidatesAsync(int days = 7, double minimumScore = 70, int take = 20, DateOnly? endDate = null, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 90); take = Math.Clamp(take, 1, 50); minimumScore = Math.Clamp(minimumScore, 0, 100);
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var candidates = await BuildCandidatesAsync(days, end, ct);
        var matching = candidates.Where(candidate => candidate.ShadowScore >= minimumScore).OrderByDescending(candidate => candidate.ShadowScore).ToList();
        return new(DateTime.UtcNow, end.AddDays(-days + 1), end, minimumScore, matching.Count, matching.Take(take).ToList());
    }

    private async Task<List<RiskShadowCandidate>> BuildCandidatesAsync(int days, DateOnly end, CancellationToken ct)
    {
        var start = end.AddDays(-days + 1);
        var current = await context.UserDailyRiskScores.AsNoTracking().Where(x => x.Date >= start && x.Date <= end)
            .GroupBy(x => new { x.UserEmail, x.Team }).Select(g => new { g.Key.UserEmail, g.Key.Team, Score = g.Average(x => x.DailyRiskScore), Incidents = g.Sum(x => x.IncidentCount) })
            .ToListAsync(ct);
        if (current.Count == 0) return [];

        var userEmails = current.Select(item => item.UserEmail).Distinct().ToList();
        var baselineStart = start.AddDays(-60);
        var baselineRows = await context.UserDailyRiskScores.AsNoTracking()
            .Where(x => userEmails.Contains(x.UserEmail) && x.Date >= baselineStart && x.Date < start)
            .Select(x => new { x.UserEmail, x.DailyRiskScore }).ToListAsync(ct);
        var baselineByUser = baselineRows
            .GroupBy(item => item.UserEmail, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Average(item => item.DailyRiskScore), StringComparer.OrdinalIgnoreCase);
        var latestAt = await context.IsolationForestScores.AsNoTracking().MaxAsync(x => (DateTime?)x.CalculatedAt, ct);
        var latestScores = await context.IsolationForestScores.AsNoTracking().Where(x => latestAt != null && x.CalculatedAt == latestAt.Value)
            .Select(x => new { x.UserEmail, x.IFScore, x.BaselineIncidentCount, x.IsAnomaly }).ToListAsync(ct);
        var ifByUser = latestScores
            .GroupBy(item => item.UserEmail, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group =>
            {
                var chosen = group.OrderByDescending(item => item.IFScore).ThenByDescending(item => item.BaselineIncidentCount).First();
                return (Score: chosen.IFScore, Baseline: chosen.BaselineIncidentCount, Anomaly: chosen.IsAnomaly);
            }, StringComparer.OrdinalIgnoreCase);
        var results = new List<RiskShadowCandidate>();
        foreach (var user in current)
        {
            var baseline = baselineByUser.GetValueOrDefault(user.UserEmail);
            var delta = user.Score - baseline;
            ifByUser.TryGetValue(user.UserEmail, out var forest);
            var shadow = Math.Clamp(user.Score * .55 + forest.Score * .30 + Math.Clamp(delta, 0, 30) * .50, 0, 100);
            var evidence = new List<string> { $"Günlük risk ortalaması: {user.Score:F1}", $"Kişisel baz çizgisi farkı: {delta:+0.0;-0.0;0.0}" };
            if (latestAt != null) evidence.Add($"Isolation Forest: {forest.Score:F1}" + (forest.Anomaly ? " (anomali)" : string.Empty));
            results.Add(new(user.UserEmail, user.Team, Math.Round(shadow, 1), Math.Round(user.Score, 1), Math.Round(forest.Score, 1), Math.Round(delta, 1), user.Incidents,
                forest.Baseline >= 10 ? "high" : forest.Baseline > 0 ? "medium" : "low", evidence));
        }
        return results;
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
