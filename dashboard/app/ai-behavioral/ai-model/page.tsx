'use client'

import { useState, useEffect, useMemo, useCallback } from 'react'
import apiClient from '@/lib/axios'
import { BarChart3, RefreshCw, Play, Clock, AlertTriangle, Users, Layers, ChevronDown, ChevronUp, ChevronRight, Info, TrendingUp, ArrowUpRight, ArrowDownRight, Minus } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import { useTranslation } from '@/components/LanguageProvider'
import {
  fmt, num, familyLabel, dimensionLabel, readingLine, formatValue,
  groupReasonsByEvent, DIMENSION_COLOR,
  type Reason, type ReasonGroup, type DimensionConfidence, type ReasonEvidence,
  type EvidenceIncident, type ConfidenceLevel,
} from '@/lib/aiModelCatalog'

// ── Types ─────────────────────────────────────────────────────────────────────

interface IFScore {
  user_email: string
  department?: string
  calculated_at: string
  if_score: number
  anomaly_raw: number
  is_anomaly: boolean
  incident_count: number
  baseline_incident_count: number
  // Null when the user was not scored in the comparison run — never treat that as a drop to zero.
  previous_score: number | null
  previous_calculated_at: string | null
  score_delta: number | null
  previous_is_anomaly: boolean | null
  reasons: Reason[]
  secondary_signals: Reason[]
  dimensions: DimensionConfidence[]
  confidence_level: ConfidenceLevel
  explained_share_pct: number
  unexplained_share_pct: number
  cohort_rank: number
  cohort_size: number
  caveats: string[]
  team_context: Record<string, number>
}

interface IFStatus {
  last_run_at?: string
  last_user_count: number
  status: 'idle' | 'running' | 'completed' | 'failed'
  last_error?: string
  is_running: boolean
}

interface IFOverview {
  status: IFStatus
  user_scores: IFScore[]
  total_users: number
  anomaly_count: number
  score_window_days: number
  previous_run_at: string | null
  department_risks: unknown[]
}

const scoreColor = (score: number) => {
  if (score >= 75) return '#dc2626'
  if (score >= 50) return '#f59e0b'
  if (score >= 25) return '#3b82f6'
  return '#10b981'
}

const CONFIDENCE_ICON: Record<ConfidenceLevel, string> = {
  high: '▲', medium: '◆', low: '◇', none: '╱',
}

const CONFIDENCE_COLOR: Record<ConfidenceLevel, string> = {
  high: '#10b981', medium: '#f59e0b', low: '#94a3b8', none: '#94a3b8',
}

// ── Evidence split bar (the three-dimension story, finally on screen) ─────────

function EvidenceSplit({ dimensions, locale }: { dimensions: DimensionConfidence[]; locale: 'tr' | 'en' }) {
  const total = dimensions.reduce((sum, d) => sum + d.share_pct, 0)
  if (total <= 0) return null

  return (
    <div style={{ display: 'flex', height: '26px', borderRadius: '6px', overflow: 'hidden', border: '1px solid var(--border)' }}>
      {dimensions.map(d => {
        const width = d.share_pct / total * 100
        if (width < 0.5) return null
        const unavailable = !d.available
        return (
          <div
            key={d.dimension}
            title={`${dimensionLabel(locale, d.dimension)} — ${num(locale, d.share_pct, 0)}%`}
            style={{
              width: `${width}%`,
              background: unavailable ? 'var(--border)' : DIMENSION_COLOR[d.dimension],
              color: unavailable ? 'var(--text-muted)' : '#fff',
              fontSize: '11px', fontWeight: 700,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              whiteSpace: 'nowrap', overflow: 'hidden',
            }}
          >
            {width > 14 ? `${dimensionLabel(locale, d.dimension)} ${num(locale, d.share_pct, 0)}%` : ''}
          </div>
        )
      })}
    </div>
  )
}

// ── Movement since the previous run ──────────────────────────────────────────

/** Below which a move is read as "no change". Min-max normalization jitters by about this much. */
const FLAT_DELTA = 0.5

function fmtDate(locale: 'tr' | 'en', iso: string) {
  return new Date(iso).toLocaleDateString(locale === 'tr' ? 'tr-TR' : 'en-US')
}

/**
 * The score alone says where the user sits today; the delta says whether that is new. Rendered as
 * a chip next to the score because "68, and it was 24 last week" is a different alert from "68,
 * same as always".
 */
function TrendBadge({ user, locale }: { user: IFScore; locale: 'tr' | 'en' }) {
  // `?? null` rather than a null check: a backend that has not shipped these fields yet sends
  // undefined, and undefined must read as "no comparison", not fall through into NaN arithmetic.
  const delta = user.score_delta ?? null
  const previous = user.previous_score ?? null

  if (previous === null || delta === null) {
    return <span className="if-trend" style={{ color: 'var(--text-muted)' }}>{fmt(locale, 'trend.new')}</span>
  }

  const title = [
    fmt(locale, 'trend.previous', { score: num(locale, previous, 1) }),
    user.previous_calculated_at ? fmt(locale, 'trend.comparedTo', { date: fmtDate(locale, user.previous_calculated_at) }) : '',
  ].filter(Boolean).join(' · ')

  if (Math.abs(delta) < FLAT_DELTA) {
    return (
      <span className="if-trend" style={{ color: 'var(--text-muted)' }} title={title}>
        <Minus size={11} /> {fmt(locale, 'trend.flat')}
      </span>
    )
  }

  // A rising score is the thing worth noticing, so it takes the alert colour; a falling one is the
  // reassuring case and stays green.
  const up = delta > 0
  return (
    <span className="if-trend" style={{ color: up ? '#dc2626' : '#10b981' }} title={title}>
      {up ? <ArrowUpRight size={11} /> : <ArrowDownRight size={11} />}
      {fmt(locale, up ? 'trend.up' : 'trend.down', { delta: num(locale, Math.abs(delta), 1) })}
    </span>
  )
}

// ── One finding, on a single line ─────────────────────────────────────────────

/**
 * A finding never owns a card of its own any more. One incident can move several feature families
 * at once — off-hours AND classifier density AND severity — and giving each its own block made a
 * single event read as a stack of separate events. Findings are now compact rows inside the event
 * they belong to.
 */
function FindingRow({ reason, locale, secondary }: { reason: Reason; locale: 'tr' | 'en'; secondary?: boolean }) {
  const color = DIMENSION_COLOR[reason.dimension] ?? '#4C78A8'
  const raises = reason.effect === 'raises'
  const accent = secondary ? 'var(--text-secondary)' : raises ? '#dc2626' : '#10b981'
  // A tenth of a point matters at 3 points and is noise at 30, so the precision follows the value.
  const magnitude = Math.abs(reason.impact_points) >= 10
    ? num(locale, reason.impact_points, 0)
    : num(locale, reason.impact_points, 1)
  const signedPoints = `${reason.impact_points > 0 ? '+' : ''}${magnitude}`

  return (
    <div className="if-finding">
      <span className="if-finding-dot" style={{ background: color }} />

      <div style={{ minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
          <span style={{ fontSize: '13.5px', fontWeight: 650, color: 'var(--text-primary)', letterSpacing: '-.01em' }}>
            {familyLabel(locale, reason.family_key)}
          </span>
          <span className="if-dim-chip" style={{ color, background: `${color}14`, borderColor: `${color}33` }}>
            {dimensionLabel(locale, reason.dimension)}
          </span>
        </div>

        {/* One plain sentence: what happened, and how unusual it is. No sigma, no reference value,
            no tail percentile — those describe the feature matrix, not the person. */}
        <div style={{ fontSize: '12.5px', color: 'var(--text-secondary)', lineHeight: 1.55, marginTop: '3px' }}>
          {readingLine(locale, reason)}
          {reason.risk_reading === 'unusual_not_risky' && (
            <> <span style={{ fontStyle: 'italic', color: 'var(--text-muted)' }}>{fmt(locale, 'reading.unusual_not_risky')}</span></>
          )}
        </div>
      </div>

      {/* Absolute points, not a bar renormalized to the local maximum — so #1 and #3 are
          actually comparable, and comparable across users too. */}
      <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
        <div style={{ fontSize: '13.5px', fontWeight: 750, color: accent, fontVariantNumeric: 'tabular-nums' }}>
          {fmt(locale, 'effect.points', { points: signedPoints })}
        </div>
      </div>
    </div>
  )
}

// ── One event: the incident(s), and every finding that points at them ─────────

function EventCard({
  group, userEmail, locale, secondary,
}: {
  group: ReasonGroup
  userEmail: string
  locale: 'tr' | 'en'
  secondary?: boolean
}) {
  const [evidence, setEvidence] = useState<ReasonEvidence | null>(null)
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)

  // Every member of the group carries the same evidence set by construction, so any of them
  // resolves the same incidents.
  const lead = group.reasons[0]
  const single = group.incident_ids.length === 1 && group.evidence_count === 1
  const hasEvidence = group.incident_ids.length > 0

  const load = useCallback(async () => {
    if (evidence || loading) return
    setLoading(true)
    try {
      const res = await apiClient.get(`/api/isolation-forest/user/${encodeURIComponent(userEmail)}/evidence`, {
        params: { family: lead.family_key, dimension: lead.dimension },
      })
      setEvidence(res.data)
    } catch {
      setEvidence({ user_email: userEmail, family_key: lead.family_key, dimension: lead.dimension, total_count: 0, incidents: [] })
    } finally {
      setLoading(false)
    }
  }, [evidence, loading, userEmail, lead.family_key, lead.dimension])

  // A single-incident group is titled by the incident itself, so its details are not an optional
  // drill-down — they are the heading. Fetch them up front instead of hiding the event behind a click.
  useEffect(() => { if (single) load() }, [single, load])

  const toggle = useCallback(() => {
    if (!open) load()
    setOpen(prev => !prev)
  }, [open, load])

  const headline: EvidenceIncident | null = single ? evidence?.incidents[0] ?? null : null

  const kind = !hasEvidence
    ? fmt(locale, 'group.aggregate')
    : single
      ? fmt(locale, 'group.single')
      : fmt(locale, 'group.multi', { n: num(locale, group.evidence_count, 0) })

  return (
    <div className={`if-event${secondary ? ' if-event-secondary' : ''}`}>
      <div className="if-event-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', minWidth: 0 }}>
          <span className="if-event-kind">{kind}</span>

          {headline && (
            <span style={{ fontSize: '12.5px', color: 'var(--text-secondary)', minWidth: 0 }}>
              <strong style={{ color: 'var(--text-primary)', fontWeight: 600 }}>
                {new Date(headline.timestamp).toLocaleString(locale === 'tr' ? 'tr-TR' : 'en-US')}
              </strong>
              {headline.channel ? ` · ${headline.channel}` : ''}
              {headline.destination ? ` → ${headline.destination}` : ''}
            </span>
          )}
          {single && !headline && (
            <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
              {loading ? fmt(locale, 'detail.evidenceLoading') : evidence ? fmt(locale, 'group.unknownEvent') : ''}
            </span>
          )}
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', whiteSpace: 'nowrap' }}>
          <span style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
            {group.reasons.length === 1
              ? fmt(locale, 'group.oneFinding')
              : fmt(locale, 'group.findings', { n: num(locale, group.reasons.length, 0) })}
          </span>
          {group.share_pct > 0 && (
            <span className="if-event-share" title={fmt(locale, 'group.pointsNote')}>
              {fmt(locale, 'group.share', { share: `%${num(locale, group.share_pct, 0)}` })}
            </span>
          )}
        </div>
      </div>

      {headline && (
        <div className="if-event-meta">
          {fmt(locale, 'group.eventMeta', {
            severity: `${fmt(locale, 'ev.col.severity')} ${headline.severity}/5`,
            sensitivity: `${fmt(locale, 'ev.col.sensitivity')} ${headline.data_sensitivity}/10`,
            matches: num(locale, headline.max_matches, 0),
          })}
          {headline.policy ? ` · ${headline.policy}` : ''}
        </div>
      )}

      <div className="if-finding-list">
        {group.reasons.map(r => (
          <FindingRow key={`${r.family_key}:${r.dimension}`} reason={r} locale={locale} secondary={secondary} />
        ))}
      </div>

      {hasEvidence && !single && (
        <button onClick={toggle} className="if-event-btn">
          {open
            ? fmt(locale, 'detail.hideEvidence')
            : fmt(locale, 'detail.showEvidence', { n: num(locale, group.evidence_count, 0) })}
          <ChevronRight size={12} style={{ transform: open ? 'rotate(90deg)' : 'none', transition: 'transform .15s' }} />
        </button>
      )}

      {open && !single && (
        <div style={{ marginTop: '10px', overflowX: 'auto' }}>
          {loading && <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{fmt(locale, 'detail.evidenceLoading')}</div>}
          {!loading && evidence && evidence.incidents.length === 0 && (
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{fmt(locale, 'detail.evidenceEmpty')}</div>
          )}
          {!loading && evidence && evidence.incidents.length > 0 && (
            <>
              <table className="if-event-table">
                <thead>
                  <tr>
                    {['ev.col.time', 'ev.col.channel', 'ev.col.destination', 'ev.col.action', 'ev.col.severity', 'ev.col.sensitivity', 'ev.col.matches'].map(k => (
                      <th key={k}>{fmt(locale, k)}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {evidence.incidents.map(i => (
                    <tr key={i.id}>
                      <td style={{ whiteSpace: 'nowrap' }}>{new Date(i.timestamp).toLocaleString(locale === 'tr' ? 'tr-TR' : 'en-US')}</td>
                      <td>{i.channel ?? '—'}</td>
                      <td style={{ maxWidth: '220px', overflow: 'hidden', textOverflow: 'ellipsis' }}>{i.destination ?? '—'}</td>
                      <td>{i.action ?? '—'}</td>
                      <td>{i.severity}/5</td>
                      <td>{i.data_sensitivity}/10</td>
                      <td>{num(locale, i.max_matches, 0)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {evidence.total_count > evidence.incidents.length && (
                <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '6px' }}>
                  {fmt(locale, 'detail.evidenceTruncated', {
                    shown: num(locale, evidence.incidents.length, 0),
                    total: num(locale, evidence.total_count, 0),
                  })}
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function AIModelPage() {
  const { locale } = useTranslation()
  const [overview, setOverview] = useState<IFOverview | null>(null)
  const [loading, setLoading] = useState(false)
  const [triggering, setTriggering] = useState(false)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [filter, setFilter] = useState('')
  const [page, setPage] = useState(1)
  const PAGE_SIZE = 10

  const fetchOverview = useCallback(async () => {
    setLoading(true)
    try {
      const res = await apiClient.get('/api/isolation-forest/overview')
      setOverview(res.data)
    } catch (err) {
      console.error('IF overview error:', err)
    } finally {
      setLoading(false)
    }
  }, [])

  const triggerRun = useCallback(async () => {
    if (triggering) return
    setTriggering(true)
    try {
      await apiClient.post('/api/isolation-forest/trigger')
      let tries = 0
      const poll = setInterval(async () => {
        tries++
        try {
          const statusRes = await apiClient.get('/api/isolation-forest/status')
          if (!statusRes.data.is_running || tries > 180) {
            clearInterval(poll)
            setTriggering(false)
            await fetchOverview()
          }
        } catch {
          clearInterval(poll)
          setTriggering(false)
        }
      }, 2000)
    } catch {
      setTriggering(false)
    }
  }, [triggering, fetchOverview])

  useEffect(() => { fetchOverview() }, [fetchOverview])

  const filtered = useMemo(() => {
    if (!overview) return []
    if (!filter.trim()) return overview.user_scores
    const needle = filter.toLowerCase()
    return overview.user_scores.filter(u =>
      u.user_email.toLowerCase().includes(needle) ||
      (u.department ?? '').toLowerCase().includes(needle))
  }, [overview, filter])

  const meanScore = useMemo(() => {
    if (!overview || overview.user_scores.length === 0) return 0
    return overview.user_scores.reduce((s, u) => s + u.if_score, 0) / overview.user_scores.length
  }, [overview])

  // Users who were also scored in the comparison run and moved up by more than the jitter band.
  const risingCount = useMemo(() => {
    if (!overview) return 0
    return overview.user_scores.filter(u => (u.score_delta ?? 0) > FLAT_DELTA).length
  }, [overview])

  const windowDays = overview?.score_window_days || 7
  const pageCount = Math.ceil(filtered.length / PAGE_SIZE)

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '1400px', margin: '0 auto' }}>

        {/* Header */}
        <div style={{ marginBottom: '24px', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>
              {fmt(locale, 'page.title')}
            </h1>
            <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>{fmt(locale, 'page.subtitle')}</p>
          </div>
          <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
            <button
              onClick={triggerRun}
              disabled={triggering}
              style={{ padding: '8px 18px', background: triggering ? '#6b7280' : '#7c3aed', color: 'white', border: 'none', borderRadius: '6px', cursor: triggering ? 'not-allowed' : 'pointer', display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 700, fontSize: '14px' }}
            >
              {triggering
                ? <><RefreshCw size={14} className="if-spin" /> {fmt(locale, 'page.running')}</>
                : <><Play size={14} /> {fmt(locale, 'page.runNow')}</>}
            </button>
            <button
              onClick={fetchOverview}
              disabled={loading}
              style={{ padding: '8px 14px', background: 'transparent', color: 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '14px' }}
            >
              <RefreshCw size={14} /> {fmt(locale, 'page.reload')}
            </button>
          </div>
        </div>

        {/* Model contract */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '12px', marginBottom: '24px' }}>
          {[
            { icon: <Clock size={18} />, title: 'card.daily', text: 'card.dailyBody', color: '#7c3aed' },
            { icon: <BarChart3 size={18} />, title: 'card.window', text: 'card.windowBody', color: '#2563eb' },
            { icon: <Layers size={18} />, title: 'card.baseline', text: 'card.baselineBody', color: '#059669' },
          ].map(item => (
            <div key={item.title} style={{ padding: '16px 18px', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '10px', display: 'flex', gap: '12px' }}>
              <span style={{ color: item.color, marginTop: '2px' }}>{item.icon}</span>
              <div>
                <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '3px' }}>{fmt(locale, item.title)}</div>
                <div style={{ fontSize: '12px', lineHeight: 1.5, color: 'var(--text-secondary)' }}>{fmt(locale, item.text)}</div>
              </div>
            </div>
          ))}
        </div>

        {loading && (
          <div style={{ position: 'relative', minHeight: '300px' }}>
            <LoadingOverlay isLoading message={fmt(locale, 'page.loading')} />
          </div>
        )}

        {!loading && (!overview || overview.total_users === 0) && (
          <div style={{ padding: '64px 40px', background: 'var(--surface)', borderRadius: '12px', textAlign: 'center', border: '1px solid var(--border)' }}>
            <div style={{ fontSize: '52px', marginBottom: '16px' }}>🤖</div>
            <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '10px' }}>{fmt(locale, 'page.emptyTitle')}</div>
            <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '28px', lineHeight: 1.6 }}>{fmt(locale, 'page.emptyBody')}</div>
            <button
              onClick={triggerRun}
              disabled={triggering}
              style={{ padding: '12px 32px', background: triggering ? '#6b7280' : '#7c3aed', color: 'white', border: 'none', borderRadius: '10px', cursor: triggering ? 'not-allowed' : 'pointer', fontWeight: 700, fontSize: '16px', display: 'inline-flex', alignItems: 'center', gap: '10px' }}
            >
              {triggering
                ? <><RefreshCw size={16} className="if-spin" /> {fmt(locale, 'page.running')}</>
                : <><Play size={16} /> {fmt(locale, 'page.runNow')}</>}
            </button>
          </div>
        )}

        {!loading && overview && overview.total_users > 0 && (
          <>
            {/* Status bar */}
            <div style={{ padding: '11px 16px', marginBottom: '20px', borderRadius: '8px', background: 'var(--surface)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: '12px', fontSize: '13px', color: 'var(--text-secondary)', flexWrap: 'wrap' }}>
              <Clock size={14} />
              <span>{fmt(locale, 'page.lastRun')}: <strong style={{ color: 'var(--text-primary)' }}>{overview.status?.last_run_at ? new Date(overview.status.last_run_at).toLocaleString(locale === 'tr' ? 'tr-TR' : 'en-US') : '—'}</strong></span>
              <span>•</span>
              <span><strong style={{ color: 'var(--text-primary)' }}>{fmt(locale, 'page.window', { days: windowDays })}</strong></span>
              <span>•</span>
              <span>{fmt(locale, 'page.baseline')}</span>
              <span>•</span>
              <span>{fmt(locale, 'page.analyzed', { n: num(locale, overview.total_users, 0) })}</span>
              <span>•</span>
              <span style={{ color: '#dc2626', fontWeight: 700 }}>{fmt(locale, 'page.reviewQueue', { n: num(locale, overview.anomaly_count, 0) })}</span>
              {overview.previous_run_at && (
                <>
                  <span>•</span>
                  <span>{fmt(locale, 'trend.comparedTo', { date: fmtDate(locale, overview.previous_run_at) })}</span>
                </>
              )}
              {triggering && (
                <><span>•</span><span style={{ color: '#7c3aed', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '6px' }}><RefreshCw size={12} className="if-spin" /> {fmt(locale, 'page.rerunning')}</span></>
              )}
            </div>

            {/* Stats */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '16px', marginBottom: '10px' }}>
              {[
                { label: 'stat.totalUsers', value: num(locale, overview.total_users, 0), color: 'var(--text-primary)', icon: <Users size={16} /> },
                { label: 'stat.reviewQueue', value: num(locale, overview.anomaly_count, 0), color: '#dc2626', icon: <AlertTriangle size={16} /> },
                { label: 'stat.meanScore', value: num(locale, meanScore, 1), color: '#f59e0b', icon: <BarChart3 size={16} /> },
                { label: 'stat.window', value: fmt(locale, 'stat.windowValue', { days: windowDays }), color: '#7c3aed', icon: <Clock size={16} /> },
                ...(overview.previous_run_at
                  ? [{ label: 'stat.rising', value: num(locale, risingCount, 0), color: '#dc2626', icon: <TrendingUp size={16} /> }]
                  : []),
              ].map(s => (
                <div key={s.label} style={{ background: 'var(--surface)', padding: '18px 20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                    <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{fmt(locale, s.label)}</span>
                    <span style={{ color: s.color, opacity: 0.7 }}>{s.icon}</span>
                  </div>
                  <div style={{ fontSize: '28px', fontWeight: 800, color: s.color }}>{s.value}</div>
                </div>
              ))}
            </div>

            {/* The review queue is a fixed top-5% slice, so say so rather than presenting it as a rate. */}
            <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-start', fontSize: '12px', color: 'var(--text-muted)', marginBottom: overview.previous_run_at ? '6px' : '24px', lineHeight: 1.5 }}>
              <Info size={14} style={{ flexShrink: 0, marginTop: '1px' }} />
              <span>{fmt(locale, 'stat.reviewQueueNote')}</span>
            </div>

            {overview.previous_run_at && (
              <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-start', fontSize: '12px', color: 'var(--text-muted)', marginBottom: '24px', lineHeight: 1.5 }}>
                <Info size={14} style={{ flexShrink: 0, marginTop: '1px' }} />
                <span>{fmt(locale, 'stat.risingNote', { date: fmtDate(locale, overview.previous_run_at) })}</span>
              </div>
            )}

            {/* Filter */}
            <div style={{ marginBottom: '14px' }}>
              <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>{fmt(locale, 'page.listTitle')}</div>
              <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '12px' }}>{fmt(locale, 'page.listSubtitle')}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
                <input
                  type="text"
                  placeholder={fmt(locale, 'page.search')}
                  value={filter}
                  onChange={e => { setFilter(e.target.value); setPage(1) }}
                  style={{ padding: '9px 14px', border: '1px solid var(--border)', borderRadius: '8px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px', width: '320px' }}
                />
                <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'page.counts', {
                    anomalies: num(locale, filtered.filter(u => u.is_anomaly).length, 0),
                    total: num(locale, filtered.length, 0),
                  })}
                </span>
              </div>
            </div>

            {/* User cards */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
              {filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE).map(user => (
                <UserCard
                  key={user.user_email}
                  user={user}
                  locale={locale}
                  isExpanded={expanded === user.user_email}
                  onToggle={() => setExpanded(expanded === user.user_email ? null : user.user_email)}
                />
              ))}
            </div>

            {pageCount > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '16px', justifyContent: 'flex-end' }}>
                <button onClick={() => { setPage(p => Math.max(1, p - 1)); setExpanded(null) }} disabled={page === 1}
                  style={{ padding: '5px 14px', borderRadius: '6px', border: 'none', cursor: page === 1 ? 'not-allowed' : 'pointer', background: page === 1 ? 'var(--border)' : 'var(--primary)', color: page === 1 ? 'var(--text-muted)' : 'white', fontSize: '13px' }}>
                  {fmt(locale, 'page.prev')}
                </button>
                <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{page} / {pageCount}</span>
                <button onClick={() => { setPage(p => Math.min(pageCount, p + 1)); setExpanded(null) }} disabled={page === pageCount}
                  style={{ padding: '5px 14px', borderRadius: '6px', border: 'none', cursor: page === pageCount ? 'not-allowed' : 'pointer', background: page === pageCount ? 'var(--border)' : 'var(--primary)', color: page === pageCount ? 'var(--text-muted)' : 'white', fontSize: '13px' }}>
                  {fmt(locale, 'page.next')}
                </button>
              </div>
            )}
          </>
        )}
      </div>

      <style>{`
        .if-spin { animation: ifSpin 1s linear infinite; }
        @keyframes ifSpin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }

        /* User card — status is carried by the chip and a hairline ring, not by a slab of colour
           down the left edge. Depth comes from a two-layer shadow: a tight contact shadow plus a
           wide, very soft ambient one. */
        .if-user-card {
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 14px;
          overflow: hidden;
          box-shadow: 0 1px 2px rgba(15, 23, 42, .05), 0 12px 28px -22px rgba(15, 23, 42, .45);
          transition: box-shadow .18s ease, transform .18s ease, border-color .18s ease;
        }
        .if-user-card:hover {
          transform: translateY(-1px);
          border-color: var(--border-hover);
          box-shadow: 0 2px 4px rgba(15, 23, 42, .06), 0 18px 36px -24px rgba(15, 23, 42, .5);
        }
        /* On the dark theme a slate shadow disappears into the page; depth has to come from a
           lighter top edge instead. */
        .dark-theme .if-user-card {
          box-shadow: inset 0 1px 0 rgba(255, 255, 255, .04), 0 14px 30px -24px rgba(0, 0, 0, .9);
        }
        .dark-theme .if-user-card:hover {
          box-shadow: inset 0 1px 0 rgba(255, 255, 255, .06), 0 20px 38px -24px rgba(0, 0, 0, 1);
        }
        /* A flagged user is marked by a hairline ring around the whole card rather than a coloured
           bar on one edge — the ring reads as a state, the bar read as decoration. */
        .if-user-card-flagged { box-shadow: 0 0 0 1px rgba(220, 38, 38, .22), 0 1px 2px rgba(15, 23, 42, .05), 0 12px 28px -22px rgba(15, 23, 42, .45); }
        .if-user-card-flagged:hover { box-shadow: 0 0 0 1px rgba(220, 38, 38, .3), 0 2px 4px rgba(15, 23, 42, .06), 0 18px 36px -24px rgba(15, 23, 42, .5); }
        .dark-theme .if-user-card-flagged { box-shadow: 0 0 0 1px rgba(248, 113, 113, .3), 0 14px 30px -24px rgba(0, 0, 0, .9); }
        .dark-theme .if-user-card-flagged:hover { box-shadow: 0 0 0 1px rgba(248, 113, 113, .42), 0 20px 38px -24px rgba(0, 0, 0, 1); }

        .if-status-chip {
          flex-shrink: 0;
          padding: 5px 13px;
          border-radius: 999px;
          border: 1px solid;
          font-size: 12px;
          font-weight: 650;
          letter-spacing: -.01em;
        }

        .if-trend {
          display: inline-flex;
          align-items: center;
          gap: 3px;
          font-size: 11.5px;
          font-weight: 650;
          font-variant-numeric: tabular-nums;
          white-space: nowrap;
          cursor: help;
        }

        .if-score-track {
          margin-top: 5px;
          height: 3px;
          border-radius: 999px;
          background: var(--border);
          overflow: hidden;
          display: block;
        }
        .if-score-track > span { display: block; height: 100%; border-radius: 999px; }

        /* Event cards — one per event, findings live inside */
        .if-event-list { display: flex; flex-direction: column; gap: 10px; padding: 10px 20px 4px; }

        .if-event {
          border: 1px solid var(--border);
          border-radius: 12px;
          background: var(--background);
          padding: 12px 14px;
        }
        .if-event-secondary { opacity: .82; }

        .if-event-head {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 12px;
          flex-wrap: wrap;
          padding-bottom: 9px;
          border-bottom: 1px solid var(--border);
        }
        .if-event-kind {
          font-size: 10.5px;
          font-weight: 700;
          letter-spacing: .06em;
          text-transform: uppercase;
          color: var(--text-muted);
          border: 1px solid var(--border);
          border-radius: 6px;
          padding: 2px 7px;
        }
        .if-event-share {
          font-size: 11.5px;
          font-weight: 650;
          color: var(--text-secondary);
          font-variant-numeric: tabular-nums;
          cursor: help;
        }
        .if-event-meta {
          font-size: 11.5px;
          color: var(--text-muted);
          padding-top: 8px;
        }

        .if-finding-list { display: flex; flex-direction: column; }
        .if-finding {
          display: grid;
          grid-template-columns: 8px 1fr auto;
          align-items: start;
          gap: 10px;
          padding: 10px 0 2px;
        }
        .if-finding + .if-finding { border-top: 1px dashed var(--border); padding-top: 10px; }
        .if-finding-dot { width: 8px; height: 8px; border-radius: 50%; margin-top: 5px; }

        .if-dim-chip {
          font-size: 10.5px;
          font-weight: 650;
          padding: 1px 7px;
          border-radius: 999px;
          border: 1px solid;
        }

        .if-event-btn {
          margin-top: 10px;
          padding: 5px 11px;
          font-size: 12px;
          font-weight: 600;
          background: transparent;
          color: var(--text-secondary);
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          display: inline-flex;
          align-items: center;
          gap: 5px;
          transition: border-color .15s ease, color .15s ease;
        }
        .if-event-btn:hover { color: var(--text-primary); border-color: var(--text-muted); }

        .if-event-table { width: 100%; border-collapse: collapse; font-size: 12px; min-width: 640px; }
        .if-event-table th {
          padding: 5px 8px;
          font-weight: 600;
          text-align: left;
          color: var(--text-secondary);
          border-bottom: 1px solid var(--border);
        }
        .if-event-table td { padding: 5px 8px; color: var(--text-primary); }
      `}</style>
    </div>
  )
}

// ── User card ────────────────────────────────────────────────────────────────

function UserCard({
  user, locale, isExpanded, onToggle,
}: {
  user: IFScore
  locale: 'tr' | 'en'
  isExpanded: boolean
  onToggle: () => void
}) {
  const confidence = user.confidence_level || 'low'
  const headline = user.reasons.slice(0, 2)
  const hasExplanation = user.reasons.length > 0 || user.secondary_signals.length > 0 || user.dimensions.length > 0
  const missingSelf = user.dimensions.find(d => d.dimension === 'self' && !d.available)
  const topDimension = [...user.dimensions].sort((a, b) => b.share_pct - a.share_pct)[0]
  const rankPct = user.cohort_size > 0 ? Math.max(1, Math.round(user.cohort_rank / user.cohort_size * 100)) : 0

  // One card per event rather than one per finding: an incident that is both off-hours and a heavy
  // classifier hit is one thing that happened, not two.
  const reasonGroups = useMemo(() => groupReasonsByEvent(user.reasons), [user.reasons])
  const secondaryGroups = useMemo(() => groupReasonsByEvent(user.secondary_signals), [user.secondary_signals])

  return (
    <div className={`if-user-card${user.is_anomaly ? ' if-user-card-flagged' : ''}`}>
      {/* Collapsed row — carries the reason, not the feature name */}
      <div style={{ padding: '15px 18px', cursor: 'pointer' }} onClick={onToggle}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontWeight: 650, fontSize: '14px', color: 'var(--text-primary)', letterSpacing: '-.01em', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {user.user_email}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '3px' }}>
              {user.department ?? '—'} · {fmt(locale, 'detail.window', { n: num(locale, user.incident_count, 0) })}
            </div>
          </div>
          <div style={{ textAlign: 'right', flexShrink: 0, minWidth: '96px' }}>
            <div style={{ fontSize: '10px', color: 'var(--text-muted)', letterSpacing: '.04em', textTransform: 'uppercase' }}>{fmt(locale, 'detail.score')}</div>
            <div style={{ fontSize: '24px', fontWeight: 750, color: scoreColor(user.if_score), fontVariantNumeric: 'tabular-nums', lineHeight: 1.15 }}>
              {num(locale, user.if_score, 1)}
            </div>
            {/* The score is a position in this run, so a meter reads truer than a bare number. */}
            <div className="if-score-track">
              <span style={{ width: `${Math.max(2, Math.min(100, user.if_score))}%`, background: scoreColor(user.if_score) }} />
            </div>
            <div style={{ marginTop: '5px', display: 'flex', justifyContent: 'flex-end' }}>
              <TrendBadge user={user} locale={locale} />
            </div>
          </div>
          <div className="if-status-chip" style={{
            color: user.is_anomaly ? '#dc2626' : '#10b981',
            background: user.is_anomaly ? 'rgba(220,38,38,0.08)' : 'rgba(16,185,129,0.08)',
            borderColor: user.is_anomaly ? 'rgba(220,38,38,0.28)' : 'rgba(16,185,129,0.28)',
          }}>
            {fmt(locale, user.is_anomaly ? 'detail.status.anomaly' : 'detail.status.normal')}
          </div>
          <div style={{ flexShrink: 0, fontSize: '12px', fontWeight: 600, color: CONFIDENCE_COLOR[confidence], whiteSpace: 'nowrap' }}>
            {CONFIDENCE_ICON[confidence]} {fmt(locale, `conf.${confidence}`)}
          </div>
          <div style={{ color: 'var(--text-muted)', flexShrink: 0, display: 'flex' }}>
            {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
          </div>
        </div>

        {/* Top reasons, with the numbers, right on the collapsed row */}
        {headline.map(r => (
          <div key={`${r.family_key}:${r.dimension}`} style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '7px', fontSize: '12px' }}>
            <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: DIMENSION_COLOR[r.dimension], flexShrink: 0 }} />
            <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{familyLabel(locale, r.family_key)}</span>
            <span style={{ color: 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {readingLine(locale, r)}
            </span>
          </div>
        ))}

        {missingSelf && (
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '6px', display: 'flex', gap: '6px', alignItems: 'center' }}>
            <Info size={12} />
            {fmt(locale, `conf.${missingSelf.reason_key ?? 'noBaseline'}`, { n: num(locale, missingSelf.reason_args?.n ?? 0, 0) })}
          </div>
        )}

        {user.dimensions.length > 0 && (
          <div style={{ marginTop: '8px' }}>
            <EvidenceSplit dimensions={user.dimensions} locale={locale} />
          </div>
        )}
      </div>

      {/* Expanded detail */}
      {isExpanded && (
        <div style={{ borderTop: '1px solid var(--border)' }}>
          {!hasExplanation && (
            <div style={{ padding: '20px', fontSize: '13px', color: 'var(--text-secondary)' }}>
              {fmt(locale, 'detail.noExplanation')}
            </div>
          )}

          {hasExplanation && (
            <>
              <div style={{ padding: '16px 20px 4px', display: 'flex', gap: '20px', flexWrap: 'wrap', alignItems: 'center' }}>
                <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'detail.rank', {
                    rank: num(locale, user.cohort_rank, 0),
                    total: num(locale, user.cohort_size, 0),
                    pct: `%${rankPct}`,
                  })}
                </div>
                <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'detail.baselineCount', { n: num(locale, user.baseline_incident_count, 0) })}
                </div>
                {user.previous_score !== null && user.previous_score !== undefined && (
                  <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                    {fmt(locale, 'trend.previous', { score: num(locale, user.previous_score, 1) })}
                    {user.previous_calculated_at && (
                      <span style={{ color: 'var(--text-muted)' }}>
                        {' '}({fmtDate(locale, user.previous_calculated_at)})
                      </span>
                    )}
                  </div>
                )}
                {user.previous_is_anomaly !== null && user.previous_is_anomaly !== undefined && user.previous_is_anomaly !== user.is_anomaly && (
                  <div style={{ fontSize: '13px', fontWeight: 600, color: user.is_anomaly ? '#dc2626' : '#10b981' }}>
                    {fmt(locale, user.is_anomaly ? 'trend.becameAnomaly' : 'trend.leftAnomaly')}
                  </div>
                )}
              </div>

              {topDimension && (
                <div style={{ padding: '8px 20px 0', fontSize: '12px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'detail.evidenceSplitLead', { dimension: dimensionLabel(locale, topDimension.dimension) })}
                </div>
              )}

              <div style={{ padding: '14px 20px 4px', fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', letterSpacing: '.04em' }}>
                {fmt(locale, 'detail.reasons')}
                <span style={{ fontWeight: 400, marginLeft: '8px' }}>
                  ({fmt(locale, 'detail.explained', { explained: `%${num(locale, user.explained_share_pct, 0)}` })})
                </span>
              </div>

              {user.reasons.length === 0 && (
                <div style={{ padding: '8px 20px 16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'detail.noReasons')}
                </div>
              )}
              {reasonGroups.length > 0 && (
                <div className="if-event-list">
                  {reasonGroups.map(g => (
                    <EventCard key={g.key} group={g} userEmail={user.user_email} locale={locale} />
                  ))}
                </div>
              )}

              {secondaryGroups.length > 0 && (
                <>
                  <div style={{ padding: '14px 20px 0', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '.04em' }}>
                    {fmt(locale, 'detail.secondary')}
                  </div>
                  <div className="if-event-list">
                    {secondaryGroups.map(g => (
                      <EventCard key={g.key} group={g} userEmail={user.user_email} locale={locale} secondary />
                    ))}
                  </div>
                </>
              )}

              {Object.keys(user.team_context).length > 0 && (
                <div style={{ padding: '14px 20px', borderTop: '1px solid var(--border)' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '.04em', marginBottom: '6px' }}>
                    {fmt(locale, 'detail.teamContext')}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                    {Object.entries(user.team_context).map(([key, value]) => (
                      <span key={key}>
                        {fmt(locale, `f.${key}`)}:{' '}
                        <strong style={{ color: 'var(--text-primary)' }}>
                          {formatValue(locale, value, key.includes('ratio') ? 'ratio' : key === 'dept_size' ? 'head' : key.includes('tx_size') ? 'sens' : 'count')}
                        </strong>
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {user.caveats.length > 0 && (
                <div style={{ padding: '12px 20px 16px', borderTop: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  {user.caveats.map(c => (
                    <div key={c} style={{ fontSize: '11px', color: 'var(--text-muted)', display: 'flex', gap: '6px', alignItems: 'flex-start', lineHeight: 1.5 }}>
                      <Info size={12} style={{ flexShrink: 0, marginTop: '2px' }} />
                      <span>{fmt(locale, `caveat.${c}`, { n: num(locale, user.cohort_size, 0) })}</span>
                    </div>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}
