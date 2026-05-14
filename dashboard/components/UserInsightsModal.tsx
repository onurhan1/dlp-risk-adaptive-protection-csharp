'use client'

import { useState, useEffect, useMemo } from 'react'
import apiClient from '@/lib/axios'
import dynamic from 'next/dynamic'
import Pagination from './ui/Pagination'
import { Loader2, Inbox, BarChart3, TrendingUp, TrendingDown, Minus, Target, ClipboardList, Calendar, CalendarDays, CalendarRange, AlertTriangle } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'

// Dynamic import for Plotly (client-side only)
const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface DailyScore {
    date: string
    daily_risk_score: number
    incident_count: number
    max_risk_score: number
    avg_risk_score: number
    block_count: number
    permit_count: number
    quarantine_count: number
    released_count: number
    max_max_matches: number
    avg_max_matches: number
}

interface PeriodAverage {
    avg_score: number
    total_incidents: number
    total_blocks: number
    total_quarantines: number
}

interface ComprehensiveInsights {
    user_email: string
    full_name: string
    team: string
    period: string
    start_date: string
    end_date: string
    summary: {
        total_incidents: number
        avg_daily_score: number
        max_daily_score: number
        min_daily_score: number
        total_block_count: number
        total_permit_count: number
        total_quarantine_count: number
        total_released_count: number
        max_max_matches: number
        avg_max_matches: number
    }
    period_averages: {
        weekly: PeriodAverage
        monthly: PeriodAverage
        quarterly: PeriodAverage
    }
    daily_scores: DailyScore[]
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
    const { t } = useTranslation()
    const [data, setData] = useState<ComprehensiveInsights | null>(null)
    const [activePeriod, setActivePeriod] = useState<PeriodFilter>('monthly')
    const [error, setError] = useState<string | null>(null)

    // Pagination state
    const [currentPage, setCurrentPage] = useState(1)
    const pageSize = 10

    // Reset to first page when period changes
    useEffect(() => {
        setCurrentPage(1)
    }, [activePeriod])

    // Client-side pagination for daily scores
    const paginatedDailyScores = useMemo(() => {
        if (!data?.daily_scores) return []
        const startIndex = (currentPage - 1) * pageSize
        return data.daily_scores.slice(startIndex, startIndex + pageSize)
    }, [data?.daily_scores, currentPage, pageSize])

    const totalPages = data?.daily_scores ? Math.ceil(data.daily_scores.length / pageSize) : 0

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
        if (score >= 75) return '#ef4444'
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
        daily: t('insights.last7Days'),
        weekly: t('insights.last2Weeks'),
        monthly: t('insights.last1Month'),
        quarterly: t('insights.last3Months')
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
        if (!data || data.daily_scores.length < 2) return 'stable'
        const scores = data.daily_scores
        const firstHalf = scores.slice(0, Math.floor(scores.length / 2))
        const secondHalf = scores.slice(Math.floor(scores.length / 2))
        const firstAvg = firstHalf.reduce((s, d) => s + d.daily_risk_score, 0) / firstHalf.length
        const secondAvg = secondHalf.reduce((s, d) => s + d.daily_risk_score, 0) / secondHalf.length
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
                            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'none', marginBottom: '4px' }}>{t('insights.title')}</div>
                            <h2 style={{ margin: 0, fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)' }}>{data?.full_name || userName || userEmail}</h2>
                            {(data?.full_name || userName) && <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginTop: '4px' }}>{userEmail} {data?.team && `• ${data.team}`}</div>}
                        </div>
                        <button onClick={onClose} style={{ background: 'transparent', border: 'none', fontSize: '28px', cursor: 'pointer', color: 'var(--text-secondary)', lineHeight: 1 }}>×</button>
                    </div>
                    <div style={{ display: 'flex', gap: '8px', marginTop: '20px' }}>
                        {(['daily', 'weekly', 'monthly', 'quarterly'] as PeriodFilter[]).map(period => (
                            <button key={period} onClick={() => setActivePeriod(period)} style={{ padding: '10px 24px', border: 'none', borderRadius: '24px', cursor: 'pointer', fontWeight: '600', fontSize: '13px', transition: 'all 0.2s', background: activePeriod === period ? 'var(--primary)' : 'var(--background)', color: activePeriod === period ? 'white' : 'var(--text-secondary)' }}>
                                {period === 'daily' && <Calendar size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} />}
                                {period === 'weekly' && <CalendarDays size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} />}
                                {period === 'monthly' && <CalendarRange size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} />}
                                {period === 'quarterly' && <BarChart3 size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '4px' }} />}
                                {periodLabels[period]}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflow: 'auto', padding: '24px' }}>
                    {loading ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}><div style={{ marginBottom: '16px' }}><Loader2 size={48} style={{ animation: 'spin 1s linear infinite' }} /></div><style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>{t('insights.loading')}</div>
                    ) : error ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: '#ef4444' }}><div style={{ marginBottom: '16px' }}><AlertTriangle size={48} /></div>{error}</div>
                    ) : !data || data.daily_scores.length === 0 ? (
                        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}><div style={{ marginBottom: '16px' }}><Inbox size={48} style={{ opacity: 0.4 }} /></div>{t('insights.noData')}</div>
                    ) : (
                        <div style={{ display: 'grid', gap: '24px' }}>
                            {/* Period Averages */}
                            <div style={{ background: 'linear-gradient(135deg, var(--background) 0%, var(--surface) 100%)', borderRadius: '12px', padding: '20px', border: '2px solid var(--primary)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}><BarChart3 size={18} /> {t('insights.periodAvg')}</h3>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', textAlign: 'center' }}>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', textTransform: 'none' }}>{t('insights.weeklyAvg')}</div>
                                        <div style={{ fontSize: '32px', fontWeight: '800', color: getRiskColor(data.period_averages.weekly.avg_score) }}>{data.period_averages.weekly.avg_score.toFixed(1)}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{data.period_averages.weekly.total_incidents} {t('insights.incidents')}</div>
                                    </div>
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', textAlign: 'center' }}>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', textTransform: 'none' }}>{t('insights.monthlyAvg')}</div>
                                        <div style={{ fontSize: '32px', fontWeight: '800', color: getRiskColor(data.period_averages.monthly.avg_score) }}>{data.period_averages.monthly.avg_score.toFixed(1)}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{data.period_averages.monthly.total_incidents} {t('insights.incidents')}</div>
                                    </div>
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', textAlign: 'center' }}>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', textTransform: 'none' }}>{t('insights.threeMonthAvg')}</div>
                                        <div style={{ fontSize: '32px', fontWeight: '800', color: getRiskColor(data.period_averages.quarterly.avg_score) }}>{data.period_averages.quarterly.avg_score.toFixed(1)}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{data.period_averages.quarterly.total_incidents} {t('insights.incidents')}</div>
                                    </div>
                                </div>
                            </div>

                            {/* Summary Cards */}
                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px' }}>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'none' }}>{t('insights.avgDailyScore')}</div>
                                    <div style={{ fontSize: '36px', fontWeight: '800', color: getRiskColor(data.summary.avg_daily_score) }}>{data.summary.avg_daily_score.toFixed(1)}</div>
                                    <div style={{ marginTop: '8px', padding: '4px 12px', borderRadius: '12px', fontSize: '11px', fontWeight: '700', display: 'inline-block', background: `${getRiskColor(data.summary.avg_daily_score)}20`, color: getRiskColor(data.summary.avg_daily_score) }}>{getRiskLevel(data.summary.avg_daily_score)}</div>
                                </div>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'none' }}>{t('insights.totalIncidents')}</div>
                                    <div style={{ fontSize: '36px', fontWeight: '800', color: 'var(--text-primary)' }}>{data.summary.total_incidents.toLocaleString()}</div>
                                    <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--text-secondary)' }}>in {periodLabels[activePeriod].toLowerCase()}</div>
                                </div>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'none' }}>{t('insights.maxMatches')}</div>
                                    <div style={{ fontSize: '36px', fontWeight: '800', color: (data.summary.max_max_matches ?? 0) > 100 ? '#ef4444' : 'var(--text-primary)' }}>{(data.summary.max_max_matches ?? 0).toLocaleString()}</div>
                                    <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--text-secondary)' }}>avg: {(data.summary.avg_max_matches ?? 0).toFixed(1)}</div>
                                </div>
                                <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '20px', border: '1px solid var(--border)', textAlign: 'center' }}>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'none' }}>{t('insights.trend')}</div>
                                    <div style={{ fontSize: '36px' }}>{trendDirection === 'up' ? <TrendingUp size={36} color="#ef4444" /> : trendDirection === 'down' ? <TrendingDown size={36} color="#10b981" /> : <Minus size={36} color="var(--text-secondary)" />}</div>
                                    <div style={{ marginTop: '8px', fontSize: '14px', fontWeight: '600', color: trendDirection === 'up' ? '#ef4444' : trendDirection === 'down' ? '#10b981' : 'var(--text-secondary)' }}>{trendDirection === 'up' ? t('insights.increasing') : trendDirection === 'down' ? t('insights.decreasing') : t('insights.stable')}</div>
                                </div>
                            </div>

                            {/* Action Breakdown */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}><Target size={18} /> {t('insights.actionBreakdown')}</h3>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #ef4444' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#ef4444' }}>{data.summary.total_block_count ?? 0}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{t('insights.blocked')}</div>
                                    </div>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #8b5cf6' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#8b5cf6' }}>{data.summary.total_quarantine_count ?? 0}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{t('insights.quarantined')}</div>
                                    </div>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #10b981' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#10b981' }}>{data.summary.total_permit_count ?? 0}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{t('insights.permitted')}</div>
                                    </div>
                                    <div style={{ textAlign: 'center', padding: '16px', background: 'var(--surface)', borderRadius: '8px', borderLeft: '4px solid #f59e0b' }}>
                                        <div style={{ fontSize: '24px', fontWeight: '700', color: '#f59e0b' }}>{data.summary.total_released_count ?? 0}</div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{t('insights.released')}</div>
                                    </div>
                                </div>
                            </div>

                            {/* Risk Score Chart */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}><TrendingUp size={18} /> {t('insights.riskScoreTrend')} - {periodLabels[activePeriod]}</h3>
                                <Plot
                                    data={[{ x: data.daily_scores.map(d => d.date), y: data.daily_scores.map(d => d.daily_risk_score), type: 'scatter', mode: 'lines+markers', name: 'Daily Risk Score', line: { color: '#3b82f6', width: 3, shape: 'spline' }, marker: { size: 8, color: data.daily_scores.map(d => getRiskColor(d.daily_risk_score)) }, fill: 'tozeroy', fillcolor: 'rgba(59, 130, 246, 0.1)' }]}
                                    layout={{ ...plotlyLayout, height: 350, yaxis: { ...plotlyLayout.yaxis, title: 'Risk Score (0-100)', range: [0, 100] }, shapes: [{ type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 75, y1: 75, line: { color: '#dc2626', width: 1, dash: 'dash' } }, { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 50, y1: 50, line: { color: '#f59e0b', width: 1, dash: 'dash' } }, { type: 'line', x0: 0, x1: 1, xref: 'paper', y0: 25, y1: 25, line: { color: '#eab308', width: 1, dash: 'dash' } }], annotations: [{ x: 1.02, xref: 'paper', y: 75, text: 'Critical', showarrow: false, font: { size: 10, color: '#dc2626' } }, { x: 1.02, xref: 'paper', y: 50, text: 'High', showarrow: false, font: { size: 10, color: '#f59e0b' } }, { x: 1.02, xref: 'paper', y: 25, text: 'Medium', showarrow: false, font: { size: 10, color: '#eab308' } }] }}
                                    config={plotlyConfig}
                                    style={{ width: '100%', height: '350px' }}
                                />
                            </div>

                            {/* Incident & Matches Chart */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}><BarChart3 size={18} /> {t('insights.incidentsMatches')}</h3>
                                <Plot
                                    data={[{ x: data.daily_scores.map(d => d.date), y: data.daily_scores.map(d => d.incident_count), type: 'bar', name: 'Incidents', marker: { color: '#3b82f6', opacity: 0.8 } }, { x: data.daily_scores.map(d => d.date), y: data.daily_scores.map(d => d.max_max_matches), type: 'scatter', mode: 'lines+markers', name: 'Max Matches', yaxis: 'y2', line: { color: '#f59e0b', width: 2 }, marker: { size: 6 } }]}
                                    layout={{ ...plotlyLayout, height: 280, yaxis: { ...plotlyLayout.yaxis, title: 'Incident Count' }, yaxis2: { overlaying: 'y', side: 'right', title: 'Max Matches', gridcolor: 'transparent' } }}
                                    config={plotlyConfig}
                                    style={{ width: '100%', height: '280px' }}
                                />
                            </div>

                            {/* Detailed Table */}
                            <div style={{ background: 'var(--background)', borderRadius: '12px', padding: '24px', border: '1px solid var(--border)' }}>
                                <h3 style={{ margin: '0 0 16px', fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}><ClipboardList size={18} /> {t('insights.detailedHistory')}</h3>
                                <div style={{ overflowX: 'auto' }}>
                                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                                        <thead>
                                            <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                                <th style={{ textAlign: 'left', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>#</th>
                                                <th style={{ textAlign: 'left', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>{t('logs.time')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>{t('insights.score')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>{t('insights.incidents')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#ef4444', fontWeight: '600' }}>{t('insights.block')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#8b5cf6', fontWeight: '600' }}>{t('insights.quarantine')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#10b981', fontWeight: '600' }}>{t('insights.permit')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: '#f59e0b', fontWeight: '600' }}>{t('insights.released')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>{t('insights.maxMatches')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>{t('insights.avgMatches')}</th>
                                                <th style={{ textAlign: 'center', padding: '10px', color: 'var(--text-muted)', fontWeight: '600' }}>{t('insights.level')}</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {paginatedDailyScores.map((row, idx) => (
                                                <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                    <td style={{ padding: '10px', color: 'var(--text-secondary)' }}>{(currentPage - 1) * pageSize + idx + 1}</td>
                                                    <td style={{ padding: '10px', color: 'var(--text-primary)', fontWeight: '500' }}>{row.date}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center' }}><span style={{ fontWeight: '700', color: getRiskColor(row.daily_risk_score), fontSize: '14px' }}>{row.daily_risk_score.toFixed(1)}</span></td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: 'var(--text-secondary)' }}>{row.incident_count}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.block_count > 0 ? '#ef4444' : 'var(--text-muted)' }}>{row.block_count}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.quarantine_count > 0 ? '#8b5cf6' : 'var(--text-muted)' }}>{row.quarantine_count}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.permit_count > 0 ? '#10b981' : 'var(--text-muted)' }}>{row.permit_count}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.released_count > 0 ? '#f59e0b' : 'var(--text-muted)' }}>{row.released_count}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: row.max_max_matches > 100 ? '#ef4444' : 'var(--text-secondary)' }}>{row.max_max_matches.toLocaleString()}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center', color: 'var(--text-secondary)' }}>{row.avg_max_matches.toFixed(1)}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center' }}><span style={{ padding: '3px 10px', borderRadius: '12px', fontSize: '10px', fontWeight: '700', background: `${getRiskColor(row.daily_risk_score)}20`, color: getRiskColor(row.daily_risk_score) }}>{getRiskLevel(row.daily_risk_score)}</span></td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>

                                    {/* Pagination */}
                                    {data.daily_scores.length > pageSize && (
                                        <div style={{ marginTop: '16px' }}>
                                            <Pagination
                                                currentPage={currentPage}
                                                totalPages={totalPages}
                                                totalItems={data.daily_scores.length}
                                                pageSize={pageSize}
                                                onPageChange={setCurrentPage}
                                                showPageInput={true}
                                                showFirstLast={true}
                                                showTotalItems={true}
                                            />
                                        </div>
                                    )}
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </>
    )
}
