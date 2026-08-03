using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Pure C# Isolation Forest + feature engineering:
/// - 3 anomaly dimensions: population (raw), peer (dept z-score), self (personal baseline z-score)
/// - Log1p + StandardScaler normalization
/// - Model-faithful feature contributions via <see cref="ForestAttributor"/> (CCMA): each
///   contribution is the score drop when the feature is replaced by the cohort mean and the user
///   is re-scored through the same fitted trees.
/// </summary>
public class IsolationForestEngine
{
    private readonly ILogger<IsolationForestEngine> _logger;
    private readonly IsolationForestOptions _options;

    public IsolationForestEngine(ILogger<IsolationForestEngine> logger)
        : this(logger, IsolationForestOptions.Default)
    {
    }

    public IsolationForestEngine(ILogger<IsolationForestEngine> logger, IsolationForestOptions options)
    {
        _logger = logger;
        _options = options;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full pipeline for the scoring window. Incidents before the scoring
    /// window are used only as the user's personal baseline and are never scored.
    /// </summary>
    public List<IsolationForestScoreDto> Run(
        List<Incident> scoringIncidents,
        List<Incident> historicalIncidents)
    {
        _logger.LogInformation(
            "IsolationForestEngine: scoring {ScoringCount} incidents against {HistoricalCount} historical incidents",
            scoringIncidents.Count,
            historicalIncidents.Count);

        var users = BuildUserFeatures(scoringIncidents, historicalIncidents);
        if (users.Count == 0)
        {
            _logger.LogWarning("IsolationForestEngine: no users after feature engineering");
            return new();
        }

        var (featureNames, matrix, rawMatrix) = BuildScaledMatrix(users);
        var (scores, trees, c) = FitAndScore(matrix);

        // Normalize to 0-100.
        //
        // Min-max is a POSITION WITHIN THIS RUN, not an absolute level: the top user always lands
        // on exactly 100 and the bottom on exactly 0 however benign the week, so two days' scores
        // cannot be compared. AnomalyRaw carries the comparable quantity, and UseAbsoluteScoreScale
        // switches the display over — that changes every score, so it is opt-in.
        var minS = scores.Min();
        var maxS = scores.Max();
        var hasScoreVariation = maxS - minS > 1e-9;
        var range = Math.Max(maxS - minS, 1e-9);

        var normalized = _options.UseAbsoluteScoreScale
            ? scores.Select(AbsoluteScale).ToArray()
            : scores.Select(s => (s - minS) / range * 100.0).ToArray();

        // Flag exactly the highest-scoring contamination slice. This is a review-queue sizing rule
        // (analyst capacity is fixed), not a measurement of how risky the week was. If all scores
        // are equal, there is no evidence for ranking one user as more anomalous.
        var anomalyTargetCount = Math.Max(1, (int)Math.Ceiling(scores.Length * _options.Contamination));
        var anomalyIndexes = hasScoreVariation
            ? scores
                .Select((score, index) => new { score, index })
                .OrderByDescending(x => x.score)
                .Take(anomalyTargetCount)
                .Select(x => x.index)
                .ToHashSet()
            : new HashSet<int>();

        // Optional second gate on the raw score, so a genuinely quiet week can produce zero
        // absolute anomalies while the review queue still exists.
        if (_options.AbsoluteAnomalyThreshold is { } threshold)
            anomalyIndexes.RemoveWhere(i => scores[i] <= threshold);

        var contributions = ComputeContributions(
            featureNames, matrix, rawMatrix, scores, range, trees, c, users);

        var result = new IsolationForestScoreDto[users.Count];
        for (int i = 0; i < users.Count; i++)
        {
            var u = users[i];
            var a = contributions[i];
            var e = a.Explanation;

            result[i] = new IsolationForestScoreDto
            {
                UserEmail = u.Source,
                Department = u.Department,
                CalculatedAt = DateTime.UtcNow,
                IFScore = Math.Round(normalized[i], 1),
                AnomalyRaw = scores[i],
                IsAnomaly = users.Count > 1 && anomalyIndexes.Contains(i),
                IncidentCount = u.IncidentCount,
                BaselineIncidentCount = u.BaselineIncidentCount,
                TopFeatures = a.TopFeatures.Take(8).ToList(),
                // Computed over the FULL eligible contribution vector, not the truncated top-N —
                // otherwise a dimension made of many small contributions reports 0.
                GroupBreakdown = a.GroupBreakdown,
                Reasons = e.Reasons,
                SecondarySignals = e.SecondarySignals,
                Dimensions = e.Dimensions,
                ConfidenceLevel = e.ConfidenceLevel,
                ExplainedSharePct = e.ExplainedSharePct,
                UnexplainedSharePct = e.UnexplainedSharePct,
                CohortRank = e.CohortRank,
                CohortSize = e.CohortSize,
                Caveats = e.Caveats,
                TeamContext = e.TeamContext
            };
        }

        _logger.LogInformation("IsolationForestEngine: finished, {Anomalies}/{Total} anomalies", result.Count(r => r.IsAnomaly), result.Length);
        return result.ToList();
    }

    /// <summary>
    /// Maps the raw isolation-forest score onto a fixed 0-100 band. The raw score already lives in
    /// (0,1) with 0.5 meaning "average path length", so the anchors carry a stable meaning across
    /// runs — unlike min-max, which is redefined by whoever happens to be top that day.
    /// </summary>
    private double AbsoluteScale(double raw)
    {
        var span = Math.Max(_options.AbsoluteScaleCeiling - _options.AbsoluteScaleFloor, 1e-9);
        return Math.Clamp((raw - _options.AbsoluteScaleFloor) / span, 0.0, 1.0) * 100.0;
    }

    // ── Feature Engineering (mirrors notebook sections 3-7) ──────────────────

    private List<UserFeatures> BuildUserFeatures(
        List<Incident> scoringIncidents,
        List<Incident> historicalIncidents)
    {
        // ── Step 3: incident-level enrichment ──────────────────────────────
        var enriched = scoringIncidents.Select(inc => new EnrichedIncident(inc)).ToList();
        var historical = historicalIncidents.Select(inc => new EnrichedIncident(inc)).ToList();

        // Channel rarity is learned from prior history. For a brand-new population,
        // the current cohort is used as a safe fallback.
        var rarityReference = historical.Count > 0 ? historical : enriched;
        var chFreq = rarityReference
            .GroupBy(e => e.Channel)
            .ToDictionary(g => g.Key, g => (double)g.Count() / rarityReference.Count);
        var unseenChannelFrequency = 1.0 / (rarityReference.Count + 1.0);

        foreach (var e in historical)
            e.ChannelRarity = -Math.Log(Math.Max(chFreq.GetValueOrDefault(e.Channel, unseenChannelFrequency), 1e-6));
        foreach (var e in enriched)
            e.ChannelRarity = -Math.Log(Math.Max(chFreq.GetValueOrDefault(e.Channel, unseenChannelFrequency), 1e-6));

        // ── Step 4: compare current incidents with the user's entire prior history ──
        var selfBaseCols = new[] { "tx_size", "max_matches", "channel_rarity", "severity", "action_risk", "hour" };
        var userStats = historical
            .GroupBy(e => e.Source)
            .ToDictionary(g => g.Key, g => new UserStats(
                g.ToList(), _options.MinUserIncidents, _options.EnableModelCorrections, _options.MaxSelfZ));
        var historicalCounts = historical
            .GroupBy(e => e.Source)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var e in enriched)
        {
            var stats = userStats.GetValueOrDefault(e.Source) ?? UserStats.Empty;
            e.SelfZ["tx_size"] = stats.SelfZ(e.TxSize, "tx_size");
            e.SelfZ["max_matches"] = stats.SelfZ(e.MaxMatches, "max_matches");
            e.SelfZ["channel_rarity"] = stats.SelfZ(e.ChannelRarity, "channel_rarity");
            e.SelfZ["severity"] = stats.SelfZ(e.Severity, "severity");
            e.SelfZ["action_risk"] = stats.SelfZ(e.ActionRisk, "action_risk");
            e.SelfZ["hour"] = stats.SelfZ(e.Hour, "hour");
            e.AnySelfOutlier = e.SelfZ.Values.Any(z => Math.Abs(z) > 2) ? 1 : 0;
            e.StrongSelfOutlier = e.SelfZ.Values.Any(z => Math.Abs(z) > 4) ? 1 : 0;
        }

        // ── Step 5a: classic user aggregates ──────────────────────────────
        var userGroups = enriched.GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.ToList());
        var users = userGroups.Select(kv => AggregatUser(kv.Key, kv.Value)).ToList();

        // ── Step 5b: self-baseline aggregates ─────────────────────────────
        foreach (var u in users)
        {
            var incs = userGroups[u.Source];
            u.BaselineIncidentCount = historicalCounts.GetValueOrDefault(u.Source);
            u.HasSelfBaseline = userStats.GetValueOrDefault(u.Source)?.HasBaseline == true;
            foreach (var col in selfBaseCols)
            {
                u.SelfAgg[$"max_self_z_{col}"] = incs.Max(e => Math.Abs(e.SelfZ.GetValueOrDefault(col, 0)));
                // The mean keeps its sign under corrections: aggregating Math.Abs destroys the one
                // piece of information a self reason needs to say "above his norm" vs "below".
                u.SelfAgg[$"mean_self_z_{col}"] = _options.EnableModelCorrections
                    ? incs.Average(e => e.SelfZ.GetValueOrDefault(col, 0))
                    : incs.Average(e => Math.Abs(e.SelfZ.GetValueOrDefault(col, 0)));
            }
            u.SelfOutlierCount = incs.Sum(e => e.AnySelfOutlier);
            u.StrongSelfOutlierCount = incs.Sum(e => e.StrongSelfOutlier);
            u.SelfOutlierRatio = u.IncidentCount > 0 ? (double)u.SelfOutlierCount / u.IncidentCount : 0;
            u.StrongSelfOutlierRatio = u.IncidentCount > 0 ? (double)u.StrongSelfOutlierCount / u.IncidentCount : 0;
            u.FamilyEvidence = CollectFamilyEvidence(incs);
        }

        // ── Step 6: dept context + peer z-scores ──────────────────────────
        AddDeptContext(users, _options.MinDeptSize);

        return users;
    }

    private const int MaxEvidencePerFamily = 50;

    /// <summary>
    /// Records which of the user's window incidents drove each behaviour family, at scoring time.
    /// Persisting the ids is what makes the drill-down exact: recomputing the predicate at query
    /// time would drift as soon as the model or the window changes.
    /// </summary>
    private static Dictionary<string, FamilyEvidence> CollectFamilyEvidence(List<EnrichedIncident> incs)
    {
        static FamilyEvidence Filter(IEnumerable<EnrichedIncident> matching)
        {
            var list = matching.ToList();
            return new FamilyEvidence(
                list.Take(MaxEvidencePerFamily).Select(e => e.Id).ToList(),
                list.Count);
        }

        // For "how extreme" families every incident qualifies; the interesting ones are the top of
        // the ordering, so the total stays the window count.
        FamilyEvidence Ranked(Func<EnrichedIncident, double> key) => new(
            incs.OrderByDescending(key).ThenBy(e => e.Id).Take(MaxEvidencePerFamily).Select(e => e.Id).ToList(),
            incs.Count);

        var spread = incs
            .GroupBy(e => (e.Channel, e.Destination))
            .Select(g => g.OrderBy(e => e.Id).First())
            .ToList();

        return new Dictionary<string, FamilyEvidence>(StringComparer.Ordinal)
        {
            [RiskFeatureCatalog.FamOffHours] = Filter(incs.Where(e => e.IsOffHours || e.IsWeekend || e.IsNight)),
            [RiskFeatureCatalog.FamPermissiveOutcome] = Filter(incs.Where(e => e.ActionRisk >= 4)),
            [RiskFeatureCatalog.FamSeverity] = Filter(incs.Where(e => e.Severity >= 4)),
            [RiskFeatureCatalog.FamBaselineBreak] = Filter(incs.Where(e => e.AnySelfOutlier == 1)),
            [RiskFeatureCatalog.FamDataSensitivity] = Ranked(e => e.TxSize),
            [RiskFeatureCatalog.FamClassifierHits] = Ranked(e => e.MaxMatches),
            [RiskFeatureCatalog.FamRareChannel] = Ranked(e => e.ChannelRarity),
            [RiskFeatureCatalog.FamSpread] = new(
                spread.Take(MaxEvidencePerFamily).Select(e => e.Id).ToList(), spread.Count),
            [RiskFeatureCatalog.FamVolume] = new(
                incs.OrderBy(e => e.Timestamp).Take(MaxEvidencePerFamily).Select(e => e.Id).ToList(), incs.Count)
        };
    }

    private sealed record FamilyEvidence(List<int> Ids, int Count)
    {
        public static FamilyEvidence None { get; } = new(new List<int>(), 0);
    }

    private static UserFeatures AggregatUser(string source, List<EnrichedIncident> incs)
    {
        var activeDays = Math.Max(1.0,
            (incs.Max(i => i.Timestamp) - incs.Min(i => i.Timestamp)).TotalDays);

        var u = new UserFeatures
        {
            Source = source,
            Department = incs.First().Department ?? "Unknown",
            IncidentCount = incs.Count,
            UniquePolicies = incs.Select(i => i.Policy).Distinct().Count(),
            UniqueChannels = incs.Select(i => i.Channel).Distinct().Count(),
            UniqueDestinations = incs.Select(i => i.Destination).Distinct().Count(),
            MeanSeverity = incs.Average(i => i.Severity),
            MaxSeverity = incs.Max(i => i.Severity),
            HighSevCount = incs.Count(i => i.Severity >= 4),
            HighSevRatio = incs.Average(i => i.Severity >= 4 ? 1.0 : 0.0),
            MeanActionRisk = incs.Average(i => i.ActionRisk),
            AllowedCount = incs.Count(i => i.ActionRisk >= 4),
            AllowedRatio = incs.Average(i => i.ActionRisk >= 4 ? 1.0 : 0.0),
            TotalTxSize = incs.Sum(i => i.TxSize),
            MeanTxSize = incs.Average(i => i.TxSize),
            MaxTxSize = incs.Max(i => i.TxSize),
            StdTxSize = StdDev(incs.Select(i => i.TxSize)),
            TotalMaxMatches = incs.Sum(i => i.MaxMatches),
            MeanMaxMatches = incs.Average(i => i.MaxMatches),
            MaxMaxMatches = incs.Max(i => i.MaxMatches),
            OffHoursCount = incs.Count(i => i.IsOffHours),
            OffHoursRatio = incs.Average(i => i.IsOffHours ? 1.0 : 0.0),
            WeekendRatio = incs.Average(i => i.IsWeekend ? 1.0 : 0.0),
            NightRatio = incs.Average(i => i.IsNight ? 1.0 : 0.0),
            MeanChannelRarity = incs.Average(i => i.ChannelRarity),
            MaxChannelRarity = incs.Max(i => i.ChannelRarity),
            IncidentsPerDay = incs.Count / activeDays,
        };
        return u;
    }

    private static void AddDeptContext(List<UserFeatures> users, int minDeptSize)
    {
        var deptGroups = users.GroupBy(u => u.Department).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kv in deptGroups)
        {
            var dept = kv.Value;
            var deptSize = dept.Count;
            foreach (var u in dept)
            {
                u.DeptSize = deptSize;
                u.DeptMeanIncidentCount = dept.Average(d => d.IncidentCount);
                u.DeptMeanOffHoursRatio = dept.Average(d => d.OffHoursRatio);
                u.DeptMeanAllowedRatio = dept.Average(d => d.AllowedRatio);
                u.DeptMeanHighSevRatio = dept.Average(d => d.HighSevRatio);
                u.DeptMeanTxSize = dept.Average(d => d.MeanTxSize);
            }
        }

        // Peer z-scores for key metrics
        var peerCols = new (string Name, Func<UserFeatures, double> Get, Action<UserFeatures, double> Set)[]
        {
            ("incident_count", u => u.IncidentCount, (u, v) => u.PeerZ["incident_count"] = v),
            ("unique_policies", u => u.UniquePolicies, (u, v) => u.PeerZ["unique_policies"] = v),
            ("unique_channels", u => u.UniqueChannels, (u, v) => u.PeerZ["unique_channels"] = v),
            ("unique_destinations", u => u.UniqueDestinations, (u, v) => u.PeerZ["unique_destinations"] = v),
            ("high_sev_ratio", u => u.HighSevRatio, (u, v) => u.PeerZ["high_sev_ratio"] = v),
            ("allowed_ratio", u => u.AllowedRatio, (u, v) => u.PeerZ["allowed_ratio"] = v),
            ("off_hours_ratio", u => u.OffHoursRatio, (u, v) => u.PeerZ["off_hours_ratio"] = v),
            ("weekend_ratio", u => u.WeekendRatio, (u, v) => u.PeerZ["weekend_ratio"] = v),
            ("night_ratio", u => u.NightRatio, (u, v) => u.PeerZ["night_ratio"] = v),
            ("mean_tx_size", u => u.MeanTxSize, (u, v) => u.PeerZ["mean_tx_size"] = v),
            ("max_tx_size", u => u.MaxTxSize, (u, v) => u.PeerZ["max_tx_size"] = v),
            ("mean_max_matches", u => u.MeanMaxMatches, (u, v) => u.PeerZ["mean_max_matches"] = v),
            ("incidents_per_day", u => u.IncidentsPerDay, (u, v) => u.PeerZ["incidents_per_day"] = v),
        };

        foreach (var col in peerCols)
        {
            var globalMean = users.Average(col.Get);
            var globalStd = Math.Max(StdDev(users.Select(col.Get)), 1e-9);

            foreach (var kv in deptGroups)
            {
                var dept = kv.Value;
                var deptMean = dept.Average(col.Get);
                var deptStd = dept.Count >= minDeptSize ? Math.Max(StdDev(dept.Select(col.Get)), 1e-9) : globalStd;
                var refMean = dept.Count >= minDeptSize ? deptMean : globalMean;
                var refStd = dept.Count >= minDeptSize ? deptStd : globalStd;
                foreach (var u in dept)
                    col.Set(u, (col.Get(u) - refMean) / refStd);
            }
        }
    }

    // ── Matrix Building + Scaling ─────────────────────────────────────────────

    private static readonly string[] RawFeatureNames = {
        "incident_count", "unique_policies", "unique_channels", "unique_destinations",
        "mean_severity", "max_severity", "high_sev_ratio", "high_sev_count",
        "mean_action_risk", "allowed_ratio", "allowed_count",
        "total_tx_size", "mean_tx_size", "max_tx_size", "std_tx_size",
        "total_max_matches", "mean_max_matches", "max_max_matches",
        "off_hours_ratio", "off_hours_count", "weekend_ratio", "night_ratio",
        "mean_channel_rarity", "max_channel_rarity", "incidents_per_day"
    };

    private static readonly string[] DeptContextFeatureNames = {
        "dept_size", "dept_mean_incident_count", "dept_mean_off_hours_ratio",
        "dept_mean_allowed_ratio", "dept_mean_high_sev_ratio", "dept_mean_tx_size"
    };

    private static readonly HashSet<string> LogCols = new(new[] {
        "incident_count", "unique_policies", "unique_channels", "unique_destinations",
        "high_sev_count", "allowed_count",
        "total_tx_size", "mean_tx_size", "max_tx_size", "std_tx_size",
        "total_max_matches", "mean_max_matches", "max_max_matches",
        "off_hours_count", "incidents_per_day",
        "dept_size", "dept_mean_incident_count", "dept_mean_tx_size",
        "self_outlier_count", "strong_self_outlier_count"
    });

    /// <summary>
    /// Returns the feature names, the scaled matrix the forest sees, and a snapshot of the values
    /// in their original units taken <em>before</em> log1p and standardization. The explanation
    /// layer needs that snapshot: "14 off-hours incidents" is evidence, "1.83" is not.
    /// </summary>
    private (string[] names, double[][] matrix, double[][] raw) BuildScaledMatrix(List<UserFeatures> users)
    {
        var featureNames = new List<string>();
        featureNames.AddRange(RawFeatureNames);

        // The dept_* columns are constant within a department, so the forest can isolate whole
        // teams on them, and the 13 *_peer_z columns already encode exactly these means. They stay
        // in the payload as TeamContext either way — this only removes them from the matrix.
        if (!_options.EnableModelCorrections)
            featureNames.AddRange(DeptContextFeatureNames);

        // Peer feature names
        if (users.First().PeerZ.Count > 0)
            featureNames.AddRange(users.First().PeerZ.Keys.Select(k => k + "_peer_z"));

        // Self feature names
        if (users.First().SelfAgg.Count > 0)
            featureNames.AddRange(users.First().SelfAgg.Keys);

        featureNames.Add("self_outlier_count");
        featureNames.Add("strong_self_outlier_count");
        featureNames.Add("self_outlier_ratio");
        featureNames.Add("strong_self_outlier_ratio");

        if (_options.EnableModelCorrections)
            featureNames.Add("self_baseline_available");

        // Build raw matrix
        var rawMatrix = users.Select(u => GetFeatureVector(u, featureNames.ToArray())).ToArray();

        if (_options.EnableModelCorrections)
            ImputeMissingSelfColumns(users, featureNames, rawMatrix);

        // Keep the original units for the explanation layer before any transform touches them.
        var rawSnapshot = rawMatrix.Select(r => (double[])r.Clone()).ToArray();

        // Log1p for heavy-tailed features
        for (int f = 0; f < featureNames.Count; f++)
        {
            if (!LogCols.Contains(featureNames[f])) continue;
            for (int r = 0; r < rawMatrix.Length; r++)
                rawMatrix[r][f] = Math.Log(1.0 + Math.Max(0, rawMatrix[r][f]));
        }

        // Standard scaling (per column)
        for (int f = 0; f < featureNames.Count; f++)
        {
            var col = rawMatrix.Select(r => r[f]).ToArray();
            var mean = col.Average();
            var std = Math.Max(StdDev(col.AsEnumerable()), 1e-9);
            for (int r = 0; r < rawMatrix.Length; r++)
                rawMatrix[r][f] = (rawMatrix[r][f] - mean) / std;
        }

        return (featureNames.ToArray(), rawMatrix, rawSnapshot);
    }

    /// <summary>
    /// Replaces the self columns of users with no usable personal baseline by the median over the
    /// users who <em>do</em> have one. Leaving them at 0 encodes "no data" as an extreme value:
    /// after standardization it lands around −3σ, so a new joiner is isolated precisely because
    /// they are new, and then explained with a personal-baseline metric that does not exist for
    /// them. The median is used rather than the mean so a degenerate baseline cannot drag it.
    /// The companion <c>self_baseline_available</c> column keeps the missingness itself visible
    /// to the model, as a fact rather than as a fake measurement.
    /// </summary>
    private static void ImputeMissingSelfColumns(
        List<UserFeatures> users, List<string> featureNames, double[][] matrix)
    {
        var observed = Enumerable.Range(0, users.Count).Where(i => users[i].HasSelfBaseline).ToArray();
        if (observed.Length == 0 || observed.Length == users.Count) return;

        var missing = Enumerable.Range(0, users.Count).Where(i => !users[i].HasSelfBaseline).ToArray();

        for (int f = 0; f < featureNames.Count; f++)
        {
            var name = featureNames[f];
            if (name == "self_baseline_available") continue;
            if (!name.Contains("self_z") && !name.Contains("self_outlier")) continue;

            var values = observed.Select(i => matrix[i][f]).OrderBy(v => v).ToArray();
            var mid = values.Length / 2;
            var median = values.Length % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2.0;

            foreach (var i in missing) matrix[i][f] = median;
        }
    }

    private static double[] GetFeatureVector(UserFeatures u, string[] names)
    {
        var v = new double[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            v[i] = names[i] switch
            {
                "incident_count" => u.IncidentCount,
                "unique_policies" => u.UniquePolicies,
                "unique_channels" => u.UniqueChannels,
                "unique_destinations" => u.UniqueDestinations,
                "mean_severity" => u.MeanSeverity,
                "max_severity" => u.MaxSeverity,
                "high_sev_ratio" => u.HighSevRatio,
                "high_sev_count" => u.HighSevCount,
                "mean_action_risk" => u.MeanActionRisk,
                "allowed_ratio" => u.AllowedRatio,
                "allowed_count" => u.AllowedCount,
                "total_tx_size" => u.TotalTxSize,
                "mean_tx_size" => u.MeanTxSize,
                "max_tx_size" => u.MaxTxSize,
                "std_tx_size" => u.StdTxSize,
                "total_max_matches" => u.TotalMaxMatches,
                "mean_max_matches" => u.MeanMaxMatches,
                "max_max_matches" => u.MaxMaxMatches,
                "off_hours_ratio" => u.OffHoursRatio,
                "off_hours_count" => u.OffHoursCount,
                "weekend_ratio" => u.WeekendRatio,
                "night_ratio" => u.NightRatio,
                "mean_channel_rarity" => u.MeanChannelRarity,
                "max_channel_rarity" => u.MaxChannelRarity,
                "incidents_per_day" => u.IncidentsPerDay,
                "dept_size" => u.DeptSize,
                "dept_mean_incident_count" => u.DeptMeanIncidentCount,
                "dept_mean_off_hours_ratio" => u.DeptMeanOffHoursRatio,
                "dept_mean_allowed_ratio" => u.DeptMeanAllowedRatio,
                "dept_mean_high_sev_ratio" => u.DeptMeanHighSevRatio,
                "dept_mean_tx_size" => u.DeptMeanTxSize,
                "self_outlier_count" => u.SelfOutlierCount,
                "strong_self_outlier_count" => u.StrongSelfOutlierCount,
                "self_outlier_ratio" => u.SelfOutlierRatio,
                "strong_self_outlier_ratio" => u.StrongSelfOutlierRatio,
                "self_baseline_available" => u.HasSelfBaseline ? 1 : 0,
                _ => names[i].EndsWith("_peer_z")
                    ? u.PeerZ.GetValueOrDefault(names[i][..^7], 0)
                    : u.SelfAgg.GetValueOrDefault(names[i], 0)
            };
        }
        return v;
    }

    // ── Isolation Forest (iTrees) ─────────────────────────────────────────────

    private (double[] Scores, IsolationTree[] Trees, double C) FitAndScore(double[][] matrix)
    {
        int n = matrix.Length;
        int d = matrix[0].Length;

        if (n == 1)
            return (new[] { 0.0 }, Array.Empty<IsolationTree>(), 1.0);

        int actualMaxSamples = Math.Min(_options.MaxSamples, n);
        var maxDepth = (int)Math.Ceiling(Math.Log2(actualMaxSamples));

        // Each tree derives its own Random from (seed, index) so the forest is identical
        // regardless of build order or thread count.
        var trees = new IsolationTree[_options.NEstimators];
        for (int t = 0; t < _options.NEstimators; t++)
        {
            var rng = new Random(_options.Seed * 31 + t);
            var sample = SampleRows(matrix, actualMaxSamples, rng);
            trees[t] = BuildTree(sample, 0, maxDepth, d, rng);
        }

        double c = IsolationTree.AvgPathLength(actualMaxSamples);
        var scores = new double[n];
        for (int i = 0; i < n; i++)
        {
            var avgPath = trees.Average(tree => IsolationTree.PathLength(matrix[i], tree, 0));
            scores[i] = Math.Pow(2, -avgPath / c); // higher = more anomalous
        }
        return (scores, trees, c);
    }

    /// <summary>Partial Fisher-Yates: draws k distinct rows without ordering the whole population.</summary>
    private static double[][] SampleRows(double[][] matrix, int k, Random rng)
    {
        int n = matrix.Length;
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;

        for (int i = 0; i < k; i++)
        {
            int j = i + rng.Next(n - i);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }

        var sample = new double[k][];
        for (int i = 0; i < k; i++) sample[i] = matrix[idx[i]];
        return sample;
    }

    private IsolationTree BuildTree(double[][] data, int depth, int maxDepth, int d, Random rng)
    {
        if (depth >= maxDepth || data.Length <= 1)
            return new IsolationTree { IsLeaf = true, Size = data.Length };

        var allowed = _options.AllowedFeatures;
        int feat = allowed is null ? rng.Next(d) : allowed[rng.Next(allowed.Count)];

        var vals = data.Select(r => r[feat]).ToArray();
        var min = vals.Min();
        var max = vals.Max();
        if (Math.Abs(max - min) < 1e-9)
            return new IsolationTree { IsLeaf = true, Size = data.Length };

        var split = min + rng.NextDouble() * (max - min);
        var left = data.Where(r => r[feat] < split).ToArray();
        var right = data.Where(r => r[feat] >= split).ToArray();

        return new IsolationTree
        {
            IsLeaf = false,
            Feature = feat,
            Split = split,
            Left = BuildTree(left, depth + 1, maxDepth, d, rng),
            Right = BuildTree(right, depth + 1, maxDepth, d, rng)
        };
    }

    // ── Model-faithful Feature Contributions (CCMA) ───────────────────────────

    /// <summary>
    /// For every user, the per-feature score delta obtained by replacing that feature with the
    /// cohort mean and re-scoring through the same fitted trees (see <see cref="ForestAttributor"/>).
    /// Returned already ranked by |Δ| descending, with two candidates removed:
    /// <list type="bullet">
    /// <item>every <c>dept_ctx</c> feature — they are identical for all members of a department,
    /// so they describe the team, not the person, and can never be an actionable reason;</item>
    /// <item>every <c>self</c> feature for a user whose personal baseline is too thin to trust —
    /// otherwise absent data (encoded as 0, which standardizes to a large negative) becomes the
    /// headline reason for a user who has no personal history at all.</item>
    /// </list>
    /// </summary>
    private UserAttribution[] ComputeContributions(
        string[] names,
        double[][] matrix,
        double[][] rawMatrix,
        double[] scores,
        double scoreRange,
        IsolationTree[] trees,
        double c,
        List<UserFeatures> users)
    {
        int n = matrix.Length;
        int d = names.Length;

        var groups = new string[d];
        var meta = new RiskFeature[d];
        for (int f = 0; f < d; f++)
        {
            groups[f] = GetFeatureGroup(names[f]);
            meta[f] = RiskFeatureCatalog.Get(names[f]);

            // With signed aggregation the mean self-z carries a direction, so it stops being a
            // magnitude-only column and a self reason can finally say "above" or "below".
            if (_options.EnableModelCorrections && names[f].StartsWith("mean_self_z_", StringComparison.Ordinal))
                meta[f] = meta[f] with { ValueKind = "z" };
        }

        var result = new UserAttribution[n];

        // A single-user cohort has nothing to be isolated against; no forest, no reasons.
        if (trees.Length == 0)
        {
            for (int i = 0; i < n; i++) result[i] = UserAttribution.Empty(n);
            return result;
        }

        var attributor = new ForestAttributor(trees, c, d);
        var cells = RiskFeatureCatalog.BuildCells(names);
        var columnStats = ColumnStats.Build(rawMatrix, d);
        var centroidScore = attributor.FullyAblatedScore();

        // 1-based rank by score, descending.
        var rankByIndex = new int[n];
        var ordered = Enumerable.Range(0, n).OrderByDescending(i => scores[i]).ToArray();
        for (int r = 0; r < n; r++) rankByIndex[ordered[r]] = r + 1;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism };

        Parallel.For(0, n, parallelOptions, i =>
        {
            var delta = attributor.AttributeSingles(matrix[i], out var baseScore);
            var selfTrusted = users[i].BaselineIncidentCount >= _options.MinUserIncidents;

            // Context features describe the department, not the person, so they never explain
            // anyone. Self features explain nobody whose personal baseline is too thin to trust.
            bool Eligible(int f) =>
                meta[f].Dimension != RiskFeatureCatalog.DimContext &&
                groups[f] != "dept_ctx" &&
                (groups[f] != "self" || selfTrusted) &&
                Math.Abs(delta[f]) > _options.MinAbsContribution;

            // ── Per-feature list (legacy contract) ───────────────────────────
            // Rank on full precision, then round for serialization — never the other way round.
            var topFeatures = Enumerable.Range(0, d)
                .Where(Eligible)
                .OrderByDescending(f => Math.Abs(delta[f]))
                .ThenBy(f => names[f], StringComparer.Ordinal)
                .Select(f => new FeatureContributionDto
                {
                    Name = names[f],
                    DisplayName = names[f],
                    Group = groups[f],
                    ShapValue = Math.Round(delta[f], 6),
                    Direction = delta[f] > 0 ? "risk ↑" : "risk ↓",
                    ActualValue = Math.Round(matrix[i][f], 3)
                })
                .ToList();

            var groupBreakdown = Enumerable.Range(0, d)
                .Where(Eligible)
                .GroupBy(f => groups[f])
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => Math.Abs(delta[f])), 6));

            // ── Reason cells ─────────────────────────────────────────────────
            var candidates = cells
                .Where(cell => cell.Dimension != RiskFeatureCatalog.DimSelf || selfTrusted)
                .Select(cell =>
                {
                    var members = cell.Members.Where(Eligible).ToArray();
                    return (cell, members, impact: members.Length == 0
                        ? 0.0
                        : attributor.AttributeGroup(matrix[i], members, baseScore));
                })
                .Where(x => x.members.Length > 0 && Math.Abs(x.impact) > _options.MinAbsContribution)
                .OrderByDescending(x => Math.Abs(x.impact))
                .ThenBy(x => x.cell.Priority)
                .ThenBy(x => x.cell.Dimension, StringComparer.Ordinal)
                .Take(MaxReasonCells)
                .ToList();

            var sequential = attributor.AttributeSequential(
                matrix[i],
                candidates.Select(x => (IReadOnlyList<int>)x.members).ToList(),
                baseScore,
                out var residual);

            // Total explainable movement: this user's score minus the cohort centroid's. Shares are
            // taken against it so they plus the residual add to exactly 100 rather than being
            // renormalized into a tidy but fictional 100%.
            var total = baseScore - centroidScore;
            var totalUsable = Math.Abs(total) > 1e-12;

            var reasons = new List<ReasonDto>();
            var secondary = new List<ReasonDto>();

            for (int k = 0; k < candidates.Count; k++)
            {
                var (cell, members, impact) = candidates[k];
                var anchor = members.OrderByDescending(f => Math.Abs(delta[f]))
                                    .ThenBy(f => names[f], StringComparer.Ordinal)
                                    .First();

                var reason = BuildReason(
                    cell, members, anchor, names, meta, matrix[i], rawMatrix[i], columnStats,
                    impact, scoreRange, totalUsable ? sequential[k] / total * 100.0 : 0.0, k + 1,
                    users[i].FamilyEvidence.GetValueOrDefault(cell.Family) ?? FamilyEvidence.None);

                if (reason.RiskReading == "risk_indicator" && reason.Effect == "raises")
                    reasons.Add(reason);
                else
                    secondary.Add(reason);
            }

            var explanation = new UserExplanationDto
            {
                Reasons = reasons,
                SecondarySignals = secondary,
                Dimensions = BuildDimensionConfidence(users[i], groupBreakdown, n),
                ConfidenceLevel = ConfidenceLevelOf(users[i], selfTrusted),
                ExplainedSharePct = Math.Round(reasons.Concat(secondary).Sum(r => r.ImpactSharePct), 2),
                UnexplainedSharePct = Math.Round(totalUsable ? residual / total * 100.0 : 100.0, 2),
                CohortRank = rankByIndex[i],
                CohortSize = n,
                Caveats = BuildCaveats(users[i], selfTrusted),
                TeamContext = TeamContextOf(users[i])
            };

            result[i] = new UserAttribution(topFeatures, groupBreakdown, explanation);
        });

        return result;
    }

    private const int MaxReasonCells = 5;

    private static ReasonDto BuildReason(
        RiskCell cell,
        int[] members,
        int anchor,
        string[] names,
        RiskFeature[] meta,
        double[] scaledRow,
        double[] rawRow,
        ColumnStats stats,
        double impact,
        double scoreRange,
        double sharePct,
        int rank,
        FamilyEvidence evidence)
    {
        var m = meta[anchor];
        var observed = rawRow[anchor];

        // A peer column already IS a z against the department mean, so its natural reference is 0.
        var isPeer = m.Dimension == RiskFeatureCatalog.DimPeer;
        var reference = isPeer ? 0.0 : stats.Median[anchor];
        var sigma = isPeer ? observed : scaledRow[anchor];

        // The 12 *_self_z_* columns aggregate Math.Abs(z): the sign is destroyed at aggregation, so
        // claiming an above/below direction for them would be a guess.
        string deviation;
        if (m.ValueKind == "zabs")
            deviation = "unknown";
        else if (observed > reference + 1e-9)
            deviation = "above";
        else if (observed < reference - 1e-9)
            deviation = "below";
        else
            deviation = "at_norm";

        var effect = impact > 1e-12 ? "raises" : impact < -1e-12 ? "lowers" : "none";

        // Impact is the model's verdict; polarity only decides how it is worded. A below-cohort
        // value on a higher-is-riskier metric is genuinely unusual but is not a risk indicator.
        var riskReading = m.Polarity switch
        {
            FeaturePolarity.ContextOnly => "descriptive",
            FeaturePolarity.HigherIsRiskier when deviation == "below" => "unusual_not_risky",
            _ => "risk_indicator"
        };

        return new ReasonDto
        {
            FamilyKey = cell.Family,
            Dimension = cell.Dimension,
            AnchorFeature = names[anchor],
            Members = members.Select(f => names[f]).ToList(),
            Rank = rank,
            Impact = Math.Round(impact, 6),
            ImpactPoints = Math.Round(impact / scoreRange * 100.0, 2),
            ImpactSharePct = Math.Round(sharePct, 2),
            Effect = effect,
            Deviation = deviation,
            DeviationSigma = Math.Round(sigma, 3),
            ObservedValue = Math.Round(observed, 4),
            ReferenceValue = Math.Round(reference, 4),
            TailPct = Math.Round(stats.TailPct(anchor, observed, deviation), 2),
            ValueKind = m.ValueKind,
            Polarity = m.Polarity switch
            {
                FeaturePolarity.HigherIsRiskier => "higher_is_riskier",
                FeaturePolarity.TwoSided => "two_sided",
                _ => "context_only"
            },
            RiskReading = riskReading,
            EvidenceIncidentIds = evidence.Ids,
            EvidenceCount = evidence.Count
        };
    }

    private List<DimensionConfidenceDto> BuildDimensionConfidence(
        UserFeatures u, Dictionary<string, double> groupBreakdown, int cohortSize)
    {
        var totalWeight = groupBreakdown
            .Where(kv => kv.Key != "dept_ctx")
            .Sum(kv => kv.Value);

        double Share(string group) => totalWeight > 1e-12
            ? Math.Round(groupBreakdown.GetValueOrDefault(group) / totalWeight * 100.0, 1)
            : 0.0;

        var baseline = u.BaselineIncidentCount;
        var selfAvailable = baseline >= _options.MinUserIncidents;
        var peerAvailable = u.DeptSize >= _options.MinDeptSize;

        return new List<DimensionConfidenceDto>
        {
            new()
            {
                Dimension = RiskFeatureCatalog.DimSelf,
                Available = selfAvailable,
                Level = !selfAvailable ? "none" : baseline >= 10 ? "high" : "medium",
                ReasonKey = baseline == 0 ? "noBaseline" : !selfAvailable ? "thinBaseline" : null,
                ReasonArgs = new Dictionary<string, double> { ["n"] = baseline },
                SharePct = Share("self")
            },
            new()
            {
                Dimension = RiskFeatureCatalog.DimPeer,
                Available = peerAvailable,
                Level = !peerAvailable ? "low" : u.DeptSize >= 8 ? "high" : "medium",
                ReasonKey = peerAvailable ? null : "tinyDept",
                ReasonArgs = new Dictionary<string, double> { ["n"] = u.DeptSize },
                SharePct = Share("peer")
            },
            new()
            {
                Dimension = RiskFeatureCatalog.DimPopulation,
                Available = cohortSize > 1,
                Level = cohortSize >= 30 ? "high" : cohortSize >= 10 ? "medium" : "low",
                ReasonKey = cohortSize >= 10 ? null : "smallCohort",
                ReasonArgs = new Dictionary<string, double> { ["n"] = cohortSize },
                SharePct = Share("raw")
            }
        };
    }

    private string ConfidenceLevelOf(UserFeatures u, bool selfTrusted)
    {
        if (!selfTrusted) return "low";
        if (u.BaselineIncidentCount >= 10 && u.DeptSize >= _options.MinDeptSize) return "high";
        return "medium";
    }

    private List<string> BuildCaveats(UserFeatures u, bool selfTrusted)
    {
        // The displayed score is a min-max position within this run's cohort, not an absolute level.
        var caveats = new List<string> { "score_is_cohort_relative" };
        if (!selfTrusted) caveats.Add("insufficient_personal_baseline");
        if (u.DeptSize < _options.MinDeptSize) caveats.Add("small_department");
        return caveats;
    }

    /// <summary>
    /// The department facts, read straight off the user rather than off the matrix — under
    /// EnableModelCorrections these columns are no longer in the matrix at all, but they are still
    /// the comparison basis the analyst needs to see.
    /// </summary>
    private static Dictionary<string, double> TeamContextOf(UserFeatures u) => new()
    {
        ["dept_size"] = u.DeptSize,
        ["dept_mean_incident_count"] = Math.Round(u.DeptMeanIncidentCount, 4),
        ["dept_mean_off_hours_ratio"] = Math.Round(u.DeptMeanOffHoursRatio, 4),
        ["dept_mean_allowed_ratio"] = Math.Round(u.DeptMeanAllowedRatio, 4),
        ["dept_mean_high_sev_ratio"] = Math.Round(u.DeptMeanHighSevRatio, 4),
        ["dept_mean_tx_size"] = Math.Round(u.DeptMeanTxSize, 4)
    };

    private static string GetFeatureGroup(string name)
    {
        if (name.Contains("self_z") || name.Contains("self_outlier")) return "self";
        if (name.EndsWith("_peer_z")) return "peer";
        if (name.StartsWith("dept_")) return "dept_ctx";
        return "raw";
    }

    private sealed record UserAttribution(
        List<FeatureContributionDto> TopFeatures,
        Dictionary<string, double> GroupBreakdown,
        UserExplanationDto Explanation)
    {
        public static UserAttribution Empty(int cohortSize) => new(
            new List<FeatureContributionDto>(),
            new Dictionary<string, double>(),
            new UserExplanationDto
            {
                ConfidenceLevel = "low",
                UnexplainedSharePct = 100,
                CohortRank = 1,
                CohortSize = cohortSize,
                Caveats = new List<string> { "single_user_cohort" }
            });
    }

    /// <summary>Per-column cohort statistics in raw units, for evidence and tail percentages.</summary>
    private sealed class ColumnStats
    {
        private readonly double[][] _sorted;
        public double[] Median { get; }

        private ColumnStats(double[][] sorted, double[] median)
        {
            _sorted = sorted;
            Median = median;
        }

        public static ColumnStats Build(double[][] rawMatrix, int d)
        {
            var sorted = new double[d][];
            var median = new double[d];

            for (int f = 0; f < d; f++)
            {
                var col = new double[rawMatrix.Length];
                for (int r = 0; r < rawMatrix.Length; r++) col[r] = rawMatrix[r][f];
                Array.Sort(col);
                sorted[f] = col;
                median[f] = col.Length == 0
                    ? 0
                    : col.Length % 2 == 1
                        ? col[col.Length / 2]
                        : (col[col.Length / 2 - 1] + col[col.Length / 2]) / 2.0;
            }

            return new ColumnStats(sorted, median);
        }

        /// <summary>Share of the cohort further out than this value, in the same direction.</summary>
        public double TailPct(int feature, double value, string deviation)
        {
            var col = _sorted[feature];
            if (col.Length == 0) return 0;

            int more = deviation == "below"
                ? LowerBound(col, value)
                : col.Length - UpperBound(col, value);

            return (double)more / col.Length * 100.0;
        }

        private static int LowerBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sorted[mid] < value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private static int UpperBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sorted[mid] <= value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double StdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0;
        var mean = list.Average();
        return Math.Sqrt(list.Sum(v => Math.Pow(v - mean, 2)) / (list.Count - 1));
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private class EnrichedIncident
    {
        public int Id { get; }
        public string Source { get; }
        public string Department { get; }
        public string Channel { get; }
        public string Destination { get; }
        public string Policy { get; }
        public DateTime Timestamp { get; }
        public double Severity { get; }
        public double ActionRisk { get; }
        public double TxSize { get; }
        public double MaxMatches { get; }
        public double ChannelRarity { get; set; }
        public int Hour { get; }
        public bool IsOffHours { get; }
        public bool IsWeekend { get; }
        public bool IsNight { get; }
        public Dictionary<string, double> SelfZ { get; } = new();
        public int AnySelfOutlier { get; set; }
        public int StrongSelfOutlier { get; set; }

        private static readonly Dictionary<string, double> ActionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["blocked"] = 1, ["block"] = 1, ["denied"] = 1, ["deny"] = 1,
            ["quarantined"] = 2, ["quarantine"] = 2,
            ["notified"] = 3, ["notify"] = 3, ["alerted"] = 3, ["alert"] = 3,
            ["allowed"] = 4, ["allow"] = 4, ["monitor"] = 4, ["monitored"] = 4, ["audit"] = 4,
            ["authorized"] = 4, ["released"] = 4
        };

        public EnrichedIncident(Incident inc)
        {
            Id = inc.Id;
            Source = inc.UserEmail ?? inc.LoginName ?? "unknown";
            Department = inc.Department ?? inc.Team ?? "Unknown";
            Channel = inc.Channel ?? "unknown";
            Destination = inc.Destination ?? "unknown";
            Policy = inc.Policy ?? inc.RuleName ?? "unknown";
            Timestamp = inc.Timestamp;
            Severity = Math.Min(5, Math.Max(1, inc.Severity));
            ActionRisk = ActionMap.GetValueOrDefault(inc.Action ?? "", 3);
            TxSize = Math.Max(0, inc.DataSensitivity);
            MaxMatches = Math.Max(0, inc.MaxMatches);
            Hour = inc.Timestamp.Hour;
            IsOffHours = Hour < 8 || Hour > 19;
            IsWeekend = (int)inc.Timestamp.DayOfWeek >= 5;
            IsNight = Hour < 6 || Hour >= 22;
        }
    }

    private class UserStats
    {
        private readonly Dictionary<string, (double Centre, double Scale)> _stats = new();
        private readonly bool _robust;
        private readonly double _maxZ;

        public bool HasBaseline { get; }

        public static UserStats Empty { get; } = new(new List<EnrichedIncident>(), 1, false, 8.0);

        /// <summary>Integer-valued columns need a scale floor or a single repeated value blows up.</summary>
        private static readonly Dictionary<string, double> ScaleFloor = new()
        {
            ["severity"] = 0.5,
            ["action_risk"] = 0.5,
            ["hour"] = 0.5
        };

        public UserStats(List<EnrichedIncident> incs, int minCount, bool robust, double maxZ)
        {
            _robust = robust;
            _maxZ = maxZ;
            if (incs.Count < minCount) return;
            HasBaseline = true;

            AddStat("tx_size", incs.Select(e => e.TxSize));
            AddStat("max_matches", incs.Select(e => e.MaxMatches));
            AddStat("channel_rarity", incs.Select(e => e.ChannelRarity));
            AddStat("severity", incs.Select(e => e.Severity));
            AddStat("action_risk", incs.Select(e => e.ActionRisk));
            AddStat("hour", incs.Select(e => (double)e.Hour));
        }

        private void AddStat(string name, IEnumerable<double> vals)
        {
            var list = vals.ToList();

            if (!_robust)
            {
                _stats[name] = (list.Average(), StdDev(list));
                return;
            }

            // Median / MAD: one wild incident in the baseline no longer sets the scale for the rest.
            var median = Median(list);
            var mad = Median(list.Select(v => Math.Abs(v - median)).ToList());
            var scale = Math.Max(mad / 0.6745, ScaleFloor.GetValueOrDefault(name, 0.0));
            _stats[name] = (median, scale);
        }

        public double SelfZ(double value, string col)
        {
            if (!_stats.TryGetValue(col, out var s)) return 0;

            if (s.Scale < 1e-9)
            {
                // A baseline with no spread at all. The old code returned a hard ±5, which is
                // indistinguishable from a real 5σ reading; bound it and keep it clearly smaller
                // than anything a genuine deviation can reach.
                var difference = value - s.Centre;
                var tolerance = Math.Max(Math.Abs(s.Centre) * 0.05, 1e-6);
                if (Math.Abs(difference) <= tolerance) return 0;
                return Math.Sign(difference) * (_robust ? 3.0 : 5.0);
            }

            var z = (value - s.Centre) / s.Scale;
            return _robust ? Math.Clamp(z, -_maxZ, _maxZ) : z;
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        private static double StdDev(IEnumerable<double> vals)
        {
            var l = vals.ToList();
            if (l.Count <= 1) return 0;
            var m = l.Average();
            return Math.Sqrt(l.Sum(v => Math.Pow(v - m, 2)) / (l.Count - 1));
        }
    }

    private class UserFeatures
    {
        public string Source { get; set; } = "";
        public string Department { get; set; } = "Unknown";
        public int IncidentCount { get; set; }
        public int BaselineIncidentCount { get; set; }
        public int UniquePolicies { get; set; }
        public int UniqueChannels { get; set; }
        public int UniqueDestinations { get; set; }
        public double MeanSeverity { get; set; }
        public double MaxSeverity { get; set; }
        public int HighSevCount { get; set; }
        public double HighSevRatio { get; set; }
        public double MeanActionRisk { get; set; }
        public int AllowedCount { get; set; }
        public double AllowedRatio { get; set; }
        public double TotalTxSize { get; set; }
        public double MeanTxSize { get; set; }
        public double MaxTxSize { get; set; }
        public double StdTxSize { get; set; }
        public double TotalMaxMatches { get; set; }
        public double MeanMaxMatches { get; set; }
        public double MaxMaxMatches { get; set; }
        public int OffHoursCount { get; set; }
        public double OffHoursRatio { get; set; }
        public double WeekendRatio { get; set; }
        public double NightRatio { get; set; }
        public double MeanChannelRarity { get; set; }
        public double MaxChannelRarity { get; set; }
        public double IncidentsPerDay { get; set; }
        // Dept context
        public int DeptSize { get; set; }
        public double DeptMeanIncidentCount { get; set; }
        public double DeptMeanOffHoursRatio { get; set; }
        public double DeptMeanAllowedRatio { get; set; }
        public double DeptMeanHighSevRatio { get; set; }
        public double DeptMeanTxSize { get; set; }
        // Self baseline
        public Dictionary<string, double> SelfAgg { get; set; } = new();
        public bool HasSelfBaseline { get; set; }
        public int SelfOutlierCount { get; set; }
        public int StrongSelfOutlierCount { get; set; }
        public double SelfOutlierRatio { get; set; }
        public double StrongSelfOutlierRatio { get; set; }
        // Peer z-scores
        public Dictionary<string, double> PeerZ { get; set; } = new();
        // Which window incidents drove each behaviour family (evidence drill-down)
        public Dictionary<string, FamilyEvidence> FamilyEvidence { get; set; } = new();
    }
}
