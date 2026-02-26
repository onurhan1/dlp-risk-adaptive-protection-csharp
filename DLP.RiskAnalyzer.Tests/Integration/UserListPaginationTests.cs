using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Integration;

/// <summary>
/// Integration-style tests for RiskAnalyzerService.GetUserListAsync.
/// Uses EF Core InMemory provider (no live PostgreSQL needed).
/// 
/// These tests verify the P-01 fix: aggregation, search, and pagination
/// are now executed at the DB layer instead of loading all rows into memory.
/// </summary>
public class UserListPaginationTests : IDisposable
{
    private readonly AnalyzerDbContext _context;
    private readonly RiskAnalyzerService _sut;

    public UserListPaginationTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AnalyzerDbContext(options);

        var repoMock = new Mock<IIncidentRepository>();
        _sut = new RiskAnalyzerService(repoMock.Object, _context);

        SeedUserDailyScores();
    }

    private void SeedUserDailyScores()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var scores = new List<UserDailyRiskScore>
        {
            // User A — 3 days of data, avg score 80
            new() { UserEmail = "alice@company.com", Date = today.AddDays(-1), DailyRiskScore = 90, IncidentCount = 5, FullName = "Alice Smith",  Team = "Finance" },
            new() { UserEmail = "alice@company.com", Date = today.AddDays(-2), DailyRiskScore = 80, IncidentCount = 3, FullName = "Alice Smith",  Team = "Finance" },
            new() { UserEmail = "alice@company.com", Date = today.AddDays(-3), DailyRiskScore = 70, IncidentCount = 2, FullName = "Alice Smith",  Team = "Finance" },

            // User B — 1 day, score 50
            new() { UserEmail = "bob@company.com",   Date = today.AddDays(-1), DailyRiskScore = 50, IncidentCount = 2, FullName = "Bob Jones",    Team = "IT" },

            // User C — old data (outside 30-day window) — should NOT appear
            new() { UserEmail = "charlie@company.com", Date = today.AddDays(-35), DailyRiskScore = 99, IncidentCount = 10 },
        };

        _context.UserDailyRiskScores.AddRange(scores);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetUserList_ReturnsOnlyUsersWithinLast30Days()
    {
        var result = await _sut.GetUserListAsync(page: 1, pageSize: 100);

        result.Users.Should().HaveCount(2, "charlie@company.com has data older than 30 days and must be excluded");
    }

    [Fact]
    public async Task GetUserList_OrdersByAverageScoreDescending()
    {
        var result = await _sut.GetUserListAsync(page: 1, pageSize: 100);

        var scores = result.Users.Select(u => u.RiskScore).ToList();

        scores.Should().BeInDescendingOrder("users must be ordered by average risk score descending");
    }

    [Fact]
    public async Task GetUserList_SearchByEmail_FiltersCorrectly()
    {
        var result = await _sut.GetUserListAsync(page: 1, pageSize: 100, search: "alice");

        result.Users.Should().HaveCount(1);
        result.Users[0].UserEmail.Should().Be("alice@company.com");
    }

    [Fact]
    public async Task GetUserList_SearchByFullName_FiltersCorrectly()
    {
        var result = await _sut.GetUserListAsync(page: 1, pageSize: 100, search: "Bob Jones");

        result.Users.Should().HaveCount(1);
        result.Users[0].UserEmail.Should().Be("bob@company.com");
    }

    [Fact]
    public async Task GetUserList_PageSize1_ReturnsOnlyFirstPage()
    {
        var result = await _sut.GetUserListAsync(page: 1, pageSize: 1);

        result.Users.Should().HaveCount(1);
        result.Total.Should().Be(2, "total must reflect the full un-paged count");
    }

    [Fact]
    public async Task GetUserList_Page2_ReturnsSecondUser()
    {
        var result = await _sut.GetUserListAsync(page: 2, pageSize: 1);

        result.Users.Should().HaveCount(1);
        // Page 1 = alice (avg 80), Page 2 = bob (avg 50)
        result.Users[0].UserEmail.Should().Be("bob@company.com");
    }

    [Fact]
    public async Task GetUserList_AverageScoreCalculation_IsCorrect()
    {
        var result = await _sut.GetUserListAsync(page: 1, pageSize: 100);

        var alice = result.Users.First(u => u.UserEmail == "alice@company.com");

        // (90 + 80 + 70) / 3 = 80.0
        alice.RiskScore.Should().BeApproximately(80.0, 0.1);
    }

    [Fact]
    public async Task GetUserList_EmptyDatabase_ReturnsTotalZero()
    {
        // Fresh empty context
        var emptyOptions = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var emptyCtx = new AnalyzerDbContext(emptyOptions);
        var repoMock = new Mock<IIncidentRepository>();
        var emptyService = new RiskAnalyzerService(repoMock.Object, emptyCtx);

        var result = await emptyService.GetUserListAsync();

        result.Total.Should().Be(0);
    }

    public void Dispose() => _context.Dispose();
}
