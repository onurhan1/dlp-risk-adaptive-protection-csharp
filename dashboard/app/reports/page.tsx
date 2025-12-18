'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format, subDays } from 'date-fns'

import { getApiUrlDynamic } from '@/lib/api-config'
import ActionIncidentsModal from '@/components/ActionIncidentsModal'

interface ActionSummary {
  authorized: number
  block: number
  quarantine: number
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

interface Report {
  id: number
  report_type: string
  generated_at: string
  filename: string
  status: string
}

export default function ReportsPage() {
  const [reports, setReports] = useState<Report[]>([])
  const [dailySummary, setDailySummary] = useState<DailySummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [summaryLoading, setSummaryLoading] = useState(false)
  const [generating, setGenerating] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const [selectedDate, setSelectedDate] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [expandedPolicies, setExpandedPolicies] = useState<Set<string>>(new Set())

  // Modal state for action incidents
  const [showModal, setShowModal] = useState(false)
  const [selectedAction, setSelectedAction] = useState<string>('')
  const [actionIncidents, setActionIncidents] = useState<any[]>([])
  const [incidentsLoading, setIncidentsLoading] = useState(false)

  useEffect(() => {
    fetchReports()
  }, [])

  useEffect(() => {
    fetchDailySummary()
  }, [selectedDate])

  const fetchReports = async () => {
    setLoading(true)
    try {
      const token = localStorage.getItem('authToken')
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/reports`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      })
      setReports(response.data.reports || [])
    } catch (error: any) {
      console.error('Error fetching reports:', error)
    } finally {
      setLoading(false)
    }
  }

  const fetchDailySummary = async () => {
    setSummaryLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/reports/daily-summary`, {
        params: { date: selectedDate }
      })
      console.log('Daily Summary API Response:', response.data)
      console.log('Top Users Sample:', response.data.top_users?.[0])
      setDailySummary(response.data)
    } catch (error: any) {
      console.error('Error fetching daily summary:', error)
      setDailySummary(null)
    } finally {
      setSummaryLoading(false)
    }
  }

  const fetchActionIncidents = async (action: string) => {
    setIncidentsLoading(true)
    setShowModal(true)
    setSelectedAction(action)
    setActionIncidents([])

    try {
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/risk/incidents/by-action`, {
        params: {
          action: action,
          date: selectedDate
        }
      })
      setActionIncidents(response.data)
    } catch (error: any) {
      console.error('Error fetching action incidents:', error)
      setActionIncidents([])
    } finally {
      setIncidentsLoading(false)
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

  return (
    <div className="dashboard-page">
      <div className="dashboard-header">
        <div>
          <h1>Daily Summary Reports</h1>
          <p className="dashboard-subtitle">View and export daily security reports</p>
        </div>
      </div>

      {message && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '6px',
            marginBottom: '24px',
            background: message.type === 'success' ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
            color: message.type === 'success' ? '#5cb85c' : '#d9534f',
            border: `1px solid ${message.type === 'success' ? 'rgba(92, 184, 92, 0.3)' : 'rgba(217, 83, 79, 0.3)'}`
          }}
        >
          {message.text}
        </div>
      )}

      {/* Date Selection and Download */}
      <div className="card" style={{ marginBottom: '24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '16px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <label style={{ fontWeight: '600', color: 'var(--text-primary)' }}>Report Date:</label>
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              style={{
                padding: '10px 14px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px',
                background: 'var(--surface)',
                color: 'var(--text-primary)'
              }}
            />
          </div>
          <button
            onClick={downloadPdf}
            disabled={generating || summaryLoading}
            style={{
              padding: '12px 24px',
              background: 'var(--primary)',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: generating ? 'not-allowed' : 'pointer',
              fontWeight: '600',
              fontSize: '14px',
              opacity: generating ? 0.6 : 1,
              boxShadow: '0 2px 8px rgba(0, 168, 232, 0.3)',
              transition: 'all 0.2s'
            }}
          >
            {generating ? 'Generating PDF...' : '📥 Download PDF Report'}
          </button>
        </div>
      </div>

      {summaryLoading ? (
        <div className="card" style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
          Loading daily summary...
        </div>
      ) : !dailySummary ? (
        <div className="card" style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
          No data available for {selectedDate}
        </div>
      ) : (
        <>
          {/* Action Summary Cards */}
          <div className="card" style={{ marginBottom: '24px' }}>
            <h2>Action Summary</h2>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginTop: '16px' }}>
              <div style={{
                background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                padding: '20px',
                borderRadius: '8px',
                color: 'white'
              }}>
                <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>AUTHORIZED</div>
                <div style={{ fontSize: '32px', fontWeight: '700' }}>{dailySummary.action_summary.authorized}</div>
              </div>
              <div
                onClick={() => fetchActionIncidents('BLOCK')}
                style={{
                  background: 'linear-gradient(135deg, #ef4444 0%, #dc2626 100%)',
                  padding: '20px',
                  borderRadius: '8px',
                  color: 'white',
                  cursor: 'pointer',
                  transition: 'transform 0.2s, box-shadow 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateY(-2px)'
                  e.currentTarget.style.boxShadow = '0 4px 12px rgba(239, 68, 68, 0.4)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'translateY(0)'
                  e.currentTarget.style.boxShadow = 'none'
                }}
              >
                <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>BLOCK</div>
                <div style={{ fontSize: '32px', fontWeight: '700' }}>{dailySummary.action_summary.block}</div>
              </div>
              <div
                onClick={() => fetchActionIncidents('QUARANTINE')}
                style={{
                  background: 'linear-gradient(135deg, #9013ff 0%, #7d0962 100%)',
                  padding: '20px',
                  borderRadius: '8px',
                  color: 'white',
                  cursor: 'pointer',
                  transition: 'transform 0.2s, box-shadow 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateY(-2px)'
                  e.currentTarget.style.boxShadow = '0 4px 12px rgba(144, 19, 255, 0.4)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'translateY(0)'
                  e.currentTarget.style.boxShadow = 'none'
                }}
              >
                <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>QUARANTINE</div>
                <div style={{ fontSize: '32px', fontWeight: '700' }}>{dailySummary.action_summary.quarantine}</div>
              </div>
              <div style={{
                background: 'linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%)',
                padding: '20px',
                borderRadius: '8px',
                color: 'white'
              }}>
                <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>TOTAL</div>
                <div style={{ fontSize: '32px', fontWeight: '700' }}>{dailySummary.action_summary.total}</div>
              </div>
            </div>
          </div>

          {/* Two Column Grid */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '24px', marginBottom: '24px' }}>
            {/* Top 10 Users */}
            <div className="card">
              <h2>Top 10 Users</h2>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>User</th>
                    <th className="text-center">Risk</th>
                    <th className="text-right">Incidents</th>
                  </tr>
                </thead>
                <tbody>
                  {dailySummary.top_users.length === 0 ? (
                    <tr>
                      <td colSpan={4} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                        No data
                      </td>
                    </tr>
                  ) : (
                    dailySummary.top_users.map((user, idx) => (
                      <tr key={idx}>
                        <td>{idx + 1}</td>
                        <td>
                          <div style={{ display: 'flex', flexDirection: 'column' }}>
                            <span>{user.login_name || user.user_email}</span>
                            {user.department && (
                              <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{user.department}</span>
                            )}
                          </div>
                        </td>
                        <td className="text-center">
                          <span style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: '600',
                            color: 'white',
                            backgroundColor: getRiskColor(user.risk_score)
                          }}>
                            {user.risk_score}
                          </span>
                        </td>
                        <td className="text-right">{user.total_alerts}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Channel Breakdown */}
            <div className="card">
              <h2>Channel Breakdown</h2>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Channel</th>
                    <th className="text-right">Alerts</th>
                    <th className="text-right">Percentage</th>
                  </tr>
                </thead>
                <tbody>
                  {dailySummary.channel_breakdown.length === 0 ? (
                    <tr>
                      <td colSpan={3} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                        No data
                      </td>
                    </tr>
                  ) : (
                    dailySummary.channel_breakdown.map((channel, idx) => (
                      <tr key={idx}>
                        <td>{channel.channel}</td>
                        <td className="text-right">{channel.total_alerts}</td>
                        <td className="text-right">
                          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '12px' }}>
                            <span style={{ minWidth: '45px', textAlign: 'right' }}>{channel.percentage.toFixed(1)}%</span>
                            <div style={{
                              width: '100px',
                              height: '8px',
                              backgroundColor: 'var(--border)',
                              borderRadius: '4px',
                              overflow: 'hidden'
                            }}>
                              <div style={{
                                width: `${channel.percentage}%`,
                                height: '100%',
                                backgroundColor: 'var(--primary)',
                                borderRadius: '4px'
                              }} />
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

          {/* Top 10 Policies with Rules */}
          <div className="card" style={{ marginBottom: '24px' }}>
            <h2>Top 10 Policies with Top 3 Rules</h2>
            <div style={{ marginTop: '16px' }}>
              {dailySummary.top_policies.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                  No data
                </div>
              ) : (
                dailySummary.top_policies.map((policy, idx) => (
                  <div
                    key={idx}
                    style={{
                      border: '1px solid var(--border)',
                      borderRadius: '8px',
                      marginBottom: '8px',
                      overflow: 'hidden'
                    }}
                  >
                    <div
                      onClick={() => togglePolicy(policy.policy_name)}
                      style={{
                        padding: '12px 16px',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        cursor: 'pointer',
                        background: 'var(--surface-hover)',
                        transition: 'background 0.2s'
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                        <span style={{
                          fontSize: '16px',
                          transition: 'transform 0.2s',
                          transform: expandedPolicies.has(policy.policy_name) ? 'rotate(90deg)' : 'rotate(0deg)'
                        }}>
                          ▶
                        </span>
                        <span style={{ fontWeight: '600', color: 'var(--text-primary)' }}>
                          {policy.policy_name}
                        </span>
                      </div>
                      <span style={{
                        padding: '4px 12px',
                        backgroundColor: '#f57c00',
                        color: 'white',
                        borderRadius: '12px',
                        fontSize: '12px',
                        fontWeight: '600'
                      }}>
                        {policy.total_alerts} alerts
                      </span>
                    </div>
                    {expandedPolicies.has(policy.policy_name) && (
                      <div style={{ padding: '12px 16px 12px 48px', borderTop: '1px solid var(--border)' }}>
                        {policy.top_rules.length === 0 ? (
                          <div style={{ color: '#999', fontSize: '13px' }}>No rules available</div>
                        ) : (
                          policy.top_rules.map((rule, rIdx) => (
                            <div
                              key={rIdx}
                              style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                padding: '8px 0',
                                borderBottom: rIdx < policy.top_rules.length - 1 ? '1px solid var(--border)' : 'none'
                              }}
                            >
                              <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
                                • {rule.rule_name}
                              </span>
                              <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>
                                {rule.alert_count} alerts
                              </span>
                            </div>
                          ))
                        )}
                      </div>
                    )}
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Top 10 Destinations */}
          <div className="card">
            <h2>Top 10 Destinations</h2>
            <table className="data-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Destination</th>
                  <th className="text-right">Alerts</th>
                </tr>
              </thead>
              <tbody>
                {dailySummary.top_destinations.length === 0 ? (
                  <tr>
                    <td colSpan={3} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                      No data
                    </td>
                  </tr>
                ) : (
                  dailySummary.top_destinations.map((dest, idx) => (
                    <tr key={idx}>
                      <td>{idx + 1}</td>
                      <td>{dest.destination}</td>
                      <td className="text-right">{dest.total_alerts}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </>
      )}

      <style jsx>{`
        .dashboard-page {
          background: transparent;
          min-height: calc(100vh - 64px);
          padding: 0;
        }

        .dashboard-header {
          margin-bottom: 24px;
        }

        .dashboard-header h1 {
          font-size: 28px;
          font-weight: 700;
          color: var(--text-primary);
          margin: 0 0 8px 0;
          letter-spacing: -0.02em;
        }

        .dashboard-subtitle {
          font-size: 14px;
          color: var(--text-secondary);
          margin: 0;
        }

        .card {
          background: var(--surface);
          border-radius: 6px;
          padding: 24px;
          box-shadow: var(--shadow);
          border: 1px solid var(--border);
          transition: all 0.2s;
        }

        .card:hover {
          box-shadow: var(--shadow-md);
          border-color: var(--border-hover);
        }

        .card h2 {
          margin: 0 0 16px 0;
          color: var(--text-primary);
          font-size: 18px;
          font-weight: 600;
          letter-spacing: -0.02em;
        }

        .data-table {
          width: 100%;
          border-collapse: collapse;
        }

        .data-table th {
          background: var(--background-secondary);
          padding: 12px;
          text-align: left;
          font-size: 11px;
          font-weight: 700;
          color: var(--text-secondary);
          text-transform: uppercase;
          letter-spacing: 0.5px;
          border-bottom: 2px solid var(--border);
        }

        .data-table td {
          padding: 12px;
          border-bottom: 1px solid var(--border);
          font-size: 14px;
          color: var(--text-primary);
        }

        .data-table tr:hover {
          background: var(--surface-hover);
        }

        .text-right {
          text-align: right;
        }

        .text-center {
          text-align: center;
        }

        @media (max-width: 1024px) {
          .dashboard-page > div[style*="grid-template-columns: 1fr 1fr"] {
            grid-template-columns: 1fr !important;
          }
        }

        @media (max-width: 768px) {
          .card > div[style*="grid-template-columns: repeat(4"] {
            grid-template-columns: repeat(2, 1fr) !important;
          }
        }
        `}
      </style>

      {/* Action Incidents Modal */}
      <ActionIncidentsModal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        action={selectedAction}
        date={selectedDate}
        incidents={actionIncidents}
        loading={incidentsLoading}
      />
    </div>
  )
}
