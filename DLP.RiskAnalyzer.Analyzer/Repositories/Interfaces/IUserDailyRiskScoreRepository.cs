using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;

public interface IUserDailyRiskScoreRepository
{
    Task<List<UserDailyRiskScore>> GetUserDailyScoresAsync(string userEmail, DateOnly startDate, DateOnly endDate);
    Task<List<UserDailyRiskScore>> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate);
    Task<(List<TopRiskyUserItem> Items, int TotalCount, int TotalPages)> GetTopRiskyUsersAsync(
        DateOnly startDate, DateOnly endDate, int minDaysRequired, double minScore, string period, int page, int pageSize);
    Task<List<DailySummaryScoreItem>> GetDailySummariesAsync(DateOnly startDate, DateOnly endDate);
    Task<(List<UserDailyRiskScore> Items, int TotalCount)> GetHighImpactScoresAsync(
        DateOnly startDate, DateOnly endDate, int minMaxMatches, int minDailyRiskScore, int page, int pageSize);
}
