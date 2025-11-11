'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format, subDays } from 'date-fns'

import { API_URL } from '@/lib/api-config'

interface Report {
  id: number
  report_type: string
  generated_at: string
  filename: string
  status: string
}

export default function ReportsPage() {
  const [reports, setReports] = useState<Report[]>([])
  const [loading, setLoading] = useState(true)
  const [generating, setGenerating] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  
  const [reportType, setReportType] = useState<'daily' | 'department' | 'user_risk'>('daily')
  const [startDate, setStartDate] = useState(format(subDays(new Date(), 7), 'yyyy-MM-dd'))
  const [endDate, setEndDate] = useState(format(new Date(), 'yyyy-MM-dd'))

  useEffect(() => {
    fetchReports()
  }, [])

  const fetchReports = async () => {
    setLoading(true)
    try {
      const token = localStorage.getItem('authToken')
      const response = await axios.get(`${API_URL}/api/reports`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      })
      setReports(response.data.reports || [])
    } catch (error: any) {
      console.error('Error fetching reports:', error)
      setMessage({ 
        type: 'error', 
        text: error.response?.data?.detail || 'Failed to load reports' 
      })
      setTimeout(() => setMessage(null), 5000)
    } finally {
      setLoading(false)
    }
  }

  const generateReport = async () => {
    setGenerating(true)
    setMessage(null)
    try {
      const token = localStorage.getItem('authToken')
      const response = await axios.post(
        `${API_URL}/api/reports/generate`,
        {
          report_type: reportType,
          start_date: startDate,
          end_date: endDate
        },
        {
          headers: token ? { Authorization: `Bearer ${token}` } : {}
        }
      )

      if (response.data.success) {
        setMessage({ 
          type: 'success', 
          text: `Report generated successfully: ${response.data.filename}` 
        })
        setTimeout(() => setMessage(null), 5000)
        // Refresh reports list
        await fetchReports()
      } else {
        throw new Error(response.data.message || 'Failed to generate report')
      }
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to generate report'
      setMessage({ type: 'error', text: errorMessage })
      setTimeout(() => setMessage(null), 5000)
    } finally {
      setGenerating(false)
    }
  }

  const downloadReport = async (reportId: number, filename: string) => {
    try {
      const token = localStorage.getItem('authToken')
      const response = await axios.get(
        `${API_URL}/api/reports/${reportId}/download`,
        {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
          responseType: 'blob'
        }
      )

      const blob = new Blob([response.data], { type: 'application/pdf' })
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(url)
    } catch (error: any) {
      setMessage({ 
        type: 'error', 
        text: error.response?.data?.detail || 'Failed to download report' 
      })
      setTimeout(() => setMessage(null), 5000)
    }
  }

  if (loading) {
    return (
      <div className="dashboard-page">
        <div className="loading">Loading reports...</div>
      </div>
    )
  }

  return (
    <div className="dashboard-page">
      <div className="dashboard-header">
        <div>
          <h1>Reports</h1>
          <p className="dashboard-subtitle">Generate and manage security reports</p>
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

      {/* Generate Report Section */}
      <div className="card">
        <h2>Generate New Report</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
              Report Type
            </label>
            <select
              value={reportType}
              onChange={(e) => setReportType(e.target.value as 'daily' | 'department' | 'user_risk')}
              style={{
                width: '100%',
                maxWidth: '400px',
                padding: '10px 14px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px',
                background: 'var(--surface)',
                color: 'var(--text-primary)'
              }}
            >
              <option value="daily">Daily Summary</option>
              <option value="department">Department Summary</option>
              <option value="user_risk">User Risk Trends</option>
            </select>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
            <div>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                Start Date
              </label>
              <input
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                style={{
                  width: '100%',
                  padding: '10px 14px',
                  border: '1px solid var(--border)',
                  borderRadius: '6px',
                  fontSize: '14px',
                  background: 'var(--surface)',
                  color: 'var(--text-primary)'
                }}
              />
            </div>

            <div>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                End Date
              </label>
              <input
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                style={{
                  width: '100%',
                  padding: '10px 14px',
                  border: '1px solid var(--border)',
                  borderRadius: '6px',
                  fontSize: '14px',
                  background: 'var(--surface)',
                  color: 'var(--text-primary)'
                }}
              />
            </div>
          </div>

          <button
            onClick={generateReport}
            disabled={generating}
            style={{
              padding: '12px 32px',
              background: 'var(--primary)',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: generating ? 'not-allowed' : 'pointer',
              fontWeight: '600',
              fontSize: '16px',
              opacity: generating ? 0.6 : 1,
              alignSelf: 'flex-start',
              boxShadow: '0 2px 8px rgba(0, 168, 232, 0.3)',
              transition: 'all 0.2s'
            }}
            onMouseEnter={(e) => {
              if (!generating) {
                e.currentTarget.style.transform = 'translateY(-1px)'
                e.currentTarget.style.boxShadow = '0 4px 12px rgba(0, 168, 232, 0.4)'
              }
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.transform = 'translateY(0)'
              e.currentTarget.style.boxShadow = '0 2px 8px rgba(0, 168, 232, 0.3)'
            }}
          >
            {generating ? 'Generating...' : 'Generate Report'}
          </button>
        </div>
      </div>

      {/* Reports List */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <h2>Generated Reports</h2>
          <button
            onClick={fetchReports}
            style={{
              padding: '8px 16px',
              background: 'var(--surface-hover)',
              color: 'var(--text-primary)',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              cursor: 'pointer',
              fontSize: '14px',
              fontWeight: '500'
            }}
          >
            Refresh
          </button>
        </div>

        {reports.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
            No reports generated yet. Generate a report to get started.
          </div>
        ) : (
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Report Type</th>
                  <th>Generated At</th>
                  <th>Status</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {reports.map((report) => (
                  <tr key={report.id}>
                    <td>{report.id}</td>
                    <td>{report.report_type}</td>
                    <td>{new Date(report.generated_at).toLocaleString()}</td>
                    <td>
                      <span
                        style={{
                          padding: '4px 12px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: '600',
                          background: report.status === 'completed' ? 'rgba(92, 184, 92, 0.1)' : 'rgba(240, 173, 78, 0.1)',
                          color: report.status === 'completed' ? '#5cb85c' : '#f0ad4e'
                        }}
                      >
                        {report.status}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        onClick={() => downloadReport(report.id, report.filename)}
                        style={{
                          padding: '6px 12px',
                          background: 'var(--primary)',
                          color: 'white',
                          border: 'none',
                          borderRadius: '6px',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '500'
                        }}
                      >
                        Download
                      </button>
                    </td>
                  </tr>
                ))}
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
        }

        .card h2 {
          margin: 0 0 16px 0;
          color: var(--text-primary);
          font-size: 18px;
          font-weight: 600;
          letter-spacing: -0.02em;
        }

        .table-container {
          overflow-x: auto;
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

        .loading {
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 40px;
          color: var(--text-secondary);
        }

        @media (max-width: 768px) {
          .dashboard-page {
            padding: 0;
          }

          .card {
            padding: 16px;
          }
        }
      `}</style>
    </div>
  )
}

