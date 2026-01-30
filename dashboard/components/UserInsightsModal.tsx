'use client'

import { useState, useEffect, useMemo } from 'react'
import apiClient from '@/lib/axios'
import dynamic from 'next/dynamic'
import { format, subDays, subWeeks, subMonths } from 'date-fns'

// Dynamic import for Plotly (client-side only)
const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface UserDailyRiskScore {
    id: number
    userEmail: string
    date: string
    dailyRiskScore: number
    incidentCount: number
    maxRiskScore: number
    avgRiskScore: number
    team?: string
    fullName?: string
}

interface TrendSummary {
    period: string
    totalIncidents: number
    avgDailyScore: number
    maxDailyScore: number
    minDailyScore: number
    trendDirection: 'up' | 'down' | 'stable'
    riskLevel: 'critical' | 'high' | 'medium' | 'low'
}

interface UserInsightsModalProps {
    isOpen: boolean
    onClose: () => void
    userEmail: string
    userName?: string
}

type PeriodFilter = 'daily' | 'weekly' | 'monthly' | 'quarterly'

export default function UserInsightsModal({
    isOpen,
    onClose,
    userEmail,
    userName
}: UserInsightsModalProps) {
    const [loading, setLoading] = useState(true)
    const [dailyScores, setDailyScores] = useState<UserDailyRiskScore[]>([])
    const [activePeriod, setActivePeriod] = useState<PeriodFilter>('daily')
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        if (isOpen && userEmail) {
            fetchData()
        }
    }, [isOpen, userEmail, activePeriod])

    const getDateRange = (period: PeriodFilter): { start: Date, end: Date } => {
        const end = new Date()
        let start: Date
        switch (period) {
            case 'daily':
                start = subDays(end, 7)
                break
            case 'weekly':
                start = subWeeks(end, 4)
                break
            case 'monthly':
                start = subMonths(end, 1)
                break
            case 'quarterly':
                start = subMonths(end, 3)
                break
            default:
                start = subDays(end, 7)
        }
        return { start, end }
    }

    const fetchData = async () => {
        setLoading(true)
        setError(null)
        try {
            const { start, end } = getDateRange(activePeriod)
            const startDate = format(start, 'yyyy-MM-dd')
            const endDate = format(end, 'yyyy-MM-dd')

            const response = await apiClient.get<UserDailyRiskScore[]>(
                `/api/risk-trends/user/${encodeURIComponent(userEmail)}/daily`,
                { params: { startDate, endDate } }
            )
            setDailyScores(response.data || [])
        } catch (err: any) {
            console.error('Error fetching user insights:', err)
            setError('Failed to load user insights')
            setDailyScores([])
        } finally {
            setLoading(false)
        }
    }

    // Calculate summary statistics
    const summary = useMemo((): TrendSummary | null => {
        if (dailyScores.length === 0) return null

        const totalIncidents = dailyScores.reduce((sum, s) => sum + s.incidentCount, 0)
        const avgDailyScore = dailyScores.reduce((sum, s) => sum + s.dailyRiskScore, 0) / dailyScores.length
        const maxDailyScore = Math.max(...dailyScores.map(s => s.dailyRiskScore))
        const minDailyScore = Math.min(...dailyScores.map(s => s.dailyRiskScore))

        // Calculate trend direction
        let trendDirection: 'up' | 'down' | 'stable' = 'stable'
        if (dailyScores.length >= 2) {
            const firstHalf = dailyScores.slice(0, Math.floor(dailyScores.length / 2))
            const secondHalf = dailyScores.slice(Math.floor(dailyScores.length / 2))
            const firstAvg = firstHalf.reduce((s, d) => s + d.dailyRiskScore, 0) / firstHalf.length
            const secondAvg = secondHalf.reduce((s, d) => s + d.dailyRiskScore, 0) / secondHalf.length
            if (secondAvg > firstAvg * 1.1) trendDirection = 'up'
            else if (secondAvg < firstAvg * 0.9) trendDirection = 'down'
        }

        // Determine risk level
        let riskLevel: 'critical' | 'high' | 'medium' | 'low' = 'low'
        if (avgDailyScore >= 75) riskLevel = 'critical'
        else if (avgDailyScore >= 50) riskLevel = 'high'
        else if (avgDailyScore >= 25) riskLevel = 'medium'

        return {
            period: activePeriod,
            totalIncidents,
            avgDailyScore,
            maxDailyScore,
            minDailyScore,
            trendDirection,
            riskLevel
        }
    }, [dailyScores, activePeriod])

    // Aggregate data for weekly/monthly/quarterly views
    const aggregatedData = useMemo(() => {
        if (activePeriod === 'daily') {
            return dailyScores.map(s => ({
                label: format(new Date(s.date), 'MMM dd'),
                score: s.dailyRiskScore,
                incidents: s.incidentCount,
                maxScore: s.maxRiskScore,
                avgScore: s.avgRiskScore,
                date: s.date
            }))
        }

        // For weekly, monthly, quarterly - aggregate by week
        const grouped: Record<string, { scores: number[], incidents: number, maxScore: number, avgScores: number[] }> = {}

        dailyScores.forEach(s => {
            let key: string
            const date = new Date(s.date)
            if (activePeriod === 'weekly') {
                // Group by week number
                const weekStart = new Date(date)
                weekStart.setDate(date.getDate() - date.getDay())
                key = format(weekStart, 'MMM dd')
            } else {
                // Group by week for monthly/quarterly too but show more weeks
                const weekStart = new Date(date)
                weekStart.setDate(date.getDate() - date.getDay())
                key = format(weekStart, 'MMM dd')
            }

            if (!grouped[key]) {
                grouped[key] = { scores: [], incidents: 0, maxScore: 0, avgScores: [] }
            }
            grouped[key].scores.push(s.dailyRiskScore)
            grouped[key].incidents += s.incidentCount
            grouped[key].maxScore = Math.max(grouped[key].maxScore, s.maxRiskScore)
            grouped[key].avgScores.push(s.avgRiskScore)
        })

        return Object.entries(grouped).map(([label, data]) => ({
            label,
            score: data.scores.reduce((a, b) => a + b, 0) / data.scores.length,
            incidents: data.incidents,
            maxScore: data.maxScore,
            avgScore: data.avgScores.reduce((a, b) => a + b, 0) / data.avgScores.length,
            date: label
        }))
    }, [dailyScores, activePeriod])

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
        if (score >= 75) return '#dc2626' // Critical - red
        if (score >= 50) return '#f59e0b' // High - orange  
        if (score >= 25) return '#eab308' // Medium - yellow
        return '#10b981' // Low - green
    }

    const getTrendIcon = (direction: 'up' | 'down' | 'stable') => {
        switch (direction) {
            case 'up': return '📈'
            case 'down': return '📉'
            default: return '➡️'
        }
    }

    const periodLabels: Record<PeriodFilter, string> = {
        daily: 'Last 7 Days',
        weekly: 'Last 4 Weeks',
        monthly: 'Last Month',
        quarterly: 'Last 3 Months'
    }

    // Plotly layout config (dark theme)
    const plotlyLayout = {
        paper_bgcolor: 'transparent',
        plot_bgcolor: 'transparent',
        font: { color: '#9ca3af', size: 11 },
        margin: { l: 50, r: 30, t: 30, b: 50 },
        xaxis: { gridcolor: '#374151', linecolor: '#374151', tickangle: -45 },
        yaxis: { gridcolor: '#374151', linecolor: '#374151', title: 'Risk Score (0-100)' },
        legend: { orientation: 'h' as const, y: -0.25 },
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
                    maxWidth: '1200px',
                    maxHeight: '90vh',
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
                                User Risk Insights
                            </div>
                            <h2 style={{ margin: 0, fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)' }}>
                                {userName || userEmail}
                            </h2>
                            {userName && (
                                <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginTop: '4px' }}>
                                    {userEmail}
                                </div>
                            )}
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

                    {/* Period Filter Tabs */}
                    <div style={{ display: 'flex', gap: '8px', marginTop: '20px' }}>
                        {(['daily', 'weekly', 'monthly', 'quarterly'] as PeriodFilter[]).map(period => (
                            <button
                                key={period}
                                onClick={() => setActivePeriod(period)}
                                style={{
                                    padding: '10px 24px',
                                    border: 'none',
                                    borderRadius: '24px',
                                    cursor: 'pointer',
                                    fontWeight: '600',
                                    fontSize: '13px',
                                    transition: 'all 0.2s',
                                    background: activePeriod === period ? 'var(--primary)' : 'var(--background)',
                                    color: activePeriod === period ? 'white' : 'var(--text-secondary)'
                                }}
                            >
                                {period === 'daily' && '📅'} 
                                {period === 'weekly' && '📆'} 
                                {period === 'monthly' && '🗓️'} 
                                {period === 'quarterly' && '📊'} 
                                {' '}{periodLabels[period]}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflow: 'auto', padding: '24px' }}>
                    {loading ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>⏳</div>
                            Loading user insights...
                        </div>
                    ) : error ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: '#ef4444' }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>⚠️</div>
                            {error}
                        </div>
                    ) : dailyScores.length === 0 ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>📭</div>
                            No risk data available for this period
                        </div>
                    ) : (
                        <div style={{ display: 'grid', gap: '24px' }}>
                            {/* Summary Cards */}
                            {summary && (
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
                                    {/* Average Risk Score */}
                                    <div style={{
                                        background: 'var(--background)',
                                        borderRadius: '12px',
                                        padding: '20px',
                                        border: '1px solid var(--border)',
                                        textAlign: 'center'
                                    }}>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>
                                            Avg Daily Score
                                        </div>
                                        <div style={{
                                            fontSize: '36px',
                                            fontWeight: '800',
                                            color: getRiskColor(summary.avgDailyScore)
                                        }}>
                                            {summary.avgDailyScore.toFixed(1)}
                                        </div>
                                        <div style={{
                                            marginTop: '8px',
                                            padding: '4px 12px',
                                            borderRadius: '12px',
                                            fontSize: '11px',
                                            fontWeight: '700',
                                            display: 'inline-block',
                                            background: `${getRiskColor(summary.avgDailyScore)}20`,
                                            color: getRiskColor(summary.avgDailyScore)
                                        }}>
                                            {summary.riskLevel.toUpperCase()}
                                        </div>
                                    </div>

                                    {/* Total Incidents */}
                                    <div style={{
                                        background: 'var(--background)',
                                        borderRadius: '12px',
                                        padding: '20px',
                                        border: '1px solid var(--border)',
                                        textAlign: 'center'
                                    }}>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>
                                            Total Incidents
                                        </div>
                                        <div style={{ fontSize: '36px', fontWeight: '800', color: 'var(--text-primary)' }}>
                                            {summary.totalIncidents.toLocaleString()}
                                        </div>
                                        <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--text-secondary)' }}>
                                            in {periodLabels[activePeriod].toLowerCase()}
                                        </div>
                                    </div>

                                    {/* Max Risk Score */}
                                    <div style={{
                                        background: 'var(--background)',
                                        borderRadius: '12px',
                                        padding: '20px',
                                        border: '1px solid var(--border)',
                                        textAlign: 'center'
                                    }}>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>
                                            Peak Daily Score
                                        </div>
                                        <div style={{
                                            fontSize: '36px',
                                            fontWeight: '800',
                                            color: getRiskColor(summary.maxDailyScore)
                                        }}>
                                            {summary.maxDailyScore.toFixed(1)}
                                        </div>
                                        <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--text-secondary)' }}>
                                            highest in period
                                        </div>
                                    </div>

                                    {/* Trend Direction */}
                                    <div style={{
                                        background: 'var(--background)',
                                        borderRadius: '12px',
                                        padding: '20px',
                                        border: '1px solid var(--border)',
                                        textAlign: 'center'
                                    }}>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>
                                            Trend
                                        </div>
                                        <div style={{ fontSize: '36px' }}>
                                            {getTrendIcon(summary.trendDirection)}
                                        </div>
                                        <div style={{
                                            marginTop: '8px',
                                            fontSize: '14px',
                                            fontWeight: '600',
                                            color: summary.trendDirection === 'up' ? '#ef4444' :
                                                   summary.trendDirection === 'down' ? '#10b981' : 'var(--text-secondary)'
                                        }}>
                                            {summary.trendDirection === 'up' ? 'Increasing' :
                                             summary.trendDirection === 'down' ? 'Decreasing' : 'Stable'}
                                        </div>
                                    </div>
                                </div>
                            )}

                            {/* Risk Score Chart */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>
                                    📈 Risk Score Trend - {periodLabels[activePeriod]}
                                </h3>
                                <Plot
                                    data={[
                                        {
                                            x: aggregatedData.map(d => d.label),
                                            y: aggregatedData.map(d => d.score),
                                            type: 'scatter',
                                            mode: 'lines+markers',
                                            name: 'Daily Risk Score',
                                            line: { color: '#3b82f6', width: 3, shape: 'spline' },
                                            marker: { size: 8, color: aggregatedData.map(d => getRiskColor(d.score)) },
                                            fill: 'tozeroy',
                                            fillcolor: 'rgba(59, 130, 246, 0.1)'
                                        }
                                    ]}
                                    layout={{
                                        ...plotlyLayout,
                                        height: 350,
                                        yaxis: { ...plotlyLayout.yaxis, range: [0, 100] },
                                        shapes: [
                                            // Critical threshold line
                                            { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 75, y1: 75, line: { color: '#dc2626', width: 1, dash: 'dash' } },
                                            // High threshold line
                                            { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 50, y1: 50, line: { color: '#f59e0b', width: 1, dash: 'dash' } },
                                            // Medium threshold line
                                            { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 25, y1: 25, line: { color: '#eab308', width: 1, dash: 'dash' } }
                                        ],
                                        annotations: [
                                            { x: 1.02, xref: 'paper', y: 75, text: 'Critical', showarrow: false, font: { size: 10, color: '#dc2626' } },
                                            { x: 1.02, xref: 'paper', y: 50, text: 'High', showarrow: false, font: { size: 10, color: '#f59e0b' } },
                                            { x: 1.02, xref: 'paper', y: 25, text: 'Medium', showarrow: false, font: { size: 10, color: '#eab308' } }
                                        ]
                                    }}
                                    config={plotlyConfig}
                                    style={{ width: '100%', height: '350px' }}
                                />
                            </div>

                            {/* Incident Count Chart */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>
                                    📊 Incident Volume
                                </h3>
                                <Plot
                                    data={[
                                        {
                                            x: aggregatedData.map(d => d.label),
                                            y: aggregatedData.map(d => d.incidents),
                                            type: 'bar',
                                            name: 'Incidents',
                                            marker: {
                                                color: aggregatedData.map(d => getRiskColor(d.score)),
                                                opacity: 0.8
                                            }
                                        }
                                    ]}
                                    layout={{
                                        ...plotlyLayout,
                                        height: 250,
                                        yaxis: { ...plotlyLayout.yaxis, title: 'Incident Count' }
                                    }}
                                    config={plotlyConfig}
                                    style={{ width: '100%', height: '250px' }}
                                />
                            </div>

                            {/* Detailed Data Table */}
                            <div style={{
                                background: 'var(--background)',
                                borderRadius: '12px',
                                padding: '24px',
                                border: '1px solid var(--border)'
                            }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>
                                    📋 Detailed History
                                </h3>
                                <div style={{ overflowX: 'auto' }}>
                                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                                        <thead>
                                            <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                                <th style={{ textAlign: 'left', padding: '12px', color: 'var(--text-muted)', fontWeight: '600' }}>
                                                    {activePeriod === 'daily' ? 'Date' : 'Period'}
                                                </th>
                                                <th style={{ textAlign: 'center', padding: '12px', color: 'var(--text-muted)', fontWeight: '600' }}>Risk Score</th>
                                                <th style={{ textAlign: 'center', padding: '12px', color: 'var(--text-muted)', fontWeight: '600' }}>Incidents</th>
                                                <th style={{ textAlign: 'center', padding: '12px', color: 'var(--text-muted)', fontWeight: '600' }}>Max Score</th>
                                                <th style={{ textAlign: 'center', padding: '12px', color: 'var(--text-muted)', fontWeight: '600' }}>Avg Score</th>
                                                <th style={{ textAlign: 'center', padding: '12px', color: 'var(--text-muted)', fontWeight: '600' }}>Risk Level</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {aggregatedData.map((row, idx) => {
                                                const riskLevel = row.score >= 75 ? 'Critical' :
                                                                  row.score >= 50 ? 'High' :
                                                                  row.score >= 25 ? 'Medium' : 'Low'
                                                return (
                                                    <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                        <td style={{ padding: '12px', color: 'var(--text-primary)', fontWeight: '500' }}>
                                                            {row.label}
                                                        </td>
                                                        <td style={{ padding: '12px', textAlign: 'center' }}>
                                                            <span style={{
                                                                fontWeight: '700',
                                                                color: getRiskColor(row.score),
                                                                fontSize: '15px'
                                                            }}>
                                                                {row.score.toFixed(1)}
                                                            </span>
                                                        </td>
                                                        <td style={{ padding: '12px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                                                            {row.incidents.toLocaleString()}
                                                        </td>
                                                        <td style={{ padding: '12px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                                                            {row.maxScore.toFixed(0)}
                                                        </td>
                                                        <td style={{ padding: '12px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                                                            {row.avgScore.toFixed(1)}
                                                        </td>
                                                        <td style={{ padding: '12px', textAlign: 'center' }}>
                                                            <span style={{
                                                                padding: '4px 12px',
                                                                borderRadius: '12px',
                                                                fontSize: '11px',
                                                                fontWeight: '700',
                                                                background: `${getRiskColor(row.score)}20`,
                                                                color: getRiskColor(row.score)
                                                            }}>
                                                                {riskLevel}
                                                            </span>
                                                        </td>
                                                    </tr>
                                                )
                                            })}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </>
    )
}
