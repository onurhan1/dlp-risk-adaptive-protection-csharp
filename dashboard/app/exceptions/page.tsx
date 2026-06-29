'use client'

import React, { useState, useEffect, useMemo, Suspense } from 'react'
import apiClient from '@/lib/axios'
import { Shield } from 'lucide-react'
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

function AnalyticsPageContent() {
  const { t } = useTranslation()
  const [incidents, setIncidents] = useState<Incident[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetchIncidents()
  }, [])

  const fetchIncidents = async () => {
    setLoading(true)
    try {
      const queryParams: any = { limit: 500, order_by: 'timestamp_desc' }

      // Phase 1: Fast initial load
      const initialResponse = await apiClient.get('/api/incidents', { params: queryParams })
      const initialData = Array.isArray(initialResponse.data) ? initialResponse.data : []
      const initialMapped = initialData.map(mapIncidentData)
      setIncidents(initialMapped)
      setLoading(false)

      // Phase 2: Load remaining data in background
      if (initialMapped.length >= 500) {
        try {
          const fullResponse = await apiClient.get('/api/incidents', {
            params: { ...queryParams, limit: 1000000000 }
          })
          const fullData = Array.isArray(fullResponse.data) ? fullResponse.data : []
          if (fullData.length >= initialMapped.length) {
            setIncidents(fullData.map(mapIncidentData))
          }
        } catch (error) {
          console.error('Error fetching remaining incidents:', error)
        }
      }
    } catch (error) {
      console.error('Error fetching incidents:', error)
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

        {/* Main Content - Hidden during initial loading */}
        {!loading && (<>
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
