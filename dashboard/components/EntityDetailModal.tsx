'use client'

import { useState, useEffect, useMemo } from 'react'
import apiClient from '@/lib/axios'
import dynamic from 'next/dynamic'
import Pagination from './ui/Pagination'
import { Loader2, AlertTriangle, BarChart3, TrendingUp, TrendingDown, Minus, Target, ClipboardList, Bot, Clock, Calendar } from 'lucide-react'

// Dynamic import for Plotly (client-side only)
const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface DailyData {
    date: string
    count: number
    block_count: number
    quarantine_count: number
    authorized_count: number
    released_count: number
    total_matches: number
}

interface TrendDataPoint {
    label: string
    count: number
    block_count: number
    quarantine_count: number
    authorized_count: number
    released_count: number
    total_matches: number
    start_date?: string
    end_date?: string
    daily_breakdown?: DailyData[]
}

interface DestinationPattern {
    destination: string
    incident_count: number
    total_matches: number
    is_new: boolean
}

interface IncidentSummary {
    id: number
    login_name: string
    destination: string
    channel: string
    action: string
    max_matches: number
    timestamp: string
}

interface ZScoreDetail {
    z_score: number
    mean: number
    std_dev: number
    current_value: number
    baseline_value: number
    formula: string
}

interface EntityDetailData {
    entity_type: string
    entity_id: string
    risk_score: number
    anomaly_level: string
    ai_explanation: string
    ai_recommendation: string
    reference_incident_ids: number[]
    analysis_date: string
    z_scores: Record<string, number>
    z_score_details?: Record<string, ZScoreDetail>
    weekly_trends: TrendDataPoint[]
    monthly_trends: TrendDataPoint[]
    action_counts: Record<string, number>
    action_z_scores: Record<string, number>
    total_incidents: number
    total_matches: number
    avg_matches_per_incident: number
    destination_patterns: DestinationPattern[]
    destination_diversity: number
    top_incidents: IncidentSummary[]
}

interface EntityDetailModalProps {
    isOpen: boolean
    onClose: () => void
    entityType: string
    entityId: string
}

const ACTION_COLORS: Record<string, string> = {
    BLOCK: '#ef4444',
    BLOCKED: '#ef4444',
    QUARANTINE: '#8b5cf6',
    QUARANTINED: '#8b5cf6',
    AUTHORIZED: '#10b981',
    RELEASED: '#f59e0b'
}

export default function EntityDetailModal({
    isOpen,
    onClose,
    entityType,
    entityId
}: EntityDetailModalProps) {
    const [data, setData] = useState<EntityDetailData | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [activeView, setActiveView] = useState<'overview' | 'trends' | 'incidents'>('overview')
    const [selectedWeek, setSelectedWeek] = useState<TrendDataPoint | null>(null)
    const [selectedZScore, setSelectedZScore] = useState<{ key: string; detail: ZScoreDetail } | null>(null)
    const [showAllReferenceIncidents, setShowAllReferenceIncidents] = useState(false)
    const [hoveredIncident, setHoveredIncident] = useState<IncidentSummary | null>(null)

    // Pagination for incidents tab
    const [incidentsPage, setIncidentsPage] = useState(1)
    const incidentsPageSize = 10

    // Reset incidents page when switching to incidents tab
    useEffect(() => {
        if (activeView === 'incidents') {
            setIncidentsPage(1)
        }
    }, [activeView])

    // Client-side pagination for incidents
    const paginatedIncidents = useMemo(() => {
        if (!data?.top_incidents) return []
        const startIndex = (incidentsPage - 1) * incidentsPageSize
        return data.top_incidents.slice(startIndex, startIndex + incidentsPageSize)
    }, [data?.top_incidents, incidentsPage, incidentsPageSize])

    const incidentsTotalPages = data?.top_incidents ? Math.ceil(data.top_incidents.length / incidentsPageSize) : 0

    useEffect(() => {
        if (isOpen && entityType && entityId) {
            fetchDetail()
        }
    }, [isOpen, entityType, entityId])

    // State for 7-day comparison analysis
    const [weeklyData, setWeeklyData] = useState<EntityDetailData | null>(null)

    const fetchDetail = async () => {
        setLoading(true)
        setError(null)
        try {
            // Fetch both 30-day (main) and 7-day (comparison) data in parallel
            const [response30, response7] = await Promise.all([
                apiClient.get(
                    `/api/ai-behavioral/entity/${entityType}/${encodeURIComponent(entityId)}/detail`,
                    { params: { lookbackDays: 30 } }
                ),
                apiClient.get(
                    `/api/ai-behavioral/entity/${entityType}/${encodeURIComponent(entityId)}/detail`,
                    { params: { lookbackDays: 7 } }
                )
            ])
            setData(response30.data)
            setWeeklyData(response7.data)
        } catch (err: any) {
            setError(err.response?.data?.detail || 'Failed to load detail')
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onClose()
        }
        if (isOpen) {
            window.addEventListener('keydown', handleEscape)
            return () => window.removeEventListener('keydown', handleEscape)
        }
    }, [isOpen, onClose])

    if (!isOpen) return null

    const getRiskColor = (score: number) => {
        if (score >= 80) return '#ef4444'
        if (score >= 50) return '#f59e0b'
        return '#10b981'
    }

    const getZScoreStatus = (z: number) => {
        const absZ = Math.abs(z)
        if (absZ >= 3) return { label: 'CRITICAL', color: '#dc2626', dot: '#dc2626' }
        if (absZ >= 2) return { label: 'HIGH', color: '#ef4444', dot: '#ef4444' }
        if (absZ >= 1) return { label: 'MEDIUM', color: '#f59e0b', dot: '#f59e0b' }
        return { label: 'NORMAL', color: '#10b981', dot: '#10b981' }
    }

    // Plotly layout config (dark theme)
    const plotlyLayout = {
        paper_bgcolor: 'transparent',
        plot_bgcolor: 'transparent',
        font: { color: '#9ca3af', size: 11 },
        margin: { l: 40, r: 20, t: 30, b: 40 },
        xaxis: { gridcolor: '#374151', linecolor: '#374151' },
        yaxis: { gridcolor: '#374151', linecolor: '#374151' },
        legend: { orientation: 'h' as const, y: -0.2 },
        showlegend: true
    }

    const plotlyConfig = { displayModeBar: false, responsive: true }

    return (
        <>
            {/* Backdrop */}
            <div
                onClick={onClose}
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: 'rgba(0, 0, 0, 0.7)',
                    zIndex: 9998,
                    backdropFilter: 'blur(4px)'
                }}
            />

            {/* Modal */}
            <div
                onClick={(e) => e.stopPropagation()}
                style={{
                    position: 'fixed',
                    top: '50%',
                    left: '50%',
                    transform: 'translate(-50%, -50%)',
                    backgroundColor: 'var(--surface)',
                    borderRadius: '16px',
                    border: '1px solid var(--border)',
                    boxShadow: '0 25px 70px rgba(0, 0, 0, 0.5)',
                    zIndex: 9999,
                    width: '95%',
                    maxWidth: '1400px',
                    maxHeight: '95vh',
                    display: 'flex',
                    flexDirection: 'column',
                    overflow: 'hidden'
                }}
            >
                {/* Header */}
                <div style={{
                    padding: '24px',
                    borderBottom: '1px solid var(--border)',
                    background: 'linear-gradient(135deg, var(--surface) 0%, var(--background-secondary) 100%)'
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                        <div>
                            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'none', marginBottom: '4px' }}>
                                {entityType} Analysis
                            </div>
                            <h2 style={{ margin: 0, fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)' }}>
                                {entityId}
                            </h2>
                        </div>
                        <button
                            onClick={onClose}
                            style={{
                                background: 'transparent',
                                border: 'none',
                                fontSize: '28px',
                                cursor: 'pointer',
                                color: 'var(--text-secondary)',
                                lineHeight: 1
                            }}
                        >
                            ×
                        </button>
                    </div>

                    {/* Tab Navigation */}
                    <div style={{ display: 'flex', gap: '8px', marginTop: '16px' }}>
                        {(['overview', 'trends', 'incidents'] as const).map(view => (
                            <button
                                key={view}
                                onClick={() => setActiveView(view)}
                                style={{
                                    padding: '8px 20px',
                                    border: 'none',
                                    borderRadius: '20px',
                                    cursor: 'pointer',
                                    fontWeight: '600',
                                    fontSize: '13px',
                                    background: activeView === view ? 'var(--primary)' : 'var(--background)',
                                    color: activeView === view ? 'white' : 'var(--text-secondary)'
                                }}
                            >
                                {view === 'overview' ? <><BarChart3 size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Overview</> :
                                    view === 'trends' ? <><TrendingUp size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Trends</> :
                                        <><ClipboardList size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Incidents</>}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflow: 'auto', padding: '24px' }}>
                    {loading ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
                            <div style={{ marginBottom: '16px' }}><Loader2 size={48} style={{ animation: 'spin 1s linear infinite' }} /></div>
                            <style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
                            Loading detailed analysis...
                        </div>
                    ) : error ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: '#ef4444' }}>
                            <div style={{ marginBottom: '16px' }}><AlertTriangle size={48} /></div>
                            {error}
                        </div>
                    ) : data && activeView === 'overview' ? (
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '24px' }}>
                            {/* Risk Score Card */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                    Risk Assessment
                                </h3>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
                                    <div style={{
                                        width: '100px',
                                        height: '100px',
                                        borderRadius: '50%',
                                        background: `conic-gradient(${getRiskColor(data.risk_score)} ${data.risk_score * 3.6}deg, var(--border) 0deg)`,
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center'
                                    }}>
                                        <div style={{
                                            width: '80px',
                                            height: '80px',
                                            borderRadius: '50%',
                                            background: 'var(--background)',
                                            display: 'flex',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            flexDirection: 'column'
                                        }}>
                                            <div style={{ fontSize: '28px', fontWeight: '700', color: getRiskColor(data.risk_score) }}>
                                                {data.risk_score}
                                            </div>
                                        </div>
                                    </div>
                                    <div>
                                        <div style={{
                                            fontSize: '20px',
                                            fontWeight: '700',
                                            color: getRiskColor(data.risk_score),
                                            textTransform: 'none'
                                        }}>
                                            {data.anomaly_level} RISK
                                        </div>
                                        <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginTop: '4px' }}>
                                            {data.total_incidents} incidents • {data.total_matches.toLocaleString()} matches
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* 7-Day Weekly Analysis Comparison */}
                            {weeklyData && (
                                <div style={{
                                    background: 'linear-gradient(135deg, var(--background) 0%, var(--surface) 100%)',
                                    borderRadius: '12px',
                                    padding: '20px',
                                    border: '2px solid var(--primary)',
                                    position: 'relative'
                                }}>
                                    <div style={{
                                        position: 'absolute',
                                        top: '-10px',
                                        left: '16px',
                                        background: 'var(--primary)',
                                        color: 'white',
                                        padding: '2px 12px',
                                        borderRadius: '12px',
                                        fontSize: '11px',
                                        fontWeight: '700'
                                    }}>
                                        7-DAY ANALYSIS
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginTop: '8px' }}>
                                        {/* Weekly Score */}
                                        <div style={{ textAlign: 'center' }}>
                                            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px' }}>
                                                Weekly Risk Score
                                            </div>
                                            <div style={{
                                                fontSize: '28px',
                                                fontWeight: '800',
                                                color: getRiskColor(weeklyData.risk_score)
                                            }}>
                                                {weeklyData.risk_score}
                                            </div>
                                            <div style={{
                                                fontSize: '12px',
                                                fontWeight: '600',
                                                color: getRiskColor(weeklyData.risk_score),
                                                textTransform: 'none'
                                            }}>
                                                {weeklyData.anomaly_level}
                                            </div>
                                        </div>
                                        {/* Trend Comparison */}
                                        <div style={{ textAlign: 'center' }}>
                                            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px' }}>
                                                vs 30-Day Score
                                            </div>
                                            {(() => {
                                                const diff = weeklyData.risk_score - data.risk_score
                                                const trendIcon = diff > 5 ? 'up' : diff < -5 ? 'down' : 'stable'
                                                const trendColor = diff > 5 ? '#ef4444' : diff < -5 ? '#10b981' : '#6b7280'
                                                const trendText = diff > 5 ? 'Rising' : diff < -5 ? 'Declining' : 'Stable'
                                                return (
                                                    <>
                                                        <div style={{ fontSize: '24px' }}>{trendIcon === 'up' ? <TrendingUp size={24} color={trendColor} /> : trendIcon === 'down' ? <TrendingDown size={24} color={trendColor} /> : <Minus size={24} color={trendColor} />}</div>
                                                        <div style={{ fontSize: '14px', fontWeight: '700', color: trendColor }}>
                                                            {diff > 0 ? '+' : ''}{diff} ({trendText})
                                                        </div>
                                                    </>
                                                )
                                            })()}
                                        </div>
                                    </div>
                                    <div style={{
                                        marginTop: '12px',
                                        padding: '8px',
                                        background: 'var(--background)',
                                        borderRadius: '8px',
                                        fontSize: '12px',
                                        color: 'var(--text-secondary)',
                                        display: 'grid',
                                        gridTemplateColumns: '1fr 1fr',
                                        gap: '8px'
                                    }}>
                                        <div><BarChart3 size={12} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} />Weekly Incidents: <strong>{weeklyData.total_incidents}</strong></div>
                                        <div><BarChart3 size={12} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} />Monthly Incidents: <strong>{data.total_incidents}</strong></div>
                                    </div>
                                </div>
                            )}

                            {/* Action Breakdown - Plotly Pie Chart */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                    Action Breakdown
                                </h3>
                                {Object.keys(data.action_counts).length > 0 ? (
                                    <Plot
                                        data={[{
                                            type: 'pie',
                                            values: Object.values(data.action_counts),
                                            labels: Object.keys(data.action_counts),
                                            marker: {
                                                colors: Object.keys(data.action_counts).map(a => ACTION_COLORS[a] || '#6b7280')
                                            },
                                            hole: 0.4,
                                            textinfo: 'label+percent',
                                            textposition: 'outside'
                                        }]}
                                        layout={{ ...plotlyLayout, height: 200, showlegend: false }}
                                        config={plotlyConfig}
                                        style={{ width: '100%', height: '200px' }}
                                    />
                                ) : (
                                    <div style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '40px' }}>
                                        No action data available
                                    </div>
                                )}
                            </div>

                            {/* Z-Score Table */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)',
                                gridColumn: 'span 2'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                    Z-Score Analysis <span style={{ fontSize: '11px', fontWeight: 'normal' }}>(click for details)</span>
                                </h3>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '12px' }}>
                                    {Object.entries(data.z_scores).map(([key, value]) => {
                                        const status = getZScoreStatus(value)
                                        const detail = data.z_score_details?.[key]
                                        return (
                                            <div
                                                key={key}
                                                onClick={() => {
                                                    if (detail) {
                                                        setSelectedZScore({ key, detail })
                                                    }
                                                }}
                                                style={{
                                                    display: 'flex',
                                                    justifyContent: 'space-between',
                                                    alignItems: 'center',
                                                    padding: '12px',
                                                    background: 'var(--surface)',
                                                    borderRadius: '8px',
                                                    border: '1px solid var(--border)',
                                                    cursor: detail ? 'pointer' : 'default',
                                                    transition: 'all 0.2s'
                                                }}
                                                onMouseEnter={(e) => {
                                                    if (detail) {
                                                        e.currentTarget.style.borderColor = 'var(--primary)'
                                                        e.currentTarget.style.background = 'var(--background-secondary)'
                                                    }
                                                }}
                                                onMouseLeave={(e) => {
                                                    e.currentTarget.style.borderColor = 'var(--border)'
                                                    e.currentTarget.style.background = 'var(--surface)'
                                                }}
                                            >
                                                <span style={{ fontSize: '12px', color: 'var(--text-secondary)', textTransform: 'capitalize' }}>
                                                    {key.replace(/_/g, ' ')}
                                                </span>
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                    <span style={{ fontWeight: '700', color: status.color }}>
                                                        {value.toFixed(2)}
                                                    </span>
                                                    <span style={{ width: '10px', height: '10px', borderRadius: '50%', backgroundColor: status.dot, display: 'inline-block' }}></span>
                                                </div>
                                            </div>
                                        )
                                    })}
                                </div>

                                {/* Z-Score Detail Popup */}
                                {selectedZScore && (
                                    <div style={{
                                        marginTop: '16px',
                                        background: 'var(--surface)',
                                        borderRadius: '8px',
                                        padding: '16px',
                                        border: '2px solid var(--primary)',
                                        position: 'relative'
                                    }}>
                                        <button
                                            onClick={() => setSelectedZScore(null)}
                                            style={{
                                                position: 'absolute',
                                                right: '8px',
                                                top: '8px',
                                                background: 'none',
                                                border: 'none',
                                                color: 'var(--text-muted)',
                                                cursor: 'pointer',
                                                fontSize: '18px'
                                            }}
                                        >×</button>
                                        <h4 style={{ margin: '0 0 12px', fontSize: '14px', color: 'var(--primary)', textTransform: 'capitalize' }}>
                                            <BarChart3 size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> {selectedZScore.key.replace(/_/g, ' ')} Calculation Details
                                        </h4>
                                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px', marginBottom: '12px' }}>
                                            <div style={{ padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Current Value</div>
                                                <div style={{ fontSize: '16px', fontWeight: '700', color: 'var(--text-primary)' }}>
                                                    {selectedZScore.detail.current_value?.toFixed(2) ?? 'N/A'}
                                                </div>
                                            </div>
                                            <div style={{ padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Baseline (Mean)</div>
                                                <div style={{ fontSize: '16px', fontWeight: '700', color: 'var(--text-primary)' }}>
                                                    {selectedZScore.detail.mean?.toFixed(2) ?? 'N/A'}
                                                </div>
                                            </div>
                                            <div style={{ padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Standard Deviation</div>
                                                <div style={{ fontSize: '16px', fontWeight: '700', color: 'var(--text-primary)' }}>
                                                    {selectedZScore.detail.std_dev?.toFixed(2) ?? 'N/A'}
                                                </div>
                                            </div>
                                            <div style={{ padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Z-Score Result</div>
                                                <div style={{ fontSize: '16px', fontWeight: '700', color: getZScoreStatus(selectedZScore.detail.z_score).color }}>
                                                    {selectedZScore.detail.z_score?.toFixed(2) ?? 'N/A'}
                                                </div>
                                            </div>
                                        </div>
                                        <div style={{ padding: '8px', background: 'var(--background)', borderRadius: '6px', fontFamily: 'monospace', fontSize: '12px', color: 'var(--text-secondary)' }}>
                                            <strong>Formula:</strong> {selectedZScore.detail.formula}
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* AI Analysis */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)',
                                gridColumn: 'span 2'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                    <Bot size={16} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> AI Analysis
                                </h3>
                                <div style={{ marginBottom: '16px' }}>
                                    <div style={{ fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Explanation</div>
                                    <p style={{ margin: 0, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                                        {data.ai_explanation}
                                    </p>
                                </div>
                                <div>
                                    <div style={{ fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Recommendation</div>
                                    <p style={{ margin: 0, color: 'var(--primary)', lineHeight: 1.6 }}>
                                        {data.ai_recommendation}
                                    </p>
                                </div>
                            </div>

                            {/* Reference Incidents */}
                            {data.top_incidents && data.top_incidents.length > 0 && (
                                <div style={{
                                    background: 'var(--background)',
                                    borderRadius: '12px',
                                    padding: '24px',
                                    border: '1px solid var(--border)',
                                    gridColumn: 'span 2'
                                }}>
                                    <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                        <ClipboardList size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Reference Incidents ({data.top_incidents.length})
                                    </h3>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', position: 'relative' }}>
                                        {(showAllReferenceIncidents ? data.top_incidents : data.top_incidents.slice(0, 5)).map((inc, idx) => (
                                            <div
                                                key={inc.id}
                                                onMouseEnter={() => setHoveredIncident(inc)}
                                                onMouseLeave={() => setHoveredIncident(null)}
                                                style={{
                                                    padding: '6px 12px',
                                                    background: 'var(--surface)',
                                                    borderRadius: '6px',
                                                    border: '1px solid var(--border)',
                                                    fontSize: '12px',
                                                    cursor: 'pointer',
                                                    transition: 'all 0.2s',
                                                    position: 'relative'
                                                }}
                                                onMouseOver={(e) => {
                                                    e.currentTarget.style.borderColor = 'var(--primary)'
                                                    e.currentTarget.style.background = 'var(--background-secondary)'
                                                }}
                                                onMouseOut={(e) => {
                                                    e.currentTarget.style.borderColor = 'var(--border)'
                                                    e.currentTarget.style.background = 'var(--surface)'
                                                }}
                                            >
                                                <span style={{ fontWeight: '600', color: 'var(--text-primary)' }}>#{inc.id}</span>
                                                <span style={{ color: 'var(--text-muted)', marginLeft: '4px' }}>• {inc.action}</span>
                                            </div>
                                        ))}
                                        {data.top_incidents.length > 5 && !showAllReferenceIncidents && (
                                            <button
                                                onClick={() => setShowAllReferenceIncidents(true)}
                                                style={{
                                                    padding: '6px 12px',
                                                    background: 'var(--primary)',
                                                    color: 'white',
                                                    borderRadius: '6px',
                                                    border: 'none',
                                                    fontSize: '12px',
                                                    cursor: 'pointer',
                                                    fontWeight: '600'
                                                }}
                                            >
                                                +{data.top_incidents.length - 5} more...
                                            </button>
                                        )}
                                        {showAllReferenceIncidents && data.top_incidents.length > 5 && (
                                            <button
                                                onClick={() => setShowAllReferenceIncidents(false)}
                                                style={{
                                                    padding: '6px 12px',
                                                    background: 'var(--text-muted)',
                                                    color: 'white',
                                                    borderRadius: '6px',
                                                    border: 'none',
                                                    fontSize: '12px',
                                                    cursor: 'pointer',
                                                    fontWeight: '600'
                                                }}
                                            >
                                                Show less
                                            </button>
                                        )}
                                    </div>

                                    {/* Floating Hover Tooltip - doesn't affect layout */}
                                    {hoveredIncident && (
                                        <div style={{
                                            position: 'fixed',
                                            top: '50%',
                                            left: '50%',
                                            transform: 'translate(-50%, -50%)',
                                            padding: '16px',
                                            background: 'var(--surface)',
                                            borderRadius: '12px',
                                            border: '2px solid var(--primary)',
                                            fontSize: '12px',
                                            boxShadow: '0 20px 40px rgba(0,0,0,0.3)',
                                            zIndex: 10000,
                                            minWidth: '350px',
                                            maxWidth: '450px'
                                        }}>
                                            <div style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                marginBottom: '12px',
                                                paddingBottom: '8px',
                                                borderBottom: '1px solid var(--border)'
                                            }}>
                                                <span style={{ fontWeight: '700', color: 'var(--primary)', fontSize: '14px' }}>
                                                    <ClipboardList size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Incident #{hoveredIncident.id}
                                                </span>
                                                <span style={{ color: 'var(--text-muted)', fontSize: '11px' }}>
                                                    <Clock size={11} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '2px' }} /> {new Date(hoveredIncident.timestamp).toLocaleString()}
                                                </span>
                                            </div>
                                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px' }}>
                                                <div>
                                                    <div style={{ color: 'var(--text-muted)', marginBottom: '2px', fontSize: '10px', textTransform: 'none' }}>Login</div>
                                                    <div style={{ fontWeight: '600', color: 'var(--text-primary)' }}>{hoveredIncident.login_name}</div>
                                                </div>
                                                <div>
                                                    <div style={{ color: 'var(--text-muted)', marginBottom: '2px', fontSize: '10px', textTransform: 'none' }}>Channel</div>
                                                    <div style={{ fontWeight: '600', color: 'var(--text-primary)' }}>{hoveredIncident.channel}</div>
                                                </div>
                                                <div>
                                                    <div style={{ color: 'var(--text-muted)', marginBottom: '2px', fontSize: '10px', textTransform: 'none' }}>Action</div>
                                                    <div style={{
                                                        fontWeight: '700',
                                                        color: ACTION_COLORS[hoveredIncident.action] || 'var(--text-primary)'
                                                    }}>{hoveredIncident.action}</div>
                                                </div>
                                                <div>
                                                    <div style={{ color: 'var(--text-muted)', marginBottom: '2px', fontSize: '10px', textTransform: 'none' }}>Max Matches</div>
                                                    <div style={{ fontWeight: '700', color: hoveredIncident.max_matches > 100 ? '#ef4444' : 'var(--text-primary)' }}>{hoveredIncident.max_matches.toLocaleString()}</div>
                                                </div>
                                            </div>
                                            <div style={{ marginTop: '12px' }}>
                                                <div style={{ color: 'var(--text-muted)', marginBottom: '2px', fontSize: '10px', textTransform: 'none' }}>Destination</div>
                                                <div style={{ fontWeight: '600', color: 'var(--text-primary)', wordBreak: 'break-all' }}>{hoveredIncident.destination}</div>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            )}
                        </div>
                    ) : data && activeView === 'trends' ? (
                        <div style={{ display: 'grid', gap: '24px' }}>
                            {/* Weekly Chart - Stacked Bar */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                    <BarChart3 size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Weekly Incidents <span style={{ fontWeight: 'normal', fontSize: '12px' }}>(click week for daily details)</span>
                                </h3>
                                {data.weekly_trends.length > 0 ? (
                                    <Plot
                                        data={[
                                            { x: data.weekly_trends.map(t => t.label), y: data.weekly_trends.map(t => t.block_count), name: 'Block', type: 'bar', marker: { color: '#ef4444' } },
                                            { x: data.weekly_trends.map(t => t.label), y: data.weekly_trends.map(t => t.quarantine_count), name: 'Quarantine', type: 'bar', marker: { color: '#8b5cf6' } },
                                            { x: data.weekly_trends.map(t => t.label), y: data.weekly_trends.map(t => t.authorized_count), name: 'Authorized', type: 'bar', marker: { color: '#10b981' } },
                                            { x: data.weekly_trends.map(t => t.label), y: data.weekly_trends.map(t => t.released_count), name: 'Released', type: 'bar', marker: { color: '#f59e0b' } }
                                        ]}
                                        layout={{ ...plotlyLayout, height: 300, barmode: 'stack' }}
                                        config={plotlyConfig}
                                        style={{ width: '100%', height: '300px' }}
                                        onClick={(event: any) => {
                                            if (event.points && event.points.length > 0) {
                                                const weekLabel = event.points[0].x
                                                const selectedWeekData = data.weekly_trends.find(w => w.label === weekLabel)
                                                if (selectedWeekData) {
                                                    setSelectedWeek(selectedWeekData)
                                                }
                                            }
                                        }}
                                    />
                                ) : (
                                    <div style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '60px' }}>
                                        No weekly trend data available
                                    </div>
                                )}

                                {/* Daily Breakdown Popup */}
                                {selectedWeek && selectedWeek.daily_breakdown && (
                                    <div style={{
                                        marginTop: '16px',
                                        background: 'var(--surface)',
                                        borderRadius: '8px',
                                        padding: '16px',
                                        border: '1px solid var(--primary)',
                                        position: 'relative'
                                    }}>
                                        <button
                                            onClick={() => setSelectedWeek(null)}
                                            style={{
                                                position: 'absolute',
                                                right: '8px',
                                                top: '8px',
                                                background: 'none',
                                                border: 'none',
                                                color: 'var(--text-muted)',
                                                cursor: 'pointer',
                                                fontSize: '18px'
                                            }}
                                        >×</button>
                                        <h4 style={{ margin: '0 0 12px', fontSize: '14px', color: 'var(--primary)', display: 'flex', alignItems: 'center', gap: '6px' }}>
                                            <Calendar size={14} /> Daily Breakdown: {selectedWeek.label} ({selectedWeek.start_date} to {selectedWeek.end_date})
                                        </h4>
                                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                                            <thead>
                                                <tr style={{ borderBottom: '1px solid var(--border)' }}>
                                                    <th style={{ padding: '8px', textAlign: 'left' }}>Date</th>
                                                    <th style={{ padding: '8px', textAlign: 'center' }}>Total</th>
                                                    <th style={{ padding: '8px', textAlign: 'center', color: '#ef4444' }}>Block</th>
                                                    <th style={{ padding: '8px', textAlign: 'center', color: '#8b5cf6' }}>Quarantine</th>
                                                    <th style={{ padding: '8px', textAlign: 'center', color: '#10b981' }}>Authorized</th>
                                                    <th style={{ padding: '8px', textAlign: 'center', color: '#f59e0b' }}>Released</th>
                                                    <th style={{ padding: '8px', textAlign: 'right' }}>Matches</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {selectedWeek.daily_breakdown.map((day, idx) => (
                                                    <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                        <td style={{ padding: '8px' }}>{day.date}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', fontWeight: '600' }}>{day.count}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#ef4444' }}>{day.block_count}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#8b5cf6' }}>{day.quarantine_count}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#10b981' }}>{day.authorized_count}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#f59e0b' }}>{day.released_count}</td>
                                                        <td style={{ padding: '8px', textAlign: 'right' }}>{day.total_matches}</td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                )}
                            </div>

                            {/* Monthly Chart - Line */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                    <TrendingUp size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Monthly Trend
                                </h3>
                                {data.monthly_trends.length > 0 ? (
                                    <Plot
                                        data={[
                                            { x: data.monthly_trends.map(t => t.label), y: data.monthly_trends.map(t => t.count), name: 'Total Incidents', type: 'scatter', mode: 'lines+markers', line: { color: '#3b82f6', width: 3 }, marker: { size: 8 } },
                                            { x: data.monthly_trends.map(t => t.label), y: data.monthly_trends.map(t => t.total_matches), name: 'Total Matches', type: 'scatter', mode: 'lines+markers', yaxis: 'y2', line: { color: '#f59e0b', width: 2 }, marker: { size: 6 } }
                                        ]}
                                        layout={{ ...plotlyLayout, height: 300, yaxis2: { overlaying: 'y', side: 'right', gridcolor: '#374151' } }}
                                        config={plotlyConfig}
                                        style={{ width: '100%', height: '300px' }}
                                    />
                                ) : (
                                    <div style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '60px' }}>
                                        No monthly trend data available
                                    </div>
                                )}
                            </div>

                            {/* Destination Patterns */}
                            {data.destination_patterns.length > 0 && (
                                <div style={{
                                    background: 'var(--background)',
                                    borderRadius: '12px',
                                    padding: '24px',
                                    border: '1px solid var(--border)'
                                }}>
                                    <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                        <Target size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Top Destinations
                                    </h3>
                                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))', gap: '12px' }}>
                                        {data.destination_patterns.slice(0, 10).map((dp, idx) => (
                                            <div key={idx} style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                padding: '12px',
                                                background: 'var(--surface)',
                                                borderRadius: '8px',
                                                border: dp.is_new ? '1px solid #f59e0b' : '1px solid var(--border)'
                                            }}>
                                                <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '180px' }}>
                                                    <span style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{dp.destination}</span>
                                                    {dp.is_new && <span style={{ marginLeft: '8px', fontSize: '10px', color: '#f59e0b' }}>NEW</span>}
                                                </div>
                                                <div style={{ textAlign: 'right', fontSize: '12px' }}>
                                                    <div style={{ color: 'var(--text-primary)', fontWeight: '600' }}>{dp.incident_count}</div>
                                                    <div style={{ color: 'var(--text-muted)' }}>{dp.total_matches} matches</div>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            )}
                        </div>
                    ) : data && activeView === 'incidents' ? (
                        <div style={{
                            background: 'var(--background)',
                            borderRadius: '12px',
                            padding: '24px',
                            border: '1px solid var(--border)'
                        }}>
                            <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'none' }}>
                                <ClipboardList size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} /> Top Incidents (by matches)
                            </h3>
                            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                <thead>
                                    <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>#</th>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>User</th>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>Destination</th>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>Channel</th>
                                        <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>Action</th>
                                        <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>Matches</th>
                                        <th style={{ padding: '12px', textAlign: 'right', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'none' }}>Date</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {paginatedIncidents.map((inc, idx) => (
                                        <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                                                {(incidentsPage - 1) * incidentsPageSize + idx + 1}
                                            </td>
                                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>{inc.login_name}</td>
                                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)', maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{inc.destination}</td>
                                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>{inc.channel}</td>
                                            <td style={{ padding: '12px', textAlign: 'center' }}>
                                                <span style={{
                                                    padding: '4px 10px',
                                                    borderRadius: '12px',
                                                    fontSize: '11px',
                                                    fontWeight: '600',
                                                    color: 'white',
                                                    background: ACTION_COLORS[inc.action?.toUpperCase()] || '#6b7280'
                                                }}>
                                                    {inc.action}
                                                </span>
                                            </td>
                                            <td style={{ padding: '12px', textAlign: 'center', fontWeight: '600', color: inc.max_matches >= 10 ? '#ef4444' : 'var(--text-primary)' }}>
                                                {inc.max_matches}
                                            </td>
                                            <td style={{ padding: '12px', textAlign: 'right', fontSize: '12px', color: 'var(--text-muted)' }}>
                                                {new Date(inc.timestamp).toLocaleDateString()}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>

                            {/* Pagination */}
                            {data.top_incidents.length > incidentsPageSize && (
                                <div style={{ marginTop: '16px' }}>
                                    <Pagination
                                        currentPage={incidentsPage}
                                        totalPages={incidentsTotalPages}
                                        totalItems={data.top_incidents.length}
                                        pageSize={incidentsPageSize}
                                        onPageChange={setIncidentsPage}
                                        showPageInput={true}
                                        showFirstLast={true}
                                        showTotalItems={true}
                                    />
                                </div>
                            )}
                        </div>
                    ) : null}
                </div>
            </div>
        </>
    )
}
