using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Implementations;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Tests.Integration;

public class IncidentRepositoryTests : IDisposable
{
    private readonly AnalyzerDbContext _db;
    private readonly IncidentRepository _sut;

    public IncidentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"RepoTests_{Guid.NewGuid()}")
            .Options;
        _db = new AnalyzerDbContext(options);
        _sut = new IncidentRepository(_db);
    }

    private Incident MakeIncident(
        string user = "user@test.com",
        DateTime? ts = null,
        int? riskScore = null,
        string? channel = null,
        string? department = null,
        string? policy = null,
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
            Policy = policy ?? "DLP-001",
            Action = action ?? "BLOCK",
            MaxMatches = maxMatches,
            Severity = 5
        };
    }

    private async Task SeedIncidents(params Incident[] incidents)
    {
        _db.Incidents.AddRange(incidents);
        await _db.SaveChangesAsync();
    }

    // ─── GetIncidentsAsync (with maxRows) ────────────────────────────────────

    [Fact]
    public async Task GetIncidentsAsync_RespectsMaxRows()
    {
        var incidents = Enumerable.Range(0, 50)
            .Select(i => MakeIncident(ts: DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();
        await SeedIncidents(incidents);

        var result = await _sut.GetIncidentsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            maxRows: 10);

        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetIncidentsAsync_DefaultLimit_ReturnsAll()
    {
        var incidents = Enumerable.Range(0, 5)
            .Select(i => MakeIncident(ts: DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();
        await SeedIncidents(incidents);

        var result = await _sut.GetIncidentsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        result.Should().HaveCount(5);
    }

    // ─── Paginated ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIncidentsAsync_Paginated_ReturnsCorrectPage()
    {
        var incidents = Enumerable.Range(0, 25)
            .Select(i => MakeIncident(ts: DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();
        await SeedIncidents(incidents);

        var page1 = await _sut.GetIncidentsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            page: 1, pageSize: 10);

        var page2 = await _sut.GetIncidentsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            page: 2, pageSize: 10);

        page1.Should().HaveCount(10);
        page2.Should().HaveCount(10);
        page1.Select(i => i.Timestamp).Should().NotIntersectWith(page2.Select(i => i.Timestamp));
    }

    // ─── GetIncidentsWithoutRiskScoreAsync ───────────────────────────────────

    [Fact]
    public async Task GetIncidentsWithoutRiskScore_RespectsBatchSize()
    {
        var incidents = Enumerable.Range(0, 20)
            .Select(i => MakeIncident(riskScore: null, ts: DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();
        await SeedIncidents(incidents);

        var result = await _sut.GetIncidentsWithoutRiskScoreAsync(batchSize: 5);
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetIncidentsWithoutRiskScore_ExcludesScoredIncidents()
    {
        await SeedIncidents(
            MakeIncident(riskScore: null),
            MakeIncident(riskScore: 50),
            MakeIncident(riskScore: null));

        var result = await _sut.GetIncidentsWithoutRiskScoreAsync();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.RiskScore == null);
    }

    // ─── UpdateIncidentsAsync (batch save) ───────────────────────────────────

    [Fact]
    public async Task UpdateIncidents_BatchSave_UpdatesAll()
    {
        var incidents = new[]
        {
            MakeIncident(riskScore: null),
            MakeIncident(riskScore: null)
        };
        await SeedIncidents(incidents);

        foreach (var inc in incidents)
            inc.RiskScore = 42;

        var count = await _sut.UpdateIncidentsAsync(incidents);
        count.Should().BeGreaterThan(0);

        var all = await _db.Incidents.ToListAsync();
        all.Should().OnlyContain(i => i.RiskScore == 42);
    }

    // ─── GetPolicyRepeatCountsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetPolicyRepeatCounts_GroupsByPolicy()
    {
        await SeedIncidents(
            MakeIncident(user: "a@test.com", policy: "P1", ts: DateTime.UtcNow.AddHours(-10)),
            MakeIncident(user: "a@test.com", policy: "P1", ts: DateTime.UtcNow.AddHours(-9)),
            MakeIncident(user: "a@test.com", policy: "P2", ts: DateTime.UtcNow.AddHours(-8)));

        var counts = await _sut.GetPolicyRepeatCountsAsync("a@test.com", DateTime.UtcNow);

        counts.Should().ContainKey("P1").WhoseValue.Should().Be(2);
        counts.Should().ContainKey("P2").WhoseValue.Should().Be(1);
    }

    // ─── DB-side aggregation: UserRiskTrends ─────────────────────────────────

    [Fact]
    public async Task GetUserRiskTrendsAggregated_GroupsByUserAndDay()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@test.com", ts: today.AddHours(1), riskScore: 30),
            MakeIncident(user: "a@test.com", ts: today.AddHours(2), riskScore: 60),
            MakeIncident(user: "b@test.com", ts: today.AddHours(3), riskScore: 20));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var trends = await _sut.GetUserRiskTrendsAggregatedAsync(start, end);

        trends.Should().HaveCount(2);
        var trendA = trends.First(t => t.UserEmail == "a@test.com");
        trendA.TotalIncidents.Should().Be(2);
        trendA.MaxRiskScore.Should().Be(60);
    }

    [Fact]
    public async Task GetUserRiskTrendsAggregated_FilterByUser()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "a@test.com", ts: today.AddHours(1), riskScore: 30),
            MakeIncident(user: "b@test.com", ts: today.AddHours(2), riskScore: 60));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var trends = await _sut.GetUserRiskTrendsAggregatedAsync(start, end, "b@test.com");

        trends.Should().HaveCount(1);
        trends[0].UserEmail.Should().Be("b@test.com");
    }

    // ─── DB-side aggregation: DailySummaries ─────────────────────────────────

    [Fact]
    public async Task GetDailySummariesAggregated_AggregatesPerDay()
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        await SeedIncidents(
            MakeIncident(user: "a@test.com", ts: today.AddHours(1), riskScore: 80),
            MakeIncident(user: "b@test.com", ts: today.AddHours(2), riskScore: 20),
            MakeIncident(user: "c@test.com", ts: yesterday.AddHours(3), riskScore: 10));

        var start = DateOnly.FromDateTime(yesterday.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var summaries = await _sut.GetDailySummariesAggregatedAsync(start, end);

        summaries.Should().HaveCount(2);
        var todaySummary = summaries.First(s => s.Date == DateOnly.FromDateTime(today));
        todaySummary.TotalIncidents.Should().Be(2);
        todaySummary.UniqueUsers.Should().Be(2);
    }

    // ─── DB-side aggregation: ChannelBreakdown ───────────────────────────────

    [Fact]
    public async Task GetChannelBreakdownAggregated_GroupsByChannel()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(channel: "Email", ts: today.AddHours(1), riskScore: 80),
            MakeIncident(channel: "Email", ts: today.AddHours(2), riskScore: 30),
            MakeIncident(channel: "USB",   ts: today.AddHours(3), riskScore: 10));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var breakdown = await _sut.GetChannelBreakdownAggregatedAsync(start, end);

        breakdown.Should().HaveCount(2);
        var email = breakdown.First(b => b.Channel == "Email");
        email.TotalIncidents.Should().Be(2);
    }

    // ─── DB-side aggregation: Heatmap ────────────────────────────────────────

    [Fact]
    public async Task GetHeatmapAggregated_DepartmentDimension()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(department: "IT", ts: today.AddHours(1)),
            MakeIncident(department: "IT", ts: today.AddHours(2)),
            MakeIncident(department: "HR", ts: today.AddHours(3)));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var items = await _sut.GetHeatmapAggregatedAsync(start, end, "department");

        items.Should().HaveCount(2);
        items[0].Label.Should().Be("IT");
        items[0].Count.Should().Be(2);
    }

    [Fact]
    public async Task GetHeatmapAggregated_RespectsLimit()
    {
        var today = DateTime.UtcNow.Date;
        var incidents = Enumerable.Range(0, 15)
            .Select(i => MakeIncident(user: $"u{i}@test.com", ts: today.AddMinutes(-i)))
            .ToArray();
        await SeedIncidents(incidents);

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var items = await _sut.GetHeatmapAggregatedAsync(start, end, "user", limit: 5);

        items.Should().HaveCount(5);
    }

    // ─── DB-side aggregation: TopUsers ────────────────────────────────────────

    [Fact]
    public async Task GetTopUsersAggregated_FiltersLowRisk()
    {
        var today = DateTime.UtcNow.Date;
        await SeedIncidents(
            MakeIncident(user: "risky@test.com", ts: today.AddHours(1), riskScore: 60),
            MakeIncident(user: "safe@test.com", ts: today.AddHours(2), riskScore: 10));

        var start = DateOnly.FromDateTime(today.AddDays(-1));
        var end = DateOnly.FromDateTime(today.AddDays(1));

        var top = await _sut.GetTopUsersAggregatedAsync(start, end, minRiskScore: 35);

        top.Should().HaveCount(1);
        top[0].UserEmail.Should().Be("risky@test.com");
    }

    // ─── GetWeeklyIncidentsCountAsync ────────────────────────────────────────

    [Fact]
    public async Task GetWeeklyIncidentsCount_OnlyCountsLast7Days()
    {
        var now = DateTime.UtcNow;
        await SeedIncidents(
            MakeIncident(user: "a@test.com", ts: now.AddDays(-3)),
            MakeIncident(user: "a@test.com", ts: now.AddDays(-6)),
            MakeIncident(user: "a@test.com", ts: now.AddDays(-10)));

        var count = await _sut.GetWeeklyIncidentsCountAsync("a@test.com", now);
        count.Should().Be(2);
    }

    public void Dispose() => _db.Dispose();
}
