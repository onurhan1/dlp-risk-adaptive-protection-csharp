using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;

/// <summary>
/// Repository interface for AI Behavioral Analysis operations
/// </summary>
public interface IAIAnalysisRepository
{
    Task<AIBehavioralAnalysis?> GetAnalysisAsync(string entityType, string entityId, DateTime analysisDate);
    Task SaveAnalysisAsync(AIBehavioralAnalysis analysis);
    Task<Dictionary<string, string>> GetAISettingsAsync();
}
