'use client'

import React, { useState, useEffect, useMemo, Suspense } from 'react'
import apiClient, { LONG_REQUEST_TIMEOUT_MS } from '@/lib/axios'
import { Shield, AlertTriangle, RefreshCw } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import { useTranslation } from '@/components/LanguageProvider'
import HeatmapSection from './_components/HeatmapSection'
import IncidentTable from './_components/IncidentTable'
import ExceptionRecommendation from './_components/ExceptionRecommendation'
import type { Incident } from './_lib/types'
import { mapIncidentData, normalizeTeamName, extractPoliciesFromIncidents } from './_lib/utils'

export default function AnalyticsPage() {
  return (
    <Suspense fallback={<div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', color: 'var(--text-secondary)' }}>Loading...</div>}>
      <AnalyticsPageContent />
    </Suspense>
  )
}

const INITIAL_PAGE_SIZE = 500

function AnalyticsPageContent() {
  const { t } = useTranslation()
  const [incidents, setIncidents] = useState<Incident[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [partialCount, setPartialCount] = useState<number | null>(null)

  useEffect(() => {
    fetchIncidents()
  }, [])

  // Timeout ile diger hatalari ayirir; kullanici "sunucu yavas" ile "istek basarisiz"i ayirt edebilsin.
  const describeError = (err: any) =>
    err?.code === 'ECONNABORTED' ? t('exc.loadTimeout') : t('exc.loadFailed')

  const fetchIncidents = async () => {
    setLoading(true)
    setError(null)
    setPartialCount(null)
    try {
      // compact=true: sayfanin kullandigi 14 alan doner, tam DTO'nun 31 alani degil.
      const queryParams: any = { limit: INITIAL_PAGE_SIZE, order_by: 'timestamp_desc', compact: true }

      // Phase 1: Fast initial load
      const initialResponse = await apiClient.get('/api/incidents', { params: queryParams })
      const initialData = Array.isArray(initialResponse.data) ? initialResponse.data : []
      const initialMapped = initialData.map(mapIncidentData)
      setIncidents(initialMapped)
      setLoading(false)

      // Phase 2: Load remaining data in background
      if (initialMapped.length >= INITIAL_PAGE_SIZE) {
        try {
          const fullResponse = await apiClient.get('/api/incidents', {
            params: { ...queryParams, limit: 1000000000 },
            timeout: LONG_REQUEST_TIMEOUT_MS
          })
          const fullData = Array.isArray(fullResponse.data) ? fullResponse.data : []
          if (fullData.length >= initialMapped.length) {
            setIncidents(fullData.map(mapIncidentData))
          }
        } catch (err) {
          // Tablo ilk sayfayla calismaya devam eder; ama analiz eksik veriye dayandigi
          // icin kullanici bunu bilmeli, sessizce gecilmemeli.
          console.error('Error fetching remaining incidents:', err)
          setPartialCount(initialMapped.length)
        }
      }
    } catch (err) {
      console.error('Error fetching incidents:', err)
      setError(describeError(err))
      setIncidents([])
      setLoading(false)
    }
  }

  // Shared derived data - computed once, passed as props
  const uniqueDepartments = useMemo(() =>
    Array.from(new Set(incidents.map((i: Incident) => i.department).filter((d: string | undefined): d is string => Boolean(d)))).sort(),
    [incidents]
  )

  const uniqueTeams = useMemo(() => {
    const normalizedTeams = new Set<string>()
    incidents.forEach((i: Incident) => {
      if (i.team) normalizedTeams.add(normalizeTeamName(i.team))
    })
    return Array.from(normalizedTeams).sort()
  }, [incidents])

  const uniqueActions = useMemo(() =>
    Array.from(new Set(incidents.map((i: Incident) => i.action || 'Permit'))).sort(),
    [incidents]
  )

  const uniqueChannels = useMemo(() =>
    Array.from(new Set(incidents.map((i: Incident) => i.channel).filter((c: string | undefined): c is string => Boolean(c)))).sort(),
    [incidents]
  )

  const uniquePolicies = useMemo(() => extractPoliciesFromIncidents(incidents), [incidents])

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px', position: 'relative' }}>
      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        {/* Page Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div style={{
              width: '38px',
              height: '38px',
              borderRadius: '10px',
              background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 4px 12px rgba(99, 102, 241, 0.3)'
            }}>
              <Shield size={20} color="#fff" />
            </div>
            <h1 style={{ fontSize: '24px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #6366f1, #8b5cf6)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>{t('heatmap.teamBasedAnalysis')}</h1>
          </div>
        </div>

        {/* Full Page Loading Screen */}
        {loading && (
          <LoadingOverlay isLoading={loading} message={`${t('heatmap.teamBasedAnalysis')} ${t('settings.loadingSettings').replace('...', '')}...`} />
        )}

        {/* Ilk yukleme basarisiz: bos sayfa yerine sebep ve yeniden deneme */}
        {!loading && error && (
          <div style={{
            display: 'flex', alignItems: 'center', gap: '12px', padding: '16px 20px',
            background: 'var(--surface)', border: '1px solid #fca5a5', borderLeft: '4px solid #ef4444',
            borderRadius: '10px', marginBottom: '24px'
          }}>
            <AlertTriangle size={20} color="#ef4444" style={{ flexShrink: 0 }} />
            <span style={{ flex: 1, fontSize: '14px', color: 'var(--text-primary)' }}>{error}</span>
            <button
              onClick={fetchIncidents}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '8px 14px',
                background: 'var(--surface-hover)', color: 'var(--text-primary)',
                border: '1px solid var(--border)', borderRadius: '8px', cursor: 'pointer',
                fontSize: '13px', flexShrink: 0
              }}
            >
              <RefreshCw size={14} /> {t('exc.retry')}
            </button>
          </div>
        )}

        {/* Faz 2 basarisiz: analiz eksik veri uzerinde, kullanici bunu gormeli */}
        {!loading && !error && partialCount !== null && (
          <div style={{
            display: 'flex', alignItems: 'center', gap: '12px', padding: '12px 20px',
            background: 'var(--surface)', border: '1px solid #fcd34d', borderLeft: '4px solid #f59e0b',
            borderRadius: '10px', marginBottom: '24px'
          }}>
            <AlertTriangle size={18} color="#f59e0b" style={{ flexShrink: 0 }} />
            <span style={{ flex: 1, fontSize: '13px', color: 'var(--text-primary)' }}>
              {t('exc.partialData', { count: partialCount })}
            </span>
            <button
              onClick={fetchIncidents}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '6px 12px',
                background: 'var(--surface-hover)', color: 'var(--text-primary)',
                border: '1px solid var(--border)', borderRadius: '8px', cursor: 'pointer',
                fontSize: '12px', flexShrink: 0
              }}
            >
              <RefreshCw size={13} /> {t('exc.retry')}
            </button>
          </div>
        )}

        {/* Main Content - Hidden during initial loading */}
        {!loading && !error && (<>
          <HeatmapSection
            incidents={incidents}
            uniqueDepartments={uniqueDepartments}
            uniqueTeams={uniqueTeams}
            uniqueActions={uniqueActions}
          />

          <IncidentTable incidents={incidents} />

          <ExceptionRecommendation
            incidents={incidents}
            uniqueDepartments={uniqueDepartments}
            uniqueTeams={uniqueTeams}
            uniqueActions={uniqueActions}
            uniqueChannels={uniqueChannels}
            uniquePolicies={uniquePolicies}
          />
        </>)}
      </div>
    </div>
  )
}
