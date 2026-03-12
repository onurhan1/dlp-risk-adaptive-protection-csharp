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
import Pagination from '../components/ui/Pagination'
import LoadingOverlay from '../components/ui/LoadingOverlay'
import GridExport, { ExportColumn } from '../components/ui/GridExport'
import { useTranslation } from '../components/LanguageProvider'
import { resolveUser } from '@/lib/userUtils'

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
  const { t } = useTranslation()
  const [dailySummary, setDailySummary] = useState<DailySummary[]>([])
  const [deptSummary, setDeptSummary] = useState<DepartmentSummary[]>([])
  const [topRules, setTopRules] = useState<TopRule[]>([])
  const [topUsers24h, setTopUsers24h] = useState<TopUser[]>([])
  const [topUsersPeriod, setTopUsersPeriod] = useState<TopUser[]>([])

  // Placeholder data for preview when no API data
  const placeholderUsers: TopUser[] = [
    { user_email: 'ahmet.yilmaz@company.com', risk_score: 92, days_with_activity: 28, total_incidents: 89, total_blocks: 12 },
    { user_email: 'mehmet.kaya@company.com', risk_score: 85, days_with_activity: 22, total_incidents: 67, total_blocks: 8 },
    { user_email: 'elif.demir@company.com', risk_score: 78, days_with_activity: 18, total_incidents: 54, total_blocks: 6 },
    { user_email: 'fatma.celik@company.com', risk_score: 71, days_with_activity: 15, total_incidents: 42, total_blocks: 4 },
    { user_email: 'ali.ozturk@company.com', risk_score: 65, days_with_activity: 12, total_incidents: 38, total_blocks: 3 },
    { user_email: 'ayse.sahin@company.com', risk_score: 58, days_with_activity: 10, total_incidents: 31, total_blocks: 2 },
    { user_email: 'emre.acar@company.com', risk_score: 52, days_with_activity: 9, total_incidents: 28, total_blocks: 2 },
    { user_email: 'zeynep.erdogan@company.com', risk_score: 45, days_with_activity: 8, total_incidents: 22, total_blocks: 1 },
    { user_email: 'can.arslan@company.com', risk_score: 38, days_with_activity: 7, total_incidents: 18, total_blocks: 1 },
    { user_email: 'selin.tas@company.com', risk_score: 32, days_with_activity: 5, total_incidents: 15, total_blocks: 0 },
  ]
  const [highImpactAlerts, setHighImpactAlerts] = useState<HighImpactAlert[]>([])
  const [highImpactPagination, setHighImpactPagination] = useState({ page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
  const [expandedAlerts, setExpandedAlerts] = useState<Set<string>>(new Set())
  const [selectedPeriod, setSelectedPeriod] = useState<string>('quarterly')
  // Period selectors for Data Movement and Top Rules
  const [dataMovementDays, setDataMovementDays] = useState<number>(30)
  const [topRulesDays, setTopRulesDays] = useState<number>(30)
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
  const [trendsDataMinDate, setTrendsDataMinDate] = useState<string | null>(null)
  const todayStr = format(new Date(), 'yyyy-MM-dd')

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
      // Use risk-trends endpoint which supports startDate/endDate parameters
      const response = await axios.get(`${apiUrl}/api/risk-trends/daily-summary`, {
        params: {
          startDate: trendsDateRange.start,
          endDate: trendsDateRange.end
        }
      })
      setDailySummary(response.data)
      // On first load, determine the earliest date in the data for calendar min constraint
      if (!trendsDataMinDate && response.data && response.data.length > 0) {
        const dates = response.data.map((d: any) => d.date || d.Date || '').filter(Boolean).sort()
        if (dates.length > 0) {
          setTrendsDataMinDate(dates[0])
          // If current start is before data min, adjust it
          if (trendsDateRange.start < dates[0]) {
            setTrendsDateRange(prev => ({ ...prev, start: dates[0] }))
          }
        }
      }
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
    <div className="dashboard-page" style={{ position: 'relative' }}>
      <LoadingOverlay isLoading={loading} message={t('common.loading')} />
      <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h1>{t('dashboard.title')}</h1>
          <p className="dashboard-subtitle">{t('dashboard.subtitle')}</p>
        </div>
        <button
          onClick={() => setShowReportModal(true)}
          style={{
            padding: '8px 16px',
            background: 'transparent',
            color: 'var(--text-secondary)',
            border: '1px solid var(--border)',
            borderRadius: '8px',
            cursor: 'pointer',
            fontWeight: '500',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            transition: 'all 0.2s'
          }}
        >
          📊 {t('dashboard.dailyReport')}
        </button>
      </div>

      {/* Action Summary - Donut Chart + Cards */}
      {actionSummary && (
        <div className="card" style={{ marginBottom: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h2>{t('dashboard.actionAnalysis')}</h2>
            <span style={{ fontSize: '13px', color: 'var(--text-muted)', fontWeight: '500' }}>
              {dateRange.start} — {dateRange.end}
            </span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '350px 1fr', gap: '24px', alignItems: 'center' }}>
            {/* Donut Chart */}
            <div style={{ height: '300px', position: 'relative' }}>
              <Plot
                data={[{
                  values: [
                    actionSummary.authorized,
                    actionSummary.block,
                    actionSummary.quarantine,
                    actionSummary.released || 0,
                  ],
                  labels: ['AUTHORIZED', 'BLOCK', 'QUARANTINE', 'RELEASED'],
                  type: 'pie',
                  hole: 0.55,
                  marker: {
                    colors: ['#10b981', '#ef4444', '#a855f7', '#f59e0b'],
                    line: { color: 'rgba(0,0,0,0.1)', width: 1 }
                  },
                  textinfo: 'none',
                  hoverinfo: 'label+value+percent',
                  sort: false,
                }]}
                layout={{
                  margin: { t: 10, b: 10, l: 10, r: 10 },
                  showlegend: false,
                  paper_bgcolor: 'transparent',
                  plot_bgcolor: 'transparent',
                  annotations: [{
                    text: `<b>${actionSummary.total}</b><br><span style="font-size:13px">${t('common.total')}</span>`,
                    showarrow: false,
                    font: { size: 24, color: 'var(--text-primary)' },
                    x: 0.5, y: 0.5,
                  }],
                  height: 300,
                }}
                config={{ displayModeBar: false, responsive: true }}
                style={{ width: '100%', height: '100%' }}
              />
              {/* Clickable Total center overlay */}
              <div
                onClick={() => fetchActionIncidents('TOTAL')}
                style={{
                  position: 'absolute',
                  top: '50%',
                  left: '50%',
                  transform: 'translate(-50%, -50%)',
                  width: '100px',
                  height: '100px',
                  borderRadius: '50%',
                  cursor: 'pointer',
                  zIndex: 10,
                }}
                title="View all incidents"
              />
            </div>

            {/* Action Cards Grid */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px' }}>
              {[
                { action: 'AUTHORIZED', value: actionSummary.authorized, color: '#10b981' },
                { action: 'BLOCK', value: actionSummary.block, color: '#ef4444' },
                { action: 'QUARANTINE', value: actionSummary.quarantine, color: '#8B5CF6' },
                { action: 'RELEASED', value: actionSummary.released || 0, color: '#f59e0b' },
              ].map(({ action, value, color }) => (
                <div
                  key={action}
                  onClick={() => fetchActionIncidents(action)}
                  style={{
                    position: 'relative',
                    overflow: 'hidden',
                    background: 'var(--surface)',
                    border: '1px solid var(--border)',
                    padding: '16px 16px 16px 24px',
                    borderRadius: '12px',
                    cursor: 'pointer',
                    transition: 'all 0.2s',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.borderColor = 'var(--border-hover)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.borderColor = 'var(--border)'
                  }}
                >
                  {/* Left color bar */}
                  <div style={{
                    position: 'absolute',
                    left: 0,
                    top: 0,
                    bottom: 0,
                    width: '4px',
                    backgroundColor: color,
                  }} />
                  <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '4px' }}>{action}</div>
                  <div style={{ fontSize: '28px', fontWeight: '600', color: 'var(--text-primary)' }}>{value}</div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )
      }

      {/* Daily Incident Trends - Full Width */}
      <div className="card">
        <div className="chart-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <h2 style={{ margin: 0, fontSize: '24px' }}>📈 {t('dashboard.dailyTrends')}</h2>
            <p className="chart-subtitle" style={{ margin: '4px 0 0 0', fontSize: '15px' }}>
              {t('dashboard.showing')} {dailySummary.length} {t('dashboard.days')} • {trendsDateRange.start} {t('common.to')} {trendsDateRange.end}
            </p>
          </div>
          <div className="filter-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
            <input
              type="date"
              className="filter-input"
              value={trendsDateRange.start}
              min={trendsDataMinDate || undefined}
              max={trendsDateRange.end}
              onChange={(e) => {
                const newStart = e.target.value
                if (newStart <= trendsDateRange.end) {
                  setTrendsDateRange(prev => ({ ...prev, start: newStart }))
                }
              }}
              style={{ padding: '6px 10px', minWidth: '130px', fontSize: '15px' }}
            />
            <span style={{ color: 'var(--text-muted)', fontSize: '15px' }}>{t('common.to')}</span>
            <input
              type="date"
              className="filter-input"
              value={trendsDateRange.end}
              min={trendsDateRange.start}
              max={todayStr}
              onChange={(e) => {
                const newEnd = e.target.value
                if (newEnd >= trendsDateRange.start && newEnd <= todayStr) {
                  setTrendsDateRange(prev => ({ ...prev, end: newEnd }))
                }
              }}
              style={{ padding: '6px 10px', minWidth: '130px', fontSize: '15px' }}
            />
          </div>
        </div>
        {dailySummaryLoading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '300px', color: '#999' }}>
            {t('common.loading')}
          </div>
        ) : dailySummary.length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '300px', color: '#999' }}>
            {t('common.noData')}
          </div>
        ) : (() => {
          const sorted = dailySummary.slice().sort((a, b) => {
            const dateA = a.date || a.Date || ''
            const dateB = b.date || b.Date || ''
            return dateA.localeCompare(dateB)
          })
          const dates = sorted.map(d => {
            const ds = d.date || d.Date || ''
            try { return new Date(ds).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) } catch { return ds }
          })
          const totals = sorted.map(d => d.total_incidents ?? d.totalIncidents ?? d.TotalIncidents ?? 0)
          const highRisks = sorted.map(d => d.high_risk_count ?? d.highRiskCount ?? d.HighRiskCount ?? 0)
          const avgScores = sorted.map(d => d.avg_risk_score ?? d.avgRiskScore ?? d.AvgRiskScore ?? 0)

          return (
            <Plot
              data={[
                {
                  x: dates,
                  y: totals,
                  type: 'scatter',
                  mode: 'lines',
                  name: t('dashboard.totalIncidents'),
                  fill: 'tozeroy',
                  fillcolor: 'rgba(59, 130, 246, 0.15)',
                  line: { color: '#3b82f6', width: 2.5, shape: 'spline' },
                  hovertemplate: '<b>%{x}</b><br>Incidents: %{y}<extra></extra>',
                },
                {
                  x: dates,
                  y: highRisks,
                  type: 'scatter',
                  mode: 'lines+markers',
                  name: t('dashboard.highRiskUsers'),
                  line: { color: '#ef4444', width: 2, dash: 'dot' },
                  marker: { size: 5, color: '#ef4444' },
                  hovertemplate: '<b>%{x}</b><br>High Risk: %{y}<extra></extra>',
                },
                {
                  x: dates,
                  y: avgScores,
                  type: 'scatter',
                  mode: 'lines',
                  name: t('dashboard.avgRiskScore'),
                  fill: 'tozeroy',
                  fillcolor: 'rgba(245, 158, 11, 0.08)',
                  line: { color: '#f59e0b', width: 1.5, shape: 'spline' },
                  yaxis: 'y2',
                  hovertemplate: '<b>%{x}</b><br>Avg Score: %{y:.1f}<extra></extra>',
                },
              ]}
              layout={{
                margin: { t: 20, b: 60, l: 60, r: 60 },
                paper_bgcolor: 'transparent',
                plot_bgcolor: 'transparent',
                xaxis: {
                  gridcolor: 'rgba(128,128,128,0.08)',
                  tickfont: { size: 12, color: 'var(--text-muted)' },
                  tickangle: -45,
                  showgrid: true,
                },
                yaxis: {
                  title: { text: t('dashboard.incidents'), font: { size: 13, color: 'var(--text-muted)' } },
                  gridcolor: 'rgba(128,128,128,0.08)',
                  tickfont: { size: 12, color: 'var(--text-muted)' },
                  zeroline: false,
                },
                yaxis2: {
                  title: { text: t('dashboard.avgRiskScore'), font: { size: 13, color: 'var(--text-muted)' } },
                  overlaying: 'y',
                  side: 'right',
                  gridcolor: 'rgba(128,128,128,0.05)',
                  tickfont: { size: 12, color: '#f59e0b' },
                  zeroline: false,
                  range: [0, 100],
                },
                legend: {
                  orientation: 'h',
                  x: 0.5,
                  y: -0.25,
                  xanchor: 'center',
                  font: { size: 13, color: 'var(--text-muted)' },
                  bgcolor: 'transparent',
                },
                height: 400,
                hovermode: 'x unified',
              }}
              config={{ displayModeBar: false, responsive: true }}
              style={{ width: '100%' }}
            />
          )
        })()}
      </div>

      {/* Two Column Layout - Period Top Users & 24h Top Users */}
      <div className="dashboard-grid">
        {/* Top Risky Users with Period Selector */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ margin: 0 }}>🎯 {t('dashboard.topRiskyUsers')}</h2>
            <select
              value={selectedPeriod}
              onChange={(e) => setSelectedPeriod(e.target.value)}
              style={{
                padding: '6px 12px',
                borderRadius: '8px',
                border: '1px solid var(--border)',
                background: 'transparent',
                color: 'var(--text-secondary)',
                fontSize: '14px',
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
                <th>{t('common.user')}</th>
                <th className="text-center">{t('dashboard.riskScore')}</th>
                <th className="text-center">{t('dashboard.daysActive')}</th>
                <th className="text-center">{t('dashboard.incidents')}</th>
                <th className="text-right">{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="loading-cell">{t('common.loading')}</td>
                </tr>
              ) : (
                (topUsersPeriod.length > 0 ? topUsersPeriod : placeholderUsers).slice((topUsersPeriodPage - 1) * usersPerPage, topUsersPeriodPage * usersPerPage).map((user, idx) => (
                  <tr key={idx}>
                    <td className="user-email-cell">
                      <span style={{ fontWeight: '500' }} title={user.user_email}>{resolveUser(user.user_email, user.full_name)}</span>
                    </td>
                    <td className="text-center">
                      <span style={{
                        padding: '4px 8px',
                        borderRadius: '6px',
                        fontSize: '11px',
                        fontWeight: '600',
                        backgroundColor: user.risk_score >= 75 ? 'var(--risk-critical-bg)' :
                          user.risk_score >= 50 ? 'var(--risk-high-bg)' :
                            user.risk_score >= 25 ? 'var(--risk-medium-bg)' : 'var(--risk-low-bg)',
                        color: user.risk_score >= 75 ? 'var(--risk-critical-text)' :
                          user.risk_score >= 50 ? 'var(--risk-high-text)' :
                            user.risk_score >= 25 ? 'var(--risk-medium-text)' : 'var(--risk-low-text)'
                      }}>
                        {Math.round(user.risk_score)}
                      </span>
                    </td>
                    <td className="text-center">{user.days_with_activity || 0}</td>
                    <td className="text-center">{user.total_incidents || 0}</td>
                    <td className="text-right">
                      <button
                        onClick={() => router.push(`/investigation?user=${encodeURIComponent(user.user_email)}`)}
                        className="investigate-btn"
                      >
                        Investigate →
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pagination for Top Risky Users */}
          <Pagination
            currentPage={topUsersPeriodPage}
            totalPages={Math.ceil((topUsersPeriod.length > 0 ? topUsersPeriod : placeholderUsers).length / usersPerPage)}
            totalItems={(topUsersPeriod.length > 0 ? topUsersPeriod : placeholderUsers).length}
            onPageChange={setTopUsersPeriodPage}
            compact
            labels={{ totalItems: t('pagination.totalItems') }}
          />
          {/* Export for Top Risky Users */}
          <GridExport
            data={topUsersPeriod}
            fileName={`top-risky-users-${selectedPeriod}`}
            columns={[
              { key: 'user_email', header: 'User Email', width: 30 },
              { key: 'risk_score', header: 'Risk Score', width: 12, formatter: (v: number) => Math.round(v).toString() },
              { key: 'days_with_activity', header: 'Days Active', width: 12 },
              { key: 'total_incidents', header: 'Incidents', width: 12 },
            ]}
          />
        </div>

        {/* 24-Hour Top Users - Today's Activity */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ margin: 0 }}>⚡ {t('dashboard.todayActiveUsers')}</h2>
            <span style={{ fontSize: '12px', backgroundColor: 'var(--risk-high-bg)', padding: '4px 12px', borderRadius: '6px', color: 'var(--risk-high-text)', fontWeight: '600' }}>Last 24 Hours</span>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>{t('common.user')}</th>
                <th className="text-center">{t('dashboard.riskScore')}</th>
                <th className="text-center">{t('dashboard.blocks')}</th>
                <th className="text-center">{t('dashboard.incidents')}</th>
                <th className="text-right">{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="loading-cell">{t('common.loading')}</td>
                </tr>
              ) : (
                (topUsers24h.length > 0 ? topUsers24h : placeholderUsers.slice(0, 5)).slice((topUsers24hPage - 1) * usersPerPage, topUsers24hPage * usersPerPage).map((user, idx) => (
                  <tr key={idx}>
                    <td className="user-email-cell">
                      <span style={{ fontWeight: '500' }} title={user.user_email}>{resolveUser(user.user_email, user.full_name)}</span>
                    </td>
                    <td className="text-center">
                      <span style={{
                        padding: '4px 8px',
                        borderRadius: '6px',
                        fontSize: '11px',
                        fontWeight: '600',
                        backgroundColor: user.risk_score >= 75 ? 'var(--risk-critical-bg)' :
                          user.risk_score >= 50 ? 'var(--risk-high-bg)' :
                            user.risk_score >= 25 ? 'var(--risk-medium-bg)' : 'var(--risk-low-bg)',
                        color: user.risk_score >= 75 ? 'var(--risk-critical-text)' :
                          user.risk_score >= 50 ? 'var(--risk-high-text)' :
                            user.risk_score >= 25 ? 'var(--risk-medium-text)' : 'var(--risk-low-text)'
                      }}>
                        {Math.round(user.risk_score)}
                      </span>
                    </td>
                    <td className="text-center">{user.total_blocks || 0}</td>
                    <td className="text-center">{user.total_incidents || 0}</td>
                    <td className="text-right">
                      <button
                        onClick={() => router.push(`/investigation?user=${encodeURIComponent(user.user_email)}`)}
                        className="investigate-btn"
                      >
                        Investigate →
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pagination for Today's Active Users */}
          <Pagination
            currentPage={topUsers24hPage}
            totalPages={Math.ceil((topUsers24h.length > 0 ? topUsers24h : placeholderUsers.slice(0, 5)).length / usersPerPage)}
            totalItems={(topUsers24h.length > 0 ? topUsers24h : placeholderUsers.slice(0, 5)).length}
            onPageChange={setTopUsers24hPage}
            compact
            labels={{ totalItems: t('pagination.totalItems') }}
          />
          {/* Export for Today's Active Users */}
          <GridExport
            data={topUsers24h}
            fileName="todays-active-users"
            columns={[
              { key: 'user_email', header: 'User Email', width: 30 },
              { key: 'risk_score', header: 'Risk Score', width: 12, formatter: (v: number) => Math.round(v).toString() },
              { key: 'total_blocks', header: 'Blocks', width: 12 },
              { key: 'total_incidents', header: 'Incidents', width: 12 },
            ]}
          />
        </div>
      </div>

      {/* Data Movement Chart & Top Matched Rules - Side by Side */}
      <div className="dashboard-grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <div className="card" style={{ position: 'relative', overflow: 'visible' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <h2 style={{ margin: 0 }}>{t('dashboard.dataMovement')}</h2>
            <select
              value={dataMovementDays}
              onChange={(e) => setDataMovementDays(Number(e.target.value))}
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
              <option value={7}>Last 1 Week</option>
              <option value={14}>Last 2 Weeks</option>
              <option value={30}>Last 1 Month</option>
              <option value={90}>Last 3 Months</option>
              <option value={180}>Last 6 Months</option>
            </select>
          </div>
          <div style={{ position: 'relative' }}>
            <ChannelActivity days={dataMovementDays} />
          </div>
        </div>

        <div className="card">
          <div className="card-header-row" style={{ marginBottom: '8px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
              <h2 style={{ margin: 0 }}>{t('dashboard.topRules')}</h2>
              <select
                value={topRulesDays}
                onChange={(e) => {
                  const newDays = Number(e.target.value)
                  setTopRulesDays(newDays)
                  // Re-fetch incidents for this period
                  const fetchRules = async () => {
                    try {
                      const apiUrl = getApiUrlDynamic()
                      const endDate = new Date()
                      const startDate = new Date()
                      startDate.setDate(startDate.getDate() - newDays)
                      const incidentsRes = await axios.get(`${apiUrl}/api/incidents`, {
                        params: {
                          startDate: format(startDate, 'yyyy-MM-dd'),
                          endDate: format(endDate, 'yyyy-MM-dd'),
                          limit: 5000,
                          orderBy: 'risk_score_desc'
                        }
                      })
                      const rulesMap = new Map<string, number>()
                      ;(incidentsRes.data || []).forEach((incident: any) => {
                        const ruleName = incident.policy || 'Unknown Rule'
                        rulesMap.set(ruleName, (rulesMap.get(ruleName) || 0) + 1)
                      })
                      const topRulesData = Array.from(rulesMap.entries())
                        .map(([rule_name, total_alerts]) => ({ rule_name, total_alerts }))
                        .sort((a, b) => b.total_alerts - a.total_alerts)
                        .slice(0, 10)
                      setTopRules(topRulesData)
                    } catch (error) {
                      console.error('Error fetching rules:', error)
                    }
                  }
                  fetchRules()
                }}
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
                <option value={7}>Last 1 Week</option>
                <option value={14}>Last 2 Weeks</option>
                <option value={30}>Last 1 Month</option>
                <option value={90}>Last 3 Months</option>
                <option value={180}>Last 6 Months</option>
              </select>
            </div>
            <div className="total-alerts">
              <span className="total-label">Total alerts: {totalAlerts}</span>
            </div>
          </div>
          {loading ? (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '200px', color: 'var(--text-muted)' }}>
              {t('common.loading')}
            </div>
          ) : topRules.length === 0 ? (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '200px', color: 'var(--text-muted)' }}>
              {t('common.noData')}
            </div>
          ) : (
            <Plot
              data={[{
                type: 'bar',
                orientation: 'h',
                y: topRules.slice().reverse().map(r => r.rule_name.length > 35 ? r.rule_name.substring(0, 35) + '...' : r.rule_name),
                x: topRules.slice().reverse().map(r => r.total_alerts),
                text: topRules.slice().reverse().map(r => r.total_alerts.toString()),
                textposition: 'outside',
                textfont: { size: 11, color: 'var(--text-primary)' },
                hovertext: topRules.slice().reverse().map(r => `${r.rule_name}: ${r.total_alerts} alerts`),
                hoverinfo: 'text',
                marker: {
                  color: topRules.slice().reverse().map((_, i) => {
                    const colors = ['#3b82f6', '#6366f1', '#8b5cf6', '#a855f7', '#06b6d4', '#14b8a6', '#10b981', '#22c55e', '#84cc16', '#eab308']
                    return colors[i % colors.length]
                  }),
                  line: { width: 0 },
                },
              }]}
              layout={{
                margin: { t: 5, b: 30, l: 200, r: 60 },
                paper_bgcolor: 'transparent',
                plot_bgcolor: 'transparent',
                xaxis: {
                  gridcolor: 'rgba(128,128,128,0.1)',
                  zeroline: false,
                  tickfont: { size: 11, color: 'var(--text-muted)' },
                },
                yaxis: {
                  tickfont: { size: 10, color: 'var(--text-primary)' },
                  automargin: true,
                },
                height: Math.max(200, topRules.length * 32 + 40),
                bargap: 0.3,
              }}
              config={{ displayModeBar: false, responsive: true }}
              style={{ width: '100%' }}
            />
          )}
        </div>
      </div>

      {/* High Impact Alerts - Potential Data Exfiltration */}
      {highImpactAlerts.length > 0 && (
        <div className="card" style={{ marginBottom: '24px', border: '1px solid var(--border)', background: 'var(--surface)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <div style={{
                width: '40px', height: '40px', borderRadius: '10px',
                background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: '20px'
              }}>🔍</div>
              <div>
                <h2 style={{ margin: 0, color: 'var(--text-primary)', fontSize: '22px', fontWeight: '700' }}>{t('dashboard.potentialExfiltration')}</h2>
                <p style={{ margin: '2px 0 0 0', fontSize: '14px', color: 'var(--text-muted)' }}>
                  {t('dashboard.highVolumeTransfer')}
                </p>
              </div>
            </div>
            <span style={{
              fontSize: '14px',
              color: '#6366f1',
              backgroundColor: 'rgba(99, 102, 241, 0.1)',
              padding: '6px 16px',
              borderRadius: '20px',
              fontWeight: '600',
              border: '1px solid rgba(99, 102, 241, 0.2)'
            }}>
              {highImpactPagination.totalCount} {t('dashboard.alerts')}{highImpactPagination.totalCount !== 1 ? 's' : ''}
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

              const severityConfig = alert.severity_level === 'Critical'
                ? { bg: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', border: 'rgba(239, 68, 68, 0.2)' }
                : alert.severity_level === 'High'
                ? { bg: 'rgba(245, 158, 11, 0.1)', color: '#f59e0b', border: 'rgba(245, 158, 11, 0.2)' }
                : { bg: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6', border: 'rgba(59, 130, 246, 0.2)' }

              return (
                <div
                  key={idx}
                  style={{
                    borderRadius: '10px',
                    border: `1px solid var(--border)`,
                    background: 'var(--background)',
                    overflow: 'hidden',
                    transition: 'box-shadow 0.2s'
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.boxShadow = '0 2px 8px rgba(0,0,0,0.08)' }}
                  onMouseLeave={(e) => { e.currentTarget.style.boxShadow = 'none' }}
                >
                  {/* Compact Header Row */}
                  <div
                    onClick={toggleExpand}
                    style={{
                      padding: '14px 16px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      cursor: 'pointer',
                      background: isExpanded ? 'var(--background-secondary)' : 'transparent',
                      transition: 'background 0.2s'
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '16px', flex: 1 }}>
                      <span style={{
                        fontSize: '16px',
                        transition: 'transform 0.2s',
                        transform: isExpanded ? 'rotate(90deg)' : 'rotate(0deg)',
                        color: 'var(--text-muted)'
                      }}>▶</span>

                      <span style={{
                        padding: '4px 12px',
                        borderRadius: '6px',
                        fontSize: '11px',
                        fontWeight: '600',
                        color: severityConfig.color,
                        backgroundColor: severityConfig.bg,
                        border: `1px solid ${severityConfig.border}`,
                        minWidth: '65px',
                        textAlign: 'center',
                        letterSpacing: '0.3px'
                      }}>
                        {alert.severity_level.toUpperCase()}
                      </span>

                      <div style={{ fontWeight: '600', color: 'var(--text-primary)', fontSize: '15px', minWidth: '200px' }}>
                        {resolveUser(alert.user_email, alert.full_name)}
                      </div>

                      <div style={{ fontSize: '13px', color: 'var(--text-muted)', display: 'flex', gap: '16px', alignItems: 'center' }}>
                        <span><strong style={{ color: 'var(--text-primary)' }}>{alert.max_max_matches}</strong> {t('dashboard.matches')}</span>
                        <span>{t('dashboard.score')}: <strong style={{ color: 'var(--text-primary)' }}>{Math.round(alert.daily_risk_score)}</strong></span>
                        <span style={{ color: 'var(--text-secondary)' }}>{alert.highest_risk_date}</span>
                        {alert.is_single_day_event && (
                          <span style={{ color: '#f59e0b', fontWeight: '500', fontSize: '12px' }}>⚡ {t('dashboard.singleDay')}</span>
                        )}
                      </div>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation()
                        router.push(`/investigation?user=${encodeURIComponent(alert.user_email)}`)
                      }}
                      className="investigate-btn"
                    >
                      {t('dashboard.investigate')} →
                    </button>
                  </div>

                  {/* Expanded Details Panel */}
                  {isExpanded && (
                    <div style={{
                      padding: '16px',
                      borderTop: '1px solid var(--border)',
                      background: 'var(--background-secondary)'
                    }}>
                      {/* Summary Stats */}
                      <div style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))',
                        gap: '12px',
                        marginBottom: '16px'
                      }}>
                        {[
                          { label: t('dashboard.impactScore'), value: Math.round(alert.impact_score), color: '#6366f1' },
                          { label: t('dashboard.incidents'), value: alert.incident_count, color: 'var(--text-primary)' },
                          { label: t('dashboard.blocks'), value: alert.block_count, color: '#ef4444' },
                          { label: t('dashboard.quarantines'), value: alert.quarantine_count, color: '#f59e0b' },
                          { label: t('dashboard.activeDays'), value: alert.days_with_activity, color: 'var(--text-primary)' },
                        ].map((stat, sIdx) => (
                          <div key={sIdx} style={{ textAlign: 'center', padding: '12px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '4px' }}>{stat.label}</div>
                            <div style={{ fontSize: '22px', fontWeight: '700', color: stat.color }}>{stat.value}</div>
                          </div>
                        ))}
                      </div>

                      {/* Incident Details Table */}
                      {alert.incident_details && alert.incident_details.length > 0 && (
                        <div>
                          <h4 style={{ margin: '0 0 8px 0', fontSize: '15px', color: 'var(--text-primary)', fontWeight: '600' }}>
                            📄 {t('dashboard.incidentDetails')} — {alert.highest_risk_date}
                          </h4>
                          <div style={{
                            border: '1px solid var(--border)',
                            borderRadius: '8px',
                            overflow: 'hidden',
                            fontSize: '13px'
                          }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed' }}>
                              <thead>
                                <tr style={{ background: 'var(--background)' }}>
                                  <th style={{ padding: '10px', textAlign: 'left', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '18%' }}>{t('dashboard.fileName')}</th>
                                  <th style={{ padding: '10px', textAlign: 'left', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '18%' }}>{t('dashboard.destination')}</th>
                                  <th style={{ padding: '10px', textAlign: 'left', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '10%' }}>{t('dashboard.channel')}</th>
                                  <th style={{ padding: '10px', textAlign: 'left', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '10%' }}>{t('dashboard.action')}</th>
                                  <th style={{ padding: '10px', textAlign: 'left', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '20%' }}>{t('dashboard.policy')}</th>
                                  <th style={{ padding: '10px', textAlign: 'right', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '10%' }}>{t('dashboard.matches')}</th>
                                  <th style={{ padding: '10px', textAlign: 'left', borderBottom: '1px solid var(--border)', fontSize: '13px', width: '14%' }}>{t('dashboard.timestamp')}</th>
                                </tr>
                              </thead>
                              <tbody>
                                {alert.incident_details.map((detail, dIdx) => (
                                  <tr key={dIdx} style={{ borderBottom: dIdx < alert.incident_details.length - 1 ? '1px solid var(--border)' : 'none' }}>
                                    <td style={{ padding: '10px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={detail.file_name}>
                                      {detail.file_name || '-'}
                                    </td>
                                    <td style={{ padding: '10px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={detail.destination}>
                                      {detail.destination ? (detail.destination.length > 25 ? detail.destination.substring(0, 22) + '...' : detail.destination) : '-'}
                                    </td>
                                    <td style={{ padding: '10px' }}>
                                      <span style={{
                                        padding: '3px 8px',
                                        borderRadius: '6px',
                                        fontSize: '11px',
                                        background: detail.channel === 'Email' ? 'rgba(59, 130, 246, 0.1)' :
                                          detail.channel === 'USB' ? 'rgba(139, 92, 246, 0.1)' :
                                            detail.channel === 'Cloud' ? 'rgba(6, 182, 212, 0.1)' : 'rgba(107, 114, 128, 0.1)',
                                        color: detail.channel === 'Email' ? '#3b82f6' :
                                          detail.channel === 'USB' ? '#8b5cf6' :
                                            detail.channel === 'Cloud' ? '#06b6d4' : '#6b7280',
                                        fontWeight: '500'
                                      }}>
                                        {detail.channel || '-'}
                                      </span>
                                    </td>
                                    <td style={{ padding: '10px' }}>
                                      <span style={{
                                        padding: '3px 8px',
                                        borderRadius: '6px',
                                        fontSize: '11px',
                                        fontWeight: '500',
                                        background: (() => {
                                          const action = (detail.action || '').toUpperCase()
                                          if (action === 'BLOCK' || action === 'BLOCKED') return 'rgba(239, 68, 68, 0.1)'
                                          if (action === 'QUARANTINE' || action === 'QUARANTINED') return 'rgba(245, 158, 11, 0.1)'
                                          if (action === 'AUTHORIZED' || action === 'RELEASED' || action === 'PERMIT' || action === 'ALLOWED') return 'rgba(34, 197, 94, 0.1)'
                                          return 'rgba(107, 114, 128, 0.1)'
                                        })(),
                                        color: (() => {
                                          const action = (detail.action || '').toUpperCase()
                                          if (action === 'BLOCK' || action === 'BLOCKED') return '#ef4444'
                                          if (action === 'QUARANTINE' || action === 'QUARANTINED') return '#f59e0b'
                                          if (action === 'AUTHORIZED' || action === 'RELEASED' || action === 'PERMIT' || action === 'ALLOWED') return '#22c55e'
                                          return '#6b7280'
                                        })()
                                      }}>
                                        {detail.action || '-'}
                                      </span>
                                    </td>
                                    <td style={{ padding: '10px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={detail.policy}>
                                      {detail.policy || '-'}
                                    </td>
                                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: '600', color: '#6366f1', fontSize: '14px' }}>
                                      {detail.max_matches}
                                    </td>
                                    <td style={{ padding: '10px', fontSize: '12px', color: 'var(--text-muted)' }}>
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
          <Pagination
            currentPage={highImpactPagination.page}
            totalPages={highImpactPagination.totalPages}
            totalItems={highImpactPagination.totalCount}
            onPageChange={async (newPage: number) => {
              const apiUrl = getApiUrlDynamic()
              const res = await axios.get(`${apiUrl}/api/risk-trends/high-impact-alerts`, {
                params: { days: 30, minMaxMatches: 100, minDailyRiskScore: 80, page: newPage, pageSize: 20 }
              })
              const data = res.data as HighImpactAlertsResponse
              setHighImpactAlerts(data.data)
              setHighImpactPagination(data.pagination)
            }}
            compact
            labels={{
              totalItems: t('pagination.totalItems'),
            }}
          />
        </div>
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
          font-size: 24px;
          font-weight: 600;
          color: var(--text-primary);
          margin: 0 0 4px 0;
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
          padding: 8px 12px;
          border: 1px solid var(--border);
          border-radius: 8px;
          font-size: 14px;
          background: var(--surface);
          color: var(--text-primary);
          min-width: 180px;
          transition: colors 0.2s;
        }

        .filter-input:focus,
        .filter-select:focus {
          outline: none;
          border-color: var(--text-primary);
          box-shadow: 0 0 0 2px rgba(15, 23, 42, 0.1);
        }

        .download-btn {
          background-color: var(--primary);
          color: white;
          padding: 8px 16px;
          border: none;
          border-radius: 8px;
          font-size: 14px;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.2s;
          margin-left: auto;
        }

        .download-btn:hover {
          background-color: var(--primary-dark);
        }

        .card {
          background: var(--surface);
          border-radius: 16px;
          padding: 24px;
          box-shadow: 0 4px 20px rgba(15, 23, 42, 0.06);
          border: none;
          margin-bottom: 24px;
          transition: colors 0.2s;
        }

        .card h2 {
          margin: 0 0 16px 0;
          color: var(--text-primary);
          font-size: 16px;
          font-weight: 600;
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
          color: var(--text-secondary);
        }

        .data-table {
          width: 100%;
          border-collapse: collapse;
          table-layout: fixed;
        }

        .data-table th {
          background: transparent;
          padding: 10px 8px;
          text-align: left;
          font-size: 12px;
          font-weight: 600;
          color: var(--text-secondary);
          text-transform: uppercase;
          letter-spacing: 0.5px;
          border-bottom: 1px solid var(--border);
          overflow: hidden;
          text-overflow: ellipsis;
          white-space: nowrap;
        }

        .data-table th:first-child {
          width: 38%;
        }

        .data-table th:last-child {
          width: 110px;
        }

        .data-table td {
          padding: 12px 8px;
          border-bottom: 1px solid var(--border);
          font-size: 14px;
          color: var(--text-primary);
          overflow: hidden;
          text-overflow: ellipsis;
          white-space: nowrap;
        }

        .data-table tr:hover {
          background: var(--surface-hover);
        }

        .data-table tr:last-child td {
          border-bottom: none;
        }

        .user-email-cell {
          overflow: hidden;
          text-overflow: ellipsis;
          white-space: nowrap;
          max-width: 0;
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
          color: var(--text-secondary);
          padding: 40px !important;
        }

        .chart-header {
          margin-bottom: 16px;
        }

        .chart-subtitle {
          font-size: 14px;
          color: var(--text-secondary);
          margin: 4px 0 0 0;
        }

        @media (max-width: 1024px) {
          .dashboard-grid {
            grid-template-columns: 1fr;
          }
        }

        .investigate-btn {
          padding: 6px 12px;
          border-radius: 8px;
          border: 1px solid var(--border);
          background: transparent;
          color: var(--text-secondary);
          font-size: 12px;
          font-weight: 500;
          cursor: pointer;
          white-space: nowrap;
          transition: all 0.2s;
          display: inline-flex;
          align-items: center;
          gap: 4px;
        }

        .investigate-btn:hover {
          color: var(--text-primary);
          background: var(--surface-hover);
          border-color: var(--border-hover);
        }

        @media (max-width: 768px) {
          .dashboard-page {
            padding: 16px;
          }

          .dashboard-header h1 {
            font-size: 20px;
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