namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

internal sealed record ExcitationPair(string From, string To, int Observations, double Lift);

internal sealed record GapStatistics(
    int Count, double P10, double P25, double Median, double P75, double P90,
    double ShareUnder5Min, double ShareUnder60Min, double SuggestedWindowMinutes);

/// <summary>
/// Learns which event types make which other event types more likely to follow soon — a
/// discrete-mark excitation matrix, estimated by counting.
///
/// This is the learned replacement for hand-written scenario rules. Rather than someone declaring
/// "removable media followed by external mail is exfiltration", the matrix measures
/// <c>lift(a→b) = P(b within Δ | a) / P(b within Δ)</c> across the whole organisation's history.
/// Every pair that is genuinely unusual-but-connected surfaces, including the ones nobody thought
/// to enumerate. A pair that turns out to be routine gets a lift near 1 and changes nothing.
///
/// Δ itself is read off the observed inter-event gap distribution rather than decreed, so the
/// "session" notion stops being a magic number.
/// </summary>
internal sealed class ExcitationModel
{
    private readonly SurprisalOptions _options;
    private readonly Dictionary<(string From, string To), int> _pairCounts = new();
    private readonly Dictionary<string, int> _fromCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _markCounts = new(StringComparer.Ordinal);
    private int _totalEvents;
    private int _totalPairs;

    public ExcitationModel(SurprisalOptions options) => _options = options;

    public GapStatistics? Gaps { get; private set; }

    /// <summary>The mark an event carries in the matrix. Channel is coarse enough to estimate well
    /// (7 values → 49 cells) while still expressing "moved to a different tool".</summary>
    public static string MarkOf(EventToken e) => e.Channel;

    public void Fit(IReadOnlyList<EventToken> baseline)
    {
        Gaps = ComputeGaps(baseline);
        var window = _options.ExcitationWindowMinutes;

        foreach (var perUser in baseline.GroupBy(e => e.User, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = perUser.OrderBy(e => e.Timestamp).ToList();
            _totalEvents += ordered.Count;

            foreach (var e in ordered)
                _markCounts[MarkOf(e)] = _markCounts.GetValueOrDefault(MarkOf(e)) + 1;

            for (int i = 0; i < ordered.Count; i++)
            {
                var from = MarkOf(ordered[i]);
                var sawFollower = false;

                for (int j = i + 1; j < ordered.Count; j++)
                {
                    var minutes = (ordered[j].Timestamp - ordered[i].Timestamp).TotalMinutes;
                    if (minutes > window) break;

                    var to = MarkOf(ordered[j]);
                    _pairCounts[(from, to)] = _pairCounts.GetValueOrDefault((from, to)) + 1;
                    _totalPairs++;
                    sawFollower = true;
                }

                if (sawFollower) _fromCounts[from] = _fromCounts.GetValueOrDefault(from) + 1;
            }
        }
    }

    /// <summary>
    /// Multiplier for an event given what preceded it inside the window. Returns 1.0 (no effect)
    /// when there is no predecessor, when the pair is too rare to trust, or when the pair is
    /// routine — so the model stays silent unless the data supports speaking.
    /// </summary>
    public (double Multiplier, string? ExcitedBy) MultiplierFor(EventToken current, IReadOnlyList<EventToken> recent)
    {
        double best = 1.0;
        string? by = null;
        var currentMark = MarkOf(current);

        foreach (var prior in recent)
        {
            var minutes = (current.Timestamp - prior.Timestamp).TotalMinutes;
            if (minutes < 0 || minutes > _options.ExcitationWindowMinutes) continue;

            var priorMark = MarkOf(prior);

            // Same-channel repetition is burstiness, not combination. Two emails in a row is the
            // single most common pattern in the data (and therefore has a high lift), but it says
            // nothing about someone reaching for a second capability. Volume and accumulation
            // already account for it; letting it multiply here would boost nearly half of all
            // events and drown out the cross-channel signal this term exists to find.
            if (string.Equals(priorMark, currentMark, StringComparison.OrdinalIgnoreCase)) continue;

            var lift = LiftOf(priorMark, currentMark);
            if (lift <= best) continue;

            best = lift;
            by = priorMark;
        }

        return (Math.Clamp(best, 1.0, _options.MaxExcitationMultiplier), by);
    }

    public double LiftOf(string from, string to)
    {
        var pairs = _pairCounts.GetValueOrDefault((from, to));
        if (pairs < _options.MinPairObservations) return 1.0;

        var fromTotal = _pairCounts.Where(kv => kv.Key.From == from).Sum(kv => kv.Value);
        if (fromTotal <= 0 || _totalPairs <= 0) return 1.0;

        var conditional = (double)pairs / fromTotal;              // P(to | from, within Δ)
        var marginal = (double)_markCounts.GetValueOrDefault(to) / Math.Max(_totalEvents, 1);
        if (marginal <= 0) return 1.0;

        return conditional / marginal;
    }

    public IReadOnlyList<ExcitationPair> AllPairs() =>
        _pairCounts
            .Select(kv => new ExcitationPair(kv.Key.From, kv.Key.To, kv.Value, LiftOf(kv.Key.From, kv.Key.To)))
            .OrderByDescending(p => p.Lift)
            .ThenByDescending(p => p.Observations)
            .ToList();

    public IReadOnlyList<string> Marks() =>
        _markCounts.Keys.OrderByDescending(k => _markCounts[k]).ToList();

    public int PairObservations(string from, string to) => _pairCounts.GetValueOrDefault((from, to));

    // ── Gap distribution — where Δ should come from ──────────────────────────

    public static GapStatistics ComputeGaps(IReadOnlyList<EventToken> events)
    {
        var gaps = new List<double>();

        foreach (var perUser in events.GroupBy(e => e.User, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = perUser.OrderBy(e => e.Timestamp).ToList();
            for (int i = 1; i < ordered.Count; i++)
                gaps.Add((ordered[i].Timestamp - ordered[i - 1].Timestamp).TotalMinutes);
        }

        if (gaps.Count == 0)
            return new GapStatistics(0, 0, 0, 0, 0, 0, 0, 0, 60);

        gaps.Sort();
        double Q(double p) => gaps[Math.Clamp((int)(gaps.Count * p), 0, gaps.Count - 1)];

        return new GapStatistics(
            gaps.Count, Q(0.10), Q(0.25), Q(0.50), Q(0.75), Q(0.90),
            gaps.Count(g => g < 5) / (double)gaps.Count,
            gaps.Count(g => g < 60) / (double)gaps.Count,
            SuggestWindow(gaps));
    }

    /// <summary>
    /// Fits a two-component mixture on log-gaps and returns the crossover. Inter-event gaps are
    /// bimodal — a tight burst mode and a "next time he did something" mode — and the crossover is
    /// the natural boundary between them. Falls back to p75 when the data is not bimodal.
    /// </summary>
    private static double SuggestWindow(List<double> sortedGaps)
    {
        var logs = sortedGaps.Where(g => g > 0).Select(g => Math.Log(g)).ToList();
        if (logs.Count < 50) return sortedGaps[Math.Min(sortedGaps.Count - 1, (int)(sortedGaps.Count * 0.75))];

        // 1-D 2-means on log gaps, then take the midpoint between the two centres.
        double lo = logs[(int)(logs.Count * 0.10)], hi = logs[(int)(logs.Count * 0.90)];
        for (int iter = 0; iter < 60; iter++)
        {
            var mid = (lo + hi) / 2;
            var low = logs.Where(g => g <= mid).ToList();
            var high = logs.Where(g => g > mid).ToList();
            if (low.Count == 0 || high.Count == 0) break;

            var newLo = low.Average();
            var newHi = high.Average();
            if (Math.Abs(newLo - lo) < 1e-9 && Math.Abs(newHi - hi) < 1e-9) break;
            lo = newLo; hi = newHi;
        }

        var boundary = Math.Exp((lo + hi) / 2);
        return Math.Clamp(boundary, 5, 720);
    }
}
