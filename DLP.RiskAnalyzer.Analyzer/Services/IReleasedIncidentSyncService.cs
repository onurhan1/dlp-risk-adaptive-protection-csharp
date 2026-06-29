namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IReleasedIncidentSyncService
{
    Task<ReleasedSyncResult> SyncAsync(int lookbackHours = 24);
}
