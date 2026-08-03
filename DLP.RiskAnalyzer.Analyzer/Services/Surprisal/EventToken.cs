namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

/// <summary>
/// One DLP incident reduced to the categorical fields the surprisal model estimates
/// probabilities over, plus the numeric fields the consequence term needs.
///
/// Everything here is a low-cardinality token on purpose. Raw <c>destination</c> has ~950 distinct
/// values in a single day of production traffic; estimating P(destination) over that is hopeless
/// and would make every destination look rare. The tokenizer buckets it into ~9 classes instead.
/// </summary>
internal sealed record EventToken
{
    public int IncidentId { get; init; }
    public string User { get; init; } = "";
    public string Department { get; init; } = "";
    public DateTime Timestamp { get; init; }

    // ── Categorical fields the model learns distributions over ───────────────
    public string Channel { get; init; } = Unknown;
    public string Policy { get; init; } = Unknown;
    public string DestinationClass { get; init; } = Unknown;
    public string Classifier { get; init; } = Unknown;
    public string MatchTier { get; init; } = Unknown;
    public string TimeBucket { get; init; } = Unknown;
    public string Action { get; init; } = Unknown;

    // ── Numeric / derived, for the consequence term and diagnostics ──────────
    public double MaxMatches { get; init; }
    public int Severity { get; init; }
    public int DataSensitivity { get; init; }

    /// <summary>True when the DLP verdict let the data through (RELEASED / AUTHORIZED / CONFIRM_CONTINUE).</summary>
    public bool Egressed { get; init; }

    /// <summary>All classifiers on the incident, for diagnostics and evidence.</summary>
    public IReadOnlyList<string> AllClassifiers { get; init; } = Array.Empty<string>();

    public const string Unknown = "unknown";

    /// <summary>Field ids, used as dictionary keys everywhere downstream.</summary>
    public static class Fields
    {
        public const string Channel = "channel";
        public const string Policy = "policy";
        public const string DestinationClass = "destination_class";
        public const string Classifier = "classifier";
        public const string MatchTier = "match_tier";
        public const string TimeBucket = "time_bucket";
        public const string Action = "action";

        public static readonly string[] All =
        {
            Channel, Policy, DestinationClass, Classifier, MatchTier, TimeBucket, Action
        };
    }

    public string Value(string field) => field switch
    {
        Fields.Channel => Channel,
        Fields.Policy => Policy,
        Fields.DestinationClass => DestinationClass,
        Fields.Classifier => Classifier,
        Fields.MatchTier => MatchTier,
        Fields.TimeBucket => TimeBucket,
        Fields.Action => Action,
        _ => Unknown
    };
}

/// <summary>Destination buckets. Names are stable keys — the UI localizes them.</summary>
internal static class DestinationClasses
{
    public const string Internal = "internal";
    public const string CorporateExternal = "corporate_external";
    public const string PersonalWebmail = "personal_webmail";
    public const string PersonalCloud = "personal_cloud";
    public const string RemovableMedia = "removable_media";
    public const string Printer = "printer";
    public const string InternalHost = "internal_host";
    public const string ExternalDomain = "external_domain";
    public const string Unknown = "unknown";
}

internal static class TimeBuckets
{
    public const string Work = "work";          // weekday 08:00-19:00
    public const string Evening = "evening";    // weekday 19:00-22:00
    public const string Night = "night";        // 22:00-08:00
    public const string Weekend = "weekend";
}
