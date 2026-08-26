namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// A visual investigation workflow ("playbook"): a node graph that can be run manually
/// or on a schedule. The graph itself lives in <see cref="GraphJson"/>; the scheduler only
/// needs <see cref="Enabled"/>, <see cref="ScheduleCron"/> and <see cref="NextRunAt"/>,
/// which are denormalised from the trigger node on every save.
/// </summary>
public class Playbook
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Serialised <see cref="PlaybookGraph"/> (nodes + edges).</summary>
    public string GraphJson { get; set; } = string.Empty;

    /// <summary>Scheduler only picks up enabled playbooks.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When false (the default) every run is a dry run: recipients are computed and logged
    /// as "pending" but no mail leaves the system until a human approves it.
    /// </summary>
    public bool AutoSend { get; set; }

    /// <summary>5-field cron expression compiled from the schedule trigger node. Null = no schedule.</summary>
    public string? ScheduleCron { get; set; }

    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class PlaybookRunStatus
{
    public const string Running = "running";
    public const string Success = "success";
    public const string Failed = "failed";
    public const string AwaitingApproval = "awaiting_approval";
}

public static class PlaybookTriggerType
{
    public const string Schedule = "schedule";
    public const string Manual = "manual";
    public const string EmailRequest = "email_request";
}

/// <summary>One execution of a <see cref="Playbook"/>.</summary>
public class PlaybookRun
{
    public int Id { get; set; }
    public int PlaybookId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>See <see cref="PlaybookRunStatus"/>.</summary>
    public string Status { get; set; } = PlaybookRunStatus.Running;

    /// <summary>See <see cref="PlaybookTriggerType"/>.</summary>
    public string TriggerType { get; set; } = PlaybookTriggerType.Manual;

    public bool DryRun { get; set; }

    /// <summary>Serialised per-node execution log (JSON array), shown in the run history panel.</summary>
    public string? NodeLogJson { get; set; }

    public int MailsSent { get; set; }
    public int MailsPending { get; set; }
    public int MailsFailed { get; set; }
    public int MailsSkipped { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class PlaybookMailStatus
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

/// <summary>
/// One mail produced by a send-mail node. This table is the reportable audit trail:
/// "on this date, this user was queried with this subject".
/// </summary>
public class PlaybookMailLog
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public int PlaybookId { get; set; }

    /// <summary>Graph node id that produced this row (a playbook may have several mail nodes).</summary>
    public string NodeId { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Team { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? CcEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? CorrelationCode { get; set; }
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string? TemplateMatchReason { get; set; }
    public string? IncidentSummaryJson { get; set; }

    /// <summary>Which weekly-flag criterion put this user into the flow.</summary>
    public string? SourceCriterion { get; set; }

    public int TriggerCount { get; set; }

    /// <summary>See <see cref="PlaybookMailStatus"/>.</summary>
    public string Status { get; set; } = PlaybookMailStatus.Pending;

    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
