using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Detects users that should be queried / emailed for the week based on three
/// time-window incident patterns.
/// </summary>
public class WeeklyFlagService : IWeeklyFlagService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IExternalUserDirectoryService _externalUserDirectory;

    public WeeklyFlagService(
        IIncidentRepository incidentRepository,
        IExternalUserDirectoryService externalUserDirectory)
    {
        _incidentRepository = incidentRepository;
        _externalUserDirectory = externalUserDirectory;
    }

    // --- Detection thresholds (could later be moved to system_settings) ---
    private const string PersonalEmailPolicyName = "KT Şüpheli Kullanıcı Aktivitesi";
    private static readonly TimeSpan PersonalEmailWindow = TimeSpan.FromMinutes(10);
    private const int PersonalEmailMinCount = 2;

    private static readonly TimeSpan HighVolumeWindow = TimeSpan.FromMinutes(30);
    private const int HighVolumeMinCount = 10;

    private const int MassiveMatchesThreshold = 500;
    private const int MassiveMatchesConsecutive = 2;

    private const int MaxSampleIncidents = 10;

    public async Task<WeeklyFlagsResult> GetWeeklyFlagsAsync(int days)
    {
        if (days <= 0) days = 7;
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var incidents = await _incidentRepository.GetIncidentsForWeeklyFlagsAsync(startDate, endDate);

        // Group by a resolved identifier instead of raw UserEmail: the importer falls back to
        // "unknown" for UserEmail, which would otherwise merge many different people into one
        // group and show a random person's name for all of them.
        var byUser = incidents
            .Select(i => new { Key = ResolveUserKey(i), Incident = i })
            .Where(x => x.Key != null)
            .GroupBy(x => x.Key!, x => x.Incident, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new WeeklyFlagsResult();

        foreach (var group in byUser)
        {
            var userIncidents = group.OrderBy(i => i.Timestamp).ToList();

            // --- Section 1: personal email senders (specific policy, 10 min window, >= 2) ---
            var policyHits = userIncidents
                .Where(i => !string.IsNullOrWhiteSpace(i.Policy) &&
                            i.Policy!.Trim().Equals(PersonalEmailPolicyName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (HasWindow(policyHits, PersonalEmailWindow, PersonalEmailMinCount))
            {
                result.PersonalEmailSenders.Add(BuildUser(group.Key, policyHits, policyHits.Count));
            }

            // --- Section 2: high volume (30 min window, >= 10 incidents) ---
            var maxWindowCount = MaxWindowCount(userIncidents, HighVolumeWindow);
            if (maxWindowCount >= HighVolumeMinCount)
            {
                result.HighVolume.Add(BuildUser(group.Key, userIncidents, maxWindowCount));
            }

            // --- Section 3: consecutive 500+ max matches incidents ---
            var massiveHits = ConsecutiveMassiveMatches(userIncidents);
            if (massiveHits.Count > 0)
            {
                result.MassiveMatches.Add(BuildUser(group.Key, massiveHits, massiveHits.Count));
            }
        }

        result.PersonalEmailSenders = result.PersonalEmailSenders.OrderByDescending(u => u.TriggerCount).ToList();
        result.HighVolume = result.HighVolume.OrderByDescending(u => u.TriggerCount).ToList();
        result.MassiveMatches = result.MassiveMatches.OrderByDescending(u => u.TriggerCount).ToList();

        result.PersonalEmailSenders = await EnrichUsersAsync(result.PersonalEmailSenders);
        result.HighVolume = await EnrichUsersAsync(result.HighVolume);
        result.MassiveMatches = await EnrichUsersAsync(result.MassiveMatches);

        return result;
    }

    /// <summary>True if any sliding window of the given length contains at least minCount items.</summary>
    private static bool HasWindow(List<Incident> sorted, TimeSpan window, int minCount)
    {
        if (sorted.Count < minCount) return false;
        var times = sorted.Select(i => i.Timestamp).OrderBy(t => t).ToList();
        int left = 0;
        for (int right = 0; right < times.Count; right++)
        {
            while (times[right] - times[left] > window) left++;
            if (right - left + 1 >= minCount) return true;
        }
        return false;
    }

    /// <summary>Maximum number of items contained in any sliding window of the given length.</summary>
    private static int MaxWindowCount(List<Incident> sorted, TimeSpan window)
    {
        if (sorted.Count == 0) return 0;
        var times = sorted.Select(i => i.Timestamp).OrderBy(t => t).ToList();
        int left = 0, max = 0;
        for (int right = 0; right < times.Count; right++)
        {
            while (times[right] - times[left] > window) left++;
            max = Math.Max(max, right - left + 1);
        }
        return max;
    }

    /// <summary>
    /// Returns incidents that take part in a run of <see cref="MassiveMatchesConsecutive"/>
    /// consecutive (time-ordered) incidents each with MaxMatches >= threshold.
    /// </summary>
    private static List<Incident> ConsecutiveMassiveMatches(List<Incident> sorted)
    {
        var flagged = new List<Incident>();
        int run = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            if (EffectiveMaxMatches(sorted[i]) >= MassiveMatchesThreshold)
            {
                run++;
                if (run >= MassiveMatchesConsecutive)
                {
                    // include the incident that just completed the run, plus the previous ones in the run
                    for (int back = i - MassiveMatchesConsecutive + 1; back <= i; back++)
                    {
                        if (!flagged.Contains(sorted[back])) flagged.Add(sorted[back]);
                    }
                }
            }
            else
            {
                run = 0;
            }
        }
        return flagged;
    }

    /// <summary>
    /// Best available identifier for grouping: UserEmail unless empty/"unknown",
    /// then EmailAddress, then LoginName. Null when no usable identifier exists.
    /// </summary>
    private static string? ResolveUserKey(Incident i)
    {
        if (!string.IsNullOrWhiteSpace(i.UserEmail) &&
            !i.UserEmail.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return i.UserEmail;
        if (!string.IsNullOrWhiteSpace(i.EmailAddress)) return i.EmailAddress;
        if (!string.IsNullOrWhiteSpace(i.LoginName)) return i.LoginName;
        return null;
    }

    private static WeeklyFlagUserDto BuildUser(string userEmail, List<Incident> incidents, int triggerCount)
    {
        // Prefer the most recent non-empty values so a stray/old record cannot
        // override the user's current name or contact address.
        var newestFirst = incidents.OrderByDescending(i => i.Timestamp).ToList();
        var contact = newestFirst.Select(i => i.EmailAddress).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)) ?? userEmail;
        var samples = newestFirst
            .Take(MaxSampleIncidents)
            .Select(i => new WeeklyFlagIncidentDto(i.Timestamp, i.Policy, EffectiveMaxMatches(i), i.Destination, i.Channel))
            .ToList();

        return new WeeklyFlagUserDto(
            UserEmail: userEmail,
            FullName: null,
            Team: newestFirst.Select(i => i.Team).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
            ContactEmail: contact,
            TriggerCount: triggerCount,
            FirstSeen: incidents.Min(i => i.Timestamp),
            LastSeen: incidents.Max(i => i.Timestamp),
            SampleIncidents: samples);
    }

    private async Task<List<WeeklyFlagUserDto>> EnrichUsersAsync(List<WeeklyFlagUserDto> users)
    {
        var enriched = new List<WeeklyFlagUserDto>(users.Count);
        foreach (var user in users)
        {
            var profile = await _externalUserDirectory.ResolveUserAsync(user.UserEmail);
            if (profile == null)
            {
                enriched.Add(user);
                continue;
            }

            enriched.Add(user with
            {
                FullName = FirstNonEmpty(profile.FullName, user.FullName),
                Team = FirstNonEmpty(profile.Department, user.Team),
                ContactEmail = FirstNonEmpty(profile.Email, user.ContactEmail, user.UserEmail) ?? user.ContactEmail,
                Gender = FirstNonEmpty(profile.Gender, user.Gender)
            });
        }

        return enriched;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int EffectiveMaxMatches(Incident incident)
    {
        if (incident.MaxMatches > 0) return incident.MaxMatches;
        return ViolationTriggerParser.ExtractMaxMatches(incident.ViolationTriggers);
    }
}
