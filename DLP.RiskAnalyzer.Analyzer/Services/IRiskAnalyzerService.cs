using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Interface for the extended risk analyzer service providing dashboard data operations.
/// </summary>
public interface IRiskAnalyzerService
{
    Task<List<UserRiskTrend>> GetUserRiskTrendsAsync(int days = 30, string? user = null);
    Task<List<DepartmentSummary>> GetDepartmentSummariesAsync(DateOnly? startDate, DateOnly? endDate);
    Task<List<DailySummary>> GetDailySummariesAsync(int days = 7);
    Task<RiskHeatmapData> GetRiskHeatmapAsync(string dimension, DateOnly? startDate, DateOnly? endDate);
    Task<int> ProcessRedisStreamAsync(IRedisStreamProcessor redisProcessor);
    Task<int> CalculateRiskScoresAsync();
    Task<UserListResponse> GetUserListAsync(int page = 1, int pageSize = 15, string? search = null);
    Task<ChannelActivityResponse> GetChannelActivityAsync(DateOnly? startDate, DateOnly? endDate, int days = 30);
    Task<List<IOBDetectionItem>> GetIOBDetectionsAsync(DateOnly? startDate, DateOnly? endDate, string? category = null);
    Task<List<TopUserItem>> GetTopUsersByDayAsync(int days = 30, int limit = 20, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<TopRuleItem>> GetTopRulesByDayAsync(int days = 30, int limit = 10, DateTime? startDate = null, DateTime? endDate = null);
    Task<DailyReportResponse> GetDailyReportDataAsync(DateTime date);
    double GetDisplayScore(int riskScore);
    Task<int> CalculateDailyScoresAsync(DateOnly? date = null);
}
