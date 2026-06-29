namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// Reusable email template for investigation / notification mails.
/// </summary>
public class MailTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
