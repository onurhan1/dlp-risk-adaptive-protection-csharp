namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IAnomalyDetector
{
    Task<Dictionary<string, double>> CalculateUserBaselineAsync(
        string userEmail, string metricType);

    Task<Dictionary<string, object>> DetectAnomaliesAsync(
        string userEmail, double currentValue, string metricType = "cloud_upload");
}
