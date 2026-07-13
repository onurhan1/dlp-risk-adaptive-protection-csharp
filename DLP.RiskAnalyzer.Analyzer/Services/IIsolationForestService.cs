using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IIsolationForestService
{
    Task<IsolationForestStatusDto> GetStatusAsync();
    Task<IsolationForestOverviewDto> GetOverviewAsync();
    Task<IsolationForestStatusDto> TriggerRunAsync();
    Task RunAsync();
}
