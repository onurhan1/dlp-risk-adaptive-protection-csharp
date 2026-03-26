namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IAnomalyDetector
{
    Task<Dictionary<string, double>> CalculateUserBaselineAsync(
        string userEmail, int lookbackDays = 30);

    Task<Dictionary<string, object>> DetectAnomaliesAsync(
        string userEmail, int lookbackDays = 30);
}
