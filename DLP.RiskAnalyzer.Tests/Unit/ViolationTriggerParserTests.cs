using DLP.RiskAnalyzer.Analyzer.Helpers;
using FluentAssertions;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Unit tests for ViolationTriggerParser.
/// This class was extracted from 3 copies of duplicate parsing logic scattered
/// across RiskController, DatabaseService, and RiskAnalyzerService.
/// These tests ensure the single source of truth is correct.
/// </summary>
public class ViolationTriggerParserTests
{
    // ─── Test data ────────────────────────────────────────────────────────────

    private const string ValidSingleTrigger = """
        [{
            "RuleName": "Credit Card Detection",
            "Classifiers": [
                { "NumberMatches": 15 },
                { "NumberMatches": 42 }
            ]
        }]
        """;

    private const string ValidMultipleTriggers = """
        [
            {
                "RuleName": "Rule A",
                "Classifiers": [{ "NumberMatches": 10 }]
            },
            {
                "RuleName": "Rule B",
                "Classifiers": [{ "NumberMatches": 99 }]
            }
        ]
        """;

    private const string CamelCaseVariant = """
        [{
            "ruleName": "camelCase Rule",
            "Classifiers": [{ "numberMatches": 7 }]
        }]
        """;

    private const string MissingRuleName = """
        [{ "Classifiers": [{ "NumberMatches": 5 }] }]
        """;

    // ─── ExtractFirstRuleName ─────────────────────────────────────────────────

    [Fact]
    public void ExtractFirstRuleName_ValidJson_ReturnsFirstRuleName()
    {
        ViolationTriggerParser.ExtractFirstRuleName(ValidSingleTrigger)
            .Should().Be("Credit Card Detection");
    }

    [Fact]
    public void ExtractFirstRuleName_MultipleRules_ReturnsFirst()
    {
        ViolationTriggerParser.ExtractFirstRuleName(ValidMultipleTriggers)
            .Should().Be("Rule A");
    }

    [Fact]
    public void ExtractFirstRuleName_CamelCasePropertyName_ReturnsValue()
    {
        ViolationTriggerParser.ExtractFirstRuleName(CamelCaseVariant)
            .Should().Be("camelCase Rule");
    }

    [Fact]
    public void ExtractFirstRuleName_MissingRuleName_ReturnsNull()
    {
        ViolationTriggerParser.ExtractFirstRuleName(MissingRuleName)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractFirstRuleName_NullOrEmpty_ReturnsNull(string? input)
    {
        ViolationTriggerParser.ExtractFirstRuleName(input).Should().BeNull();
    }

    [Fact]
    public void ExtractFirstRuleName_MalformedJson_ReturnsNull()
    {
        ViolationTriggerParser.ExtractFirstRuleName("THIS IS NOT JSON {{{")
            .Should().BeNull("malformed JSON must be silently ignored");
    }

    // ─── ExtractAllRuleNames ──────────────────────────────────────────────────

    [Fact]
    public void ExtractAllRuleNames_MultipleTriggers_ReturnsAll()
    {
        var names = ViolationTriggerParser.ExtractAllRuleNames(ValidMultipleTriggers);
        names.Should().BeEquivalentTo(new[] { "Rule A", "Rule B" });
    }

    [Fact]
    public void ExtractAllRuleNames_DuplicateRuleNames_ReturnsDistinct()
    {
        const string duplicates = """
            [
                { "RuleName": "Same Rule", "Classifiers": [] },
                { "RuleName": "Same Rule", "Classifiers": [] }
            ]
            """;

        ViolationTriggerParser.ExtractAllRuleNames(duplicates)
            .Should().HaveCount(1, "duplicates must be deduplicated");
    }

    // ─── ExtractMaxMatches ────────────────────────────────────────────────────

    [Fact]
    public void ExtractMaxMatches_SingleTrigger_ReturnsHighestClassifierMatch()
    {
        // Classifiers have 15 and 42 — expect 42
        ViolationTriggerParser.ExtractMaxMatches(ValidSingleTrigger)
            .Should().Be(42);
    }

    [Fact]
    public void ExtractMaxMatches_MultipleTriggers_ReturnsGlobalMax()
    {
        // Rule A has 10, Rule B has 99 — expect 99
        ViolationTriggerParser.ExtractMaxMatches(ValidMultipleTriggers)
            .Should().Be(99);
    }

    [Fact]
    public void ExtractMaxMatches_NoClassifiers_ReturnsZero()
    {
        const string noClassifiers = """[{ "RuleName": "Test" }]""";
        ViolationTriggerParser.ExtractMaxMatches(noClassifiers).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractMaxMatches_NullOrEmpty_ReturnsZero(string? input)
    {
        ViolationTriggerParser.ExtractMaxMatches(input).Should().Be(0);
    }

    // ─── ExtractSummary (single-pass) ─────────────────────────────────────────

    [Fact]
    public void ExtractSummary_SingleTrigger_ReturnsBothValuesInOnePass()
    {
        var (ruleName, maxMatches) = ViolationTriggerParser.ExtractSummary(ValidSingleTrigger);

        ruleName.Should().Be("Credit Card Detection");
        maxMatches.Should().Be(42);
    }

    [Fact]
    public void ExtractSummary_MultipleTriggers_ReturnsFirstRuleAndGlobalMax()
    {
        var (ruleName, maxMatches) = ViolationTriggerParser.ExtractSummary(ValidMultipleTriggers);

        ruleName.Should().Be("Rule A",    "first trigger's rule name must be returned");
        maxMatches.Should().Be(99,        "global max across all triggers must be returned");
    }

    [Fact]
    public void ExtractSummary_MalformedJson_ReturnsSafeDefaults()
    {
        var (ruleName, maxMatches) = ViolationTriggerParser.ExtractSummary("not valid json");

        ruleName.Should().BeNull();
        maxMatches.Should().Be(0);
    }

    [Fact]
    public void ExtractSummary_NullInput_ReturnsSafeDefaults()
    {
        var (ruleName, maxMatches) = ViolationTriggerParser.ExtractSummary(null);

        ruleName.Should().BeNull();
        maxMatches.Should().Be(0);
    }
}
