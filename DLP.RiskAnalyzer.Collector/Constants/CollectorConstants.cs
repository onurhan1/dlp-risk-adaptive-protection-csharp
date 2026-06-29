namespace DLP.RiskAnalyzer.Collector.Constants;

public static class CollectorConstants
{
    // Chunking and Retries
    public const int DefaultChunkSizeHours = 4;
    public const int MaxApiRetries = 3;
    
    // Delays
    public const int ChunkDelayMs = 500;
    public const int RetryDelaySeconds = 5;
    
    // Known Forcepoint DLP Task Names
    public const string TaskNameReleasedMessage = "Released quarantined message";
}
