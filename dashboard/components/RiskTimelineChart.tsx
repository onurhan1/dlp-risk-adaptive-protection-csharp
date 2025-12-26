'use client'

import { useEffect, useState } from 'react'
import axios from 'axios'
import { format, subDays } from 'date-fns'

import { getApiUrlDynamic } from '@/lib/api-config'

interface TopUser {
  user_email: string
  login_name: string
  total_alerts: number
  risk_score: number
  department: string
  risk_level: string
}

interface TopRule {
  rule_name: string
  total_alerts: number
  avg_risk_score: number
  unique_users: number
}

export default function RiskTimelineChart({ days = 30 }: { days?: number }) {
  const [data, setData] = useState<any>(null)
  const [topUsers, setTopUsers] = useState<TopUser[]>([])
  const [topRules, setTopRules] = useState<TopRule[]>([])
  const [loading, setLoading] = useState(true)
  const [activeTab, setActiveTab] = useState<'users' | 'alerts'>('users')

  // Date range state
  const [dateRange, setDateRange] = useState({
    start: format(subDays(new Date(), 30), 'yyyy-MM-dd'),
    end: format(new Date(), 'yyyy-MM-dd')
  })

  useEffect(() => {
    fetchData()
  }, [dateRange.start, dateRange.end])

  const fetchData = async () => {
    setLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()

      // Calculate days from date range
      const startDate = new Date(dateRange.start)
      const endDate = new Date(dateRange.end)
      const daysDiff = Math.ceil((endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24)) + 1

      // Fetch daily summary for chart with date range
      const [summaryResponse, usersResponse, rulesResponse] = await Promise.all([
        axios.get(`${apiUrl}/api/risk/daily-summary`, {
          params: {
            startDate: dateRange.start,
            endDate: dateRange.end,
            days: daysDiff
          }
        }),
        axios.get(`${apiUrl}/api/risk/top-users-daily`, {
          params: {
            startDate: dateRange.start,
            endDate: dateRange.end,
            limit: 20
          }
        }),
        axios.get(`${apiUrl}/api/risk/top-rules-daily`, {
          params: {
            startDate: dateRange.start,
            endDate: dateRange.end,
            limit: 10
          }
        })
      ])

      // Transform daily summary for chart
      const transformed = summaryResponse.data.map((item: any) => {
        return {
          ...item,
          total: item.total_incidents
        }
      })

      setData(transformed)
      // Normalize risk scores: if > 100, it's on 1000-scale, divide by 10
      const normalizedUsers = (usersResponse.data || []).map((user: any) => ({
        ...user,
        risk_score: user.risk_score > 100 ? Math.round(user.risk_score / 10) : user.risk_score
      })).sort((a: any, b: any) => b.risk_score - a.risk_score)
      setTopUsers(normalizedUsers)
      setTopRules(rulesResponse.data || [])
    } catch (error) {
      console.error('Error fetching timeline data:', error)
    } finally {
      setLoading(false)
    }
  }

  const getRiskColor = (score: number): string => {
    // Updated thresholds for normalized 0-100 scale
    if (score >= 75) return '#d32f2f'  // Critical (750-1000 in 1000-scale)
    if (score >= 50) return '#f57c00'  // High (500-749)
    if (score >= 25) return '#fbc02d'  // Medium (250-499)
    return '#4caf50'  // Low (0-249)
  }

  if (loading) {
    return <div>Loading investigation timeline...</div>
  }

  // Chart data for timeline view - only showing total incidents
  const chartData: any[] = []

  if (data && data.length > 0) {
    chartData.push({
      x: data.map((d: any) => d.date),
      y: data.map((d: any) => d.total),
      type: 'scatter',
      mode: 'lines+markers',
      name: 'Total Incidents',
      line: { color: '#1976d2', width: 3 },
      marker: { size: 8 }
    })
  }

  return (
    <div className="risk-timeline-chart">
      <div className="chart-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <h3>Investigation</h3>
          <div className="date-filters" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input
              type="date"
              value={dateRange.start}
              onChange={(e) => setDateRange({ ...dateRange, start: e.target.value })}
              style={{
                padding: '6px 10px',
                borderRadius: '6px',
                border: '1px solid var(--border)',
                background: 'var(--background)',
                color: 'var(--text-primary)',
                fontSize: '13px'
              }}
            />
            <span style={{ color: 'var(--text-secondary)' }}>to</span>
            <input
              type="date"
              value={dateRange.end}
              onChange={(e) => setDateRange({ ...dateRange, end: e.target.value })}
              style={{
                padding: '6px 10px',
                borderRadius: '6px',
                border: '1px solid var(--border)',
                background: 'var(--background)',
                color: 'var(--text-primary)',
                fontSize: '13px'
              }}
            />
          </div>
        </div>
        <div className="tabs">
          <button
            className={`tab ${activeTab === 'users' ? 'active' : ''}`}
            onClick={() => setActiveTab('users')}
          >
            Risky users
          </button>
          <button
            className={`tab ${activeTab === 'alerts' ? 'active' : ''}`}
            onClick={() => setActiveTab('alerts')}
          >
            Alerts
          </button>
        </div>
      </div>


      {activeTab === 'users' ? (
        <div className="tab-content">
          <div className="users-table">
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>User</th>
                  <th>Risk Score</th>
                  <th>Incidents</th>
                </tr>
              </thead>
              <tbody>
                {topUsers.length === 0 ? (
                  <tr>
                    <td colSpan={4} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                      No data available
                    </td>
                  </tr>
                ) : (
                  topUsers.map((user, idx) => (
                    <tr key={idx}>
                      <td>{idx + 1}</td>
                      <td>
                        <div className="user-info">
                          <span style={{ color: getRiskColor(user.risk_score) }}>
                            {user.login_name || user.user_email}
                          </span>
                          {user.department && (
                            <span className="department">{user.department}</span>
                          )}
                        </div>
                      </td>
                      <td>
                        <span
                          className="risk-badge"
                          style={{
                            backgroundColor: getRiskColor(user.risk_score),
                            color: 'white',
                            padding: '2px 8px',
                            borderRadius: '12px',
                            fontSize: '12px',
                            fontWeight: '600'
                          }}
                        >
                          {user.risk_score}
                        </span>
                      </td>
                      <td>{user.total_alerts}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <div className="tab-content">
          <div className="rules-table">
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Rule</th>
                  <th>Alerts</th>
                  <th>Users</th>
                </tr>
              </thead>
              <tbody>
                {topRules.length === 0 ? (
                  <tr>
                    <td colSpan={4} style={{ textAlign: 'center', padding: '20px', color: '#999' }}>
                      No data available
                    </td>
                  </tr>
                ) : (
                  topRules.map((rule, idx) => (
                    <tr key={idx}>
                      <td>{idx + 1}</td>
                      <td style={{ color: '#f57c00', fontWeight: '500' }}>{rule.rule_name}</td>
                      <td>
                        <span style={{
                          backgroundColor: '#f57c00',
                          color: 'white',
                          padding: '2px 8px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: '600'
                        }}>
                          {rule.total_alerts}
                        </span>
                      </td>
                      <td>{rule.unique_users}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <style jsx>{`
        .risk-timeline-chart {
          background: var(--surface);
          border-radius: 8px;
          padding: 20px;
          color: var(--text-primary);
        }

        .chart-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 16px;
        }

        .chart-header h3 {
          margin: 0;
          color: var(--text-primary);
        }

        .tabs {
          display: flex;
          gap: 4px;
          background: var(--background-secondary);
          padding: 4px;
          border-radius: 8px;
        }

        .tab {
          padding: 8px 16px;
          background: none;
          border: none;
          cursor: pointer;
          color: var(--text-secondary);
          font-size: 14px;
          font-weight: 500;
          border-radius: 6px;
          transition: all 0.2s;
        }

        .tab:hover {
          color: var(--text-primary);
          background: var(--surface-hover);
        }

        .tab.active {
          color: white;
          background: var(--primary);
        }

        .filters {
          display: flex;
          gap: 16px;
          margin-bottom: 16px;
          flex-wrap: wrap;
        }

        .filter-item {
          display: flex;
          align-items: center;
          gap: 6px;
          cursor: pointer;
          font-size: 12px;
        }

        .filter-dot {
          width: 12px;
          height: 12px;
          border-radius: 50%;
        }

        .tab-content {
          margin-bottom: 20px;
          max-height: 400px;
          overflow-y: auto;
        }

        .users-table table,
        .rules-table table {
          width: 100%;
          border-collapse: collapse;
        }

        .users-table th,
        .users-table td,
        .rules-table th,
        .rules-table td {
          padding: 10px 12px;
          text-align: left;
          border-bottom: 1px solid var(--border);
          font-size: 13px;
        }

        .users-table th,
        .rules-table th {
          background: var(--background-secondary);
          font-weight: 600;
          color: var(--text-secondary);
          text-transform: uppercase;
          font-size: 11px;
          letter-spacing: 0.5px;
        }

        .users-table tr:hover,
        .rules-table tr:hover {
          background: var(--surface-hover);
        }

        .user-info {
          display: flex;
          flex-direction: column;
          gap: 2px;
        }

        .department {
          font-size: 11px;
          color: var(--text-muted);
        }
      `}</style>
    </div>
  )
}
