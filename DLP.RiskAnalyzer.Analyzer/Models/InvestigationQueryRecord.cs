namespace DLP.RiskAnalyzer.Analyzer.Models;

public static class InvestigationQueryStatus
{
    public const string Pending = "bekliyor";
    public const string Queried = "sorgulandi";
    public const string ReplyReview = "cevap_inceleme_bekliyor";
    public const string ReminderUnanswered = "hatirlatma_yanitsiz";
    public const string Completed = "tamamlandi";
}

public class InvestigationQueryRecord
{
    public int Id { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MailAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime? QueryDate { get; set; }
    public string ResponseStatus { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string QueryStatus { get; set; } = InvestigationQueryStatus.Pending;
    public string? Source { get; set; }
    public string? Team { get; set; }
    public string? Notes { get; set; }
    public int? PlaybookMailLogId { get; set; }
    public string? CorrelationCode { get; set; }
    public DateTime? FirstSentAt { get; set; }
    public DateTime? ReplyReceivedAt { get; set; }
    public string? ReplyMessageId { get; set; }
    public string? ReplyPreview { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public int ReminderCount { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public class InvestigationInboundMail
{
    public int Id { get; set; }
    public string MessageKey { get; set; } = string.Empty;
    public string? RfcMessageId { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime? ReceivedAt { get; set; }
    public string? BodyPreview { get; set; }
    public int? InvestigationQueryId { get; set; }
    public string ProcessingResult { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
