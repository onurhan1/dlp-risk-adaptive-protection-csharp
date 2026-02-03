'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import dynamic from 'next/dynamic'
import axios from 'axios'
import { format, subDays } from 'date-fns'
import ChannelActivity from '../components/ChannelActivity'
import RiskLevelBadge from '../components/RiskLevelBadge'
import ActionIncidentsModal from '../components/ActionIncidentsModal'
import HighRiskUsersModal from '../components/HighRiskUsersModal'
import ReportModal from '../components/ReportModal'

const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

import { getApiUrlDynamic } from '@/lib/api-config'

interface DailySummary {
  date?: string
  Date?: string
  total_incidents?: number
  TotalIncidents?: number
  totalIncidents?: number  // camelCase from .NET
  high_risk_count?: number
  HighRiskCount?: number
  highRiskCount?: number  // camelCase from .NET
  avg_risk_score?: number
  AvgRiskScore?: number
  avgRiskScore?: number  // camelCase from .NET
  unique_users?: number
  UniqueUsers?: number
  uniqueUsers?: number  // camelCase from .NET
  departments_affected?: number
  DepartmentsAffected?: number
  departmentsAffected?: number  // camelCase from .NET
}

interface DepartmentSummary {
  department: string
  total_incidents: number
  high_risk_count: number
  avg_risk_score: number
  unique_users: number
}

interface TopRule {
  rule_name: string
  total_alerts: number
}

interface TopUser {
  user_email: string
  full_name?: string
  team?: string
  total_incidents?: number
  total_alerts?: number
  risk_score: number
  avg_daily_score?: number
  max_daily_score?: number
  total_blocks?: number
  total_quarantines?: number
  days_with_activity?: number
  period?: string
}

interface IncidentDetail {
  file_name: string
  destination: string
  channel: string
  action: string
  policy: string
  max_matches: number
  timestamp: string
}

interface HighImpactAlert {
  user_email: string
  full_name?: string
  team?: string
  impact_score: number
  max_max_matches: number
  highest_risk_date: string
  daily_risk_score: number
  incident_count: number
  block_count: number
  quarantine_count: number
  days_with_activity: number
  total_incidents_in_period: number
  is_single_day_event: boolean
  severity_level: string
  incident_details: IncidentDetail[]
}

interface HighImpactAlertsResponse {
  data: HighImpactAlert[]
  pagination: {
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
  }
}

interface ActionSummary {
  authorized: number
  block: number
  quarantine: number
  released: number
  unknown: number
  total: number
}

export default function Home() {
  const router = useRouter()
  const [dailySummary, setDailySummary] = useState<DailySummary[]>([])
  const [deptSummary, setDeptSummary] = useState<DepartmentSummary[]>([])
  const [topRules, setTopRules] = useState<TopRule[]>([])
  const [topUsers24h, setTopUsers24h] = useState<TopUser[]>([])
  const [topUsersPeriod, setTopUsersPeriod] = useState<TopUser[]>([])
  const [highImpactAlerts, setHighImpactAlerts] = useState<HighImpactAlert[]>([])
  const [highImpactPagination, setHighImpactPagination] = useState({ page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
  const [expandedAlerts, setExpandedAlerts] = useState<Set<string>>(new Set())
  const [selectedPeriod, setSelectedPeriod] = useState<string>('quarterly')
  // Pagination for Top Risky Users tables
  const [topUsersPeriodPage, setTopUsersPeriodPage] = useState(1)
  const [topUsers24hPage, setTopUsers24hPage] = useState(1)
  const usersPerPage = 10
  const [actionSummary, setActionSummary] = useState<ActionSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [dailySummaryLoading, setDailySummaryLoading] = useState(true)
  const [selectedDimension, setSelectedDimension] = useState('department')
  const [dateRange, setDateRange] = useState({
    start: format(subDays(new Date(), 30), 'yyyy-MM-dd'),
    end: format(new Date(), 'yyyy-MM-dd')
  })

  // Independent date range for Daily Trends Chart - defaulted to roughly "All Time" (from 2023)
  const [trendsDateRange, setTrendsDateRange] = useState({
    start: '2023-01-01',
    end: format(new Date(), 'yyyy-MM-dd')
  })

  // Modal state for action incidents
  const [showModal, setShowModal] = useState(false)
  const [selectedAction, setSelectedAction] = useState<string>('')

  // Modal state for high-risk users
  const [showHighRiskModal, setShowHighRiskModal] = useState(false)

  // Modal state for Report
  const [showReportModal, setShowReportModal] = useState(false)
  const [selectedHighRiskDate, setSelectedHighRiskDate] = useState<string>('')

  useEffect(() => {
    fetchData()
  }, [selectedDimension, dateRange.start, dateRange.end, selectedPeriod])

  // Separate effect for Daily Trends
  useEffect(() => {
    fetchDailyTrends()
  }, [trendsDateRange.start, trendsDateRange.end])

  const fetchDailyTrends = async () => {
    setDailySummaryLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()
      // Use new optional parameters logic
      const response = await axios.get(`${apiUrl}/api/risk/daily-summary`, {
        params: {
          startDate: trendsDateRange.start,
          endDate: trendsDateRange.end
        }
      })
      setDailySummary(response.data)
      console.log('Daily Trends Data:', response.data)
    } catch (error) {
      console.error('Error fetching daily trends:', error)
      setDailySummary([])
    } finally {
      setDailySummaryLoading(false)
    }
  }

  const fetchData = async () => {
    setLoading(true)
    try {
      const currentStart = dateRange.start
      const currentEnd = dateRange.end
      const days = Math.ceil((new Date(currentEnd).getTime() - new Date(currentStart).getTime()) / (1000 * 60 * 60 * 24))

      // Get API URL dynamically for each request
      const apiUrl = getApiUrlDynamic()

      // Fetch data from new user_daily_risk_scores based endpoints
      const [deptRes, topUsers24hRes, topUsersPeriodRes, highImpactRes, actionRes, incidentsRes] = await Promise.all([
        axios.get(`${apiUrl}/api/risk/department-summary`, {
          params: {
            startDate: currentStart,
            endDate: currentEnd
          }
        }).catch(() => ({ data: [] })),
        // Top users 24h from user_daily_risk_scores
        axios.get(`${apiUrl}/api/risk-trends/top-users`, {
          params: { period: '24h', limit: 50 }
        }).catch(() => ({ data: [] })),
        // Top users for selected period from user_daily_risk_scores
        axios.get(`${apiUrl}/api/risk-trends/top-users`, {
          params: { period: selectedPeriod, limit: 50 }
        }).catch(() => ({ data: [] })),
        // High impact alerts - potential data exfiltration (minMaxMatches=100, minDailyRiskScore=80)
        axios.get(`${apiUrl}/api/risk-trends/high-impact-alerts`, {
          params: { days: 30, minMaxMatches: 100, minDailyRiskScore: 80, page: 1, pageSize: 20 }
        }).catch(() => ({ data: { data: [], pagination: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } } })),
        axios.get(`${apiUrl}/api/risk/action-summary?days=${days}`).catch(() => ({ data: null })),
        // Still need incidents for rules calculation
        axios.get(`${apiUrl}/api/incidents`, {
          params: {
            startDate: currentStart,
            endDate: currentEnd,
            limit: 5000,
            orderBy: 'risk_score_desc'
          }
        }).catch(() => ({ data: [] }))
      ])

      setDeptSummary(deptRes.data)
      setActionSummary(actionRes.data)

      // Set top users from new API (already normalized 0-100 scale with consistency factor)
      setTopUsers24h(topUsers24hRes.data || [])
      setTopUsersPeriod(topUsersPeriodRes.data || [])
      // Reset pagination to page 1 when data changes
      setTopUsersPeriodPage(1)
      setTopUsers24hPage(1)

      // Set high impact alerts with pagination (potential data exfiltration)
      const highImpactData = highImpactRes.data as HighImpactAlertsResponse
      setHighImpactAlerts(highImpactData?.data || [])
      if (highImpactData?.pagination) {
        setHighImpactPagination(highImpactData.pagination)
      }

      // Calculate top rules from dateRange incidents
      const rulesMap = new Map<string, number>()
      incidentsRes.data.forEach((incident: any) => {
        const ruleName = incident.policy || 'Unknown Rule'
        rulesMap.set(ruleName, (rulesMap.get(ruleName) || 0) + 1)
      })
      const topRulesData = Array.from(rulesMap.entries())
        .map(([rule_name, total_alerts]) => ({ rule_name, total_alerts }))
        .sort((a, b) => b.total_alerts - a.total_alerts)
        .slice(0, 10)
      setTopRules(topRulesData)

    } catch (error) {
      console.error('Error fetching data:', error)
    } finally {
      setLoading(false)
    }
  }

  const fetchActionIncidents = (action: string) => {
    setShowModal(true)
    setSelectedAction(action)
  }

  const downloadReport = async () => {
    try {
      const token = localStorage.getItem('authToken')
      const apiUrl = getApiUrlDynamic()
      const url = `${apiUrl}/api/reports/summary?start_date=${dateRange.start}&end_date=${dateRange.end}`

      const response = await axios.get(url, {
        responseType: 'blob',
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        timeout: 30000 // 30 second timeout
      })

      // Check if response is actually a blob
      if (response.data instanceof Blob) {
        if (response.data.size === 0) {
          throw new Error('Empty PDF file received from server')
        }

        const blob = response.data
        const downloadUrl = window.URL.createObjectURL(blob)
        const link = document.createElement('a')
        link.href = downloadUrl
        link.download = `dlp_report_${dateRange.start}_to_${dateRange.end}.pdf`
        document.body.appendChild(link)
        link.click()

        // Cleanup
        setTimeout(() => {
          link.remove()
          window.URL.revokeObjectURL(downloadUrl)
        }, 100)
      } else {
        // If not a blob, might be JSON error
        const text = await response.data.text()
        try {
          const errorData = JSON.parse(text)
          throw new Error(errorData.detail || 'Failed to generate report')
        } catch {
          throw new Error('Invalid response format from server')
        }
      }
    } catch (error: any) {
      console.error('Error downloading report:', error)
      let errorMessage = 'Failed to download report'

      if (error.response) {
        // Try to parse error response
        if (error.response.data instanceof Blob) {
          const text = await error.response.data.text()
          try {
            const errorData = JSON.parse(text)
            errorMessage = errorData.detail || errorMessage
          } catch {
            errorMessage = `Server error: ${error.response.status}`
          }
        } else {
          errorMessage = error.response.data?.detail || error.response.statusText || errorMessage
        }
      } else if (error.message) {
        errorMessage = error.message
      }

      alert(`Failed to download report: ${errorMessage}`)
    }
  }

  const dailyTrendData = {
    x: dailySummary.map(s => s.date),
    y: dailySummary.map(s => s.total_incidents),
    type: 'scatter',
    mode: 'lines+markers',
    name: 'Incidents',
    line: { color: '#283593', width: 2 },
    marker: { size: 4 }
  }

  const totalAlerts = topRules.reduce((sum, r) => sum + r.total_alerts, 0)

  return (
    <div className="dashboard-page">
      <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h1>RADAR - Risk Adaptive Data Analysis Dashboard</h1>
          <p className="dashboard-subtitle">Real-time data loss prevention incident analysis and risk scoring</p>
        </div>
        <button
          onClick={() => setShowReportModal(true)}
          style={{
            padding: '10px 20px',
            background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
            color: 'white',
            border: 'none',
            borderRadius: '8px',
            cursor: 'pointer',
            fontWeight: '600',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            boxShadow: '0 4px 12px rgba(99, 102, 241, 0.3)',
            transition: 'all 0.2s'
          }}
        >
          📊 Daily Report
        </button>
      </div>

      {/* Action Summary Card */}
      {actionSummary && (
        <div className="card" style={{ marginBottom: '24px' }}>
          <h2>Action Analysis</h2>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '16px' }}>
            <div
              onClick={() => fetchActionIncidents('AUTHORIZED')}
              style={{
                background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                padding: '20px',
                borderRadius: '8px',
                color: 'white',
                cursor: 'pointer',
                transition: 'transform 0.2s, box-shadow 0.2s'
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.transform = 'translateY(-2px)'
                e.currentTarget.style.boxShadow = '0 4px 12px rgba(16, 185, 129, 0.4)'
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.transform = 'translateY(0)'
                e.currentTarget.style.boxShadow = 'none'
              }}
            >
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>AUTHORIZED</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.authorized}</div>
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
                e.currentTarget.style.transform = 'translateY(-0)'
                e.currentTarget.style.boxShadow = 'none'
              }}
            >
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>BLOCK</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.block}</div>
            </div>
            <div
              onClick={() => fetchActionIncidents('QUARANTINE')}
              style={{
                background: 'linear-gradient(135deg, #90137fff 0%, #7d0962ff 100%)',
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
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.quarantine}</div>
            </div>
            <div
              onClick={() => fetchActionIncidents('RELEASED')}
              style={{
                background: 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)',
                padding: '20px',
                borderRadius: '8px',
                color: 'white',
                cursor: 'pointer',
                transition: 'transform 0.2s, box-shadow 0.2s'
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.transform = 'translateY(-2px)'
                e.currentTarget.style.boxShadow = '0 4px 12px rgba(245, 158, 11, 0.4)'
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.transform = 'translateY(0)'
                e.currentTarget.style.boxShadow = 'none'
              }}
            >
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>RELEASED</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.released || 0}</div>
            </div>
            <div
              onClick={() => fetchActionIncidents('TOTAL')}
              style={{
                background: 'linear-gradient(135deg, #060a30ff 0%, #021128ff 100%)',
                padding: '20px',
                borderRadius: '8px',
                color: 'white',
                cursor: 'pointer',
                transition: 'transform 0.2s, box-shadow 0.2s'
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.transform = 'translateY(-2px)'
                e.currentTarget.style.boxShadow = '0 4px 12px rgba(6, 10, 48, 0.4)'
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.transform = 'translateY(0)'
                e.currentTarget.style.boxShadow = 'none'
              }}
            >
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>TOTAL</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.total}</div>
            </div>
          </div>
        </div>
      )
      }

      {/* High Impact Alerts - Potential Data Exfiltration with Accordion UI */}
      {highImpactAlerts.length > 0 && (
        <div className="card" style={{ marginBottom: '24px', border: '1px solid #dc2626', background: 'linear-gradient(135deg, rgba(220, 38, 38, 0.05) 0%, var(--surface) 100%)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '24px' }}>🚨</span>
              <div>
                <h2 style={{ margin: 0, color: '#dc2626' }}>Potential Data Exfiltration</h2>
                <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-muted)' }}>
                  High-volume data transfer events (max_matches ≥ 100, daily_score ≥ 80) - Last 30 days
                </p>
              </div>
            </div>
            <span style={{
              fontSize: '12px',
              color: 'white',
              backgroundColor: '#dc2626',
              padding: '4px 12px',
              borderRadius: '12px',
              fontWeight: '600'
            }}>
              {highImpactPagination.totalCount} Alert{highImpactPagination.totalCount !== 1 ? 's' : ''}
            </span>
          </div>

          {/* Accordion List */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {highImpactAlerts.map((alert, idx) => {
              const isExpanded = expandedAlerts.has(alert.user_email + alert.highest_risk_date)
              const toggleExpand = () => {
                const key = alert.user_email + alert.highest_risk_date
                setExpandedAlerts(prev => {
                  const newSet = new Set(prev)
                  if (newSet.has(key)) {
                    newSet.delete(key)
                  } else {
                    newSet.add(key)
                  }
                  return newSet
                })
              }

              return (
                <div
                  key={idx}
                  style={{
                    borderRadius: '8px',
                    border: `1px solid ${alert.severity_level === 'Critical' ? '#dc2626' : alert.severity_level === 'High' ? '#f59e0b' : '#eab308'}`,
                    background: 'var(--background)',
                    overflow: 'hidden'
                  }}
                >
                  {/* Compact Header Row - Always Visible */}
                  <div
                    onClick={toggleExpand}
                    style={{
                      padding: '12px 16px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      cursor: 'pointer',
                      background: isExpanded ? 'rgba(220, 38, 38, 0.05)' : 'transparent',
                      transition: 'background 0.2s'
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '16px', flex: 1 }}>
                      <span style={{
                        fontSize: '16px',
                        transition: 'transform 0.2s',
                        transform: isExpanded ? 'rotate(90deg)' : 'rotate(0deg)'
                      }}>▶</span>

                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '4px',
                        fontSize: '10px',
                        fontWeight: '700',
                        color: 'white',
                        backgroundColor: alert.severity_level === 'Critical' ? '#dc2626' :
                          alert.severity_level === 'High' ? '#f59e0b' : '#eab308',
                        minWidth: '60px',
                        textAlign: 'center'
                      }}>
                        {alert.severity_level.toUpperCase()}
                      </span>

                      <div style={{ fontWeight: '600', color: 'var(--text-primary)', fontSize: '14px', minWidth: '200px' }}>
                        {alert.user_email}
                      </div>

                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'flex', gap: '16px' }}>
                        <span><strong style={{ color: '#dc2626' }}>{alert.max_max_matches}</strong> matches</span>
                        <span>Score: <strong>{Math.round(alert.daily_risk_score)}</strong></span>
                        <span>{alert.highest_risk_date}</span>
                        {alert.is_single_day_event && (
                          <span style={{ color: '#dc2626', fontWeight: '500' }}>⚠️ Single-day</span>
                        )}
                      </div>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation()
                        router.push(`/investigation?user=${encodeURIComponent(alert.user_email)}`)
                      }}
                      style={{
                        padding: '6px 12px',
                        borderRadius: '6px',
                        border: 'none',
                        background: '#dc2626',
                        color: 'white',
                        fontSize: '11px',
                        fontWeight: '600',
                        cursor: 'pointer',
                        whiteSpace: 'nowrap'
                      }}
                    >
                      🔍 Investigate
                    </button>
                  </div>

                  {/* Expanded Details Panel */}
                  {isExpanded && (
                    <div style={{
                      padding: '16px',
                      borderTop: '1px solid var(--border)',
                      background: 'var(--surface)'
                    }}>
                      {/* Summary Stats */}
                      <div style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))',
                        gap: '12px',
                        marginBottom: '16px'
                      }}>
                        <div style={{ textAlign: 'center', padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Impact Score</div>
                          <div style={{ fontSize: '18px', fontWeight: '700', color: '#dc2626' }}>{Math.round(alert.impact_score)}</div>
                        </div>
                        <div style={{ textAlign: 'center', padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Incidents</div>
                          <div style={{ fontSize: '18px', fontWeight: '700' }}>{alert.incident_count}</div>
                        </div>
                        <div style={{ textAlign: 'center', padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Blocks</div>
                          <div style={{ fontSize: '18px', fontWeight: '700', color: '#ef4444' }}>{alert.block_count}</div>
                        </div>
                        <div style={{ textAlign: 'center', padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Quarantines</div>
                          <div style={{ fontSize: '18px', fontWeight: '700', color: '#f59e0b' }}>{alert.quarantine_count}</div>
                        </div>
                        <div style={{ textAlign: 'center', padding: '8px', background: 'var(--background)', borderRadius: '6px' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Active Days</div>
                          <div style={{ fontSize: '18px', fontWeight: '700' }}>{alert.days_with_activity}</div>
                        </div>
                      </div>

                      {/* Incident Details Table */}
                      {alert.incident_details && alert.incident_details.length > 0 && (
                        <div>
                          <h4 style={{ margin: '0 0 8px 0', fontSize: '13px', color: 'var(--text-primary)' }}>
                            📄 Top Incidents on {alert.highest_risk_date}
                          </h4>
                          <div style={{
                            border: '1px solid var(--border)',
                            borderRadius: '6px',
                            overflow: 'hidden',
                            fontSize: '12px'
                          }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                              <thead>
                                <tr style={{ background: 'var(--background)' }}>
                                  <th style={{ padding: '8px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>File</th>
                                  <th style={{ padding: '8px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Destination</th>
                                  <th style={{ padding: '8px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Channel</th>
                                  <th style={{ padding: '8px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Action</th>
                                  <th style={{ padding: '8px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Policy</th>
                                  <th style={{ padding: '8px', textAlign: 'right', borderBottom: '1px solid var(--border)' }}>Matches</th>
                                  <th style={{ padding: '8px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Time</th>
                                </tr>
                              </thead>
                              <tbody>
                                {alert.incident_details.map((detail, dIdx) => (
                                  <tr key={dIdx} style={{ borderBottom: dIdx < alert.incident_details.length - 1 ? '1px solid var(--border)' : 'none' }}>
                                    <td style={{ padding: '8px', maxWidth: '150px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={detail.file_name}>
                                      {detail.file_name || '-'}
                                    </td>
                                    <td style={{ padding: '8px', maxWidth: '120px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={detail.destination}>
                                      {detail.destination || '-'}
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                      <span style={{
                                        padding: '2px 6px',
                                        borderRadius: '4px',
                                        fontSize: '10px',
                                        background: detail.channel === 'Email' ? '#3b82f6' :
                                          detail.channel === 'USB' ? '#8b5cf6' :
                                            detail.channel === 'Cloud' ? '#06b6d4' : '#6b7280',
                                        color: 'white'
                                      }}>
                                        {detail.channel || '-'}
                                      </span>
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                      <span style={{
                                        padding: '2px 6px',
                                        borderRadius: '4px',
                                        fontSize: '10px',
                                        background: (() => {
                                          const action = (detail.action || '').toUpperCase()
                                          if (action === 'BLOCK' || action === 'BLOCKED') return '#ef4444'  // Kırmızı
                                          if (action === 'QUARANTINE' || action === 'QUARANTINED') return '#f59e0b'  // Turuncu
                                          if (action === 'AUTHORIZED' || action === 'RELEASED' || action === 'PERMIT' || action === 'ALLOWED') return '#22c55e'  // Yeşil
                                          return '#6b7280'  // Gri (bilinmeyen)
                                        })(),
                                        color: 'white'
                                      }}>
                                        {detail.action || '-'}
                                      </span>
                                    </td>
                                    <td style={{ padding: '8px', maxWidth: '150px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={detail.policy}>
                                      {detail.policy || '-'}
                                    </td>
                                    <td style={{ padding: '8px', textAlign: 'right', fontWeight: '600', color: '#dc2626' }}>
                                      {detail.max_matches}
                                    </td>
                                    <td style={{ padding: '8px', fontSize: '11px', color: 'var(--text-muted)' }}>
                                      {detail.timestamp?.split(' ')[1] || '-'}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )
            })}
          </div>

          {/* Pagination */}
          {highImpactPagination.totalPages > 1 && (
            <div style={{
              display: 'flex',
              justifyContent: 'center',
              alignItems: 'center',
              gap: '12px',
              marginTop: '16px',
              paddingTop: '16px',
              borderTop: '1px solid var(--border)'
            }}>
              <button
                disabled={highImpactPagination.page <= 1}
                onClick={async () => {
                  const newPage = highImpactPagination.page - 1
                  const apiUrl = getApiUrlDynamic()
                  const res = await axios.get(`${apiUrl}/api/risk-trends/high-impact-alerts`, {
                    params: { days: 30, minMaxMatches: 100, minDailyRiskScore: 80, page: newPage, pageSize: 20 }
                  })
                  const data = res.data as HighImpactAlertsResponse
                  setHighImpactAlerts(data.data)
                  setHighImpactPagination(data.pagination)
                }}
                style={{
                  padding: '6px 12px',
                  borderRadius: '6px',
                  border: '1px solid var(--border)',
                  background: highImpactPagination.page <= 1 ? 'var(--surface)' : 'var(--background)',
                  color: highImpactPagination.page <= 1 ? 'var(--text-muted)' : 'var(--text-primary)',
                  cursor: highImpactPagination.page <= 1 ? 'not-allowed' : 'pointer',
                  fontSize: '12px'
                }}
              >
                ← Previous
              </button>
              <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                Page {highImpactPagination.page} of {highImpactPagination.totalPages}
              </span>
              <button
                disabled={highImpactPagination.page >= highImpactPagination.totalPages}
                onClick={async () => {
                  const newPage = highImpactPagination.page + 1
                  const apiUrl = getApiUrlDynamic()
                  const res = await axios.get(`${apiUrl}/api/risk-trends/high-impact-alerts`, {
                    params: { days: 30, minMaxMatches: 100, minDailyRiskScore: 80, page: newPage, pageSize: 20 }
                  })
                  const data = res.data as HighImpactAlertsResponse
                  setHighImpactAlerts(data.data)
                  setHighImpactPagination(data.pagination)
                }}
                style={{
                  padding: '6px 12px',
                  borderRadius: '6px',
                  border: '1px solid var(--border)',
                  background: highImpactPagination.page >= highImpactPagination.totalPages ? 'var(--surface)' : 'var(--background)',
                  color: highImpactPagination.page >= highImpactPagination.totalPages ? 'var(--text-muted)' : 'var(--text-primary)',
                  cursor: highImpactPagination.page >= highImpactPagination.totalPages ? 'not-allowed' : 'pointer',
                  fontSize: '12px'
                }}
              >
                Next →
              </button>
            </div>
          )}
        </div>
      )}

      {/* Two Column Layout - Period Top Users & 24h Top Users */}
      <div className="dashboard-grid">
        {/* Top Risky Users with Period Selector */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ margin: 0 }}>🎯 Top Risky Users</h2>
            <select
              value={selectedPeriod}
              onChange={(e) => setSelectedPeriod(e.target.value)}
              style={{
                padding: '6px 12px',
                borderRadius: '8px',
                border: '1px solid var(--border)',
                background: 'var(--surface)',
                color: 'var(--text-primary)',
                fontSize: '12px',
                fontWeight: '500',
                cursor: 'pointer'
              }}
            >
              <option value="weekly">Last Week</option>
              <option value="monthly">Last 1 Month</option>
              <option value="quarterly">Last 3 Months</option>
              <option value="6month">Last 6 Months</option>
              <option value="yearly">Last 1 Year</option>
            </select>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th className="text-center">Risk Score</th>
                <th className="text-center">Days Active</th>
                <th className="text-center">Incidents</th>
                <th className="text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="loading-cell">Loading...</td>
                </tr>
              ) : topUsersPeriod.length === 0 ? (
                <tr>
                  <td colSpan={5} className="empty-cell">No data available</td>
                </tr>
              ) : (
                topUsersPeriod.slice((topUsersPeriodPage - 1) * usersPerPage, topUsersPeriodPage * usersPerPage).map((user, idx) => (
                  <tr key={idx}>
                    <td>
                      <div className="user-cell">
                        <div style={{ fontWeight: '500' }}>{user.user_email}</div>
                      </div>
                    </td>
                    <td className="text-center">
                      <span style={{
                        padding: '4px 10px',
                        borderRadius: '12px',
                        fontSize: '12px',
                        fontWeight: '600',
                        color: 'white',
                        backgroundColor: user.risk_score >= 75 ? '#d32f2f' :
                          user.risk_score >= 50 ? '#f57c00' :
                            user.risk_score >= 25 ? '#fbc02d' : '#4caf50'
                      }}>
                        {Math.round(user.risk_score)}
                      </span>
                    </td>
                    <td className="text-center">{user.days_with_activity || 0}</td>
                    <td className="text-center">{user.total_incidents || 0}</td>
                    <td className="text-right">
                      <button
                        onClick={() => router.push(`/investigation?user=${encodeURIComponent(user.user_email)}`)}
                        style={{
                          padding: '4px 10px',
                          borderRadius: '6px',
                          border: 'none',
                          background: '#3b82f6',
                          color: 'white',
                          fontSize: '11px',
                          fontWeight: '600',
                          cursor: 'pointer',
                          whiteSpace: 'nowrap'
                        }}
                      >
                        🔍 Investigate
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pagination for Top Risky Users */}
          {topUsersPeriod.length > usersPerPage && (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '12px', marginTop: '12px', paddingTop: '12px', borderTop: '1px solid var(--border)' }}>
              <button
                disabled={topUsersPeriodPage <= 1}
                onClick={() => setTopUsersPeriodPage(p => Math.max(1, p - 1))}
                style={{ padding: '4px 10px', borderRadius: '6px', border: '1px solid var(--border)', background: topUsersPeriodPage <= 1 ? 'var(--surface)' : 'var(--background)', color: topUsersPeriodPage <= 1 ? 'var(--text-muted)' : 'var(--text-primary)', cursor: topUsersPeriodPage <= 1 ? 'not-allowed' : 'pointer', fontSize: '12px' }}
              >
                ← Prev
              </button>
              <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                {topUsersPeriodPage} / {Math.ceil(topUsersPeriod.length / usersPerPage)}
              </span>
              <button
                disabled={topUsersPeriodPage >= Math.ceil(topUsersPeriod.length / usersPerPage)}
                onClick={() => setTopUsersPeriodPage(p => Math.min(Math.ceil(topUsersPeriod.length / usersPerPage), p + 1))}
                style={{ padding: '4px 10px', borderRadius: '6px', border: '1px solid var(--border)', background: topUsersPeriodPage >= Math.ceil(topUsersPeriod.length / usersPerPage) ? 'var(--surface)' : 'var(--background)', color: topUsersPeriodPage >= Math.ceil(topUsersPeriod.length / usersPerPage) ? 'var(--text-muted)' : 'var(--text-primary)', cursor: topUsersPeriodPage >= Math.ceil(topUsersPeriod.length / usersPerPage) ? 'not-allowed' : 'pointer', fontSize: '12px' }}
              >
                Next →
              </button>
            </div>
          )}
        </div>

        {/* 24-Hour Top Users - Today's Activity */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ margin: 0 }}>⚡ Today's Active Users</h2>
            <span style={{ fontSize: '12px', backgroundColor: '#f57c00', padding: '4px 12px', borderRadius: '12px', color: 'white' }}>Last 24 Hours</span>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th className="text-center">Risk Score</th>
                <th className="text-center">Blocks</th>
                <th className="text-center">Incidents</th>
                <th className="text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="loading-cell">Loading...</td>
                </tr>
              ) : topUsers24h.length === 0 ? (
                <tr>
                  <td colSpan={5} className="empty-cell">No activity today</td>
                </tr>
              ) : (
                topUsers24h.slice((topUsers24hPage - 1) * usersPerPage, topUsers24hPage * usersPerPage).map((user, idx) => (
                  <tr key={idx}>
                    <td>
                      <div className="user-cell">
                        <div style={{ fontWeight: '500' }}>{user.user_email}</div>
                      </div>
                    </td>
                    <td className="text-center">
                      <span style={{
                        padding: '4px 10px',
                        borderRadius: '12px',
                        fontSize: '12px',
                        fontWeight: '600',
                        color: 'white',
                        backgroundColor: user.risk_score >= 75 ? '#d32f2f' :
                          user.risk_score >= 50 ? '#f57c00' :
                            user.risk_score >= 25 ? '#fbc02d' : '#4caf50'
                      }}>
                        {Math.round(user.risk_score)}
                      </span>
                    </td>
                    <td className="text-center">{user.total_blocks || 0}</td>
                    <td className="text-center">{user.total_incidents || 0}</td>
                    <td className="text-right">
                      <button
                        onClick={() => router.push(`/investigation?user=${encodeURIComponent(user.user_email)}`)}
                        style={{
                          padding: '4px 10px',
                          borderRadius: '6px',
                          border: 'none',
                          background: '#f57c00',
                          color: 'white',
                          fontSize: '11px',
                          fontWeight: '600',
                          cursor: 'pointer',
                          whiteSpace: 'nowrap'
                        }}
                      >
                        🔍 Investigate
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pagination for Today's Active Users */}
          {topUsers24h.length > usersPerPage && (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '12px', marginTop: '12px', paddingTop: '12px', borderTop: '1px solid var(--border)' }}>
              <button
                disabled={topUsers24hPage <= 1}
                onClick={() => setTopUsers24hPage(p => Math.max(1, p - 1))}
                style={{ padding: '4px 10px', borderRadius: '6px', border: '1px solid var(--border)', background: topUsers24hPage <= 1 ? 'var(--surface)' : 'var(--background)', color: topUsers24hPage <= 1 ? 'var(--text-muted)' : 'var(--text-primary)', cursor: topUsers24hPage <= 1 ? 'not-allowed' : 'pointer', fontSize: '12px' }}
              >
                ← Prev
              </button>
              <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                {topUsers24hPage} / {Math.ceil(topUsers24h.length / usersPerPage)}
              </span>
              <button
                disabled={topUsers24hPage >= Math.ceil(topUsers24h.length / usersPerPage)}
                onClick={() => setTopUsers24hPage(p => Math.min(Math.ceil(topUsers24h.length / usersPerPage), p + 1))}
                style={{ padding: '4px 10px', borderRadius: '6px', border: '1px solid var(--border)', background: topUsers24hPage >= Math.ceil(topUsers24h.length / usersPerPage) ? 'var(--surface)' : 'var(--background)', color: topUsers24hPage >= Math.ceil(topUsers24h.length / usersPerPage) ? 'var(--text-muted)' : 'var(--text-primary)', cursor: topUsers24hPage >= Math.ceil(topUsers24h.length / usersPerPage) ? 'not-allowed' : 'pointer', fontSize: '12px' }}
              >
                Next →
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Data Movement Chart & Top Matched Rules - Side by Side */}
      <div className="dashboard-grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <div className="card" style={{ position: 'relative', overflow: 'visible' }}>
          <h2>Data Movement 30 days</h2>
          <div style={{ position: 'relative' }}>
            <ChannelActivity days={30} />
          </div>
        </div>

        <div className="card">
          <div className="card-header-row">
            <h2>Top matched rules</h2>
            <div className="total-alerts">
              <span className="total-label">Total alerts last 30 days: {totalAlerts}</span>
            </div>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>Rule</th>
                <th className="text-right">Total alerts</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={2} className="loading-cell">Loading...</td>
                </tr>
              ) : topRules.length === 0 ? (
                <tr>
                  <td colSpan={2} className="empty-cell">No data available</td>
                </tr>
              ) : (
                topRules.map((rule, idx) => (
                  <tr key={idx}>
                    <td>{rule.rule_name}</td>
                    <td className="text-right">{rule.total_alerts}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Daily Incident Trends - Full Width */}
      <div className="card">
        <div className="chart-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <h2 style={{ margin: 0 }}>📈 Daily Incident Trends</h2>
            <p className="chart-subtitle" style={{ margin: '4px 0 0 0' }}>
              Showing {dailySummary.length} days • {trendsDateRange.start} to {trendsDateRange.end}
            </p>
          </div>
          <div className="filter-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
            <input
              type="date"
              className="filter-input"
              value={trendsDateRange.start}
              onChange={(e) => setTrendsDateRange(prev => ({ ...prev, start: e.target.value }))}
              style={{ padding: '6px 10px', minWidth: '130px' }}
            />
            <span style={{ color: '#666' }}>to</span>
            <input
              type="date"
              className="filter-input"
              value={trendsDateRange.end}
              onChange={(e) => setTrendsDateRange(prev => ({ ...prev, end: e.target.value }))}
              style={{ padding: '6px 10px', minWidth: '130px' }}
            />
          </div>
        </div>
        {dailySummaryLoading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '200px', color: '#999' }}>
            Loading trends...
          </div>
        ) : dailySummary.length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '200px', color: '#999' }}>
            No data available for selected date range
          </div>
        ) : (
          <div style={{ overflowX: 'auto', maxHeight: '500px' }}>
            <table className="data-table">
              <thead style={{ position: 'sticky', top: 0, background: 'var(--surface)', zIndex: 1, boxShadow: '0 2px 4px rgba(0,0,0,0.05)' }}>
                <tr>
                  <th>Date</th>
                  <th>Total Incidents</th>
                  <th style={{ cursor: 'help' }} title="High-risk users (max risk score ≥ 61)">High Risk Users</th>
                  <th>Avg Score</th>
                  <th>Unique Users</th>
                  <th style={{ width: '200px' }}>Distribution</th>
                </tr>
              </thead>
              <tbody>
                {(() => {
                  const maxIncidents = Math.max(...dailySummary.map(d => d.total_incidents ?? d.totalIncidents ?? d.TotalIncidents ?? 0));
                  return dailySummary
                    .slice() // Create a shallow copy to sort
                    .sort((a, b) => {
                      const dateA = a.date || a.Date || '';
                      const dateB = b.date || b.Date || '';
                      return dateB.localeCompare(dateA); // Newest first
                    })
                    // REMOVED: .slice(0, 14) limitation - now shows all data matching filter
                    .map((day, idx) => {
                      const total = day.total_incidents ?? day.totalIncidents ?? day.TotalIncidents ?? 0;
                      const highRisk = day.high_risk_count ?? day.highRiskCount ?? day.HighRiskCount ?? 0;
                      const avgScore = day.avg_risk_score ?? day.avgRiskScore ?? day.AvgRiskScore ?? 0;
                      const uniqueUsers = day.unique_users ?? day.uniqueUsers ?? day.UniqueUsers ?? 0;
                      const dateStr = day.date || day.Date || '';
                      const percentage = maxIncidents > 0 ? (total / maxIncidents) * 100 : 0;

                      // Format date
                      let formattedDate = dateStr;
                      try {
                        const d = new Date(dateStr);
                        formattedDate = d.toLocaleDateString('en-US', { day: '2-digit', month: 'short', weekday: 'short', year: 'numeric' });
                      } catch { }

                      return (
                        <tr key={idx}>
                          <td style={{ fontWeight: '500' }}>{formattedDate}</td>
                          <td>
                            <span style={{
                              fontWeight: '600',
                              color: total > 100 ? '#ef4444' : total > 50 ? '#f59e0b' : '#10b981'
                            }}>
                              {total}
                            </span>
                          </td>
                          <td>
                            <span
                              onClick={() => {
                                if (highRisk > 0) {
                                  setSelectedHighRiskDate(dateStr)
                                  setShowHighRiskModal(true)
                                }
                              }}
                              style={{
                                backgroundColor: highRisk > 0 ? 'rgba(239, 68, 68, 0.2)' : 'transparent',
                                color: highRisk > 0 ? '#ef4444' : '#999',
                                padding: '2px 8px',
                                borderRadius: '10px',
                                fontSize: '12px',
                                fontWeight: '600',
                                cursor: highRisk > 0 ? 'pointer' : 'default',
                                transition: 'all 0.2s'
                              }}
                              onMouseEnter={(e) => {
                                if (highRisk > 0) {
                                  e.currentTarget.style.backgroundColor = 'rgba(239, 68, 68, 0.4)'
                                }
                              }}
                              onMouseLeave={(e) => {
                                if (highRisk > 0) {
                                  e.currentTarget.style.backgroundColor = 'rgba(239, 68, 68, 0.2)'
                                }
                              }}
                              title={highRisk > 0 ? 'Click to view high-risk users' : ''}
                            >
                              {highRisk}
                            </span>
                          </td>
                          <td>
                            <span style={{
                              color: avgScore >= 70 ? '#ef4444' : avgScore >= 40 ? '#f59e0b' : '#10b981',
                              fontWeight: '500'
                            }}>
                              {avgScore.toFixed(1)}
                            </span>
                          </td>
                          <td>{uniqueUsers}</td>
                          <td>
                            <div style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: '8px',
                              width: '100%'
                            }}>
                              <div style={{
                                flex: 1,
                                height: '12px',
                                backgroundColor: 'rgba(128,128,128,0.2)',
                                borderRadius: '6px',
                                overflow: 'hidden',
                                position: 'relative'
                              }}>
                                <div style={{
                                  position: 'absolute',
                                  left: 0,
                                  top: 0,
                                  height: '100%',
                                  width: `${percentage}%`,
                                  background: 'linear-gradient(90deg, #3b82f6 0%, #60a5fa 100%)',
                                  borderRadius: '6px',
                                  transition: 'width 0.3s ease'
                                }} />
                                {highRisk > 0 && (
                                  <div style={{
                                    position: 'absolute',
                                    left: 0,
                                    top: 0,
                                    height: '100%',
                                    width: `${(highRisk / maxIncidents) * 100}%`,
                                    backgroundColor: '#ef4444',
                                    borderRadius: '6px 0 0 6px'
                                  }} />
                                )}
                              </div>
                            </div>
                          </td>
                        </tr>
                      );
                    });
                })()}
              </tbody>
            </table>
          </div>
        )}
      </div>

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

        .dashboard-filters {
          display: flex;
          gap: 16px;
          margin-bottom: 24px;
          flex-wrap: wrap;
          align-items: flex-end;
        }

        .filter-group {
          display: flex;
          flex-direction: column;
          gap: 6px;
        }

        .filter-group label {
          font-size: 12px;
          color: var(--text-secondary);
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.5px;
        }

        .filter-input,
        .filter-select {
          padding: 10px 14px;
          border: 1px solid var(--border);
          border-radius: 6px;
          font-size: 14px;
          background: var(--surface);
          color: var(--text-primary);
          min-width: 180px;
          transition: all 0.2s;
        }

        .filter-input:focus,
        .filter-select:focus {
          outline: none;
          border-color: var(--primary);
          box-shadow: 0 0 0 3px rgba(0, 168, 232, 0.1);
        }

        .download-btn {
          background-color: var(--primary);
          color: white;
          padding: 10px 20px;
          border: none;
          border-radius: 6px;
          font-size: 14px;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.2s;
          margin-left: auto;
          box-shadow: 0 2px 8px rgba(0, 168, 232, 0.3);
        }

        .download-btn:hover {
          background-color: var(--primary-dark);
          transform: translateY(-1px);
          box-shadow: 0 4px 12px rgba(0, 168, 232, 0.4);
        }

        .card {
          background: var(--surface);
          border-radius: 6px;
          padding: 24px;
          box-shadow: var(--shadow);
          border: 1px solid var(--border);
          margin-bottom: 24px;
          transition: all 0.2s;
        }

        .card:hover {
          box-shadow: var(--shadow-md);
          border-color: var(--border-hover);
          transform: translateY(-2px);
        }

        .card h2 {
          margin: 0 0 16px 0;
          color: var(--text-primary);
          font-size: 18px;
          font-weight: 600;
          letter-spacing: -0.02em;
        }

        .dashboard-grid {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 24px;
          margin-bottom: 24px;
        }

        .card-header-row {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 16px;
        }

        .total-alerts {
          display: flex;
          flex-direction: column;
          align-items: flex-end;
        }

        .total-label {
          font-size: 14px;
          color: #666;
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

        .user-cell {
          display: flex;
          align-items: center;
          gap: 12px;
        }

        .text-right {
          text-align: right;
        }

        .text-center {
          text-align: center;
        }

        .loading-cell,
        .empty-cell {
          text-align: center;
          color: #999;
          padding: 40px !important;
        }

        .chart-header {
          margin-bottom: 20px;
        }

        .chart-subtitle {
          font-size: 14px;
          color: #666;
          margin: 4px 0 0 0;
        }

        @media (max-width: 1024px) {
          .dashboard-grid {
            grid-template-columns: 1fr;
          }
        }

        @media (max-width: 768px) {
          .dashboard-page {
            padding: 16px;
          }

          .dashboard-header h1 {
            font-size: 24px;
          }

          .dashboard-filters {
            flex-direction: column;
          }

          .download-btn {
            margin-left: 0;
            width: 100%;
          }
        }
      `}</style>

      {/* Action Incidents Modal */}
      <ActionIncidentsModal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        action={selectedAction}
      />

      {/* High Risk Users Modal */}
      <HighRiskUsersModal
        isOpen={showHighRiskModal}
        onClose={() => setShowHighRiskModal(false)}
        date={selectedHighRiskDate}
      />

      {/* Report Modal */}
      <ReportModal
        isOpen={showReportModal}
        onClose={() => setShowReportModal(false)}
      />
    </div >
  )
}