using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using SharedRiskAnalyzer = DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Extended edge-case tests for the risk scoring formula.
/// Complements the existing RiskScoringTests with boundary and
/// combinatorial scenarios that are critical for a security product.
/// </summary>
public class RiskScoringEdgeCaseTests
{
    private readonly SharedRiskAnalyzer _sut = new();

    // ─── Boundary transitions for MaxMatchesTier ─────────────────────────────

    [Theory]
    [InlineData(-1,  7)]
    [InlineData(-100, 7)]
    [InlineData(int.MinValue, 7)]
    public void GetMaxMatchesTier_NegativeValues_ReturnsLowestTier(int matches, int expected)
    {
        _sut.GetMaxMatchesTier(matches).Should().Be(expected);
    }

    // ─── CalculateRiskScoreV2 combinatorial scenarios ────────────────────────

    [Fact]
    public void CalculateRiskScoreV2_ZeroDestination_BlockEmail_FormulaCorrect()
    {
        // Tier 0-15 = 7, Email=1.0, dest=0, BLOCK=1.0 → (7*1.0+0)*1.0 = 7
        _sut.CalculateRiskScoreV2(5, "Email", 0, "BLOCK").Should().Be(7);
    }

    [Fact]
    public void CalculateRiskScoreV2_MaxDestination_Authorized_ReducedByActionMultiplier()
    {
        // Tier 501+ = 85, Email=1.0, dest=15, AUTHORIZED=0.2 → (85+15)*0.2 = 20
        _sut.CalculateRiskScoreV2(999, "Email", 15, "AUTHORIZED").Should().Be(20);
    }

    [Fact]
    public void CalculateRiskScoreV2_EndpointPrinting_MidTier()
    {
        // Tier 31-50 = 25, Printing=0.4, dest=5, BLOCK=1.0 → (25*0.4+5)*1.0 = 15
        _sut.CalculateRiskScoreV2(40, "ENDPOINT_PRINTING", 5, "BLOCK").Should().Be(15);
    }

    [Fact]
    public void CalculateRiskScoreV2_NullChannelNullAction_UsesDefaults()
    {
        // null channel → Default=1.0, null action → 0.2
        // Tier 0-15 = 7, dest=5 → (7*1.0 + 5) * 0.2 = 2.4 → (int)2
        var score = _sut.CalculateRiskScoreV2(10, null, 5, null);
        score.Should().Be(2);
    }

    [Fact]
    public void CalculateRiskScoreV2_Quarantined_SameAsBlock()
    {
        int blockScore = _sut.CalculateRiskScoreV2(100, "Email", 10, "BLOCK");
        int quarantinedScore = _sut.CalculateRiskScoreV2(100, "Email", 10, "QUARANTINED");

        quarantinedScore.Should().Be(blockScore,
            "QUARANTINED and BLOCK should have the same multiplier (1.0)");
    }

    [Fact]
    public void CalculateRiskScoreV2_AllTierBoundaries_ProduceMonotonicallyIncreasingScores()
    {
        var boundaries = new[] { 0, 16, 31, 51, 101, 251, 501 };
        var scores = boundaries.Select(m => _sut.CalculateRiskScoreV2(m, "Email", 5, "BLOCK")).ToList();

        for (int i = 1; i < scores.Count; i++)
        {
            scores[i].Should().BeGreaterThan(scores[i - 1],
                $"tier boundary {boundaries[i]} should produce a higher score than {boundaries[i - 1]}");
        }
    }

    [Fact]
    public void CalculateRiskScoreV2_DestinationScore_IncreasesOutput()
    {
        int lowDest = _sut.CalculateRiskScoreV2(50, "Email", 2, "BLOCK");
        int highDest = _sut.CalculateRiskScoreV2(50, "Email", 15, "BLOCK");

        highDest.Should().BeGreaterThan(lowDest);
    }

    // ─── GetRiskLevel edge boundaries ────────────────────────────────────────

    [Theory]
    [InlineData(-1, "Low")]
    [InlineData(int.MinValue, "Low")]
    [InlineData(int.MaxValue, "High")]
    public void GetRiskLevel_ExtremeValues_DoesNotThrow(int score, string expected)
    {
        _sut.GetRiskLevel(score).Should().Be(expected);
    }

    // ─── GetDisplayScore ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(50, 50.0)]
    [InlineData(100, 100.0)]
    public void GetDisplayScore_ReturnsDirectValue(int input, double expected)
    {
        _sut.GetDisplayScore(input).Should().Be(expected);
    }

    // ─── GetPolicyAction ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("High", "Email", "Encrypt")]
    [InlineData("Low", "USB", "Audit")]
    [InlineData("Medium", "Print", "Audit")]
    [InlineData("Critical", "Cloud", "Block")]
    [InlineData("Unknown", "Email", "Audit")]
    [InlineData("High", "UnknownChannel", "Audit")]
    public void GetPolicyAction_ReturnsExpectedAction(string level, string channel, string expected)
    {
        _sut.GetPolicyAction(level, channel).Should().Be(expected);
    }

    // ─── DetectIOB edge cases ────────────────────────────────────────────────

    [Fact]
    public void DetectIOB_UsbHighSeverity_ReturnsIOB299()
    {
        var incident = new Incident
        {
            UserEmail = "user@company.com",
            Channel = "USB",
            Severity = 7
        };
        _sut.DetectIOB(incident).Should().Contain("IOB-299");
    }

    [Fact]
    public void DetectIOB_UsbLowSeverity_DoesNotReturnIOB299()
    {
        var incident = new Incident
        {
            UserEmail = "user@company.com",
            Channel = "USB",
            Severity = 6
        };
        _sut.DetectIOB(incident).Should().NotContain("IOB-299");
    }

    [Fact]
    public void DetectIOB_CloudHighSensitivity_ReturnsIOB811()
    {
        var incident = new Incident
        {
            UserEmail = "user@company.com",
            Channel = "Cloud",
            DataSensitivity = 8
        };
        _sut.DetectIOB(incident).Should().Contain("IOB-811");
    }

    [Fact]
    public void DetectIOB_AgentTampering_ReturnsIOB280()
    {
        var incident = new Incident
        {
            UserEmail = "user@company.com",
            Policy = "Agent Tampering Detection",
            Severity = 8
        };
        _sut.DetectIOB(incident).Should().Contain("IOB-280");
    }

    [Fact]
    public void DetectIOB_MultipleConditions_ReturnsMultipleIOBs()
    {
        var incident = new Incident
        {
            UserEmail = "user@personal.com",
            Channel = "Email",
            Severity = 3,
            RepeatCount = 15
        };

        var iobs = _sut.DetectIOB(incident);
        iobs.Should().Contain("IOB-511");
        iobs.Should().Contain("IOB-311");
    }

    // ─── Action multiplier case-insensitivity ────────────────────────────────

    [Theory]
    [InlineData("block")]
    [InlineData("Block")]
    [InlineData("BLOCK")]
    [InlineData("bLoCk")]
    public void GetActionMultiplier_CaseInsensitive(string action)
    {
        _sut.GetActionMultiplier(action).Should().Be(1.0);
    }

    // ─── Channel multiplier partial match ────────────────────────────────────

    [Theory]
    [InlineData("ENDPOINT_LAN_OUTBOUND", 0.2)]
    [InlineData("ENDPOINT_PRINTING_SECURE", 0.4)]
    [InlineData("Some_PRINTER_channel", 0.4)]
    public void GetChannelMultiplier_PartialMatch_Works(string channel, double expected)
    {
        _sut.GetChannelMultiplier(channel).Should().BeApproximately(expected, 0.001);
    }
}
