namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Forcepoint DLP API'den çekilen policy rule exception bilgisini cache'ler.
/// Hiyerarşi: Policy → Rule → Exception
/// </summary>
public class PolicyRuleException
{
    public int Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;     // Parent policy adı
    public string RuleName { get; set; } = string.Empty;       // Parent rule adı
    public string ExceptionName { get; set; } = string.Empty;  // Exception kural adı
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
