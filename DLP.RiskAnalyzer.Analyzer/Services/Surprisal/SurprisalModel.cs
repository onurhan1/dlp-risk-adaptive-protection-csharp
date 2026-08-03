namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

/// <summary>
/// Per-field contribution to an event's surprisal, kept so the reason layer can say
/// <em>which part</em> of the event was unexpected and against <em>which</em> baseline.
/// </summary>
internal sealed record FieldSurprisal(
    string Field,
    string Value,
    string? ConditionedOn,
    string? ConditionValue,
    double Probability,
    double Bits,           // -ln P, weighted
    double RawBits,        // -ln P, unweighted
    double PersonalWeight, // λu at scoring time — how much of this was "his own history"
    double ClusterWeight,
    double OrgWeight,
    double PersonalProbability,
    double ClusterProbability,
    double OrgProbability,
    int PersonalObservations);

internal sealed record EventSurprisal(
    EventToken Token,
    double TotalBits,
    double Consequence,
    double ExcitationMultiplier,
    string? ExcitedBy,
    double Score,
    IReadOnlyList<FieldSurprisal> Fields);

/// <summary>
/// Hierarchical conditional-probability model — a Katz-style backoff estimator over DLP event
/// fields, fitted from history and scored by surprisal.
///
/// <code>
/// P̂(value | user) = λu·P_user + λg·P_cluster + λo·P_org
/// λu = n_user / (n_user + K)
/// surprisal(event) = Σ_field  w_field · −ln P̂(field value | context)
/// </code>
///
/// Nothing in here is a rule: every probability is counted from the organisation's own history.
/// A user whose department routinely handles a data class produces a high P̂ for it and therefore
/// almost no surprisal — the "expected for this role" behaviour falls out of the estimator rather
/// than being declared. Conversely a channel this user has never touched produces a low personal
/// term, and λu decides how much that should count given how much history he actually has.
/// </summary>
internal sealed class SurprisalModel
{
    private readonly SurprisalOptions _options;

    // field -> conditionValue -> value -> weighted count
    private readonly Dictionary<string, Counter> _org = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Counter>> _cluster = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Counter>> _user = new(StringComparer.Ordinal);

    private IReadOnlyDictionary<string, string> _clusterOf = new Dictionary<string, string>();

    public SurprisalModel(SurprisalOptions options)
    {
        _options = options;
        foreach (var field in EventToken.Fields.All)
        {
            _org[field] = new Counter();
            _cluster[field] = new(StringComparer.Ordinal);
            _user[field] = new(StringComparer.Ordinal);
        }
    }

    /// <summary>Vocabulary and counts, exposed for the diagnostic report.</summary>
    public IReadOnlyDictionary<string, Counter> OrgCounts => _org;

    public IReadOnlyDictionary<string, string> ClusterAssignments => _clusterOf;

    // ── Fit ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Accumulates counts from baseline events. Each event is weighted by recency, so a behaviour
    /// someone stopped three months ago fades instead of anchoring their baseline forever.
    /// </summary>
    public void Fit(IEnumerable<EventToken> baseline, IReadOnlyDictionary<string, string> clusterOf, DateTime asOf)
    {
        _clusterOf = clusterOf;
        var lambda = Math.Log(2) / Math.Max(_options.BaselineRecencyHalfLifeDays, 0.5);

        foreach (var e in baseline)
        {
            var ageDays = Math.Max(0, (asOf - e.Timestamp).TotalDays);
            var w = Math.Exp(-lambda * ageDays);
            if (w < 1e-6) continue;

            var cluster = clusterOf.GetValueOrDefault(e.User, "unassigned");

            foreach (var field in EventToken.Fields.All)
            {
                var cond = ConditionValueFor(field, e);
                var value = e.Value(field);

                _org[field].Add(cond, value, w);

                if (!_cluster[field].TryGetValue(cluster, out var cc))
                    _cluster[field][cluster] = cc = new Counter();
                cc.Add(cond, value, w);

                if (!_user[field].TryGetValue(e.User, out var uc))
                    _user[field][e.User] = uc = new Counter();
                uc.Add(cond, value, w);
            }
        }
    }

    private string ConditionValueFor(string field, EventToken e) =>
        _options.FieldConditioning.TryGetValue(field, out var on) ? e.Value(on) : Counter.NoCondition;

    // ── Score ────────────────────────────────────────────────────────────────

    public EventSurprisal Score(EventToken e, double excitationMultiplier = 1.0, string? excitedBy = null)
    {
        var cluster = _clusterOf.GetValueOrDefault(e.User, "unassigned");
        var fields = new List<FieldSurprisal>(EventToken.Fields.All.Length);
        double total = 0;

        foreach (var field in EventToken.Fields.All)
        {
            var cond = ConditionValueFor(field, e);
            var condOn = _options.FieldConditioning.GetValueOrDefault(field);
            var value = e.Value(field);

            var orgCounter = _org[field];
            var clusterCounter = _cluster[field].GetValueOrDefault(cluster);
            var userCounter = _user[field].GetValueOrDefault(e.User);

            var nUser = userCounter?.Total(cond) ?? 0;
            var nCluster = clusterCounter?.Total(cond) ?? 0;

            // λu is a function of how much history this user actually has. No history -> no weight,
            // which is the honest behaviour: the term is absent, not zero-valued.
            var lambdaU = nUser / (nUser + _options.PersonalBackoffK);
            var lambdaG = (1 - lambdaU) * (nCluster / (nCluster + _options.ClusterBackoffK));
            var lambdaO = 1 - lambdaU - lambdaG;

            var pUser = userCounter?.Probability(cond, value, 0) ?? 0;
            var pCluster = clusterCounter?.Probability(cond, value, 0) ?? 0;
            var pOrg = orgCounter.Probability(cond, value, _options.OrgSmoothingAlpha);

            var p = Math.Max(lambdaU * pUser + lambdaG * pCluster + lambdaO * pOrg, 1e-12);
            var rawBits = -Math.Log(p);
            var weight = _options.FieldWeights.GetValueOrDefault(field, 1.0);
            var bits = rawBits * weight;
            total += bits;

            fields.Add(new FieldSurprisal(
                field, value, condOn, condOn is null ? null : cond,
                p, bits, rawBits,
                lambdaU, lambdaG, lambdaO,
                pUser, pCluster, pOrg,
                (int)Math.Round(nUser)));
        }

        var consequence = Consequence(e);
        var excitation = Math.Clamp(excitationMultiplier, 1.0, _options.MaxExcitationMultiplier);

        return new EventSurprisal(
            e, total, consequence, excitation, excitedBy,
            total * consequence * excitation,
            fields);
    }

    /// <summary>
    /// The only hand-set part of the whole model: did the data actually leave, how sensitive was
    /// the destination, and how much matched. Surprisal says "unexpected"; this says "and it
    /// mattered". Without it a rare-but-blocked event would outrank a routine-but-successful leak.
    /// </summary>
    public double Consequence(EventToken e)
    {
        var egress = e.Egressed ? _options.EgressMultiplier : _options.BlockedMultiplier;
        var trust = _options.DestinationTrust.GetValueOrDefault(e.DestinationClass, 1.0);
        var volume = 1.0 + _options.MatchVolumeWeight * Math.Log(1 + Math.Max(0, e.MaxMatches));
        return egress * trust * volume;
    }

    // ── Counting ─────────────────────────────────────────────────────────────

    /// <summary>Weighted counts of value-given-condition, with add-alpha smoothing on demand.</summary>
    internal sealed class Counter
    {
        public const string NoCondition = "*";

        private readonly Dictionary<string, Dictionary<string, double>> _counts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _totals = new(StringComparer.Ordinal);
        private readonly HashSet<string> _vocabulary = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Vocabulary => _vocabulary;
        public IReadOnlyDictionary<string, Dictionary<string, double>> Counts => _counts;

        public void Add(string condition, string value, double weight)
        {
            if (!_counts.TryGetValue(condition, out var inner))
                _counts[condition] = inner = new Dictionary<string, double>(StringComparer.Ordinal);

            inner[value] = inner.GetValueOrDefault(value) + weight;
            _totals[condition] = _totals.GetValueOrDefault(condition) + weight;
            _vocabulary.Add(value);
        }

        public double Total(string condition) => _totals.GetValueOrDefault(condition);

        public double Probability(string condition, string value, double alpha)
        {
            var total = _totals.GetValueOrDefault(condition);
            var count = _counts.TryGetValue(condition, out var inner) ? inner.GetValueOrDefault(value) : 0;

            if (alpha <= 0) return total <= 0 ? 0 : count / total;

            // +1 on the vocabulary reserves mass for a value never seen before, so an unfamiliar
            // policy name after a policy-set change degrades gracefully instead of exploding.
            var denominator = total + alpha * (_vocabulary.Count + 1);
            return denominator <= 0 ? 0 : (count + alpha) / denominator;
        }
    }
}
