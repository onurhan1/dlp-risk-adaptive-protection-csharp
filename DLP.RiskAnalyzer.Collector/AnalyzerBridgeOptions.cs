namespace DLP.RiskAnalyzer.Collector;

public class AnalyzerBridgeOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5001";
    public string InternalSecret { get; set; } = string.Empty;
    public int ConfigPollIntervalSeconds { get; set; } = 300;
}

