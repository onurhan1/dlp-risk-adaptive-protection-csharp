using System.Globalization;

namespace DLP.RiskAnalyzer.Analyzer.Helpers;

/// <summary>
/// Minimal 5-field cron parser/evaluator: "minute hour day-of-month month day-of-week".
/// Written by hand rather than pulling in a NuGet package, because this deployment is
/// offline-first. Supports "*", plain numbers, lists (1,3,5), ranges (1-5) and steps (*&#47;15,
/// 0-30/10). Day-of-week accepts 0-7 with both 0 and 7 meaning Sunday.
/// </summary>
public static class CronSchedule
{
    /// <summary>How far ahead <see cref="Next"/> is willing to search before giving up.</summary>
    private const int MaxSearchDays = 366;

    public static bool TryParse(string? expression, out CronExpression? parsed, out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Cron ifadesi boş";
            return false;
        }

        var fields = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            error = "Cron ifadesi 5 alandan oluşmalı (dakika saat ayın-günü ay haftanın-günü)";
            return false;
        }

        if (!TryParseField(fields[0], 0, 59, out var minutes, out error)) return false;
        if (!TryParseField(fields[1], 0, 23, out var hours, out error)) return false;
        if (!TryParseField(fields[2], 1, 31, out var daysOfMonth, out error)) return false;
        if (!TryParseField(fields[3], 1, 12, out var months, out error)) return false;
        if (!TryParseField(fields[4], 0, 7, out var daysOfWeek, out error)) return false;

        // Normalise Sunday: cron allows both 0 and 7.
        if (daysOfWeek.Contains(7))
        {
            daysOfWeek.Remove(7);
            daysOfWeek.Add(0);
        }

        parsed = new CronExpression(
            expression.Trim(),
            minutes,
            hours,
            daysOfMonth,
            months,
            daysOfWeek,
            DayOfMonthRestricted: fields[2] != "*",
            DayOfWeekRestricted: fields[4] != "*");
        return true;
    }

    public static bool IsValid(string? expression) => TryParse(expression, out _, out _);

    /// <summary>
    /// First matching time strictly after <paramref name="fromUtc"/>, or null when the
    /// expression is invalid or matches nothing within <see cref="MaxSearchDays"/>.
    /// </summary>
    public static DateTime? Next(string? expression, DateTime fromUtc)
    {
        if (!TryParse(expression, out var cron, out _) || cron == null) return null;

        // Start at the next whole minute — a schedule never fires twice in the same minute.
        var candidate = new DateTime(fromUtc.Year, fromUtc.Month, fromUtc.Day,
                                     fromUtc.Hour, fromUtc.Minute, 0, DateTimeKind.Utc)
                        .AddMinutes(1);
        var limit = candidate.AddDays(MaxSearchDays);

        while (candidate < limit)
        {
            if (!cron.Months.Contains(candidate.Month))
            {
                // Skip to the first minute of the next month.
                candidate = new DateTime(candidate.Year, candidate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                continue;
            }

            if (!cron.MatchesDay(candidate))
            {
                candidate = candidate.Date.AddDays(1);
                continue;
            }

            if (!cron.Hours.Contains(candidate.Hour))
            {
                candidate = candidate.Date.AddHours(candidate.Hour + 1);
                continue;
            }

            if (!cron.Minutes.Contains(candidate.Minute))
            {
                candidate = candidate.AddMinutes(1);
                continue;
            }

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// First matching time strictly after <paramref name="fromUtc"/>, interpreting cron fields
    /// in <paramref name="timeZone"/> and returning UTC for storage/comparison.
    /// </summary>
    public static DateTime? Next(string? expression, DateTime fromUtc, TimeZoneInfo timeZone)
    {
        if (!TryParse(expression, out var cron, out _) || cron == null) return null;

        var normalizedUtc = fromUtc.Kind == DateTimeKind.Utc
            ? fromUtc
            : DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, timeZone);

        var candidate = new DateTime(fromLocal.Year, fromLocal.Month, fromLocal.Day,
                                     fromLocal.Hour, fromLocal.Minute, 0, DateTimeKind.Unspecified)
                        .AddMinutes(1);
        var limit = candidate.AddDays(MaxSearchDays);

        while (candidate < limit)
        {
            if (!cron.Months.Contains(candidate.Month))
            {
                candidate = new DateTime(candidate.Year, candidate.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
                continue;
            }

            if (!cron.MatchesDay(candidate))
            {
                candidate = candidate.Date.AddDays(1);
                continue;
            }

            if (!cron.Hours.Contains(candidate.Hour))
            {
                candidate = candidate.Date.AddHours(candidate.Hour + 1);
                continue;
            }

            if (!cron.Minutes.Contains(candidate.Minute))
            {
                candidate = candidate.AddMinutes(1);
                continue;
            }

            return TimeZoneInfo.ConvertTimeToUtc(candidate, timeZone);
        }

        return null;
    }

    /// <summary>Human-readable Turkish summary, used in the playbook list and node card.</summary>
    public static string Describe(string? expression)
    {
        if (!TryParse(expression, out var cron, out _) || cron == null) return "Zamanlama yok";

        var fields = cron.Raw.Split(' ');
        var minuteField = fields[0];
        var hourField = fields[1];
        var dowField = fields[4];
        var domField = fields[2];

        string TimeOfDay()
        {
            var hour = cron.Hours.First();
            var minute = cron.Minutes.First();
            return $"{hour:D2}:{minute:D2}";
        }

        // Weekdays: Monday through Friday, fixed hour and minute.
        if (dowField == "1-5" && domField == "*" && cron.Hours.Count == 1 && cron.Minutes.Count == 1)
            return $"Hafta ici {TimeOfDay()} ({RadarTimeZone.DisplayName})";

        // Weekly: single day-of-week, fixed hour and minute.
        if (dowField != "*" && domField == "*" && cron.Hours.Count == 1 && cron.Minutes.Count == 1)
        {
            var dayNames = cron.DaysOfWeek.OrderBy(d => d == 0 ? 7 : d).Select(TurkishDayName);
            return $"Her {string.Join(", ", dayNames)} {TimeOfDay()} ({RadarTimeZone.DisplayName})";
        }

        // Daily: every day, fixed hour and minute.
        if (dowField == "*" && domField == "*" && cron.Hours.Count == 1 && cron.Minutes.Count == 1)
            return $"Her gün {TimeOfDay()} ({RadarTimeZone.DisplayName})";

        // Hourly: every hour at a fixed minute.
        if (dowField == "*" && domField == "*" && hourField == "*" && cron.Minutes.Count == 1)
            return $"Her saat :{cron.Minutes.First():D2} ({RadarTimeZone.DisplayName})";

        // Every N minutes.
        if (minuteField.StartsWith("*/") && hourField == "*" && domField == "*" && dowField == "*")
            return $"Her {minuteField[2..]} dakikada";

        return $"Cron: {cron.Raw} ({RadarTimeZone.DisplayName})";
    }

    private static string TurkishDayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Pazar",
        1 => "Pazartesi",
        2 => "Salı",
        3 => "Çarşamba",
        4 => "Perşembe",
        5 => "Cuma",
        6 => "Cumartesi",
        _ => dayOfWeek.ToString(CultureInfo.InvariantCulture)
    };

    private static bool TryParseField(string field, int min, int max, out HashSet<int> values, out string? error)
    {
        values = new HashSet<int>();
        error = null;

        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = part.Trim();
            var step = 1;

            var slash = segment.IndexOf('/');
            if (slash >= 0)
            {
                if (!int.TryParse(segment[(slash + 1)..], out step) || step <= 0)
                {
                    error = $"Geçersiz cron adımı: '{segment}'";
                    return false;
                }
                segment = segment[..slash];
            }

            int from, to;
            if (segment == "*")
            {
                from = min;
                to = max;
            }
            else if (segment.Contains('-'))
            {
                var bounds = segment.Split('-');
                if (bounds.Length != 2 ||
                    !int.TryParse(bounds[0], out from) ||
                    !int.TryParse(bounds[1], out to))
                {
                    error = $"Geçersiz cron aralığı: '{segment}'";
                    return false;
                }
            }
            else if (int.TryParse(segment, out var single))
            {
                from = to = single;
            }
            else
            {
                error = $"Geçersiz cron değeri: '{segment}'";
                return false;
            }

            if (from < min || to > max || from > to)
            {
                error = $"Cron değeri {min}-{max} aralığında olmalı: '{part}'";
                return false;
            }

            for (var v = from; v <= to; v += step) values.Add(v);
        }

        if (values.Count == 0)
        {
            error = $"Cron alanı boş: '{field}'";
            return false;
        }

        return true;
    }
}

/// <summary>A parsed 5-field cron expression.</summary>
public record CronExpression(
    string Raw,
    HashSet<int> Minutes,
    HashSet<int> Hours,
    HashSet<int> DaysOfMonth,
    HashSet<int> Months,
    HashSet<int> DaysOfWeek,
    bool DayOfMonthRestricted,
    bool DayOfWeekRestricted)
{
    /// <summary>
    /// Standard cron day semantics: when both day-of-month and day-of-week are restricted the
    /// fields are OR-ed, otherwise only the restricted one applies.
    /// </summary>
    public bool MatchesDay(DateTime moment)
    {
        var domMatch = DaysOfMonth.Contains(moment.Day);
        var dowMatch = DaysOfWeek.Contains((int)moment.DayOfWeek);

        if (DayOfMonthRestricted && DayOfWeekRestricted) return domMatch || dowMatch;
        if (DayOfMonthRestricted) return domMatch;
        if (DayOfWeekRestricted) return dowMatch;
        return true;
    }
}
