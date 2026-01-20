'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'
import dynamic from 'next/dynamic'

// Dynamic import for Plotly (client-side only)
const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface DailyData {
    date: string
    count: number
    blockCount: number
    quarantineCount: number
    authorizedCount: number
    releasedCount: number
    totalMatches: number
}

interface TrendDataPoint {
    label: string
    count: number
    blockCount: number
    quarantineCount: number
    authorizedCount: number
    releasedCount: number
    totalMatches: number
    startDate?: string
    endDate?: string
    dailyBreakdown?: DailyData[]
}

interface DestinationPattern {
    destination: string
    incidentCount: number
    totalMatches: number
    isNew: boolean
}

interface IncidentSummary {
    id: number
    loginName: string
    destination: string
    channel: string
    action: string
    maxMatches: number
    timestamp: string
}

interface EntityDetailData {
    entityType: string
    entityId: string
    riskScore: number
    anomalyLevel: string
    aiExplanation: string
    aiRecommendation: string
    referenceIncidentIds: number[]
    analysisDate: string
    zScores: Record<string, number>
    weeklyTrends: TrendDataPoint[]
    monthlyTrends: TrendDataPoint[]
    actionCounts: Record<string, number>
    actionZScores: Record<string, number>
    totalIncidents: number
    totalMatches: number
    avgMatchesPerIncident: number
    destinationPatterns: DestinationPattern[]
    destinationDiversity: number
    topIncidents: IncidentSummary[]
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

    useEffect(() => {
        if (isOpen && entityType && entityId) {
            fetchDetail()
        }
    }, [isOpen, entityType, entityId])

    const fetchDetail = async () => {
        setLoading(true)
        setError(null)
        try {
            const response = await apiClient.get(
                `/api/ai-behavioral/entity/${entityType}/${encodeURIComponent(entityId)}/detail`,
                { params: { lookbackDays: 30 } }
            )
            setData(response.data)
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
        if (absZ >= 3) return { label: 'CRITICAL', color: '#dc2626', icon: '🔴' }
        if (absZ >= 2) return { label: 'HIGH', color: '#ef4444', icon: '🟠' }
        if (absZ >= 1) return { label: 'MEDIUM', color: '#f59e0b', icon: '🟡' }
        return { label: 'NORMAL', color: '#10b981', icon: '🟢' }
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
                            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>
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
                                {view === 'overview' ? '📊 Overview' : view === 'trends' ? '📈 Trends' : '📋 Incidents'}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflow: 'auto', padding: '24px' }}>
                    {loading ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>⏳</div>
                            Loading detailed analysis...
                        </div>
                    ) : error ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: '#ef4444' }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>⚠️</div>
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
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                    Risk Assessment
                                </h3>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
                                    <div style={{
                                        width: '100px',
                                        height: '100px',
                                        borderRadius: '50%',
                                        background: `conic-gradient(${getRiskColor(data.riskScore)} ${data.riskScore * 3.6}deg, var(--border) 0deg)`,
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
                                            <div style={{ fontSize: '28px', fontWeight: '700', color: getRiskColor(data.riskScore) }}>
                                                {data.riskScore}
                                            </div>
                                        </div>
                                    </div>
                                    <div>
                                        <div style={{
                                            fontSize: '20px',
                                            fontWeight: '700',
                                            color: getRiskColor(data.riskScore),
                                            textTransform: 'uppercase'
                                        }}>
                                            {data.anomalyLevel} RISK
                                        </div>
                                        <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginTop: '4px' }}>
                                            {data.totalIncidents} incidents • {data.totalMatches.toLocaleString()} matches
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Action Breakdown - Plotly Pie Chart */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                    Action Breakdown
                                </h3>
                                {Object.keys(data.actionCounts).length > 0 ? (
                                    <Plot
                                        data={[{
                                            type: 'pie',
                                            values: Object.values(data.actionCounts),
                                            labels: Object.keys(data.actionCounts),
                                            marker: {
                                                colors: Object.keys(data.actionCounts).map(a => ACTION_COLORS[a] || '#6b7280')
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
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                    Z-Score Analysis
                                </h3>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '12px' }}>
                                    {Object.entries(data.zScores).map(([key, value]) => {
                                        const status = getZScoreStatus(value)
                                        return (
                                            <div key={key} style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                padding: '12px',
                                                background: 'var(--surface)',
                                                borderRadius: '8px',
                                                border: '1px solid var(--border)'
                                            }}>
                                                <span style={{ fontSize: '12px', color: 'var(--text-secondary)', textTransform: 'capitalize' }}>
                                                    {key.replace(/_/g, ' ')}
                                                </span>
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                    <span style={{ fontWeight: '700', color: status.color }}>
                                                        {value.toFixed(2)}
                                                    </span>
                                                    <span>{status.icon}</span>
                                                </div>
                                            </div>
                                        )
                                    })}
                                </div>
                            </div>

                            {/* AI Analysis */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)',
                                gridColumn: 'span 2'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                    🤖 AI Analysis
                                </h3>
                                <div style={{ marginBottom: '16px' }}>
                                    <div style={{ fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Explanation</div>
                                    <p style={{ margin: 0, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                                        {data.aiExplanation}
                                    </p>
                                </div>
                                <div>
                                    <div style={{ fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Recommendation</div>
                                    <p style={{ margin: 0, color: 'var(--primary)', lineHeight: 1.6 }}>
                                        {data.aiRecommendation}
                                    </p>
                                </div>
                            </div>
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
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                    📊 Weekly Incidents <span style={{ fontWeight: 'normal', fontSize: '12px' }}>(click week for daily details)</span>
                                </h3>
                                {data.weeklyTrends.length > 0 ? (
                                    <Plot
                                        data={[
                                            { x: data.weeklyTrends.map(t => t.label), y: data.weeklyTrends.map(t => t.blockCount), name: 'Block', type: 'bar', marker: { color: '#ef4444' } },
                                            { x: data.weeklyTrends.map(t => t.label), y: data.weeklyTrends.map(t => t.quarantineCount), name: 'Quarantine', type: 'bar', marker: { color: '#8b5cf6' } },
                                            { x: data.weeklyTrends.map(t => t.label), y: data.weeklyTrends.map(t => t.authorizedCount), name: 'Authorized', type: 'bar', marker: { color: '#10b981' } },
                                            { x: data.weeklyTrends.map(t => t.label), y: data.weeklyTrends.map(t => t.releasedCount), name: 'Released', type: 'bar', marker: { color: '#f59e0b' } }
                                        ]}
                                        layout={{ ...plotlyLayout, height: 300, barmode: 'stack' }}
                                        config={plotlyConfig}
                                        style={{ width: '100%', height: '300px' }}
                                        onClick={(event: any) => {
                                            if (event.points && event.points.length > 0) {
                                                const weekLabel = event.points[0].x
                                                const selectedWeekData = data.weeklyTrends.find(w => w.label === weekLabel)
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
                                {selectedWeek && selectedWeek.dailyBreakdown && (
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
                                        <h4 style={{ margin: '0 0 12px', fontSize: '14px', color: 'var(--primary)' }}>
                                            📅 Daily Breakdown: {selectedWeek.label} ({selectedWeek.startDate} to {selectedWeek.endDate})
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
                                                {selectedWeek.dailyBreakdown.map((day, idx) => (
                                                    <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                        <td style={{ padding: '8px' }}>{day.date}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', fontWeight: '600' }}>{day.count}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#ef4444' }}>{day.blockCount}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#8b5cf6' }}>{day.quarantineCount}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#10b981' }}>{day.authorizedCount}</td>
                                                        <td style={{ padding: '8px', textAlign: 'center', color: '#f59e0b' }}>{day.releasedCount}</td>
                                                        <td style={{ padding: '8px', textAlign: 'right' }}>{day.totalMatches}</td>
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
                                <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                    📈 Monthly Trend
                                </h3>
                                {data.monthlyTrends.length > 0 ? (
                                    <Plot
                                        data={[
                                            { x: data.monthlyTrends.map(t => t.label), y: data.monthlyTrends.map(t => t.count), name: 'Total Incidents', type: 'scatter', mode: 'lines+markers', line: { color: '#3b82f6', width: 3 }, marker: { size: 8 } },
                                            { x: data.monthlyTrends.map(t => t.label), y: data.monthlyTrends.map(t => t.totalMatches), name: 'Total Matches', type: 'scatter', mode: 'lines+markers', yaxis: 'y2', line: { color: '#f59e0b', width: 2 }, marker: { size: 6 } }
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
                            {data.destinationPatterns.length > 0 && (
                                <div style={{
                                    background: 'var(--background)',
                                    borderRadius: '12px',
                                    padding: '24px',
                                    border: '1px solid var(--border)'
                                }}>
                                    <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                        🎯 Top Destinations
                                    </h3>
                                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))', gap: '12px' }}>
                                        {data.destinationPatterns.slice(0, 10).map((dp, idx) => (
                                            <div key={idx} style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                padding: '12px',
                                                background: 'var(--surface)',
                                                borderRadius: '8px',
                                                border: dp.isNew ? '1px solid #f59e0b' : '1px solid var(--border)'
                                            }}>
                                                <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '180px' }}>
                                                    <span style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{dp.destination}</span>
                                                    {dp.isNew && <span style={{ marginLeft: '8px', fontSize: '10px', color: '#f59e0b' }}>NEW</span>}
                                                </div>
                                                <div style={{ textAlign: 'right', fontSize: '12px' }}>
                                                    <div style={{ color: 'var(--text-primary)', fontWeight: '600' }}>{dp.incidentCount}</div>
                                                    <div style={{ color: 'var(--text-muted)' }}>{dp.totalMatches} matches</div>
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
                            <h3 style={{ margin: '0 0 16px', fontSize: '14px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                                📋 Top Incidents (by matches)
                            </h3>
                            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                <thead>
                                    <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>User</th>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Destination</th>
                                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Channel</th>
                                        <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Action</th>
                                        <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Matches</th>
                                        <th style={{ padding: '12px', textAlign: 'right', fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Date</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {data.topIncidents.map((inc, idx) => (
                                        <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>{inc.loginName}</td>
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
                                            <td style={{ padding: '12px', textAlign: 'center', fontWeight: '600', color: inc.maxMatches >= 10 ? '#ef4444' : 'var(--text-primary)' }}>
                                                {inc.maxMatches}
                                            </td>
                                            <td style={{ padding: '12px', textAlign: 'right', fontSize: '12px', color: 'var(--text-muted)' }}>
                                                {new Date(inc.timestamp).toLocaleDateString()}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    ) : null}
                </div>
            </div>
        </>
    )
}
