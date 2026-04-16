namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// OpenAI API Service interface - Testing connections and generating AI analysis
/// </summary>
public interface IOpenAIService
{
    Task<bool> TestConnectionAsync(string apiKey, string? model = null);

    Task<(string Explanation, string Recommendation)> GenerateAnalysisAsync(
        string apiKey,
        string model,
        string entityType,
        string entityId,
        Dictionary<string, object> analysisData,
        double? temperature = null,
        int? maxTokens = null);
}
