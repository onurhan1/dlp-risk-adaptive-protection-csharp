namespace DLP.RiskAnalyzer.Analyzer.Constants;

public static class RedisProcessorConstants
{
    public const int DefaultBatchSize = 500;
    
    public const int PendingPhaseMaxBatches = 3;
    public const int NewPhaseMaxBatches = 10;
    
    public const int ReleasedPendingPhaseMaxBatches = 3;
    public const int ReleasedNewPhaseMaxBatches = 5;
    
    public const string TaskNameReleasedMessage = "Released quarantined message";
}
