using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DLP.RiskAnalyzer.Shared.Services;

/// <summary>
/// Identifies public-mail recipients and whether a recipient plausibly belongs to the sender.
/// The result is an investigation signal, never an automatic enforcement decision.
/// </summary>
public static class PersonalEmailIdentityMatcher
{
    // Deliberately exact domains. A contains/ends-with check would classify addresses such as
    // gmail.com.example as personal webmail.
    private static readonly HashSet<string> PersonalDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com",
        "hotmail.com", "hotmail.co.uk", "hotmail.de", "hotmail.fr",
        "outlook.com", "outlook.com.tr", "outlook.de", "outlook.fr",
        "live.com", "live.co.uk", "msn.com",
        "yahoo.com", "yahoo.co.uk", "yahoo.com.tr", "ymail.com",
        "icloud.com", "me.com", "mac.com",
        "proton.me", "protonmail.com",
        "yandex.com", "yandex.ru", "mail.ru", "bk.ru", "inbox.ru", "list.ru",
        "gmx.com", "gmx.de", "aol.com", "zoho.com", "mynet.com", "superonline.com"
    };

    // Extracts complete mailbox candidates only. It is intentionally not used to infer identity.
    private static readonly Regex MailboxRegex = new(
        @"(?<![A-Z0-9.!#$%&'*+/=?^_`{|}~\-])(?<local>[A-Z0-9.!#$%&'*+/=?^_`{|}~\-]+)@(?<domain>(?:[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?\.)+[A-Z]{2,63})(?![A-Z0-9\-])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsPersonalDestination(string? destination) =>
        ExtractMailboxes(destination).Any(address => IsPersonalDomain(address.Domain));

    public static bool IsPersonalDomain(string? domain) =>
        !string.IsNullOrWhiteSpace(domain) && PersonalDomains.Contains(domain.Trim().TrimEnd('.'));

    public static IReadOnlyList<PersonalEmailIdentityMatch> MatchDestination(
        string? senderEmail,
        string? loginName,
        string? fullName,
        string? destination)
    {
        var senderCandidates = BuildSenderCandidates(senderEmail, loginName, fullName);
        return ExtractMailboxes(destination)
            .Where(address => IsPersonalDomain(address.Domain))
            .Select(address => Match(senderCandidates, address))
            .OrderByDescending(result => result.Confidence)
            .ThenBy(result => result.Recipient, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static PersonalEmailIdentityMatch? FindBestMatch(
        string? senderEmail,
        string? loginName,
        string? fullName,
        string? destination) =>
        MatchDestination(senderEmail, loginName, fullName, destination).FirstOrDefault();

    private static PersonalEmailIdentityMatch Match(IReadOnlyList<SenderCandidate> candidates, Mailbox recipient)
    {
        var normalizedRecipient = NormalizeLocalPart(recipient.LocalPart, recipient.Domain);
        if (normalizedRecipient.Length == 0)
            return new PersonalEmailIdentityMatch(recipient.ToString(), recipient.Domain, false, false,
                PersonalEmailIdentityMatchType.None, PersonalEmailIdentityConfidence.None, null, "Recipient local-part is empty after normalization.");

        var exact = candidates.FirstOrDefault(candidate =>
            candidate.Kind is SenderCandidateKind.Email or SenderCandidateKind.Login &&
            candidate.Normalized == normalizedRecipient);
        if (exact is not null)
        {
            return new PersonalEmailIdentityMatch(recipient.ToString(), recipient.Domain, true, true,
                PersonalEmailIdentityMatchType.ExactLocalPart, PersonalEmailIdentityConfidence.High,
                exact.Source, "Corporate mailbox/login local-part exactly matches the public recipient local-part.");
        }

        var name = candidates.FirstOrDefault(candidate =>
            candidate.Kind == SenderCandidateKind.FullName &&
            candidate.Normalized.Length >= 6 && candidate.Normalized == normalizedRecipient);
        if (name is not null)
        {
            return new PersonalEmailIdentityMatch(recipient.ToString(), recipient.Domain, true, true,
                PersonalEmailIdentityMatchType.ExactFullName, PersonalEmailIdentityConfidence.High,
                name.Source, "Directory full name exactly matches the public recipient local-part after normalization.");
        }

        // Fuzzy matching is intentionally conservative: it is visible for human review but must
        // never be treated as a high-confidence ownership claim.
        var fuzzy = candidates
            .Where(candidate => candidate.Normalized.Length >= 8 && normalizedRecipient.Length >= 8)
            .Select(candidate => new { Candidate = candidate, Distance = LevenshteinDistance(candidate.Normalized, normalizedRecipient) })
            .Where(x => x.Distance <= AllowedDistance(x.Candidate.Normalized.Length, normalizedRecipient.Length))
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Candidate.Kind == SenderCandidateKind.FullName)
            .FirstOrDefault();
        if (fuzzy is not null)
        {
            return new PersonalEmailIdentityMatch(recipient.ToString(), recipient.Domain, true, true,
                PersonalEmailIdentityMatchType.ConservativeFuzzy, PersonalEmailIdentityConfidence.Low,
                fuzzy.Candidate.Source, $"Normalized local-parts differ by {fuzzy.Distance} character(s); human review is required.");
        }

        return new PersonalEmailIdentityMatch(recipient.ToString(), recipient.Domain, true, false,
            PersonalEmailIdentityMatchType.None, PersonalEmailIdentityConfidence.None, null,
            "Recipient uses a recognized personal-mail domain, but no reliable sender identity match was found.");
    }

    private static List<SenderCandidate> BuildSenderCandidates(string? senderEmail, string? loginName, string? fullName)
    {
        var candidates = new List<SenderCandidate>();
        AddMailboxLocalPart(candidates, senderEmail, SenderCandidateKind.Email);
        AddLogin(candidates, loginName);
        AddFullName(candidates, fullName);
        return candidates
            .Where(candidate => candidate.Normalized.Length >= 3)
            .DistinctBy(candidate => (candidate.Kind, candidate.Normalized), SenderCandidateComparer.Instance)
            .ToList();
    }

    private static void AddMailboxLocalPart(List<SenderCandidate> candidates, string? value, SenderCandidateKind kind)
    {
        var mailbox = ExtractMailboxes(value).FirstOrDefault();
        if (mailbox is null) return;
        var normalized = NormalizeLocalPart(mailbox.LocalPart, mailbox.Domain);
        if (normalized.Length > 0) candidates.Add(new SenderCandidate(normalized, mailbox.LocalPart, kind));
    }

    private static void AddLogin(List<SenderCandidate> candidates, string? loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName)) return;
        var value = loginName.Trim();
        var separator = Math.Max(value.LastIndexOf('\\'), value.LastIndexOf('/'));
        if (separator >= 0 && separator < value.Length - 1) value = value[(separator + 1)..];
        if (value.Contains('@'))
        {
            AddMailboxLocalPart(candidates, value, SenderCandidateKind.Login);
            return;
        }

        var normalized = NormalizeIdentity(value);
        if (normalized.Length > 0) candidates.Add(new SenderCandidate(normalized, value, SenderCandidateKind.Login));
    }

    private static void AddFullName(List<SenderCandidate> candidates, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return;
        var normalized = NormalizeIdentity(fullName);
        if (normalized.Length >= 6) candidates.Add(new SenderCandidate(normalized, fullName.Trim(), SenderCandidateKind.FullName));
    }

    private static IEnumerable<Mailbox> ExtractMailboxes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        foreach (Match match in MailboxRegex.Matches(value))
        {
            var local = match.Groups["local"].Value;
            var domain = match.Groups["domain"].Value.TrimEnd('.').ToLowerInvariant();
            if (local.Length > 0 && domain.Length > 0) yield return new Mailbox(local, domain);
        }
    }

    private static string NormalizeLocalPart(string localPart, string domain)
    {
        var effective = localPart.Trim();
        if (domain.Equals("gmail.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("googlemail.com", StringComparison.OrdinalIgnoreCase))
        {
            var plus = effective.IndexOf('+');
            if (plus >= 0) effective = effective[..plus];
        }
        return NormalizeIdentity(effective);
    }

    private static string NormalizeIdentity(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ğ' => 'g',
                'ç' => 'c',
                'ö' => 'o',
                'ü' => 'u',
                _ when char.IsLetterOrDigit(character) => character,
                _ => '\0'
            });
        }
        return builder.ToString().Replace("\0", string.Empty, StringComparison.Ordinal);
    }

    private static int AllowedDistance(int leftLength, int rightLength)
    {
        var shortest = Math.Min(leftLength, rightLength);
        if (shortest < 8) return 0;
        if (shortest <= 10) return 1;
        return 2;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[right.Length];
    }

    private sealed record Mailbox(string LocalPart, string Domain)
    {
        public override string ToString() => $"{LocalPart}@{Domain}";
    }

    private sealed record SenderCandidate(string Normalized, string Source, SenderCandidateKind Kind);

    private enum SenderCandidateKind { Email, Login, FullName }

    private sealed class SenderCandidateComparer : IEqualityComparer<(SenderCandidateKind Kind, string Normalized)>
    {
        public static readonly SenderCandidateComparer Instance = new();
        public bool Equals((SenderCandidateKind Kind, string Normalized) x, (SenderCandidateKind Kind, string Normalized) y) =>
            x.Kind == y.Kind && StringComparer.Ordinal.Equals(x.Normalized, y.Normalized);
        public int GetHashCode((SenderCandidateKind Kind, string Normalized) value) =>
            HashCode.Combine(value.Kind, StringComparer.Ordinal.GetHashCode(value.Normalized));
    }
}

public enum PersonalEmailIdentityMatchType
{
    None,
    ExactLocalPart,
    ExactFullName,
    ConservativeFuzzy
}

public enum PersonalEmailIdentityConfidence
{
    None,
    Low,
    High
}

public sealed record PersonalEmailIdentityMatch(
    string Recipient,
    string Domain,
    bool IsPersonalDomain,
    bool HasIdentityMatch,
    PersonalEmailIdentityMatchType MatchType,
    PersonalEmailIdentityConfidence Confidence,
    string? MatchedSenderValue,
    string Evidence);
