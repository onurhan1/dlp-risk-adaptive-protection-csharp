namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Policy Management Service interface - Forcepoint DLP Policy operations
/// </summary>
public interface IPolicyService
{
    Task<string> GetAccessTokenAsync();
    Task<List<Dictionary<string, object>>> FetchPoliciesAsync();
    Task<Dictionary<string, object>> FetchPolicyAsync(string policyId);
    Dictionary<string, object> GetPolicyRecommendation(int riskScore, string riskLevel, string channel);
}
