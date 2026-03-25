'use client'

import { useState, useEffect, useMemo } from 'react'
import { useRouter } from 'next/navigation'
import apiClient from '@/lib/axios'
import EntityDetailModal from '@/components/EntityDetailModal'
import { BarChart3, Bot } from 'lucide-react'
import LoadingOverlay from '@/components/ui/LoadingOverlay'

interface AIBehavioralAnalysis {
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

interface AIBehavioralOverview {
  total_analyzed: number
  high_anomaly_count: number
  medium_anomaly_count: number
  low_anomaly_count: number
  user_anomalies: AIBehavioralAnalysis[]
  channel_anomalies: AIBehavioralAnalysis[]
  department_anomalies: AIBehavioralAnalysis[]
  destination_anomalies: AIBehavioralAnalysis[]
  rule_anomalies: AIBehavioralAnalysis[]
  unique_users: string[]
  unique_channels: string[]
  unique_departments: string[]
  unique_destinations: string[]
  unique_rules: string[]
  top_anomalies: AIBehavioralAnalysis[]
  anomaly_by_channel: Record<string, number>
  anomaly_by_department: Record<string, number>
}

type EntityTab = 'users' | 'channels' | 'departments' | 'destinations' | 'rules'

export default function AIBehavioralPage() {
  const router = useRouter()
  const [overview, setOverview] = useState<AIBehavioralOverview | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedEntity, setSelectedEntity] = useState<AIBehavioralAnalysis | null>(null)
  const [lookbackDays, setLookbackDays] = useState(30)
  const [analyzing, setAnalyzing] = useState(false)
  const [activeTab, setActiveTab] = useState<EntityTab>('users')
  const [filterText, setFilterText] = useState('')
  const [showDropdown, setShowDropdown] = useState(false)
  const [currentPage, setCurrentPage] = useState(1)
  const itemsPerPage = 100

  // Azure AI data state
  const [azureAIUsers, setAzureAIUsers] = useState<Map<string, number>>(new Map())
  const [showOnlyAzureAI, setShowOnlyAzureAI] = useState(false)

  // Detail modal state
  const [detailModalOpen, setDetailModalOpen] = useState(false)
  const [detailEntity, setDetailEntity] = useState<{ type: string; id: string } | null>(null)

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search)
      const entityType = params.get('entityType')
      const entityId = params.get('entityId')
      if (entityType && entityId) {
        const decodedEntityId = decodeURIComponent(entityId)
        analyzeEntity(entityType, decodedEntityId)
      } else {
        fetchOverview()
      }
    } else {
      fetchOverview()
    }
  }, [lookbackDays])

  const fetchOverview = async (forceRefresh: boolean = false) => {
    setLoading(true)
    try {
      const overviewRes = await apiClient.get('/api/ai-behavioral/overview', {
        params: { lookbackDays, forceRefresh }
      })
      setOverview(overviewRes.data)

      // Fetch Azure AI data separately to not block main data if it fails
      try {
        const azureAIRes = await apiClient.get('/api/azure-ai/users-with-analysis')
        // Build a map of user email -> Azure AI average score
        const azureMap = new Map<string, number>()
        if (azureAIRes.data?.users) {
          azureAIRes.data.users.forEach((u: any) => {
            azureMap.set(u.user_email || u.userEmail, u.average_risk_score || u.averageRiskScore)
          })
        }
        setAzureAIUsers(azureMap)
      } catch (azureError) {
        console.warn('Azure AI data fetch failed, continuing without it:', azureError)
      }
    } catch (error: any) {
      console.error('Error fetching AI behavioral overview:', error)
    } finally {
      setLoading(false)
    }
  }

  const analyzeEntity = async (entityType: string, entityId: string) => {
    setAnalyzing(true)
    setLoading(true)
    try {
      const response = await apiClient.get(`/api/ai-behavioral/entity/${entityType}/${encodeURIComponent(entityId)}`, {
        params: { lookbackDays }
      })
      setSelectedEntity(response.data)
    } catch (error: any) {
      console.error('Error analyzing entity:', error)
      try {
        const postResponse = await apiClient.post('/api/ai-behavioral/analyze', {
          entityType,
          entityId,
          lookbackDays
        })
        setSelectedEntity(postResponse.data)
      } catch (postError: any) {
        alert(postError.response?.data?.detail || 'Failed to analyze entity')
      }
    } finally {
      setAnalyzing(false)
      setLoading(false)
    }
  }

  const getAnomalyColor = (level: string): string => {
    switch (level.toLowerCase()) {
      case 'critical': return '#7c2d12' // dark red for critical
      case 'high': return '#dc2626'
      case 'medium': return '#f59e0b'
      case 'low': return '#10b981'
      default: return '#6b7280'
    }
  }

  const getRiskColor = (score: number): string => {
    if (score >= 85) return '#7c2d12' // critical
    if (score >= 65) return '#dc2626' // high
    if (score >= 40) return '#f59e0b' // medium
    return '#10b981' // low
  }

  // Get current tab's anomalies and unique values
  const currentTabData = useMemo(() => {
    if (!overview) return { anomalies: [], uniqueValues: [] }

    switch (activeTab) {
      case 'users':
        return { anomalies: overview.user_anomalies || [], uniqueValues: overview.unique_users || [] }
      case 'channels':
        return { anomalies: overview.channel_anomalies || [], uniqueValues: overview.unique_channels || [] }
      case 'departments':
        return { anomalies: overview.department_anomalies || [], uniqueValues: overview.unique_departments || [] }
      case 'destinations':
        return { anomalies: overview.destination_anomalies || [], uniqueValues: overview.unique_destinations || [] }
      case 'rules':
        return { anomalies: overview.rule_anomalies || [], uniqueValues: overview.unique_rules || [] }
      default:
        return { anomalies: [], uniqueValues: [] }
    }
  }, [overview, activeTab])

  // Filter anomalies based on filter text and Azure AI filter
  const filteredAnomalies = useMemo(() => {
    let anomalies = currentTabData.anomalies

    // Filter by text
    if (filterText.trim()) {
      anomalies = anomalies.filter(a =>
        a.entity_id.toLowerCase().includes(filterText.toLowerCase())
      )
    }

    // Filter by Azure AI analysis (only for users tab)
    if (showOnlyAzureAI && activeTab === 'users') {
      anomalies = anomalies.filter(a => azureAIUsers.has(a.entity_id))
    }

    return anomalies
  }, [currentTabData.anomalies, filterText, showOnlyAzureAI, activeTab, azureAIUsers])

  // Pagination
  const totalPages = Math.ceil(filteredAnomalies.length / itemsPerPage)
  const paginatedAnomalies = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage
    return filteredAnomalies.slice(startIndex, startIndex + itemsPerPage)
  }, [filteredAnomalies, currentPage, itemsPerPage])

  // Reset page when filter or tab changes
  useEffect(() => {
    setCurrentPage(1)
  }, [filterText, activeTab])

  // Filter dropdown suggestions - show analyzed entities from anomalies
  const filteredSuggestions = useMemo(() => {
    // Get entity IDs from anomalies (these are the analyzed ones)
    const analyzedEntityIds = currentTabData.anomalies.map(a => a.entity_id)

    if (!filterText.trim()) {
      // When not typing, show all analyzed entities
      return analyzedEntityIds
    }

    // When typing, filter and show matches
    return analyzedEntityIds
      .filter(v => v.toLowerCase().includes(filterText.toLowerCase()))
  }, [currentTabData.anomalies, filterText])

  const tabConfig = [
    { key: 'users' as const, label: 'Users', count: overview?.user_anomalies?.length || 0 },
    { key: 'channels' as const, label: 'Channels', count: overview?.channel_anomalies?.length || 0 },
    { key: 'departments' as const, label: 'Departments', count: overview?.department_anomalies?.length || 0 },
    { key: 'destinations' as const, label: 'Destinations', count: overview?.destination_anomalies?.length || 0 },
    { key: 'rules' as const, label: 'Rules', count: overview?.rule_anomalies?.length || 0 },
  ]

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', background: 'var(--background)', position: 'relative' }}>
        <LoadingOverlay isLoading={loading} message="AI Behavioral Analysis yükleniyor" />
      </div>
    )
  }

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '1400px', margin: '0 auto' }}>
        {/* Header */}
        <div style={{ marginBottom: '32px' }}>
          <h1 style={{ fontSize: '32px', fontWeight: '700', color: 'var(--text-primary)', marginBottom: '8px' }}>
            AI Behavioral Analysis
          </h1>
          <p style={{ fontSize: '16px', color: 'var(--text-secondary)' }}>
            Advanced anomaly detection using statistical analysis and behavioral patterns
          </p>
        </div>

        {/* Controls */}
        <div style={{
          background: 'var(--surface)',
          padding: '20px',
          borderRadius: '12px',
          marginBottom: '24px',
          display: 'flex',
          gap: '16px',
          alignItems: 'center',
          flexWrap: 'wrap'
        }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: '500' }}>
            Lookback Period:
            <select
              value={lookbackDays}
              onChange={(e) => setLookbackDays(Number(e.target.value))}
              style={{
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                background: 'var(--surface)',
                color: 'var(--text-primary)'
              }}
            >
              <option value={7}>7 days</option>
              <option value={14}>14 days</option>
              <option value={30}>30 days</option>
            </select>
          </label>
          <button
            onClick={() => fetchOverview(true)}
            disabled={loading}
            style={{
              padding: '8px 16px',
              background: 'var(--primary)',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: loading ? 'not-allowed' : 'pointer',
              opacity: loading ? 0.6 : 1
            }}
          >
            Refresh
          </button>

          {/* Azure AI Filter Toggle */}
          {activeTab === 'users' && (
            <button
              onClick={() => setShowOnlyAzureAI(!showOnlyAzureAI)}
              style={{
                padding: '8px 16px',
                background: showOnlyAzureAI ? '#10b981' : 'var(--background)',
                color: showOnlyAzureAI ? 'white' : 'var(--text-secondary)',
                border: showOnlyAzureAI ? 'none' : '1px solid var(--border)',
                borderRadius: '6px',
                cursor: 'pointer',
                fontWeight: '600',
                fontSize: '13px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <Bot size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> {showOnlyAzureAI ? 'Showing AI Analyzed' : 'Show AI Analyzed Only'}
              <span style={{
                background: showOnlyAzureAI ? 'rgba(255,255,255,0.2)' : 'var(--primary)',
                color: 'white',
                padding: '2px 8px',
                borderRadius: '10px',
                fontSize: '11px'
              }}>
                {azureAIUsers.size}
              </span>
            </button>
          )}
        </div>

        {/* Overview Stats */}
        {overview && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Total Analyzed</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: 'var(--text-primary)' }}>
                {overview.total_analyzed}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>High Anomalies</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: '#dc2626' }}>
                {overview.high_anomaly_count}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Medium Anomalies</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: '#f59e0b' }}>
                {overview.medium_anomaly_count}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Low Anomalies</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: '#10b981' }}>
                {overview.low_anomaly_count}
              </div>
            </div>
          </div>
        )}

        {/* Entity Tabs */}
        {overview && (
          <div style={{ background: 'var(--surface)', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
            {/* Tab Headers */}
            <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', overflow: 'auto' }}>
              {tabConfig.map(tab => (
                <button
                  key={tab.key}
                  onClick={() => { setActiveTab(tab.key); setFilterText(''); }}
                  style={{
                    padding: '16px 24px',
                    background: activeTab === tab.key ? 'var(--primary)' : 'transparent',
                    color: activeTab === tab.key ? 'white' : 'var(--text-secondary)',
                    border: 'none',
                    cursor: 'pointer',
                    fontWeight: '600',
                    fontSize: '14px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    whiteSpace: 'nowrap'
                  }}
                >
                  {tab.label}
                  <span style={{
                    background: activeTab === tab.key ? 'rgba(255,255,255,0.2)' : 'var(--border)',
                    padding: '2px 8px',
                    borderRadius: '12px',
                    fontSize: '12px'
                  }}>
                    {tab.count}
                  </span>
                </button>
              ))}
            </div>

            {/* Filter Input with Autocomplete */}
            <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', position: 'relative' }}>
              <input
                type="text"
                placeholder={`Filter ${activeTab}...`}
                value={filterText}
                onChange={(e) => setFilterText(e.target.value)}
                onFocus={() => setShowDropdown(true)}
                onBlur={() => setTimeout(() => setShowDropdown(false), 200)}
                style={{
                  width: '100%',
                  maxWidth: '400px',
                  padding: '10px 14px',
                  border: '1px solid var(--border)',
                  borderRadius: '8px',
                  background: 'var(--background)',
                  color: 'var(--text-primary)',
                  fontSize: '14px'
                }}
              />
              {/* Dropdown Suggestions */}
              {showDropdown && filteredSuggestions.length > 0 && (
                <div style={{
                  position: 'absolute',
                  top: '100%',
                  left: '16px',
                  width: '400px',
                  maxHeight: '300px',
                  overflowY: 'auto',
                  background: 'var(--surface)',
                  border: '1px solid var(--border)',
                  borderRadius: '8px',
                  boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                  zIndex: 100
                }}>
                  {filteredSuggestions.map((suggestion, idx) => (
                    <div
                      key={idx}
                      onClick={() => { setFilterText(suggestion); setShowDropdown(false); }}
                      style={{
                        padding: '10px 14px',
                        cursor: 'pointer',
                        borderBottom: idx < filteredSuggestions.length - 1 ? '1px solid var(--border)' : 'none',
                        color: 'var(--text-primary)'
                      }}
                      onMouseEnter={(e) => e.currentTarget.style.background = 'var(--primary)'}
                      onMouseLeave={(e) => e.currentTarget.style.background = 'transparent'}
                    >
                      {suggestion}
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Anomalies List */}
            <div style={{ padding: '16px' }}>
              {/* Pagination Info */}
              {filteredAnomalies.length > 0 && (
                <div style={{ marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>
                    Showing {((currentPage - 1) * itemsPerPage) + 1} - {Math.min(currentPage * itemsPerPage, filteredAnomalies.length)} of {filteredAnomalies.length}
                  </span>
                  {totalPages > 1 && (
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                      <button
                        onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                        disabled={currentPage === 1}
                        style={{
                          padding: '6px 12px',
                          background: currentPage === 1 ? 'var(--border)' : 'var(--primary)',
                          color: currentPage === 1 ? 'var(--text-muted)' : 'white',
                          border: 'none',
                          borderRadius: '6px',
                          cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
                          fontSize: '13px'
                        }}
                      >
                        ← Prev
                      </button>
                      <span style={{ color: 'var(--text-primary)', fontSize: '14px' }}>
                        Page {currentPage} / {totalPages}
                      </span>
                      <button
                        onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                        disabled={currentPage === totalPages}
                        style={{
                          padding: '6px 12px',
                          background: currentPage === totalPages ? 'var(--border)' : 'var(--primary)',
                          color: currentPage === totalPages ? 'var(--text-muted)' : 'white',
                          border: 'none',
                          borderRadius: '6px',
                          cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
                          fontSize: '13px'
                        }}
                      >
                        Next →
                      </button>
                    </div>
                  )}
                </div>
              )}
              {paginatedAnomalies.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
                  No anomalies found for this entity type
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  {paginatedAnomalies.map((anomaly, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: '16px',
                        background: selectedEntity?.entity_id === anomaly.entity_id ? 'var(--primary)' : 'var(--background)',
                        borderRadius: '8px',
                        border: `2px solid ${getAnomalyColor(anomaly.anomaly_level)}`,
                        transition: 'all 0.2s'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '8px' }}>
                        <div
                          style={{ flex: 1, cursor: 'pointer' }}
                          onClick={() => {
                            router.push(`/ai-behavioral?entityType=${encodeURIComponent(anomaly.entity_type)}&entityId=${encodeURIComponent(anomaly.entity_id)}`)
                          }}
                        >
                          <div style={{ fontSize: '14px', fontWeight: '600', color: selectedEntity?.entity_id === anomaly.entity_id ? 'white' : 'var(--text-primary)' }}>
                            {anomaly.entity_id}
                          </div>
                          <div style={{ fontSize: '12px', color: selectedEntity?.entity_id === anomaly.entity_id ? 'rgba(255,255,255,0.8)' : 'var(--text-secondary)', marginTop: '4px' }}>
                            {anomaly.ai_explanation}
                          </div>
                        </div>
                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '4px' }}>
                            <div style={{
                              padding: '4px 12px',
                              borderRadius: '12px',
                              background: getAnomalyColor(anomaly.anomaly_level),
                              color: 'white',
                              fontSize: '12px',
                              fontWeight: '600'
                            }}>
                              {anomaly.anomaly_level.toUpperCase()}
                            </div>
                            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                              <div style={{
                                fontSize: '20px',
                                fontWeight: '700',
                                color: selectedEntity?.entity_id === anomaly.entity_id ? 'white' : getRiskColor(anomaly.risk_score)
                              }}>
                                {anomaly.risk_score}
                              </div>
                              {activeTab === 'users' && azureAIUsers.has(anomaly.entity_id) && (
                                <div style={{
                                  display: 'flex',
                                  flexDirection: 'column',
                                  alignItems: 'center',
                                  padding: '4px 8px',
                                  background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
                                  borderRadius: '8px'
                                }}>
                                  <span style={{ fontSize: '9px', color: 'rgba(255,255,255,0.8)' }}>Azure AI</span>
                                  <span style={{ fontSize: '14px', fontWeight: '700', color: 'white' }}>
                                    {azureAIUsers.get(anomaly.entity_id)}
                                  </span>
                                </div>
                              )}
                            </div>
                          </div>
                          <button
                            onClick={(e) => {
                              e.stopPropagation()
                              setDetailEntity({ type: anomaly.entity_type, id: anomaly.entity_id })
                              setDetailModalOpen(true)
                            }}
                            style={{
                              padding: '6px 12px',
                              background: '#0ea5e9',
                              color: 'white',
                              border: 'none',
                              borderRadius: '6px',
                              cursor: 'pointer',
                              fontSize: '12px',
                              fontWeight: '500',
                              whiteSpace: 'nowrap'
                            }}
                          >
                            <BarChart3 size={12} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Analyze
                          </button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {/* Selected Entity Details */}
        {selectedEntity && (
          <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', border: '1px solid var(--border)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '20px' }}>
              <h2 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)' }}>
                Analysis Details: {selectedEntity.entity_type.toUpperCase()} - {selectedEntity.entity_id}
              </h2>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  onClick={() => analyzeEntity(selectedEntity.entity_type, selectedEntity.entity_id)}
                  disabled={analyzing}
                  style={{
                    padding: '8px 16px',
                    background: analyzing ? '#9ca3af' : 'var(--primary)',
                    color: 'white',
                    border: 'none',
                    borderRadius: '6px',
                    cursor: analyzing ? 'not-allowed' : 'pointer',
                    fontWeight: '500',
                    opacity: analyzing ? 0.6 : 1
                  }}
                >
                  {analyzing ? 'Analyzing...' : 'Re-analyze'}
                </button>
                <button
                  onClick={() => setSelectedEntity(null)}
                  style={{
                    padding: '8px 16px',
                    background: 'transparent',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    cursor: 'pointer'
                  }}
                >
                  Close
                </button>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: '16px', marginBottom: '24px' }}>
              <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Risk Score</div>
                <div style={{ fontSize: '32px', fontWeight: '700', color: getRiskColor(selectedEntity.risk_score) }}>
                  {selectedEntity.risk_score}
                </div>
              </div>
              <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Anomaly Level</div>
                <div style={{
                  fontSize: '20px',
                  fontWeight: '700',
                  color: getAnomalyColor(selectedEntity.anomaly_level)
                }}>
                  {selectedEntity.anomaly_level.toUpperCase()}
                </div>
              </div>
              <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Reference Incidents</div>
                <div style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', marginBottom: '8px' }}>
                  {selectedEntity.reference_incident_ids.length}
                </div>
                {selectedEntity.reference_incident_ids.length > 0 && (
                  <div style={{ marginTop: '8px' }}>
                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>View in Investigation:</div>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                      {selectedEntity.reference_incident_ids.slice(0, 5).map((incidentId) => (
                        <a
                          key={incidentId}
                          href={`/investigation?user=${encodeURIComponent(selectedEntity.entity_id)}&incident=${incidentId}`}
                          style={{
                            padding: '4px 8px',
                            background: 'var(--primary)',
                            color: 'white',
                            borderRadius: '4px',
                            fontSize: '11px',
                            textDecoration: 'none',
                            display: 'inline-block'
                          }}
                        >
                          #{incidentId}
                        </a>
                      ))}
                      {selectedEntity.reference_incident_ids.length > 5 && (
                        <span style={{ fontSize: '11px', color: 'var(--text-secondary)', padding: '4px 8px' }}>
                          +{selectedEntity.reference_incident_ids.length - 5} more
                        </span>
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)' }}>AI Explanation</h3>
              <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)', color: 'var(--text-primary)' }}>
                {selectedEntity.ai_explanation}
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)' }}>AI Recommendation</h3>
              <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)', color: 'var(--text-primary)' }}>
                {selectedEntity.ai_recommendation}
              </div>
            </div>

            {Object.keys(selectedEntity.analysis_metadata || {}).length > 0 && (
              <div>
                <h3 style={{ fontSize: '16px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)' }}>Analysis Metadata</h3>
                <div style={{ padding: '16px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                  <pre style={{ fontSize: '12px', color: 'var(--text-primary)', margin: 0, whiteSpace: 'pre-wrap' }}>
                    {JSON.stringify(selectedEntity.analysis_metadata, null, 2)}
                  </pre>
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Entity Detail Modal */}
      {detailEntity && (
        <EntityDetailModal
          isOpen={detailModalOpen}
          onClose={() => setDetailModalOpen(false)}
          entityType={detailEntity.type}
          entityId={detailEntity.id}
        />
      )}
    </div>
  )
}
