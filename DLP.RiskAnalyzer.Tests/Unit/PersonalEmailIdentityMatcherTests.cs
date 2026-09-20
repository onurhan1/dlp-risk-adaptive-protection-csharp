using DLP.RiskAnalyzer.Shared.Services;
using FluentAssertions;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class PersonalEmailIdentityMatcherTests
{
    [Theory]
    [InlineData("abc@kuveytturk.com.tr", "abc@hotmail.com")]
    [InlineData("ayse.yilmaz@kuveytturk.com.tr", "Ayse-Yilmaz@outlook.com")]
    [InlineData("mehmet@kuveytturk.com.tr", "Mehmet+personal@gmail.com")]
    [InlineData("cagri@kuveytturk.com.tr", "cagri@googlemail.com")]
    public void MatchDestination_ExactCorporateLocalPart_ReturnsHighConfidence(string sender, string recipient)
    {
        var result = PersonalEmailIdentityMatcher.FindBestMatch(sender, null, null, recipient);

        result.Should().NotBeNull();
        result!.IsPersonalDomain.Should().BeTrue();
        result.HasIdentityMatch.Should().BeTrue();
        result.MatchType.Should().Be(PersonalEmailIdentityMatchType.ExactLocalPart);
        result.Confidence.Should().Be(PersonalEmailIdentityConfidence.High);
    }

    [Fact]
    public void MatchDestination_DirectoryFullName_ReturnsHighConfidence()
    {
        PersonalEmailIdentityMatcher.IsPersonalDestination("ayse.yilmaz@icloud.com").Should().BeTrue();
        var result = PersonalEmailIdentityMatcher.FindBestMatch(
            "a.yilmaz@kuveytturk.com.tr", "KUVEYTTURK\\ayilmaz", "Ayse Yilmaz", "ayse.yilmaz@icloud.com");

        result.Should().NotBeNull();
        result!.MatchType.Should().Be(PersonalEmailIdentityMatchType.ExactFullName);
        result.Confidence.Should().Be(PersonalEmailIdentityConfidence.High);
    }

    [Fact]
    public void MatchDestination_MultipleRecipients_ReturnsPersonalMatchesOnly()
    {
        var result = PersonalEmailIdentityMatcher.MatchDestination(
            "abc@kuveytturk.com.tr", null, null, "Vendor <legal@partner.example>; abc@hotmail.com");

        result.Should().ContainSingle();
        result[0].Recipient.Should().Be("abc@hotmail.com");
    }

    [Theory]
    [InlineData("abc@kuveytturk.com.tr", "abc@gmail.com.example")]
    [InlineData("abc@kuveytturk.com.tr", "abc@company-outlook.com")]
    [InlineData("abc@kuveytturk.com.tr", "not-an-email")]
    public void MatchDestination_LookalikeOrInvalidDomain_IsNotPersonal(string sender, string destination)
    {
        PersonalEmailIdentityMatcher.MatchDestination(sender, null, null, destination).Should().BeEmpty();
    }

    [Fact]
    public void MatchDestination_ShortOrUnrelatedIdentity_DoesNotUseFuzzyMatch()
    {
        var result = PersonalEmailIdentityMatcher.FindBestMatch("ali@kuveytturk.com.tr", null, null, "alx@hotmail.com");

        result.Should().NotBeNull();
        result!.HasIdentityMatch.Should().BeFalse();
        result.MatchType.Should().Be(PersonalEmailIdentityMatchType.None);
    }

    [Fact]
    public void MatchDestination_NearLongLocalPart_IsLowConfidenceReviewSignal()
    {
        var result = PersonalEmailIdentityMatcher.FindBestMatch(
            "ahmetyilmaz@kuveytturk.com.tr", null, null, "ahmetyilmazz@yahoo.com");

        result.Should().NotBeNull();
        result!.HasIdentityMatch.Should().BeTrue();
        result.MatchType.Should().Be(PersonalEmailIdentityMatchType.ConservativeFuzzy);
        result.Confidence.Should().Be(PersonalEmailIdentityConfidence.Low);
    }
}
