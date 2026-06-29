using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Unit tests for BehaviorMetricsCalculator — the core anomaly scoring engine.
/// Validates Z-score based risk tier calculations, metric aggregation, and threat profiling.
/// </summary>
public class BehaviorMetricsCalculatorTests
{
    private readonly BehaviorMetricsCalculator _sut;

    public BehaviorMetricsCalculatorTests()
    {
        var logger = new Mock<ILogger<BehaviorMetricsCalculator>>();
        _sut = new BehaviorMetricsCalculator(logger.Object);
    }

    // ─── CalculateEnhancedRiskScore — Tier Verification ──────────────────────

    [Fact]
    public void CalculateEnhancedRiskScore_EmptyZScores_ReturnsBaseline()
    {
        var zScores = new Dictionary<string, double>();
        _sut.CalculateEnhancedRiskScore(zScores).Should().Be(10);
    }

    [Fact]
    public void CalculateEnhancedRiskScore_AllLowZScores_ReturnsLowTier()
    {
        var zScores = new Dictionary<string, double>
        {
            { "incident_count", 0.3 },
            { "severity", 0.2 },
            { "max_matches", 0.1 }
        };
        var score = _sut.CalculateEnhancedRiskScore(zScores);
        score.Should().BeLessThan(BehaviorThresholds.MediumRiskThreshold,
            "all Z-scores below 0.5 should remain in LOW tier");
    }

    [Fact]
    public void CalculateEnhancedRiskScore_SingleHighZScore_ReturnsMediumOrHighTier()
    {
        var zScores = new Dictionary<string, double>
        {
            { "incident_count", 3.5 },
            { "severity", 0.5 },
            { "max_matches", 0.3 }
        };
        var score = _sut.CalculateEnhancedRiskScore(zScores);
        score.Should().BeGreaterOrEqualTo(BehaviorThresholds.HighRiskThreshold,
            "a Z-score >= 3.0 should push into HIGH tier");
    }

    [Fact]
    public void CalculateEnhancedRiskScore_TwoExtremeZScores_ReturnsCritical100()
    {
        var zScores = new Dictionary<string, double>
        {
            { "incident_count", 4.5 },
            { "action_block", 4.2 },
            { "severity", 1.0 },
            { "max_matches", 0.5 }
        };
        var score = _sut.CalculateEnhancedRiskScore(zScores);
        score.Should().Be(100,
            "two extreme Z-scores (>= 4.0) should produce maximum risk = 100");
    }

    [Fact]
    public void CalculateEnhancedRiskScore_OneExtremeOneHigh_Returns95()
    {
        var zScores = new Dictionary<string, double>
        {
            { "incident_count", 4.5 },
            { "action_block", 3.5 },
            { "severity", 0.5 },
            { "max_matches", 0.2 }
        };
        var score = _sut.CalculateEnhancedRiskScore(zScores);
        score.Should().Be(95);
    }

    [Fact]
    public void CalculateEnhancedRiskScore_MultipleModerateZScores_ReturnsMediumTier()
    {
        var zScores = new Dictionary<string, double>
        {
            { "incident_count", 1.6 },
            { "severity", 1.7 },
            { "max_matches", 1.5 }
        };
        var score = _sut.CalculateEnhancedRiskScore(zScores);
        score.Should().BeInRange(BehaviorThresholds.MediumRiskThreshold,
            BehaviorThresholds.HighRiskThreshold - 1,
            "multiple moderate Z-scores should place in MEDIUM tier");
    }

    // ─── DetermineAnomalyLevel ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, "low")]
    [InlineData(39, "low")]
    [InlineData(40, "medium")]
    [InlineData(64, "medium")]
    [InlineData(65, "high")]
    [InlineData(84, "high")]
    [InlineData(85, "critical")]
    [InlineData(100, "critical")]
    public void DetermineAnomalyLevel_ReturnCorrectBoundaries(int riskScore, string expectedLevel)
    {
        _sut.DetermineAnomalyLevel(riskScore).Should().Be(expectedLevel);
    }

    // ─── CalculateEnhancedMetrics ────────────────────────────────────────────

    [Fact]
    public void CalculateEnhancedMetrics_EmptyList_ReturnsDefaultMetrics()
    {
        var metrics = _sut.CalculateEnhancedMetrics(new List<Incident>());
        metrics.TotalIncidents.Should().Be(0);
        metrics.MeanIncidentsPerDay.Should().Be(0);
    }

    [Fact]
    public void CalculateEnhancedMetrics_WithIncidents_CalculatesCorrectTotals()
    {
        var incidents = new List<Incident>
        {
            new() { Timestamp = DateTime.UtcNow, Severity = 3, Channel = "Email", Action = "BLOCK", MaxMatches = 10 },
            new() { Timestamp = DateTime.UtcNow, Severity = 7, Channel = "Email", Action = "BLOCK", MaxMatches = 20 },
            new() { Timestamp = DateTime.UtcNow, Severity = 5, Channel = "USB", Action = "AUTHORIZED", MaxMatches = 5 },
        };

        var metrics = _sut.CalculateEnhancedMetrics(incidents);

        metrics.TotalIncidents.Should().Be(3);
        metrics.AvgSeverity.Should().Be(5.0);
        metrics.TotalMatches.Should().Be(35);
        metrics.ChannelCounts.Should().ContainKey("Email").WhoseValue.Should().Be(2);
        metrics.ChannelCounts.Should().ContainKey("USB").WhoseValue.Should().Be(1);
        metrics.ActionCounts.Should().ContainKey("BLOCK").WhoseValue.Should().Be(2);
        metrics.ActionCounts.Should().ContainKey("AUTHORIZED").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void CalculateEnhancedMetrics_SingleIncident_StdDevIsZero()
    {
        var incidents = new List<Incident>
        {
            new() { Timestamp = DateTime.UtcNow, Severity = 5, Channel = "Email", Action = "BLOCK", MaxMatches = 10 }
        };

        var metrics = _sut.CalculateEnhancedMetrics(incidents);

        metrics.StdDevIncidentsPerDay.Should().Be(0);
        metrics.StdDevSeverity.Should().Be(0);
        metrics.StdDevMatches.Should().Be(0);
    }

    // ─── GetEffectiveMaxMatches ──────────────────────────────────────────────

    [Fact]
    public void GetEffectiveMaxMatches_DbValueGreaterThanZero_ReturnsDbValue()
    {
        var incident = new Incident { MaxMatches = 42, ViolationTriggers = null };
        _sut.GetEffectiveMaxMatches(incident).Should().Be(42);
    }

    [Fact]
    public void GetEffectiveMaxMatches_DbValueZero_EmptyTriggers_ReturnsZero()
    {
        var incident = new Incident { MaxMatches = 0, ViolationTriggers = "" };
        _sut.GetEffectiveMaxMatches(incident).Should().Be(0);
    }

    [Fact]
    public void GetEffectiveMaxMatches_DbValueZero_ValidJson_CalculatesFromJson()
    {
        var triggers = "[{\"MatchCount\":15},{\"MatchCount\":8}]";
        var incident = new Incident { MaxMatches = 0, ViolationTriggers = triggers };
        _sut.GetEffectiveMaxMatches(incident).Should().Be(15);
    }

    [Fact]
    public void GetEffectiveMaxMatches_DbValueZero_InvalidJson_ReturnsZero()
    {
        var incident = new Incident { MaxMatches = 0, ViolationTriggers = "not-json" };
        _sut.GetEffectiveMaxMatches(incident).Should().Be(0,
            "invalid JSON should be handled gracefully and return 0");
    }

    // ─── CalculateThreatProfileMultiplier ─────────────────────────────────────

    [Fact]
    public void CalculateThreatProfileMultiplier_EmptyList_ReturnsOne()
    {
        _sut.CalculateThreatProfileMultiplier(new List<Incident>()).Should().Be(1.0);
    }

    [Fact]
    public void CalculateThreatProfileMultiplier_AllBlockActions_ReturnsHighMultiplier()
    {
        var incidents = new List<Incident>
        {
            new() { Action = "BLOCK", Severity = 8, MaxMatches = 100 },
            new() { Action = "BLOCK", Severity = 9, MaxMatches = 200 }
        };

        var multiplier = _sut.CalculateThreatProfileMultiplier(incidents);
        multiplier.Should().BeGreaterOrEqualTo(0.8, "all BLOCK actions with high severity should produce a high multiplier");
    }

    [Fact]
    public void CalculateThreatProfileMultiplier_AllAuthorizedActions_ReturnsLowerMultiplier()
    {
        var incidents = new List<Incident>
        {
            new() { Action = "AUTHORIZED", Severity = 1, MaxMatches = 1 },
            new() { Action = "AUTHORIZED", Severity = 2, MaxMatches = 2 }
        };

        var multiplier = _sut.CalculateThreatProfileMultiplier(incidents);
        multiplier.Should().BeLessThan(0.5, "all AUTHORIZED actions with low severity should produce a low multiplier");
    }

    [Fact]
    public void CalculateThreatProfileMultiplier_MultiplierIsClamped_Between02And10()
    {
        // Test with extreme high-threat incidents
        var highThreat = new List<Incident>
        {
            new() { Action = "BLOCK", Severity = 10, MaxMatches = 10000 }
        };
        _sut.CalculateThreatProfileMultiplier(highThreat).Should().BeLessOrEqualTo(1.0);

        // Test with extreme low-threat incidents
        var lowThreat = new List<Incident>
        {
            new() { Action = "RELEASED", Severity = 1, MaxMatches = 0 }
        };
        _sut.CalculateThreatProfileMultiplier(lowThreat).Should().BeGreaterOrEqualTo(0.2);
    }
}
