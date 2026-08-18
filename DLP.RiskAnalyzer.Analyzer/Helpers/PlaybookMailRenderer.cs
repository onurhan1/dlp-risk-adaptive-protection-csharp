using System.Text;
using System.Text.RegularExpressions;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Helpers;

/// <summary>
/// Server-side twin of the dashboard's template rendering
/// (<c>applyPlaceholders</c> / <c>toEmailHtml</c> in components/investigation/types.ts).
/// A playbook renders the same templates the analyst previews by hand, so the two must agree
/// character for character — otherwise the "Mail Gönder" preview and the scheduled mail differ.
/// </summary>
public static class PlaybookMailRenderer
{
    /// <summary>Turkish formats matching the browser's toLocaleString('tr-TR') output.</summary>
    private const string DateTimeFormat = "dd.MM.yyyy HH:mm:ss";
    private const string DateFormat = "dd.MM.yyyy";

    /// <summary>Block-level tags that mean the template body is already HTML.</summary>
    private static readonly Regex BlockLevelHtml = new(
        @"<(p|div|br|table|tr|td|th|ul|ol|li|h[1-6]|html|body|blockquote|pre|section)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlDocument = new(@"<html[\s>]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Substitutes the template placeholders for a flagged user.
    /// Note that <c>{{tam_ad}}</c> intentionally resolves to the user's account address rather
    /// than their display name — that is what the dashboard does, and the placeholder is
    /// documented there as "Kullanıcı adı (username)".
    /// </summary>
    public static string ApplyPlaceholders(string? text, WeeklyFlagUserDto user, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var incidents = user.SampleIncidents ?? new List<WeeklyFlagIncidentDto>();
        var primary = incidents
            .OrderByDescending(i => i.MaxMatches)
            .ThenByDescending(i => i.Timestamp)
            .FirstOrDefault();
        var summary = string.Join("\n", incidents.Select(i =>
            $"- {i.Timestamp.ToString(DateTimeFormat)} | {Dash(i.Policy)} | {i.MaxMatches} eşleşme | {Dash(i.Destination)}"));

        return text
            .Replace("{{kullanici}}", string.IsNullOrWhiteSpace(user.ContactEmail) ? user.UserEmail : user.ContactEmail)
            .Replace("{{tam_ad}}", user.UserEmail)
            .Replace("{{takim}}", Dash(user.Team))
            .Replace("{{tarih}}", nowUtc.ToString(DateFormat))
            .Replace("{{destination}}", Dash(primary?.Destination))
            .Replace("{{hedef}}", Dash(primary?.Destination))
            .Replace("{{kanal}}", Dash(primary?.Channel))
            .Replace("{{channel}}", Dash(primary?.Channel))
            .Replace("{{policy}}", Dash(primary?.Policy))
            .Replace("{{kural}}", Dash(primary?.Policy))
            .Replace("{{max_match}}", primary?.MaxMatches.ToString() ?? "-")
            .Replace("{{max_matches}}", primary?.MaxMatches.ToString() ?? "-")
            .Replace("{{olaylar}}", summary.Length > 0 ? summary : "-");
    }

    /// <summary>
    /// Substitutes the placeholders available to a metric mail. A metric mail describes the whole
    /// organisation rather than one person, so it gets its own token set — the per-user tokens
    /// have no meaning here and are left untouched.
    /// </summary>
    public static string ApplyMetricPlaceholders(string? text, PlaybookMetric metric, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var breakdown = metric.Breakdown.Count > 0
            ? string.Join("\n", metric.Breakdown.Select(b => $"- {b.Label}: {b.Count}"))
            : "-";

        var period = $"{metric.WindowStart.ToString(DateFormat)} - {metric.WindowEnd.ToString(DateFormat)}";

        return text
            .Replace("{{metrik}}", metric.Label)
            .Replace("{{deger}}", FormatNumber(metric.Value))
            .Replace("{{esik}}", metric.Threshold.HasValue ? FormatNumber(metric.Threshold.Value) : "-")
            .Replace("{{toplam_incident}}", metric.TotalIncidents.ToString())
            .Replace("{{kullanici_sayisi}}", metric.UniqueUsers.ToString())
            .Replace("{{gun}}", metric.Days.ToString())
            .Replace("{{donem}}", period)
            .Replace("{{filtreler}}", string.IsNullOrWhiteSpace(metric.FilterSummary) ? "-" : metric.FilterSummary)
            .Replace("{{ozet}}", breakdown)
            .Replace("{{tarih}}", nowUtc.ToString(DateFormat));
    }

    /// <summary>Whole numbers render without a decimal tail; averages keep one digit.</summary>
    private static string FormatNumber(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.05
            ? Math.Round(value).ToString("0")
            : value.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

    /// <summary>
    /// Wraps a template body for sending with IsBodyHtml=true. Plain-text bodies (the common
    /// case, since templates are authored in a textarea) get their newlines converted to
    /// &lt;br /&gt; so the recipient does not receive one unformatted block. Bodies that already
    /// contain HTML are passed through untouched.
    /// </summary>
    public static string ToEmailHtml(string? body)
    {
        var content = (body ?? string.Empty).Trim();
        if (content.Length == 0) return string.Empty;

        // A full document brings its own <head>/<style>; do not wrap it.
        if (HtmlDocument.IsMatch(content)) return content;

        var inner = BlockLevelHtml.IsMatch(content)
            ? content
            : EscapeHtml(content).Replace("\r\n", "<br />").Replace("\r", "<br />").Replace("\n", "<br />");

        return "<div style=\"font-family: Arial, Helvetica, sans-serif; font-size: 14px; " +
               $"line-height: 1.6; color: #0f172a;\">{inner}</div>";
    }

    private static string EscapeHtml(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
