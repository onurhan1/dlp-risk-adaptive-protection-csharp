/**
 * Rendering catalog for the AI risk model's reason layer.
 *
 * The server sends KEYS and NUMBERS only — `family_key`, `dimension`, `anchor_feature`,
 * `observed_value`, `reference_value`, `deviation_sigma`. Every word an analyst reads is produced
 * here, so the same payload renders in Turkish or English and nothing has to be reverse-engineered
 * from a display string.
 */

import type { Locale } from './i18n'

// ── Wire types ────────────────────────────────────────────────────────────────

export type Dimension = 'self' | 'peer' | 'population'
export type Effect = 'raises' | 'lowers' | 'none'
export type Deviation = 'above' | 'below' | 'at_norm' | 'unknown'
export type Polarity = 'higher_is_riskier' | 'two_sided' | 'context_only'
export type RiskReading = 'risk_indicator' | 'unusual_not_risky' | 'descriptive'
export type ValueKind = 'count' | 'ratio' | 'sens' | 'sev' | 'act' | 'rarity' | 'rate' | 'z' | 'zabs' | 'head'
export type ConfidenceLevel = 'high' | 'medium' | 'low' | 'none'

export interface Reason {
  family_key: string
  dimension: Dimension
  anchor_feature: string
  members: string[]
  rank: number
  impact: number
  impact_points: number
  impact_share_pct: number
  effect: Effect
  deviation: Deviation
  deviation_sigma: number | null
  observed_value: number | null
  reference_value: number | null
  tail_pct: number
  value_kind: ValueKind
  polarity: Polarity
  risk_reading: RiskReading
  evidence_incident_ids: number[]
  evidence_count: number
}

export interface DimensionConfidence {
  dimension: Dimension
  available: boolean
  level: ConfidenceLevel
  reason_key: string | null
  reason_args: Record<string, number>
  share_pct: number
}

export interface EvidenceIncident {
  id: number
  timestamp: string
  channel?: string | null
  destination?: string | null
  action?: string | null
  policy?: string | null
  rule_name?: string | null
  severity: number
  data_sensitivity: number
  max_matches: number
  file_name?: string | null
}

export interface ReasonEvidence {
  user_email: string
  family_key: string
  dimension: string
  total_count: number
  incidents: EvidenceIncident[]
}

// ── Event grouping ────────────────────────────────────────────────────────────

/**
 * Findings that point at the same incidents, collapsed into one event.
 *
 * The model scores a user's whole window, not individual incidents, so a single incident that is
 * both off-hours and a heavy classifier hit legitimately moves two feature families. Rendering
 * those as two independent cards reads as two separate things happening — the analyst sees a list
 * that grows downward and silently double-counts one event in their head. Grouping on the evidence
 * set puts them back together: one event, several findings.
 */
export interface ReasonGroup {
  key: string
  incident_ids: number[]
  evidence_count: number
  reasons: Reason[]
  /** Sum of the members' shares. Shares come from sequential ablation, so they are additive. */
  share_pct: number
  /** Largest single marginal impact in the group — used for ordering, never summed. */
  top_points: number
}

export function groupReasonsByEvent(reasons: Reason[]): ReasonGroup[] {
  const groups: ReasonGroup[] = []
  const byKey = new Map<string, ReasonGroup>()

  reasons.forEach((reason, index) => {
    const ids = [...reason.evidence_incident_ids].sort((a, b) => a - b)

    // A reason with no incidents behind it (an aggregate ratio, a baseline break with nothing
    // recorded) is its own group: merging those on an empty key would fuse unrelated findings.
    const key = ids.length > 0 ? `e:${ids.join(',')}` : `r:${reason.family_key}:${reason.dimension}:${index}`

    let group = byKey.get(key)
    if (!group) {
      group = {
        key,
        incident_ids: ids,
        evidence_count: reason.evidence_count,
        reasons: [],
        share_pct: 0,
        top_points: 0,
      }
      byKey.set(key, group)
      groups.push(group)
    }

    group.reasons.push(reason)
    group.share_pct += reason.impact_share_pct
    group.top_points = Math.max(group.top_points, Math.abs(reason.impact_points))
    group.evidence_count = Math.max(group.evidence_count, reason.evidence_count)
  })

  return groups.sort((a, b) => b.share_pct - a.share_pct || b.top_points - a.top_points)
}

// ── Strings ───────────────────────────────────────────────────────────────────

type Dict = Record<string, string>

const TR: Dict = {
  // Dimensions — note "population" is NOT "this week"; after standardization a raw column is a
  // z-score across the whole scored cohort.
  'dim.self': 'Kendi Geçmişi',
  'dim.peer': 'Ekibi',
  'dim.population': 'Kurum Geneli',
  'dim.self.desc': 'Pencere öncesi kişisel geçmişinden sapma',
  'dim.peer.desc': 'Aynı dönemdeki ekibinden sapma',
  'dim.population.desc': 'Skorlanan tüm kullanıcılara göre konum',

  // Families
  'family.baseline_break': 'Kişisel Norm Kırılması',
  'family.data_sensitivity': 'Hassas Veri Duyarlılığı',
  'family.classifier_hits': 'Sınıflandırıcı Eşleşme Yoğunluğu',
  'family.permissive_outcome': 'Engellenmeyen Eylemler',
  'family.rare_channel': 'Nadir Kanal Kullanımı',
  'family.off_hours': 'Mesai Dışı Aktivite',
  'family.spread': 'Kanal & Hedef Yayılımı',
  'family.severity': 'Olay Ciddiyeti',
  'family.volume': 'Olay Hacmi & Tempo',
  'family.team_context': 'Ekip Bağlamı',

  // What was observed, in the metric's own units. No reference value, no sigma: an analyst reads
  // "how much" and "is that a lot", and the second question is answered in words below.
  // No trailing punctuation: the reading templates below close the sentence.
  'ev.baseline_break': 'Son 7 günde {count} olay kişisel normunun dışında',
  'ev.data_sensitivity': 'Veri duyarlılığı {observed}',
  'ev.classifier_hits': '{observed} sınıflandırıcı eşleşmesi',
  'ev.permissive_outcome': 'Eylemlerin {observed} kadarı engellenmedi',
  'ev.rare_channel': 'Alışılmadık kanal kullanımı',
  'ev.off_hours': 'Hareketin {observed} kadarı mesai dışında',
  'ev.spread': '{observed} farklı çıkış noktası kullanıldı',
  'ev.severity': 'Olay ciddiyeti {observed}',
  'ev.volume': 'Olay hacmi {observed}',
  'ev.fallback': 'Gözlenen değer {observed}',

  // How far from normal, in words. The sigma is still what decides the wording — it just is not
  // printed, because "1,9σ üzerinde" tells an analyst nothing they can act on.
  'rel.self': 'kendi geçmişine göre',
  'rel.peer': 'ekibine göre',
  'rel.population': 'kurum geneline göre',
  'lvl.above.extreme': 'olağandışı yüksek',
  'lvl.above.strong': 'belirgin şekilde yüksek',
  'lvl.above.mild': 'normalin üzerinde',
  'lvl.above.flat': 'normale yakın',
  'lvl.below.extreme': 'olağandışı düşük',
  'lvl.below.strong': 'belirgin şekilde düşük',
  'lvl.below.mild': 'normalin altında',
  'lvl.below.flat': 'normale yakın',
  'lvl.unknown.extreme': 'olağandışı',
  'lvl.unknown.strong': 'belirgin şekilde sıra dışı',
  'lvl.unknown.mild': 'hafif sıra dışı',
  'lvl.unknown.flat': 'normale yakın',
  'lvl.at_norm': 'normal seviyede',
  'read.withValue': '{evidence} — {relation} {level}.',
  'read.plain': '{relation} {level}.',

  'effect.raises': 'Skoru yükseltiyor',
  'effect.lowers': 'Skoru düşürüyor',
  'effect.points': '{points} puan',
  'effect.share': 'skorun {share}',
  'reading.unusual_not_risky': 'Sıra dışı, tek başına risk göstergesi değil',

  // Confidence
  'conf.high': 'Yüksek güven',
  'conf.medium': 'Orta güven',
  'conf.low': 'Düşük güven',
  'conf.none': 'Veri yok',
  'conf.noBaseline': 'Kişisel geçmiş yok — yalnızca ekip ve kurum kıyası',
  'conf.thinBaseline': 'Kişisel geçmiş yalnızca {n} olay — kendi normu güvenilir değil',
  'conf.tinyDept': 'Ekipte yalnızca {n} kişi aktif — ekip kıyası zayıf',
  'conf.smallCohort': 'Bu koşuda yalnızca {n} kullanıcı skorlandı',

  // Caveats
  'caveat.score_is_cohort_relative':
    'Skor bu çalıştırmadaki {n} kullanıcı arasında görecelidir; farklı günlerin skorları doğrudan karşılaştırılamaz.',
  'caveat.insufficient_personal_baseline':
    'Kişisel geçmiş yetersiz olduğu için "kendi geçmişi" boyutu bu kullanıcıda kullanılmadı.',
  'caveat.small_department': 'Departman küçük olduğu için ekip kıyası zayıf.',
  'caveat.single_user_cohort': 'Tek kullanıcılık kohort — kıyaslanacak kimse yok.',

  // Page chrome
  'page.title': 'Günlük AI Davranış Risk Skoru',
  'page.subtitle':
    'Her kullanıcı için son 7 günlük davranışı, kullanıcının tüm geçmişi ve ekip davranışıyla karşılaştırır.',
  'page.runNow': 'Şimdi Çalıştır',
  'page.running': 'Hesaplanıyor...',
  'page.reload': 'Sonuçları Yükle',
  'page.loading': 'AI Risk modeli sonuçları yükleniyor...',
  'page.emptyTitle': 'Henüz AI Risk Model skoru hesaplanmamış',
  'page.emptyBody':
    '"Şimdi Çalıştır" ile ilk 7 günlük risk skorlamasını başlatın. Sonraki skorlar her gün otomatik güncellenecek.',
  'page.lastRun': 'Son analiz',
  'page.window': '{days} günlük davranış penceresi',
  'page.baseline': 'Baseline: tüm geçmiş',
  'page.analyzed': '{n} kullanıcı analiz edildi',
  'page.reviewQueue': '{n} kullanıcı inceleme adayı',
  'page.rerunning': 'Yeni analiz devam ediyor...',
  'page.search': 'Kullanıcı adı veya departman ara...',
  'page.listTitle': 'Kullanıcı riskleri',
  'page.listSubtitle': 'Her kullanıcı için skor ve skoru oluşturan davranışlar tek yerde gösterilir.',
  'page.counts': '{anomalies} incelenmeli · {total} kullanıcı',
  'page.prev': '‹ Önceki',
  'page.next': 'Sonraki ›',

  // Contract cards
  'card.daily': 'Her gün çalışır',
  'card.dailyBody': 'Skorlar günlük batch ile otomatik yenilenir.',
  'card.window': 'Son 7 günü skorlar',
  'card.windowBody': 'Kullanıcının güncel haftalık davranışı değerlendirilir.',
  'card.baseline': 'Üç boyutla kıyaslar',
  'card.baselineBody': 'Kendi geçmişi, ekibi ve kurum geneli ayrı ayrı hesaplanır.',

  // Stats
  'stat.totalUsers': 'Toplam Kullanıcı',
  'stat.reviewQueue': 'İnceleme Adayı',
  'stat.meanScore': 'Kurum Ort. Skor',
  'stat.window': 'Skor Penceresi',
  'stat.windowValue': '{days} gün',
  // The review queue is a fixed top-N slice, so calling it a measured "risk rate" would be a lie.
  'stat.reviewQueueNote':
    'Her çalıştırmada en yüksek skorlu %5 inceleme adayı olarak işaretlenir. Bu oran sabittir, haftanın gerçek risk seviyesini göstermez.',

  // Detail card
  'detail.status.anomaly': '⚠ İncelenmeli',
  'detail.status.normal': '✓ Normal',
  'detail.score': 'AI Risk Skoru',
  'detail.rank': '{total} kullanıcı içinde {rank}. sırada (ilk {pct})',
  'detail.evidenceSplit': 'KANIT DAĞILIMI',
  'detail.evidenceSplitLead': 'Skorun kanıtı ağırlıklı olarak "{dimension}" boyutundan geliyor.',
  'detail.reasons': 'ÖNE ÇIKAN DAVRANIŞLAR',
  'detail.explained': 'skorun {explained}’i bu davranışlarla açıklanıyor',

  // Movement since the previous run
  'trend.comparedTo': '{date} koşusuna göre',
  'trend.up': '{delta} arttı',
  'trend.down': '{delta} azaldı',
  'trend.flat': 'Değişim yok',
  'trend.new': 'İlk kez skorlandı',
  'trend.previous': 'Önceki skor: {score}',
  'trend.becameAnomaly': 'Bu koşuda inceleme listesine girdi',
  'trend.leftAnomaly': 'Bu koşuda inceleme listesinden çıktı',
  'stat.rising': 'Skoru Yükselen',
  'stat.risingNote': '{date} koşusuna göre skoru artan kullanıcı sayısı.',
  'detail.secondary': 'İKİNCİL SİNYALLER (sıra dışı, tek başına risk göstergesi değil)',
  'detail.teamContext': 'EKİP BAĞLAMI (karşılaştırma tabanı — gerekçe değildir)',
  'detail.noReasons': 'Bu kullanıcı için gösterilecek gerekçe yok.',
  'detail.noExplanation':
    'Bu satır gerekçe katmanından önce hesaplanmış. Yeni bir analiz çalıştırın.',
  'detail.window': 'Son 7 gün: {n} olay',
  'detail.baselineCount': 'Geçmiş baseline: {n} olay',
  'detail.showEvidence': '{n} olayı gör',
  'detail.hideEvidence': 'Olayları gizle',
  'detail.evidenceLoading': 'Olaylar yükleniyor...',
  'detail.evidenceEmpty': 'Bu gerekçeye bağlı olay bulunamadı.',
  'detail.evidenceTruncated': '{shown} / {total} olay gösteriliyor',

  // Event groups — one event, several findings
  'group.single': 'Tek olay',
  'group.multi': '{n} olay',
  'group.aggregate': 'Dönem geneli davranış',
  'group.findings': '{n} bulgu',
  'group.oneFinding': '1 bulgu',
  'group.share': 'skorun {share}’i',
  'group.pointsNote':
    'Puanlar davranış ailesine aittir, olaya değil: aynı olay birden fazla davranışı tetikleyebilir, bu yüzden puanlar toplanmaz.',
  'group.eventMeta': '{severity} · {sensitivity} · {matches} eşleşme',
  'group.unknownEvent': 'Olay detayı yüklenemedi',

  // Evidence table
  'ev.col.time': 'Zaman',
  'ev.col.channel': 'Kanal',
  'ev.col.destination': 'Hedef',
  'ev.col.action': 'Eylem',
  'ev.col.severity': 'Ciddiyet',
  'ev.col.sensitivity': 'Duyarlılık',
  'ev.col.matches': 'Eşleşme',

  // Team context labels
  'f.dept_size': 'Ekip büyüklüğü',
  'f.dept_mean_incident_count': 'Ekip ort. olay',
  'f.dept_mean_off_hours_ratio': 'Ekip ort. mesai dışı',
  'f.dept_mean_allowed_ratio': 'Ekip ort. engellenmeyen',
  'f.dept_mean_high_sev_ratio': 'Ekip ort. yüksek risk',
  'f.dept_mean_tx_size': 'Ekip ort. duyarlılık',
}

const EN: Dict = {
  'dim.self': 'Own History',
  'dim.peer': 'Their Team',
  'dim.population': 'Organization',
  'dim.self.desc': 'Deviation from their pre-window personal history',
  'dim.peer.desc': 'Deviation from their team over the same period',
  'dim.population.desc': 'Position against every scored user',

  'family.baseline_break': 'Personal Baseline Break',
  'family.data_sensitivity': 'Sensitive-Data Exposure',
  'family.classifier_hits': 'Classifier Match Density',
  'family.permissive_outcome': 'Permitted / Unblocked Actions',
  'family.rare_channel': 'Rare Channel Usage',
  'family.off_hours': 'Off-Hours Activity',
  'family.spread': 'Channel / Destination Breadth',
  'family.severity': 'Incident Severity',
  'family.volume': 'Volume & Tempo',
  'family.team_context': 'Team Context',

  'ev.baseline_break': '{count} incidents in the last 7 days fall outside their personal norm',
  'ev.data_sensitivity': 'Data sensitivity {observed}',
  'ev.classifier_hits': '{observed} classifier matches',
  'ev.permissive_outcome': '{observed} of actions went unblocked',
  'ev.rare_channel': 'Unusual channel usage',
  'ev.off_hours': '{observed} of the activity happened off-hours',
  'ev.spread': '{observed} distinct exit points used',
  'ev.severity': 'Incident severity {observed}',
  'ev.volume': 'Incident volume {observed}',
  'ev.fallback': 'Observed value {observed}',

  'rel.self': 'against their own history',
  'rel.peer': 'against their team',
  'rel.population': 'across the organization',
  'lvl.above.extreme': 'exceptionally high',
  'lvl.above.strong': 'clearly high',
  'lvl.above.mild': 'above normal',
  'lvl.above.flat': 'close to normal',
  'lvl.below.extreme': 'exceptionally low',
  'lvl.below.strong': 'clearly low',
  'lvl.below.mild': 'below normal',
  'lvl.below.flat': 'close to normal',
  'lvl.unknown.extreme': 'exceptional',
  'lvl.unknown.strong': 'clearly unusual',
  'lvl.unknown.mild': 'slightly unusual',
  'lvl.unknown.flat': 'close to normal',
  'lvl.at_norm': 'at the norm',
  'read.withValue': '{evidence} — {level} {relation}.',
  'read.plain': '{level} {relation}.',

  'effect.raises': 'Raises the score',
  'effect.lowers': 'Lowers the score',
  'effect.points': '{points} points',
  'effect.share': '{share} of the score',
  'reading.unusual_not_risky': 'Unusual, but not a risk indicator on its own',

  'conf.high': 'High confidence',
  'conf.medium': 'Medium confidence',
  'conf.low': 'Low confidence',
  'conf.none': 'No data',
  'conf.noBaseline': 'No personal history — compared against team and organization only',
  'conf.thinBaseline': 'Only {n} prior incidents — their personal norm is not reliable',
  'conf.tinyDept': 'Only {n} people active in the team — the peer comparison is weak',
  'conf.smallCohort': 'Only {n} users were scored in this run',

  'caveat.score_is_cohort_relative':
    'The score is relative to the {n} users in this run; scores from different days are not directly comparable.',
  'caveat.insufficient_personal_baseline':
    'The "own history" dimension was not used for this user — their baseline is too thin.',
  'caveat.small_department': 'The department is small, so the peer comparison is weak.',
  'caveat.single_user_cohort': 'Single-user cohort — there is nobody to compare against.',

  'page.title': 'Daily AI Behavior Risk Score',
  'page.subtitle':
    "Compares each user's last 7 days against their own history and their team's behavior.",
  'page.runNow': 'Run now',
  'page.running': 'Calculating...',
  'page.reload': 'Reload results',
  'page.loading': 'Loading AI risk model results...',
  'page.emptyTitle': 'No AI risk model score has been calculated yet',
  'page.emptyBody':
    'Use "Run now" to start the first 7-day scoring. Later scores refresh automatically every day.',
  'page.lastRun': 'Last run',
  'page.window': '{days}-day behavior window',
  'page.baseline': 'Baseline: all history',
  'page.analyzed': '{n} users analyzed',
  'page.reviewQueue': '{n} users in the review queue',
  'page.rerunning': 'A new run is in progress...',
  'page.search': 'Search user or department...',
  'page.listTitle': 'User risks',
  'page.listSubtitle': 'Score and the behaviors behind it, in one place, for every user.',
  'page.counts': '{anomalies} to review · {total} users',
  'page.prev': '‹ Previous',
  'page.next': 'Next ›',

  'card.daily': 'Runs every day',
  'card.dailyBody': 'Scores refresh automatically through the daily batch.',
  'card.window': 'Scores the last 7 days',
  'card.windowBody': "The user's current weekly behavior is evaluated.",
  'card.baseline': 'Compares on three axes',
  'card.baselineBody': 'Own history, team and organization are computed separately.',

  'stat.totalUsers': 'Total users',
  'stat.reviewQueue': 'Review queue',
  'stat.meanScore': 'Org. mean score',
  'stat.window': 'Score window',
  'stat.windowValue': '{days} days',
  'stat.reviewQueueNote':
    'Every run flags the top-scoring 5% for review. That share is fixed and does not reflect the actual risk level of the week.',

  'detail.status.anomaly': '⚠ Review',
  'detail.status.normal': '✓ Normal',
  'detail.score': 'AI risk score',
  'detail.rank': 'Rank {rank} of {total} (top {pct})',
  'detail.evidenceSplit': 'EVIDENCE SPLIT',
  'detail.evidenceSplitLead': 'The evidence comes mostly from the "{dimension}" dimension.',
  'detail.reasons': 'LEADING BEHAVIORS',
  'detail.explained': '{explained} of the score is explained by these behaviours',

  'trend.comparedTo': 'vs the {date} run',
  'trend.up': 'up {delta}',
  'trend.down': 'down {delta}',
  'trend.flat': 'No change',
  'trend.new': 'Scored for the first time',
  'trend.previous': 'Previous score: {score}',
  'trend.becameAnomaly': 'Entered the review queue in this run',
  'trend.leftAnomaly': 'Left the review queue in this run',
  'stat.rising': 'Rising scores',
  'stat.risingNote': 'Users whose score went up compared with the {date} run.',
  'detail.secondary': 'SECONDARY SIGNALS (unusual, not a risk indicator on their own)',
  'detail.teamContext': 'TEAM CONTEXT (comparison basis — not a reason)',
  'detail.noReasons': 'There is nothing to report for this user.',
  'detail.noExplanation': 'This row predates the reason layer. Trigger a new run.',
  'detail.window': 'Last 7 days: {n} incidents',
  'detail.baselineCount': 'Prior baseline: {n} incidents',
  'detail.showEvidence': 'View {n} incidents',
  'detail.hideEvidence': 'Hide incidents',
  'detail.evidenceLoading': 'Loading incidents...',
  'detail.evidenceEmpty': 'No incidents found for this reason.',
  'detail.evidenceTruncated': 'Showing {shown} of {total} incidents',

  'group.single': 'Single event',
  'group.multi': '{n} events',
  'group.aggregate': 'Behaviour across the window',
  'group.findings': '{n} findings',
  'group.oneFinding': '1 finding',
  'group.share': '{share} of the score',
  'group.pointsNote':
    'Points belong to the behaviour family, not to the event: one event can trigger several behaviours, which is why the points are not summed.',
  'group.eventMeta': '{severity} · {sensitivity} · {matches} matches',
  'group.unknownEvent': 'Event details could not be loaded',

  'ev.col.time': 'Time',
  'ev.col.channel': 'Channel',
  'ev.col.destination': 'Destination',
  'ev.col.action': 'Action',
  'ev.col.severity': 'Severity',
  'ev.col.sensitivity': 'Sensitivity',
  'ev.col.matches': 'Matches',

  'f.dept_size': 'Team size',
  'f.dept_mean_incident_count': 'Team avg. incidents',
  'f.dept_mean_off_hours_ratio': 'Team avg. off-hours',
  'f.dept_mean_allowed_ratio': 'Team avg. unblocked',
  'f.dept_mean_high_sev_ratio': 'Team avg. high severity',
  'f.dept_mean_tx_size': 'Team avg. sensitivity',
}

const CATALOG: Record<Locale, Dict> = { tr: TR, en: EN }

// ── Rendering helpers ─────────────────────────────────────────────────────────

export function fmt(locale: Locale, key: string, vars?: Record<string, string | number>): string {
  const raw = CATALOG[locale]?.[key] ?? CATALOG.tr[key] ?? key
  return vars
    ? raw.replace(/\{(\w+)\}/g, (match, name) => (name in vars ? String(vars[name]) : match))
    : raw
}

const intl = (locale: Locale) => (locale === 'tr' ? 'tr-TR' : 'en-US')

export function num(locale: Locale, value: number, digits = 1): string {
  return new Intl.NumberFormat(intl(locale), {
    minimumFractionDigits: 0,
    maximumFractionDigits: digits,
  }).format(value)
}

/**
 * Formats a value in its own units. `sens` is Incident.DataSensitivity — an integer sensitivity
 * score, NOT a byte count; the old UI called it "transfer size", which was simply wrong.
 */
export function formatValue(locale: Locale, value: number | null, kind: ValueKind): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—'
  switch (kind) {
    case 'ratio':
      return `%${num(locale, value * 100, 0)}`
    case 'count':
    case 'head':
      return num(locale, value, 0)
    case 'sens':
      return `${num(locale, value, 1)}/10`
    case 'sev':
      return `${num(locale, value, 1)}/5`
    case 'act':
      return `${num(locale, value, 1)}/4`
    case 'rate':
      return `${num(locale, value, 2)}${locale === 'tr' ? '/gün' : '/day'}`
    case 'rarity':
      return num(locale, value, 2)
    case 'z':
    case 'zabs':
      return `${num(locale, value, 2)}σ`
    default:
      return num(locale, value, 2)
  }
}

export function familyLabel(locale: Locale, familyKey: string): string {
  return fmt(locale, `family.${familyKey}`)
}

export function dimensionLabel(locale: Locale, dimension: string): string {
  return fmt(locale, `dim.${dimension}`)
}

/** The observed value in its own units. Empty when the metric has no unit a human can read. */
export function evidenceLine(locale: Locale, reason: Reason): string {
  const observed = formatValue(locale, reason.observed_value, reason.value_kind)
  const key = CATALOG[locale][`ev.${reason.family_key}`] ? `ev.${reason.family_key}` : 'ev.fallback'
  return fmt(locale, key, { observed, count: reason.evidence_count })
}

/**
 * How unusual this is, in words.
 *
 * The sigma still decides the wording, it is simply never printed: "1,9σ above the cohort median"
 * is a statement about the feature matrix, and an analyst deciding whether to open an
 * investigation cannot act on it. Bands are deliberately coarse — the model's own confidence does
 * not justify finer ones.
 */
function levelKey(reason: Reason): string {
  if (reason.deviation === 'at_norm') return 'lvl.at_norm'

  const sigma = Math.abs(reason.deviation_sigma ?? 0)
  const band = sigma >= 3 ? 'extreme' : sigma >= 2 ? 'strong' : sigma >= 1 ? 'mild' : 'flat'
  const direction = reason.deviation === 'above' || reason.deviation === 'below' ? reason.deviation : 'unknown'
  return `lvl.${direction}.${band}`
}

function capitalize(locale: Locale, text: string): string {
  if (!text) return text
  return text.charAt(0).toLocaleUpperCase(locale === 'tr' ? 'tr-TR' : 'en-US') + text.slice(1)
}

/**
 * The single sentence under a finding's title: what was observed, and how unusual it is.
 *
 * A z-scored column has no unit anyone reads — its "value" is the sigma itself — so those findings
 * carry the verdict alone rather than printing the number twice in two disguises.
 */
export function readingLine(locale: Locale, reason: Reason): string {
  const level = fmt(locale, levelKey(reason))
  const relation = fmt(locale, `rel.${reason.dimension}`)

  const unitless = reason.value_kind === 'z' || reason.value_kind === 'zabs' || reason.observed_value === null
  if (unitless) return capitalize(locale, fmt(locale, 'read.plain', { relation, level }))

  return capitalize(locale, fmt(locale, 'read.withValue', {
    evidence: evidenceLine(locale, reason),
    relation,
    level,
  }))
}

export const DIMENSION_COLOR: Record<string, string> = {
  self: '#F58518',
  peer: '#54A24B',
  population: '#4C78A8',
  context: '#B279A2',
}
