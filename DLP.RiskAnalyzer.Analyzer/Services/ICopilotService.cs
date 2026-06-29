namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// GitHub Copilot API Service interface - Testing connections and validating API keys
/// </summary>
public interface ICopilotService
{
    Task<bool> TestConnectionAsync(string apiKey);
    Task<bool> CheckCopilotAccessAsync(string apiKey);
}
