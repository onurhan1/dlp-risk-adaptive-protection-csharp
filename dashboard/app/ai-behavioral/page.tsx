'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'
import { getApiUrlDynamic } from '@/lib/api-config'

interface AIBehavioralAnalysis {
  entityType: string
  entityId: string
  riskScore: number
  anomalyLevel: string
  aiExplanation: string
  aiRecommendation: string
  referenceIncidentIds: number[]
  analysisMetadata: Record<string, any>
  analysisDate: string
}

interface AIBehavioralOverview {
  totalAnalyzed: number
  highAnomalyCount: number
  mediumAnomalyCount: number
  lowAnomalyCount: number
  topAnomalies: AIBehavioralAnalysis[]
  anomalyByChannel: Record<string, number>
  anomalyByDepartment: Record<string, number>
}

export default function AIBehavioralPage() {
  const [overview, setOverview] = useState<AIBehavioralOverview | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedEntity, setSelectedEntity] = useState<AIBehavioralAnalysis | null>(null)
  const [lookbackDays, setLookbackDays] = useState(7)
  const [analyzing, setAnalyzing] = useState(false)

  // Load entity from URL params if provided
  useEffect(() => {
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search)
      const entityType = params.get('entityType')
      const entityId = params.get('entityId')
      if (entityType && entityId) {
        // Decode entityId if it's URL encoded (e.g., %40 becomes @)
        const decodedEntityId = decodeURIComponent(entityId)
        analyzeEntity(entityType, decodedEntityId)
      } else {
        fetchOverview()
      }
    } else {
      fetchOverview()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lookbackDays])

  const fetchOverview = async () => {
    setLoading(true)
    try {
      const response = await apiClient.get('/api/ai-behavioral/overview', {
        params: { lookbackDays }
      })
      setOverview(response.data)
    } catch (error: any) {
      console.error('Error fetching AI behavioral overview:', error)
      if (error.response?.status === 404) {
        console.error('AI Behavioral endpoint not found. Please ensure the API is running and updated.')
      }
    } finally {
      setLoading(false)
    }
  }

  const analyzeEntity = async (entityType: string, entityId: string) => {
    setAnalyzing(true)
    setLoading(true)
    try {
      // Use GET endpoint to fetch analysis (faster, uses cached if available)
      const response = await apiClient.get(`/api/ai-behavioral/entity/${entityType}/${encodeURIComponent(entityId)}`, {
        params: { lookbackDays }
      })
      setSelectedEntity(response.data)
    } catch (error: any) {
      console.error('Error analyzing entity:', error)
      // If GET fails, try POST to trigger new analysis
      try {
        const postResponse = await apiClient.post('/api/ai-behavioral/analyze', {
          entityType,
          entityId,
          lookbackDays
        })
        setSelectedEntity(postResponse.data)
      } catch (postError: any) {
        const errorMessage = postError.response?.data?.detail || 
                            error.response?.data?.detail ||
                            postError.response?.status === 404 ? 'AI Behavioral endpoint not found. Please ensure the API is running and updated.' :
                            error.response?.status === 500 ? `Server error: ${error.response?.data?.detail || 'Failed to analyze entity'}` :
                            postError.message || error.message || 'Failed to analyze entity'
        alert(errorMessage)
      }
    } finally {
      setAnalyzing(false)
      setLoading(false)
    }
  }

  const getAnomalyColor = (level: string): string => {
    switch (level.toLowerCase()) {
      case 'high': return '#dc2626'
      case 'medium': return '#f59e0b'
      case 'low': return '#10b981'
      default: return '#6b7280'
    }
  }

  const getRiskColor = (score: number): string => {
    if (score >= 80) return '#dc2626'
    if (score >= 50) return '#f59e0b'
    return '#10b981'
  }

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', background: 'var(--background)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <div style={{ fontSize: '18px', color: 'var(--text-secondary)' }}>Loading AI Behavioral Analysis...</div>
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
            onClick={fetchOverview}
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
        </div>

        {/* Overview Stats */}
        {overview && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Total Analyzed</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: 'var(--text-primary)' }}>
                {overview.totalAnalyzed}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>High Anomalies</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: '#dc2626' }}>
                {overview.highAnomalyCount}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Medium Anomalies</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: '#f59e0b' }}>
                {overview.mediumAnomalyCount}
              </div>
            </div>
            <div style={{ background: 'var(--surface)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Low Anomalies</div>
              <div style={{ fontSize: '32px', fontWeight: '700', color: '#10b981' }}>
                {overview.lowAnomalyCount}
              </div>
            </div>
          </div>
        )}

        {/* Top Anomalies */}
        {overview && overview.topAnomalies.length > 0 && (
          <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
            <h2 style={{ fontSize: '20px', fontWeight: '600', marginBottom: '20px', color: 'var(--text-primary)' }}>
              Top Anomalies
            </h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {overview.topAnomalies.map((anomaly, idx) => (
                <div
                  key={idx}
                  style={{
                    padding: '16px',
                    background: selectedEntity?.entityId === anomaly.entityId ? 'var(--primary)' : 'var(--surface)',
                    borderRadius: '8px',
                    border: `2px solid ${getAnomalyColor(anomaly.anomalyLevel)}`,
                    transition: 'all 0.2s'
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '8px' }}>
                    <div style={{ flex: 1, cursor: 'pointer' }} onClick={() => setSelectedEntity(anomaly)}>
                      <div style={{ fontSize: '14px', fontWeight: '600', color: selectedEntity?.entityId === anomaly.entityId ? 'white' : 'var(--text-primary)' }}>
                        {anomaly.entityType.toUpperCase()}: {anomaly.entityId}
                      </div>
                      <div style={{ fontSize: '12px', color: selectedEntity?.entityId === anomaly.entityId ? 'rgba(255,255,255,0.8)' : 'var(--text-secondary)', marginTop: '4px' }}>
                        {anomaly.aiExplanation}
                      </div>
                    </div>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '4px' }}>
                        <div style={{
                          padding: '4px 12px',
                          borderRadius: '12px',
                          background: getAnomalyColor(anomaly.anomalyLevel),
                          color: 'white',
                          fontSize: '12px',
                          fontWeight: '600'
                        }}>
                          {anomaly.anomalyLevel.toUpperCase()}
                        </div>
                        <div style={{
                          fontSize: '20px',
                          fontWeight: '700',
                          color: selectedEntity?.entityId === anomaly.entityId ? 'white' : getRiskColor(anomaly.riskScore)
                        }}>
                          {anomaly.riskScore}
                        </div>
                      </div>
                      <button
                        onClick={(e) => {
                          e.stopPropagation()
                          analyzeEntity(anomaly.entityType, anomaly.entityId)
                        }}
                        disabled={analyzing}
                        style={{
                          padding: '6px 12px',
                          background: analyzing ? '#9ca3af' : '#0ea5e9',
                          color: 'white',
                          border: 'none',
                          borderRadius: '6px',
                          cursor: analyzing ? 'not-allowed' : 'pointer',
                          fontSize: '12px',
                          fontWeight: '500',
                          opacity: analyzing ? 0.6 : 1,
                          whiteSpace: 'nowrap'
                        }}
                      >
                        {analyzing ? '...' : 'Analyze'}
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Selected Entity Details */}
        {selectedEntity && (
          <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', border: '1px solid var(--border)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '20px' }}>
              <h2 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)' }}>
                Analysis Details: {selectedEntity.entityType.toUpperCase()} - {selectedEntity.entityId}
              </h2>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  onClick={() => analyzeEntity(selectedEntity.entityType, selectedEntity.entityId)}
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
              <div style={{ padding: '16px', background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Risk Score</div>
                <div style={{ fontSize: '32px', fontWeight: '700', color: getRiskColor(selectedEntity.riskScore) }}>
                  {selectedEntity.riskScore}
                </div>
              </div>
              <div style={{ padding: '16px', background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Anomaly Level</div>
                <div style={{
                  fontSize: '20px',
                  fontWeight: '700',
                  color: getAnomalyColor(selectedEntity.anomalyLevel)
                }}>
                  {selectedEntity.anomalyLevel.toUpperCase()}
                </div>
              </div>
              <div style={{ padding: '16px', background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Reference Incidents</div>
                <div style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', marginBottom: '8px' }}>
                  {selectedEntity.referenceIncidentIds.length}
                </div>
                {selectedEntity.referenceIncidentIds.length > 0 && (
                  <div style={{ marginTop: '8px' }}>
                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '4px' }}>View in Investigation:</div>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                      {selectedEntity.referenceIncidentIds.slice(0, 5).map((incidentId) => (
                        <a
                          key={incidentId}
                          href={`/investigation?user=${encodeURIComponent(selectedEntity.entityId)}&incident=${incidentId}`}
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
                      {selectedEntity.referenceIncidentIds.length > 5 && (
                        <span style={{ fontSize: '11px', color: 'var(--text-secondary)', padding: '4px 8px' }}>
                          +{selectedEntity.referenceIncidentIds.length - 5} more
                        </span>
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)' }}>AI Explanation</h3>
              <div style={{ padding: '16px', background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', color: 'var(--text-primary)' }}>
                {selectedEntity.aiExplanation}
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)' }}>AI Recommendation</h3>
              <div style={{ padding: '16px', background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', color: 'var(--text-primary)' }}>
                {selectedEntity.aiRecommendation}
              </div>
            </div>

            {Object.keys(selectedEntity.analysisMetadata).length > 0 && (
              <div>
                <h3 style={{ fontSize: '16px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)' }}>Analysis Metadata</h3>
                <div style={{ padding: '16px', background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                  <pre style={{ fontSize: '12px', color: 'var(--text-primary)', margin: 0, whiteSpace: 'pre-wrap' }}>
                    {JSON.stringify(selectedEntity.analysisMetadata, null, 2)}
                  </pre>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

