namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

/// <summary>
/// Configuration for the behavioural surprisal model. Bound from <c>Surprisal</c> in appsettings.
///
/// Nothing here is a risk rule. These are estimator hyperparameters (how much smoothing, how long
/// a baseline window) plus the small consequence table — the only place a human states which
/// direction is bad, because no amount of data can derive that.
/// </summary>
public sealed class SurprisalOptions
{
    public const string SectionName = "Surprisal";

    // ── Baseline windows ─────────────────────────────────────────────────────

    /// <summary>Trailing window for the personal and cluster baselines. A two-year-old baseline is
    /// wrong for someone who changed roles six months ago.</summary>
    public int BaselineWindowDays { get; set; } = 90;

    /// <summary>Recency half-life applied to baseline counts, so old behaviour fades smoothly.</summary>
    public double BaselineRecencyHalfLifeDays { get; set; } = 45;

    /// <summary>Half-life of the accumulated risk score. Replaces the hard 7-day window.</summary>
    public double RiskHalfLifeDays { get; set; } = 14;

    /// <summary>Events older than this are not scored (they only feed baselines).</summary>
    public int ScoreWindowDays { get; set; } = 30;

    // ── Backoff smoothing ────────────────────────────────────────────────────

    /// <summary>
    /// Personal-term pseudo-count. λu = n_user / (n_user + K). A user with no history gets λu = 0,
    /// so the personal term simply carries no weight — missingness never becomes a fake signal.
    /// </summary>
    public double PersonalBackoffK { get; set; } = 30;

    /// <summary>Same, for the behavioural-cluster term.</summary>
    public double ClusterBackoffK { get; set; } = 200;

    /// <summary>Add-alpha smoothing on the organisation term, and the mass reserved for values
    /// never seen before.</summary>
    public double OrgSmoothingAlpha { get; set; } = 0.5;

    // ── Field weights and conditioning ───────────────────────────────────────

    /// <summary>
    /// Per-field weight in the total surprisal. Start uniform-ish and tune from the diagnostic
    /// report, which shows each field's contribution distribution.
    /// </summary>
    public Dictionary<string, double> FieldWeights { get; set; } = new()
    {
        [EventToken.Fields.Channel] = 1.0,
        [EventToken.Fields.Policy] = 1.0,
        [EventToken.Fields.DestinationClass] = 1.5,
        [EventToken.Fields.Classifier] = 1.25,
        [EventToken.Fields.MatchTier] = 1.0,
        [EventToken.Fields.TimeBucket] = 0.75,
        [EventToken.Fields.Action] = 0.5
    };

    /// <summary>
    /// Fields estimated conditionally on another field. Printing to a printer is unremarkable;
    /// P(destination_class | channel) says so, P(destination_class) alone does not.
    /// </summary>
    public Dictionary<string, string> FieldConditioning { get; set; } = new()
    {
        [EventToken.Fields.DestinationClass] = EventToken.Fields.Channel,
        [EventToken.Fields.Classifier] = EventToken.Fields.Policy,
        [EventToken.Fields.Action] = EventToken.Fields.Channel
    };

    // ── Behavioural clustering ───────────────────────────────────────────────

    public int MinClusterCount { get; set; } = 4;
    public int MaxClusterCount { get; set; } = 40;

    /// <summary>Users with fewer baseline events than this are assigned to the nearest cluster but
    /// do not influence the centroids.</summary>
    public int MinEventsForClustering { get; set; } = 5;

    public int ClusteringSeed { get; set; } = 42;
    public int ClusteringMaxIterations { get; set; } = 50;

    // ── Excitation (the learned replacement for sessionization) ──────────────

    /// <summary>Window for the pair-excitation lift. Set from the observed gap distribution — the
    /// diagnostic report prints what the data suggests.</summary>
    public double ExcitationWindowMinutes { get; set; } = 60;

    /// <summary>Pairs observed fewer times than this are not trusted as lift estimates.</summary>
    public int MinPairObservations { get; set; } = 5;

    /// <summary>Cap on the excitation multiplier so one rare pair cannot dominate a score.</summary>
    public double MaxExcitationMultiplier { get; set; } = 3.0;

    // ── Consequence (the only hand-set semantics) ────────────────────────────

    /// <summary>Multiplier when the DLP verdict let the data through versus stopping it.</summary>
    public double EgressMultiplier { get; set; } = 1.0;
    public double BlockedMultiplier { get; set; } = 0.35;

    /// <summary>Destination trust multipliers. Data reaching a personal endpoint is worse than data
    /// reaching a corporate one; this is a judgement, not something the data can state.</summary>
    public Dictionary<string, double> DestinationTrust { get; set; } = new()
    {
        [DestinationClasses.PersonalWebmail] = 1.6,
        [DestinationClasses.PersonalCloud] = 1.6,
        [DestinationClasses.RemovableMedia] = 1.5,
        [DestinationClasses.ExternalDomain] = 1.2,
        [DestinationClasses.CorporateExternal] = 1.0,
        [DestinationClasses.Printer] = 0.9,
        [DestinationClasses.InternalHost] = 0.6,
        [DestinationClasses.Internal] = 0.5,
        [DestinationClasses.Unknown] = 1.0
    };

    /// <summary>Weight of log-scaled match volume in the consequence term.</summary>
    public double MatchVolumeWeight { get; set; } = 0.25;

    // ── Corporate domains, for destination bucketing ─────────────────────────

    public List<string> InternalDomains { get; set; } = new();

    /// <summary>How many events to list in the diagnostic report's "most surprising" sections.</summary>
    public int DiagnosticTopN { get; set; } = 25;
}
