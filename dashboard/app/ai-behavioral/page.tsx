'use client'

import { useState, useEffect, useCallback } from 'react'
import { useRouter } from 'next/navigation'
import apiClient from '@/lib/axios'
import { BarChart3, TrendingUp, Users, AlertTriangle, BrainCircuit, ShieldCheck, RefreshCw, Clock, Calendar, ChevronRight, Info } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import EntityDetailModal from '@/components/EntityDetailModal'

interface RuleBasedAnalysis {
  entity_type: string
  entity_id: string
  risk_score: number
  anomaly_level: string
  ai_explanation: string
}

interface RuleBasedOverview {
  total_analyzed: number
  high_anomaly_count: number
  medium_anomaly_count: number
  low_anomaly_count: number
  user_anomalies: RuleBasedAnalysis[]
}

interface IFScore {
  user_email: string
  if_score: number
  is_anomaly: boolean
  department?: string
  incident_count: number
}

interface IFOverview {
  user_scores: IFScore[]
  total_users: number
  anomaly_count: number
  score_window_days?: number
  status?: {
    last_run_at?: string
    last_user_count?: number
  }
}

const LOOKBACK_OPTIONS = [
  { label: '7 Gün',  value: 7 },
  { label: '14 Gün', value: 14 },
  { label: '30 Gün', value: 30 },
  { label: '60 Gün', value: 60 },
  { label: '90 Gün', value: 90 },
]

interface MergedUser {
  user_id: string
  rule_score: number
  rule_level: string
  ai_score: number | null
  is_ai_anomaly: boolean | null
  department?: string
  incident_count: number
  explanation: string
}

function getRuleColor(score: number) {
  if (score >= 85) return '#7c2d12'
  if (score >= 65) return '#dc2626'
  if (score >= 40) return '#f59e0b'
  return '#10b981'
}

function getAIColor(score: number) {
  if (score >= 75) return '#dc2626'
  if (score >= 50) return '#f59e0b'
  if (score >= 25) return '#3b82f6'
  return '#10b981'
}

function getLevelBg(level: string) {
  switch (level?.toLowerCase()) {
    case 'critical': return { bg: 'rgba(124,45,18,0.12)', color: '#7c2d12', border: 'rgba(124,45,18,0.25)' }
    case 'high':     return { bg: 'rgba(220,38,38,0.1)',  color: '#dc2626', border: 'rgba(220,38,38,0.25)' }
    case 'medium':   return { bg: 'rgba(245,158,11,0.1)', color: '#d97706', border: 'rgba(245,158,11,0.25)' }
    default:         return { bg: 'rgba(16,185,129,0.1)', color: '#10b981', border: 'rgba(16,185,129,0.25)' }
  }
}

export default function AIBehavioralOverviewPage() {
  const router = useRouter()
  const [lookbackDays, setLookbackDays] = useState(7)
  const [ruleOverview, setRuleOverview] = useState<RuleBasedOverview | null>(null)
  const [ifOverview, setIfOverview]     = useState<IFOverview | null>(null)
  const [ruleLoading, setRuleLoading]   = useState(true)
  const [aiLoading, setAiLoading]       = useState(true)
  const [detailOpen, setDetailOpen]     = useState(false)
  const [detailEntity, setDetailEntity] = useState<{ type: string; id: string } | null>(null)

  const fetchIF = useCallback((top20Emails?: string[]) => {
    setAiLoading(true)
    const params = top20Emails && top20Emails.length > 0
      ? { requiredEmails: top20Emails.join(',') }
      : {}
    apiClient.get('/api/isolation-forest/overview', { params })
      .then(r => setIfOverview(r.data))
      .catch(() => {})
      .finally(() => setAiLoading(false))
  }, [])

  // Initial load: fetch rule first, then IF with required emails from top 20
  useEffect(() => {
    setRuleLoading(true)
    setRuleOverview(null)
    apiClient.get('/api/ai-behavioral/overview', { params: { lookbackDays } })
      .then(r => {
        const data: RuleBasedOverview = r.data
        setRuleOverview(data)
        // Extract top 20 emails to guarantee IF scores for them
        const top20Emails = (data.user_anomalies || [])
          .sort((a, b) => b.risk_score - a.risk_score)
          .slice(0, 20)
          .map(u => u.entity_id)
        fetchIF(top20Emails)
      })
      .catch(() => { fetchIF() })
      .finally(() => setRuleLoading(false))
  }, [lookbackDays, fetchIF])
  const isLoading = ruleLoading || aiLoading

  // Merge: top 20 by rule score
  const top20: MergedUser[] = (() => {
    if (!ruleOverview) return []
    const aiMap = new Map<string, IFScore>()
    if (ifOverview) {
      ifOverview.user_scores.forEach(u => aiMap.set(u.user_email.toLowerCase(), u))
    }
    return (ruleOverview.user_anomalies || [])
      .sort((a, b) => b.risk_score - a.risk_score)
      .slice(0, 20)
      .map(u => {
        const ai = aiMap.get(u.entity_id.toLowerCase())
        return {
          user_id: u.entity_id,
          rule_score: u.risk_score,
          rule_level: u.anomaly_level,
          ai_score: ai ? ai.if_score : null,
          is_ai_anomaly: ai ? ai.is_anomaly : null,
          department: ai?.department,
          incident_count: ai?.incident_count ?? 0,
          explanation: u.ai_explanation,
        }
      })
  })()

  const stats = [
    { label: 'Toplam Analiz Edilen', value: ruleOverview?.total_analyzed ?? '—',       color: 'var(--text-primary)', icon: <Users size={18} />,        note: `Son ${lookbackDays} gün`,     loading: ruleLoading },
    { label: 'Yüksek Riskli',        value: ruleOverview?.high_anomaly_count ?? '—',   color: '#dc2626',             icon: <AlertTriangle size={18} />, note: 'Kural tabanlı',               loading: ruleLoading },
    { label: 'Orta Riskli',          value: ruleOverview?.medium_anomaly_count ?? '—', color: '#f59e0b',             icon: <TrendingUp size={18} />,    note: 'Kural tabanlı',               loading: ruleLoading },
    { label: 'AI İnceleme Adayı',    value: ifOverview?.anomaly_count ?? '—',          color: '#7c3aed',             icon: <BrainCircuit size={18} />,  note: 'Son 7 gün / tüm geçmiş',     loading: aiLoading },
  ]

  const ifLastRun = ifOverview?.status?.last_run_at
    ? new Date(ifOverview.status.last_run_at).toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })
    : null

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '28px' }}>
      <div style={{ maxWidth: '1300px', margin: '0 auto' }}>

        {/* ── Header ── */}
        <div style={{ marginBottom: '24px', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '16px', flexWrap: 'wrap' }}>
          <div>
            <h1 style={{ fontSize: '26px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '6px' }}>
              🛡️ AI Behavioral Analiz — Genel Bakış
            </h1>
            <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
              Kural tabanlı skor seçilen zaman penceresine göre hesaplanır.
               AI skoru her gün son <strong>7 günlük</strong> davranış için yenilenir ve kişinin 7 günden eski tüm geçmişiyle karşılaştırılır.
            </p>
          </div>
          <div style={{ display: 'flex', gap: '10px', flexShrink: 0 }}>
            <button
              onClick={() => router.push('/ai-behavioral/rule-based')}
              style={{ padding: '8px 16px', background: 'rgba(59,130,246,0.1)', color: '#3b82f6', border: '1px solid rgba(59,130,246,0.25)', borderRadius: '8px', cursor: 'pointer', fontWeight: 600, fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <ShieldCheck size={14} /> Kural Tabanlı Detay
            </button>
            <button
              onClick={() => router.push('/ai-behavioral/ai-model')}
              style={{ padding: '8px 16px', background: 'rgba(124,58,237,0.1)', color: '#7c3aed', border: '1px solid rgba(124,58,237,0.25)', borderRadius: '8px', cursor: 'pointer', fontWeight: 600, fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <BrainCircuit size={14} /> AI Model Detay
            </button>
          </div>
        </div>

        {/* ── Lookback window selector ── */}
        <div style={{ background: 'var(--surface)', borderRadius: '12px', padding: '16px 20px', border: '1px solid var(--border)', marginBottom: '20px', display: 'flex', alignItems: 'center', gap: '16px', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-secondary)', fontSize: '13px', fontWeight: 600 }}>
            <Calendar size={15} />
            <span>Kural Tabanlı Zaman Penceresi:</span>
          </div>
          <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
            {LOOKBACK_OPTIONS.map(opt => (
              <button
                key={opt.value}
                onClick={() => setLookbackDays(opt.value)}
                style={{
                  padding: '6px 14px', borderRadius: '20px', fontSize: '13px', fontWeight: 600,
                  cursor: 'pointer', transition: 'all 0.15s',
                  background: lookbackDays === opt.value ? 'var(--primary)' : 'var(--background)',
                  color: lookbackDays === opt.value ? 'white' : 'var(--text-secondary)',
                  border: lookbackDays === opt.value ? '1px solid var(--primary)' : '1px solid var(--border)',
                }}
              >
                {opt.label}
              </button>
            ))}
          </div>
          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)', background: 'rgba(124,58,237,0.08)', border: '1px solid rgba(124,58,237,0.2)', borderRadius: '8px', padding: '6px 12px' }}>
              <Clock size={12} color='#7c3aed' />
              <span>AI son güncelleme:</span>
              <strong style={{ color: '#7c3aed' }}>{aiLoading ? '…' : (ifLastRun ?? 'Henüz çalışmadı')}</strong>
            </div>            <button
              onClick={() => fetchIF()}
              disabled={aiLoading}
              title="AI skorlarını yenile"
              style={{ padding: '6px 10px', borderRadius: '8px', background: 'transparent', border: '1px solid var(--border)', cursor: 'pointer', color: 'var(--text-secondary)', display: 'flex', alignItems: 'center' }}
            >
              <RefreshCw size={13} style={{ animation: aiLoading ? 'spin 1s linear infinite' : 'none' }} />
            </button>
          </div>
        </div>

        {/* ── Info banners ── */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
          <div style={{ background: 'rgba(59,130,246,0.06)', border: '1px solid rgba(59,130,246,0.18)', borderRadius: '10px', padding: '12px 16px', display: 'flex', gap: '10px', alignItems: 'flex-start' }}>
            <ShieldCheck size={16} color='#3b82f6' style={{ marginTop: '2px', flexShrink: 0 }} />
            <div>
              <div style={{ fontSize: '13px', fontWeight: 700, color: '#3b82f6', marginBottom: '3px' }}>📐 Kural Tabanlı Risk Skoru</div>
              <div style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
                Seçilen <strong>{lookbackDays} günlük</strong> pencerede incident oluşturan tüm kullanıcılar için anlık hesaplanır. Politika ihlalleri, kanal ve departman anomalileri dikkate alınır.
              </div>
            </div>
          </div>
          <div style={{ background: 'rgba(124,58,237,0.06)', border: '1px solid rgba(124,58,237,0.18)', borderRadius: '10px', padding: '12px 16px', display: 'flex', gap: '10px', alignItems: 'flex-start' }}>
            <BrainCircuit size={16} color='#7c3aed' style={{ marginTop: '2px', flexShrink: 0 }} />
            <div>
              <div style={{ fontSize: '13px', fontWeight: 700, color: '#7c3aed', marginBottom: '3px' }}>🤖 AI Risk Skoru (Isolation Forest)</div>
              <div style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
                Her gün son <strong>7 günlük</strong> davranışı skorlar. Kişinin bu pencerenin öncesindeki <strong>tüm geçmişi</strong> kişisel norm, aynı dönemdeki ekip davranışı ise akran karşılaştırması olarak kullanılır.
              </div>
            </div>
          </div>
        </div>

        {/* ── Stats row ── */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: '16px', marginBottom: '24px' }}>
          {stats.map((s, i) => (
            <div key={i} style={{ background: 'var(--surface)', borderRadius: '12px', padding: '18px 20px', border: '1px solid var(--border)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: 500 }}>{s.label}</span>
                <span style={{ color: s.color, opacity: 0.7 }}>{s.icon}</span>
              </div>
              <div style={{ fontSize: '30px', fontWeight: 800, color: s.color, marginBottom: '4px' }}>{s.loading ? '…' : s.value}</div>
              <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>{s.note}</div>
            </div>
          ))}
        </div>

        {/* ── Top 20 table ── */}
        <div style={{ background: 'var(--surface)', borderRadius: '14px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ padding: '16px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <span style={{ fontWeight: 700, fontSize: '16px', color: 'var(--text-primary)' }}>🏆 En Riskli 20 Kullanıcı</span>
              <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '3px' }}>
                Kural tabanlı skora göre sıralanmış (son {lookbackDays} gün) — AI skoru son 7 günün günlük batch sonucudur
              </div>
            </div>
            {ruleLoading && <RefreshCw size={16} style={{ animation: 'spin 1s linear infinite', color: 'var(--text-secondary)' }} />}
          </div>

          {ruleLoading ? (
            <div style={{ position: 'relative', minHeight: '200px' }}>
              <LoadingOverlay isLoading message="Kural tabanlı veriler yükleniyor..." />
            </div>
          ) : top20.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-secondary)' }}>
              <div style={{ fontSize: '40px', marginBottom: '12px' }}>📭</div>
              <div>Seçilen {lookbackDays} günlük pencerede analiz verisi bulunamadı.</div>
              <div style={{ fontSize: '12px', marginTop: '6px' }}>Farklı bir zaman penceresi seçmeyi deneyin.</div>
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <thead>
                  <tr style={{ background: 'var(--background)' }}>
                    {[
                      { h: '#' },
                      { h: 'Kullanıcı' },
                      { h: 'Departman' },
                      { h: `Kural Skoru (${lookbackDays}g)`, info: '#3b82f6' },
                      { h: 'Risk Seviyesi' },
                      { h: 'AI Skoru (7 gün)', info: '#7c3aed' },
                      { h: 'AI Durumu' },
                      { h: 'Detay' },
                    ].map((col, ci) => (
                      <th key={ci} style={{ padding: '11px 16px', textAlign: 'left', color: 'var(--text-secondary)', fontWeight: 600, whiteSpace: 'nowrap', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
                        <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                          {col.h}
                          {col.info && <Info size={11} color={col.info} />}
                        </span>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {top20.map((u, i) => {
                    const lvl = getLevelBg(u.rule_level)
                    return (
                      <tr
                        key={i}
                        style={{ borderBottom: '1px solid var(--border)', transition: 'background 0.15s' }}
                        onMouseEnter={e => (e.currentTarget.style.background = 'var(--background)')}
                        onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
                      >
                        <td style={{ padding: '12px 16px', fontWeight: 700, color: i < 3 ? '#f59e0b' : 'var(--text-secondary)', fontSize: '14px', width: '48px' }}>
                          {i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : i + 1}
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <div style={{ fontWeight: 600, color: 'var(--text-primary)', maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={u.user_id}>{u.user_id}</div>
                          {u.incident_count > 0 && <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '2px' }}>Son 7 günde {u.incident_count} olay</div>}
                        </td>
                        <td style={{ padding: '12px 16px', color: 'var(--text-secondary)' }}>{u.department ?? '—'}</td>
                        <td style={{ padding: '12px 16px' }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                            <div style={{ width: '70px', height: '6px', background: 'var(--border)', borderRadius: '3px', overflow: 'hidden' }}>
                              <div style={{ height: '100%', width: `${u.rule_score}%`, background: getRuleColor(u.rule_score), borderRadius: '3px' }} />
                            </div>
                            <span style={{ fontWeight: 700, color: getRuleColor(u.rule_score), fontSize: '15px' }}>{u.rule_score}</span>
                          </div>
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <span style={{ padding: '3px 10px', borderRadius: '10px', fontSize: '11px', fontWeight: 700, background: lvl.bg, color: lvl.color, border: `1px solid ${lvl.border}` }}>
                            {u.rule_level?.toUpperCase() ?? '—'}
                          </span>
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          {aiLoading ? (
                            <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>yükleniyor…</span>
                          ) : u.ai_score !== null ? (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                              <div style={{ width: '50px', height: '6px', background: 'var(--border)', borderRadius: '3px', overflow: 'hidden' }}>
                                <div style={{ height: '100%', width: `${u.ai_score}%`, background: getAIColor(u.ai_score), borderRadius: '3px' }} />
                              </div>
                              <span style={{ fontWeight: 700, color: getAIColor(u.ai_score), fontSize: '15px' }}>{u.ai_score.toFixed(1)}</span>
                            </div>
                          ) : (
                            <span style={{ color: 'var(--text-secondary)', fontSize: '12px' }} title="Henüz AI skoru yok">—</span>
                          )}
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          {aiLoading ? (
                            <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>…</span>
                          ) : u.is_ai_anomaly === null ? (
                            <span style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>—</span>
                          ) : u.is_ai_anomaly ? (
                            <span style={{ padding: '3px 10px', borderRadius: '10px', fontSize: '11px', fontWeight: 700, background: 'rgba(220,38,38,0.1)', color: '#dc2626', border: '1px solid rgba(220,38,38,0.2)' }}>⚠️ Anormal</span>
                          ) : (
                            <span style={{ padding: '3px 10px', borderRadius: '10px', fontSize: '11px', fontWeight: 700, background: 'rgba(16,185,129,0.1)', color: '#10b981', border: '1px solid rgba(16,185,129,0.2)' }}>✓ Normal</span>
                          )}
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <button
                            onClick={() => { setDetailEntity({ type: 'user', id: u.user_id }); setDetailOpen(true) }}
                            style={{ padding: '5px 12px', background: '#0ea5e9', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', fontWeight: 500, display: 'flex', alignItems: 'center', gap: '4px' }}
                          >
                            <BarChart3 size={12} /> Detay
                          </button>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

      </div>

      {detailEntity && (
        <EntityDetailModal
          isOpen={detailOpen}
          onClose={() => setDetailOpen(false)}
          entityType={detailEntity.type}
          entityId={detailEntity.id}
        />
      )}

      <style>{`
        @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
      `}</style>
    </div>
  )
}
