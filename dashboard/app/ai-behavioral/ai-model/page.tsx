'use client'

import { useState, useEffect, useMemo, useCallback } from 'react'
import apiClient from '@/lib/axios'
import { BarChart3, RefreshCw, Play, Clock, AlertTriangle, Users, Layers, ChevronDown, ChevronUp, ChevronRight, Info } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import { useTranslation } from '@/components/LanguageProvider'
import {
  fmt, num, familyLabel, dimensionLabel, evidenceLine, deviationLine, formatValue,
  DIMENSION_COLOR,
  type Reason, type DimensionConfidence, type ReasonEvidence, type ConfidenceLevel,
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

// ── One reason card ──────────────────────────────────────────────────────────

function ReasonCard({
  reason, userEmail, locale, secondary,
}: {
  reason: Reason
  userEmail: string
  locale: 'tr' | 'en'
  secondary?: boolean
}) {
  const [evidence, setEvidence] = useState<ReasonEvidence | null>(null)
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)

  const color = DIMENSION_COLOR[reason.dimension] ?? '#4C78A8'
  const raises = reason.effect === 'raises'
  const accent = secondary ? 'var(--text-secondary)' : raises ? '#dc2626' : '#10b981'

  const toggleEvidence = useCallback(async () => {
    if (open) { setOpen(false); return }
    setOpen(true)
    if (evidence || loading) return
    setLoading(true)
    try {
      const res = await apiClient.get(`/api/isolation-forest/user/${encodeURIComponent(userEmail)}/evidence`, {
        params: { family: reason.family_key, dimension: reason.dimension },
      })
      setEvidence(res.data)
    } catch {
      setEvidence({ user_email: userEmail, family_key: reason.family_key, dimension: reason.dimension, total_count: 0, incidents: [] })
    } finally {
      setLoading(false)
    }
  }, [open, evidence, loading, userEmail, reason.family_key, reason.dimension])

  const signedPoints = `${reason.impact_points > 0 ? '+' : ''}${num(locale, reason.impact_points, 1)}`

  return (
    <div style={{ padding: '14px 16px', borderTop: '1px solid var(--border)' }}>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: '10px', flexWrap: 'wrap' }}>
        <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: color, flexShrink: 0 }} />
        <span style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
          {familyLabel(locale, reason.family_key)}
        </span>
        <span style={{ fontSize: '11px', fontWeight: 600, color, background: `${color}1f`, padding: '2px 8px', borderRadius: '10px' }}>
          {dimensionLabel(locale, reason.dimension)}
        </span>
        <span style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '10px' }}>
          {/* Absolute points, not a bar renormalized to the local maximum — so #1 and #3 are
              actually comparable, and comparable across users too. */}
          <span style={{ fontSize: '14px', fontWeight: 800, color: accent }}>
            {fmt(locale, 'effect.points', { points: signedPoints })}
          </span>
          {reason.impact_share_pct > 0 && (
            <span style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
              {fmt(locale, 'effect.share', { share: `%${num(locale, reason.impact_share_pct, 0)}` })}
            </span>
          )}
        </span>
      </div>

      <div style={{ fontSize: '13px', color: 'var(--text-secondary)', lineHeight: 1.6, marginTop: '6px' }}>
        {evidenceLine(locale, reason)}{' '}
        <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{deviationLine(locale, reason)}</span>
        {reason.tail_pct > 0 && reason.tail_pct < 50 && (
          <> · {fmt(locale, 'dev.tail', { tail: `%${num(locale, reason.tail_pct, 1)}` })}</>
        )}
      </div>

      {reason.risk_reading === 'unusual_not_risky' && (
        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px', fontStyle: 'italic' }}>
          {fmt(locale, 'reading.unusual_not_risky')}
        </div>
      )}

      {reason.evidence_count > 0 && (
        <button
          onClick={toggleEvidence}
          style={{
            marginTop: '8px', padding: '4px 10px', fontSize: '12px', fontWeight: 600,
            background: 'transparent', color, border: `1px solid ${color}66`, borderRadius: '6px',
            cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '4px',
          }}
        >
          {open
            ? fmt(locale, 'detail.hideEvidence')
            : fmt(locale, 'detail.showEvidence', { n: num(locale, reason.evidence_count, 0) })}
          <ChevronRight size={12} style={{ transform: open ? 'rotate(90deg)' : 'none', transition: 'transform .15s' }} />
        </button>
      )}

      {open && (
        <div style={{ marginTop: '10px', overflowX: 'auto' }}>
          {loading && <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{fmt(locale, 'detail.evidenceLoading')}</div>}
          {!loading && evidence && evidence.incidents.length === 0 && (
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{fmt(locale, 'detail.evidenceEmpty')}</div>
          )}
          {!loading && evidence && evidence.incidents.length > 0 && (
            <>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', minWidth: '640px' }}>
                <thead>
                  <tr style={{ color: 'var(--text-secondary)', textAlign: 'left' }}>
                    {['ev.col.time', 'ev.col.channel', 'ev.col.destination', 'ev.col.action', 'ev.col.severity', 'ev.col.sensitivity', 'ev.col.matches'].map(k => (
                      <th key={k} style={{ padding: '5px 8px', fontWeight: 600, borderBottom: '1px solid var(--border)' }}>{fmt(locale, k)}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {evidence.incidents.map(i => (
                    <tr key={i.id} style={{ color: 'var(--text-primary)' }}>
                      <td style={{ padding: '5px 8px', whiteSpace: 'nowrap' }}>{new Date(i.timestamp).toLocaleString(locale === 'tr' ? 'tr-TR' : 'en-US')}</td>
                      <td style={{ padding: '5px 8px' }}>{i.channel ?? '—'}</td>
                      <td style={{ padding: '5px 8px', maxWidth: '220px', overflow: 'hidden', textOverflow: 'ellipsis' }}>{i.destination ?? '—'}</td>
                      <td style={{ padding: '5px 8px' }}>{i.action ?? '—'}</td>
                      <td style={{ padding: '5px 8px' }}>{i.severity}/5</td>
                      <td style={{ padding: '5px 8px' }}>{i.data_sensitivity}/10</td>
                      <td style={{ padding: '5px 8px' }}>{num(locale, i.max_matches, 0)}</td>
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
            <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-start', fontSize: '12px', color: 'var(--text-muted)', marginBottom: '24px', lineHeight: 1.5 }}>
              <Info size={14} style={{ flexShrink: 0, marginTop: '1px' }} />
              <span>{fmt(locale, 'stat.reviewQueueNote')}</span>
            </div>

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

  return (
    <div style={{ background: 'var(--surface)', borderRadius: '10px', border: '1px solid var(--border)', borderLeft: `4px solid ${user.is_anomaly ? '#dc2626' : '#10b981'}`, overflow: 'hidden' }}>
      {/* Collapsed row — carries the reason, not the feature name */}
      <div style={{ padding: '13px 16px', cursor: 'pointer' }} onClick={onToggle}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontWeight: 600, fontSize: '14px', color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {user.user_email}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>
              {user.department ?? '—'} · {fmt(locale, 'detail.window', { n: num(locale, user.incident_count, 0) })}
            </div>
          </div>
          <div style={{ textAlign: 'center', flexShrink: 0 }}>
            <div style={{ fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '1px' }}>{fmt(locale, 'detail.score')}</div>
            <div style={{ fontSize: '22px', fontWeight: 800, color: scoreColor(user.if_score) }}>{num(locale, user.if_score, 1)}</div>
          </div>
          <div style={{ padding: '5px 14px', borderRadius: '12px', flexShrink: 0, background: user.is_anomaly ? 'rgba(220,38,38,0.1)' : 'rgba(16,185,129,0.1)', color: user.is_anomaly ? '#dc2626' : '#10b981', border: `1px solid ${user.is_anomaly ? '#dc262640' : '#10b98140'}`, fontSize: '12px', fontWeight: 700 }}>
            {fmt(locale, user.is_anomaly ? 'detail.status.anomaly' : 'detail.status.normal')}
          </div>
          <div style={{ flexShrink: 0, fontSize: '12px', fontWeight: 600, color: CONFIDENCE_COLOR[confidence], whiteSpace: 'nowrap' }}>
            {CONFIDENCE_ICON[confidence]} {fmt(locale, `conf.${confidence}`)}
          </div>
          <div style={{ color: 'var(--text-secondary)', flexShrink: 0 }}>
            {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
          </div>
        </div>

        {/* Top reasons, with the numbers, right on the collapsed row */}
        {headline.map(r => (
          <div key={`${r.family_key}:${r.dimension}`} style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '6px', fontSize: '12px' }}>
            <span style={{ width: '7px', height: '7px', borderRadius: '50%', background: DIMENSION_COLOR[r.dimension], flexShrink: 0 }} />
            <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{familyLabel(locale, r.family_key)}</span>
            <span style={{ color: 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {evidenceLine(locale, r)} {deviationLine(locale, r)}
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
              </div>

              {topDimension && (
                <div style={{ padding: '8px 20px 0', fontSize: '12px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'detail.evidenceSplitLead', { dimension: dimensionLabel(locale, topDimension.dimension) })}
                </div>
              )}

              <div style={{ padding: '14px 20px 4px', fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', letterSpacing: '.04em' }}>
                {fmt(locale, 'detail.reasons')}
                <span style={{ fontWeight: 400, marginLeft: '8px' }}>
                  ({fmt(locale, 'detail.explained', {
                    explained: `%${num(locale, user.explained_share_pct, 0)}`,
                    unexplained: `%${num(locale, user.unexplained_share_pct, 0)}`,
                  })})
                </span>
              </div>

              {user.reasons.length === 0 && (
                <div style={{ padding: '8px 20px 16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                  {fmt(locale, 'detail.noReasons')}
                </div>
              )}
              {user.reasons.map(r => (
                <ReasonCard key={`${r.family_key}:${r.dimension}`} reason={r} userEmail={user.user_email} locale={locale} />
              ))}

              {user.secondary_signals.length > 0 && (
                <>
                  <div style={{ padding: '14px 20px 0', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '.04em' }}>
                    {fmt(locale, 'detail.secondary')}
                  </div>
                  {user.secondary_signals.map(r => (
                    <ReasonCard key={`${r.family_key}:${r.dimension}`} reason={r} userEmail={user.user_email} locale={locale} secondary />
                  ))}
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
