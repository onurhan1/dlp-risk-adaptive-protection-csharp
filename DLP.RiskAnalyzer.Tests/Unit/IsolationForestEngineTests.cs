using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class IsolationForestEngineTests
{
    private readonly IsolationForestEngine _sut = Engine();

    private static IsolationForestEngine Engine(IsolationForestOptions? options = null) =>
        new(new Mock<ILogger<IsolationForestEngine>>().Object, options ?? IsolationForestOptions.Default);

    // ── Windowing / baseline ──────────────────────────────────────────────────

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

    /// <summary>
    /// Replaces the former two-user version of this test. A two-user cohort is the single most
    /// degenerate case available: standardization forces every non-constant column to z = ±0.70711,
    /// so the old |z|-based weighting was provably a no-op there and the assertion would have passed
    /// even if the forest had been deleted.
    /// </summary>
    [Fact]
    public void Run_StablePersonalHistory_MakesCurrentDeviationAContributingFeature()
    {
        var now = DateTime.UtcNow;
        var (current, history) = Cohort(userCount: 60);

        // alice keeps a stable personal baseline, then spikes hard inside the window.
        for (var i = 1; i <= 12; i++)
            history.Add(Incident("alice@company.com", now.AddDays(-10 - 5 * i), 3, dataSensitivity: 10));
        current.Add(Incident("alice@company.com", now.AddDays(-1), 3, dataSensitivity: 400));
        current.Add(Incident("alice@company.com", now.AddDays(-2), 3, dataSensitivity: 380));

        var alice = Engine().Run(current, history).Single(r => r.UserEmail == "alice@company.com");

        alice.TopFeatures.Should().Contain(
            f => f.Group == "self" && f.ShapValue > 0,
            "a deviation from a stable personal baseline must raise the score");
    }

    // ── T1: the explanation must be an explanation OF THE MODEL ──────────────

    /// <summary>
    /// Fails by construction on the pre-CCMA implementation: the old ComputeContributions never
    /// received the fitted trees, so its output was invariant to the forest.
    /// </summary>
    [Fact]
    public void Contributions_ChangeWhenTheForestChanges()
    {
        var (current, history) = Cohort(userCount: 80);

        var a = Engine(new IsolationForestOptions { NEstimators = 100, Seed = 42 }).Run(current, history);
        var b = Engine(new IsolationForestOptions { NEstimators = 15, Seed = 7 }).Run(current, history);

        static Dictionary<string, string> Rankings(List<IsolationForestScoreDto> r) => r.ToDictionary(
            s => s.UserEmail,
            s => string.Join(",", s.TopFeatures.Select(f => f.Name)));

        var rankingsA = Rankings(a);
        var rankingsB = Rankings(b);
        var changed = rankingsA.Count(kv => rankingsB[kv.Key] != kv.Value);

        changed.Should().BeGreaterThan(0,
            "reason ranking must depend on the fitted forest, not only on the feature matrix");
    }

    // ── T2: reasons can only name features the forest actually split on ──────

    [Fact]
    public void Reasons_OnlyNameFeaturesTheForestActuallySplitOn()
    {
        // Indices 0-4 of the feature matrix, in BuildScaledMatrix order.
        var allowedNames = new[]
        {
            "incident_count", "unique_policies", "unique_channels", "unique_destinations", "mean_severity"
        };

        var (current, history) = Cohort(userCount: 80);
        var result = Engine(new IsolationForestOptions { AllowedFeatures = new[] { 0, 1, 2, 3, 4 } })
            .Run(current, history);

        result.SelectMany(r => r.TopFeatures)
            .Should().OnlyContain(f => allowedNames.Contains(f.Name),
                "a feature the forest never split on cannot change any path length, so its delta is exactly 0");
    }

    // ── T3: the reported impact is the real counterfactual ───────────────────

    [Fact]
    public void ReportedImpact_MatchesIndependentRescoring()
    {
        // A hand-built forest: two features, two trees, fully known geometry.
        var trees = new[]
        {
            Node(feature: 0, split: 0.5,
                 left: Leaf(8),
                 right: Node(feature: 1, split: 1.5, left: Leaf(4), right: Leaf(1))),
            Node(feature: 1, split: 0.0,
                 left: Leaf(6),
                 right: Node(feature: 0, split: 2.0, left: Leaf(3), right: Leaf(1)))
        };

        const double c = 5.0;
        var attributor = new ForestAttributor(trees, c, featureCount: 2);
        var x = new[] { 2.5, 2.5 };

        var delta = attributor.AttributeSingles(x, out var baseScore);

        // Independently: re-walk the trees for the original point and for each ablated point.
        double SumPath(double[] p) => trees.Sum(t => IsolationTree.PathLength(p, t, 0));
        double Score(double sum) => Math.Pow(2.0, -(sum / trees.Length) / c);

        Score(SumPath(x)).Should().BeApproximately(baseScore, 1e-12);

        for (int f = 0; f < 2; f++)
        {
            var ablated = (double[])x.Clone();
            ablated[f] = 0.0;
            delta[f].Should().BeApproximately(baseScore - Score(SumPath(ablated)), 1e-12,
                "the reported contribution must be the actual score change through the same trees");
        }
    }

    [Fact]
    public void Attribution_IgnoresFeaturesNotOnThePath()
    {
        // Feature 1 is never tested, so ablating it cannot change anything.
        var trees = new[] { Node(feature: 0, split: 0.5, left: Leaf(5), right: Leaf(2)) };
        var delta = new ForestAttributor(trees, c: 4.0, featureCount: 3).AttributeSingles(new[] { 9.0, 9.0, 9.0 }, out _);

        delta[1].Should().Be(0.0);
        delta[2].Should().Be(0.0);
        delta[0].Should().NotBe(0.0);
    }

    // ── T4: an unusually LOW value is still a risk driver ────────────────────

    /// <summary>
    /// An isolation forest isolates both tails. The old code derived direction from the sign of the
    /// standardized value, so a user isolated for being far below the cohort was explained as
    /// "risk ↓" — green — on a card stamped "needs review".
    /// </summary>
    [Fact]
    public void UnusuallyLowValue_IsExplainedAsRiskIncreasing()
    {
        var now = DateTime.UtcNow;
        var current = new List<Incident>();
        var history = new List<Incident>();

        // 40 busy users, plus one who went almost silent this week.
        for (var u = 0; u < 40; u++)
        {
            var email = $"busy{u}@company.com";
            for (var i = 0; i < 12; i++)
            {
                current.Add(Incident(email, now.AddDays(-1 - i % 6), 4, dataSensitivity: 200 + u));
                history.Add(Incident(email, now.AddDays(-30 - i), 4, dataSensitivity: 200 + u));
            }
        }

        const string quiet = "quiet@company.com";
        current.Add(Incident(quiet, now.AddDays(-2), 1, dataSensitivity: 1));
        for (var i = 0; i < 12; i++)
            history.Add(Incident(quiet, now.AddDays(-30 - i), 1, dataSensitivity: 1));

        var result = Engine().Run(current, history).Single(r => r.UserEmail == quiet);

        result.TopFeatures.Should().Contain(
            f => f.ActualValue < 0 && f.ShapValue > 0,
            "being far below the cohort is what isolated this user, so it must read as raising the score");
    }

    // ── T5 / T6: absent and non-personal data must never become a reason ─────

    [Fact]
    public void UserWithoutSelfBaseline_HasNoSelfReasons()
    {
        var now = DateTime.UtcNow;
        var (current, history) = Cohort(userCount: 20);

        // alice has only two prior incidents — below MinUserIncidents, so every self feature is 0.
        current.Add(Incident("alice@company.com", now.AddDays(-1), 5, dataSensitivity: 300));
        current.Add(Incident("alice@company.com", now.AddDays(-2), 5, dataSensitivity: 280));
        history.Add(Incident("alice@company.com", now.AddDays(-40), 2, dataSensitivity: 10));
        history.Add(Incident("alice@company.com", now.AddDays(-50), 2, dataSensitivity: 10));

        var alice = Engine().Run(current, history).Single(r => r.UserEmail == "alice@company.com");

        alice.BaselineIncidentCount.Should().Be(2);
        alice.TopFeatures.Should().NotContain(f => f.Group == "self",
            "a personal-baseline metric cannot explain a user who has no usable personal baseline");
        alice.GroupBreakdown.Should().NotContainKey("self");
    }

    [Fact]
    public void Reasons_ContainNoDeptContextFeatures()
    {
        var (current, history) = Cohort(userCount: 60);
        var result = Engine().Run(current, history);

        result.SelectMany(r => r.TopFeatures)
            .Should().NotContain(f => f.Group == "dept_ctx",
                "department-level features are identical for every member, so they describe the team, not the person");
        result.Should().OnlyContain(r => !r.GroupBreakdown.ContainsKey("dept_ctx"));
    }

    // ── T7: the group breakdown must not inherit the top-N truncation ────────

    [Fact]
    public void GroupBreakdown_IsIndependentOfTopFeatureTruncation()
    {
        var (current, history) = Cohort(userCount: 60);
        var result = Engine().Run(current, history);

        foreach (var user in result.Where(r => r.TopFeatures.Count > 0))
        {
            var fromTop = user.TopFeatures.GroupBy(f => f.Group)
                .ToDictionary(g => g.Key, g => g.Sum(f => Math.Abs(f.ShapValue)));

            foreach (var (group, total) in user.GroupBreakdown)
                total.Should().BeGreaterThanOrEqualTo(fromTop.GetValueOrDefault(group) - 1e-9,
                    "the breakdown sums the full contribution vector, not just the displayed slice");
        }

        result.Should().Contain(
            r => r.GroupBreakdown.Values.Sum() > r.TopFeatures.Sum(f => Math.Abs(f.ShapValue)) + 1e-9,
            "at least one user must have contributions beyond the eight displayed");
    }

    // ── T9 / T10: degenerate cohort and determinism ──────────────────────────

    [Fact]
    public void SingleUserCohort_YieldsNoReasons()
    {
        var result = _sut.Run(
            new List<Incident> { Incident("solo@company.com", DateTime.UtcNow, 3) },
            new List<Incident>());

        result.Should().ContainSingle();
        result[0].TopFeatures.Should().BeEmpty("there is nothing to be isolated against");
        result[0].GroupBreakdown.Should().BeEmpty();
    }

    [Fact]
    public void Run_IsDeterministic_AndParallelInvariant()
    {
        var (current, history) = Cohort(userCount: 60);

        static string Fingerprint(List<IsolationForestScoreDto> r) => string.Join("|", r
            .OrderBy(s => s.UserEmail, StringComparer.Ordinal)
            .Select(s => $"{s.UserEmail}:{s.AnomalyRaw:R}:" +
                         string.Join(",", s.TopFeatures.Select(f => $"{f.Name}={f.ShapValue:R}"))));

        var single = Engine(new IsolationForestOptions { MaxDegreeOfParallelism = 1 }).Run(current, history);
        var eight = Engine(new IsolationForestOptions { MaxDegreeOfParallelism = 8 }).Run(current, history);
        var again = Engine(new IsolationForestOptions { MaxDegreeOfParallelism = 8 }).Run(current, history);

        Fingerprint(eight).Should().Be(Fingerprint(single));
        Fingerprint(again).Should().Be(Fingerprint(eight));
    }

    [Fact]
    public void AnomalyRaw_IsTheUnnormalizedScore()
    {
        var (current, history) = Cohort(userCount: 40);
        var result = Engine().Run(current, history);

        result.Should().OnlyContain(r => r.AnomalyRaw > 0 && r.AnomalyRaw < 1,
            "the raw isolation-forest score lives in (0,1) and is what makes runs comparable");
        result.Max(r => r.IFScore).Should().Be(100, "min-max always pins the top user at 100");
        result.Should().Contain(r => Math.Abs(r.AnomalyRaw - r.IFScore) > 1e-6,
            "the raw score must no longer be a copy of the normalized one");
    }

    // ── Reason layer ─────────────────────────────────────────────────────────

    [Fact]
    public void Reasons_CarryRawUnitsNotStandardizedValues()
    {
        var (current, history) = Cohort(userCount: 60);
        var result = Engine().Run(current, history);

        var withReasons = result.Where(r => r.Reasons.Count > 0).ToList();
        withReasons.Should().NotBeEmpty();

        foreach (var reason in withReasons.SelectMany(r => r.Reasons))
        {
            reason.ObservedValue.Should().NotBeNull();
            reason.ValueKind.Should().NotBeNullOrEmpty();
            reason.FamilyKey.Should().NotBeNullOrEmpty();
            reason.Members.Should().NotBeEmpty();
            reason.Members.Should().Contain(reason.AnchorFeature);
        }

        // A ratio is a proportion; if these were standardized values they would be routinely
        // negative and would routinely exceed 1.
        var ratios = withReasons.SelectMany(r => r.Reasons).Where(r => r.ValueKind == "ratio").ToList();
        ratios.Should().OnlyContain(r => r.ObservedValue >= 0 && r.ObservedValue <= 1);
    }

    [Fact]
    public void SelfReasons_ReportUnknownDeviation_WhileAggregationDiscardsTheSign()
    {
        var (current, history) = Cohort(userCount: 60);
        var result = Engine().Run(current, history);

        result.SelectMany(r => r.Reasons.Concat(r.SecondarySignals))
            .Where(r => r.ValueKind == "zabs")
            .Should().OnlyContain(r => r.Deviation == "unknown",
                "max/mean_self_z_* aggregate Math.Abs(z), so any above/below claim would be a guess");
    }

    [Fact]
    public void SharesAndResidual_SumToOneHundred()
    {
        var (current, history) = Cohort(userCount: 60);
        var result = Engine().Run(current, history);

        foreach (var user in result.Where(r => r.Reasons.Count + r.SecondarySignals.Count > 0))
        {
            var total = user.ExplainedSharePct + user.UnexplainedSharePct;
            total.Should().BeApproximately(100.0, 0.5,
                "sequential ablation is exact, so the shares plus the residual must account for the whole score");
        }
    }

    [Fact]
    public void Reasons_NeverNameAContextFeature()
    {
        var (current, history) = Cohort(userCount: 60);
        var result = Engine().Run(current, history);

        result.SelectMany(r => r.Reasons.Concat(r.SecondarySignals))
            .Should().OnlyContain(r => r.Dimension != "context" && r.Polarity != "context_only");
        result.Should().OnlyContain(r => r.TeamContext.Count > 0);
    }

    [Fact]
    public void ThinBaseline_IsReportedAsLowConfidence_NotAsASelfReason()
    {
        var now = DateTime.UtcNow;
        var (current, history) = Cohort(userCount: 30);

        current.Add(Incident("newcomer@company.com", now.AddDays(-1), 5, dataSensitivity: 400));
        current.Add(Incident("newcomer@company.com", now.AddDays(-2), 5, dataSensitivity: 380));

        var user = Engine().Run(current, history).Single(r => r.UserEmail == "newcomer@company.com");

        user.ConfidenceLevel.Should().Be("low");
        user.Caveats.Should().Contain("insufficient_personal_baseline");
        user.Reasons.Should().NotContain(r => r.Dimension == "self");

        var self = user.Dimensions.Single(d => d.Dimension == "self");
        self.Available.Should().BeFalse();
        self.ReasonKey.Should().Be("noBaseline");
    }

    [Fact]
    public void Evidence_PointsAtRealWindowIncidents()
    {
        var (current, history) = Cohort(userCount: 40);
        for (var i = 0; i < current.Count; i++) current[i].Id = i + 1;

        var result = Engine().Run(current, history);

        foreach (var user in result)
        {
            var windowIds = current.Where(c => c.UserEmail == user.UserEmail).Select(c => c.Id).ToHashSet();
            foreach (var reason in user.Reasons.Concat(user.SecondarySignals))
            {
                // A family whose predicate matched nothing legitimately has no evidence, so this is
                // a subset check rather than a non-empty one.
                reason.EvidenceIncidentIds.Should().BeSubsetOf(windowIds,
                    "evidence must be the user's own incidents from the scored window");
                reason.EvidenceCount.Should().BeGreaterThanOrEqualTo(reason.EvidenceIncidentIds.Count);
            }
        }

        result.SelectMany(r => r.Reasons.Concat(r.SecondarySignals))
            .Should().Contain(r => r.EvidenceIncidentIds.Count > 0,
                "at least some reasons must be traceable back to concrete incidents");
    }

    // ── Model corrections (opt-in; these change every score) ─────────────────

    [Fact]
    public void ModelCorrections_AreOffByDefault()
    {
        IsolationForestOptions.Default.EnableModelCorrections.Should().BeFalse();
        IsolationForestOptions.Default.UseAbsoluteScoreScale.Should().BeFalse();
        IsolationForestOptions.Default.AbsoluteAnomalyThreshold.Should().BeNull();
    }

    [Fact]
    public void ModelCorrections_DropDeptContextFromTheMatrix_AndSignTheSelfMean()
    {
        var (current, history) = Cohort(userCount: 60);
        var corrected = Engine(new IsolationForestOptions { EnableModelCorrections = true }).Run(current, history);

        corrected.SelectMany(r => r.TopFeatures)
            .Should().NotContain(f => f.Name.StartsWith("dept_") || f.Name == "self_baseline_available");

        // TeamContext still carries the department facts — they left the matrix, not the payload.
        corrected.Should().OnlyContain(r => r.TeamContext.ContainsKey("dept_size"));

        var signed = corrected
            .SelectMany(r => r.Reasons.Concat(r.SecondarySignals))
            .Where(r => r.AnchorFeature.StartsWith("mean_self_z_"))
            .ToList();
        signed.Should().OnlyContain(r => r.ValueKind == "z" && r.Deviation != "unknown");
    }

    [Fact]
    public void ModelCorrections_StopIsolatingAUserForHavingNoHistory()
    {
        var now = DateTime.UtcNow;
        var (current, history) = Cohort(userCount: 30);

        // Unremarkable behaviour, no prior history at all. Every one of the 16 self columns is
        // therefore 0, which standardizes to a large negative — so the untouched model isolates
        // this user in sixteen dimensions purely for being new.
        current.Add(Incident("newcomer@company.com", now.AddDays(-1), 3, dataSensitivity: 60));

        double RawScore(IsolationForestOptions options) =>
            Engine(options).Run(current, history).Single(r => r.UserEmail == "newcomer@company.com").AnomalyRaw;

        var untouched = RawScore(IsolationForestOptions.Default);
        var corrected = RawScore(new IsolationForestOptions { EnableModelCorrections = true });

        corrected.Should().BeLessThan(untouched,
            "imputing at the observed median removes the isolation that missing data was creating");
    }

    [Fact]
    public void AbsoluteScoreScale_IsStableAcrossCohorts()
    {
        var (currentA, historyA) = Cohort(userCount: 40);
        var (currentB, historyB) = Cohort(userCount: 60);
        var options = new IsolationForestOptions { UseAbsoluteScoreScale = true };

        var a = Engine(options).Run(currentA, historyA);
        var b = Engine(options).Run(currentB, historyB);

        // Min-max pins the top user at exactly 100 in EVERY run; a fixed scale must not.
        a.Should().OnlyContain(r => r.IFScore >= 0 && r.IFScore <= 100);
        b.Should().OnlyContain(r => r.IFScore >= 0 && r.IFScore <= 100);
        (a.Max(r => r.IFScore) == 100 && b.Max(r => r.IFScore) == 100).Should().BeFalse(
            "a fixed scale must not manufacture a perfect 100 in every cohort");
    }

    [Fact]
    public void AbsoluteAnomalyThreshold_CanEmptyTheQueueOnAQuietRun()
    {
        var (current, history) = Cohort(userCount: 40);

        var unreachable = Engine(new IsolationForestOptions { AbsoluteAnomalyThreshold = 0.999 })
            .Run(current, history);
        unreachable.Should().OnlyContain(r => !r.IsAnomaly,
            "a quiet week must be allowed to produce zero absolute anomalies");

        var relative = Engine().Run(current, history);
        relative.Should().Contain(r => r.IsAnomaly, "the relative review queue still exists");
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A cohort with enough spread that standardization does not collapse to ±0.70711 and the
    /// departments are large enough for peer z-scores to engage.
    /// </summary>
    private static (List<Incident> Current, List<Incident> History) Cohort(int userCount)
    {
        var rng = new Random(1907);
        var now = DateTime.UtcNow;
        var departments = new[] { "Hazine", "BT", "Operasyon", "Satis", "Hukuk" };
        var channels = new[] { "Email", "Web", "USB", "Printer", "Cloud" };
        var actions = new[] { "blocked", "quarantined", "notified", "allowed", "authorized" };

        var current = new List<Incident>();
        var history = new List<Incident>();

        for (var u = 0; u < userCount; u++)
        {
            var email = $"user{u:D3}@company.com";
            var dept = departments[u % departments.Length];
            var intensity = 1 + u % 7;

            for (var i = 0; i < 2 + intensity; i++)
                current.Add(new Incident
                {
                    UserEmail = email,
                    Department = dept,
                    Timestamp = now.AddDays(-rng.Next(0, 7)).AddHours(-rng.Next(0, 24)),
                    Severity = 1 + (u + i) % 5,
                    DataSensitivity = rng.Next(1, 50) * intensity,
                    MaxMatches = rng.Next(1, 200) * intensity,
                    Channel = channels[(u + i) % channels.Length],
                    Destination = $"dest{(u + i) % 11}@example.com",
                    Policy = $"policy-{(u + i) % 9}",
                    Action = actions[(u + i) % actions.Length]
                });

            for (var i = 0; i < 10 + u % 5; i++)
                history.Add(new Incident
                {
                    UserEmail = email,
                    Department = dept,
                    Timestamp = now.AddDays(-10 - rng.Next(0, 300)),
                    Severity = 1 + (u + i) % 4,
                    DataSensitivity = rng.Next(1, 40) * intensity,
                    MaxMatches = rng.Next(1, 150) * intensity,
                    Channel = channels[(u + i + 2) % channels.Length],
                    Destination = $"dest{(u + i) % 7}@example.com",
                    Policy = $"policy-{(u + i) % 6}",
                    Action = actions[(u + i + 1) % actions.Length]
                });
        }

        return (current, history);
    }

    private static IsolationTree Leaf(int size) => new() { IsLeaf = true, Size = size };

    private static IsolationTree Node(int feature, double split, IsolationTree left, IsolationTree right) =>
        new() { IsLeaf = false, Feature = feature, Split = split, Left = left, Right = right };

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
