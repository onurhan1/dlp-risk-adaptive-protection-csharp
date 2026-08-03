namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Tuning knobs for <see cref="IsolationForestEngine"/>. The defaults are the production values;
/// the type exists so tests can vary the forest (seed, tree count, split space) and assert that
/// the explanation follows it.
/// </summary>
public sealed record IsolationForestOptions
{
    public static IsolationForestOptions Default { get; } = new();

    /// <summary>Number of isolation trees.</summary>
    public int NEstimators { get; init; } = 100;

    /// <summary>Share of the cohort flagged as the review queue each run.</summary>
    public double Contamination { get; init; } = 0.05;

    /// <summary>Rows sampled per tree.</summary>
    public int MaxSamples { get; init; } = 256;

    /// <summary>Minimum prior incidents before a personal (self) baseline is trusted.</summary>
    public int MinUserIncidents { get; init; } = 3;

    /// <summary>Minimum department size before peer z-scores use the department instead of the population.</summary>
    public int MinDeptSize { get; init; } = 3;

    /// <summary>Base seed. Each tree derives its own <see cref="Random"/> from this and its index,
    /// so the forest is identical regardless of build order or thread count.</summary>
    public int Seed { get; init; } = 42;

    /// <summary>Worker count for the attribution pass. -1 lets the runtime decide.</summary>
    public int MaxDegreeOfParallelism { get; init; } = -1;

    /// <summary>
    /// Restricts tree splits to these feature indices. Null = every feature is eligible.
    /// Used by tests to prove reasons can only name features the forest actually split on.
    /// </summary>
    public IReadOnlyList<int>? AllowedFeatures { get; init; }

    /// <summary>Contributions with |Δ| at or below this are dropped rather than padding the list.</summary>
    public double MinAbsContribution { get; init; } = 1e-9;

    // ── Model corrections (opt-in — these CHANGE EVERY SCORE) ────────────────
    // Everything above leaves the fitted model bit-identical. The flags below do not: turning them
    // on re-baselines the whole population, so they ship behind config and get announced.

    /// <summary>
    /// Enables four fixes that alter the feature matrix:
    /// <list type="bullet">
    /// <item>a user with no personal baseline gets the observed-subpopulation median on the self
    /// columns plus an explicit <c>self_baseline_available</c> flag, instead of 0 — which
    /// standardizes to roughly −3σ and isolates people for being new;</item>
    /// <item>self z-scores become median/MAD-based, clamped, so one degenerate baseline cannot
    /// compress a column for the whole organization;</item>
    /// <item><c>mean_self_z_*</c> keeps its sign, which is what lets a self reason say "above" or
    /// "below" instead of "unknown";</item>
    /// <item>the six <c>dept_*</c> columns leave the matrix. They are constant within a department,
    /// already encoded by the 13 <c>*_peer_z</c> columns, and absorb ~10% of all random splits.</item>
    /// </list>
    /// </summary>
    public bool EnableModelCorrections { get; init; }

    /// <summary>
    /// Displays a fixed mapping of the raw score instead of the cohort min-max. Min-max always
    /// pins the top user at exactly 100 and the bottom at exactly 0, so scores cannot be compared
    /// across days even in principle.
    /// </summary>
    public bool UseAbsoluteScoreScale { get; init; }

    /// <summary>Raw-score anchors for the absolute scale. 0.5 is the classic "average path length" point.</summary>
    public double AbsoluteScaleFloor { get; init; } = 0.45;
    public double AbsoluteScaleCeiling { get; init; } = 0.70;

    /// <summary>
    /// When set, a user is additionally flagged only if their raw score clears this bar — so a
    /// genuinely quiet week can produce zero absolute anomalies while the review queue still exists.
    /// </summary>
    public double? AbsoluteAnomalyThreshold { get; init; }

    /// <summary>Robust self z-scores are clamped here rather than running to 1e8.</summary>
    public double MaxSelfZ { get; init; } = 8.0;
}
