using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IInvestigationQueryRemediationSyncService
{
    Task<int> SyncAsync(
        IEnumerable<InvestigationQueryRecord> records,
        string actor,
        DateTime syncAt,
        CancellationToken ct = default);
}
