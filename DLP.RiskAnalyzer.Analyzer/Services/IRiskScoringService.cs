using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IRiskScoringService
{
    Task<int> CalculateDailyScoresAsync(DateOnly? date = null);
    double GetDisplayScore(int riskScore);
    Task<int> CalculateRiskScoresAsync();
}
