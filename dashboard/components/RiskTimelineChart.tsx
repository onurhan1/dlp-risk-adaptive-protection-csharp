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
  const [selectedSeverities, setSelectedSeverities] = useState({
    critical: true,
    high: true,
    medium: true,
    low: true,
    total: true
  })

  useEffect(() => {
    fetchData()
  }, [days])

  const fetchData = async () => {
    setLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()

      // Fetch daily summary for chart
      const [summaryResponse, usersResponse, rulesResponse] = await Promise.all([
        axios.get(`${apiUrl}/api/risk/daily-summary`, { params: { days } }),
        axios.get(`${apiUrl}/api/risk/top-users-daily`, { params: { days, limit: 20 } }),
        axios.get(`${apiUrl}/api/risk/top-rules-daily`, { params: { days, limit: 10 } })
      ])

      // Transform daily summary for chart
      const transformed = summaryResponse.data.map((item: any) => {
        const critical = item.avg_risk_score >= 91 ? item.total_incidents * 0.2 : 0
        const high = item.avg_risk_score >= 61 && item.avg_risk_score < 91 ? item.total_incidents * 0.3 : 0
        const medium = item.avg_risk_score >= 41 && item.avg_risk_score < 61 ? item.total_incidents * 0.3 : 0
        const low = item.avg_risk_score < 41 ? item.total_incidents * 0.2 : 0

        return {
          ...item,
          critical: Math.round(critical),
          high: Math.round(high),
          medium: Math.round(medium),
          low: Math.round(low),
          total: item.total_incidents
        }
      })

      setData(transformed)
      setTopUsers(usersResponse.data || [])
      setTopRules(rulesResponse.data || [])
    } catch (error) {
      console.error('Error fetching timeline data:', error)
    } finally {
      setLoading(false)
    }
  }

  const getRiskColor = (score: number): string => {
    if (score >= 91) return '#d32f2f'
    if (score >= 61) return '#f57c00'
    if (score >= 41) return '#fbc02d'
    return '#4caf50'
  }

  if (loading) {
    return <div>Loading investigation timeline...</div>
  }

  // Chart data for timeline view
  const chartData: any[] = []

  if (data && data.length > 0) {
    if (selectedSeverities.critical) {
      chartData.push({
        x: data.map((d: any) => d.date),
        y: data.map((d: any) => d.critical),
        type: 'scatter',
        mode: 'lines+markers',
        name: 'Critical',
        line: { color: '#d32f2f', width: 2 },
        marker: { size: 6 }
      })
    }

    if (selectedSeverities.high) {
      chartData.push({
        x: data.map((d: any) => d.date),
        y: data.map((d: any) => d.high),
        type: 'scatter',
        mode: 'lines+markers',
        name: 'High',
        line: { color: '#f57c00', width: 2 },
        marker: { size: 6 }
      })
    }

    if (selectedSeverities.medium) {
      chartData.push({
        x: data.map((d: any) => d.date),
        y: data.map((d: any) => d.medium),
        type: 'scatter',
        mode: 'lines+markers',
        name: 'Medium',
        line: { color: '#fbc02d', width: 2 },
        marker: { size: 6 }
      })
    }

    if (selectedSeverities.low) {
      chartData.push({
        x: data.map((d: any) => d.date),
        y: data.map((d: any) => d.low),
        type: 'scatter',
        mode: 'lines+markers',
        name: 'Low',
        line: { color: '#4caf50', width: 2 },
        marker: { size: 6 }
      })
    }

    if (selectedSeverities.total) {
      chartData.push({
        x: data.map((d: any) => d.date),
        y: data.map((d: any) => d.total),
        type: 'scatter',
        mode: 'lines+markers',
        name: 'Total',
        line: { color: '#1976d2', width: 3 },
        marker: { size: 8 }
      })
    }
  }

  return (
    <div className="risk-timeline-chart">
      <div className="chart-header">
        <h3>Investigation {days} days</h3>
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

      <div className="filters">
        <label className="filter-item">
          <input
            type="checkbox"
            checked={selectedSeverities.critical}
            onChange={(e) => setSelectedSeverities({ ...selectedSeverities, critical: e.target.checked })}
          />
          <span className="filter-dot" style={{ backgroundColor: '#d32f2f' }} />
          Critical
        </label>
        <label className="filter-item">
          <input
            type="checkbox"
            checked={selectedSeverities.high}
            onChange={(e) => setSelectedSeverities({ ...selectedSeverities, high: e.target.checked })}
          />
          <span className="filter-dot" style={{ backgroundColor: '#f57c00' }} />
          High
        </label>
        <label className="filter-item">
          <input
            type="checkbox"
            checked={selectedSeverities.medium}
            onChange={(e) => setSelectedSeverities({ ...selectedSeverities, medium: e.target.checked })}
          />
          <span className="filter-dot" style={{ backgroundColor: '#fbc02d' }} />
          Medium
        </label>
        <label className="filter-item">
          <input
            type="checkbox"
            checked={selectedSeverities.low}
            onChange={(e) => setSelectedSeverities({ ...selectedSeverities, low: e.target.checked })}
          />
          <span className="filter-dot" style={{ backgroundColor: '#4caf50' }} />
          Low
        </label>
        <label className="filter-item">
          <input
            type="checkbox"
            checked={selectedSeverities.total}
            onChange={(e) => setSelectedSeverities({ ...selectedSeverities, total: e.target.checked })}
          />
          <span className="filter-dot" style={{ backgroundColor: '#1976d2' }} />
          Total
        </label>
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
