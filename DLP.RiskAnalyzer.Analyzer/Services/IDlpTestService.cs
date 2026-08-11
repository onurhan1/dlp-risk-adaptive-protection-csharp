using System.Threading.Tasks;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public record DlpTestResult(int StatusCode, object Content);

public interface IDlpTestService
{
    Task<DlpTestResult> TestAuthenticationAsync();
    Task<DlpTestResult> TestConnectionAsync();
    Task<DlpTestResult> TestFetchIncidentsAsync(int hours = 24);
    Task<DlpTestResult> GetPolicyRulesAsync(string policyName);
    Task<DlpTestResult> GetEnabledPolicyNamesAsync(string type);
    Task<DlpTestResult> GetAllPolicyRulesExceptionsAsync(string type);
    Task<DlpTestResult> GetPolicyRulesExceptionsAsync(string type, string ruleName, string? policyName = null);
    Task<DlpTestResult> DebugPolicyRulesExceptionsAsync(string type, string ruleName, string? policyName = null);
    Task<DlpTestResult> GetConfigAsync();
}
