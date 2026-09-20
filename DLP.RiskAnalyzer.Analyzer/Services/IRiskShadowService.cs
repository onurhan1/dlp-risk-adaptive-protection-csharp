namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>Read-only, explainable risk ranking used for analyst shadow review.</summary>
public interface IRiskShadowService
{
    Task<RiskShadowSnapshot> GetSnapshotAsync(int days = 30, int take = 20, CancellationToken ct = default);
    Task<RiskShadowListResult> GetHighRiskCandidatesAsync(int days = 7, double minimumScore = 70, int take = 20, DateOnly? endDate = null, CancellationToken ct = default);
    Task<RiskShadowReviewSummary> RecordReviewAsync(string userEmail, string verdict, string reviewer, string? note, CancellationToken ct = default);
}

public sealed record RiskShadowSnapshot(DateTime GeneratedAtUtc, int Days, int CandidateCount, IReadOnlyList<RiskShadowCandidate> Candidates, RiskShadowReviewSummary Reviews);
public sealed record RiskShadowCandidate(string UserEmail, string? Team, double ShadowScore, double DailyRiskScore,
    double IsolationForestScore, double BaselineDelta, int IncidentCount, string Confidence, IReadOnlyList<string> Evidence);
public sealed record RiskShadowListResult(DateTime GeneratedAtUtc, DateOnly StartDate, DateOnly EndDate, double MinimumScore,
    int MatchingCandidateCount, IReadOnlyList<RiskShadowCandidate> Candidates);
public sealed record RiskShadowReviewSummary(int Confirmed, int FalsePositive, int NeedsReview, int Reviewed, double Precision);
