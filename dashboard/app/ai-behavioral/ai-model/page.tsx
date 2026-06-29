'use client'

import { useState, useEffect, useMemo } from 'react'
import apiClient from '@/lib/axios'
import EntityDetailModal from '@/components/EntityDetailModal'
import { BarChart3, RefreshCw, Play, Clock, AlertTriangle, Users, TrendingUp, ChevronDown, ChevronUp } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'

// ── Types ─────────────────────────────────────────────────────────────────────

interface FeatureContribution {
  name: string
  display_name: string
  group: string
  shap_value: number
  direction: string
  actual_value: number
}

interface IFScore {
  user_email: string
  department?: string
  calculated_at: string
  if_score: number
  is_anomaly: boolean
  incident_count: number
  top_features: FeatureContribution[]
  group_breakdown: Record<string, number>
}

interface DeptIFRisk {
  department: string
  user_count: number
  anomaly_count: number
  mean_score: number
  max_score: number
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
  department_risks: DeptIFRisk[]
}

// ── Group metadata ─────────────────────────────────────────────────────────────

const GROUP_META: Record<string, { color: string; bg: string; label: string; desc: string }> = {
  raw:      { color: '#4C78A8', bg: 'rgba(76,120,168,0.15)',  label: 'Davranış',             desc: 'Genel davranış metrikleri' },
  peer:     { color: '#54A24B', bg: 'rgba(84,162,75,0.15)',   label: 'Ekip Karşılaştırması', desc: 'Departman ortalamasından sapma' },
  self:     { color: '#F58518', bg: 'rgba(245,133,24,0.15)',  label: 'Kişisel Norm',         desc: 'Kişinin kendi geçmişine göre sapma' },
  dept_ctx: { color: '#B279A2', bg: 'rgba(178,121,162,0.15)', label: 'Departman Bağlamı',    desc: 'Departman geneli bağlam özellikleri' },
}

// ── Feature display names ─────────────────────────────────────────────────────

const FEATURE_DISPLAY_NAMES: Record<string, string> = {
  incident_count: 'Toplam Olay Sayısı', unique_policies: 'Tetiklenen Politika Çeşitliliği',
  unique_channels: 'Kullanılan Kanal Çeşitliliği', unique_destinations: 'Hedef Çeşitliliği',
  mean_severity: 'Ortalama Risk Seviyesi', max_severity: 'Maksimum Risk Seviyesi',
  high_sev_ratio: 'Yüksek Riskli Olay Oranı', high_sev_count: 'Yüksek Riskli Olay Sayısı',
  mean_action_risk: 'Ortalama Eylem Riski', allowed_ratio: 'İzin Verilen Eylem Oranı',
  allowed_count: 'İzin Verilen Eylem Sayısı', total_tx_size: 'Toplam Transfer Hacmi',
  mean_tx_size: 'Ortalama Transfer Boyutu', max_tx_size: 'Maksimum Transfer Boyutu',
  std_tx_size: 'Transfer Boyutu Değişkenliği', total_max_matches: 'Toplam Eşleşme Sayısı',
  mean_max_matches: 'Ortalama Eşleşme Sayısı', max_max_matches: 'Maksimum Eşleşme Sayısı',
  off_hours_ratio: 'İş Dışı Hareket Oranı', off_hours_count: 'İş Dışı Hareket Sayısı',
  weekend_ratio: 'Hafta Sonu Hareket Oranı', night_ratio: 'Gece Hareket Oranı',
  mean_channel_rarity: 'Ortalama Kanal Nadirliği', max_channel_rarity: 'Maksimum Kanal Nadirliği',
  incidents_per_day: 'Günlük Ortalama Olay Sayısı',
  dept_size: 'Departman Büyüklüğü', dept_mean_incident_count: 'Departman Ort. Olay Sayısı',
  dept_mean_off_hours_ratio: 'Departman Ort. İş Dışı Oranı', dept_mean_allowed_ratio: 'Departman Ort. İzin Oranı',
  dept_mean_high_sev_ratio: 'Departman Ort. Yüksek Risk Oranı', dept_mean_tx_size: 'Departman Ort. Transfer Boyutu',
  incident_count_peer_z: 'Ekibine Göre Olay Sapması', unique_policies_peer_z: 'Ekibine Göre Politika Sapması',
  off_hours_ratio_peer_z: 'Ekibine Göre İş Dışı Oran Sapması', off_hours_count_peer_z: 'Ekibine Göre İş Dışı Sayı Sapması',
  high_sev_ratio_peer_z: 'Ekibine Göre Yüksek Risk Oranı Sapması', mean_severity_peer_z: 'Ekibine Göre Risk Seviyesi Sapması',
  total_tx_size_peer_z: 'Ekibine Göre Transfer Hacmi Sapması', mean_tx_size_peer_z: 'Ekibine Göre Transfer Boyutu Sapması',
  max_self_z_tx_size: 'Kendi Normuna Göre Maks. Transfer Sapması', mean_self_z_tx_size: 'Kendi Normuna Göre Ort. Transfer Sapması',
  max_self_z_severity: 'Kendi Normuna Göre Maks. Risk Seviyesi Sapması', mean_self_z_severity: 'Kendi Normuna Göre Ort. Risk Seviyesi Sapması',
  max_self_z_channel_rarity: 'Kendi Normuna Göre Maks. Kanal Nadirliği Sapması', mean_self_z_channel_rarity: 'Kendi Normuna Göre Ort. Kanal Nadirliği Sapması',
  max_self_z_off_hours_ratio: 'Kendi Normuna Göre Maks. İş Dışı Oran Sapması', mean_self_z_off_hours_ratio: 'Kendi Normuna Göre Ort. İş Dışı Oran Sapması',
  self_outlier_count: 'Kişisel Anomali Sayısı', strong_self_outlier_count: 'Güçlü Kişisel Anomali Sayısı',
  self_outlier_ratio: 'Kişisel Anomali Oranı', strong_self_outlier_ratio: 'Güçlü Kişisel Anomali Oranı',
}

function normalizeDisplayNameToKey(displayName: string): string {
  return displayName.toLowerCase()
    .replace(/\s*\(peer\)\s*$/i, '_peer_z').replace(/\s*\(self\)\s*$/i, '_self_z')
    .replace(/\s*\(peer\)\s*/g, '_peer_z_').replace(/\s*\(self\)\s*/g, '_self_z_')
    .replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '').replace(/_+/g, '_').replace(/^_|_$/g, '')
}

function getFriendlyFeatureName(feature: FeatureContribution): string {
  if (feature.name && FEATURE_DISPLAY_NAMES[feature.name]) return FEATURE_DISPLAY_NAMES[feature.name]
  if (feature.display_name && FEATURE_DISPLAY_NAMES[feature.display_name]) return FEATURE_DISPLAY_NAMES[feature.display_name]
  if (feature.display_name) {
    const normalized = normalizeDisplayNameToKey(feature.display_name)
    if (FEATURE_DISPLAY_NAMES[normalized]) return FEATURE_DISPLAY_NAMES[normalized]
  }
  if (feature.display_name && !feature.display_name.includes('_')) return feature.display_name
  return feature.display_name || feature.name
}

// ── SHAP bar chart ────────────────────────────────────────────────────────────

function ShapBarChart({ features }: { features: FeatureContribution[] }) {
  const top = features.slice(0, 5)
  const maxAbs = Math.max(...top.map(f => Math.abs(f.shap_value)), 1e-9)
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginTop: '12px' }}>
      {top.map((f, i) => {
        const pct = Math.abs(f.shap_value) / maxAbs * 100
        const meta = GROUP_META[f.group] ?? GROUP_META['raw']
        const isRisk = f.shap_value > 0
        return (
          <div key={i} style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: '13px', color: 'var(--text-primary)', fontWeight: 500 }}>{getFriendlyFeatureName(f)}</span>
              <span style={{ fontSize: '11px', fontWeight: 700, color: isRisk ? '#ef4444' : '#10b981', background: isRisk ? 'rgba(239,68,68,0.1)' : 'rgba(16,185,129,0.1)', padding: '2px 8px', borderRadius: '10px' }}>
                {isRisk ? '🔺 Riski artırıyor' : '🔻 Riski azaltıyor'}
              </span>
            </div>
            <div style={{ height: '10px', background: 'var(--border)', borderRadius: '5px', overflow: 'hidden' }}>
              <div style={{ height: '100%', width: `${pct}%`, background: isRisk ? `linear-gradient(90deg, ${meta.color}, #ef4444)` : '#10b981', borderRadius: '5px', transition: 'width 0.4s ease' }} />
            </div>
          </div>
        )
      })}
    </div>
  )
}

const getIFColor = (score: number) => {
  if (score >= 75) return '#dc2626'
  if (score >= 50) return '#f59e0b'
  if (score >= 25) return '#3b82f6'
  return '#10b981'
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function AIModelPage() {
  const [ifOverview, setIfOverview] = useState<IFOverview | null>(null)
  const [ifLoading, setIfLoading] = useState(false)
  const [ifLookback, setIfLookback] = useState(30)
  const [triggering, setTriggering] = useState(false)
  const [expandedUser, setExpandedUser] = useState<string | null>(null)
  const [ifFilter, setIfFilter] = useState('')
  const [deptPage, setDeptPage] = useState(1)
  const [userPage, setUserPage] = useState(1)
  const [detailOpen, setDetailOpen] = useState(false)
  const [detailEntity, setDetailEntity] = useState<{ type: string; id: string } | null>(null)
  const IF_PAGE_SIZE = 10

  const fetchIFOverview = async () => {
    setIfLoading(true)
    try {
      const res = await apiClient.get('/api/isolation-forest/overview')
      setIfOverview(res.data)
    } catch (err: any) {
      console.error('IF overview error:', err)
    } finally {
      setIfLoading(false)
    }
  }

  const triggerIFRun = async () => {
    if (triggering) return
    setTriggering(true)
    try {
      await apiClient.post('/api/isolation-forest/trigger', null, { params: { lookbackDays: ifLookback } })
      let tries = 0
      const poll = setInterval(async () => {
        tries++
        try {
          const statusRes = await apiClient.get('/api/isolation-forest/status')
          if (!statusRes.data.is_running || tries > 180) {
            clearInterval(poll)
            setTriggering(false)
            await fetchIFOverview()
          }
        } catch {
          clearInterval(poll)
          setTriggering(false)
        }
      }, 2000)
    } catch {
      setTriggering(false)
    }
  }

  useEffect(() => { fetchIFOverview() }, [])

  const filteredIFUsers = useMemo(() => {
    if (!ifOverview) return []
    if (!ifFilter.trim()) return ifOverview.user_scores
    return ifOverview.user_scores.filter(u =>
      u.user_email.toLowerCase().includes(ifFilter.toLowerCase()) ||
      (u.department ?? '').toLowerCase().includes(ifFilter.toLowerCase())
    )
  }, [ifOverview, ifFilter])

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '1400px', margin: '0 auto' }}>

        {/* Header */}
        <div style={{ marginBottom: '24px', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>
              🤖 AI Risk Model Skoru
            </h1>
            <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
              Makine öğrenmesi tabanlı davranışsal anomali tespiti (Isolation Forest)
            </p>
          </div>
          {/* Controls */}
          <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', fontWeight: 500 }}>
              Süre:
              <select
                value={ifLookback}
                onChange={e => setIfLookback(Number(e.target.value))}
                style={{ padding: '6px 10px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px' }}
              >
                <option value={14}>14 Gün</option>
                <option value={30}>30 Gün</option>
                <option value={60}>60 Gün</option>
                <option value={90}>90 Gün</option>
              </select>
            </label>
            <button
              onClick={triggerIFRun}
              disabled={triggering}
              style={{ padding: '8px 18px', background: triggering ? '#6b7280' : '#7c3aed', color: 'white', border: 'none', borderRadius: '6px', cursor: triggering ? 'not-allowed' : 'pointer', display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 700, fontSize: '14px' }}
            >
              {triggering ? <><RefreshCw size={14} className="if-spin" /> Hesaplanıyor...</> : <><Play size={14} /> Şimdi Çalıştır</>}
            </button>
            <button
              onClick={fetchIFOverview}
              disabled={ifLoading}
              style={{ padding: '8px 14px', background: 'transparent', color: 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '14px' }}
            >
              <RefreshCw size={14} /> Sonuçları Yükle
            </button>
          </div>
        </div>

        {/* Loading */}
        {ifLoading && (
          <div style={{ position: 'relative', minHeight: '300px' }}>
            <LoadingOverlay isLoading message="AI Risk modeli sonuçları yükleniyor..." />
          </div>
        )}

        {/* Empty state */}
        {!ifLoading && (!ifOverview || ifOverview.total_users === 0) && (
          <div style={{ padding: '64px 40px', background: 'var(--surface)', borderRadius: '12px', textAlign: 'center', border: '1px solid var(--border)' }}>
            <div style={{ fontSize: '52px', marginBottom: '16px' }}>🤖</div>
            <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '10px' }}>Henüz AI Risk Model skoru hesaplanmamış</div>
            <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '28px', lineHeight: 1.6 }}>
              "Şimdi Çalıştır" ile ilk AI risk skorlamasını başlatın.<br />Günlük analiz her gece otomatik çalışacak.
            </div>
            <button
              onClick={triggerIFRun}
              disabled={triggering}
              style={{ padding: '12px 32px', background: triggering ? '#6b7280' : '#7c3aed', color: 'white', border: 'none', borderRadius: '10px', cursor: triggering ? 'not-allowed' : 'pointer', fontWeight: 700, fontSize: '16px', display: 'inline-flex', alignItems: 'center', gap: '10px' }}
            >
              {triggering ? <><RefreshCw size={16} className="if-spin" /> Hesaplanıyor...</> : <><Play size={16} /> Şimdi Çalıştır</>}
            </button>
          </div>
        )}

        {/* Results */}
        {!ifLoading && ifOverview && ifOverview.total_users > 0 && (
          <>
            {/* Status bar */}
            <div style={{ padding: '11px 16px', marginBottom: '20px', borderRadius: '8px', background: 'var(--surface)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: '12px', fontSize: '13px', color: 'var(--text-secondary)', flexWrap: 'wrap' }}>
              <Clock size={14} />
              <span>Son analiz: <strong style={{ color: 'var(--text-primary)' }}>{ifOverview.status?.last_run_at ? new Date(ifOverview.status.last_run_at).toLocaleString('tr-TR') : '—'}</strong></span>
              <span>•</span>
              <span><strong style={{ color: 'var(--text-primary)' }}>{ifOverview.total_users}</strong> kullanıcı analiz edildi</span>
              <span>•</span>
              <span style={{ color: '#dc2626', fontWeight: 700 }}>{ifOverview.anomaly_count} anormal davranış tespit edildi</span>
              {triggering && (
                <><span>•</span><span style={{ color: '#7c3aed', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '6px' }}><RefreshCw size={12} className="if-spin" /> Yeni analiz devam ediyor...</span></>
              )}
            </div>

            {/* Stats */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '16px', marginBottom: '24px' }}>
              {[
                { label: 'Toplam Kullanıcı',  value: ifOverview.total_users,    color: 'var(--text-primary)', icon: <Users size={16} /> },
                { label: 'Anormal Davranış',  value: ifOverview.anomaly_count,  color: '#dc2626',             icon: <AlertTriangle size={16} /> },
                { label: 'Risk Oranı',        value: `${Math.round(ifOverview.anomaly_count / Math.max(ifOverview.total_users, 1) * 100)}%`, color: '#f59e0b', icon: <TrendingUp size={16} /> },
                { label: 'Departman Sayısı',  value: ifOverview.department_risks.length, color: '#7c3aed',   icon: <BarChart3 size={16} /> },
              ].map((s, i) => (
                <div key={i} style={{ background: 'var(--surface)', padding: '18px 20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                    <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{s.label}</span>
                    <span style={{ color: s.color, opacity: 0.7 }}>{s.icon}</span>
                  </div>
                  <div style={{ fontSize: '28px', fontWeight: 800, color: s.color }}>{s.value}</div>
                </div>
              ))}
            </div>

            {/* 2-col grid: dept table + top features */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '24px' }}>

              {/* Department risk table */}
              {ifOverview.department_risks.length > 0 && (() => {
                const deptTotal = Math.ceil(ifOverview.department_risks.length / IF_PAGE_SIZE)
                const deptSlice = ifOverview.department_risks.slice((deptPage - 1) * IF_PAGE_SIZE, deptPage * IF_PAGE_SIZE)
                return (
                  <div style={{ background: 'var(--surface)', borderRadius: '12px', border: '1px solid var(--border)', overflow: 'hidden' }}>
                    <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontWeight: 700, fontSize: '15px', color: 'var(--text-primary)' }}>🏢 Departman Risk Özeti</span>
                      <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>
                        {(deptPage - 1) * IF_PAGE_SIZE + 1}–{Math.min(deptPage * IF_PAGE_SIZE, ifOverview.department_risks.length)} / {ifOverview.department_risks.length}
                      </span>
                    </div>
                    <div style={{ overflowX: 'auto' }}>
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                        <thead>
                          <tr style={{ background: 'var(--background)' }}>
                            {['Departman', 'Kullanıcı', 'Risk Altında', 'Risk Oranı', 'Ort. Skor'].map(h => (
                              <th key={h} style={{ padding: '10px 16px', textAlign: 'left', color: 'var(--text-secondary)', fontWeight: 600, whiteSpace: 'nowrap', borderBottom: '1px solid var(--border)' }}>{h}</th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {deptSlice.map((dept, i) => {
                            const anomalyRate = dept.anomaly_count / Math.max(dept.user_count, 1)
                            return (
                              <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}
                                onMouseEnter={e => (e.currentTarget.style.background = 'var(--background)')}
                                onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}>
                                <td style={{ padding: '10px 16px', fontWeight: 600, color: 'var(--text-primary)' }}>{dept.department}</td>
                                <td style={{ padding: '10px 16px', color: 'var(--text-secondary)' }}>{dept.user_count}</td>
                                <td style={{ padding: '10px 16px', color: '#dc2626', fontWeight: 700 }}>{dept.anomaly_count}</td>
                                <td style={{ padding: '10px 16px' }}>
                                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                    <div style={{ width: '60px', height: '6px', background: 'var(--border)', borderRadius: '3px' }}>
                                      <div style={{ height: '100%', width: `${anomalyRate * 100}%`, background: anomalyRate > 0.3 ? '#dc2626' : anomalyRate > 0.1 ? '#f59e0b' : '#10b981', borderRadius: '3px' }} />
                                    </div>
                                    <span style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>{Math.round(anomalyRate * 100)}%</span>
                                  </div>
                                </td>
                                <td style={{ padding: '10px 16px', fontWeight: 600, color: getIFColor(dept.mean_score) }}>{dept.mean_score.toFixed(1)}</td>
                              </tr>
                            )
                          })}
                        </tbody>
                      </table>
                    </div>
                    {deptTotal > 1 && (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '10px 20px 16px', justifyContent: 'flex-end' }}>
                        <button onClick={() => setDeptPage(p => Math.max(1, p - 1))} disabled={deptPage === 1}
                          style={{ padding: '4px 12px', borderRadius: '6px', border: 'none', cursor: deptPage === 1 ? 'not-allowed' : 'pointer', background: deptPage === 1 ? 'var(--border)' : 'var(--primary)', color: deptPage === 1 ? 'var(--text-muted)' : 'white', fontSize: '13px' }}>‹ Önceki</button>
                        <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{deptPage} / {deptTotal}</span>
                        <button onClick={() => setDeptPage(p => Math.min(deptTotal, p + 1))} disabled={deptPage === deptTotal}
                          style={{ padding: '4px 12px', borderRadius: '6px', border: 'none', cursor: deptPage === deptTotal ? 'not-allowed' : 'pointer', background: deptPage === deptTotal ? 'var(--border)' : 'var(--primary)', color: deptPage === deptTotal ? 'var(--text-muted)' : 'white', fontSize: '13px' }}>Sonraki ›</button>
                      </div>
                    )}
                  </div>
                )
              })()}

              {/* Top global risk features */}
              <div style={{ background: 'var(--surface)', borderRadius: '12px', border: '1px solid var(--border)', overflow: 'hidden' }}>
                <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border)' }}>
                  <span style={{ fontWeight: 700, fontSize: '15px', color: 'var(--text-primary)' }}>🔥 En Yüksek Riskli Davranış Göstergeleri</span>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>Tüm kullanıcılarda en çok anomaliye neden olan faktörler</div>
                </div>
                <div style={{ padding: '16px 20px' }}>
                  {(() => {
                    const featureMap: Record<string, { totalShap: number; count: number; feature: FeatureContribution }> = {}
                    ifOverview.user_scores.forEach(u => {
                      u.top_features.forEach(f => {
                        const key = f.name || f.display_name
                        if (!featureMap[key]) featureMap[key] = { totalShap: 0, count: 0, feature: f }
                        featureMap[key].totalShap += Math.abs(f.shap_value)
                        featureMap[key].count++
                      })
                    })
                    const topGlobal = Object.values(featureMap).sort((a, b) => b.totalShap - a.totalShap).slice(0, 6)
                    const maxShap = Math.max(...topGlobal.map(x => x.totalShap), 1)
                    return topGlobal.map((item, i) => {
                      const pct = item.totalShap / maxShap * 100
                      const fname = getFriendlyFeatureName(item.feature)
                      const meta = GROUP_META[item.feature.group] ?? GROUP_META['raw']
                      return (
                        <div key={i} style={{ marginBottom: '14px' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-primary)' }}>{fname}</span>
                            <span style={{ fontSize: '11px', color: meta.color, background: meta.bg, padding: '2px 8px', borderRadius: '10px', border: `1px solid ${meta.color}40`, fontWeight: 700 }}>{meta.label}</span>
                          </div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                            <div style={{ flex: 1, height: '8px', background: 'var(--border)', borderRadius: '4px', overflow: 'hidden' }}>
                              <div style={{ height: '100%', width: `${pct}%`, background: `linear-gradient(90deg, ${meta.color}, #ef4444)`, borderRadius: '4px', transition: 'width 0.4s ease' }} />
                            </div>
                            <span style={{ fontSize: '11px', color: 'var(--text-secondary)', flexShrink: 0 }}>{item.count} kişide görüldü</span>
                          </div>
                        </div>
                      )
                    })
                  })()}
                </div>
              </div>
            </div>

            {/* User list filter */}
            <div style={{ marginBottom: '14px', display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
              <input
                type="text"
                placeholder="Kullanıcı adı veya departman ara..."
                value={ifFilter}
                onChange={e => { setIfFilter(e.target.value); setUserPage(1) }}
                style={{ padding: '9px 14px', border: '1px solid var(--border)', borderRadius: '8px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px', width: '320px' }}
              />
              <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                {filteredIFUsers.filter(u => u.is_anomaly).length} anormal davranış · {filteredIFUsers.length} kullanıcı
              </span>
            </div>

            {/* User score cards */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
              {filteredIFUsers.slice((userPage - 1) * IF_PAGE_SIZE, userPage * IF_PAGE_SIZE).map((user, idx) => {
                const isExpanded = expandedUser === user.user_email
                const topFeatures = [...user.top_features].sort((a, b) => Math.abs(b.shap_value) - Math.abs(a.shap_value)).slice(0, 5)
                return (
                  <div key={idx} style={{ background: 'var(--surface)', borderRadius: '10px', border: '1px solid var(--border)', borderLeft: `4px solid ${user.is_anomaly ? '#dc2626' : '#10b981'}`, overflow: 'hidden' }}>
                    {/* Collapsed row */}
                    <div style={{ padding: '13px 16px', display: 'flex', alignItems: 'center', gap: '12px', cursor: 'pointer' }} onClick={() => setExpandedUser(isExpanded ? null : user.user_email)}>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontWeight: 600, fontSize: '14px', color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{user.user_email}</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>{user.department ?? '—'} &nbsp;·&nbsp; {user.incident_count} güvenlik olayı</div>
                      </div>
                      <div style={{ textAlign: 'center', flexShrink: 0 }}>
                        <div style={{ fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '1px' }}>AI Risk Skoru</div>
                        <div style={{ fontSize: '22px', fontWeight: 800, color: getIFColor(user.if_score) }}>{user.if_score.toFixed(1)}</div>
                      </div>
                      <div style={{ padding: '5px 14px', borderRadius: '12px', flexShrink: 0, background: user.is_anomaly ? 'rgba(220,38,38,0.1)' : 'rgba(16,185,129,0.1)', color: user.is_anomaly ? '#dc2626' : '#10b981', border: `1px solid ${user.is_anomaly ? '#dc262640' : '#10b98140'}`, fontSize: '12px', fontWeight: 700 }}>
                        {user.is_anomaly ? '⚠️ Anormal Davranış' : '✓ Normal'}
                      </div>
                      <button
                        onClick={e => { e.stopPropagation(); setDetailEntity({ type: 'user', id: user.user_email }); setDetailOpen(true) }}
                        style={{ padding: '6px 12px', background: '#0ea5e9', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px', flexShrink: 0 }}
                      >
                        <BarChart3 size={12} /> Detay
                      </button>
                      <div style={{ color: 'var(--text-secondary)', flexShrink: 0 }}>
                        {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                      </div>
                    </div>

                    {/* Expanded detail */}
                    {isExpanded && (
                      <div style={{ borderTop: '1px solid var(--border)', padding: '20px 20px 24px' }}>
                        {user.is_anomaly && (
                          <div style={{ padding: '14px 18px', borderRadius: '10px', marginBottom: '20px', background: 'rgba(220,38,38,0.05)', border: '1px solid rgba(220,38,38,0.2)' }}>
                            <div style={{ fontSize: '13px', fontWeight: 700, color: '#dc2626', marginBottom: '8px' }}>🔍 Neden anormal davranış gösteriyor?</div>
                            <p style={{ fontSize: '13px', color: 'var(--text-secondary)', lineHeight: 1.6, margin: 0 }}>
                              Bu kullanıcının son dönem davranışları, <strong style={{ color: 'var(--text-primary)' }}>kendi geçmiş normundan</strong> ve <strong style={{ color: 'var(--text-primary)' }}>ekibinden beklenenden</strong> belirgin şekilde sapma gösteriyor.
                              {topFeatures.length > 0 && (
                                <> Özellikle <strong style={{ color: '#dc2626' }}>{getFriendlyFeatureName(topFeatures[0])}</strong>{topFeatures[1] ? <> ve <strong style={{ color: '#dc2626' }}>{getFriendlyFeatureName(topFeatures[1])}</strong></> : ''} alanlarında alışılmışın dışında aktivite tespit edildi.</>
                              )}
                            </p>
                          </div>
                        )}
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 240px', gap: '24px' }}>
                          <div>
                            <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '12px' }}>📊 Risk Yaratan Başlıca Faktörler</div>
                            {topFeatures.length > 0 ? <ShapBarChart features={topFeatures} /> : <div style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>Veri yok</div>}
                          </div>
                          <div>
                            <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '10px', border: '1px solid var(--border)' }}>
                              <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '12px', fontWeight: 600 }}>Kullanıcı Risk Özeti</div>
                              {[
                                { label: 'AI Risk Skoru',  value: `${user.if_score.toFixed(1)} / 100`, color: getIFColor(user.if_score) },
                                { label: 'Durum',          value: user.is_anomaly ? 'Anormal ⚠️' : 'Normal ✓', color: user.is_anomaly ? '#dc2626' : '#10b981' },
                                { label: 'Güvenlik Olayı', value: String(user.incident_count), color: 'var(--text-primary)' },
                                { label: 'Analiz Tarihi',  value: new Date(user.calculated_at).toLocaleDateString('tr-TR'), color: 'var(--text-secondary)' },
                              ].map(row => (
                                <div key={row.label} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '8px' }}>
                                  <span style={{ color: 'var(--text-secondary)' }}>{row.label}</span>
                                  <span style={{ fontWeight: 700, color: row.color }}>{row.value}</span>
                                </div>
                              ))}
                            </div>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>

            {/* User pagination */}
            {Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE) > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '16px', justifyContent: 'flex-end' }}>
                <button onClick={() => { setUserPage(p => Math.max(1, p - 1)); setExpandedUser(null) }} disabled={userPage === 1}
                  style={{ padding: '5px 14px', borderRadius: '6px', border: 'none', cursor: userPage === 1 ? 'not-allowed' : 'pointer', background: userPage === 1 ? 'var(--border)' : 'var(--primary)', color: userPage === 1 ? 'var(--text-muted)' : 'white', fontSize: '13px' }}>‹ Önceki</button>
                <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{userPage} / {Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE)}</span>
                <button onClick={() => { setUserPage(p => Math.min(Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE), p + 1)); setExpandedUser(null) }} disabled={userPage === Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE)}
                  style={{ padding: '5px 14px', borderRadius: '6px', border: 'none', cursor: userPage === Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE) ? 'not-allowed' : 'pointer', background: userPage === Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE) ? 'var(--border)' : 'var(--primary)', color: userPage === Math.ceil(filteredIFUsers.length / IF_PAGE_SIZE) ? 'var(--text-muted)' : 'white', fontSize: '13px' }}>Sonraki ›</button>
              </div>
            )}
          </>
        )}
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
        .if-spin { animation: ifSpin 1s linear infinite; }
        @keyframes ifSpin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
      `}</style>
    </div>
  )
}
