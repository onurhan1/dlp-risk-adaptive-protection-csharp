namespace DLP.RiskAnalyzer.Analyzer.Models;

public sealed class RiskShadowReview
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty; // confirmed | false_positive | needs_review
    public string Reviewer { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
