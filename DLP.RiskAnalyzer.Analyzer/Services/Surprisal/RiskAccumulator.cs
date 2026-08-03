namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

internal sealed record UserRisk(
    string User,
    string Department,
    string Cluster,
    double Score,
    int ScoredEvents,
    int BaselineEvents,
    double PersonalWeight,
    DateTime LastEvent,
    IReadOnlyList<EventSurprisal> TopEvents,
    IReadOnlyDictionary<string, double> FieldContribution);

/// <summary>
/// Accumulates event scores with exponential decay instead of summing a fixed window.
///
/// A hard 7-day window has a cliff: an event that mattered on day 7 contributes nothing on day 8,
/// so a user's score can collapse overnight without their behaviour changing. Decay keeps the score
/// continuous and current, makes slow drip exfiltration visible (many small events that never all
/// fall inside one window still accumulate), and lets consecutive events compound naturally.
/// </summary>
internal sealed class RiskAccumulator
{
    private readonly SurprisalOptions _options;

    public RiskAccumulator(SurprisalOptions options) => _options = options;

    public IReadOnlyList<UserRisk> Accumulate(
        IReadOnlyList<EventSurprisal> scored,
        IReadOnlyDictionary<string, string> clusterOf,
        IReadOnlyDictionary<string, int> baselineCounts,
        DateTime asOf)
    {
        var lambda = Math.Log(2) / Math.Max(_options.RiskHalfLifeDays, 0.5);
        var results = new List<UserRisk>();

        foreach (var group in scored.GroupBy(s => s.Token.User, StringComparer.OrdinalIgnoreCase))
        {
            var events = group.ToList();
            double total = 0;
            var fieldContribution = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var e in events)
            {
                var ageDays = Math.Max(0, (asOf - e.Token.Timestamp).TotalDays);
                var decay = Math.Exp(-lambda * ageDays);
                total += e.Score * decay;

                foreach (var f in e.Fields)
                    fieldContribution[f.Field] =
                        fieldContribution.GetValueOrDefault(f.Field) + f.Bits * decay * e.Consequence * e.ExcitationMultiplier;
            }

            var user = group.Key;
            results.Add(new UserRisk(
                user,
                events[0].Token.Department,
                clusterOf.GetValueOrDefault(user, "unassigned"),
                total,
                events.Count,
                baselineCounts.GetValueOrDefault(user),
                events.Max(e => e.Fields.Max(f => f.PersonalWeight)),
                events.Max(e => e.Token.Timestamp),
                events.OrderByDescending(e => e.Score).Take(5).ToList(),
                fieldContribution));
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }
}
