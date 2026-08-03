using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IIsolationForestService
{
    Task<IsolationForestStatusDto> GetStatusAsync();
    Task<IsolationForestOverviewDto> GetOverviewAsync();
    Task<IsolationForestStatusDto> TriggerRunAsync();
    Task RunAsync();

    /// <summary>
    /// The incidents that drove one reason for one user, from the latest run. Resolves ids recorded
    /// at scoring time rather than re-deriving the predicate, so what the analyst opens is exactly
    /// what the model saw.
    /// </summary>
    Task<ReasonEvidenceDto> GetReasonEvidenceAsync(string userEmail, string familyKey, string? dimension);
}
