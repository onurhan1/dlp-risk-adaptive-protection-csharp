'use client'

import { useEffect, useState } from 'react'
import axios from 'axios'
import dynamic from 'next/dynamic'
import { format, subDays } from 'date-fns'

const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

import { API_URL } from '@/lib/api-config'

export default function RiskTimelineChart({ days = 30 }: { days?: number }) {
  const [data, setData] = useState<any>(null)
  const [loading, setLoading] = useState(true)
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
      const response = await axios.get(`${API_URL}/api/risk/daily-summary`, {
        params: { days }
      })
      
      // Transform to include risk level breakdown
      const transformed = response.data.map((item: any) => {
        // Estimate risk level counts from avg risk score
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
    } catch (error) {
      console.error('Error fetching timeline data:', error)
    } finally {
      setLoading(false)
    }
  }

  if (loading || !data) {
    return <div>Loading investigation timeline...</div>
  }

  const chartData: any[] = []

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

  return (
    <div className="risk-timeline-chart">
      <div className="chart-header">
        <h3>Investigation {days} days</h3>
        <div className="tabs">
          <button className="tab active">Risky users</button>
          <button className="tab">Alerts</button>
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

      <Plot
        data={chartData}
        layout={{
          height: 300,
          xaxis: { title: 'Date' },
          yaxis: { title: 'Alert Count' },
          hovermode: 'x unified',
          showlegend: true,
          legend: { orientation: 'h', y: -0.2 }
        }}
        style={{ width: '100%', height: '300px' }}
      />

      <style jsx>{`
        .risk-timeline-chart {
          background: white;
          border-radius: 8px;
          padding: 20px;
        }

        .chart-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 16px;
        }

        .chart-header h3 {
          margin: 0;
          color: #333;
        }

        .tabs {
          display: flex;
          gap: 8px;
        }

        .tab {
          padding: 6px 12px;
          background: none;
          border: none;
          cursor: pointer;
          color: #666;
          font-size: 14px;
        }

        .tab.active {
          color: #2196f3;
          border-bottom: 2px solid #2196f3;
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
      `}</style>
    </div>
  )
}

