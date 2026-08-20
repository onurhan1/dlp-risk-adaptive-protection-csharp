namespace DLP.RiskAnalyzer.Analyzer.Helpers;

public static class RadarTimeZone
{
    public const string DisplayName = "Türkiye saati";
    public const string ShortName = "TR";

    public static TimeZoneInfo Turkey { get; } = ResolveTurkeyTimeZone();

    public static DateTime NowTurkey() => ToTurkeyTime(DateTime.UtcNow);

    public static DateTime ToTurkeyTime(DateTime utc)
    {
        var normalizedUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, Turkey);
    }

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        foreach (var id in new[] { "Turkey Standard Time", "Europe/Istanbul" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), DisplayName, DisplayName);
    }
}
