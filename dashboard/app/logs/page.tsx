'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'
import { getApiUrlDynamic } from '@/lib/api-config'

interface AuditLog {
  id: number
  timestamp: string
  eventType: string
  userName: string
  userRole?: string
  action: string
  resource?: string
  details?: string
  ipAddress?: string
  userAgent?: string
  success: boolean
  errorMessage?: string
  statusCode?: number
  durationMs?: number
}

interface AuditLogsResponse {
  logs: AuditLog[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export default function LogsPage() {
  const [activeTab, setActiveTab] = useState<'audit' | 'application'>('audit')
  const [auditLogs, setAuditLogs] = useState<AuditLog[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(100)
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  
  // Filters
  const [startDate, setStartDate] = useState<string>('')
  const [endDate, setEndDate] = useState<string>('')
  const [eventType, setEventType] = useState<string>('')
  const [userName, setUserName] = useState<string>('')
  const [eventTypes, setEventTypes] = useState<string[]>([])
  const [initialLoad, setInitialLoad] = useState(true)

  useEffect(() => {
    fetchEventTypes()
  }, [])

  useEffect(() => {
    // Reset initial load when tab changes
    setInitialLoad(true)
    // Clear logs when tab changes
    if (activeTab === 'audit') {
      setAuditLogs([])
      setTotal(0)
      setTotalPages(0)
    }
  }, [activeTab])
  
  useEffect(() => {
    // Fetch when pagination changes (but only if we already have data loaded)
    if (activeTab === 'audit' && !initialLoad && auditLogs.length > 0) {
      fetchAuditLogs()
    }
  }, [page, pageSize])

  const fetchEventTypes = async () => {
    try {
      const response = await apiClient.get('/api/logs/audit/event-types')
      setEventTypes(response.data)
    } catch (err: any) {
      console.error('Error fetching event types:', err)
    }
  }

  const fetchAuditLogs = async () => {
    setLoading(true)
    setError(null)
    try {
      const params: any = {
        page,
        pageSize
      }

      if (startDate) params.startDate = new Date(startDate).toISOString()
      if (endDate) params.endDate = new Date(endDate).toISOString()
      if (eventType) params.eventType = eventType
      if (userName) params.userName = userName

      const response = await apiClient.get<AuditLogsResponse>('/api/logs/audit', { params })
      if (response.data && response.data.logs) {
        setAuditLogs(response.data.logs)
        setTotal(response.data.total || 0)
        setTotalPages(response.data.totalPages || 0)
      } else {
        setAuditLogs([])
        setTotal(0)
        setTotalPages(0)
      }
    } catch (err: any) {
      console.error('Error fetching audit logs:', err)
      if (err.code === 'ECONNREFUSED' || err.message?.includes('Network Error') || err.message?.includes('Failed to fetch')) {
        setError('Network Error: Could not connect to the API server. Please ensure the backend is running on http://localhost:5001')
      } else if (err.response?.data?.detail) {
        setError(err.response.data.detail)
      } else if (err.response?.status) {
        setError(`Failed to fetch audit logs: ${err.response.status} ${err.response.statusText}`)
      } else {
        setError(err.message || 'Failed to fetch audit logs')
      }
    } finally {
      setLoading(false)
    }
  }

  const formatTimestamp = (timestamp: string) => {
    return new Date(timestamp).toLocaleString('en-US', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    })
  }

  const getEventTypeColor = (eventType: string) => {
    const colors: Record<string, string> = {
      'Login': '#10b981',
      'Logout': '#6366f1',
      'UserCreate': '#3b82f6',
      'UserUpdate': '#f59e0b',
      'UserDelete': '#ef4444',
      'SettingsChange': '#8b5cf6',
      'IncidentView': '#06b6d4',
      'ApiCall': '#64748b'
    }
    return colors[eventType] || '#64748b'
  }

  const clearFilters = () => {
    setStartDate('')
    setEndDate('')
    setEventType('')
    setUserName('')
    setPage(1)
    setInitialLoad(true)
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1600px', margin: '0 auto' }}>
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '28px', fontWeight: '700', color: 'var(--text-primary)', marginBottom: '8px' }}>
          Logs
        </h1>
        <p style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>
          View audit logs and application logs for security analysis
        </p>
      </div>

      {/* Tabs */}
      <div style={{ 
        display: 'flex', 
        gap: '8px', 
        marginBottom: '24px',
        borderBottom: '2px solid var(--border)'
      }}>
        <button
          onClick={() => setActiveTab('audit')}
          style={{
            padding: '12px 24px',
            background: activeTab === 'audit' ? 'var(--primary)' : 'transparent',
            color: activeTab === 'audit' ? 'white' : 'var(--text-primary)',
            border: 'none',
            borderBottom: activeTab === 'audit' ? '2px solid var(--primary)' : '2px solid transparent',
            cursor: 'pointer',
            fontWeight: '600',
            fontSize: '14px',
            transition: 'all 0.2s'
          }}
        >
          Audit Logs
        </button>
        <button
          onClick={() => setActiveTab('application')}
          style={{
            padding: '12px 24px',
            background: activeTab === 'application' ? 'var(--primary)' : 'transparent',
            color: activeTab === 'application' ? 'white' : 'var(--text-primary)',
            border: 'none',
            borderBottom: activeTab === 'application' ? '2px solid var(--primary)' : '2px solid transparent',
            cursor: 'pointer',
            fontWeight: '600',
            fontSize: '14px',
            transition: 'all 0.2s'
          }}
        >
          Application Logs
        </button>
      </div>

      {activeTab === 'audit' && (
        <div>
          {/* Filters */}
          <div style={{
            background: 'var(--surface)',
            padding: '20px',
            borderRadius: '12px',
            marginBottom: '24px',
            border: '1px solid var(--border)'
          }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '16px' }}>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>
                  Start Date
                </label>
                <input
                  type="datetime-local"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--surface)',
                    color: 'var(--text-primary)'
                  }}
                />
              </div>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>
                  End Date
                </label>
                <input
                  type="datetime-local"
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--surface)',
                    color: 'var(--text-primary)'
                  }}
                />
              </div>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>
                  Event Type
                </label>
                <select
                  value={eventType}
                  onChange={(e) => setEventType(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--surface)',
                    color: 'var(--text-primary)'
                  }}
                >
                  <option value="">All Types</option>
                  {eventTypes.map(type => (
                    <option key={type} value={type}>{type}</option>
                  ))}
                </select>
              </div>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>
                  User Name
                </label>
                <input
                  type="text"
                  value={userName}
                  onChange={(e) => setUserName(e.target.value)}
                  placeholder="Filter by user..."
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--surface)',
                    color: 'var(--text-primary)'
                  }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                onClick={clearFilters}
                style={{
                  padding: '8px 16px',
                  background: 'var(--surface)',
                  border: '1px solid var(--border)',
                  borderRadius: '6px',
                  cursor: 'pointer',
                  fontSize: '14px',
                  color: 'var(--text-primary)'
                }}
              >
                Clear Filters
              </button>
              <button
                onClick={() => {
                  setPage(1)
                  setInitialLoad(false)
                  fetchAuditLogs()
                }}
                disabled={loading}
                style={{
                  padding: '8px 20px',
                  background: 'var(--primary)',
                  color: 'white',
                  border: 'none',
                  borderRadius: '6px',
                  cursor: loading ? 'not-allowed' : 'pointer',
                  fontWeight: '600',
                  fontSize: '14px',
                  opacity: loading ? 0.6 : 1,
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px'
                }}
              >
                {loading ? (
                  'Searching...'
                ) : (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <circle cx="11" cy="11" r="8"></circle>
                      <path d="m21 21-4.35-4.35"></path>
                    </svg>
                    Search
                  </>
                )}
              </button>
            </div>
          </div>

          {/* Logs Table */}
          {loading && auditLogs.length === 0 && (
            <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
              Loading logs...
            </div>
          )}

          {error && (
            <div style={{
              padding: '16px',
              background: 'rgba(239, 68, 68, 0.1)',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              borderRadius: '8px',
              color: '#ef4444',
              marginBottom: '24px'
            }}>
              {error}
              {error.includes('Failed to fetch') && (
                <div style={{ marginTop: '8px', fontSize: '12px', opacity: 0.8, color: 'var(--text-secondary)' }}>
                  Note: Make sure the database migration has been run. Execute: dotnet ef database update
                </div>
              )}
            </div>
          )}

          {!loading && !error && (
            <>
              <div style={{
                background: 'var(--surface)',
                borderRadius: '12px',
                border: '1px solid var(--border)',
                overflow: 'hidden'
              }}>
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                    <thead>
                      <tr style={{ background: 'var(--surface)', borderBottom: '2px solid var(--border)' }}>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Timestamp</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Event Type</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>User</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Action</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Resource</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>IP Address</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Status</th>
                        <th style={{ padding: '12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Duration</th>
                      </tr>
                    </thead>
                    <tbody>
                      {auditLogs.length === 0 ? (
                        <tr>
                          <td colSpan={8} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                            No audit logs found
                          </td>
                        </tr>
                      ) : (
                        auditLogs.map((log) => (
                          <tr key={log.id} style={{ borderBottom: '1px solid var(--border)', transition: 'background 0.2s', background: 'transparent' }}
                              onMouseEnter={(e) => e.currentTarget.style.background = 'var(--surface-hover)'}
                              onMouseLeave={(e) => e.currentTarget.style.background = 'transparent'}>
                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                              {formatTimestamp(log.timestamp)}
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px' }}>
                              <span style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                background: getEventTypeColor(log.eventType) + '20',
                                color: getEventTypeColor(log.eventType),
                                fontSize: '12px',
                                fontWeight: '600'
                              }}>
                                {log.eventType}
                              </span>
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                              <div>
                                <div style={{ fontWeight: '500' }}>{log.userName}</div>
                                {log.userRole && (
                                  <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>{log.userRole}</div>
                                )}
                              </div>
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)', fontFamily: 'monospace' }}>
                              {log.action}
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                              {log.resource || '-'}
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)', fontFamily: 'monospace' }}>
                              {log.ipAddress || '-'}
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px' }}>
                              <span style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                background: log.success ? '#10b98120' : '#ef444420',
                                color: log.success ? '#10b981' : '#ef4444',
                                fontSize: '12px',
                                fontWeight: '600'
                              }}>
                                {log.success ? 'Success' : 'Failed'}
                              </span>
                              {log.statusCode && (
                                <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '4px' }}>
                                  {log.statusCode}
                                </div>
                              )}
                            </td>
                            <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                              {log.durationMs ? `${log.durationMs}ms` : '-'}
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Pagination */}
              <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginTop: '24px',
                padding: '16px',
                background: 'var(--surface)',
                borderRadius: '8px',
                border: '1px solid var(--border)'
              }}>
                <div style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
                  Showing {auditLogs.length > 0 ? (page - 1) * pageSize + 1 : 0} - {Math.min(page * pageSize, total)} of {total} logs
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <button
                    onClick={() => setPage(p => Math.max(1, p - 1))}
                    disabled={page === 1}
                    style={{
                      padding: '8px 16px',
                      background: page === 1 ? 'var(--surface)' : 'var(--primary)',
                      color: page === 1 ? 'var(--text-secondary)' : 'white',
                      border: 'none',
                      borderRadius: '6px',
                      cursor: page === 1 ? 'not-allowed' : 'pointer',
                      fontSize: '14px'
                    }}
                  >
                    Previous
                  </button>
                  <button
                    onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                    style={{
                      padding: '8px 16px',
                      background: page >= totalPages ? 'var(--surface)' : 'var(--primary)',
                      color: page >= totalPages ? 'var(--text-secondary)' : 'white',
                      border: 'none',
                      borderRadius: '6px',
                      cursor: page >= totalPages ? 'not-allowed' : 'pointer',
                      fontSize: '14px'
                    }}
                  >
                    Next
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      )}

      {activeTab === 'application' && (
        <div style={{
          background: 'var(--surface)',
          padding: '40px',
          borderRadius: '12px',
          border: '1px solid var(--border)',
          textAlign: 'center'
        }}>
          <div style={{ fontSize: '48px', marginBottom: '16px' }}>📋</div>
          <h2 style={{ fontSize: '20px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>
            Application Logs
          </h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '24px' }}>
            Application logs are automatically sent to Splunk when configured. 
            View logs in your Splunk instance or check log files on the server.
          </p>
          <div style={{
            padding: '16px',
            background: 'var(--surface)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            textAlign: 'left',
            maxWidth: '600px',
            margin: '0 auto'
          }}>
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '8px' }}>Splunk Configuration:</div>
            <div style={{ fontSize: '13px', color: 'var(--text-primary)', fontFamily: 'monospace' }}>
              <div>HEC URL: Configure in appsettings.json</div>
              <div>Index: dlp_risk_analyzer</div>
              <div>Sourcetype: dlp:application</div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

