using DLP.RiskAnalyzer.Shared.Constants;

namespace DLP.RiskAnalyzer.Shared.Helpers;

public static class PeriodHelper
{
    public static DateOnly GetStartDate(DateOnly endDate, string period) => period.ToLower() switch
    {
        "daily" or "24h"           => endDate.AddDays(-1),
        "weekly"                   => endDate.AddDays(-RiskConstants.Periods.WeeklyDays),
        "monthly" or "1month"      => endDate.AddDays(-RiskConstants.Periods.MonthlyDays),
        "quarterly" or "3month"    => endDate.AddDays(-RiskConstants.Periods.QuarterlyDays),
        "6month"                   => endDate.AddDays(-RiskConstants.Periods.SemiAnnualDays),
        "yearly" or "12month"      => endDate.AddDays(-RiskConstants.Periods.YearlyDays),
        _                          => endDate.AddDays(-RiskConstants.Periods.MonthlyDays)
    };
}
