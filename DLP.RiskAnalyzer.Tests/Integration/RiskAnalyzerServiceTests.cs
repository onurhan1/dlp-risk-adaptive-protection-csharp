using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Implementations;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Tests.Integration;

public class RiskAnalyzerServiceTests : IDisposable
{
    private readonly AnalyzerDbContext _db;
    private readonly IncidentRepository _repo;
    private readonly RiskAnalyzerService _sut;

    public RiskAnalyzerServiceTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"ServiceTests_{Guid.NewGuid()}")
            .Options;
        _db = new AnalyzerDbContext(options);
        _repo = new IncidentRepository(_db);
        var dailyScoreRepo = new DLP.RiskAnalyzer.Analyzer.Repositories.Implementations.UserDailyRiskScoreRepository(_db);
        var userInsights = new UserInsightsService(dailyScoreRepo, _repo);
        _sut = new RiskAnalyzerService(_repo, _db, userInsights);
    }

    private Incident MakeIncident(
        string user = "user@test.com",
        DateTime? ts = null,
        int? riskScore = null,
        string? channel = null,
        string? department = null,
        string? action = null,
        int maxMatches = 10)
    {
        return new Incident
        {
            UserEmail = user,
            Timestamp = ts ?? DateTime.UtcNow,
            RiskScore = riskScore,
            Channel = channel ?? "Email",
            Department = department ?? "IT",
            Action = action ?? "BLOCK",
            MaxMatches = maxMatches,
            Severity = 5,
            Policy = "DLP-001"
        };
    }

    private async Task SeedIncidents(params Incident[] incidents)
    {
        _db.Incidents.AddRange(incidents);
        await _db.SaveChangesAsync();
    }

    // ─── GetUserRiskTrendsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetUserRiskTrendsAsync_ReturnsGroupedByUserAndDay()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@t.com", ts: today.AddHours(1), riskScore: 40),
            MakeIncident(user: "a@t.com", ts: today.AddHours(2), riskScore: 70),
            MakeIncident(user: "b@t.com", ts: today.AddHours(3), riskScore: 20));

        var trends = await _sut.GetUserRiskTrendsAsync(days: 7);

        trends.Should().HaveCount(2);
        var trendA = trends.First(t => t.UserEmail == "a@t.com");
        trendA.RiskScore.Should().Be(70);
        trendA.TotalIncidents.Should().Be(2);
    }

    [Fact]
    public async Task GetUserRiskTrendsAsync_WithUserFilter_OnlyReturnsSpecifiedUser()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@t.com", ts: today.AddHours(1), riskScore: 40),
            MakeIncident(user: "b@t.com", ts: today.AddHours(2), riskScore: 70));

        var trends = await _sut.GetUserRiskTrendsAsync(days: 7, user: "b@t.com");

        trends.Should().HaveCount(1);
        trends[0].UserEmail.Should().Be("b@t.com");
    }

    [Fact]
    public async Task GetUserRiskTrendsAsync_NoData_ReturnsEmpty()
    {
        var trends = await _sut.GetUserRiskTrendsAsync(days: 7);
        trends.Should().BeEmpty();
    }

    // ─── GetDailySummariesAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetDailySummariesAsync_AggregatesPerDay()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@t.com", ts: today.AddHours(1), riskScore: 80, department: "IT"),
            MakeIncident(user: "b@t.com", ts: today.AddHours(2), riskScore: 20, department: "HR"),
            MakeIncident(user: "c@t.com", ts: today.AddHours(3), riskScore: 60, department: "IT"));

        var summaries = await _sut.GetDailySummariesAsync(days: 3);

        summaries.Should().HaveCount(1);
        summaries[0].TotalIncidents.Should().Be(3);
        summaries[0].UniqueUsers.Should().Be(3);
    }

    // ─── GetRiskHeatmapAsync ─────────────────────────────────────────────────

    [Theory]
    [InlineData("department")]
    [InlineData("user")]
    [InlineData("channel")]
    public async Task GetRiskHeatmapAsync_AllDimensions_ReturnData(string dimension)
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@t.com", department: "IT", channel: "Email", ts: today.AddHours(1)),
            MakeIncident(user: "b@t.com", department: "HR", channel: "USB", ts: today.AddHours(2)));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var result = await _sut.GetRiskHeatmapAsync(dimension, start, end);

        result.Labels.Should().HaveCountGreaterThan(0);
        result.Values.Should().HaveCountGreaterThan(0);
        result.Labels.Should().HaveCount(result.Values.Count);
        result.Dimension.Should().Be(dimension);
    }

    [Fact]
    public async Task GetRiskHeatmapAsync_DefaultDateRange_Uses30Days()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(ts: today.AddHours(1)));

        var result = await _sut.GetRiskHeatmapAsync("user", null, null);

        result.DateRange.Should().ContainKey("start");
        result.DateRange.Should().ContainKey("end");
    }

    // ─── GetChannelActivityAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetChannelActivityAsync_CalculatesPercentages()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(channel: "Email", ts: today.AddHours(1), riskScore: 80),
            MakeIncident(channel: "Email", ts: today.AddHours(2), riskScore: 30),
            MakeIncident(channel: "USB",   ts: today.AddHours(3), riskScore: 10));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var result = await _sut.GetChannelActivityAsync(start, end);

        result.Total.Should().Be(3);
        result.Channels.Should().HaveCount(2);
    }

    // ─── GetTopUsersByDayAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetTopUsersByDayAsync_FiltersLowRiskUsers()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "risky@t.com", ts: today.AddHours(1), riskScore: 60),
            MakeIncident(user: "safe@t.com", ts: today.AddHours(2), riskScore: 10));

        var result = await _sut.GetTopUsersByDayAsync(days: 3, limit: 10);

        result.Should().HaveCount(1);
        result[0].UserEmail.Should().Be("risky@t.com");
    }

    [Fact]
    public async Task GetTopUsersByDayAsync_RespectsDateRange()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "old@t.com", ts: today.AddDays(-60), riskScore: 90),
            MakeIncident(user: "recent@t.com", ts: today.AddHours(1), riskScore: 50));

        var result = await _sut.GetTopUsersByDayAsync(
            days: 0,
            startDate: today.AddDays(-1),
            endDate: today.AddDays(1));

        result.Should().ContainSingle();
        result[0].UserEmail.Should().Be("recent@t.com");
    }

    // ─── GetIOBDetectionsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetIOBDetectionsAsync_DetectsAndAggregates()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@personal.com", channel: "Email", ts: today.AddHours(1)),
            MakeIncident(user: "b@personal.com", channel: "Email", ts: today.AddHours(2)));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var result = await _sut.GetIOBDetectionsAsync(start, end);

        result.Should().Contain(r => r.Code == "IOB-511");
        var iob511 = result.First(r => r.Code == "IOB-511");
        iob511.UsersAffected.Should().Be(2);
    }

    // ─── CalculateRiskScoresAsync ────────────────────────────────────────────

    [Fact]
    public async Task CalculateRiskScoresAsync_SetsRiskScoreOnUnscored()
    {
        await SeedIncidents(
            MakeIncident(riskScore: null, channel: "Email", action: "BLOCK", maxMatches: 100));

        var count = await _sut.CalculateRiskScoresAsync();

        count.Should().Be(1);
        var incident = await _db.Incidents.FirstAsync();
        incident.RiskScore.Should().NotBeNull();
        incident.RiskScore.Should().BeGreaterThan(0);
        incident.RiskLevel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CalculateRiskScoresAsync_SkipsScoredIncidents()
    {
        await SeedIncidents(MakeIncident(riskScore: 50));

        var count = await _sut.CalculateRiskScoresAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task CalculateRiskScoresAsync_ReleasedAction_SetsZero()
    {
        await SeedIncidents(
            MakeIncident(riskScore: null, action: "RELEASED", maxMatches: 500));

        await _sut.CalculateRiskScoresAsync();

        var incident = await _db.Incidents.FirstAsync();
        incident.RiskScore.Should().Be(0);
    }

    // ─── CalculateDailyScoresAsync ───────────────────────────────────────────

    [Fact]
    public async Task CalculateDailyScoresAsync_CreatesRecords()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayDt = today.ToDateTime(TimeOnly.MinValue);

        await SeedIncidents(
            MakeIncident(user: "a@t.com", ts: todayDt.AddHours(1), riskScore: 50, action: "BLOCK"),
            MakeIncident(user: "a@t.com", ts: todayDt.AddHours(2), riskScore: 30, action: "AUTHORIZED"));

        var count = await _sut.CalculateDailyScoresAsync(today);

        count.Should().Be(1);
        var score = await _db.UserDailyRiskScores.FirstAsync();
        score.UserEmail.Should().Be("a@t.com");
        score.IncidentCount.Should().Be(2);
        score.DailyRiskScore.Should().BeGreaterThan(0);
        score.BlockCount.Should().Be(1);
    }

    [Fact]
    public async Task CalculateDailyScoresAsync_UpdatesExistingRecords()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayDt = today.ToDateTime(TimeOnly.MinValue);

        await SeedIncidents(
            MakeIncident(user: "a@t.com", ts: todayDt.AddHours(1), riskScore: 50));

        await _sut.CalculateDailyScoresAsync(today);
        var initialScore = (await _db.UserDailyRiskScores.FirstAsync()).DailyRiskScore;

        _db.Incidents.Add(MakeIncident(user: "a@t.com", ts: todayDt.AddHours(3), riskScore: 90));
        await _db.SaveChangesAsync();

        await _sut.CalculateDailyScoresAsync(today);

        var scores = await _db.UserDailyRiskScores.Where(s => s.UserEmail == "a@t.com").ToListAsync();
        scores.Should().HaveCount(1, "should update existing, not create duplicate");
        scores[0].DailyRiskScore.Should().BeGreaterThan(initialScore);
    }

    // ─── GetDepartmentSummariesAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetDepartmentSummariesAsync_GroupsByDepartment()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(department: "IT", user: "a@t.com", ts: today.AddHours(1), riskScore: 60),
            MakeIncident(department: "IT", user: "b@t.com", ts: today.AddHours(2), riskScore: 20),
            MakeIncident(department: "HR", user: "c@t.com", ts: today.AddHours(3), riskScore: 40));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var summaries = await _sut.GetDepartmentSummariesAsync(start, end);

        summaries.Should().HaveCount(2);
        var it = summaries.First(s => s.Department == "IT");
        it.TotalIncidents.Should().Be(2);
        it.UniqueUsers.Should().Be(2);
    }

    // ─── GetRiskyUsersReportAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetRiskyUsersReportAsync_AggregatesFromDailyScores()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _db.UserDailyRiskScores.AddRange(
            new UserDailyRiskScore { UserEmail = "risky@t.com", Date = today, DailyRiskScore = 75, IncidentCount = 10, MaxRiskScore = 80, AvgRiskScore = 70, CreatedAt = DateTime.UtcNow },
            new UserDailyRiskScore { UserEmail = "risky@t.com", Date = today.AddDays(-1), DailyRiskScore = 60, IncidentCount = 5, MaxRiskScore = 65, AvgRiskScore = 55, CreatedAt = DateTime.UtcNow },
            new UserDailyRiskScore { UserEmail = "risky@t.com", Date = today.AddDays(-2), DailyRiskScore = 50, IncidentCount = 3, MaxRiskScore = 55, AvgRiskScore = 45, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _sut.GetRiskyUsersReportAsync("weekly");

        result.Should().HaveCount(1);
        result[0].UserEmail.Should().Be("risky@t.com");
        result[0].CurrentScore.Should().Be(75);
    }

    public void Dispose() => _db.Dispose();
}
