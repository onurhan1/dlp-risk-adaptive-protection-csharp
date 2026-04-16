namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Azure OpenAI API Service interface - Testing connections and generating AI analysis
/// </summary>
public interface IAzureOpenAIService
{
    Task<bool> TestConnectionAsync(string endpoint, string apiKey);

    Task<(string Explanation, string Recommendation)> GenerateAnalysisAsync(
        string endpoint,
        string apiKey,
        string model,
        string entityType,
        string entityId,
        Dictionary<string, object> analysisData,
        double? temperature = null,
        int? maxTokens = null);
}
