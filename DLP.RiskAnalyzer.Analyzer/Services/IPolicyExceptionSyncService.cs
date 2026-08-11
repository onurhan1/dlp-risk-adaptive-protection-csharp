namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IPolicyExceptionSyncService
{
    Task<int> SyncAsync(bool force = false);
    Task<Dictionary<string, string>> GetExceptionLookupAsync();
    Task<bool> IsExceptionAsync(string policyName, string ruleName);
}
