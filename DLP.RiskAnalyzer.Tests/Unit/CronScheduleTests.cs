using DLP.RiskAnalyzer.Analyzer.Helpers;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class CronScheduleTests
{
    [Fact]
    public void Next_WithTurkeyTimezone_InterpretsCronHourAsTurkeyLocalTime()
    {
        var fromUtc = new DateTime(2026, 08, 20, 05, 58, 00, DateTimeKind.Utc);

        var next = CronSchedule.Next("0 9 * * *", fromUtc, RadarTimeZone.Turkey);

        Assert.Equal(new DateTime(2026, 08, 20, 06, 00, 00, DateTimeKind.Utc), next);
    }
}
