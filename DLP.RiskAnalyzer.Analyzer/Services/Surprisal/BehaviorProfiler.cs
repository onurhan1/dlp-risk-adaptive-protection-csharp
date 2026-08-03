namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

internal sealed record UserProfile(string User, string Department, int EventCount, double[] Vector);

internal sealed record BehaviorCluster(
    string Id,
    int UserCount,
    int EventCount,
    double[] Centroid,
    IReadOnlyList<string> Users,
    IReadOnlyList<(string Label, double Share)> TopTraits);

internal sealed record ClusteringResult(
    IReadOnlyDictionary<string, string> ClusterOf,
    IReadOnlyList<BehaviorCluster> Clusters,
    IReadOnlyList<string> Dimensions,
    double Silhouette,
    int ChosenK);

/// <summary>
/// Groups users by how they actually behave rather than by where the org chart puts them.
///
/// This exists because the org chart does not work as a peer group here: in production the median
/// department has a handful of users, so a department-based z-score has almost no cohort to compare
/// against and quietly degenerates into a population score. A behavioural cluster puts payroll and
/// HR together regardless of department, and puts a developer who behaves like a DBA with the DBAs.
///
/// The profile is a concatenation of normalised histograms — channel mix, policy mix, destination
/// mix, timing mix — so two users are close when they use the same tools on the same kinds of data
/// at the same times.
/// </summary>
internal sealed class BehaviorProfiler
{
    private readonly SurprisalOptions _options;

    public BehaviorProfiler(SurprisalOptions options) => _options = options;

    public ClusteringResult Build(IReadOnlyList<EventToken> baseline)
    {
        var (dimensions, blocks) = BuildDimensions(baseline);
        var profiles = BuildProfiles(baseline, dimensions, blocks);

        if (profiles.Count == 0)
            return new ClusteringResult(new Dictionary<string, string>(), Array.Empty<BehaviorCluster>(),
                dimensions, 0, 0);

        // Only users with enough history shape the centroids; sparse users are assigned afterwards.
        var trainable = profiles.Where(p => p.EventCount >= _options.MinEventsForClustering).ToList();
        if (trainable.Count < _options.MinClusterCount * 2)
            trainable = profiles.ToList();

        var best = ChooseK(trainable, dimensions.Count);

        var clusterOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in profiles)
            clusterOf[p.User] = "c" + Nearest(p.Vector, best.Centroids);

        var clusters = new List<BehaviorCluster>();
        for (int k = 0; k < best.Centroids.Count; k++)
        {
            var id = "c" + k;
            var members = profiles.Where(p => clusterOf[p.User] == id).ToList();
            clusters.Add(new BehaviorCluster(
                id,
                members.Count,
                members.Sum(m => m.EventCount),
                best.Centroids[k],
                members.Select(m => m.User).OrderBy(u => u, StringComparer.Ordinal).ToList(),
                DescribeCentroid(best.Centroids[k], dimensions)));
        }

        return new ClusteringResult(clusterOf, clusters, dimensions, best.Silhouette, best.Centroids.Count);
    }

    // ── Profile space ────────────────────────────────────────────────────────

    /// <summary>
    /// One dimension per (field, value) for the fields that describe *how someone works*. Match tier
    /// and action are deliberately excluded — they describe the incident, not the person.
    /// </summary>
    private static (List<string> Dimensions, List<(int Start, int Length)> Blocks) BuildDimensions(
        IReadOnlyList<EventToken> events)
    {
        var fields = new[]
        {
            EventToken.Fields.Channel,
            EventToken.Fields.Policy,
            EventToken.Fields.DestinationClass,
            EventToken.Fields.TimeBucket
        };

        var dimensions = new List<string>();
        var blocks = new List<(int, int)>();

        foreach (var field in fields)
        {
            var values = events.Select(e => e.Value(field))
                .GroupBy(v => v, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key)
                // A long tail of one-off policies would swamp the distance metric.
                .Take(field == EventToken.Fields.Policy ? 40 : 20)
                .ToList();

            blocks.Add((dimensions.Count, values.Count));
            dimensions.AddRange(values.Select(v => $"{field}={v}"));
        }

        return (dimensions, blocks);
    }

    private List<UserProfile> BuildProfiles(
        IReadOnlyList<EventToken> events, List<string> dimensions, List<(int Start, int Length)> blocks)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < dimensions.Count; i++) index[dimensions[i]] = i;

        var byUser = events.GroupBy(e => e.User, StringComparer.OrdinalIgnoreCase);
        var profiles = new List<UserProfile>();

        foreach (var g in byUser)
        {
            var vec = new double[dimensions.Count];
            foreach (var e in g)
                foreach (var field in new[] { EventToken.Fields.Channel, EventToken.Fields.Policy,
                                              EventToken.Fields.DestinationClass, EventToken.Fields.TimeBucket })
                    if (index.TryGetValue($"{field}={e.Value(field)}", out var i))
                        vec[i] += 1;

            // Normalise each field block independently, so a user is described by their *mix*
            // within each field rather than by how many events they happen to generate.
            foreach (var (start, length) in blocks)
            {
                double sum = 0;
                for (int i = start; i < start + length; i++) sum += vec[i];
                if (sum <= 0) continue;
                for (int i = start; i < start + length; i++) vec[i] /= sum;
            }

            profiles.Add(new UserProfile(g.Key, g.First().Department, g.Count(), vec));
        }

        return profiles;
    }

    // ── k-means ──────────────────────────────────────────────────────────────

    private sealed record KResult(List<double[]> Centroids, double Silhouette);

    private KResult ChooseK(List<UserProfile> profiles, int dim)
    {
        var maxK = Math.Min(_options.MaxClusterCount, Math.Max(_options.MinClusterCount, profiles.Count / 5));
        var minK = Math.Min(_options.MinClusterCount, maxK);

        KResult? best = null;
        for (int k = minK; k <= maxK; k++)
        {
            var centroids = KMeans(profiles, k, dim);
            var silhouette = Silhouette(profiles, centroids);
            if (best is null || silhouette > best.Silhouette)
                best = new KResult(centroids, silhouette);
        }

        return best ?? new KResult(new List<double[]> { new double[dim] }, 0);
    }

    private List<double[]> KMeans(List<UserProfile> profiles, int k, int dim)
    {
        var rng = new Random(_options.ClusteringSeed + k);
        var centroids = KMeansPlusPlusInit(profiles, k, dim, rng);
        var assignment = new int[profiles.Count];

        for (int iter = 0; iter < _options.ClusteringMaxIterations; iter++)
        {
            var changed = false;
            for (int i = 0; i < profiles.Count; i++)
            {
                var nearest = Nearest(profiles[i].Vector, centroids);
                if (nearest != assignment[i]) { assignment[i] = nearest; changed = true; }
            }

            var sums = new double[k][];
            var counts = new int[k];
            for (int c = 0; c < k; c++) sums[c] = new double[dim];

            for (int i = 0; i < profiles.Count; i++)
            {
                var c = assignment[i];
                counts[c]++;
                for (int d = 0; d < dim; d++) sums[c][d] += profiles[i].Vector[d];
            }

            for (int c = 0; c < k; c++)
                if (counts[c] > 0)
                    for (int d = 0; d < dim; d++) centroids[c][d] = sums[c][d] / counts[c];

            if (!changed) break;
        }

        return centroids;
    }

    private static List<double[]> KMeansPlusPlusInit(List<UserProfile> profiles, int k, int dim, Random rng)
    {
        var centroids = new List<double[]> { (double[])profiles[rng.Next(profiles.Count)].Vector.Clone() };

        while (centroids.Count < k)
        {
            var distances = profiles.Select(p => centroids.Min(c => SquaredDistance(p.Vector, c))).ToArray();
            var total = distances.Sum();

            if (total <= 1e-12)
            {
                centroids.Add((double[])profiles[rng.Next(profiles.Count)].Vector.Clone());
                continue;
            }

            var target = rng.NextDouble() * total;
            double acc = 0;
            int chosen = profiles.Count - 1;
            for (int i = 0; i < distances.Length; i++)
            {
                acc += distances[i];
                if (acc >= target) { chosen = i; break; }
            }

            centroids.Add((double[])profiles[chosen].Vector.Clone());
        }

        return centroids;
    }

    private static int Nearest(double[] v, IReadOnlyList<double[]> centroids)
    {
        int best = 0;
        double bestDist = double.MaxValue;
        for (int c = 0; c < centroids.Count; c++)
        {
            var d = SquaredDistance(v, centroids[c]);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    private static double SquaredDistance(double[] a, double[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++) { var d = a[i] - b[i]; sum += d * d; }
        return sum;
    }

    /// <summary>
    /// Centroid-based silhouette approximation: (nearest-other − own) / max(...). Cheap enough to
    /// sweep k over the whole range, unlike the pairwise definition at O(n²).
    /// </summary>
    private static double Silhouette(List<UserProfile> profiles, List<double[]> centroids)
    {
        if (centroids.Count < 2) return 0;
        double sum = 0;

        foreach (var p in profiles)
        {
            double own = double.MaxValue, other = double.MaxValue;
            foreach (var c in centroids)
            {
                var d = Math.Sqrt(SquaredDistance(p.Vector, c));
                if (d < own) { other = own; own = d; }
                else if (d < other) other = d;
            }
            if (other == double.MaxValue || Math.Max(own, other) <= 1e-12) continue;
            sum += (other - own) / Math.Max(own, other);
        }

        return sum / profiles.Count;
    }

    private static List<(string Label, double Share)> DescribeCentroid(double[] centroid, List<string> dimensions) =>
        centroid
            .Select((v, i) => (Label: dimensions[i], Share: v))
            .Where(x => x.Share > 0.05)
            .OrderByDescending(x => x.Share)
            .Take(6)
            .ToList();
}
