'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format } from 'date-fns'
import { getApiUrlDynamic } from '@/lib/api-config'
import ActionIncidentsModal from './ActionIncidentsModal'

interface ActionSummary {
    authorized: number
    block: number
    quarantine: number
    released: number
    total: number
}

interface TopUser {
    user_email: string
    login_name: string
    email_address?: string
    department?: string
    total_alerts: number
    risk_score: number
    risk_level: string
}

interface TopPolicy {
    policy_name: string
    total_alerts: number
    top_rules: Array<{
        rule_name: string
        alert_count: number
    }>
}

interface ChannelBreakdown {
    channel: string
    total_alerts: number
    percentage: number
}

interface TopDestination {
    destination: string
    total_alerts: number
}

interface DailySummary {
    date: string
    action_summary: ActionSummary
    top_users: TopUser[]
    top_policies: TopPolicy[]
    channel_breakdown: ChannelBreakdown[]
    top_destinations: TopDestination[]
}

interface ReportModalProps {
    isOpen: boolean
    onClose: () => void
}

export default function ReportModal({ isOpen, onClose }: ReportModalProps) {
    const [dailySummary, setDailySummary] = useState<DailySummary | null>(null)
    const [loading, setLoading] = useState(false)
    const [generating, setGenerating] = useState(false)
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
    const [selectedDate, setSelectedDate] = useState(format(new Date(), 'yyyy-MM-dd'))
    const [expandedPolicies, setExpandedPolicies] = useState<Set<string>>(new Set())
    const [reportView, setReportView] = useState<'daily_summary' | 'risky_users'>('daily_summary')
    const [period, setPeriod] = useState<'weekly' | 'monthly' | 'quarterly'>('monthly')
    const [riskyUsers, setRiskyUsers] = useState<any[]>([])
    const [loadingRiskyUsers, setLoadingRiskyUsers] = useState(false)
    const [showActionModal, setShowActionModal] = useState(false)
    const [selectedAction, setSelectedAction] = useState<string>('')

    useEffect(() => {
        if (isOpen) {
            if (reportView === 'daily_summary') {
                fetchDailySummary()
            } else {
                fetchRiskyUsers()
            }
        }
    }, [isOpen, selectedDate, reportView, period])

    const fetchDailySummary = async () => {
        setLoading(true)
        try {
            const apiUrl = getApiUrlDynamic()
            const response = await axios.get(`${apiUrl}/api/reports/daily-summary`, {
                params: { date: selectedDate }
            })
            setDailySummary(response.data)
        } catch (error: any) {
            console.error('Error fetching daily summary:', error)
            setDailySummary(null)
        } finally {
            setLoading(false)
        }
    }

    const fetchRiskyUsers = async () => {
        setLoadingRiskyUsers(true)
        try {
            const apiUrl = getApiUrlDynamic()
            const response = await axios.get(`${apiUrl}/api/risk-trends/users/report`, {
                params: { period }
            })
            setRiskyUsers(response.data)
        } catch (error: any) {
            console.error('Error fetching risky users:', error)
            setRiskyUsers([])
        } finally {
            setLoadingRiskyUsers(false)
        }
    }

    const downloadPdf = async () => {
        setGenerating(true)
        setMessage(null)
        try {
            const token = localStorage.getItem('authToken')
            const apiUrl = getApiUrlDynamic()
            const response = await axios.get(`${apiUrl}/api/reports/daily-summary/pdf`, {
                params: { date: selectedDate },
                headers: token ? { Authorization: `Bearer ${token}` } : {},
                responseType: 'blob'
            })

            const blob = new Blob([response.data], { type: 'application/pdf' })
            const url = window.URL.createObjectURL(blob)
            const link = document.createElement('a')
            link.href = url
            link.download = `daily_summary_${selectedDate}.pdf`
            document.body.appendChild(link)
            link.click()
            link.remove()
            window.URL.revokeObjectURL(url)

            setMessage({ type: 'success', text: 'PDF downloaded successfully!' })
            setTimeout(() => setMessage(null), 5000)
        } catch (error: any) {
            setMessage({
                type: 'error',
                text: error.response?.data?.detail || 'Failed to download PDF'
            })
            setTimeout(() => setMessage(null), 5000)
        } finally {
            setGenerating(false)
        }
    }

    const togglePolicy = (policyName: string) => {
        const newExpanded = new Set(expandedPolicies)
        if (newExpanded.has(policyName)) {
            newExpanded.delete(policyName)
        } else {
            newExpanded.add(policyName)
        }
        setExpandedPolicies(newExpanded)
    }

    const getRiskColor = (score: number): string => {
        if (score >= 91) return '#d32f2f'
        if (score >= 61) return '#f57c00'
        if (score >= 41) return '#fbc02d'
        return '#4caf50'
    }

    const fetchActionIncidents = (action: string) => {
        setShowActionModal(true)
        setSelectedAction(action)
    }

    if (!isOpen) return null

    return (
        <>
            <div
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: 'rgba(0, 0, 0, 0.7)',
                    zIndex: 2000,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: '20px'
                }}
                onClick={onClose}
            >
                <div
                    style={{
                        background: 'var(--surface)',
                        borderRadius: '12px',
                        width: '95%',
                        maxWidth: '1400px',
                        maxHeight: '90vh',
                        overflow: 'auto',
                        boxShadow: '0 20px 60px rgba(0, 0, 0, 0.5)'
                    }}
                    onClick={(e) => e.stopPropagation()}
                >
                    {/* Header */}
                    <div style={{
                        padding: '20px 24px',
                        borderBottom: '1px solid var(--border)',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        position: 'sticky',
                        top: 0,
                        background: 'var(--surface)',
                        zIndex: 10
                    }}>
                        <div>
                            <h2 style={{ margin: 0, fontSize: '20px', fontWeight: 700 }}>
                                {reportView === 'daily_summary' ? '📅 Daily Summary Reports' : '📈 Risky Users Trend Report'}
                            </h2>
                            <p style={{ margin: '4px 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>
                                {reportView === 'daily_summary'
                                    ? 'View and export daily security reports'
                                    : 'Analyze user risk trends over time'}
                            </p>
                        </div>
                        <button
                            onClick={onClose}
                            style={{
                                background: 'none',
                                border: 'none',
                                fontSize: '24px',
                                cursor: 'pointer',
                                color: 'var(--text-secondary)',
                                padding: '4px 8px'
                            }}
                        >
                            ×
                        </button>
                    </div>

                    {/* Content */}
                    <div style={{ padding: '24px' }}>
                        {message && (
                            <div
                                style={{
                                    padding: '12px 16px',
                                    borderRadius: '6px',
                                    marginBottom: '16px',
                                    background: message.type === 'success' ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
                                    color: message.type === 'success' ? '#5cb85c' : '#d9534f',
                                    border: `1px solid ${message.type === 'success' ? 'rgba(92, 184, 92, 0.3)' : 'rgba(217, 83, 79, 0.3)'}`
                                }}
                            >
                                {message.text}
                            </div>
                        )}

                        {/* Report Type Tabs */}
                        <div style={{ display: 'flex', gap: '12px', marginBottom: '24px' }}>
                            <button
                                onClick={() => setReportView('daily_summary')}
                                style={{
                                    padding: '10px 20px',
                                    borderRadius: '8px',
                                    border: 'none',
                                    fontSize: '14px',
                                    fontWeight: '600',
                                    cursor: 'pointer',
                                    background: reportView === 'daily_summary' ? 'var(--primary)' : 'var(--background)',
                                    color: reportView === 'daily_summary' ? 'white' : 'var(--text-secondary)'
                                }}
                            >
                                📅 Daily Summary
                            </button>
                            <button
                                onClick={() => setReportView('risky_users')}
                                style={{
                                    padding: '10px 20px',
                                    borderRadius: '8px',
                                    border: 'none',
                                    fontSize: '14px',
                                    fontWeight: '600',
                                    cursor: 'pointer',
                                    background: reportView === 'risky_users' ? 'var(--primary)' : 'var(--background)',
                                    color: reportView === 'risky_users' ? 'white' : 'var(--text-secondary)'
                                }}
                            >
                                📈 Risky Users Trends
                            </button>
                        </div>

                        {/* Filters */}
                        <div style={{
                            background: 'var(--background)',
                            padding: '16px',
                            borderRadius: '8px',
                            marginBottom: '24px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            flexWrap: 'wrap',
                            gap: '16px'
                        }}>
                            {reportView === 'daily_summary' ? (
                                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                    <label style={{ fontWeight: '600', color: 'var(--text-primary)' }}>Report Date:</label>
                                    <input
                                        type="date"
                                        value={selectedDate}
                                        onChange={(e) => setSelectedDate(e.target.value)}
                                        style={{
                                            padding: '8px 12px',
                                            border: '1px solid var(--border)',
                                            borderRadius: '6px',
                                            fontSize: '14px',
                                            background: 'var(--surface)',
                                            color: 'var(--text-primary)'
                                        }}
                                    />
                                </div>
                            ) : (
                                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                    <label style={{ fontWeight: '600', color: 'var(--text-primary)' }}>Analysis Period:</label>
                                    <div style={{ display: 'flex', background: 'var(--surface)', padding: '4px', borderRadius: '6px' }}>
                                        {(['weekly', 'monthly', 'quarterly'] as const).map((p) => (
                                            <button
                                                key={p}
                                                onClick={() => setPeriod(p)}
                                                style={{
                                                    padding: '6px 16px',
                                                    borderRadius: '4px',
                                                    border: 'none',
                                                    fontSize: '13px',
                                                    fontWeight: '500',
                                                    cursor: 'pointer',
                                                    background: period === p ? 'var(--primary)' : 'transparent',
                                                    color: period === p ? 'white' : 'var(--text-secondary)',
                                                    textTransform: 'capitalize'
                                                }}
                                            >
                                                {p}
                                            </button>
                                        ))}
                                    </div>
                                </div>
                            )}

                            {reportView === 'daily_summary' && (
                                <button
                                    onClick={downloadPdf}
                                    disabled={generating || loading}
                                    style={{
                                        padding: '10px 20px',
                                        background: 'var(--primary)',
                                        color: 'white',
                                        border: 'none',
                                        borderRadius: '6px',
                                        cursor: generating ? 'not-allowed' : 'pointer',
                                        fontWeight: '600',
                                        fontSize: '14px',
                                        opacity: generating ? 0.6 : 1
                                    }}
                                >
                                    {generating ? 'Generating PDF...' : '📥 Download PDF Report'}
                                </button>
                            )}
                        </div>

                        {/* Daily Summary Content */}
                        {reportView === 'daily_summary' ? (
                            loading ? (
                                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                                    Loading daily summary...
                                </div>
                            ) : !dailySummary ? (
                                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                                    No data available for {selectedDate}
                                </div>
                            ) : (
                                <>
                                    {/* Action Summary Cards */}
                                    <div style={{ marginBottom: '24px' }}>
                                        <h3 style={{ marginBottom: '16px', fontSize: '16px', fontWeight: 600 }}>Action Summary</h3>
                                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '12px' }}>
                                            {[
                                                { key: 'AUTHORIZED', value: dailySummary.action_summary.authorized, color: '#10b981' },
                                                { key: 'BLOCK', value: dailySummary.action_summary.block, color: '#ef4444' },
                                                { key: 'QUARANTINE', value: dailySummary.action_summary.quarantine, color: '#9013ff' },
                                                { key: 'RELEASED', value: dailySummary.action_summary.released || 0, color: '#f59e0b' },
                                                { key: 'TOTAL', value: dailySummary.action_summary.total, color: '#3b82f6' }
                                            ].map((item) => (
                                                <div
                                                    key={item.key}
                                                    onClick={() => fetchActionIncidents(item.key)}
                                                    style={{
                                                        background: item.color,
                                                        padding: '16px',
                                                        borderRadius: '8px',
                                                        color: 'white',
                                                        cursor: 'pointer',
                                                        transition: 'transform 0.2s'
                                                    }}
                                                >
                                                    <div style={{ fontSize: '11px', opacity: 0.9, marginBottom: '4px' }}>{item.key}</div>
                                                    <div style={{ fontSize: '28px', fontWeight: '700' }}>{item.value}</div>
                                                </div>
                                            ))}
                                        </div>
                                    </div>

                                    {/* Two Column Grid */}
                                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '24px' }}>
                                        {/* Top 10 Users */}
                                        <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px' }}>
                                            <h3 style={{ marginBottom: '12px', fontSize: '15px', fontWeight: 600 }}>Top 10 Users</h3>
                                            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                                                <thead>
                                                    <tr style={{ borderBottom: '1px solid var(--border)' }}>
                                                        <th style={{ padding: '8px', textAlign: 'left' }}>#</th>
                                                        <th style={{ padding: '8px', textAlign: 'left' }}>User</th>
                                                        <th style={{ padding: '8px', textAlign: 'center' }}>Risk</th>
                                                        <th style={{ padding: '8px', textAlign: 'right' }}>Incidents</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {dailySummary.top_users.length === 0 ? (
                                                        <tr><td colSpan={4} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>No data</td></tr>
                                                    ) : (
                                                        dailySummary.top_users.map((user, idx) => (
                                                            <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                                <td style={{ padding: '8px' }}>{idx + 1}</td>
                                                                <td style={{ padding: '8px' }}>{user.login_name || user.user_email}</td>
                                                                <td style={{ padding: '8px', textAlign: 'center' }}>
                                                                    <span style={{
                                                                        padding: '2px 8px',
                                                                        borderRadius: '10px',
                                                                        fontSize: '11px',
                                                                        fontWeight: '600',
                                                                        color: 'white',
                                                                        backgroundColor: getRiskColor(user.risk_score)
                                                                    }}>
                                                                        {user.risk_score}
                                                                    </span>
                                                                </td>
                                                                <td style={{ padding: '8px', textAlign: 'right' }}>{user.total_alerts}</td>
                                                            </tr>
                                                        ))
                                                    )}
                                                </tbody>
                                            </table>
                                        </div>

                                        {/* Channel Breakdown */}
                                        <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px' }}>
                                            <h3 style={{ marginBottom: '12px', fontSize: '15px', fontWeight: 600 }}>Channel Breakdown</h3>
                                            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                                                <thead>
                                                    <tr style={{ borderBottom: '1px solid var(--border)' }}>
                                                        <th style={{ padding: '8px', textAlign: 'left' }}>Channel</th>
                                                        <th style={{ padding: '8px', textAlign: 'right' }}>Alerts</th>
                                                        <th style={{ padding: '8px', textAlign: 'right' }}>Percentage</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {dailySummary.channel_breakdown.length === 0 ? (
                                                        <tr><td colSpan={3} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>No data</td></tr>
                                                    ) : (
                                                        dailySummary.channel_breakdown.map((channel, idx) => (
                                                            <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                                <td style={{ padding: '8px' }}>{channel.channel}</td>
                                                                <td style={{ padding: '8px', textAlign: 'right' }}>{channel.total_alerts}</td>
                                                                <td style={{ padding: '8px', textAlign: 'right' }}>
                                                                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '8px' }}>
                                                                        <span>{channel.percentage.toFixed(1)}%</span>
                                                                        <div style={{ width: '60px', height: '6px', backgroundColor: 'var(--border)', borderRadius: '3px', overflow: 'hidden' }}>
                                                                            <div style={{ width: `${channel.percentage}%`, height: '100%', backgroundColor: 'var(--primary)', borderRadius: '3px' }} />
                                                                        </div>
                                                                    </div>
                                                                </td>
                                                            </tr>
                                                        ))
                                                    )}
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>

                                    {/* Top Policies */}
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px', marginBottom: '24px' }}>
                                        <h3 style={{ marginBottom: '12px', fontSize: '15px', fontWeight: 600 }}>Top 10 Policies</h3>
                                        {dailySummary.top_policies.length === 0 ? (
                                            <div style={{ textAlign: 'center', padding: '20px', color: '#999' }}>No data</div>
                                        ) : (
                                            dailySummary.top_policies.map((policy, idx) => (
                                                <div key={idx} style={{ border: '1px solid var(--border)', borderRadius: '6px', marginBottom: '8px' }}>
                                                    <div
                                                        onClick={() => togglePolicy(policy.policy_name)}
                                                        style={{
                                                            padding: '10px 14px',
                                                            display: 'flex',
                                                            justifyContent: 'space-between',
                                                            alignItems: 'center',
                                                            cursor: 'pointer',
                                                            background: 'var(--surface)'
                                                        }}
                                                    >
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                                            <span style={{ transform: expandedPolicies.has(policy.policy_name) ? 'rotate(90deg)' : 'rotate(0deg)', transition: 'transform 0.2s' }}>▶</span>
                                                            <span style={{ fontWeight: '500', fontSize: '13px' }}>{policy.policy_name}</span>
                                                        </div>
                                                        <span style={{ padding: '3px 10px', backgroundColor: '#f57c00', color: 'white', borderRadius: '10px', fontSize: '12px', fontWeight: '600' }}>
                                                            {policy.total_alerts} alerts
                                                        </span>
                                                    </div>
                                                    {expandedPolicies.has(policy.policy_name) && (
                                                        <div style={{ padding: '10px 14px 10px 40px', borderTop: '1px solid var(--border)', fontSize: '13px' }}>
                                                            {policy.top_rules.length === 0 ? (
                                                                <div style={{ color: '#999' }}>No rules available</div>
                                                            ) : (
                                                                policy.top_rules.map((rule, rIdx) => (
                                                                    <div key={rIdx} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: rIdx < policy.top_rules.length - 1 ? '1px solid var(--border)' : 'none' }}>
                                                                        <span style={{ color: 'var(--text-secondary)' }}>• {rule.rule_name}</span>
                                                                        <span style={{ color: 'var(--text-muted)' }}>{rule.alert_count} alerts</span>
                                                                    </div>
                                                                ))
                                                            )}
                                                        </div>
                                                    )}
                                                </div>
                                            ))
                                        )}
                                    </div>

                                    {/* Top Destinations */}
                                    <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px' }}>
                                        <h3 style={{ marginBottom: '12px', fontSize: '15px', fontWeight: 600 }}>Top 10 Destinations</h3>
                                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                                            <thead>
                                                <tr style={{ borderBottom: '1px solid var(--border)' }}>
                                                    <th style={{ padding: '8px', textAlign: 'left' }}>#</th>
                                                    <th style={{ padding: '8px', textAlign: 'left' }}>Destination</th>
                                                    <th style={{ padding: '8px', textAlign: 'right' }}>Alerts</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {dailySummary.top_destinations.length === 0 ? (
                                                    <tr><td colSpan={3} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>No data</td></tr>
                                                ) : (
                                                    dailySummary.top_destinations.map((dest, idx) => (
                                                        <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                            <td style={{ padding: '8px' }}>{idx + 1}</td>
                                                            <td style={{ padding: '8px' }}>{dest.destination}</td>
                                                            <td style={{ padding: '8px', textAlign: 'right' }}>{dest.total_alerts}</td>
                                                        </tr>
                                                    ))
                                                )}
                                            </tbody>
                                        </table>
                                    </div>
                                </>
                            )
                        ) : (
                            // Risky Users Trends
                            loadingRiskyUsers ? (
                                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                                    Loading trend report...
                                </div>
                            ) : riskyUsers.length === 0 ? (
                                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                                    No risky users found in this period.
                                </div>
                            ) : (
                                <div style={{ background: 'var(--background)', borderRadius: '8px', padding: '16px' }}>
                                    <h3 style={{ marginBottom: '16px', fontSize: '15px', fontWeight: 600 }}>Risky Users Trends ({period})</h3>
                                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                                        <thead>
                                            <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                                <th style={{ padding: '10px', textAlign: 'left' }}>Rank</th>
                                                <th style={{ padding: '10px', textAlign: 'left' }}>User</th>
                                                <th style={{ padding: '10px', textAlign: 'right' }}>Current Risk</th>
                                                <th style={{ padding: '10px', textAlign: 'right' }}>Avg Risk</th>
                                                <th style={{ padding: '10px', textAlign: 'right' }}>Max Risk</th>
                                                <th style={{ padding: '10px', textAlign: 'right' }}>Incidents</th>
                                                <th style={{ padding: '10px', textAlign: 'center' }}>Trend</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {riskyUsers.map((user, idx) => (
                                                <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                                    <td style={{ padding: '10px' }}>{idx + 1}</td>
                                                    <td style={{ padding: '10px', fontWeight: '500' }}>{user.user_email}</td>
                                                    <td style={{ padding: '10px', textAlign: 'right' }}>
                                                        <span style={{
                                                            padding: '2px 8px',
                                                            borderRadius: '10px',
                                                            fontSize: '11px',
                                                            fontWeight: '600',
                                                            color: 'white',
                                                            backgroundColor: getRiskColor(user.current_score)
                                                        }}>
                                                            {user.current_score?.toFixed(0) || 0}
                                                        </span>
                                                    </td>
                                                    <td style={{ padding: '10px', textAlign: 'right' }}>{user.avg_score?.toFixed(1) || 0}</td>
                                                    <td style={{ padding: '10px', textAlign: 'right' }}>{user.max_score?.toFixed(0) || 0}</td>
                                                    <td style={{ padding: '10px', textAlign: 'right' }}>{user.incident_count || 0}</td>
                                                    <td style={{ padding: '10px', textAlign: 'center' }}>
                                                        <span style={{
                                                            color: user.trend_change > 0 ? '#ef4444' : user.trend_change < 0 ? '#10b981' : '#6b7280',
                                                            fontWeight: '600'
                                                        }}>
                                                            {user.trend_change > 0 ? '↗' : user.trend_change < 0 ? '↘' : '➡'} {Math.abs(user.trend_change || 0).toFixed(0)}
                                                        </span>
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )
                        )}
                    </div>
                </div>
            </div>

            {/* Action Incidents Modal */}
            <ActionIncidentsModal
                isOpen={showActionModal}
                onClose={() => setShowActionModal(false)}
                action={selectedAction}
                initialDate={selectedDate}
            />
        </>
    )
}
