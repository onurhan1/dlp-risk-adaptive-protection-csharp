using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Tests.Integration;

public class UserInsightsServiceTests : IDisposable
{
    private readonly AnalyzerDbContext _db;
    private readonly UserInsightsService _sut;

    public UserInsightsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"UserInsightsTests_{Guid.NewGuid()}")
            .Options;
        _db = new AnalyzerDbContext(options);
        _sut = new UserInsightsService(_db);
    }

    public void Dispose() => _db.Dispose();

    private void SeedDailyScores(string userEmail, int daysBack, double baseScore = 50.0, int incidentCount = 5)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (int i = daysBack; i >= 0; i--)
        {
            _db.UserDailyRiskScores.Add(new UserDailyRiskScore
            {
                UserEmail = userEmail,
                Date = today.AddDays(-i),
                DailyRiskScore = baseScore + (i % 5),
                IncidentCount = incidentCount,
                MaxRiskScore = (int)(baseScore + 10),
                AvgRiskScore = baseScore,
                BlockCount = 2,
                PermitCount = 1,
                QuarantineCount = 1,
                ReleasedCount = 1,
                MaxMaxMatches = 50,
                AvgMaxMatches = 25,
                FullName = "Test User",
                Team = "Security"
            });
        }
        _db.SaveChanges();
    }

    // ─── GetUserDailyScoresAsync ─────────────────────────────────

    [Fact]
    public async Task GetUserDailyScoresAsync_ReturnsScoresInRange()
    {
        SeedDailyScores("user@test.com", daysBack: 10);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _sut.GetUserDailyScoresAsync("user@test.com", today.AddDays(-5), today);

        result.Should().HaveCount(6);
        result.Should().BeInAscendingOrder(s => s.Date);
    }

    [Fact]
    public async Task GetUserDailyScoresAsync_NoData_ReturnsEmpty()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _sut.GetUserDailyScoresAsync("nobody@test.com", today.AddDays(-5), today);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDailyScoresAsync_OnlyReturnsRequestedUser()
    {
        SeedDailyScores("alice@test.com", daysBack: 5);
        SeedDailyScores("bob@test.com", daysBack: 5);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _sut.GetUserDailyScoresAsync("alice@test.com", today.AddDays(-5), today);

        result.Should().AllSatisfy(s => s.UserEmail.Should().Be("alice@test.com"));
    }

    // ─── GetUserComprehensiveInsightsAsync ────────────────────────

    [Fact]
    public async Task GetUserComprehensiveInsightsAsync_ReturnsSummaryAndBreakdown()
    {
        SeedDailyScores("insight@test.com", daysBack: 30, baseScore: 60, incidentCount: 3);

        var result = await _sut.GetUserComprehensiveInsightsAsync("insight@test.com", "monthly");

        result.Should().NotBeNull();
        result.Period.Should().Be("monthly");
        result.Summary.Should().NotBeNull();
        result.Summary.TotalIncidents.Should().BeGreaterThan(0);
        result.Summary.AvgDailyScore.Should().BeGreaterThan(0);
        result.Summary.DaysActive.Should().BeGreaterThan(0);
        result.ActionBreakdown.Should().NotBeNull();
        result.ActionBreakdown.TotalBlocks.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUserComprehensiveInsightsAsync_PeriodAveragesHasAllKeys()
    {
        SeedDailyScores("periods@test.com", daysBack: 90, baseScore: 40);

        var result = await _sut.GetUserComprehensiveInsightsAsync("periods@test.com", "quarterly");

        result.PeriodAverages.Should().ContainKey("weekly");
        result.PeriodAverages.Should().ContainKey("monthly");
        result.PeriodAverages.Should().ContainKey("quarterly");
        result.PeriodAverages["weekly"].AvgScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUserComprehensiveInsightsAsync_NoData_ReturnsZeroSummary()
    {
        var result = await _sut.GetUserComprehensiveInsightsAsync("nodata@test.com", "weekly");

        result.Summary.TotalIncidents.Should().Be(0);
        result.Summary.AvgDailyScore.Should().Be(0);
        result.Summary.DaysActive.Should().Be(0);
        result.DailyScores.Should().BeEmpty();
    }

    // ─── GetUserWeeklyTrendAsync ─────────────────────────────────

    [Fact]
    public async Task GetUserWeeklyTrendAsync_ReturnsTrendForLastWeek()
    {
        SeedDailyScores("trend@test.com", daysBack: 10, baseScore: 45);

        var result = await _sut.GetUserWeeklyTrendAsync("trend@test.com");

        result.Period.Should().Be("weekly");
        result.IncidentCount.Should().BeGreaterThan(0);
        result.AverageDailyScore.Should().BeGreaterThan(0);
        result.Scores.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserMonthlyTrendAsync_ReturnsTrendForLastMonth()
    {
        SeedDailyScores("trend@test.com", daysBack: 35, baseScore: 55);

        var result = await _sut.GetUserMonthlyTrendAsync("trend@test.com");

        result.Period.Should().Be("monthly");
        result.IncidentCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUserQuarterlyTrendAsync_ReturnsTrendForLastQuarter()
    {
        SeedDailyScores("trend@test.com", daysBack: 95, baseScore: 30);

        var result = await _sut.GetUserQuarterlyTrendAsync("trend@test.com");

        result.Period.Should().Be("quarterly");
        result.MaxScore.Should().BeGreaterThan(0);
    }

    // ─── DetectUserAnomaliesAsync ────────────────────────────────

    [Fact]
    public async Task DetectUserAnomaliesAsync_NoData_ReturnsEmpty()
    {
        var result = await _sut.DetectUserAnomaliesAsync("nobody@test.com");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectUserAnomaliesAsync_InsufficientData_ReturnsEmpty()
    {
        SeedDailyScores("few@test.com", daysBack: 2);

        var result = await _sut.DetectUserAnomaliesAsync("few@test.com");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectUserAnomaliesAsync_WithSpike_DetectsAnomaly()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (int i = 30; i >= 3; i--)
        {
            _db.UserDailyRiskScores.Add(new UserDailyRiskScore
            {
                UserEmail = "spike@test.com",
                Date = today.AddDays(-i),
                DailyRiskScore = 10,
                IncidentCount = 1,
                MaxRiskScore = 15
            });
        }
        // Recent spike way above baseline (>3 sigma and >100)
        for (int i = 2; i >= 0; i--)
        {
            _db.UserDailyRiskScores.Add(new UserDailyRiskScore
            {
                UserEmail = "spike@test.com",
                Date = today.AddDays(-i),
                DailyRiskScore = 500,
                IncidentCount = 50,
                MaxRiskScore = 500
            });
        }
        await _db.SaveChangesAsync();

        var result = await _sut.DetectUserAnomaliesAsync("spike@test.com");

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(a => a.Should().Contain("Anomaly"));
    }

    // ─── GetRiskyUsersReportAsync ────────────────────────────────

    [Fact]
    public async Task GetRiskyUsersReportAsync_ReturnsUsersOrderedByScore()
    {
        SeedDailyScores("high@test.com", daysBack: 10, baseScore: 80, incidentCount: 10);
        SeedDailyScores("low@test.com", daysBack: 10, baseScore: 30, incidentCount: 2);

        var result = await _sut.GetRiskyUsersReportAsync("monthly");

        result.Should().NotBeEmpty();
        result.First().UserEmail.Should().Be("high@test.com");
        result.First().CurrentScore.Should().BeGreaterThan(result.Last().CurrentScore);
    }

    [Fact]
    public async Task GetRiskyUsersReportAsync_CalculatesTrend()
    {
        SeedDailyScores("trending@test.com", daysBack: 10, baseScore: 50);

        var result = await _sut.GetRiskyUsersReportAsync("monthly");
        var user = result.FirstOrDefault(u => u.UserEmail == "trending@test.com");

        user.Should().NotBeNull();
        user!.TrendDirection.Should().BeOneOf("increasing", "decreasing", "stable");
        user.Period.Should().Be("monthly");
    }

    // ─── GetDailySummaryFromDailyScoresAsync ─────────────────────

    [Fact]
    public async Task GetDailySummaryFromDailyScoresAsync_AggregatesPerDay()
    {
        SeedDailyScores("user1@test.com", daysBack: 5, baseScore: 40, incidentCount: 3);
        SeedDailyScores("user2@test.com", daysBack: 5, baseScore: 60, incidentCount: 7);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _sut.GetDailySummaryFromDailyScoresAsync(today.AddDays(-5), today);

        result.Should().HaveCount(6);
        result.Should().AllSatisfy(d =>
        {
            d.UniqueUsers.Should().Be(2);
            d.TotalIncidents.Should().Be(10);
        });
    }

    [Fact]
    public async Task GetDailySummaryFromDailyScoresAsync_CalculatesHighAndCriticalCounts()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _db.UserDailyRiskScores.Add(new UserDailyRiskScore
        {
            UserEmail = "critical@test.com",
            Date = today,
            DailyRiskScore = 80,
            IncidentCount = 5,
            MaxRiskScore = 90
        });
        _db.UserDailyRiskScores.Add(new UserDailyRiskScore
        {
            UserEmail = "normal@test.com",
            Date = today,
            DailyRiskScore = 20,
            IncidentCount = 1,
            MaxRiskScore = 25
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetDailySummaryFromDailyScoresAsync(today, today);

        result.Should().HaveCount(1);
        result[0].HighRiskCount.Should().Be(1);
        result[0].CriticalRiskCount.Should().Be(1);
    }

}
