using System.Globalization;
using System.Text.RegularExpressions;

namespace DLP.RiskAnalyzer.Analyzer.Helpers;

public static class TurkishNameHelper
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly Dictionary<string, string> TokenMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["saglam"] = "sağlam",
        ["caglar"] = "çağlar",
        ["cagatay"] = "çağatay",
        ["gokhan"] = "gökhan",
        ["gokce"] = "gökçe",
        ["gungor"] = "güngör",
        ["gunes"] = "güneş",
        ["ozgur"] = "özgür",
        ["ozge"] = "özge",
        ["ozlem"] = "özlem",
        ["cigdem"] = "çiğdem",
        ["yagmur"] = "yağmur",
        ["yilmaz"] = "yılmaz",
        ["yildiz"] = "yıldız",
        ["isik"] = "ışık",
        ["ipek"] = "ipek",
        ["ilker"] = "ilker",
        ["ibrahim"] = "ibrahim"
    };

    public static string FromEmailLocalPart(string? emailOrLogin, string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(fallback) && !fallback.Contains('@'))
            return ToTurkishTitle(fallback);

        var source = string.IsNullOrWhiteSpace(emailOrLogin) ? fallback : emailOrLogin;
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;

        var localPart = source.Split('@', 2)[0];
        var tokens = Regex.Split(localPart, "[._\\-\\s]+")
            .Where(t => !string.IsNullOrWhiteSpace(t) && !t.Any(char.IsDigit))
            .Select(NormalizeToken)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        return ToTurkishTitle(string.Join(' ', tokens));
    }

    public static string ToTurkishTitle(string value)
    {
        var lowered = value.Trim().ToLower(TurkishCulture);
        return TurkishCulture.TextInfo.ToTitleCase(lowered);
    }

    private static string NormalizeToken(string token)
    {
        var lowered = token.Trim().ToLowerInvariant();
        return TokenMap.TryGetValue(lowered, out var mapped) ? mapped : lowered;
    }
}
