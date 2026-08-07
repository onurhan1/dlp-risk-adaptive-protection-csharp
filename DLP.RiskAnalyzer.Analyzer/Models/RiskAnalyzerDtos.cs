using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Models;

// ─── User List ───────────────────────────────────────────────────────────────

public class UserListResponse
{
    public List<UserListItem> Users { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UserListItem
{
    public string UserEmail { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public int TotalIncidents { get; set; }
    public string LastIncidentDate { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
}

// ─── Channel Activity ────────────────────────────────────────────────────────

public class ChannelActivityResponse
{
    public List<ChannelActivityItem> Channels { get; set; } = [];
    public List<DestinationActivityItem> Destinations { get; set; } = [];
    public int Total { get; set; }
    public DateRangeInfo DateRange { get; set; } = new();
}

public class ChannelActivityItem
{
    public string Channel { get; set; } = string.Empty;
    public int TotalIncidents { get; set; }
    public double Percentage { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
}

public class DestinationActivityItem
{
    public string Destination { get; set; } = string.Empty;
    public int TotalIncidents { get; set; }
    public double Percentage { get; set; }
}

public class DateRangeInfo
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
}

// ─── IOB Detections ──────────────────────────────────────────────────────────

public class IOBDetectionItem
{
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
    public int UsersAffected { get; set; }
}

// ─── Top Users / Rules ───────────────────────────────────────────────────────

public class TopUserItem
{
    public string UserEmail { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public int RiskScore { get; set; }
    public string Department { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
}

public class TopRuleItem
{
    public string RuleName { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public double AvgRiskScore { get; set; }
    public int UniqueUsers { get; set; }
}

// ─── Daily Report ────────────────────────────────────────────────────────────

public class DailyReportResponse
{
    public string Date { get; set; } = string.Empty;
    public ActionSummary ActionSummary { get; set; } = new();
    public List<DailyReportTopUser> TopUsers { get; set; } = [];
    public List<DailyReportPolicy> TopPolicies { get; set; } = [];
    public List<DailyReportChannel> ChannelBreakdown { get; set; } = [];
    public List<DailyReportDestination> TopDestinations { get; set; } = [];
}

public class ActionSummary
{
    public int Authorized { get; set; }
    public int Block { get; set; }
    public int Quarantine { get; set; }
    public int Released { get; set; }
    public int Total { get; set; }
}

public class DailyReportTopUser
{
    public string UserEmail { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
}

public class DailyReportPolicy
{
    public string PolicyName { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public List<DailyReportRule> TopRules { get; set; } = [];
}

public class DailyReportRule
{
    public string RuleName { get; set; } = string.Empty;
    public int AlertCount { get; set; }
}

public class DailyReportChannel
{
    public string Channel { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public double Percentage { get; set; }
}

public class DailyReportDestination
{
    public string Destination { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public double Percentage { get; set; }
}

// ─── User Comprehensive Insights ─────────────────────────────────────────────

public class UserComprehensiveInsightsResponse
{
    public List<UserDailyRiskScore> DailyScores { get; set; } = [];
    public UserInsightsSummary Summary { get; set; } = new();
    public Dictionary<string, PeriodAverage> PeriodAverages { get; set; } = new();
    public ActionBreakdown ActionBreakdown { get; set; } = new();
    public string Period { get; set; } = string.Empty;
}

public class UserInsightsSummary
{
    public int TotalIncidents { get; set; }
    public double AvgDailyScore { get; set; }
    public double MaxDailyScore { get; set; }
    public double MinDailyScore { get; set; }
    public int DaysActive { get; set; }
    public int MaxMaxMatches { get; set; }
    public double AvgMaxMatches { get; set; }
    public int TotalBlockCount { get; set; }
    public int TotalPermitCount { get; set; }
    public int TotalQuarantineCount { get; set; }
    public int TotalReleasedCount { get; set; }
}

public class PeriodAverage
{
    public double AvgScore { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalQuarantines { get; set; }
}

public class ActionBreakdown
{
    public int TotalBlocks { get; set; }
    public int TotalPermits { get; set; }
    public int TotalQuarantines { get; set; }
    public int TotalReleased { get; set; }
}

// ─── User Trend ──────────────────────────────────────────────────────────────

public class UserTrendResponse
{
    public string Period { get; set; } = string.Empty;
    public double AverageDailyScore { get; set; }
    public double TotalScore { get; set; }
    public double MaxScore { get; set; }
    public int IncidentCount { get; set; }
    public List<UserDailyRiskScore> Scores { get; set; } = [];
}

// ─── Risky Users Report ──────────────────────────────────────────────────────

public class RiskyUserReportItem
{
    public string UserEmail { get; set; } = string.Empty;
    public double CurrentScore { get; set; }
    public double AvgScore { get; set; }
    public double MaxScore { get; set; }
    public int IncidentCount { get; set; }
    public double TrendChange { get; set; }
    public string TrendDirection { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
}

// ─── Top Risky Users From Daily Scores ───────────────────────────────────────

public class TopRiskyUserItem
{
    public string UserEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public double MaxDailyScore { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalQuarantines { get; set; }
    public int DaysWithActivity { get; set; }
    public int MinDaysRequired { get; set; }
    public string Period { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

// ─── Daily Summary From Daily Scores ─────────────────────────────────────────

public class DailySummaryScoreItem
{
    public string Date { get; set; } = string.Empty;
    public int TotalIncidents { get; set; }
    public int UniqueUsers { get; set; }
    public double AvgRiskScore { get; set; }
    public double MaxRiskScore { get; set; }
    public int HighRiskCount { get; set; }
    public int CriticalRiskCount { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalQuarantines { get; set; }
}
