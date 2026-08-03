using System.Text.Json;
using System.Text.RegularExpressions;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

/// <summary>
/// Turns raw incidents into <see cref="EventToken"/>s.
///
/// Two decisions here carry most of the model's quality:
/// <list type="bullet">
/// <item><b>Destination bucketing.</b> Raw destinations are near-unique, so they carry no
/// estimable distribution. Bucketing into ~9 trust classes is what makes
/// "sent somewhere he never sends" a learnable statement rather than a tautology.</item>
/// <item><b>Classifier extraction.</b> <c>data_type</c> is empty in production; the real data-class
/// signal lives in <c>violation_triggers[].classifiers[].classifier_name</c> together with match
/// counts. The dominant classifier (most matches) becomes the token.</item>
/// </list>
/// </summary>
internal sealed class EventTokenizer
{
    private readonly SurprisalOptions _options;
    private readonly HashSet<string> _internalDomains;

    private static readonly string[] PersonalWebmail =
    {
        "gmail.com", "googlemail.com", "hotmail.com", "outlook.com", "live.com", "msn.com",
        "yahoo.com", "yandex.com", "yandex.ru", "mail.ru", "proton.me", "protonmail.com",
        "gmx.com", "aol.com", "icloud.com", "zoho.com", "mynet.com", "superonline.com"
    };

    private static readonly string[] PersonalCloud =
    {
        "dropbox", "onedrive personal", "personal onedrive", "drive.google", "googledrive",
        "wetransfer", "mega.nz", "mediafire", "box.com", "pcloud", "sendspace", "filemail"
    };

    private static readonly string[] RemovableHints =
    {
        "usb", "removable", "flash", "sd card", "external drive", "taşınabilir", "tasinabilir"
    };

    private static readonly Regex PrivateIp = new(
        @"^(10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.|127\.)", RegexOptions.Compiled);

    private static readonly Regex AnyIp = new(
        @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EventTokenizer(SurprisalOptions options)
    {
        _options = options;
        _internalDomains = new HashSet<string>(
            options.InternalDomains.Select(d => d.Trim().ToLowerInvariant()).Where(d => d.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    public List<EventToken> Tokenize(IEnumerable<Incident> incidents) =>
        incidents.Select(Tokenize).ToList();

    public EventToken Tokenize(Incident inc)
    {
        var channel = Normalize(inc.Channel);
        var classifiers = ExtractClassifiers(inc.ViolationTriggers);

        return new EventToken
        {
            IncidentId = inc.Id,
            User = inc.UserEmail ?? inc.LoginName ?? EventToken.Unknown,
            Department = inc.Department ?? inc.Team ?? EventToken.Unknown,
            Timestamp = inc.Timestamp,
            Channel = channel,
            Policy = Normalize(FirstPolicy(inc.Policy)),
            DestinationClass = ClassifyDestination(inc.Destination, channel),
            Classifier = classifiers.Dominant,
            MatchTier = TierOf(inc.MaxMatches),
            TimeBucket = BucketOf(inc.Timestamp),
            Action = Normalize(inc.Action),
            MaxMatches = Math.Max(0, inc.MaxMatches),
            Severity = inc.Severity,
            DataSensitivity = inc.DataSensitivity,
            Egressed = IsEgress(inc.Action),
            AllClassifiers = classifiers.All
        };
    }

    // ── Destination ──────────────────────────────────────────────────────────

    public string ClassifyDestination(string? destination, string channel)
    {
        var d = (destination ?? "").Trim().ToLowerInvariant();

        // The channel already settles two of the buckets regardless of what the string says.
        if (channel.Contains("PRINTING", StringComparison.OrdinalIgnoreCase) || d.Contains("printer"))
            return DestinationClasses.Printer;

        if (d.Length == 0) return DestinationClasses.Unknown;

        if (RemovableHints.Any(h => d.Contains(h))) return DestinationClasses.RemovableMedia;
        if (PersonalCloud.Any(h => d.Contains(h))) return DestinationClasses.PersonalCloud;
        if (PersonalWebmail.Any(h => d.Contains(h))) return DestinationClasses.PersonalWebmail;

        if (AnyIp.IsMatch(d))
            return PrivateIp.IsMatch(d) ? DestinationClasses.InternalHost : DestinationClasses.ExternalDomain;

        var domain = DomainOf(d);
        if (domain.Length == 0) return DestinationClasses.Unknown;

        if (_internalDomains.Count > 0 && _internalDomains.Any(i => domain == i || domain.EndsWith("." + i, StringComparison.Ordinal)))
            return DestinationClasses.Internal;

        return domain.Contains('.') ? DestinationClasses.CorporateExternal : DestinationClasses.Unknown;
    }

    private static string DomainOf(string value)
    {
        var at = value.LastIndexOf('@');
        var s = at >= 0 ? value[(at + 1)..] : value;
        s = s.Replace("https://", "").Replace("http://", "");
        var slash = s.IndexOf('/');
        if (slash > 0) s = s[..slash];
        var colon = s.IndexOf(':');
        if (colon > 0) s = s[..colon];
        return s.Trim();
    }

    // ── Classifiers (the real data-class signal) ─────────────────────────────

    internal sealed record ClassifierSet(string Dominant, IReadOnlyList<string> All);

    public ClassifierSet ExtractClassifiers(string? violationTriggersJson)
    {
        if (string.IsNullOrWhiteSpace(violationTriggersJson) || violationTriggersJson.Trim() == "[]")
            return new ClassifierSet(EventToken.Unknown, Array.Empty<string>());

        try
        {
            using var doc = JsonDocument.Parse(violationTriggersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new ClassifierSet(EventToken.Unknown, Array.Empty<string>());

            var found = new List<(string Name, double Matches)>();

            foreach (var trigger in doc.RootElement.EnumerateArray())
            {
                if (!TryGetProperty(trigger, "classifiers", out var classifiers)) continue;
                if (classifiers.ValueKind != JsonValueKind.Array) continue;

                foreach (var c in classifiers.EnumerateArray())
                {
                    if (!TryGetProperty(c, "classifier_name", out var nameEl) &&
                        !TryGetProperty(c, "ClassifierName", out nameEl)) continue;

                    var name = nameEl.GetString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    double matches = 0;
                    if (TryGetProperty(c, "number_matches", out var m) || TryGetProperty(c, "NumberMatches", out m))
                        matches = m.ValueKind == JsonValueKind.Number ? m.GetDouble() : 0;

                    found.Add((NormalizeClassifier(name), matches));
                }
            }

            if (found.Count == 0) return new ClassifierSet(EventToken.Unknown, Array.Empty<string>());

            var dominant = found.OrderByDescending(f => f.Matches).ThenBy(f => f.Name, StringComparer.Ordinal).First().Name;
            var all = found.Select(f => f.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
            return new ClassifierSet(dominant, all);
        }
        catch (JsonException)
        {
            return new ClassifierSet(EventToken.Unknown, Array.Empty<string>());
        }
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        return el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out value);
    }

    /// <summary>
    /// "IBAN Turkish (Wide) (Script)" and "IBAN Turkish (Narrow)" are the same data class tuned
    /// differently; collapsing the tuning suffix keeps the vocabulary estimable.
    /// </summary>
    internal static string NormalizeClassifier(string name)
    {
        var s = name.Trim();
        s = Regex.Replace(s, @"\s*\((Wide|Narrow|Script|Regex|Keyword|KW)\)", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length == 0 ? EventToken.Unknown : s;
    }

    // ── Policy ───────────────────────────────────────────────────────────────

    /// <summary>Policy arrives as "A; B" when several fired. The first is the dominant one.</summary>
    private static string FirstPolicy(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy)) return EventToken.Unknown;
        var idx = policy.IndexOf(';');
        return (idx > 0 ? policy[..idx] : policy).Trim();
    }

    // ── Match volume ─────────────────────────────────────────────────────────

    /// <summary>
    /// Log-ish tiers. Production match counts are extremely heavy-tailed (p50≈9, p99≈223,
    /// max in the thousands), so raw counts cannot be a categorical token.
    /// </summary>
    public static string TierOf(double maxMatches) => maxMatches switch
    {
        <= 0 => "none",
        <= 5 => "1-5",
        <= 15 => "6-15",
        <= 50 => "16-50",
        <= 250 => "51-250",
        <= 1000 => "251-1000",
        _ => "1000+"
    };

    // ── Time ─────────────────────────────────────────────────────────────────

    public static string BucketOf(DateTime ts)
    {
        if (ts.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return TimeBuckets.Weekend;
        return ts.Hour switch
        {
            >= 8 and < 19 => TimeBuckets.Work,
            >= 19 and < 22 => TimeBuckets.Evening,
            _ => TimeBuckets.Night
        };
    }

    // ── Action ───────────────────────────────────────────────────────────────

    private static readonly HashSet<string> EgressActions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Note ENDPOINT_CONFIRM_CONTINUE: the user clicked through the warning, so the data left.
        // The legacy IsolationForestEngine.ActionMap does not know this value and silently scores
        // it as neutral.
        "released", "authorized", "permit", "permitted", "allow", "allowed",
        "monitor", "monitored", "audit", "endpoint_confirm_continue", "confirm_continue"
    };

    public static bool IsEgress(string? action) =>
        !string.IsNullOrWhiteSpace(action) && EgressActions.Contains(action.Trim());

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? EventToken.Unknown : value.Trim();
}
