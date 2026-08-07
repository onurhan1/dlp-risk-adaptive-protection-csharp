namespace DLP.RiskAnalyzer.Analyzer.Models;

public static class InvestigationQueryStatus
{
    public const string Pending = "bekliyor";
    public const string Queried = "sorgulandi";
    public const string Completed = "tamamlandi";
}

public class InvestigationQueryRecord
{
    public int Id { get; set; }
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
    public string ExtraJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
