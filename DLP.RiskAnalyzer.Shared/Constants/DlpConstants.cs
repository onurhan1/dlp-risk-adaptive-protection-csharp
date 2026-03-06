namespace DLP.RiskAnalyzer.Shared.Constants;

public static class DlpConstants
{
    /// <summary>
    /// Redis pub/sub channel used to broadcast Forcepoint DLP configuration updates.
    /// </summary>
    public const string DlpConfigChannel = "dlp:config:updated";

    /// <summary>
    /// Redis pub/sub channel used to trigger manual incident collection from the Collector service.
    /// </summary>
    public const string ManualCollectChannel = "dlp:collector:manual-collect";

    /// <summary>
    /// Redis key prefix for manual collection job status tracking.
    /// Full key format: dlp:collector:job:{jobId}
    /// </summary>
    public const string ManualCollectJobKeyPrefix = "dlp:collector:job:";
}

