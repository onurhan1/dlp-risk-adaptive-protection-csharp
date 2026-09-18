namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Read-only security copilot for reviewing RADAR coverage. It deliberately has
/// no dependencies capable of sending mail, changing workflows or writing incidents.
/// </summary>
public interface ISecurityAgentService
{
    Task<SecurityAgentContext> GetContextAsync(SecurityAgentContextRequest request, CancellationToken ct);
    Task<SecurityAgentChatResult> ChatAsync(SecurityAgentChatRequest request, CancellationToken ct);
    Task<SecurityAgentWorkflowDraftResult> CreateWorkflowDraftAsync(SecurityAgentWorkflowDraftRequest request, CancellationToken ct);
}

public sealed record SecurityAgentContextRequest(DateTime? StartUtc = null, DateTime? EndUtc = null);
public sealed record SecurityAgentChatRequest(
    string Message,
    IReadOnlyList<LocalLlmChatMessage>? History = null,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null);
public sealed record SecurityAgentChatResult(string Reply, SecurityAgentContext Context);
public sealed record SecurityAgentWorkflowDraftRequest(string Goal, DateTime? StartUtc = null, DateTime? EndUtc = null);
public sealed record SecurityAgentWorkflowDraftResult(int PlaybookId, string Name, string Summary, IReadOnlyList<string> Warnings);

public sealed record SecurityAgentContext(
    DateTime GeneratedAtUtc,
    DateTime StartUtc,
    DateTime EndUtc,
    int WorkflowCount,
    int EnabledWorkflowCount,
    int IncidentCount,
    int UniqueUsers,
    IReadOnlyList<SecurityAgentWorkflow> Workflows,
    IReadOnlyList<LocalLlmCount> Channels,
    IReadOnlyList<LocalLlmCount> Actions,
    IReadOnlyList<LocalLlmCount> Policies,
    IReadOnlyList<LocalLlmCount> NodeTypes);

public sealed record SecurityAgentWorkflow(
    int Id,
    string Name,
    bool Enabled,
    bool AutoSend,
    string? Schedule,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> ValidationErrors,
    string? LastRunStatus,
    DateTime? LastRunAt,
    int PendingMails,
    int FailedMails,
    IReadOnlyList<string> LastRunNodeSummary);
