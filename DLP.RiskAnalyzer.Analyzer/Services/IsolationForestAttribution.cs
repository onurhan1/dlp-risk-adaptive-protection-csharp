namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// A single isolation tree. Leaves carry <see cref="Size"/>; internal nodes carry the
/// split (<see cref="Feature"/>, <see cref="Split"/>).
/// </summary>
internal sealed class IsolationTree
{
    public bool IsLeaf { get; set; }
    public int Size { get; set; }
    public int Feature { get; set; }
    public double Split { get; set; }
    public IsolationTree? Left { get; set; }
    public IsolationTree? Right { get; set; }

    /// <summary>Expected path length of an unsuccessful BST search over n points.</summary>
    public static double AvgPathLength(int n) =>
        n <= 1 ? 0 : 2.0 * (Math.Log(n - 1) + 0.5772156649) - (2.0 * (n - 1) / n);

    public static double PathLength(double[] x, IsolationTree node, int depth)
    {
        while (!node.IsLeaf)
        {
            node = x[node.Feature] < node.Split ? node.Left! : node.Right!;
            depth++;
        }
        return depth + AvgPathLength(node.Size);
    }
}

/// <summary>
/// Counterfactual-at-Cohort-Mean Ablation (CCMA) — model-faithful attribution for a fitted
/// isolation forest.
///
/// For a feature f, the contribution is the drop in anomaly score when the user's value for f
/// is replaced by the cohort mean and the point is re-scored through the <em>same fitted
/// trees</em>: <c>Δ_f = s(x) − s(x | x_f := 0)</c>. The feature matrix is standardized, so 0 is
/// literally the cohort mean and the number reads as "if he had been ordinary on f, his score
/// would have been Δ lower".
///
/// <para><b>Why this is cheap.</b> A decision path depends only on the features tested along it.
/// If f is not tested on x's path in tree t, substituting any value for x_f leaves that path —
/// and its length — bit-identically unchanged. At most <c>h = ceil(log2(maxSamples))</c> distinct
/// features are tested per path out of d, so single-feature ablation costs <c>n·T·h²</c> rather
/// than <c>n·d·T·h</c>. The pruning is exact, not an approximation.</para>
///
/// <para>Positive Δ means the feature pushed the user toward isolation. Because an isolation
/// forest isolates <em>both</em> tails, a value far <em>below</em> the cohort also yields a
/// positive Δ — no polarity table is needed to get the model direction right.</para>
/// </summary>
internal sealed class ForestAttributor
{
    private readonly IsolationTree[] _trees;
    private readonly double _c;
    private readonly int _d;

    public ForestAttributor(IsolationTree[] trees, double c, int featureCount)
    {
        _trees = trees;
        _c = c;
        _d = featureCount;
    }

    /// <summary>Anomaly score from a summed path length. Higher = more anomalous.</summary>
    public double Score(double sumPath) => Math.Pow(2.0, -(sumPath / _trees.Length) / _c);

    /// <summary>
    /// Walks x's path, marking every feature tested along it. Returns the path length.
    /// Only marked features can have a nonzero delta.
    /// </summary>
    private static double CollectPath(double[] x, IsolationTree node, bool[] onPath)
    {
        int depth = 0;
        while (!node.IsLeaf)
        {
            onPath[node.Feature] = true;
            node = x[node.Feature] < node.Split ? node.Left! : node.Right!;
            depth++;
        }
        return depth + IsolationTree.AvgPathLength(node.Size);
    }

    /// <summary>
    /// Per-feature marginal contribution. <paramref name="baseScore"/> receives the user's
    /// unmodified anomaly score, recomputed here from the same trees.
    /// </summary>
    public double[] AttributeSingles(double[] x, out double baseScore)
    {
        var sumAblated = new double[_d];
        var onPath = new bool[_d];
        var anyOnPath = new bool[_d];
        var probe = (double[])x.Clone();
        double sumPath = 0.0;

        foreach (var tree in _trees)
        {
            Array.Clear(onPath);
            var h0 = CollectPath(x, tree, onPath);
            sumPath += h0;

            for (int f = 0; f < _d; f++)
            {
                if (!onPath[f])
                {
                    sumAblated[f] += h0;   // provably identical — no traversal needed
                    continue;
                }

                anyOnPath[f] = true;
                var keep = probe[f];
                probe[f] = 0.0;            // cohort mean in standardized space
                sumAblated[f] += IsolationTree.PathLength(probe, tree, 0);
                probe[f] = keep;
            }
        }

        baseScore = Score(sumPath);

        var delta = new double[_d];
        for (int f = 0; f < _d; f++)
            delta[f] = anyOnPath[f] ? baseScore - Score(sumAblated[f]) : 0.0;
        return delta;
    }

    /// <summary>
    /// Joint ablation of a correlated block. Deliberately NOT the sum of its members' singles —
    /// that sum is exactly the double counting this removes.
    /// </summary>
    public double AttributeGroup(double[] x, IReadOnlyList<int> members, double baseScore)
    {
        var probe = (double[])x.Clone();
        foreach (var f in members) probe[f] = 0.0;

        double sum = 0.0;
        foreach (var tree in _trees) sum += IsolationTree.PathLength(probe, tree, 0);
        return baseScore - Score(sum);
    }

    /// <summary>
    /// Greedy sequential ablation over the cells the UI will display, in rank order. Guarantees
    /// exact additivity for the displayed set:
    /// <c>Σ seq == s(x) − s(x | all displayed cells ablated)</c>.
    /// The gap to the fully-ablated score is returned as <paramref name="residual"/> and surfaced
    /// as "unexplained share" rather than being renormalized away.
    /// </summary>
    public double[] AttributeSequential(
        double[] x,
        IReadOnlyList<IReadOnlyList<int>> cellsInRankOrder,
        double baseScore,
        out double residual)
    {
        var probe = (double[])x.Clone();
        var seq = new double[cellsInRankOrder.Count];
        var prev = baseScore;

        for (int k = 0; k < cellsInRankOrder.Count; k++)
        {
            foreach (var f in cellsInRankOrder[k]) probe[f] = 0.0;

            double sum = 0.0;
            foreach (var tree in _trees) sum += IsolationTree.PathLength(probe, tree, 0);

            var now = Score(sum);
            seq[k] = prev - now;
            prev = now;
        }

        residual = prev - FullyAblatedScore();
        return seq;
    }

    /// <summary>Score of the cohort centroid — every feature at its mean.</summary>
    public double FullyAblatedScore()
    {
        var centroid = new double[_d];
        double sum = 0.0;
        foreach (var tree in _trees) sum += IsolationTree.PathLength(centroid, tree, 0);
        return Score(sum);
    }
}
