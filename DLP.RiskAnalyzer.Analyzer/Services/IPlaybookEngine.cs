using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// One user flowing between playbook nodes — n8n's "item" model. The payload is the existing
/// weekly-flag DTO plus the criterion that put this user into the flow, so the mail log can
/// report "why was this person queried".
/// </summary>
public record PlaybookItem(
    WeeklyFlagUserDto User,
    string SourceCriterion,
    int? InvestigationQueryId = null,
    string? ExistingCorrelationCode = null,
    InvestigationQueryRecord? InvestigationQuery = null,
    QueryTrackingDetails? Tracking = null);

public record QueryTrackingDetails(
    string LifecycleStatus,
    string FirstMailStatus,
    DateTime? FirstMailAt,
    string ReminderStatus,
    DateTime? ReplyAt,
    string? TemplateOrSubject);

/// <summary>One row of a metric's breakdown, e.g. "Email: 214".</summary>
public record PlaybookMetricBreakdown(string Label, int Count);

/// <summary>
/// An organisation-wide aggregate measured over a time window — the payload of the incident
/// metric node. Unlike <see cref="PlaybookItem"/> this is a single number rather than a list of
/// people, which is why a payload can carry either shape.
/// </summary>
public class PlaybookMetric
{
    /// <summary>See <see cref="IncidentMetricKind"/>.</summary>
    public string Kind { get; set; } = IncidentMetricKind.TotalIncidents;

    /// <summary>Human-readable metric name, used in mails and the run log.</summary>
    public string Label { get; set; } = string.Empty;

    public double Value { get; set; }

    public int TotalIncidents { get; set; }
    public int UniqueUsers { get; set; }
    public int Days { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }

    /// <summary>Which filters produced this number, so the mail can state its own scope.</summary>
    public string FilterSummary { get; set; } = string.Empty;

    public List<PlaybookMetricBreakdown> Breakdown { get; set; } = new();

    /// <summary>Filled in by the metric threshold node once it has compared the value.</summary>
    public double? Threshold { get; set; }
    public bool ThresholdExceeded { get; set; }
}

public static class IncidentMetricKind
{
    public const string TotalIncidents = "total_incidents";
    public const string UniqueUsers = "unique_users";
    public const string MaxRiskScore = "max_risk_score";
    public const string AvgRiskScore = "avg_risk_score";

    public static readonly string[] All = { TotalIncidents, UniqueUsers, MaxRiskScore, AvgRiskScore };

    public static string Label(string kind) => kind switch
    {
        TotalIncidents => "Toplam incident sayısı",
        UniqueUsers => "Etkilenen kullanıcı sayısı",
        MaxRiskScore => "En yüksek risk skoru",
        AvgRiskScore => "Ortalama risk skoru",
        _ => kind
    };
}

/// <summary>How a metric node groups its breakdown rows.</summary>
public static class IncidentBreakdownDimension
{
    public const string None = "none";
    public const string Channel = "channel";
    public const string Policy = "policy";
    public const string DataType = "data_type";
    public const string Team = "team";
    public const string Severity = "severity";

    public static readonly string[] All = { None, Channel, Policy, DataType, Team, Severity };
}

/// <summary>
/// What travels along an edge. A payload carries either a list of users (the weekly-flag path)
/// or a single aggregate metric (the incident metric path) — never a mix, though a node that
/// merges two branches will concatenate the user lists.
/// </summary>
public class PlaybookPayload
{
    public List<PlaybookItem> Items { get; set; } = new();
    public PlaybookMetric? Metric { get; set; }

    public bool HasMetric => Metric != null;

    /// <summary>What the run log reports as items in/out: a metric counts as one unit.</summary>
    public int Size => Metric != null ? 1 : Items.Count;

    public static PlaybookPayload Empty() => new();
    public static PlaybookPayload OfItems(List<PlaybookItem> items) => new() { Items = items };
    public static PlaybookPayload OfMetric(PlaybookMetric metric) => new() { Metric = metric };
}

/// <summary>One executed node, surfaced in the run history panel.</summary>
public class PlaybookNodeLog
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = "success";   // success | failed | skipped
    public int ItemsIn { get; set; }
    public int ItemsOut { get; set; }
    public long DurationMs { get; set; }
    public string? Message { get; set; }
}

public interface IPlaybookEngine
{
    /// <summary>
    /// Executes a playbook end to end and records a <see cref="PlaybookRun"/>.
    /// <paramref name="forceDryRun"/> overrides the playbook's own AutoSend setting; when null,
    /// the run is a dry run unless the playbook explicitly opted into automatic sending.
    /// </summary>
    Task<PlaybookRun> RunAsync(
        int playbookId,
        string triggerType,
        bool? forceDryRun,
        string? reportRecipientEmail = null,
        bool requestPdfAttachment = false,
        CancellationToken ct = default);

    /// <summary>Structural checks performed before a graph is saved or run.</summary>
    Task<PlaybookValidationResult> ValidateAsync(PlaybookGraph graph, CancellationToken ct = default);

    /// <summary>
    /// Sends mails that a dry run left in "pending". Returns (sent, failed).
    /// Pass a single log id to approve one row, or null to approve every pending row of the run.
    /// </summary>
    Task<(int Sent, int Failed)> ApprovePendingAsync(int runId, int? mailLogId, CancellationToken ct = default);
}
