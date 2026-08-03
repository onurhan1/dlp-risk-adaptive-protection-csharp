using DLP.RiskAnalyzer.Analyzer.Services.Surprisal;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class SurprisalModelTests
{
    // NullLogger rather than a Moq mock: SurprisalRiskService is internal, and Castle cannot
    // proxy ILogger<T> for an internal T without granting DynamicProxyGenAssembly2 access.
    private static SurprisalRiskService Service(SurprisalOptions? options = null) => new(
        new Mock<IServiceProvider>().Object,
        NullLogger<SurprisalRiskService>.Instance,
        Options.Create(options ?? new SurprisalOptions { InternalDomains = { "sirket.com" } }));

    // ── Tokenizer ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ahmet@gmail.com", "EMAIL", DestinationClasses.PersonalWebmail)]
    [InlineData("someone@sirket.com", "EMAIL", DestinationClasses.Internal)]
    [InlineData("partner@musteri.com.tr", "EMAIL", DestinationClasses.CorporateExternal)]
    [InlineData("https://dropbox.com/upload", "HTTPS", DestinationClasses.PersonalCloud)]
    [InlineData("USB Drive (16GB)", "ENDPOINT_APPLICATION", DestinationClasses.RemovableMedia)]
    [InlineData("PRINTER-FLOOR3", "ENDPOINT_PRINTING", DestinationClasses.Printer)]
    [InlineData("192.168.1.45", "ENDPOINT_LAN", DestinationClasses.InternalHost)]
    [InlineData("85.34.12.9", "HTTPS", DestinationClasses.ExternalDomain)]
    public void Destinations_AreBucketedIntoEstimableClasses(string destination, string channel, string expected)
    {
        var tokenizer = new EventTokenizer(new SurprisalOptions { InternalDomains = { "sirket.com" } });
        tokenizer.ClassifyDestination(destination, channel).Should().Be(expected);
    }

    [Fact]
    public void Classifiers_AreExtractedFromViolationTriggers()
    {
        // data_type is empty in production; this JSON is where the real data class lives.
        const string json = """
        [{"policy_name":"KT PCI-PII Politikasi","rule_name":"KT PCI/PII-Mail",
          "classifiers":[{"classifier_name":"IBAN Turkish (Wide) (Script)","number_matches":8},
                         {"classifier_name":"Turkey TC Kimlik (Wide) (Script)","number_matches":2}]}]
        """;

        var tokenizer = new EventTokenizer(new SurprisalOptions());
        var result = tokenizer.ExtractClassifiers(json);

        result.Dominant.Should().Be("IBAN Turkish", "the classifier with the most matches wins");
        result.All.Should().BeEquivalentTo(new[] { "IBAN Turkish", "Turkey TC Kimlik" },
            "tuning suffixes collapse so the vocabulary stays estimable");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("""[{"policy_name":"X"}]""")]
    public void Classifiers_DegradeToUnknownRatherThanThrowing(string json)
    {
        new EventTokenizer(new SurprisalOptions()).ExtractClassifiers(json)
            .Dominant.Should().Be(EventToken.Unknown);
    }

    [Fact]
    public void ConfirmContinue_CountsAsDataLeaving()
    {
        // The legacy IsolationForestEngine.ActionMap has no entry for this and scores it neutral,
        // but the user clicked through the warning — the data left.
        EventTokenizer.IsEgress("ENDPOINT_CONFIRM_CONTINUE").Should().BeTrue();
        EventTokenizer.IsEgress("RELEASED").Should().BeTrue();
        EventTokenizer.IsEgress("BLOCKED").Should().BeFalse();
        EventTokenizer.IsEgress("QUARANTINED").Should().BeFalse();
    }

    // ── The two behaviours the model exists for ──────────────────────────────

    [Fact]
    public void RoleExpectation_DampensWhatIsNormalForThePeerGroup()
    {
        // Ten HR users who routinely handle TC Kimlik data, ten engineers who never do.
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        for (var u = 0; u < 10; u++)
            for (var i = 0; i < 30; i++)
            {
                incidents.Add(Make($"ik{u}@sirket.com", "IK", start.AddDays(i), "EMAIL",
                    "KT Personel Verisi", "partner@musteri.com.tr", "Turkey TC Kimlik"));
                incidents.Add(Make($"dev{u}@sirket.com", "BT", start.AddDays(i), "HTTPS",
                    "KT Kaynak Kod", "github.com", "Source Code"));
            }

        // Same physical act, inside the scoring window, by one of each.
        var now = DateTime.UtcNow;
        incidents.Add(Make("ik0@sirket.com", "IK", now.AddDays(-1), "EMAIL",
            "KT Personel Verisi", "partner@musteri.com.tr", "Turkey TC Kimlik"));
        incidents.Add(Make("dev0@sirket.com", "BT", now.AddDays(-1), "EMAIL",
            "KT Personel Verisi", "partner@musteri.com.tr", "Turkey TC Kimlik"));

        var result = Service().Run(incidents, now);

        var hr = result.ScoredEvents.Single(e => e.Token.User == "ik0@sirket.com" && e.Token.Timestamp > now.AddDays(-2));
        var dev = result.ScoredEvents.Single(e => e.Token.User == "dev0@sirket.com" && e.Token.Timestamp > now.AddDays(-2));

        dev.TotalBits.Should().BeGreaterThan(hr.TotalBits,
            "handling HR data is expected for HR and unprecedented for engineering — no rule states this, " +
            "it falls out of the estimated probabilities");
    }

    [Fact]
    public void PersonalBaseline_MakesAFirstTimeChannelSurprising()
    {
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        // Everyone prints and mails; ahmet only ever mails.
        for (var u = 0; u < 12; u++)
            for (var i = 0; i < 20; i++)
            {
                incidents.Add(Make($"u{u}@sirket.com", "Ops", start.AddDays(i), "EMAIL",
                    "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));
                incidents.Add(Make($"u{u}@sirket.com", "Ops", start.AddDays(i).AddHours(2), "ENDPOINT_PRINTING",
                    "KT PCI-PII", "PRINTER-1", "IBAN Turkish"));
            }

        for (var i = 0; i < 40; i++)
            incidents.Add(Make("ahmet@sirket.com", "Ops", start.AddDays(i % 20), "EMAIL",
                "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        var now = DateTime.UtcNow;
        incidents.Add(Make("ahmet@sirket.com", "Ops", now.AddDays(-1), "ENDPOINT_PRINTING",
            "KT PCI-PII", "PRINTER-1", "IBAN Turkish"));
        incidents.Add(Make("u0@sirket.com", "Ops", now.AddDays(-1), "ENDPOINT_PRINTING",
            "KT PCI-PII", "PRINTER-1", "IBAN Turkish"));

        var result = Service().Run(incidents, now);

        var ahmet = result.ScoredEvents.Single(e => e.Token.User == "ahmet@sirket.com" && e.Token.Timestamp > now.AddDays(-2));
        var routine = result.ScoredEvents.Single(e => e.Token.User == "u0@sirket.com" && e.Token.Timestamp > now.AddDays(-2));

        var ahmetChannel = ahmet.Fields.Single(f => f.Field == EventToken.Fields.Channel);
        var routineChannel = routine.Fields.Single(f => f.Field == EventToken.Fields.Channel);

        ahmetChannel.Bits.Should().BeGreaterThan(routineChannel.Bits,
            "physically the same event, but unprecedented for this specific person");

        // λu is computed from the recency-WEIGHTED count, not the raw one, so 40 events spread over
        // the older half of the baseline window contribute an effective count well below 40.
        ahmetChannel.PersonalWeight.Should().BeGreaterThan(0.3, "he has real history behind him");
        ahmetChannel.PersonalObservations.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("u0@sirket.com", "u*@sirket.com")]
    [InlineData("ahmet.yilmaz@sirket.com", "ah******@sirket.com")]
    [InlineData("a@sirket.com", "*@sirket.com")]
    [InlineData("abc@sirket.com", "a**@sirket.com")]
    public void Addresses_AreMaskedEvenWhenShort(string input, string expected)
    {
        SurprisalDiagnostics.Mask(input).Should().Be(expected);
    }

    [Fact]
    public void NoHistory_MeansThePersonalTermCarriesNoWeight()
    {
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        for (var u = 0; u < 10; u++)
            for (var i = 0; i < 20; i++)
                incidents.Add(Make($"u{u}@sirket.com", "Ops", start.AddDays(i), "EMAIL",
                    "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        var now = DateTime.UtcNow;
        incidents.Add(Make("yeni@sirket.com", "Ops", now.AddDays(-1), "EMAIL",
            "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        var result = Service().Run(incidents, now);
        var newcomer = result.ScoredEvents.Single(e => e.Token.User == "yeni@sirket.com");

        newcomer.Fields.Should().OnlyContain(f => f.PersonalWeight == 0,
            "missing history must mean the term is absent — never a fabricated deviation");
        newcomer.Fields.Should().OnlyContain(f => f.ClusterWeight + f.OrgWeight > 0.99);
    }

    // ── Excitation: the learned replacement for scenario rules ──────────────

    [Fact]
    public void Excitation_LearnsChannelPairsNobodyEnumerated()
    {
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        // Everyone mails a lot. A handful also print right before mailing.
        for (var u = 0; u < 10; u++)
            for (var i = 0; i < 20; i++)
            {
                var day = start.AddDays(i);
                incidents.Add(Make($"u{u}@sirket.com", "Ops", day.AddHours(9), "EMAIL",
                    "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));
                if (u < 3)
                {
                    incidents.Add(Make($"u{u}@sirket.com", "Ops", day.AddHours(14), "ENDPOINT_PRINTING",
                        "KT PCI-PII", "PRINTER-1", "IBAN Turkish"));
                    incidents.Add(Make($"u{u}@sirket.com", "Ops", day.AddHours(14).AddMinutes(6), "EMAIL",
                        "KT PCI-PII", "ahmet@gmail.com", "IBAN Turkish"));
                }
            }

        var result = Service().Run(incidents, DateTime.UtcNow);
        var pairs = result.Excitation.AllPairs();

        pairs.Should().Contain(p => p.From == "ENDPOINT_PRINTING" && p.To == "EMAIL" && p.Lift > 1.0,
            "the print-then-mail relationship must be measured from the data, not declared in code");
    }

    [Fact]
    public void Excitation_IgnoresSameChannelRepetition()
    {
        // Sending two emails in a row is the commonest pattern there is, so its lift is high — but
        // it is burstiness, not a second capability being reached for. Left in, it would multiply
        // roughly half of all events and bury the cross-channel signal.
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        for (var u = 0; u < 8; u++)
            for (var i = 0; i < 20; i++)
                for (var k = 0; k < 3; k++)
                    incidents.Add(Make($"u{u}@sirket.com", "Ops", start.AddDays(i).AddMinutes(k * 4), "EMAIL",
                        "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        // A second channel elsewhere in the org, so EMAIL's marginal share is below 1 and the
        // self-pair lift is genuinely above 1 rather than trivially equal to it.
        for (var u = 8; u < 12; u++)
            for (var i = 0; i < 20; i++)
                incidents.Add(Make($"u{u}@sirket.com", "BT", start.AddDays(i), "HTTPS",
                    "KT Kaynak Kod", "github.com", "Source Code"));

        var now = DateTime.UtcNow;
        incidents.Add(Make("u0@sirket.com", "Ops", now.AddDays(-1), "EMAIL",
            "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));
        incidents.Add(Make("u0@sirket.com", "Ops", now.AddDays(-1).AddMinutes(4), "EMAIL",
            "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        var result = Service().Run(incidents, now);

        result.Excitation.AllPairs().Should().Contain(p => p.From == "EMAIL" && p.To == "EMAIL" && p.Lift > 1.0,
            "the self-pair is still measured and reported");
        result.ScoredEvents.Should().OnlyContain(e => e.ExcitationMultiplier == 1.0,
            "but it must never multiply a score");
    }

    [Fact]
    public void GapDistribution_SuggestsItsOwnWindow()
    {
        var tokens = new List<EventToken>();
        var start = DateTime.UtcNow.AddDays(-30);

        // Bursts of three a few minutes apart, then a long wait — a clearly bimodal shape.
        for (var d = 0; d < 60; d++)
        {
            var burst = start.AddDays(d / 2.0);
            for (var i = 0; i < 3; i++)
                tokens.Add(new EventToken { User = "u@sirket.com", Timestamp = burst.AddMinutes(i * 3) });
        }

        var gaps = ExcitationModel.ComputeGaps(tokens);

        gaps.ShareUnder5Min.Should().BeGreaterThan(0.5);
        gaps.SuggestedWindowMinutes.Should().BeInRange(5, 720);
        gaps.Median.Should().BeLessThan(gaps.P90);
    }

    // ── Consequence and accumulation ────────────────────────────────────────

    [Fact]
    public void BlockedEvents_ScoreLowerThanIdenticalEventsThatGotThrough()
    {
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        for (var u = 0; u < 10; u++)
            for (var i = 0; i < 20; i++)
                incidents.Add(Make($"u{u}@sirket.com", "Ops", start.AddDays(i), "EMAIL",
                    "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        var now = DateTime.UtcNow;
        incidents.Add(Make("u0@sirket.com", "Ops", now.AddDays(-1), "EMAIL",
            "KT PCI-PII", "ahmet@gmail.com", "IBAN Turkish", action: "RELEASED"));
        incidents.Add(Make("u1@sirket.com", "Ops", now.AddDays(-1), "EMAIL",
            "KT PCI-PII", "ahmet@gmail.com", "IBAN Turkish", action: "BLOCKED"));

        var result = Service().Run(incidents, now);
        var released = result.ScoredEvents.Single(e => e.Token.User == "u0@sirket.com" && e.Token.Egressed);
        var blocked = result.ScoredEvents.Single(e => e.Token.User == "u1@sirket.com" && !e.Token.Egressed);

        released.Score.Should().BeGreaterThan(blocked.Score,
            "surprisal says 'unexpected'; the consequence term says 'and it actually mattered'");
    }

    [Fact]
    public void AccumulatedRisk_DecaysWithAgeInsteadOfFallingOffAWindowEdge()
    {
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        for (var u = 0; u < 10; u++)
            for (var i = 0; i < 20; i++)
                incidents.Add(Make($"u{u}@sirket.com", "Ops", start.AddDays(i), "EMAIL",
                    "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        var now = DateTime.UtcNow;
        incidents.Add(Make("recent@sirket.com", "Ops", now.AddDays(-1), "HTTPS",
            "KT Kaynak Kod", "ahmet@gmail.com", "Source Code", action: "RELEASED"));
        incidents.Add(Make("older@sirket.com", "Ops", now.AddDays(-25), "HTTPS",
            "KT Kaynak Kod", "ahmet@gmail.com", "Source Code", action: "RELEASED"));

        var result = Service().Run(incidents, now);
        var recent = result.UserRisks.Single(r => r.User == "recent@sirket.com");
        var older = result.UserRisks.Single(r => r.User == "older@sirket.com");

        recent.Score.Should().BeGreaterThan(older.Score);
        older.Score.Should().BeGreaterThan(0, "an old event fades but never vanishes at a window boundary");
    }

    // ── The deliverable ─────────────────────────────────────────────────────

    [Fact]
    public void DiagnosticReport_CoversEverySectionAndSurvivesThinData()
    {
        var incidents = new List<Incident>();
        var start = DateTime.UtcNow.AddDays(-60);

        for (var u = 0; u < 12; u++)
            for (var i = 0; i < 15; i++)
                incidents.Add(Make($"u{u}@sirket.com", u % 3 == 0 ? "IK" : "Ops", start.AddDays(i), "EMAIL",
                    "KT PCI-PII", "partner@musteri.com.tr", "IBAN Turkish"));

        incidents.Add(Make("u0@sirket.com", "IK", DateTime.UtcNow.AddDays(-1), "HTTPS",
            "KT Kaynak Kod", "ahmet@gmail.com", "Source Code", action: "RELEASED"));

        var result = Service().Run(incidents, DateTime.UtcNow);
        var report = SurprisalDiagnostics.Render(result, new SurprisalOptions());

        foreach (var section in new[]
                 {
                     "## 1. Veri şekli", "## 2. Alan sözlükleri", "## 3. Kişisel taban",
                     "## 4. Davranışsal kümeler", "## 5. Olay aralıkları", "## 6. Sürpriz dağılımı",
                     "## 7. En yüksek skorlu", "## 8. En yüksek birikmiş", "## 9. Sağlık uyarıları",
                     "## 10. Kullanılan konfigürasyon"
                 })
            report.Should().Contain(section);

        report.Should().NotContain("u0@sirket.com", "addresses are masked — the report gets pasted around");
    }

    [Fact]
    public void Run_OnEmptyInput_DoesNotThrow()
    {
        var result = Service().Run(Array.Empty<Incident>(), DateTime.UtcNow);

        result.ScoredEvents.Should().BeEmpty();
        result.UserRisks.Should().BeEmpty();
        SurprisalDiagnostics.Render(result, new SurprisalOptions()).Should().NotBeNullOrEmpty();
    }

    // ── Fixture ─────────────────────────────────────────────────────────────

    private static int _nextId = 1;

    private static Incident Make(
        string user, string department, DateTime timestamp, string channel,
        string policy, string destination, string classifier, string action = "BLOCKED") => new()
    {
        Id = _nextId++,
        UserEmail = user,
        Department = department,
        Team = department,
        Timestamp = timestamp,
        Channel = channel,
        Policy = policy,
        RuleName = policy + "-Rule",
        Destination = destination,
        Action = action,
        Severity = 3,
        DataSensitivity = 2,
        MaxMatches = 9,
        ViolationTriggers =
            $$"""[{"policy_name":"{{policy}}","rule_name":"{{policy}}-Rule","classifiers":[{"classifier_name":"{{classifier}}","number_matches":9}]}]"""
    };
}
