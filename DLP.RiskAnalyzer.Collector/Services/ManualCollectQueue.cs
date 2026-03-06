using DLP.RiskAnalyzer.Shared.Models;
using System.Collections.Concurrent;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Thread-safe queue for manual collection commands.
/// Shared between DlpConfigurationSyncService (producer) and CollectorBackgroundService (consumer).
/// </summary>
public class ManualCollectQueue
{
    private readonly ConcurrentQueue<ManualCollectCommand> _queue = new();

    public void Enqueue(ManualCollectCommand command)
    {
        _queue.Enqueue(command);
    }

    public bool TryDequeue(out ManualCollectCommand? command)
    {
        return _queue.TryDequeue(out command);
    }

    public bool HasItems => !_queue.IsEmpty;
}
