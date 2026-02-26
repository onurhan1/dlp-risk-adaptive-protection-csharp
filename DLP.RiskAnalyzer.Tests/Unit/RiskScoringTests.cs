using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using SharedRiskAnalyzer = DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Unit tests for the core risk scoring formula (CalculateRiskScoreV2).
/// These tests are the safety net for the most security-critical calculation
/// in the system. Any regression in these formulas must fail loudly.
/// </summary>
public class RiskScoringTests
{
    private readonly SharedRiskAnalyzer _sut = new();

    // ─── GetMaxMatchesTier ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,    7)]   // zero matches → lowest tier
    [InlineData(1,    7)]
    [InlineData(15,   7)]   // boundary: still tier 1
    [InlineData(16,   14)]  // boundary: enters tier 2
    [InlineData(30,   14)]
    [InlineData(31,   25)]
    [InlineData(50,   25)]
    [InlineData(51,   40)]
    [InlineData(100,  40)]
    [InlineData(101,  55)]
    [InlineData(250,  55)]
    [InlineData(251,  70)]
    [InlineData(500,  70)]
    [InlineData(501,  85)]  // maximum tier
    [InlineData(9999, 85)]
    public void GetMaxMatchesTier_ReturnsCorrectTier(int matches, int expectedTier)
    {
        _sut.GetMaxMatchesTier(matches).Should().Be(expectedTier);
    }

    // ─── GetChannelMultiplier ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null,                1.0)]   // null → Default multiplier
    [InlineData("",                  1.0)]   // empty → Default multiplier
    [InlineData("ENDPOINT_LAN",      0.2)]
    [InlineData("endpoint_lan",      0.2)]  // case insensitive
    [InlineData("ENDPOINT_PRINTING", 0.4)]
    [InlineData("PRINTER",          0.4)]
    [InlineData("Email",             1.0)]
    [InlineData("Cloud",             1.0)]
    [InlineData("USB",               1.0)]
    [InlineData("Web",               1.0)]
    [InlineData("UNKNOWN_CHANNEL",   1.0)]
    public void GetChannelMultiplier_ReturnsCorrectMultiplier(string? channel, double expected)
    {
        _sut.GetChannelMultiplier(channel).Should().BeApproximately(expected, precision: 0.001);
    }

    // ─── GetActionMultiplier ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null,          0.2)]
    [InlineData("",            0.2)]
    [InlineData("BLOCK",       1.0)]
    [InlineData("BLOCKED",     1.0)]
    [InlineData("QUARANTINE",  1.0)]
    [InlineData("QUARANTINED", 1.0)]
    [InlineData("AUTHORIZED",  0.2)]
    [InlineData("PERMIT",      0.2)]
    [InlineData("RELEASED",    0.0)]
    [InlineData("unknown",     0.2)]  // fallback
    public void GetActionMultiplier_ReturnsCorrectMultiplier(string? action, double expected)
    {
        _sut.GetActionMultiplier(action).Should().BeApproximately(expected, precision: 0.001);
    }

    // ─── CalculateRiskScoreV2 — formula verification ──────────────────────────

    [Fact]
    public void CalculateRiskScoreV2_BlockedWithHighMatches_ReturnsHighScore()
    {
        // Tier 501+ = TierScore 85, Email multiplier 1.0, destinationScore 10 (personal), BLOCK multiplier 1.0
        // Formula: (85 * 1.0 + 10) * 1.0 = 95
        var score = _sut.CalculateRiskScoreV2(
            maxMatches:       999,
            channel:          "Email",
            destinationScore: 10,
            action:           "BLOCK");

        score.Should().Be(95);
    }

    [Fact]
    public void CalculateRiskScoreV2_ReleasedAction_ReturnsZero()
    {
        // Action multiplier for RELEASED is 0.0 → any formula result becomes 0
        var score = _sut.CalculateRiskScoreV2(
            maxMatches:       9999,
            channel:          "Email",
            destinationScore: 10,
            action:           "RELEASED");

        score.Should().Be(0,
            "RELEASED incidents should produce a zero risk score regardless of other factors");
    }

    [Fact]
    public void CalculateRiskScoreV2_EndpointLanChannel_ReducesScoreByFactor()
    {
        // Same matches/destination, Email vs ENDPOINT_LAN
        int emailScore = _sut.CalculateRiskScoreV2(100, "Email",         5, "BLOCK");
        int lanScore   = _sut.CalculateRiskScoreV2(100, "ENDPOINT_LAN",  5, "BLOCK");

        lanScore.Should().BeLessThan(emailScore,
            "ENDPOINT_LAN channel should produce a lower risk score than Email");
    }

    [Fact]
    public void CalculateRiskScoreV2_ScoreIsAlwaysCappedAt100()
    {
        // Use extreme values that would mathematically exceed 100
        var score = _sut.CalculateRiskScoreV2(
            maxMatches:       int.MaxValue,
            channel:          "Email",
            destinationScore: 100,
            action:           "BLOCK");

        score.Should().BeLessOrEqualTo(100,
            "risk score must never exceed the 100 upper bound");
    }

    [Fact]
    public void CalculateRiskScoreV2_ScoreIsNeverNegative()
    {
        var score = _sut.CalculateRiskScoreV2(
            maxMatches:       0,
            channel:          "ENDPOINT_LAN",
            destinationScore: 0,
            action:           "RELEASED");

        score.Should().BeGreaterOrEqualTo(0, "risk score must never be negative");
    }

    // ─── GetRiskLevel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   "Low")]
    [InlineData(24,  "Low")]
    [InlineData(25,  "Medium")]
    [InlineData(49,  "Medium")]
    [InlineData(50,  "High")]
    [InlineData(100, "High")]
    public void GetRiskLevel_ReturnsCorrectBucket(int score, string expected)
    {
        _sut.GetRiskLevel(score).Should().Be(expected);
    }

    // ─── DetectIOB ────────────────────────────────────────────────────────────

    [Fact]
    public void DetectIOB_ExternalEmailChannel_ReturnsIOB511()
    {
        var incident = new Incident
        {
            UserEmail = "user@personal.com",
            Channel   = "Email",
            Severity  = 3
        };

        _sut.DetectIOB(incident).Should().Contain("IOB-511");
    }

    [Fact]
    public void DetectIOB_InternalEmailDomain_DoesNotReturnIOB511()
    {
        var incident = new Incident
        {
            UserEmail = "user@company.com",
            Channel   = "Email",
            Severity  = 3
        };

        _sut.DetectIOB(incident).Should().NotContain("IOB-511");
    }

    [Fact]
    public void DetectIOB_HighRepeatCount_ReturnsIOB311()
    {
        var incident = new Incident
        {
            UserEmail   = "user@company.com",
            RepeatCount = 10
        };

        _sut.DetectIOB(incident).Should().Contain("IOB-311");
    }

    [Fact]
    public void DetectIOB_LowRepeatCount_DoesNotReturnIOB311()
    {
        var incident = new Incident
        {
            UserEmail   = "user@company.com",
            RepeatCount = 9
        };

        _sut.DetectIOB(incident).Should().NotContain("IOB-311");
    }

    [Fact]
    public void DetectIOB_NoTriggerConditions_ReturnsEmptyList()
    {
        var incident = new Incident
        {
            UserEmail       = "user@company.com",
            Channel         = "Web",
            Severity        = 1,
            DataSensitivity = 0,
            RepeatCount     = 0
        };

        _sut.DetectIOB(incident).Should().BeEmpty();
    }
}
