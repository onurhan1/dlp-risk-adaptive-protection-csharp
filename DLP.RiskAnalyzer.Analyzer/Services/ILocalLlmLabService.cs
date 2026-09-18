namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface ILocalLlmLabService
{
    Task<LocalLlmLabSettings> GetSettingsAsync(CancellationToken ct);
    Task SaveSettingsAsync(LocalLlmLabSettings settings, CancellationToken ct);
    Task<string> TestConnectionAsync(LocalLlmLabSettings settings, CancellationToken ct);
    Task<LocalLlmIncidentSnapshot> GetIncidentSnapshotAsync(LocalLlmSnapshotRequest request, CancellationToken ct);
    Task<IReadOnlyList<LocalLlmConversationSummary>> GetConversationsAsync(string ownerUsername, CancellationToken ct);
    Task<LocalLlmConversationDetail> CreateConversationAsync(string ownerUsername, string? title, CancellationToken ct);
    Task<LocalLlmConversationDetail?> GetConversationAsync(Guid conversationId, string ownerUsername, CancellationToken ct);
    Task<bool> DeleteConversationAsync(Guid conversationId, string ownerUsername, CancellationToken ct);
    Task<IReadOnlyList<LocalLlmMailProposalDto>> GetMailProposalsAsync(Guid conversationId, string ownerUsername, CancellationToken ct);
    Task<LocalLlmMailProposalDto?> UpdateMailProposalAsync(Guid proposalId, string ownerUsername, LocalLlmMailProposalUpdateRequest request, CancellationToken ct);
    Task<LocalLlmMailProposalDto?> ApproveMailProposalAsync(Guid proposalId, string ownerUsername, CancellationToken ct);
    Task<LocalLlmMailProposalDto?> RejectMailProposalAsync(Guid proposalId, string ownerUsername, CancellationToken ct);
    Task<LocalLlmChatResult> ChatAsync(LocalLlmChatRequest request, string ownerUsername, CancellationToken ct);
}

public sealed record LocalLlmLabSettings(
    bool Enabled,
    string GenerateUrl,
    string Model,
    double Temperature,
    int MaxTokens);

public sealed record LocalLlmSnapshotRequest(
    int LookbackDays = 30,
    int SampleSize = 40,
    bool MaskIdentifiers = true,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    bool Comprehensive = false);

public sealed record LocalLlmChatMessage(string Role, string Content);

public sealed record LocalLlmChatRequest(
    string Message,
    List<LocalLlmChatMessage>? History,
    int LookbackDays = 30,
    int SampleSize = 40,
    bool MaskIdentifiers = true,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    bool Comprehensive = false,
    int DetailedUserLimit = 20,
    int EvidenceRowsPerUser = 160,
    Guid? ConversationId = null);

public sealed record LocalLlmChatResult(Guid ConversationId, string Reply, LocalLlmIncidentSnapshot Snapshot, int MailDraftsPrepared = 0, int MailDraftsUnresolved = 0);

public sealed record LocalLlmConversationCreateRequest(string? Title);

public sealed record LocalLlmConversationSummary(
    Guid Id,
    string Title,
    DateTime UpdatedAt,
    int MessageCount,
    string? Preview);

public sealed record LocalLlmConversationDetail(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<LocalLlmChatMessage> Messages);

public sealed record LocalLlmMailProposalUpdateRequest(string? RecipientEmail, string? Subject, string? Body);

public sealed record LocalLlmMailProposalDto(
    Guid Id,
    Guid ConversationId,
    string UserName,
    string? FullName,
    string? Department,
    string? RecipientEmail,
    string Subject,
    string Body,
    string IncidentSummaryJson,
    string? Rationale,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? SentAt,
    string? ErrorMessage);

public sealed record LocalLlmIncidentSnapshot(
    DateTime StartUtc,
    DateTime EndUtc,
    int TotalIncidents,
    int UniqueUsers,
    int MaximumMatches,
    int ProfileCount,
    IReadOnlyList<LocalLlmCount> Actions,
    IReadOnlyList<LocalLlmCount> Channels,
    IReadOnlyList<LocalLlmCount> Policies,
    IReadOnlyList<LocalLlmUserProfile> Users,
    IReadOnlyList<LocalLlmIncidentSample> Samples);

public sealed record LocalLlmCount(string Name, int Count);

public sealed record LocalLlmUserProfile(
    string User,
    string? Department,
    int IncidentCount,
    int MaximumSeverity,
    int MaximumMatches,
    double AverageDataSensitivity,
    int RepeatCount);

public sealed record LocalLlmIncidentSample(
    DateTime Timestamp,
    string User,
    string? Department,
    string? Action,
    int Severity,
    int MaxMatches,
    int DataSensitivity,
    int RepeatCount,
    string? Policy,
    string? Rule,
    string? Channel,
    string? Destination);
