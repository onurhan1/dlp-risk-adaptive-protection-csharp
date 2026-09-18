namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>A user-owned Local LLM chat thread.</summary>
public sealed class LocalLlmConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = "Yeni sohbet";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<LocalLlmConversationMessage> Messages { get; set; } = [];
}

public sealed class LocalLlmConversationMessage
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public LocalLlmConversation? Conversation { get; set; }
}
