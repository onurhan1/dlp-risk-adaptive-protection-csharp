using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IBehaviorEngineService
{
    Task<AIBehavioralAnalysisResponse> AnalyzeEntityAsync(
        string entityType,
        string entityId,
        int lookbackDays = 7);

    Task<AIBehavioralOverviewResponse> AnalyzeOverviewAsync(int lookbackDays = 7);

    Task SaveAnalysisAsync(AIBehavioralAnalysisResponse response);

    Task<AIBehavioralDetailResponse> AnalyzeEntityDetailAsync(
        string entityType,
        string entityId,
        int lookbackDays = 30);
}
