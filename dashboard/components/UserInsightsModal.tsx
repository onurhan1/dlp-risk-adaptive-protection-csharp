'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'
import dynamic from 'next/dynamic'

// Dynamic import for Plotly (client-side only)
const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface DailyScore {
    date: string
    dailyRiskScore: number
    incidentCount: number
    maxRiskScore: number
    avgRiskScore: number
    blockCount: number
    permitCount: number
    quarantineCount: number
    releasedCount: number
    maxMaxMatches: number
    avgMaxMatches: number
}

interface PeriodAverage {
    avgScore: number
    totalIncidents: number
    totalBlocks: number
    totalQuarantines: number
}

interface ComprehensiveInsights {
    userEmail: string
    fullName: string
    team: string
    period: string
    startDate: string
    endDate: string
    summary: {
        totalIncidents: number
        avgDailyScore: number
        maxDailyScore: number
        minDailyScore: number
        totalBlockCount: number
        totalPermitCount: number
        totalQuarantineCount: number
        totalReleasedCount: number
        maxMaxMatches: number
        avgMaxMatches: number
    }
    periodAverages: {
        weekly: PeriodAverage
        monthly: PeriodAverage
        quarterly: PeriodAverage
    }
    dailyScores: DailyScore[]
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
    const [data, setData] = useState<ComprehensiveInsights | null>(null)
    const [activePeriod, setActivePeriod] = useState<PeriodFilter>('monthly')
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        if (isOpen && userEmail) {
            fetchData()
        }
    }, [isOpen, userEmail, activePeriod])

    const fetchData = async () => {
        setLoading(true)
        setError(null)
        try {
            const response = await apiClient.get<ComprehensiveInsights>(
                `/api/risk-trends/user/${encodeURIComponent(userEmail)}/comprehensive`,
                { params: { period: activePeriod } }
            )
            setData(response.data)
        } catch (err: any) {
            console.error('Error fetching user insights:', err)
            setError('Failed to load user insights')
            setData(null)
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
        if (score >= 75) return '#dc2626'
        if (score >= 50) return '#f59e0b'
        if (score >= 25) return '#eab308'
        return '#10b981'
    }

    const getRiskLevel = (score: number) => {
        if (score >= 75) return 'Critical'
        if (score >= 50) return 'High'
        if (score >= 25) return 'Medium'
        return 'Low'
    }

    const periodLabels: Record<PeriodFilter, string> = {
        daily: 'Last 7 Days',
        weekly: 'Last 2 Weeks',
        monthly: 'Last 1 Month',
        quarterly: 'Last 3 Months'
    }

    const plotlyLayout = {
        paper_bgcolor: 'transparent',
        plot_bgcolor: 'transparent',
        font: { color: '#9ca3af', size: 11 },
        margin: { l: 50, r: 30, t: 30, b: 50 },
        xaxis: { gridcolor: '#374151', linecolor: '#374151', tickangle: -45 },
        yaxis: { gridcolor: '#374151', linecolor: '#374151' },
        legend: { orientation: 'h' as const, y: -0.25 },
        showlegend: true
    }

    const plotlyConfig = { displayModeBar: false, responsive: true }

    const getTrendDirection = () => {
        if (!data || data.dailyScores.length < 2) return 'stable'
        const scores = data.dailyScores
        const firstHalf = scores.slice(0, Math.floor(scores.length / 2))
        const secondHalf = scores.slice(Math.floor(scores.length / 2))
        const firstAvg = firstHalf.reduce((s, d) => s + d.dailyRiskScore, 0) / firstHalf.length
        const secondAvg = secondHalf.reduce((s, d) => s + d.dailyRiskScore, 0) / secondHalf.length
        if (secondAvg > firstAvg * 1.1) return 'up'
        if (secondAvg < firstAvg * 0.9) return 'down'
        return 'stable'
    }

    const trendDirection = getTrendDirection()

    return (
        <>
            <div onClick={onClose} style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0, 0, 0, 0.7)', zIndex: 9998, backdropFilter: 'blur(4px)' }} />
            <div onClick={(e) => e.stopPropagation()} style={{ position: 'fixed', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', backgroundColor: 'var(--surface)', borderRadius: '16px', border: '1px solid var(--border)', boxShadow: '0 25px 70px rgba(0, 0, 0, 0.5)', zIndex: 9999, width: '95%', maxWidth: '1400px', maxHeight: '90vh', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
                {/* Header */}
                <div style={{ padding: '24px', borderBottom: '1px solid var(--border)', background: 'linear-gradient(135deg, var(--surface) 0%, var(--background-secondary) 100%)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                        <div>
                            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>User Risk Insights</div>
                            <h2 style={{ margin: 0, fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)' }}>{data?.fullName || userName || userEmail}</h2>
                            {(data?.fullName || userName) && <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginTop: '4px' }}>{userEmail} {data?.team && `• ${data.team}`}</div>}
                        </div>
                        <button onClick={onClose} style={{ background: 'transparent', border: 'none', fontSize: '28px', cursor: 'pointer', color: 'var(--text-secondary)', lineHeight: 1 }}>×</button>
                    </div>
                    <div style={{ display: 'flex', gap: '8px', marginTop: '20px' }}>
                        {(['daily', 'weekly', 'monthly', 'quarterly'] as PeriodFilter[]).map(period => (
                            <button key={period} onClick={() => setActivePeriod(period)} style={{ padding: '10px 24px', border: 'none', borderRadius: '24px', cursor: 'pointer', fontWeight: '600', fontSize: '13px', transition: 'all 0.2s', background: activePeriod === period ? 'var(--primary)' : 'var(--background)', color: activePeriod === period ? 'white' : 'var(--text-secondary)' }}>
                                {period === 'daily' && '📅'} {period === 'weekly' && '📆'} {period === 'monthly' && '🗓️'} {period === 'quarterly' && '📊'} {periodLabels[period]}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflow: 'auto', padding: '24px' }}>
                    {loading ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}><div style={{ fontSize: '48px', marginBottom: '16px' }}>⏳</div>Loading user insights...</div>
                    ) : error ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: '#ef4444' }}><div style={{ fontSize: '48px', marginBottom: '16px' }}>⚠️</div>{error}</div>
                    ) : !data || data.dailyScores.length === 0 ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}><div style={{ fontSize: '48px', marginBottom: '16px' }}>📭</div>No risk data available for this period</div>
                    ) : (
                        <div style={{ display: 'grid', gap: '24px' }}>
                            {/* Period Averages */}
                            <div style={{ background: 'linear-gradient(135deg, var(--background) 0%, var(--surface) 100%)', borderRadius: '12px', padding: '20px', border: '2px solid var(--primary)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>📊 Period Averages Comparison</h3>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', textAlign: 'center' }}>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', textTransform: 'uppercase' }}>Weekly Avg</div>
                                        <div style={{ fontSize: '32px', fontWeight: '800', color: getRiskColor(data.periodAverages.weekly.avgScore) }}>{data.periodAverages.weekly.avgScore.toFixed(1)}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{data.periodAverages.weekly.totalIncidents} incidents</div>
                                    </div>
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', textAlign: 'center' }}>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', textTransform: 'uppercase' }}>Monthly Avg</div>
                                        <div style={{ fontSize: '32px', fontWeight: '800', color: getRiskColor(data.periodAverages.monthly.avgScore) }}>{data.periodAverages.monthly.avgScore.toFixed(1)}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{data.periodAverages.monthly.totalIncidents} incidents</div>
                                    </div>
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', textAlign: 'center' }}>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', textTransform: 'uppercase' }}>3-Month Avg</div>
                                        <div style={{ fontSize: '32px', fontWeight: '800', color: getRiskColor(data.periodAverages.quarterly.avgScore) }}>{data.periodAverages.quarterly.avgScore.toFixed(1)}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{data.periodAverages.quarterly.totalIncidents} incidents</div>
                                    </div>
                                </div>
                            </div>

                            {/* Summary Cards */}
                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px' }}>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>Avg Daily Score</div>
                                    <div style={{ fontSize: '36px', fontWeight: '800', color: getRiskColor(data.summary.avgDailyScore) }}>{data.summary.avgDailyScore.toFixed(1)}</div>
                                    <div style={{ marginTop: '8px', padding: '4px 12px', borderRadius: '12px', fontSize: '11px', fontWeight: '700', display: 'inline-block', background: `${getRiskColor(data.summary.avgDailyScore)}20`, color: getRiskColor(data.summary.avgDailyScore) }}>{getRiskLevel(data.summary.avgDailyScore)}</div>
                                </div>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>Total Incidents</div>
                                    <div style={{ fontSize: '36px', fontWeight: '800', color: 'var(--text-primary)' }}>{data.summary.totalIncidents.toLocaleString()}</div>
                                    <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--text-secondary)' }}>in {periodLabels[activePeriod].toLowerCase()}</div>
                                </div>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>Max Matches</div>
                                    <div style={{ fontSize: '36px', fontWeight: '800', color: data.summary.maxMaxMatches > 100 ? '#dc2626' : 'var(--text-primary)' }}>{data.summary.maxMaxMatches.toLocaleString()}</div>
                                    <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--text-secondary)' }}>avg: {data.summary.avgMaxMatches.toFixed(1)}</div>
                                </div>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>Trend</div>
                                    <div style={{ fontSize: '36px' }}>{trendDirection === 'up' ? '📈' : trendDirection === 'down' ? '📉' : '➡️'}</div>
                                    <div style={{ marginTop: '8px', fontSize: '14px', fontWeight: '600', color: trendDirection === 'up' ? '#ef4444' : trendDirection === 'down' ? '#10b981' : 'var(--text-secondary)' }}>{trendDirection === 'up' ? 'Increasing' : trendDirection === 'down' ? 'Decreasing' : 'Stable'}</div>
                                </div>
                            </div>

                            {/* Action Breakdown */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>🎯 Action Breakdown</h3>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #ef4444' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#ef4444' }}>{data.summary.totalBlockCount}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>BLOCKED</div>
                                    </div>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #8b5cf6' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#8b5cf6' }}>{data.summary.totalQuarantineCount}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>QUARANTINED</div>
                                    </div>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #10b981' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#10b981' }}>{data.summary.totalPermitCount}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>PERMITTED</div>
                                    </div>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #f59e0b' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#f59e0b' }}>{data.summary.totalReleasedCount}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>RELEASED</div>
                                    </div>
                                </div>
                            </div>

                            {/* Risk Score Chart */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>📈 Risk Score Trend - {periodLabels[activePeriod]}</h3>
                                <Plot
                                    data={[{ x: data.dailyScores.map(d => d.date), y: data.dailyScores.map(d => d.dailyRiskScore), type: 'scatter', mode: 'lines+markers', name: 'Daily Risk Score', line: { color: '#3b82f6', width: 3, shape: 'spline' }, marker: { size: 8, color: data.dailyScores.map(d => getRiskColor(d.dailyRiskScore)) }, fill: 'tozeroy', fillcolor: 'rgba(59, 130, 246, 0.1)' }]}
                                    layout={{ ...plotlyLayout, height: 350, yaxis: { ...plotlyLayout.yaxis, title: 'Risk Score (0-100)', range: [0, 100] }, shapes: [{ type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 75, y1: 75, line: { color: '#dc2626', width: 1, dash: 'dash' } }, { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 50, y1: 50, line: { color: '#f59e0b', width: 1, dash: 'dash' } }, { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 25, y1: 25, line: { color: '#eab308', width: 1, dash: 'dash' } }], annotations: [{ x: 1.02, xref: 'paper', y: 75, text: 'Critical', showarrow: false, font: { size: 10, color: '#dc2626' } }, { x: 1.02, xref: 'paper', y: 50, text: 'High', showarrow: false, font: { size: 10, color: '#f59e0b' } }, { x: 1.02, xref: 'paper', y: 25, text: 'Medium', showarrow: false, font: { size: 10, color: '#eab308' } }] }}
                                    config={plotlyConfig}
                                    style={{ width: '100%', height: '350px' }}
                                />
                            </div>

                            {/* Incident & Matches Chart */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>📊 Incidents & Matches</h3>
                                <Plot
                                    data={[{ x: data.dailyScores.map(d => d.date), y: data.dailyScores.map(d => d.incidentCount), type: 'bar', name: 'Incidents', marker: { color: '#3b82f6', opacity: 0.8 } }, { x: data.dailyScores.map(d => d.date), y: data.dailyScores.map(d => d.maxMaxMatches), type: 'scatter', mode: 'lines+markers', name: 'Max Matches', yaxis: 'y2', line: { color: '#f59e0b', width: 2 }, marker: { size: 6 } }]}
                                    layout={{ ...plotlyLayout, height: 280, yaxis: { ...plotlyLayout.yaxis, title: 'Incident Count' }, yaxis2: { overlaying: 'y', side: 'right', title: 'Max Matches', gridcolor: 'transparent' } }}
                                    config={plotlyConfig}
                                    style={{ width: '100%', height: '280px' }}
                                />
                            </div>

                            {/* Detailed Table */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)' }}>📋 Detailed Daily History</h3>
                                <div style={{ overflowX: 'auto' }}>
                                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                                        <thead>
                                            <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                                <th style={{ textAlign: 'left', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>Date</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>Score</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>Incidents</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#ef4444', fontWeight: '600' }}>Block</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#8b5cf6', fontWeight: '600' }}>Quarantine</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#10b981', fontWeight: '600' }}>Permit</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#f59e0b', fontWeight: '600' }}>Released</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>Max Matches</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>Avg Matches</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>Level</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {data.dailyScores.map((row, idx) => (
                                                <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                    <td style={{ padding: '10px', color: 'var(--text-primary)', fontWeight: '500' }}>{row.date}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center' }}><span style={{ fontWeight: '700', color: getRiskColor(row.dailyRiskScore), fontSize: '14px' }}>{row.dailyRiskScore.toFixed(1)}</span></td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: 'var(--text-secondary)' }}>{row.incidentCount}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.blockCount > 0 ? '#ef4444' : 'var(--text-muted)' }}>{row.blockCount}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.quarantineCount > 0 ? '#8b5cf6' : 'var(--text-muted)' }}>{row.quarantineCount}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.permitCount > 0 ? '#10b981' : 'var(--text-muted)' }}>{row.permitCount}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.releasedCount > 0 ? '#f59e0b' : 'var(--text-muted)' }}>{row.releasedCount}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.maxMaxMatches > 100 ? '#ef4444' : 'var(--text-secondary)' }}>{row.maxMaxMatches.toLocaleString()}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: 'var(--text-secondary)' }}>{row.avgMaxMatches.toFixed(1)}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center' }}><span style={{ padding: '3px 10px', borderRadius: '12px', fontSize: '10px', fontWeight: '700', background: `${getRiskColor(row.dailyRiskScore)}20`, color: getRiskColor(row.dailyRiskScore) }}>{getRiskLevel(row.dailyRiskScore)}</span></td>
                                                </tr>
                                            ))}
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
