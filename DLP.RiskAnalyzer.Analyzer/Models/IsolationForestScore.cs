namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// Isolation Forest scoring result stored per user per batch run
/// </summary>
public class IsolationForestScore
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime CalculatedAt { get; set; }
    public int LookbackDays { get; set; }
    public double IFScore { get; set; }        // 0-100 normalized
    public double AnomalyRaw { get; set; }     // raw anomaly score (higher = more anomalous)
    public bool IsAnomaly { get; set; }
    public int IncidentCount { get; set; }
    public int BaselineIncidentCount { get; set; }
    public string FeatureContributions { get; set; } = "[]"; // JSON: List<FeatureContributionDto>
    public string GroupBreakdown { get; set; } = "{}";       // JSON: Dict<group, abs_sum>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Payload schema of <see cref="Explanation"/>. Rows written before the reason layer landed are
    /// version 1 and carry no explanation; the read path returns an empty reason list for them
    /// rather than deserializing a mismatched shape into a chart full of confident zeroes.
    /// </summary>
    public int ExplanationVersion { get; set; } = 1;

    /// <summary>JSON: <see cref="UserExplanationDto"/>. Empty for version 1 rows.</summary>
    public string Explanation { get; set; } = "{}";
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class IsolationForestScoreDto
{
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime CalculatedAt { get; set; }
    public double IFScore { get; set; }

    /// <summary>
    /// The raw isolation-forest score in (0,1), before the cohort min-max normalization that
    /// produces <see cref="IFScore"/>. This is the only quantity that is comparable across runs —
    /// <see cref="IFScore"/> always assigns exactly 100 to the top user and 0 to the bottom one.
    /// </summary>
    public double AnomalyRaw { get; set; }

    public bool IsAnomaly { get; set; }
    public int IncidentCount { get; set; }
    public int BaselineIncidentCount { get; set; }
    /// <summary>Per-feature contributions. Superseded by <see cref="Reasons"/>; kept one release.</summary>
    public List<FeatureContributionDto> TopFeatures { get; set; } = new();

    public Dictionary<string, double> GroupBreakdown { get; set; } = new();

    // ── Reason layer (explanation_version 2) ─────────────────────────────────
    public List<ReasonDto> Reasons { get; set; } = new();
    public List<ReasonDto> SecondarySignals { get; set; } = new();
    public List<DimensionConfidenceDto> Dimensions { get; set; } = new();
    public string ConfidenceLevel { get; set; } = string.Empty;
    public double ExplainedSharePct { get; set; }
    public double UnexplainedSharePct { get; set; }
    public int CohortRank { get; set; }
    public int CohortSize { get; set; }
    public List<string> Caveats { get; set; } = new();
    public Dictionary<string, double> TeamContext { get; set; } = new();
}

/// <summary>
/// One feature's model-faithful contribution: the drop in this user's anomaly score when the
/// feature is replaced by the cohort mean and the user is re-scored through the same fitted trees.
/// </summary>
public class FeatureContributionDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty; // raw, self, peer, dept_ctx

    /// <summary>
    /// Signed score delta. Positive = the feature pushed this user toward isolation. Note that an
    /// isolation forest isolates both tails, so a value far <em>below</em> the cohort also yields a
    /// positive delta. Named <c>ShapValue</c> for wire compatibility; it is not a Shapley value.
    /// </summary>
    public double ShapValue { get; set; }

    public string Direction { get; set; } = string.Empty;
    public double ActualValue { get; set; }
}

// ── Reason layer ─────────────────────────────────────────────────────────────

/// <summary>
/// One (family × dimension) cell, jointly ablated through the fitted forest, ready to be rendered
/// as a single human reason. The server emits KEYS ONLY — no prose, no locale. All wording lives
/// in the dashboard catalog so the same payload renders in TR and EN.
/// </summary>
public class ReasonDto
{
    // ── identity ─────────────────────────────────────────────────────────────
    public string FamilyKey { get; set; } = string.Empty;      // "off_hours"
    public string Dimension { get; set; } = string.Empty;      // self | peer | population
    public string AnchorFeature { get; set; } = string.Empty;  // drives the label and the drill-down
    public List<string> Members { get; set; } = new();         // jointly ablated, for audit
    public int Rank { get; set; }

    // ── IMPACT: effect on the model's score. Model-faithful. ─────────────────
    public double Impact { get; set; }          // signed CCMA delta, raw score units
    public double ImpactPoints { get; set; }    // the same delta on the 0-100 display scale
    public double ImpactSharePct { get; set; }  // from sequential ablation; shares + residual = 100
    public string Effect { get; set; } = string.Empty; // raises | lowers | none

    // ── DEVIATION: comparison to a NAMED reference. Orthogonal to impact. ────
    public string Deviation { get; set; } = string.Empty;  // above | below | at_norm | unknown
    public double? DeviationSigma { get; set; }
    public double? ObservedValue { get; set; }   // RAW units — pre-log1p, pre-scaling
    public double? ReferenceValue { get; set; }  // cohort / department / personal norm, raw units
    public double TailPct { get; set; }          // % of the cohort more extreme, same direction
    public string ValueKind { get; set; } = string.Empty;

    // ── polarity + data quality ──────────────────────────────────────────────
    public string Polarity { get; set; } = string.Empty;     // higher_is_riskier|two_sided|context_only
    public string RiskReading { get; set; } = string.Empty;  // risk_indicator|unusual_not_risky|descriptive

    // ── evidence drill-down ──────────────────────────────────────────────────
    public List<int> EvidenceIncidentIds { get; set; } = new();
    public int EvidenceCount { get; set; }
}

public class DimensionConfidenceDto
{
    public string Dimension { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string Level { get; set; } = string.Empty;   // high | medium | low | none
    public string? ReasonKey { get; set; }              // noBaseline | thinBaseline | tinyDept | smallCohort
    public Dictionary<string, double> ReasonArgs { get; set; } = new();
    public double SharePct { get; set; }
}

/// <summary>The whole reason payload for one user, persisted as a single JSON column.</summary>
public class UserExplanationDto
{
    public List<ReasonDto> Reasons { get; set; } = new();          // risk_indicator cells
    public List<ReasonDto> SecondarySignals { get; set; } = new(); // unusual-but-not-risky cells
    public List<DimensionConfidenceDto> Dimensions { get; set; } = new();
    public string ConfidenceLevel { get; set; } = string.Empty;    // high | medium | low
    public double ExplainedSharePct { get; set; }
    public double UnexplainedSharePct { get; set; }
    public int CohortRank { get; set; }
    public int CohortSize { get; set; }
    public List<string> Caveats { get; set; } = new();
    public Dictionary<string, double> TeamContext { get; set; } = new();
}

/// <summary>The incidents behind one reason, for the drill-down panel.</summary>
public class ReasonEvidenceDto
{
    public string UserEmail { get; set; } = string.Empty;
    public string FamilyKey { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;

    /// <summary>Total incidents matching this reason; may exceed <see cref="Incidents"/>.Count.</summary>
    public int TotalCount { get; set; }

    public List<ReasonEvidenceIncidentDto> Incidents { get; set; } = new();
}

public class ReasonEvidenceIncidentDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Channel { get; set; }
    public string? Destination { get; set; }
    public string? Action { get; set; }
    public string? Policy { get; set; }
    public string? RuleName { get; set; }
    public int Severity { get; set; }
    public int DataSensitivity { get; set; }
    public int MaxMatches { get; set; }
    public string? FileName { get; set; }
}

public class IsolationForestStatusDto
{
    public DateTime? LastRunAt { get; set; }
    public int LastUserCount { get; set; }
    public string Status { get; set; } = "idle"; // idle, running, completed, failed
    public string? LastError { get; set; }
    public bool IsRunning { get; set; }
}

public class IsolationForestOverviewDto
{
    public IsolationForestStatusDto Status { get; set; } = new();
    public List<IsolationForestScoreDto> UserScores { get; set; } = new();
    public int TotalUsers { get; set; }
    public int AnomalyCount { get; set; }
    public int ScoreWindowDays { get; set; } = 7;
    public DateTime? ScoreWindowStart { get; set; }
    public DateTime? ScoreWindowEnd { get; set; }
    public string BaselineStrategy { get; set; } = "all_time_before_score_window";
    public List<DepartmentIFRiskDto> DepartmentRisks { get; set; } = new();
}

public class DepartmentIFRiskDto
{
    public string Department { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int AnomalyCount { get; set; }
    public double MeanScore { get; set; }
    public double MaxScore { get; set; }
}
