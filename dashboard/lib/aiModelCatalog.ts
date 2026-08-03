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

  // Evidence sentence, per family. {observed}/{reference}/{sigma}/{count} come from the payload.
  'ev.baseline_break': 'Son 7 günde {count} olay kişisel normunun dışında.',
  'ev.data_sensitivity': 'Veri duyarlılığı {observed} (kıyas: {reference}).',
  'ev.classifier_hits': '{observed} sınıflandırıcı eşleşmesi (kıyas: {reference}).',
  'ev.permissive_outcome': 'Eylemlerin {observed} kadarı engellenmedi (kıyas: {reference}).',
  'ev.rare_channel': 'Kanal nadirliği {observed} (kıyas: {reference}).',
  'ev.off_hours': 'Mesai dışı hareket {observed} (kıyas: {reference}).',
  'ev.spread': '{observed} farklı çıkış noktası (kıyas: {reference}).',
  'ev.severity': 'Olay ciddiyeti {observed} (kıyas: {reference}).',
  'ev.volume': 'Olay hacmi {observed} (kıyas: {reference}).',
  'ev.fallback': 'Gözlenen {observed}, kıyas {reference}.',

  // Deviation / effect wording
  'dev.above': '{sigma}σ üzerinde',
  'dev.below': '{sigma}σ altında',
  'dev.at_norm': 'norm seviyesinde',
  'dev.unknown': '{sigma}σ sapma',
  'dev.tail': '{tail} · bu seviyede',
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
  'detail.explained': 'skorun {explained}’i açıklandı · {unexplained} etkileşim artığı',
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

  'ev.baseline_break': '{count} incidents in the last 7 days fall outside their personal norm.',
  'ev.data_sensitivity': 'Data sensitivity {observed} (reference: {reference}).',
  'ev.classifier_hits': '{observed} classifier matches (reference: {reference}).',
  'ev.permissive_outcome': '{observed} of actions went unblocked (reference: {reference}).',
  'ev.rare_channel': 'Channel rarity {observed} (reference: {reference}).',
  'ev.off_hours': 'Off-hours activity {observed} (reference: {reference}).',
  'ev.spread': '{observed} distinct exit points (reference: {reference}).',
  'ev.severity': 'Incident severity {observed} (reference: {reference}).',
  'ev.volume': 'Incident volume {observed} (reference: {reference}).',
  'ev.fallback': 'Observed {observed}, reference {reference}.',

  'dev.above': '{sigma}σ above',
  'dev.below': '{sigma}σ below',
  'dev.at_norm': 'at the norm',
  'dev.unknown': '{sigma}σ deviation',
  'dev.tail': '{tail} · at this level',
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
  'detail.explained': '{explained} of the score explained · {unexplained} interaction residual',
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

/** The one-line numeric evidence under a reason title. */
export function evidenceLine(locale: Locale, reason: Reason): string {
  const observed = formatValue(locale, reason.observed_value, reason.value_kind)
  const reference = formatValue(locale, reason.reference_value, reason.value_kind)
  const key = CATALOG[locale][`ev.${reason.family_key}`] ? `ev.${reason.family_key}` : 'ev.fallback'
  return fmt(locale, key, { observed, reference, count: reason.evidence_count })
}

/** How far from the named reference, and in which direction. Never guesses an unknown sign. */
export function deviationLine(locale: Locale, reason: Reason): string {
  const sigma = reason.deviation_sigma === null ? null : Math.abs(reason.deviation_sigma)
  const sigmaText = sigma === null ? '—' : num(locale, sigma, 1)
  return fmt(locale, `dev.${reason.deviation}`, { sigma: sigmaText })
}

export const DIMENSION_COLOR: Record<string, string> = {
  self: '#F58518',
  peer: '#54A24B',
  population: '#4C78A8',
  context: '#B279A2',
}
