using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Interface for user behavioral insights and daily risk score operations.
/// </summary>
public interface IUserInsightsService
{
    Task<List<UserDailyRiskScore>> GetUserDailyScoresAsync(string userEmail, DateOnly startDate, DateOnly endDate);
    Task<UserComprehensiveInsightsResponse> GetUserComprehensiveInsightsAsync(string userEmail, string period);
    Task<UserTrendResponse> GetUserWeeklyTrendAsync(string userEmail);
    Task<UserTrendResponse> GetUserMonthlyTrendAsync(string userEmail);
    Task<UserTrendResponse> GetUserQuarterlyTrendAsync(string userEmail);
    Task<List<string>> DetectUserAnomaliesAsync(string userEmail);
    Task<List<RiskyUserReportItem>> GetRiskyUsersReportAsync(string period);
    Task<List<TopRiskyUserItem>> GetTopRiskyUsersFromDailyScoresAsync(string period, int limit = 10, int page = 1, int pageSize = 20);
    Task<List<DailySummaryScoreItem>> GetDailySummaryFromDailyScoresAsync(DateOnly startDate, DateOnly endDate);
    Task<object> GetHighImpactAlertsAsync(int days = 7, int minMaxMatches = 0, int minDailyRiskScore = 0, int page = 1, int pageSize = 20);
}
