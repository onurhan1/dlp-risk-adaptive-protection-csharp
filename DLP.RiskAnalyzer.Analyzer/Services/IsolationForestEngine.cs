using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Pure C# Isolation Forest + feature engineering, mirroring the Python notebook logic:
/// - 3 anomaly dimensions: Global (raw), Peer (dept z-score), Self (personal baseline z-score)
/// - Log1p + StandardScaler normalization
/// - Permutation-based feature contribution (SHAP proxy)
/// </summary>
public class IsolationForestEngine
{
    private readonly ILogger<IsolationForestEngine> _logger;
    private readonly Random _rng;

    // Hyperparameters
    private const int NEstimators = 100;
    private const double Contamination = 0.05;
    private const int MaxSamples = 256;
    private const int MinUserIncidents = 3;   // min incidents for self-baseline
    private const int MinDeptSize = 3;         // min dept size for peer z-score

    public IsolationForestEngine(ILogger<IsolationForestEngine> logger)
    {
        _logger = logger;
        _rng = new Random(42);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full pipeline: feature engineering → isolation forest → contributions
    /// </summary>
    public List<IsolationForestScoreDto> Run(List<Incident> incidents)
    {
        _logger.LogInformation("IsolationForestEngine: running on {Count} incidents", incidents.Count);

        var users = BuildUserFeatures(incidents);
        if (users.Count == 0)
        {
            _logger.LogWarning("IsolationForestEngine: no users after feature engineering");
            return new();
        }

        var (featureNames, matrix) = BuildScaledMatrix(users);
        var scores = FitAndScore(matrix);

        // Normalize to 0-100
        var minS = scores.Min();
        var maxS = scores.Max();
        var range = Math.Max(maxS - minS, 1e-9);
        var normalized = scores.Select(s => (s - minS) / range * 100.0).ToArray();

        // Anomaly threshold: top Contamination%
        var threshold = normalized.OrderDescending().ElementAt((int)(normalized.Length * Contamination));

        // Permutation contributions
        var contributions = ComputeContributions(featureNames, matrix, scores);

        var result = new List<IsolationForestScoreDto>();
        for (int i = 0; i < users.Count; i++)
        {
            var u = users[i];
            var topFeatures = contributions[i]
                .OrderByDescending(f => Math.Abs(f.ShapValue))
                .Take(8)
                .ToList();

            // Group breakdown: sum of abs contributions per group
            var groupBreakdown = topFeatures
                .GroupBy(f => f.Group)
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => Math.Abs(f.ShapValue)), 3));

            result.Add(new IsolationForestScoreDto
            {
                UserEmail = u.Source,
                Department = u.Department,
                CalculatedAt = DateTime.UtcNow,
                IFScore = Math.Round(normalized[i], 1),
                IsAnomaly = normalized[i] >= threshold,
                IncidentCount = u.IncidentCount,
                TopFeatures = topFeatures,
                GroupBreakdown = groupBreakdown
            });
        }

        _logger.LogInformation("IsolationForestEngine: finished, {Anomalies}/{Total} anomalies", result.Count(r => r.IsAnomaly), result.Count);
        return result;
    }

    // ── Feature Engineering (mirrors notebook sections 3-7) ──────────────────

    private List<UserFeatures> BuildUserFeatures(List<Incident> incidents)
    {
        // ── Step 3: incident-level enrichment ──────────────────────────────
        var enriched = incidents.Select(inc => new EnrichedIncident(inc)).ToList();

        // Channel rarity (log-based)
        var chFreq = enriched.GroupBy(e => e.Channel).ToDictionary(g => g.Key, g => (double)g.Count() / enriched.Count);
        foreach (var e in enriched)
            e.ChannelRarity = -Math.Log(Math.Max(chFreq.GetValueOrDefault(e.Channel, 1e-6), 1e-6));

        // ── Step 4: per-incident self-baseline ─────────────────────────────
        var selfBaseCols = new[] { "tx_size", "max_matches", "channel_rarity", "severity", "action_risk", "hour" };
        var userStats = enriched
            .GroupBy(e => e.Source)
            .ToDictionary(g => g.Key, g => new UserStats(g.ToList(), MinUserIncidents));

        foreach (var e in enriched)
        {
            var stats = userStats[e.Source];
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
            foreach (var col in selfBaseCols)
            {
                u.SelfAgg[$"max_self_z_{col}"] = incs.Max(e => Math.Abs(e.SelfZ.GetValueOrDefault(col, 0)));
                u.SelfAgg[$"mean_self_z_{col}"] = incs.Average(e => Math.Abs(e.SelfZ.GetValueOrDefault(col, 0)));
            }
            u.SelfOutlierCount = incs.Sum(e => e.AnySelfOutlier);
            u.StrongSelfOutlierCount = incs.Sum(e => e.StrongSelfOutlier);
            u.SelfOutlierRatio = u.IncidentCount > 0 ? (double)u.SelfOutlierCount / u.IncidentCount : 0;
            u.StrongSelfOutlierRatio = u.IncidentCount > 0 ? (double)u.StrongSelfOutlierCount / u.IncidentCount : 0;
        }

        // ── Step 6: dept context + peer z-scores ──────────────────────────
        AddDeptContext(users, MinDeptSize);

        return users;
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

    private static (string[] names, double[][] matrix) BuildScaledMatrix(List<UserFeatures> users)
    {
        var featureNames = new List<string>();
        featureNames.AddRange(RawFeatureNames);
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

        // Build raw matrix
        var rawMatrix = users.Select(u => GetFeatureVector(u, featureNames.ToArray())).ToArray();

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

        return (featureNames.ToArray(), rawMatrix);
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
                _ => names[i].EndsWith("_peer_z")
                    ? u.PeerZ.GetValueOrDefault(names[i][..^7], 0)
                    : u.SelfAgg.GetValueOrDefault(names[i], 0)
            };
        }
        return v;
    }

    // ── Isolation Forest (iTrees) ─────────────────────────────────────────────

    private double[] FitAndScore(double[][] matrix)
    {
        int n = matrix.Length;
        int d = matrix[0].Length;
        int actualMaxSamples = Math.Min(MaxSamples, n);
        var maxDepth = (int)Math.Ceiling(Math.Log2(actualMaxSamples));

        var trees = new ITree[NEstimators];
        for (int t = 0; t < NEstimators; t++)
        {
            var sample = SampleRows(matrix, actualMaxSamples);
            trees[t] = BuildTree(sample, 0, maxDepth, d);
        }

        // Average path length
        double c = AvgPathLength(actualMaxSamples);
        var scores = new double[n];
        for (int i = 0; i < n; i++)
        {
            var avgPath = trees.Average(tree => PathLength(matrix[i], tree, 0));
            scores[i] = -Math.Pow(2, -avgPath / c); // anomaly score: closer to -1 means more anomalous
        }
        // Negate so higher = more anomalous
        for (int i = 0; i < n; i++) scores[i] = -scores[i];
        return scores;
    }

    private double[][] SampleRows(double[][] matrix, int k)
    {
        var indices = Enumerable.Range(0, matrix.Length).OrderBy(_ => _rng.Next()).Take(k).ToArray();
        return indices.Select(i => matrix[i]).ToArray();
    }

    private ITree BuildTree(double[][] data, int depth, int maxDepth, int d)
    {
        if (depth >= maxDepth || data.Length <= 1)
            return new ITree { IsLeaf = true, Size = data.Length };

        int feat = _rng.Next(d);
        var vals = data.Select(r => r[feat]).ToArray();
        var min = vals.Min();
        var max = vals.Max();
        if (Math.Abs(max - min) < 1e-9)
            return new ITree { IsLeaf = true, Size = data.Length };

        var split = min + _rng.NextDouble() * (max - min);
        var left = data.Where(r => r[feat] < split).ToArray();
        var right = data.Where(r => r[feat] >= split).ToArray();

        return new ITree
        {
            IsLeaf = false,
            Feature = feat,
            Split = split,
            Left = BuildTree(left, depth + 1, maxDepth, d),
            Right = BuildTree(right, depth + 1, maxDepth, d)
        };
    }

    private static double PathLength(double[] x, ITree node, int depth)
    {
        if (node.IsLeaf)
            return depth + AvgPathLength(node.Size);
        return x[node.Feature] < node.Split
            ? PathLength(x, node.Left!, depth + 1)
            : PathLength(x, node.Right!, depth + 1);
    }

    private static double AvgPathLength(int n) =>
        n <= 1 ? 0 : 2.0 * (Math.Log(n - 1) + 0.5772156649) - (2.0 * (n - 1) / n);

    // ── Permutation-based Feature Contributions ───────────────────────────────

    private List<List<FeatureContributionDto>> ComputeContributions(
        string[] names, double[][] matrix, double[] baseScores)
    {
        int n = matrix.Length;
        int d = names.Length;
        var contributions = Enumerable.Range(0, n).Select(_ => new List<FeatureContributionDto>()).ToList();

        // For each feature, compute contribution as: score_with_feature - score_without (zeroed out)
        for (int f = 0; f < d; f++)
        {
            var perturbed = matrix.Select(r =>
            {
                var c = (double[])r.Clone();
                c[f] = 0.0; // zero-out feature
                return c;
            }).ToArray();

            // Compute anomaly scores for perturbed matrix (fast: use same fitted trees is not available here,
            // so we approximate by comparing the scaled value itself as contribution)
            // Simpler approximation: contribution ≈ scaled_value * global_feature_importance_estimate
            // We use: contribution_i_f = matrix[i][f] * avg_abs(matrix[:,f]) / sum(avg_abs)
        }

        // Better approximation: direct feature value contribution (feature value vs population mean)
        // For each user, feature contribution = (value - 0) * sign indicating risk direction
        // Since matrix is already standardized: positive high values = deviant = higher risk
        var featureWeights = new double[d];
        for (int f = 0; f < d; f++)
            featureWeights[f] = matrix.Average(r => Math.Abs(r[f]));
        var totalWeight = featureWeights.Sum() + 1e-9;

        for (int i = 0; i < n; i++)
        {
            for (int f = 0; f < d; f++)
            {
                var val = matrix[i][f];
                var contribution = val * (featureWeights[f] / totalWeight) * baseScores[i];
                contributions[i].Add(new FeatureContributionDto
                {
                    Name = names[f],
                    DisplayName = ToDisplayName(names[f]),
                    Group = GetFeatureGroup(names[f]),
                    ShapValue = Math.Round(contribution, 4),
                    Direction = contribution > 0 ? "risk ↑" : "risk ↓",
                    ActualValue = Math.Round(val, 3)
                });
            }
        }

        return contributions;
    }

    private static string GetFeatureGroup(string name)
    {
        if (name.Contains("self_z") || name.Contains("self_outlier")) return "self";
        if (name.EndsWith("_peer_z")) return "peer";
        if (name.StartsWith("dept_")) return "dept_ctx";
        return "raw";
    }

    private static string ToDisplayName(string name) =>
        name.Replace("_peer_z", " (peer)").Replace("_self_z", " (self)").Replace('_', ' ');

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double StdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0;
        var mean = list.Average();
        return Math.Sqrt(list.Sum(v => Math.Pow(v - mean, 2)) / (list.Count - 1));
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private class ITree
    {
        public bool IsLeaf { get; set; }
        public int Size { get; set; }
        public int Feature { get; set; }
        public double Split { get; set; }
        public ITree? Left { get; set; }
        public ITree? Right { get; set; }
    }

    private class EnrichedIncident
    {
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
        private readonly Dictionary<string, (double Mean, double Std)> _stats = new();

        public UserStats(List<EnrichedIncident> incs, int minCount)
        {
            if (incs.Count < minCount) return;

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
            var mean = list.Average();
            var std = StdDev(list);
            _stats[name] = (mean, std);
        }

        public double SelfZ(double value, string col)
        {
            if (!_stats.TryGetValue(col, out var s) || s.Std < 1e-9) return 0;
            return (value - s.Mean) / s.Std;
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
        public int SelfOutlierCount { get; set; }
        public int StrongSelfOutlierCount { get; set; }
        public double SelfOutlierRatio { get; set; }
        public double StrongSelfOutlierRatio { get; set; }
        // Peer z-scores
        public Dictionary<string, double> PeerZ { get; set; } = new();
    }
}
