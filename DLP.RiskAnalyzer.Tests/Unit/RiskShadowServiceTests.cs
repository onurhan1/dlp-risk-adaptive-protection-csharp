using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class RiskShadowServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_RanksRiskAndIncludesExplainableEvidence()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AnalyzerDbContext(options);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.UserDailyRiskScores.AddRange(
            new UserDailyRiskScore { UserEmail = "high@kt.com", Team = "IT", Date = today, DailyRiskScore = 80, IncidentCount = 8 },
            new UserDailyRiskScore { UserEmail = "high@kt.com", Team = "IT", Date = today.AddDays(-35), DailyRiskScore = 20, IncidentCount = 1 },
            new UserDailyRiskScore { UserEmail = "low@kt.com", Team = "IT", Date = today, DailyRiskScore = 30, IncidentCount = 2 });
        db.IsolationForestScores.Add(new IsolationForestScore { UserEmail = "high@kt.com", CalculatedAt = DateTime.UtcNow, IFScore = 90, IsAnomaly = true, BaselineIncidentCount = 12, JobId = "test" });
        await db.SaveChangesAsync();

        var snapshot = await new RiskShadowService(db).GetSnapshotAsync(30, 10);

        snapshot.Candidates.Should().HaveCount(2);
        snapshot.Candidates[0].UserEmail.Should().Be("high@kt.com");
        snapshot.Candidates[0].Confidence.Should().Be("high");
        snapshot.Candidates[0].Evidence.Should().Contain(item => item.Contains("Isolation Forest"));
    }

    [Fact]
    public async Task GetHighRiskCandidatesAsync_AppliesThresholdAndReportsAllMatchingUsers()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AnalyzerDbContext(options);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.UserDailyRiskScores.AddRange(
            new UserDailyRiskScore { UserEmail = "high@kt.com", Team = "IT", Date = today, DailyRiskScore = 80, IncidentCount = 8 },
            new UserDailyRiskScore { UserEmail = "high@kt.com", Team = "IT", Date = today.AddDays(-35), DailyRiskScore = 20, IncidentCount = 1 },
            new UserDailyRiskScore { UserEmail = "medium@kt.com", Team = "IT", Date = today, DailyRiskScore = 55, IncidentCount = 2 });
        db.IsolationForestScores.Add(new IsolationForestScore { UserEmail = "high@kt.com", CalculatedAt = DateTime.UtcNow, IFScore = 90, IsAnomaly = true, BaselineIncidentCount = 12, JobId = "test" });
        await db.SaveChangesAsync();

        var result = await new RiskShadowService(db).GetHighRiskCandidatesAsync(7, 70, 20, today);

        result.MinimumScore.Should().Be(70);
        result.MatchingCandidateCount.Should().Be(1);
        result.Candidates.Should().ContainSingle(candidate => candidate.UserEmail == "high@kt.com" && candidate.ShadowScore >= 70);
    }
}
