namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// How a feature's value should be worded. This governs <em>wording only</em> — never the model.
/// The isolation forest isolates both tails on its own, so a value far below the cohort still
/// raises the score regardless of polarity.
/// </summary>
internal enum FeaturePolarity
{
    /// <summary>More is worse. Below-cohort values are unusual but not risk indicators.</summary>
    HigherIsRiskier,

    /// <summary>Interesting in both tails — a collapse in sensitivity, variability or tempo is an
    /// exfil-then-quit / tooling-change signal.</summary>
    TwoSided,

    /// <summary>A fact about the department, identical for every member. Never a reason.</summary>
    ContextOnly
}

internal sealed record RiskFeature(
    string Name,
    string Family,
    string Dimension,
    FeaturePolarity Polarity,
    string ValueKind);

/// <summary>
/// One (family × dimension) cell: the correlated block that is ablated jointly and rendered as a
/// single reason. Ablating the members one at a time and summing would triple-count — five views
/// of "he moved sensitive data" are one story, not five.
/// </summary>
internal sealed record RiskCell(
    string Family,
    string Dimension,
    int[] Members,
    int Priority);

/// <summary>
/// The single source of truth for what every column of the feature matrix means. The server emits
/// keys from here; all prose lives in the dashboard catalog.
/// </summary>
internal static class RiskFeatureCatalog
{
    // ── Dimensions ────────────────────────────────────────────────────────────
    // Note: after StandardScaler a "raw" column value IS a z-score across the whole scored
    // cohort. It is the POPULATION dimension, not "this week".
    public const string DimPopulation = "population";
    public const string DimPeer = "peer";
    public const string DimSelf = "self";
    public const string DimContext = "context";

    // ── Families, in tie-break priority order ─────────────────────────────────
    public const string FamBaselineBreak = "baseline_break";
    public const string FamDataSensitivity = "data_sensitivity";
    public const string FamClassifierHits = "classifier_hits";
    public const string FamPermissiveOutcome = "permissive_outcome";
    public const string FamRareChannel = "rare_channel";
    public const string FamOffHours = "off_hours";
    public const string FamSpread = "spread";
    public const string FamSeverity = "severity";
    public const string FamVolume = "volume";
    public const string FamTeamContext = "team_context";

    private static readonly Dictionary<string, int> FamilyPriority = new()
    {
        [FamBaselineBreak] = 1,
        [FamDataSensitivity] = 2,
        [FamClassifierHits] = 3,
        [FamPermissiveOutcome] = 4,
        [FamRareChannel] = 5,
        [FamOffHours] = 6,
        [FamSpread] = 7,
        [FamSeverity] = 8,
        [FamVolume] = 9,
        [FamTeamContext] = 99
    };

    // ── Value kinds (drive client-side formatting) ────────────────────────────
    private const string KCount = "count";
    private const string KRatio = "ratio";   // 0-1, rendered as %
    private const string KSens = "sens";     // Incident.DataSensitivity — an int score, NOT bytes
    private const string KSev = "sev";       // 1-5
    private const string KAct = "act";       // 1-4 action risk
    private const string KRarity = "rarity"; // nats, -log(channel frequency)
    private const string KRate = "rate";     // incidents/day
    private const string KZ = "z";           // signed sigma
    private const string KZAbs = "zabs";     // unsigned magnitude — sign destroyed at aggregation
    private const string KHead = "head";     // people

    private const FeaturePolarity High = FeaturePolarity.HigherIsRiskier;
    private const FeaturePolarity Two = FeaturePolarity.TwoSided;
    private const FeaturePolarity Ctx = FeaturePolarity.ContextOnly;

    private static readonly RiskFeature[] All =
    {
        // ── population (raw aggregates, standardized across the cohort) — 25 ──
        new("incident_count",             FamVolume,            DimPopulation, High, KCount),
        new("unique_policies",            FamSpread,            DimPopulation, High, KCount),
        new("unique_channels",            FamSpread,            DimPopulation, High, KCount),
        new("unique_destinations",        FamSpread,            DimPopulation, High, KCount),
        new("mean_severity",              FamSeverity,          DimPopulation, High, KSev),
        new("max_severity",               FamSeverity,          DimPopulation, High, KSev),
        new("high_sev_ratio",             FamSeverity,          DimPopulation, High, KRatio),
        new("high_sev_count",             FamSeverity,          DimPopulation, High, KCount),
        new("mean_action_risk",           FamPermissiveOutcome, DimPopulation, High, KAct),
        new("allowed_ratio",              FamPermissiveOutcome, DimPopulation, High, KRatio),
        new("allowed_count",              FamPermissiveOutcome, DimPopulation, High, KCount),
        new("total_tx_size",              FamDataSensitivity,   DimPopulation, High, KSens),
        new("mean_tx_size",               FamDataSensitivity,   DimPopulation, Two,  KSens),
        new("max_tx_size",                FamDataSensitivity,   DimPopulation, High, KSens),
        new("std_tx_size",                FamDataSensitivity,   DimPopulation, Two,  KSens),
        new("total_max_matches",          FamClassifierHits,    DimPopulation, High, KCount),
        new("mean_max_matches",           FamClassifierHits,    DimPopulation, Two,  KCount),
        new("max_max_matches",            FamClassifierHits,    DimPopulation, High, KCount),
        new("off_hours_ratio",            FamOffHours,          DimPopulation, High, KRatio),
        new("off_hours_count",            FamOffHours,          DimPopulation, High, KCount),
        new("weekend_ratio",              FamOffHours,          DimPopulation, High, KRatio),
        new("night_ratio",                FamOffHours,          DimPopulation, High, KRatio),
        new("mean_channel_rarity",        FamRareChannel,       DimPopulation, High, KRarity),
        new("max_channel_rarity",         FamRareChannel,       DimPopulation, High, KRarity),
        new("incidents_per_day",          FamVolume,            DimPopulation, Two,  KRate),

        // ── context (department facts, identical for every member) — 6 ────────
        new("dept_size",                  FamTeamContext, DimContext, Ctx, KHead),
        new("dept_mean_incident_count",   FamTeamContext, DimContext, Ctx, KCount),
        new("dept_mean_off_hours_ratio",  FamTeamContext, DimContext, Ctx, KRatio),
        new("dept_mean_allowed_ratio",    FamTeamContext, DimContext, Ctx, KRatio),
        new("dept_mean_high_sev_ratio",   FamTeamContext, DimContext, Ctx, KRatio),
        new("dept_mean_tx_size",          FamTeamContext, DimContext, Ctx, KSens),

        // ── peer (z vs same-department cohort, same window) — 13 ──────────────
        new("incident_count_peer_z",      FamVolume,            DimPeer, High, KZ),
        new("unique_policies_peer_z",     FamSpread,            DimPeer, High, KZ),
        new("unique_channels_peer_z",     FamSpread,            DimPeer, High, KZ),
        new("unique_destinations_peer_z", FamSpread,            DimPeer, High, KZ),
        new("high_sev_ratio_peer_z",      FamSeverity,          DimPeer, High, KZ),
        new("allowed_ratio_peer_z",       FamPermissiveOutcome, DimPeer, High, KZ),
        new("off_hours_ratio_peer_z",     FamOffHours,          DimPeer, High, KZ),
        new("weekend_ratio_peer_z",       FamOffHours,          DimPeer, High, KZ),
        new("night_ratio_peer_z",         FamOffHours,          DimPeer, High, KZ),
        new("mean_tx_size_peer_z",        FamDataSensitivity,   DimPeer, Two,  KZ),
        new("max_tx_size_peer_z",         FamDataSensitivity,   DimPeer, High, KZ),
        new("mean_max_matches_peer_z",    FamClassifierHits,    DimPeer, Two,  KZ),
        new("incidents_per_day_peer_z",   FamVolume,            DimPeer, Two,  KZ),

        // ── self (aggregated per-incident z vs the personal pre-window baseline) — 16 ──
        // The 12 *_self_z_* columns aggregate Math.Abs(z), so they carry MAGNITUDE ONLY.
        // Anything claiming an above/below direction for them would be guessing.
        new("max_self_z_tx_size",         FamDataSensitivity,   DimSelf, High, KZAbs),
        new("mean_self_z_tx_size",        FamDataSensitivity,   DimSelf, High, KZAbs),
        new("max_self_z_max_matches",     FamClassifierHits,    DimSelf, High, KZAbs),
        new("mean_self_z_max_matches",    FamClassifierHits,    DimSelf, High, KZAbs),
        new("max_self_z_channel_rarity",  FamRareChannel,       DimSelf, High, KZAbs),
        new("mean_self_z_channel_rarity", FamRareChannel,       DimSelf, High, KZAbs),
        new("max_self_z_severity",        FamSeverity,          DimSelf, High, KZAbs),
        new("mean_self_z_severity",       FamSeverity,          DimSelf, High, KZAbs),
        new("max_self_z_action_risk",     FamPermissiveOutcome, DimSelf, High, KZAbs),
        new("mean_self_z_action_risk",    FamPermissiveOutcome, DimSelf, High, KZAbs),
        new("max_self_z_hour",            FamOffHours,          DimSelf, High, KZAbs),
        new("mean_self_z_hour",           FamOffHours,          DimSelf, High, KZAbs),
        new("self_outlier_count",         FamBaselineBreak,     DimSelf, High, KCount),
        new("strong_self_outlier_count",  FamBaselineBreak,     DimSelf, High, KCount),
        new("self_outlier_ratio",         FamBaselineBreak,     DimSelf, High, KRatio),
        new("strong_self_outlier_ratio",  FamBaselineBreak,     DimSelf, High, KRatio),

        // Present only under EnableModelCorrections: the missingness itself, as a fact rather than
        // as an imputed measurement. Context polarity keeps it out of the reason list.
        new("self_baseline_available",    FamTeamContext,       DimContext, Ctx, KCount)
    };

    private static readonly Dictionary<string, RiskFeature> ByName =
        All.ToDictionary(f => f.Name, StringComparer.Ordinal);

    public static IReadOnlyList<RiskFeature> Features => All;

    public static bool TryGet(string name, out RiskFeature feature) => ByName.TryGetValue(name, out feature!);

    /// <summary>
    /// A feature the catalog does not know still has to be scored and grouped rather than crashing
    /// or leaking a machine name to the UI — but it is never eligible to be a reason.
    /// </summary>
    public static RiskFeature Get(string name) =>
        ByName.TryGetValue(name, out var f)
            ? f
            : new RiskFeature(name, FamTeamContext, DimContext, Ctx, KCount);

    public static int PriorityOf(string family) => FamilyPriority.GetValueOrDefault(family, 50);

    /// <summary>
    /// Builds the (family × dimension) cells for the matrix column order actually in use. Context
    /// features are excluded — they describe the department, not the person.
    /// </summary>
    public static List<RiskCell> BuildCells(string[] featureNames)
    {
        var byCell = new Dictionary<(string Family, string Dimension), List<int>>();

        for (int f = 0; f < featureNames.Length; f++)
        {
            var meta = Get(featureNames[f]);
            if (meta.Dimension == DimContext) continue;

            var key = (meta.Family, meta.Dimension);
            if (!byCell.TryGetValue(key, out var members))
                byCell[key] = members = new List<int>();
            members.Add(f);
        }

        return byCell
            .Select(kv => new RiskCell(kv.Key.Family, kv.Key.Dimension, kv.Value.ToArray(), PriorityOf(kv.Key.Family)))
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Dimension, StringComparer.Ordinal)
            .ToList();
    }
}
