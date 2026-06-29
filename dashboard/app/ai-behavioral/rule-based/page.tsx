'use client'

import { useState, useEffect, useMemo } from 'react'
import { useRouter } from 'next/navigation'
import apiClient from '@/lib/axios'
import EntityDetailModal from '@/components/EntityDetailModal'
import { BarChart3, RefreshCw } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import { useTranslation } from '@/components/LanguageProvider'

interface RuleBasedAnalysis {
  entity_type: string
  entity_id: string
  risk_score: number
  anomaly_level: string
  ai_explanation: string
  ai_recommendation: string
  reference_incident_ids: number[]
  analysis_metadata: Record<string, any>
  analysis_date: string
}

interface RuleBasedOverview {
  total_analyzed: number
  high_anomaly_count: number
  medium_anomaly_count: number
  low_anomaly_count: number
  user_anomalies: RuleBasedAnalysis[]
  channel_anomalies: RuleBasedAnalysis[]
  department_anomalies: RuleBasedAnalysis[]
  destination_anomalies: RuleBasedAnalysis[]
  rule_anomalies: RuleBasedAnalysis[]
}

type EntityTab = 'users' | 'channels' | 'departments' | 'destinations' | 'rules'

const getAnomalyColor = (level: string) => {
  switch (level?.toLowerCase()) {
    case 'critical': return '#7c2d12'
    case 'high':     return '#dc2626'
    case 'medium':   return '#f59e0b'
    case 'low':      return '#10b981'
    default:         return '#6b7280'
  }
}

const getRiskColor = (score: number) => {
  if (score >= 85) return '#7c2d12'
  if (score >= 65) return '#dc2626'
  if (score >= 40) return '#f59e0b'
  return '#10b981'
}

export default function RuleBasedPage() {
  const router = useRouter()
  const { t } = useTranslation()

  const [overview, setOverview] = useState<RuleBasedOverview | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lookbackDays, setLookbackDays] = useState(30)
  const [activeTab, setActiveTab] = useState<EntityTab>('users')
  const [filterText, setFilterText] = useState('')
  const [showDropdown, setShowDropdown] = useState(false)
  const [currentPage, setCurrentPage] = useState(1)
  const [detailOpen, setDetailOpen] = useState(false)
  const [detailEntity, setDetailEntity] = useState<{ type: string; id: string } | null>(null)
  const itemsPerPage = 50

  const fetchOverview = async (forceRefresh = false) => {
    setLoading(true)
    setError(null)
    try {
      const res = await apiClient.get('/api/ai-behavioral/overview', {
        params: { lookbackDays, forceRefresh }
      })
      setOverview(res.data)
    } catch (err: any) {
      setError(err?.response?.data?.detail || err?.message || 'Yüklenemedi')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { fetchOverview() }, [lookbackDays])
  useEffect(() => { setCurrentPage(1) }, [filterText, activeTab])

  const currentTabData = useMemo(() => {
    if (!overview) return [] as RuleBasedAnalysis[]
    switch (activeTab) {
      case 'users':        return overview.user_anomalies || []
      case 'channels':     return overview.channel_anomalies || []
      case 'departments':  return overview.department_anomalies || []
      case 'destinations': return overview.destination_anomalies || []
      case 'rules':        return overview.rule_anomalies || []
      default:             return []
    }
  }, [overview, activeTab])

  const filteredAnomalies = useMemo(() => {
    if (!filterText.trim()) return currentTabData
    return currentTabData.filter(a =>
      a.entity_id.toLowerCase().includes(filterText.toLowerCase())
    )
  }, [currentTabData, filterText])

  const totalPages = Math.ceil(filteredAnomalies.length / itemsPerPage)
  const paginatedAnomalies = useMemo(() => {
    const start = (currentPage - 1) * itemsPerPage
    return filteredAnomalies.slice(start, start + itemsPerPage)
  }, [filteredAnomalies, currentPage])

  const tabConfig: { key: EntityTab; label: string; count: number }[] = [
    { key: 'users',        label: t('ai.users'),        count: overview?.user_anomalies?.length || 0 },
    { key: 'channels',     label: t('ai.channels'),     count: overview?.channel_anomalies?.length || 0 },
    { key: 'departments',  label: t('ai.departments'),  count: overview?.department_anomalies?.length || 0 },
    { key: 'destinations', label: t('ai.destinations'), count: overview?.destination_anomalies?.length || 0 },
    { key: 'rules',        label: t('ai.rules'),        count: overview?.rule_anomalies?.length || 0 },
  ]

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '1400px', margin: '0 auto' }}>

        {/* Header */}
        <div style={{ marginBottom: '24px', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>
              📐 Kural Tabanlı Analiz
            </h1>
            <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
              Politika kurallarına göre anomali tespiti ve risk skorlaması
            </p>
          </div>

          {/* Controls */}
          <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', fontWeight: 500 }}>
              Süre:
              <select
                value={lookbackDays}
                onChange={e => setLookbackDays(Number(e.target.value))}
                style={{
                  padding: '6px 10px', border: '1px solid var(--border)', borderRadius: '6px',
                  background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px'
                }}
              >
                <option value={7}>7 Gün</option>
                <option value={14}>14 Gün</option>
                <option value={30}>30 Gün</option>
              </select>
            </label>
            <button
              onClick={() => fetchOverview(true)}
              disabled={loading}
              style={{
                padding: '8px 16px', background: 'var(--primary)', color: 'white',
                border: 'none', borderRadius: '6px', cursor: loading ? 'not-allowed' : 'pointer',
                opacity: loading ? 0.6 : 1, display: 'flex', alignItems: 'center', gap: '6px',
                fontSize: '14px', fontWeight: 600
              }}
            >
              <RefreshCw size={14} /> Yenile
            </button>
          </div>
        </div>

        {/* Loading */}
        {loading && (
          <div style={{ position: 'relative', minHeight: '300px' }}>
            <LoadingOverlay isLoading message={t('ai.loading')} />
          </div>
        )}

        {/* Error */}
        {error && !loading && (
          <div style={{
            padding: '40px', background: 'var(--surface)', borderRadius: '12px',
            textAlign: 'center', border: '1px solid rgba(239,68,68,0.2)'
          }}>
            <div style={{ fontSize: '28px', marginBottom: '12px' }}>⚠️</div>
            <div style={{ color: '#dc2626', fontWeight: 600, marginBottom: '8px' }}>{t('ai.loadFailed')}</div>
            <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '16px' }}>{error}</div>
            <button
              onClick={() => fetchOverview()}
              style={{ padding: '8px 20px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontWeight: 600 }}
            >
              {t('ai.retry')}
            </button>
          </div>
        )}

        {/* Data */}
        {!loading && !error && overview && (
          <>
            {/* Stats row */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '16px', marginBottom: '24px' }}>
              {[
                { label: t('ai.totalAnalyzed'),   value: overview.total_analyzed,      color: 'var(--text-primary)' },
                { label: t('ai.highAnomalies'),   value: overview.high_anomaly_count,  color: '#dc2626' },
                { label: t('ai.mediumAnomalies'), value: overview.medium_anomaly_count, color: '#f59e0b' },
                { label: t('ai.lowAnomalies'),    value: overview.low_anomaly_count,   color: '#10b981' },
              ].map((s, i) => (
                <div key={i} style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
                  <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '6px' }}>{s.label}</div>
                  <div style={{ fontSize: '30px', fontWeight: 700, color: s.color }}>{s.value}</div>
                </div>
              ))}
            </div>

            {/* Tabs + list */}
            <div style={{ background: 'var(--surface)', borderRadius: '12px', border: '1px solid var(--border)' }}>
              {/* Tab headers */}
              <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', overflowX: 'auto' }}>
                {tabConfig.map(tab => (
                  <button
                    key={tab.key}
                    onClick={() => { setActiveTab(tab.key); setFilterText('') }}
                    style={{
                      padding: '14px 22px',
                      background: activeTab === tab.key ? 'var(--primary)' : 'transparent',
                      color: activeTab === tab.key ? 'white' : 'var(--text-secondary)',
                      border: 'none', cursor: 'pointer', fontWeight: 600, fontSize: '14px',
                      display: 'flex', alignItems: 'center', gap: '8px', whiteSpace: 'nowrap'
                    }}
                  >
                    {tab.label}
                    <span style={{
                      background: activeTab === tab.key ? 'rgba(255,255,255,0.22)' : 'var(--border)',
                      padding: '2px 8px', borderRadius: '10px', fontSize: '12px'
                    }}>
                      {tab.count}
                    </span>
                  </button>
                ))}
              </div>

              {/* Filter input */}
              <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--border)', position: 'relative' }}>
                <input
                  type="text"
                  placeholder={`${t('ai.filter')} ${activeTab}...`}
                  value={filterText}
                  onChange={e => setFilterText(e.target.value)}
                  onFocus={() => setShowDropdown(true)}
                  onBlur={() => setTimeout(() => setShowDropdown(false), 200)}
                  style={{
                    width: '100%', maxWidth: '400px', padding: '9px 14px',
                    border: '1px solid var(--border)', borderRadius: '8px',
                    background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px'
                  }}
                />
                {showDropdown && filterText && (
                  <div style={{
                    position: 'absolute', top: '100%', left: '16px', width: '380px',
                    maxHeight: '240px', overflowY: 'auto', background: 'var(--surface)',
                    border: '1px solid var(--border)', borderRadius: '8px',
                    boxShadow: '0 4px 16px rgba(0,0,0,0.2)', zIndex: 100
                  }}>
                    {currentTabData
                      .filter(a => a.entity_id.toLowerCase().includes(filterText.toLowerCase()))
                      .slice(0, 20)
                      .map((a, i) => (
                        <div
                          key={i}
                          onClick={() => { setFilterText(a.entity_id); setShowDropdown(false) }}
                          style={{ padding: '9px 14px', cursor: 'pointer', borderBottom: '1px solid var(--border)', fontSize: '13px', color: 'var(--text-primary)' }}
                          onMouseEnter={e => (e.currentTarget.style.background = 'var(--primary)')}
                          onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
                        >
                          {a.entity_id}
                        </div>
                      ))}
                  </div>
                )}
              </div>

              {/* Pagination + list */}
              <div style={{ padding: '16px' }}>
                {filteredAnomalies.length > 0 && (
                  <div style={{ marginBottom: '12px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                      {(currentPage - 1) * itemsPerPage + 1}–{Math.min(currentPage * itemsPerPage, filteredAnomalies.length)} / {filteredAnomalies.length}
                    </span>
                    {totalPages > 1 && (
                      <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                        <button
                          onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                          disabled={currentPage === 1}
                          style={{
                            padding: '5px 12px', border: 'none', borderRadius: '6px', fontSize: '13px',
                            background: currentPage === 1 ? 'var(--border)' : 'var(--primary)',
                            color: currentPage === 1 ? 'var(--text-muted)' : 'white',
                            cursor: currentPage === 1 ? 'not-allowed' : 'pointer'
                          }}
                        >{t('ai.prev')}</button>
                        <span style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{currentPage} / {totalPages}</span>
                        <button
                          onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                          disabled={currentPage === totalPages}
                          style={{
                            padding: '5px 12px', border: 'none', borderRadius: '6px', fontSize: '13px',
                            background: currentPage === totalPages ? 'var(--border)' : 'var(--primary)',
                            color: currentPage === totalPages ? 'var(--text-muted)' : 'white',
                            cursor: currentPage === totalPages ? 'not-allowed' : 'pointer'
                          }}
                        >{t('ai.next')}</button>
                      </div>
                    )}
                  </div>
                )}

                {paginatedAnomalies.length === 0 ? (
                  <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
                    {t('ai.noAnomalies')}
                  </div>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {paginatedAnomalies.map((anomaly, idx) => (
                      <div
                        key={idx}
                        style={{
                          padding: '14px 16px', background: 'var(--background)', borderRadius: '8px',
                          border: `1px solid ${getAnomalyColor(anomaly.anomaly_level)}30`,
                          borderLeft: `4px solid ${getAnomalyColor(anomaly.anomaly_level)}`
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px' }}>
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontWeight: 600, fontSize: '14px', color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                              {anomaly.entity_id}
                            </div>
                            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>
                              {anomaly.ai_explanation}
                            </div>
                          </div>
                          <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexShrink: 0 }}>
                            <div style={{ textAlign: 'center' }}>
                              <div style={{ fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '2px' }}>Kural Skoru</div>
                              <div style={{ fontSize: '22px', fontWeight: 800, color: getRiskColor(anomaly.risk_score) }}>
                                {anomaly.risk_score}
                              </div>
                            </div>
                            <div style={{
                              padding: '4px 12px', borderRadius: '12px',
                              background: getAnomalyColor(anomaly.anomaly_level),
                              color: 'white', fontSize: '12px', fontWeight: 700
                            }}>
                              {anomaly.anomaly_level.toUpperCase()}
                            </div>
                            <button
                              onClick={() => {
                                setDetailEntity({ type: anomaly.entity_type, id: anomaly.entity_id })
                                setDetailOpen(true)
                              }}
                              style={{
                                padding: '6px 12px', background: '#0ea5e9', color: 'white',
                                border: 'none', borderRadius: '6px', cursor: 'pointer',
                                fontSize: '12px', fontWeight: 500, display: 'flex', alignItems: 'center', gap: '4px'
                              }}
                            >
                              <BarChart3 size={12} /> Analiz Et
                            </button>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
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
    </div>
  )
}
