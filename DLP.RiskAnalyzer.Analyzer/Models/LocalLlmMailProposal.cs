namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// A mail draft proposed from a Local LLM conversation. It is never sent until
/// the conversation owner explicitly approves it.
/// </summary>
public sealed class LocalLlmMailProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string OwnerUsername { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public string? RecipientEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? SourceTemplateId { get; set; }
    public string? SourceTemplateName { get; set; }
    /// <summary>saved, suggested, or fallback. A suggested template is never persisted automatically.</summary>
    public string TemplateOrigin { get; set; } = LocalLlmMailTemplateOrigin.Fallback;
    public string IncidentSummaryJson { get; set; } = "{}";
    public string? Rationale { get; set; }
    public string SourcePromptHash { get; set; } = string.Empty;
    public string Status { get; set; } = LocalLlmMailProposalStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class LocalLlmMailTemplateOrigin
{
    public const string Saved = "saved";
    public const string Suggested = "suggested";
    public const string Fallback = "fallback";
}

public static class LocalLlmMailProposalStatus
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
    public const string Unresolved = "unresolved";
}
