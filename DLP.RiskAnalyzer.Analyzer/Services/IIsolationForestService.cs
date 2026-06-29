using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IIsolationForestService
{
    Task<IsolationForestStatusDto> GetStatusAsync();
    Task<IsolationForestOverviewDto> GetOverviewAsync();    Task<IsolationForestStatusDto> TriggerRunAsync(int lookbackDays = 30);
    Task RunAsync(int lookbackDays = 30);
}
