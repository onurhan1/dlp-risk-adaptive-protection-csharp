'use client'

import { useEffect, useState } from 'react'
import dynamic from 'next/dynamic'
import axios from 'axios'
import { format, subDays } from 'date-fns'
import RiskTimelineChart from '../components/RiskTimelineChart'
import ChannelActivity from '../components/ChannelActivity'
import RiskLevelBadge from '../components/RiskLevelBadge'

const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

import { getApiUrlDynamic } from '@/lib/api-config'

interface DailySummary {
  date: string
  total_incidents: number
  high_risk_count: number
  avg_risk_score: number
  unique_users: number
  departments_affected: number
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
  total_alerts: number
  risk_score: number
}

interface ActionSummary {
  authorized: number
  block: number
  quarantine: number
  unknown: number
  total: number
}

export default function Home() {
  const [dailySummary, setDailySummary] = useState<DailySummary[]>([])
  const [deptSummary, setDeptSummary] = useState<DepartmentSummary[]>([])
  const [topRules, setTopRules] = useState<TopRule[]>([])
  const [topUsers, setTopUsers] = useState<TopUser[]>([])
  const [actionSummary, setActionSummary] = useState<ActionSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedDimension, setSelectedDimension] = useState('department')
  const [dateRange, setDateRange] = useState({
    start: format(subDays(new Date(), 30), 'yyyy-MM-dd'),
    end: format(new Date(), 'yyyy-MM-dd')
  })

  useEffect(() => {
    fetchData()
  }, [selectedDimension, dateRange.start, dateRange.end])

  const fetchData = async () => {
    setLoading(true)
    try {
      const currentStart = dateRange.start
      const currentEnd = dateRange.end
      const days = Math.ceil((new Date(currentEnd).getTime() - new Date(currentStart).getTime()) / (1000 * 60 * 60 * 24))

      // Get API URL dynamically for each request
      const apiUrl = getApiUrlDynamic()

      const [dailyRes, deptRes, incidentsRes, actionRes] = await Promise.all([
        axios.get(`${apiUrl}/api/risk/daily-summary?days=${days}`).catch(() => ({ data: [] })),
        axios.get(`${apiUrl}/api/risk/department-summary`, {
          params: {
            start_date: currentStart,
            end_date: currentEnd
          }
        }).catch(() => ({ data: [] })),
        axios.get(`${apiUrl}/api/incidents`, {
          params: {
            start_date: currentStart,
            end_date: currentEnd,
            limit: 1000,
            order_by: 'risk_score_desc'
          }
        }).catch(() => ({ data: [] })),
        axios.get(`${apiUrl}/api/risk/action-summary?days=${days}`).catch(() => ({ data: null }))
      ])

      setDailySummary(dailyRes.data)
      setDeptSummary(deptRes.data)
      setActionSummary(actionRes.data)

      // Calculate top rules
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

      // Calculate top users
      const usersMap = new Map<string, { alerts: number, risk: number }>()
      incidentsRes.data.forEach((incident: any) => {
        const user = incident.user_email
        const existing = usersMap.get(user) || { alerts: 0, risk: 0 }
        usersMap.set(user, {
          alerts: existing.alerts + 1,
          risk: Math.max(existing.risk, incident.risk_score || 0)
        })
      })
      const topUsersData = Array.from(usersMap.entries())
        .map(([user_email, data]) => ({
          user_email,
          total_alerts: data.alerts,
          risk_score: data.risk
        }))
        .sort((a, b) => b.total_alerts - a.total_alerts)
        .slice(0, 10)
      setTopUsers(topUsersData)

    } catch (error) {
      console.error('Error fetching data:', error)
    } finally {
      setLoading(false)
    }
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
      <div className="dashboard-header">
        <h1>Forcepoint DLP Risk Analyzer Dashboard</h1>
        <p className="dashboard-subtitle">Real-time data loss prevention incident analysis and risk scoring</p>
      </div>

      <div className="dashboard-filters">
        <div className="filter-group">
          <label>Start Date:</label>
          <input
            type="date"
            value={dateRange.start}
            onChange={(e) => setDateRange({ ...dateRange, start: e.target.value })}
            className="filter-input"
          />
        </div>
        <div className="filter-group">
          <label>End Date:</label>
          <input
            type="date"
            value={dateRange.end}
            onChange={(e) => setDateRange({ ...dateRange, end: e.target.value })}
            className="filter-input"
          />
        </div>
        <div className="filter-group">
          <label>Heatmap Dimension:</label>
          <select
            value={selectedDimension}
            onChange={(e) => setSelectedDimension(e.target.value)}
            className="filter-select"
          >
            <option value="department">Department</option>
            <option value="user">User</option>
            <option value="channel">Channel</option>
          </select>
        </div>
        <button className="download-btn" onClick={downloadReport}>
          Download PDF Report
        </button>
      </div>

      {/* Investigation Timeline - Full Width */}
      <div className="card timeline-card">
        <RiskTimelineChart days={30} />
      </div>

      {/* Action Summary Card */}
      {actionSummary && (
        <div className="card" style={{ marginBottom: '24px' }}>
          <h2>Action Analysis</h2>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
            <div style={{
              background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
              padding: '20px',
              borderRadius: '8px',
              color: 'white'
            }}>
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>AUTHORIZED</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.authorized}</div>
            </div>
            <div style={{
              background: 'linear-gradient(135deg, #ef4444 0%, #dc2626 100%)',
              padding: '20px',
              borderRadius: '8px',
              color: 'white'
            }}>
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>BLOCK</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.block}</div>
            </div>
            <div style={{
              background: 'linear-gradient(135deg, #90137fff 0%, #7d0962ff 100%)',
              padding: '20px',
              borderRadius: '8px',
              color: 'white'
            }}>
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>QUARANTINE</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.quarantine}</div>
            </div>
            <div style={{
              background: 'linear-gradient(135deg, #060a30ff 0%, #021128ff 100%)',
              padding: '20px',
              borderRadius: '8px',
              color: 'white'
            }}>
              <div style={{ fontSize: '12px', opacity: 0.9, marginBottom: '4px' }}>TOTAL</div>
              <div style={{ fontSize: '32px', fontWeight: '700' }}>{actionSummary.total}</div>
            </div>
          </div>
        </div>
      )}

      {/* Two Column Layout */}
      <div className="dashboard-grid">
        <div className="card">
          <h2>Top users</h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th className="text-center">Risk Score</th>
                <th className="text-right">Total alerts</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={3} className="loading-cell">Loading...</td>
                </tr>
              ) : topUsers.length === 0 ? (
                <tr>
                  <td colSpan={3} className="empty-cell">No data available</td>
                </tr>
              ) : (
                topUsers.map((user, idx) => (
                  <tr key={idx}>
                    <td>
                      <div className="user-cell">
                        <RiskLevelBadge riskScore={user.risk_score} showScore={false} />
                        <span>{user.user_email}</span>
                      </div>
                    </td>
                    <td className="text-center">
                      <span style={{
                        padding: '4px 10px',
                        borderRadius: '12px',
                        fontSize: '12px',
                        fontWeight: '600',
                        color: 'white',
                        backgroundColor: user.risk_score >= 91 ? '#d32f2f' :
                          user.risk_score >= 61 ? '#f57c00' :
                            user.risk_score >= 41 ? '#fbc02d' : '#4caf50'
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

        <div className="card" style={{ position: 'relative', overflow: 'visible' }}>
          <h2>Data Movement 30 days</h2>
          <div style={{ position: 'relative', zIndex: 9999, pointerEvents: 'auto' }}>
            <ChannelActivity days={30} />
          </div>
        </div>
      </div>

      {/* Top Matched Rules */}
      <div className="dashboard-grid">
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
        <div className="chart-header">
          <h2>📈 Daily Incident Trends</h2>
          <p className="chart-subtitle">Daily Incident Count</p>
        </div>
        <Plot
          data={[dailyTrendData]}
          layout={{
            paper_bgcolor: 'rgba(0,0,0,0)',
            plot_bgcolor: 'rgba(0,0,0,0)',
            font: { color: '#666', size: 12 },
            xaxis: {
              gridcolor: '#e0e0e0',
              title: { text: 'Date', font: { size: 14 } }
            },
            yaxis: {
              gridcolor: '#e0e0e0',
              title: { text: 'Number of Incidents', font: { size: 14 } }
            },
            height: 400,
            margin: { l: 60, r: 20, t: 40, b: 60 },
            showlegend: true
          }}
          style={{ width: '100%', height: '400px' }}
          config={{ displayModeBar: false, responsive: true }}
        />
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

        .timeline-card {
          margin-bottom: 24px;
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
    </div>
  )
}