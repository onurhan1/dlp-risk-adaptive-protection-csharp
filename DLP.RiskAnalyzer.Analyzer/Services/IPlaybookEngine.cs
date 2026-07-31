using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// One unit of data flowing between playbook nodes — n8n's "item" model. The payload is the
/// existing weekly-flag DTO plus the criterion that put this user into the flow, so the mail
/// log can report "why was this person queried".
/// </summary>
public record PlaybookItem(WeeklyFlagUserDto User, string SourceCriterion);

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
    Task<PlaybookRun> RunAsync(int playbookId, string triggerType, bool? forceDryRun, CancellationToken ct = default);

    /// <summary>Structural checks performed before a graph is saved or run.</summary>
    Task<PlaybookValidationResult> ValidateAsync(PlaybookGraph graph, CancellationToken ct = default);

    /// <summary>
    /// Sends mails that a dry run left in "pending". Returns (sent, failed).
    /// Pass a single log id to approve one row, or null to approve every pending row of the run.
    /// </summary>
    Task<(int Sent, int Failed)> ApprovePendingAsync(int runId, int? mailLogId, CancellationToken ct = default);
}
