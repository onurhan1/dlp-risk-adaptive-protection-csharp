using System.Globalization;
using System.Text;

namespace DLP.RiskAnalyzer.Analyzer.Services.Surprisal;

/// <summary>
/// Renders everything needed to tune the model into one markdown document.
///
/// The model is unsupervised, so there is no accuracy number to optimise against. Tuning happens by
/// looking at what it actually says: which fields carry the surprisal, whether the clusters are
/// coherent, whether the top-ranked events look risky to a human, and where the estimator is thin.
/// This report is that view — it is meant to be read, argued with, and fed back in.
/// </summary>
internal static class SurprisalDiagnostics
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Render(SurprisalRunResult r, SurprisalOptions o)
    {
        var sb = new StringBuilder();

        Header(sb, r, o);
        DataShape(sb, r);
        FieldVocabularies(sb, r);
        BaselineAvailability(sb, r, o);
        Clusters(sb, r);
        GapsAndExcitation(sb, r, o);
        SurprisalDistribution(sb, r);
        TopEvents(sb, r, o);
        TopUsers(sb, r, o);
        HealthWarnings(sb, r, o);
        Configuration(sb, o);

        return sb.ToString();
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private static void Header(StringBuilder sb, SurprisalRunResult r, SurprisalOptions o)
    {
        sb.AppendLine("# Davranışsal Sürpriz Modeli — Teşhis Raporu");
        sb.AppendLine();
        sb.AppendLine($"- Üretim zamanı: `{r.GeneratedAt:yyyy-MM-dd HH:mm:ss}` UTC");
        sb.AppendLine($"- Taban penceresi: son **{o.BaselineWindowDays} gün** · Skorlama penceresi: son **{o.ScoreWindowDays} gün**");
        sb.AppendLine($"- Taban olayı: **{r.BaselineTokens.Count:N0}** · Skorlanan olay: **{r.ScoredEvents.Count:N0}**");
        sb.AppendLine();
        sb.AppendLine("> Bu rapor modeli geliştirmek için üretildi. Bölüm 9'daki uyarılar ve bölüm 6-7'deki");
        sb.AppendLine("> dağılımlar, hangi alan ağırlıklarının ve eşiklerin değişmesi gerektiğini gösterir.");
        sb.AppendLine();
    }

    private static void DataShape(StringBuilder sb, SurprisalRunResult r)
    {
        sb.AppendLine("## 1. Veri şekli");
        sb.AppendLine();

        var all = r.BaselineTokens;
        if (all.Count == 0) { sb.AppendLine("_Taban veri yok._"); sb.AppendLine(); return; }

        var users = all.Select(t => t.User).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var depts = all.Select(t => t.Department).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var span = (all.Max(t => t.Timestamp) - all.Min(t => t.Timestamp)).TotalDays;

        var perUser = all.GroupBy(t => t.User, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Count()).OrderByDescending(x => x).ToList();

        sb.AppendLine("| Ölçüm | Değer |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Zaman aralığı | {all.Min(t => t.Timestamp):yyyy-MM-dd} → {all.Max(t => t.Timestamp):yyyy-MM-dd} ({span:F0} gün) |");
        sb.AppendLine($"| Olay | {all.Count:N0} |");
        sb.AppendLine($"| Kullanıcı | {users:N0} |");
        sb.AppendLine($"| Departman | {depts:N0} |");
        sb.AppendLine($"| Olay/gün (ortalama) | {all.Count / Math.Max(span, 1):F0} |");
        sb.AppendLine($"| Olay/kullanıcı — medyan / p90 / maks | {Pct(perUser, 0.5)} / {Pct(perUser, 0.1)} / {perUser.FirstOrDefault()} |");
        sb.AppendLine($"| Veri dışarı çıkan olay (egress) | {all.Count(t => t.Egressed):N0} (%{all.Count(t => t.Egressed) * 100.0 / all.Count:F1}) |");
        sb.AppendLine();

        // Department size distribution decides whether an org-chart peer group could ever work.
        var deptSizes = all.GroupBy(t => t.Department, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Select(x => x.User).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .OrderByDescending(x => x).ToList();
        var usersInUsableDepts = all.GroupBy(t => t.Department, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.User).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 3)
            .Sum(g => g.Select(x => x.User).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        sb.AppendLine($"**Departman büyüklüğü** — medyan {Pct(deptSizes, 0.5)}, maks {deptSizes.FirstOrDefault()}. ");
        sb.AppendLine($"≥3 kişilik departmanlardaki kullanıcı: **{usersInUsableDepts:N0} / {users:N0}** " +
                      $"(%{usersInUsableDepts * 100.0 / Math.Max(users, 1):F0}). ");
        sb.AppendLine("Bu oran düşükse org-şeması tabanlı peer karşılaştırması çalışmıyor demektir — davranışsal kümeleme (bölüm 4) onun yerini alır.");
        sb.AppendLine();
    }

    private static void FieldVocabularies(StringBuilder sb, SurprisalRunResult r)
    {
        sb.AppendLine("## 2. Alan sözlükleri ve kapsama");
        sb.AppendLine();
        sb.AppendLine("Kardinalite tahmin edilebilirliği belirler: çok yüksekse her değer 'nadir' çıkar ve alan bilgi taşımaz.");
        sb.AppendLine();
        sb.AppendLine("| Alan | Benzersiz değer | En sık 5 değer (pay) | `unknown` payı |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var field in EventToken.Fields.All)
        {
            var values = r.BaselineTokens.Select(t => t.Value(field)).ToList();
            if (values.Count == 0) continue;

            var groups = values.GroupBy(v => v, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count()).ToList();
            var top = string.Join(", ", groups.Take(5)
                .Select(g => $"`{Trim(g.Key, 34)}` %{g.Count() * 100.0 / values.Count:F0}"));
            var unknown = values.Count(v => v == EventToken.Unknown) * 100.0 / values.Count;

            sb.AppendLine($"| `{field}` | {groups.Count} | {top} | %{unknown:F1} |");
        }
        sb.AppendLine();

        var classifiers = r.BaselineTokens.SelectMany(t => t.AllClassifiers).ToList();
        if (classifiers.Count > 0)
        {
            sb.AppendLine("**Sınıflandırıcı sözlüğü** (`violation_triggers`'tan çıkarılan gerçek veri sınıfı):");
            sb.AppendLine();
            sb.AppendLine("| Sınıflandırıcı | Olay |");
            sb.AppendLine("|---|---|");
            foreach (var g in classifiers.GroupBy(c => c, StringComparer.Ordinal)
                         .OrderByDescending(g => g.Count()).Take(20))
                sb.AppendLine($"| `{Trim(g.Key, 50)}` | {g.Count():N0} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("> ⚠ **Hiç sınıflandırıcı çıkarılamadı.** `violation_triggers` boş ya da beklenen şemada değil —");
            sb.AppendLine("> veri sınıfı terimi devre dışı. Bir örnek satır paylaşırsanız ayrıştırıcıyı düzeltirim.");
            sb.AppendLine();
        }
    }

    private static void BaselineAvailability(StringBuilder sb, SurprisalRunResult r, SurprisalOptions o)
    {
        sb.AppendLine("## 3. Kişisel taban kullanılabilirliği (λu)");
        sb.AppendLine();
        sb.AppendLine($"`λu = n / (n + {o.PersonalBackoffK:F0})` — kişisel terimin ağırlığı. 0 ise kullanıcının geçmişi hiç sayılmıyor, ");
        sb.AppendLine("ağırlık kümeye ve kuruma gidiyor. Bu dağılım, kişisel terimin bugün gerçekten çalışıp çalışmadığını gösterir.");
        sb.AppendLine();

        var counts = r.BaselineCounts;
        if (counts.Count == 0) { sb.AppendLine("_Veri yok._"); sb.AppendLine(); return; }

        var lambdas = counts.Values.Select(n => n / (n + o.PersonalBackoffK)).OrderByDescending(x => x).ToList();
        var buckets = new (string Label, Func<double, bool> Test)[]
        {
            ("λu ≥ 0,80 (güçlü kişisel taban)", l => l >= 0.80),
            ("0,50 ≤ λu < 0,80", l => l is >= 0.50 and < 0.80),
            ("0,20 ≤ λu < 0,50", l => l is >= 0.20 and < 0.50),
            ("λu < 0,20 (neredeyse tamamen küme/kurum)", l => l < 0.20)
        };

        sb.AppendLine("| Aralık | Kullanıcı | Pay |");
        sb.AppendLine("|---|---|---|");
        foreach (var (label, test) in buckets)
        {
            var n = lambdas.Count(test);
            sb.AppendLine($"| {label} | {n:N0} | %{n * 100.0 / lambdas.Count:F1} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Medyan λu: **{Pct(lambdas, 0.5, 2)}** · Medyan taban olayı/kullanıcı: **{Pct(counts.Values.OrderByDescending(x => x).ToList(), 0.5)}**");
        sb.AppendLine();
    }

    private static void Clusters(StringBuilder sb, SurprisalRunResult r)
    {
        sb.AppendLine("## 4. Davranışsal kümeler");
        sb.AppendLine();

        var c = r.Clustering;
        if (c.Clusters.Count == 0) { sb.AppendLine("_Kümeleme yapılamadı._"); sb.AppendLine(); return; }

        sb.AppendLine($"Seçilen k = **{c.ChosenK}** (silhouette {c.Silhouette:F3}) · profil boyutu {c.Dimensions.Count}");
        sb.AppendLine();
        sb.AppendLine("Silhouette 0,2'nin altındaysa kümeler zayıf ayrışıyordur — profil alanlarını veya k aralığını değiştirmek gerekir.");
        sb.AppendLine();
        sb.AppendLine("| Küme | Kullanıcı | Olay | Baskın davranış |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var cluster in c.Clusters.OrderByDescending(x => x.UserCount))
        {
            var traits = string.Join(", ", cluster.TopTraits.Select(t => $"`{Trim(t.Label, 32)}` %{t.Share * 100:F0}"));
            sb.AppendLine($"| `{cluster.Id}` | {cluster.UserCount} | {cluster.EventCount:N0} | {traits} |");
        }
        sb.AppendLine();

        // Cross-tab against the org chart: if these matched, behavioural clustering would be pointless.
        var crossTab = r.BaselineTokens
            .GroupBy(t => t.Department, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.User).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 3)
            .Select(g => new
            {
                Dept = g.Key,
                Users = g.Select(x => x.User).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .Select(d => new
            {
                d.Dept,
                d.Users.Count,
                Clusters = d.Users.Select(u => c.ClusterOf.GetValueOrDefault(u, "-"))
                    .Distinct(StringComparer.Ordinal).Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToList();

        if (crossTab.Count > 0)
        {
            sb.AppendLine("**Departman ↔ küme örtüşmesi** (≥3 kişilik departmanlar). Bir departman birden çok kümeye dağılıyorsa,");
            sb.AppendLine("org şeması davranışı temsil etmiyor demektir — kümeleme değer katıyor.");
            sb.AppendLine();
            sb.AppendLine("| Departman | Kullanıcı | Farklı küme |");
            sb.AppendLine("|---|---|---|");
            foreach (var x in crossTab)
                sb.AppendLine($"| {Trim(x.Dept, 40)} | {x.Count} | {x.Clusters} |");
            sb.AppendLine();
        }
    }

    private static void GapsAndExcitation(StringBuilder sb, SurprisalRunResult r, SurprisalOptions o)
    {
        sb.AppendLine("## 5. Olay aralıkları ve uyarım matrisi");
        sb.AppendLine();

        var g = r.Excitation.Gaps;
        if (g is not null && g.Count > 0)
        {
            sb.AppendLine("**Ardışık olaylar arası boşluk (dakika)** — 'oturum' eşiğinin veriden okunduğu yer:");
            sb.AppendLine();
            sb.AppendLine("| p10 | p25 | medyan | p75 | p90 | <5dk | <60dk |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            sb.AppendLine($"| {g.P10:F1} | {g.P25:F1} | {g.Median:F1} | {g.P75:F1} | {g.P90:F1} | %{g.ShareUnder5Min * 100:F1} | %{g.ShareUnder60Min * 100:F1} |");
            sb.AppendLine();
            sb.AppendLine($"Verinin önerdiği Δ: **{g.SuggestedWindowMinutes:F0} dk** · şu an kullanılan: **{o.ExcitationWindowMinutes:F0} dk**");
            if (Math.Abs(g.SuggestedWindowMinutes - o.ExcitationWindowMinutes) > o.ExcitationWindowMinutes * 0.5)
                sb.AppendLine($"> ⚠ Belirgin fark var — `Surprisal:ExcitationWindowMinutes` değerini {g.SuggestedWindowMinutes:F0} yapmayı değerlendirin.");
            sb.AppendLine();
        }

        var pairs = r.Excitation.AllPairs();
        var trusted = pairs.Where(p => p.Observations >= o.MinPairObservations).ToList();
        var cross = trusted.Where(p => !string.Equals(p.From, p.To, StringComparison.OrdinalIgnoreCase)).ToList();
        var self = trusted.Where(p => string.Equals(p.From, p.To, StringComparison.OrdinalIgnoreCase)).ToList();

        sb.AppendLine("**Kanal çifti lift'i** — `lift(a→b) = P(b | a, Δ içinde) / P(b)`. 1,0 = ilişki yok.");
        sb.AppendLine($"Gözlenen çift: {pairs.Count} · güvenilir (≥{o.MinPairObservations} gözlem): **{trusted.Count}** " +
                      $"(çapraz kanal: **{cross.Count}**, aynı kanal: {self.Count})");
        sb.AppendLine();
        sb.AppendLine("Skoru yalnızca **çapraz kanal** çiftleri çarpar. Aynı kanalın tekrarı (`EMAIL → EMAIL`) burst'tür,");
        sb.AppendLine("kombinasyon değil — verideki en yaygın örüntü olduğu için lift'i yüksek çıkar ama risk bilgisi taşımaz.");
        sb.AppendLine();

        if (cross.Count > 0)
        {
            sb.AppendLine("| a → b (çapraz) | Gözlem | Lift | Skoru çarpar mı |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var p in cross.Take(20))
                sb.AppendLine($"| `{Trim(p.From, 22)}` → `{Trim(p.To, 22)}` | {p.Observations} | **{p.Lift:F2}** | {(p.Lift > 1.0 ? "evet" : "hayır (<1)")} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("> ⚠ Güven eşiğini geçen **çapraz kanal** çifti yok — uyarım terimi şu an etkisiz.");
            sb.AppendLine("> Bu, veri kombinasyonlu kullanımı desteklemiyorsa doğru davranıştır. Taban penceresini");
            sb.AppendLine("> uzatmak ya da `MinPairObservations`'ı düşürmek görünürlüğü artırabilir.");
            sb.AppendLine();
        }

        if (self.Count > 0)
        {
            sb.AppendLine("<details><summary>Aynı kanal tekrarları (bilgi amaçlı, skoru etkilemez)</summary>");
            sb.AppendLine();
            sb.AppendLine("| a → a | Gözlem | Lift |");
            sb.AppendLine("|---|---|---|");
            foreach (var p in self.Take(10))
                sb.AppendLine($"| `{Trim(p.From, 24)}` | {p.Observations} | {p.Lift:F2} |");
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }
    }

    private static void SurprisalDistribution(StringBuilder sb, SurprisalRunResult r)
    {
        sb.AppendLine("## 6. Sürpriz dağılımı");
        sb.AppendLine();

        if (r.ScoredEvents.Count == 0) { sb.AppendLine("_Skorlanan olay yok._"); sb.AppendLine(); return; }

        var totals = r.ScoredEvents.Select(e => e.TotalBits).OrderByDescending(x => x).ToList();
        var scores = r.ScoredEvents.Select(e => e.Score).OrderByDescending(x => x).ToList();

        sb.AppendLine("| Büyüklük | p50 | p75 | p90 | p99 | maks |");
        sb.AppendLine("|---|---|---|---|---|---|");
        sb.AppendLine($"| Ham sürpriz (nat) | {Pct(totals, 0.5, 2)} | {Pct(totals, 0.25, 2)} | {Pct(totals, 0.10, 2)} | {Pct(totals, 0.01, 2)} | {totals.First():F2} |");
        sb.AppendLine($"| Nihai skor | {Pct(scores, 0.5, 2)} | {Pct(scores, 0.25, 2)} | {Pct(scores, 0.10, 2)} | {Pct(scores, 0.01, 2)} | {scores.First():F2} |");
        sb.AppendLine();

        sb.AppendLine("**Alan bazında katkı** — bir alan toplam sürprizin çoğunu tek başına taşıyorsa ağırlığı fazladır;");
        sb.AppendLine("neredeyse hiç katkı vermiyorsa ya kardinalitesi düşüktür ya da ağırlığı yetersizdir.");
        sb.AppendLine();
        sb.AppendLine("| Alan | Ortalama katkı (nat) | Toplam içindeki pay | Ham (ağırlıksız) |");
        sb.AppendLine("|---|---|---|---|");

        var grandTotal = r.ScoredEvents.Sum(e => e.TotalBits);
        foreach (var field in EventToken.Fields.All)
        {
            var contributions = r.ScoredEvents
                .SelectMany(e => e.Fields.Where(f => f.Field == field))
                .ToList();
            if (contributions.Count == 0) continue;

            var sum = contributions.Sum(f => f.Bits);
            sb.AppendLine($"| `{field}` | {contributions.Average(f => f.Bits):F2} | %{sum * 100.0 / Math.Max(grandTotal, 1e-9):F1} | {contributions.Average(f => f.RawBits):F2} |");
        }
        sb.AppendLine();

        var excited = r.ScoredEvents.Count(e => e.ExcitationMultiplier > 1.0001);
        sb.AppendLine($"Uyarım çarpanı devreye giren olay: **{excited:N0}** / {r.ScoredEvents.Count:N0} " +
                      $"(%{excited * 100.0 / r.ScoredEvents.Count:F1})");
        sb.AppendLine();
    }

    private static void TopEvents(StringBuilder sb, SurprisalRunResult r, SurprisalOptions o)
    {
        sb.AppendLine($"## 7. En yüksek skorlu {o.DiagnosticTopN} olay");
        sb.AppendLine();
        sb.AppendLine("**Bu bölüm raporun kalbi.** Bunlar bir analiste riskli görünmüyorsa model yanlış şeyi ölçüyordur.");
        sb.AppendLine("Her satırın altında sürprizin hangi alandan geldiği var.");
        sb.AppendLine();

        foreach (var (e, i) in r.ScoredEvents.OrderByDescending(x => x.Score).Take(o.DiagnosticTopN).Select((x, i) => (x, i + 1)))
        {
            var t = e.Token;
            sb.AppendLine($"**{i}. skor {e.Score:F2}** — `{Mask(t.User)}` · {t.Timestamp:yyyy-MM-dd HH:mm} · olay #{t.IncidentId}");
            sb.AppendLine();
            sb.AppendLine($"- kanal `{t.Channel}` → hedef sınıfı `{t.DestinationClass}` · aksiyon `{t.Action}`" +
                          (t.Egressed ? " **(veri çıktı)**" : " (engellendi)"));
            sb.AppendLine($"- politika `{Trim(t.Policy, 46)}` · sınıflandırıcı `{Trim(t.Classifier, 40)}` · eşleşme {t.MaxMatches:F0} (`{t.MatchTier}`) · `{t.TimeBucket}`");
            sb.AppendLine($"- sürpriz {e.TotalBits:F2} nat × sonuç {e.Consequence:F2} × uyarım {e.ExcitationMultiplier:F2}" +
                          (e.ExcitedBy is null ? "" : $" (öncesinde `{e.ExcitedBy}`)"));

            var top = e.Fields.OrderByDescending(f => f.Bits).Take(3)
                .Select(f => $"`{f.Field}`={f.Bits:F2} (P̂={f.Probability:F4}, λu={f.PersonalWeight:F2}, kişisel gözlem={f.PersonalObservations})");
            sb.AppendLine($"- baskın alanlar: {string.Join(" · ", top)}");
            sb.AppendLine();
        }
    }

    private static void TopUsers(StringBuilder sb, SurprisalRunResult r, SurprisalOptions o)
    {
        sb.AppendLine($"## 8. En yüksek birikmiş riskli {o.DiagnosticTopN} kullanıcı");
        sb.AppendLine();
        sb.AppendLine($"Sönümlü birikim, yarı ömür {o.RiskHalfLifeDays:F0} gün.");
        sb.AppendLine();
        sb.AppendLine("| # | Kullanıcı | Küme | Skor | Skorlanan | Taban | λu | Baskın alan |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var (u, i) in r.UserRisks.Take(o.DiagnosticTopN).Select((x, i) => (x, i + 1)))
        {
            var dominant = u.FieldContribution.OrderByDescending(kv => kv.Value).FirstOrDefault();
            sb.AppendLine($"| {i} | `{Mask(u.User)}` | `{u.Cluster}` | **{u.Score:F1}** | {u.ScoredEvents} | {u.BaselineEvents} | {u.PersonalWeight:F2} | `{dominant.Key}` ({dominant.Value:F1}) |");
        }
        sb.AppendLine();

        if (r.IsolationForestComparison is { Count: > 0 })
        {
            sb.AppendLine("**Mevcut Isolation Forest ile örtüşme** — iki modelin ilk 50'si ne kadar aynı kişileri gösteriyor:");
            sb.AppendLine();
            foreach (var line in r.IsolationForestComparison) sb.AppendLine($"- {line}");
            sb.AppendLine();
        }
    }

    private static void HealthWarnings(StringBuilder sb, SurprisalRunResult r, SurprisalOptions o)
    {
        sb.AppendLine("## 9. Sağlık uyarıları");
        sb.AppendLine();

        var warnings = new List<string>();

        foreach (var field in EventToken.Fields.All)
        {
            var values = r.BaselineTokens.Select(t => t.Value(field)).ToList();
            if (values.Count == 0) continue;

            var distinct = values.Distinct(StringComparer.Ordinal).Count();
            var unknownShare = values.Count(v => v == EventToken.Unknown) / (double)values.Count;

            if (distinct > values.Count / 20.0 && distinct > 30)
                warnings.Add($"`{field}` kardinalitesi yüksek ({distinct} değer / {values.Count:N0} olay) — kovalama gerekebilir, aksi halde her değer 'nadir' çıkar.");

            if (unknownShare > 0.25)
                warnings.Add($"`{field}` olayların %{unknownShare * 100:F0}'inde `unknown` — alan ya boş geliyor ya da ayrıştırıcı eksik.");

            if (distinct <= 1)
                warnings.Add($"`{field}` tek değerli — hiç bilgi taşımıyor, ağırlığı 0 yapılabilir.");
        }

        var sensitivities = r.BaselineTokens.Select(t => t.DataSensitivity).Distinct().OrderBy(x => x).ToList();
        if (sensitivities.Count is > 0 and <= 4)
            warnings.Add($"`data_sensitivity` yalnızca {{{string.Join(",", sensitivities)}}} değerlerini alıyor — eski IF motorunun 8 özelliği bu tek ordinalden türüyor. Hacim sinyali olarak `max_matches` kullanılmalı.");

        var actions = r.BaselineTokens.Select(t => t.Action).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unmapped = actions.Where(a => a != EventToken.Unknown && !EventTokenizer.IsEgress(a) &&
                                          !a.Contains("block", StringComparison.OrdinalIgnoreCase) &&
                                          !a.Contains("quarantin", StringComparison.OrdinalIgnoreCase) &&
                                          !a.Contains("deny", StringComparison.OrdinalIgnoreCase) &&
                                          !a.Contains("denied", StringComparison.OrdinalIgnoreCase)).ToList();
        if (unmapped.Count > 0)
            warnings.Add($"Sınıflandırılamayan aksiyon değerleri: {string.Join(", ", unmapped.Select(a => $"`{a}`"))} — engellendi mi çıktı mı belirsiz, `EgressActions` listesine eklenmeli.");

        var noBaseline = r.BaselineCounts.Count(kv => kv.Value == 0);
        var scoredUsers = r.UserRisks.Count;
        if (scoredUsers > 0 && noBaseline > scoredUsers * 0.3)
            warnings.Add($"Skorlanan kullanıcıların %{noBaseline * 100.0 / scoredUsers:F0}'inde hiç taban yok — kişisel terim çoğunlukla devre dışı, taban penceresi uzatılabilir.");

        if (r.Clustering.Silhouette < 0.2 && r.Clustering.ChosenK > 0)
            warnings.Add($"Kümeleme silhouette'i düşük ({r.Clustering.Silhouette:F3}) — kümeler zayıf ayrışıyor, profil alanları gözden geçirilmeli.");

        var crossPairs = r.Excitation.AllPairs()
            .Count(p => p.Observations >= o.MinPairObservations &&
                        !string.Equals(p.From, p.To, StringComparison.OrdinalIgnoreCase) &&
                        p.Lift > 1.0);
        if (crossPairs == 0)
            warnings.Add("Skoru yükselten çapraz kanal çifti yok — uyarım terimi şu an etkisiz. " +
                         "Kombinasyonlu kullanım verisi yetersizse bu doğru davranıştır.");

        var excitedShare = r.ScoredEvents.Count == 0
            ? 0
            : r.ScoredEvents.Count(e => e.ExcitationMultiplier > 1.0001) / (double)r.ScoredEvents.Count;
        if (excitedShare > 0.25)
            warnings.Add($"Olayların %{excitedShare * 100:F0}'inde uyarım çarpanı devrede — bu kadar yaygınsa " +
                         "ayırt edici değil; Δ daraltılmalı ya da `MinPairObservations` yükseltilmeli.");

        if (warnings.Count == 0) sb.AppendLine("_Uyarı yok._");
        else foreach (var w in warnings) sb.AppendLine($"- ⚠ {w}");
        sb.AppendLine();
    }

    private static void Configuration(StringBuilder sb, SurprisalOptions o)
    {
        sb.AppendLine("## 10. Kullanılan konfigürasyon");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine($"  \"BaselineWindowDays\": {o.BaselineWindowDays},");
        sb.AppendLine($"  \"ScoreWindowDays\": {o.ScoreWindowDays},");
        sb.AppendLine($"  \"BaselineRecencyHalfLifeDays\": {o.BaselineRecencyHalfLifeDays.ToString(Inv)},");
        sb.AppendLine($"  \"RiskHalfLifeDays\": {o.RiskHalfLifeDays.ToString(Inv)},");
        sb.AppendLine($"  \"PersonalBackoffK\": {o.PersonalBackoffK.ToString(Inv)},");
        sb.AppendLine($"  \"ClusterBackoffK\": {o.ClusterBackoffK.ToString(Inv)},");
        sb.AppendLine($"  \"OrgSmoothingAlpha\": {o.OrgSmoothingAlpha.ToString(Inv)},");
        sb.AppendLine($"  \"ExcitationWindowMinutes\": {o.ExcitationWindowMinutes.ToString(Inv)},");
        sb.AppendLine($"  \"FieldWeights\": {{ {string.Join(", ", o.FieldWeights.Select(kv => $"\"{kv.Key}\": {kv.Value.ToString(Inv)}"))} }}");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**Geri bildirim için:** bölüm 7'deki olaylardan hangileri gerçekten riskli, hangileri gürültü?");
        sb.AppendLine("Bu tek soru alan ağırlıklarını ve sonuç çarpanlarını ayarlamak için yeterli.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Pct(IReadOnlyList<int> descending, double p) =>
        descending.Count == 0 ? "—" : descending[Math.Clamp((int)(descending.Count * p), 0, descending.Count - 1)].ToString();

    private static string Pct(IReadOnlyList<double> descending, double p, int digits) =>
        descending.Count == 0 ? "—" : descending[Math.Clamp((int)(descending.Count * p), 0, descending.Count - 1)].ToString("F" + digits);

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>
    /// Partially masks the address — the report gets pasted into chats and tickets. Short local
    /// parts must still be masked, so the number of retained characters shrinks with the length
    /// rather than the mask being skipped entirely.
    /// </summary>
    internal static string Mask(string email)
    {
        if (string.IsNullOrEmpty(email)) return email;

        var at = email.IndexOf('@');
        if (at <= 0) return email;
        if (at == 1) return "*" + email[at..];

        var keep = at <= 3 ? 1 : 2;
        return email[..keep] + new string('*', Math.Min(6, at - keep)) + email[at..];
    }
}
