'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'

interface AuditLog {
    id: number
    timestamp: string
    eventType: string
    userName: string
    userRole?: string
    action: string
    resource?: string
    ipAddress?: string
    success: boolean
    statusCode?: number
    durationMs?: number
}

interface ApplicationLog {
    timestamp: string
    level: string
    category: string
    message: string
    exception?: string
}

export default function LogsTab() {
    const [activeTab, setActiveTab] = useState<'audit' | 'application'>('audit')
    const [auditLogs, setAuditLogs] = useState<AuditLog[]>([])
    const [applicationLogs, setApplicationLogs] = useState<ApplicationLog[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [page, setPage] = useState(1)
    const [pageSize] = useState(50)
    const [total, setTotal] = useState(0)
    const [totalPages, setTotalPages] = useState(0)
    const [startDate, setStartDate] = useState('')
    const [endDate, setEndDate] = useState('')
    const [eventType, setEventType] = useState('')
    const [eventTypes, setEventTypes] = useState<string[]>([])
    const [appLevel, setAppLevel] = useState('')

    useEffect(() => {
        fetchEventTypes()
    }, [])

    const fetchEventTypes = async () => {
        try {
            const response = await apiClient.get('/api/logs/audit/event-types')
            setEventTypes(response.data)
        } catch (err) {
            console.error('Error fetching event types:', err)
        }
    }

    const fetchAuditLogs = async () => {
        setLoading(true)
        setError(null)
        try {
            const params: any = { page, pageSize }
            if (startDate) params.startDate = new Date(startDate).toISOString()
            if (endDate) params.endDate = new Date(endDate).toISOString()
            if (eventType) params.eventType = eventType

            const response = await apiClient.get('/api/logs/audit', { params })
            setAuditLogs(response.data?.logs || [])
            setTotal(response.data?.total || 0)
            setTotalPages(response.data?.totalPages || 0)
        } catch (err: any) {
            setError(err.response?.data?.detail || 'Failed to fetch audit logs')
        } finally {
            setLoading(false)
        }
    }

    const fetchApplicationLogs = async () => {
        setLoading(true)
        setError(null)
        try {
            const params: any = { page, pageSize }
            if (startDate) params.startDate = new Date(startDate).toISOString()
            if (endDate) params.endDate = new Date(endDate).toISOString()
            if (appLevel) params.level = appLevel

            const response = await apiClient.get('/api/logs/application', { params })
            setApplicationLogs(response.data?.logs || [])
            setTotal(response.data?.total || 0)
            setTotalPages(response.data?.totalPages || 0)
        } catch (err: any) {
            setError(err.response?.data?.detail || 'Failed to fetch application logs')
        } finally {
            setLoading(false)
        }
    }

    const formatTimestamp = (ts: string) => new Date(ts).toLocaleString('en-US', { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })

    const getEventColor = (type: string) => {
        const colors: Record<string, string> = { Login: '#10b981', Logout: '#6366f1', UserCreate: '#3b82f6', UserUpdate: '#f59e0b', UserDelete: '#ef4444' }
        return colors[type] || '#64748b'
    }

    const getLevelColor = (level: string) => {
        const colors: Record<string, string> = { Information: '#10b981', Warning: '#f59e0b', Error: '#ef4444', Critical: '#dc2626', Debug: '#6366f1' }
        return colors[level] || '#64748b'
    }

    const clearFilters = () => {
        setStartDate('')
        setEndDate('')
        setEventType('')
        setAppLevel('')
        setPage(1)
        if (activeTab === 'audit') setAuditLogs([])
        else setApplicationLogs([])
    }

    const handleSearch = () => {
        setPage(1)
        if (activeTab === 'audit') fetchAuditLogs()
        else fetchApplicationLogs()
    }

    const inputStyle = { padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '13px', background: 'var(--surface)', color: 'var(--text-primary)' }

    return (
        <div>
            <div style={{ marginBottom: '20px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>System Logs</h3>
                <p style={{ margin: '4px 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>View audit and application logs</p>
            </div>

            {/* Sub Tabs */}
            <div style={{ display: 'flex', gap: '8px', marginBottom: '20px', borderBottom: '2px solid var(--border)', paddingBottom: '2px' }}>
                <button onClick={() => { setActiveTab('audit'); setAuditLogs([]); setTotal(0) }} style={{
                    padding: '8px 16px', background: activeTab === 'audit' ? 'var(--primary)' : 'transparent',
                    color: activeTab === 'audit' ? 'white' : 'var(--text-primary)', border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer', fontWeight: 600, fontSize: '13px'
                }}>Audit Logs</button>
                <button onClick={() => { setActiveTab('application'); setApplicationLogs([]); setTotal(0) }} style={{
                    padding: '8px 16px', background: activeTab === 'application' ? 'var(--primary)' : 'transparent',
                    color: activeTab === 'application' ? 'white' : 'var(--text-primary)', border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer', fontWeight: 600, fontSize: '13px'
                }}>Application Logs</button>
            </div>

            {/* Filters */}
            <div style={{ background: 'var(--background)', padding: '16px', borderRadius: '8px', marginBottom: '16px' }}>
                <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: '12px' }}>
                    <div>
                        <label style={{ display: 'block', marginBottom: '4px', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Start Date</label>
                        <input type="datetime-local" value={startDate} onChange={(e) => setStartDate(e.target.value)} style={inputStyle} />
                    </div>
                    <div>
                        <label style={{ display: 'block', marginBottom: '4px', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>End Date</label>
                        <input type="datetime-local" value={endDate} onChange={(e) => setEndDate(e.target.value)} style={inputStyle} />
                    </div>
                    {activeTab === 'audit' && (
                        <div>
                            <label style={{ display: 'block', marginBottom: '4px', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Event Type</label>
                            <select value={eventType} onChange={(e) => setEventType(e.target.value)} style={{ ...inputStyle, minWidth: '150px' }}>
                                <option value="">All Types</option>
                                {eventTypes.map(t => <option key={t} value={t}>{t}</option>)}
                            </select>
                        </div>
                    )}
                    {activeTab === 'application' && (
                        <div>
                            <label style={{ display: 'block', marginBottom: '4px', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Level</label>
                            <select value={appLevel} onChange={(e) => setAppLevel(e.target.value)} style={{ ...inputStyle, minWidth: '150px' }}>
                                <option value="">All Levels</option>
                                <option value="Information">Information</option>
                                <option value="Warning">Warning</option>
                                <option value="Error">Error</option>
                                <option value="Critical">Critical</option>
                            </select>
                        </div>
                    )}
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <button onClick={clearFilters} style={{ padding: '8px 16px', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', fontSize: '13px' }}>Clear</button>
                    <button onClick={handleSearch} disabled={loading} style={{ padding: '8px 20px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: loading ? 'not-allowed' : 'pointer', fontWeight: 600, fontSize: '13px' }}>
                        {loading ? 'Searching...' : '🔍 Search'}
                    </button>
                </div>
            </div>

            {error && <div style={{ padding: '12px', background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.3)', borderRadius: '6px', color: '#ef4444', marginBottom: '16px' }}>{error}</div>}

            {/* Tables */}
            <div style={{ background: 'var(--background)', borderRadius: '8px', overflow: 'hidden' }}>
                {activeTab === 'audit' ? (
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                        <thead>
                            <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Time</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Type</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>User</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Action</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Status</th>
                            </tr>
                        </thead>
                        <tbody>
                            {auditLogs.length === 0 ? (
                                <tr><td colSpan={5} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>No audit logs. Click Search to load.</td></tr>
                            ) : auditLogs.map(log => (
                                <tr key={log.id} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '10px' }}>{formatTimestamp(log.timestamp)}</td>
                                    <td style={{ padding: '10px' }}><span style={{ padding: '3px 8px', borderRadius: '4px', background: getEventColor(log.eventType) + '20', color: getEventColor(log.eventType), fontSize: '11px', fontWeight: 600 }}>{log.eventType}</span></td>
                                    <td style={{ padding: '10px', fontWeight: 500 }}>{log.userName}</td>
                                    <td style={{ padding: '10px', fontFamily: 'monospace', fontSize: '12px' }}>{log.action}</td>
                                    <td style={{ padding: '10px' }}><span style={{ padding: '3px 8px', borderRadius: '4px', background: log.success ? '#10b98120' : '#ef444420', color: log.success ? '#10b981' : '#ef4444', fontSize: '11px', fontWeight: 600 }}>{log.success ? 'OK' : 'Failed'}</span></td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                ) : (
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                        <thead>
                            <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Time</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Level</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Category</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'none' }}>Message</th>
                            </tr>
                        </thead>
                        <tbody>
                            {applicationLogs.length === 0 ? (
                                <tr><td colSpan={4} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>No application logs. Click Search to load.</td></tr>
                            ) : applicationLogs.map((log, idx) => (
                                <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '10px' }}>{formatTimestamp(log.timestamp)}</td>
                                    <td style={{ padding: '10px' }}><span style={{ padding: '3px 8px', borderRadius: '4px', background: getLevelColor(log.level) + '20', color: getLevelColor(log.level), fontSize: '11px', fontWeight: 600 }}>{log.level}</span></td>
                                    <td style={{ padding: '10px', fontSize: '12px' }}>{log.category}</td>
                                    <td style={{ padding: '10px', maxWidth: '400px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={log.message}>{log.message}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', padding: '12px', background: 'var(--surface)', borderRadius: '6px' }}>
                    <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Page {page} of {totalPages} ({total} total)</span>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} style={{ padding: '6px 12px', border: '1px solid var(--border)', borderRadius: '4px', cursor: page === 1 ? 'not-allowed' : 'pointer', fontSize: '13px' }}>Prev</button>
                        <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page >= totalPages} style={{ padding: '6px 12px', border: '1px solid var(--border)', borderRadius: '4px', cursor: page >= totalPages ? 'not-allowed' : 'pointer', fontSize: '13px' }}>Next</button>
                    </div>
                </div>
            )}
        </div>
    )
}
