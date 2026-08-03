import {
  Clock,
  MousePointerClick,
  Users,
  Filter,
  GitBranch,
  Mail,
  FileText,
  BarChart3,
  Crosshair,
  type LucideIcon,
} from 'lucide-react'

/**
 * Playbook graph model. Field names are snake_case because the API serialises every body with
 * JsonNamingPolicy.SnakeCaseLower — the graph goes to the backend verbatim, so both sides read
 * the same keys. Node `config` keys are snake_case for the same reason (dictionary keys are not
 * transformed by the naming policy, so they must already match what PlaybookEngine reads).
 */

export type PlaybookNodeType =
  | 'trigger.schedule'
  | 'trigger.manual'
  | 'source.weeklyFlags'
  | 'source.incidentMetric'
  | 'transform.filter'
  | 'logic.condition'
  | 'logic.metricThreshold'
  | 'action.sendMail'
  | 'output.report'

export interface PlaybookNode {
  id: string
  type: PlaybookNodeType
  label: string
  x: number
  y: number
  config: Record<string, any>
}

export interface PlaybookEdge {
  id: string
  source: string
  target: string
  /** 'true' | 'false' for the condition node; null/undefined for every other node. */
  source_handle?: string | null
}

export interface PlaybookGraph {
  nodes: PlaybookNode[]
  edges: PlaybookEdge[]
}

// ── Server-side shapes ─────────────────────────────────────────────────────

export interface PlaybookNodeLog {
  node_id: string
  node_type: string
  label: string
  status: 'success' | 'failed' | 'skipped'
  items_in: number
  items_out: number
  duration_ms: number
  message?: string | null
}

export type PlaybookRunStatus = 'running' | 'success' | 'failed' | 'awaiting_approval'

export interface PlaybookRun {
  id: number
  playbook_id: number
  started_at: string
  finished_at?: string | null
  status: PlaybookRunStatus
  trigger_type: 'schedule' | 'manual'
  dry_run: boolean
  mails_sent: number
  mails_pending: number
  mails_failed: number
  mails_skipped: number
  error_message?: string | null
  node_log?: PlaybookNodeLog[] | null
}

export type PlaybookMailStatus = 'pending' | 'sent' | 'failed' | 'skipped'

export interface PlaybookMailRow {
  id: number
  run_id: number
  playbook_id: number
  node_id: string
  user_email: string
  full_name?: string | null
  team?: string | null
  to_email: string
  cc_email?: string | null
  subject: string
  body_html: string
  source_criterion?: string | null
  source_criterion_label?: string | null
  trigger_count: number
  status: PlaybookMailStatus
  created_at: string
  sent_at?: string | null
  error_message?: string | null
}

export interface PlaybookValidation {
  is_valid: boolean
  errors: string[]
  warnings: string[]
}

export interface PlaybookSummary {
  id: number
  name: string
  description?: string | null
  enabled: boolean
  auto_send: boolean
  schedule_cron?: string | null
  schedule_summary: string
  last_run_at?: string | null
  next_run_at?: string | null
  created_at: string
  updated_at: string
  pending_mails: number
  last_run?: PlaybookRun | null
}

export interface PlaybookDetail extends Omit<PlaybookSummary, 'pending_mails' | 'last_run'> {
  graph: PlaybookGraph
  validation?: PlaybookValidation | null
}

// ── Weekly-flag criteria (mirrors WeeklyFlagCriterion on the server) ───────

export const WEEKLY_FLAG_CRITERIA = [
  { value: 'personal_email_senders', label: 'Şahsi Maile Gönderim Yapanlar' },
  { value: 'high_volume', label: '30 Dakikada 10+ Olay Üretenler' },
  { value: 'massive_matches', label: 'Ard Arda 500+ Eşleşmeli Olay Üretenler' },
] as const

export function criterionLabel(value?: string | null): string {
  if (!value) return '-'
  if (value === 'source.incidentMetric') return 'Incident metriği (kurum toplamı)'
  return WEEKLY_FLAG_CRITERIA.find(c => c.value === value)?.label ?? value
}

// ── Incident metric options (mirror IncidentMetricKind / IncidentBreakdownDimension) ──

export const INCIDENT_METRICS = [
  { value: 'total_incidents', label: 'Toplam incident sayısı' },
  { value: 'unique_users', label: 'Etkilenen kullanıcı sayısı' },
  { value: 'max_risk_score', label: 'En yüksek risk skoru' },
  { value: 'avg_risk_score', label: 'Ortalama risk skoru' },
] as const

export const BREAKDOWN_DIMENSIONS = [
  { value: 'channel', label: 'Kanal' },
  { value: 'policy', label: 'Politika' },
  { value: 'data_type', label: 'Veri tipi' },
  { value: 'team', label: 'Takım' },
  { value: 'severity', label: 'Şiddet' },
  { value: 'none', label: 'Kırılım yok' },
] as const

export function metricLabel(value?: string | null): string {
  return INCIDENT_METRICS.find(m => m.value === value)?.label ?? 'Toplam incident sayısı'
}

/** Placeholders a metric mail can use — the per-user tokens do not apply there. */
export const METRIC_PLACEHOLDERS = [
  { token: '{{metrik}}', desc: 'Metrik adı (ör. Toplam incident sayısı)' },
  { token: '{{deger}}', desc: 'Ölçülen değer' },
  { token: '{{esik}}', desc: 'Karşılaştırılan eşik' },
  { token: '{{toplam_incident}}', desc: 'Filtreyi geçen incident sayısı' },
  { token: '{{kullanici_sayisi}}', desc: 'Etkilenen kullanıcı sayısı' },
  { token: '{{gun}}', desc: 'Geriye dönük gün sayısı' },
  { token: '{{donem}}', desc: 'Ölçüm dönemi (tarih aralığı)' },
  { token: '{{filtreler}}', desc: 'Uygulanan filtrelerin özeti' },
  { token: '{{ozet}}', desc: 'Kırılım listesi (ör. kanal bazında sayılar)' },
  { token: '{{tarih}}', desc: 'Bugünün tarihi' },
]

// ── Canvas geometry ────────────────────────────────────────────────────────

/**
 * Nodes use a fixed footprint so edge endpoints can be computed without measuring the DOM.
 * The card clamps its summary text to fit.
 */
export const NODE_WIDTH = 240
export const NODE_HEIGHT = 96
export const PORT_RADIUS = 7
const PORT_MID_Y = NODE_HEIGHT / 2
const PORT_TRUE_Y = NODE_HEIGHT / 2 - 16
const PORT_FALSE_Y = NODE_HEIGHT / 2 + 16

export interface Point {
  x: number
  y: number
}

export function inputPortPosition(node: PlaybookNode): Point {
  return { x: node.x, y: node.y + PORT_MID_Y }
}

/**
 * Output port position. Nodes that fork (condition, metric threshold) stack their two ports
 * around the middle; everything else has a single centred port. Driven by the catalog rather
 * than a node-type check so adding another branching node needs no geometry change.
 */
export function outputPortPosition(node: PlaybookNode, handle?: string | null): Point {
  const outputs = nodeDefinition(node.type)?.outputs ?? []
  if (outputs.length > 1) {
    const index = Math.max(0, outputs.findIndex(p => p.handle === (handle ?? null)))
    return { x: node.x + NODE_WIDTH, y: node.y + (index === 0 ? PORT_TRUE_Y : PORT_FALSE_Y) }
  }
  return { x: node.x + NODE_WIDTH, y: node.y + PORT_MID_Y }
}

/** Cubic bezier with horizontal control points — the familiar n8n / flow-editor look. */
export function edgePath(from: Point, to: Point): string {
  const distance = Math.abs(to.x - from.x)
  const curve = Math.max(40, Math.min(160, distance / 2))
  return `M ${from.x} ${from.y} C ${from.x + curve} ${from.y}, ${to.x - curve} ${to.y}, ${to.x} ${to.y}`
}

// ── Node catalog ───────────────────────────────────────────────────────────

export interface NodeOutputPort {
  handle: string | null
  label?: string
}

export interface NodeDefinition {
  type: PlaybookNodeType
  label: string
  description: string
  icon: LucideIcon
  color: string
  category: 'Tetikleyici' | 'Kaynak' | 'İşlem' | 'Çıktı'
  inputs: 0 | 1
  outputs: NodeOutputPort[]
  defaultConfig: Record<string, any>
}

export const NODE_CATALOG: NodeDefinition[] = [
  {
    type: 'trigger.schedule',
    label: 'Zamanlama',
    description: 'Akışı belirlenen gün ve saatte kendiliğinden başlatır.',
    icon: Clock,
    color: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
    category: 'Tetikleyici',
    inputs: 0,
    outputs: [{ handle: null }],
    defaultConfig: { frequency: 'weekly', day_of_week: 1, hour: 9, minute: 0, cron: '0 9 * * 1' },
  },
  {
    type: 'trigger.manual',
    label: 'Manuel Tetikleyici',
    description: '"Şimdi Çalıştır" ile elle başlatılır; zamanlama yoktur.',
    icon: MousePointerClick,
    color: 'linear-gradient(135deg, #64748b, #475569)',
    category: 'Tetikleyici',
    inputs: 0,
    outputs: [{ handle: null }],
    defaultConfig: {},
  },
  {
    type: 'source.weeklyFlags',
    label: 'Haftalık Sorgu Kaynağı',
    description: 'Haftalık Sorgu kriterlerine takılan kullanıcıları listeler.',
    icon: Users,
    color: 'linear-gradient(135deg, #0ea5e9, #2563eb)',
    category: 'Kaynak',
    inputs: 1,
    outputs: [{ handle: null }],
    defaultConfig: { days: 7, criteria: WEEKLY_FLAG_CRITERIA.map(c => c.value) },
  },
  {
    type: 'source.incidentMetric',
    label: 'Incident Metriği',
    description: 'Son N gündeki incident sayısını kurum genelinde tek bir sayı olarak hesaplar.',
    icon: BarChart3,
    color: 'linear-gradient(135deg, #0891b2, #0e7490)',
    category: 'Kaynak',
    inputs: 1,
    outputs: [{ handle: null }],
    defaultConfig: {
      days: 7,
      metric: 'total_incidents',
      breakdown_by: 'channel',
      channels: [],
      data_types: [],
      actions: [],
      severities: [],
      min_severity: null,
      min_risk_score: null,
      min_matches: null,
      policy_contains: '',
      team_contains: '',
      destination_contains: '',
    },
  },
  {
    type: 'logic.metricThreshold',
    label: 'Metrik Eşiği',
    description: 'Metrik belirlediğin eşiği geçerse akışı sürdürür, geçmezse durdurur.',
    icon: Crosshair,
    color: 'linear-gradient(135deg, #f97316, #ea580c)',
    category: 'İşlem',
    inputs: 1,
    outputs: [
      { handle: 'true', label: 'Aşıldı' },
      { handle: 'false', label: 'Aşılmadı' },
    ],
    defaultConfig: { op: 'gt', value: 100 },
  },
  {
    type: 'transform.filter',
    label: 'Filtre',
    description: 'Listeyi olay sayısı, takım, alan adı ve muafiyetlere göre daraltır.',
    icon: Filter,
    color: 'linear-gradient(135deg, #14b8a6, #0d9488)',
    category: 'İşlem',
    inputs: 1,
    outputs: [{ handle: null }],
    defaultConfig: { min_trigger_count: null, team_contains: '', email_domain_in: [], exclude_users: [] },
  },
  {
    type: 'logic.condition',
    label: 'Koşul',
    description: 'Eşiğe göre akışı "Evet" ve "Hayır" kollarına ayırır.',
    icon: GitBranch,
    color: 'linear-gradient(135deg, #f59e0b, #eab308)',
    category: 'İşlem',
    inputs: 1,
    outputs: [
      { handle: 'true', label: 'Evet' },
      { handle: 'false', label: 'Hayır' },
    ],
    defaultConfig: { field: 'triggerCount', op: 'gte', value: 5 },
  },
  {
    type: 'action.sendMail',
    label: 'Mail Gönder',
    description: 'Seçili şablonla her kullanıcıya sorgu maili hazırlar.',
    icon: Mail,
    color: 'linear-gradient(135deg, #ef4444, #f97316)',
    category: 'İşlem',
    inputs: 1,
    outputs: [{ handle: null }],
    defaultConfig: {
      template_id: null,
      subject_override: '',
      body_override: '',
      cc_email: '',
      recipient_mode: 'user',
      fixed_recipient: '',
    },
  },
  {
    type: 'output.report',
    label: 'Rapor Çıktısı',
    description: 'Gönderimleri tarih, konu ve durumla raporlar; dışa aktarılabilir.',
    icon: FileText,
    color: 'linear-gradient(135deg, #8b5cf6, #d946ef)',
    category: 'Çıktı',
    inputs: 1,
    outputs: [],
    defaultConfig: { title: 'Haftalık Sorgu Raporu' },
  },
]

export function nodeDefinition(type: string): NodeDefinition | undefined {
  return NODE_CATALOG.find(d => d.type === type)
}

export function isTriggerType(type: string): boolean {
  return type === 'trigger.schedule' || type === 'trigger.manual'
}

// ── Schedule helpers ───────────────────────────────────────────────────────

const DAY_NAMES = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi']

/** Compiles a schedule node's settings into the 5-field cron the backend stores. */
export function buildCron(config: Record<string, any>): string {
  const minute = clamp(Number(config.minute ?? 0), 0, 59)
  const hour = clamp(Number(config.hour ?? 9), 0, 23)
  const dayOfWeek = clamp(Number(config.day_of_week ?? 1), 0, 6)

  switch (config.frequency) {
    case 'cron':
      return String(config.cron ?? '').trim()
    case 'hourly':
      return `${minute} * * * *`
    case 'daily':
      return `${minute} ${hour} * * *`
    default:
      return `${minute} ${hour} * * ${dayOfWeek}`
  }
}

export function describeSchedule(config: Record<string, any>): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  const minute = clamp(Number(config.minute ?? 0), 0, 59)
  const hour = clamp(Number(config.hour ?? 9), 0, 23)

  switch (config.frequency) {
    case 'cron':
      return config.cron ? `Cron: ${config.cron}` : 'Cron ifadesi girilmedi'
    case 'hourly':
      return `Her saat :${pad(minute)}`
    case 'daily':
      return `Her gün ${pad(hour)}:${pad(minute)}`
    default:
      return `Her ${DAY_NAMES[clamp(Number(config.day_of_week ?? 1), 0, 6)]} ${pad(hour)}:${pad(minute)}`
  }
}

function clamp(value: number, min: number, max: number): number {
  if (!Number.isFinite(value)) return min
  return Math.min(max, Math.max(min, Math.round(value)))
}

/** Same 5-field grammar the backend's CronSchedule accepts. */
export function isValidCron(expression: string): boolean {
  const fields = (expression || '').trim().split(/\s+/)
  if (fields.length !== 5) return false
  const ranges: Array<[number, number]> = [[0, 59], [0, 23], [1, 31], [1, 12], [0, 7]]
  return fields.every((field, index) => isValidCronField(field, ranges[index][0], ranges[index][1]))
}

function isValidCronField(field: string, min: number, max: number): boolean {
  return field.split(',').every(part => {
    const [rangePart, stepPart] = part.split('/')
    if (stepPart !== undefined && !(Number(stepPart) > 0)) return false
    if (rangePart === '*') return true
    if (rangePart.includes('-')) {
      const [from, to] = rangePart.split('-').map(Number)
      return Number.isInteger(from) && Number.isInteger(to) && from >= min && to <= max && from <= to
    }
    const single = Number(rangePart)
    return Number.isInteger(single) && single >= min && single <= max
  })
}

// ── Node card summary ──────────────────────────────────────────────────────

/** One-line description shown in the node body on the canvas. */
export function describeNode(node: PlaybookNode, templateNames: Record<number, string> = {}): string {
  const config = node.config || {}

  switch (node.type) {
    case 'trigger.schedule':
      return describeSchedule(config)

    case 'trigger.manual':
      return 'Elle başlatılır'

    case 'source.weeklyFlags': {
      const criteria: string[] = Array.isArray(config.criteria) ? config.criteria : []
      const count = criteria.length || WEEKLY_FLAG_CRITERIA.length
      const suffix = count === WEEKLY_FLAG_CRITERIA.length ? 'tüm kriterler' : `${count} kriter`
      return `Son ${config.days ?? 7} gün · ${suffix}`
    }

    case 'source.incidentMetric': {
      const filters = countMetricFilters(config)
      const suffix = filters === 0 ? 'filtre yok' : `${filters} filtre`
      return `${metricLabel(config.metric)} · son ${config.days ?? 7} gün · ${suffix}`
    }

    case 'logic.metricThreshold': {
      const opLabel: Record<string, string> = { gt: '>', gte: '≥', lt: '<', lte: '≤', eq: '=' }
      return `Metrik ${opLabel[config.op] ?? '>'} ${config.value ?? 0} ise devam`
    }

    case 'transform.filter': {
      const parts: string[] = []
      if (config.min_trigger_count) parts.push(`min ${config.min_trigger_count} olay`)
      if (config.team_contains) parts.push(`takım: ${config.team_contains}`)
      const domains = toList(config.email_domain_in)
      if (domains.length) parts.push(`${domains.length} alan adı`)
      const excluded = toList(config.exclude_users)
      if (excluded.length) parts.push(`${excluded.length} muafiyet`)
      return parts.length ? parts.join(' · ') : 'Filtre tanımlı değil'
    }

    case 'logic.condition': {
      const fieldLabel = config.field === 'maxMatches' ? 'Eşleşme sayısı' : 'Olay sayısı'
      const opLabel: Record<string, string> = { gte: '≥', gt: '>', lte: '≤', lt: '<', eq: '=' }
      return `${fieldLabel} ${opLabel[config.op] ?? '≥'} ${config.value ?? 0}`
    }

    case 'action.sendMail': {
      const templateId = Number(config.template_id)
      const template = Number.isFinite(templateId) && templateId > 0 ? templateNames[templateId] : undefined
      const recipient = config.recipient_mode === 'fixed'
        ? (config.fixed_recipient || 'sabit alıcı girilmedi')
        : 'kullanıcının kendisi'
      if (template) return `${template} → ${recipient}`
      if (config.subject_override) return `${config.subject_override} → ${recipient}`
      return 'Şablon seçilmedi'
    }

    case 'output.report':
      return config.title || 'Rapor'

    default:
      return ''
  }
}

function toList(value: any): string[] {
  if (Array.isArray(value)) return value.filter(Boolean)
  if (typeof value === 'string') {
    return value.split(/[,;\n\r]/).map(s => s.trim()).filter(Boolean)
  }
  return []
}

/** How many of the incident metric node's optional filters are actually set. */
export function countMetricFilters(config: Record<string, any>): number {
  const lists = ['channels', 'data_types', 'actions', 'severities']
  const numbers = ['min_severity', 'min_risk_score', 'min_matches']
  const texts = ['policy_contains', 'team_contains', 'destination_contains']

  return lists.filter(k => toList(config[k]).length > 0).length
    + numbers.filter(k => config[k] !== null && config[k] !== undefined && config[k] !== '').length
    + texts.filter(k => String(config[k] ?? '').trim() !== '').length
}

// ── Client-side validation (mirrors PlaybookEngine.ValidateAsync) ──────────

export interface GraphValidation {
  errors: string[]
  warnings: string[]
}

export function validateGraph(graph: PlaybookGraph): GraphValidation {
  const errors: string[] = []
  const warnings: string[] = []

  if (graph.nodes.length === 0) {
    return { errors: ['Akışta hiç node yok.'], warnings }
  }

  const triggers = graph.nodes.filter(n => isTriggerType(n.type))
  if (triggers.length === 0) errors.push('Akışta bir tetikleyici (zamanlama ya da manuel) olmalı.')
  if (triggers.length > 1) errors.push('Akışta yalnızca bir tetikleyici olabilir.')

  const ids = new Set(graph.nodes.map(n => n.id))
  for (const edge of graph.edges) {
    if (!ids.has(edge.source) || !ids.has(edge.target)) {
      errors.push('Bir bağlantı var olmayan node\'a işaret ediyor.')
    }
  }

  if (hasCycle(graph)) errors.push('Akışta döngü var; node\'lar bir çevrim oluşturamaz.')

  for (const node of graph.nodes) {
    if (node.type === 'trigger.schedule') {
      const cron = buildCron(node.config || {})
      if (!isValidCron(cron)) errors.push(`'${node.label}' zamanlaması geçersiz.`)
    }

    if (node.type === 'source.weeklyFlags') {
      const criteria = Array.isArray(node.config?.criteria) ? node.config.criteria : []
      if (criteria.length === 0) errors.push(`'${node.label}' için en az bir kriter seçilmeli.`)
    }

    if (node.type === 'source.incidentMetric') {
      const days = Number(node.config?.days)
      if (!Number.isFinite(days) || days <= 0) {
        errors.push(`'${node.label}' gün sayısı 0'dan büyük olmalı.`)
      }
    }

    if (node.type === 'logic.metricThreshold') {
      const value = node.config?.value
      if (value === null || value === undefined || value === '' || !Number.isFinite(Number(value))) {
        errors.push(`'${node.label}' için bir eşik değeri girin.`)
      }
    }

    if (node.type === 'action.sendMail') {
      const templateId = Number(node.config?.template_id)
      const hasTemplate = Number.isFinite(templateId) && templateId > 0
      if (!hasTemplate && !String(node.config?.subject_override || '').trim()) {
        errors.push(`'${node.label}' için bir şablon seçin ya da konu girin.`)
      }
      if (node.config?.recipient_mode === 'fixed' && !isValidEmail(node.config?.fixed_recipient)) {
        errors.push(`'${node.label}' sabit alıcı adresi geçerli değil.`)
      }
      const cc = String(node.config?.cc_email || '').trim()
      if (cc && !isValidEmail(cc)) errors.push(`'${node.label}' CC adresi geçerli değil.`)
    }
  }

  // Metric path rules — a metric is one organisation-wide number, so nodes downstream of a
  // metric source behave differently from the per-user path. Mirrors PlaybookEngine.ValidateAsync.
  const metricSources = graph.nodes.filter(n => n.type === 'source.incidentMetric')
  const metricReach = new Set<string>()
  for (const source of metricSources) {
    for (const id of reachableFrom(source.id, graph)) metricReach.add(id)
  }

  for (const node of graph.nodes) {
    if (node.type === 'logic.metricThreshold' && !metricReach.has(node.id)) {
      errors.push(
        `'${node.label}' bir Incident Metriği node'una bağlı değil; metrik eşiği yalnızca metrik girdisiyle çalışır.`
      )
    }
    if (node.type === 'action.sendMail' && metricReach.has(node.id) && node.config?.recipient_mode !== 'fixed') {
      errors.push(
        `'${node.label}' bir metrik akışında olduğu için Alıcı "Sabit bir adres" olmalı (kurum toplamının kişisel bir adresi yok).`
      )
    }
  }

  for (const source of metricSources) {
    const hasThreshold = [...reachableFrom(source.id, graph)].some(
      id => id !== source.id && graph.nodes.find(n => n.id === id)?.type === 'logic.metricThreshold'
    )
    if (!hasThreshold) {
      warnings.push(
        `'${source.label}' bir Metrik Eşiği node'una bağlı değil; mail eşik kontrolü olmadan her çalıştırmada gönderilir.`
      )
    }
  }

  if (triggers.length === 1) {
    const reachable = reachableFrom(triggers[0].id, graph)
    const orphans = graph.nodes.filter(n => !reachable.has(n.id))
    if (orphans.length > 0) {
      warnings.push(
        `Tetikleyiciye bağlı olmayan ${orphans.length} node çalıştırılmayacak: ` +
        orphans.map(o => o.label).join(', ')
      )
    }
    if (!graph.nodes.some(n => n.type === 'source.weeklyFlags' || n.type === 'source.incidentMetric')) {
      warnings.push('Akışta veri kaynağı yok; hiçbir kullanıcı ya da metrik hesaplanmayacak.')
    }
    if (!graph.nodes.some(n => n.type === 'action.sendMail')) {
      warnings.push('Akışta mail gönderme adımı yok.')
    }
    if (!graph.nodes.some(n => n.type === 'output.report')) {
      warnings.push('Akışta rapor çıktısı yok; sonuçlar yine de mail kaydına yazılır.')
    }
  }

  return { errors, warnings }
}

export function reachableFrom(startId: string, graph: PlaybookGraph): Set<string> {
  const seen = new Set<string>()
  const stack = [startId]
  while (stack.length > 0) {
    const current = stack.pop()!
    if (seen.has(current)) continue
    seen.add(current)
    for (const edge of graph.edges) {
      if (edge.source === current) stack.push(edge.target)
    }
  }
  return seen
}

/** True when adding source → target would close a loop (target already reaches source). */
export function wouldCreateCycle(graph: PlaybookGraph, source: string, target: string): boolean {
  if (source === target) return true
  return reachableFrom(target, graph).has(source)
}

function hasCycle(graph: PlaybookGraph): boolean {
  const visiting = new Set<string>()
  const done = new Set<string>()

  const visit = (id: string): boolean => {
    if (done.has(id)) return false
    if (visiting.has(id)) return true
    visiting.add(id)
    for (const edge of graph.edges) {
      if (edge.source === id && visit(edge.target)) return true
    }
    visiting.delete(id)
    done.add(id)
    return false
  }

  return graph.nodes.some(n => visit(n.id))
}

export function isValidEmail(value: any): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value ?? '').trim())
}

// ── Graph construction helpers ─────────────────────────────────────────────

let idCounter = 0

/** Node/edge ids only need to be unique within one graph; a counter plus time is enough. */
export function newId(prefix: string): string {
  idCounter += 1
  return `${prefix}_${Date.now().toString(36)}${idCounter.toString(36)}`
}

export function createNode(type: PlaybookNodeType, x: number, y: number): PlaybookNode {
  const definition = nodeDefinition(type)
  return {
    id: newId('n'),
    type,
    label: definition?.label ?? type,
    x: Math.round(x),
    y: Math.round(y),
    config: JSON.parse(JSON.stringify(definition?.defaultConfig ?? {})),
  }
}

/**
 * The starter flow offered for a new playbook — exactly the chain the Weekly Review page
 * automates by hand: schedule → weekly flags → filter → mail → report.
 */
export function createStarterGraph(criteria?: string[]): PlaybookGraph {
  const trigger = createNode('trigger.schedule', 80, 200)
  const source = createNode('source.weeklyFlags', 380, 200)
  const filter = createNode('transform.filter', 680, 200)
  const mail = createNode('action.sendMail', 980, 200)
  const report = createNode('output.report', 1280, 200)

  if (criteria && criteria.length > 0) source.config.criteria = [...criteria]

  return {
    nodes: [trigger, source, filter, mail, report],
    edges: [
      { id: newId('e'), source: trigger.id, target: source.id },
      { id: newId('e'), source: source.id, target: filter.id },
      { id: newId('e'), source: filter.id, target: mail.id },
      { id: newId('e'), source: mail.id, target: report.id },
    ],
  }
}

/**
 * Starter flow for the incident-volume alert: count incidents weekly, and mail a summary to a
 * fixed address only when the count passes a threshold. The mail node is pre-set to a fixed
 * recipient because a metric has no personal address.
 */
export function createIncidentMetricGraph(): PlaybookGraph {
  const trigger = createNode('trigger.schedule', 80, 200)
  const metric = createNode('source.incidentMetric', 380, 200)
  const threshold = createNode('logic.metricThreshold', 680, 200)
  const mail = createNode('action.sendMail', 980, 168)
  const report = createNode('output.report', 1280, 168)

  mail.config.recipient_mode = 'fixed'
  mail.config.subject_override = 'DLP incident sayısı eşiği aşıldı: {{deger}}'
  mail.config.body_override = [
    'Merhaba,',
    '',
    '{{donem}} döneminde {{metrik}} değeri {{deger}} olarak ölçüldü ve {{esik}} eşiğini aştı.',
    '',
    'Toplam incident: {{toplam_incident}}',
    'Etkilenen kullanıcı sayısı: {{kullanici_sayisi}}',
    'Kapsam: {{filtreler}}',
    '',
    'Kırılım:',
    '{{ozet}}',
    '',
    'Bu mail RADAR agentic workflow tarafından otomatik üretildi.',
  ].join('\n')

  report.config.title = 'Incident Eşik Raporu'

  return {
    nodes: [trigger, metric, threshold, mail, report],
    edges: [
      { id: newId('e'), source: trigger.id, target: metric.id },
      { id: newId('e'), source: metric.id, target: threshold.id },
      // Only the "Aşıldı" branch continues to the mail node.
      { id: newId('e'), source: threshold.id, target: mail.id, source_handle: 'true' },
      { id: newId('e'), source: mail.id, target: report.id },
    ],
  }
}
