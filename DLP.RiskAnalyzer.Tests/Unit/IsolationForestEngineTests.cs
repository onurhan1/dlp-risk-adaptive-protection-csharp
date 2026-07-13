using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class IsolationForestEngineTests
{
    private readonly IsolationForestEngine _sut = new(new Mock<ILogger<IsolationForestEngine>>().Object);

    [Fact]
    public void Run_ScoresOnlyCurrentWindow_AndTracksAllPriorUserHistory()
    {
        var now = DateTime.UtcNow;
        var current = new List<Incident>
        {
            Incident("alice@company.com", now.AddDays(-1), 4),
            Incident("alice@company.com", now.AddDays(-3), 5)
        };
        var history = new List<Incident>
        {
            Incident("alice@company.com", now.AddDays(-20), 1),
            Incident("alice@company.com", now.AddDays(-40), 2),
            Incident("alice@company.com", now.AddDays(-80), 2),
            Incident("alice@company.com", now.AddDays(-400), 1),
            Incident("history-only@company.com", now.AddDays(-15), 5)
        };

        var result = _sut.Run(current, history);

        result.Should().ContainSingle();
        result[0].UserEmail.Should().Be("alice@company.com");
        result[0].IncidentCount.Should().Be(2, "only the seven-day scoring window is scored");
        result[0].BaselineIncidentCount.Should().Be(4, "all prior history for the scored user forms the baseline");
    }

    [Fact]
    public void Run_NewUserWithoutHistory_HasZeroBaselineCount()
    {
        var result = _sut.Run(
            new List<Incident> { Incident("new-user@company.com", DateTime.UtcNow, 3) },
            new List<Incident>());

        result.Should().ContainSingle();
        result[0].BaselineIncidentCount.Should().Be(0);
        result[0].IsAnomaly.Should().BeFalse("one user is not a meaningful anomaly cohort");
        result[0].IFScore.Should().Be(0);
    }

    [Fact]
    public void Run_EqualPopulationScores_DoNotCreateArtificialAnomalies()
    {
        var now = DateTime.UtcNow;
        var result = _sut.Run(
            new List<Incident>
            {
                Incident("alice@company.com", now, 3),
                Incident("bob@company.com", now, 3)
            },
            new List<Incident>());

        result.Should().OnlyContain(r => !r.IsAnomaly);
    }

    [Fact]
    public void Run_StablePersonalHistory_MakesCurrentDeviationAContributingFeature()
    {
        var now = DateTime.UtcNow;
        var current = new List<Incident>
        {
            Incident("alice@company.com", now.AddDays(-1), 4, dataSensitivity: 100),
            Incident("bob@company.com", now.AddDays(-1), 4, dataSensitivity: 100)
        };
        var history = new List<Incident>();
        for (var i = 1; i <= 3; i++)
        {
            history.Add(Incident("alice@company.com", now.AddDays(-20 * i), 4, dataSensitivity: 10));
            history.Add(Incident("bob@company.com", now.AddDays(-20 * i), 4, dataSensitivity: 100));
        }

        var alice = _sut.Run(current, history).Single(r => r.UserEmail == "alice@company.com");

        alice.TopFeatures.Should().Contain(f =>
            f.Name == "max_self_z_tx_size" && f.Group == "self" && f.ShapValue > 0,
            "a deviation from a stable all-time personal baseline must increase risk");
    }

    private static Incident Incident(
        string userEmail,
        DateTime timestamp,
        int severity,
        int? dataSensitivity = null) => new()
    {
        UserEmail = userEmail,
        Department = "Engineering",
        Timestamp = timestamp,
        Severity = severity,
        DataSensitivity = dataSensitivity ?? severity * 10,
        MaxMatches = severity,
        Channel = "Email",
        Destination = "external@example.com",
        Policy = "Sensitive data",
        Action = "blocked"
    };
}
