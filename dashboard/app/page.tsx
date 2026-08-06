'use client'

import { useCallback, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import dynamic from 'next/dynamic'
import axios from 'axios'
import { format, subDays } from 'date-fns'
import ChannelActivity, { ChannelActivitySnapshot } from '../components/ChannelActivity'
import TopBreakdownCard, { BreakdownSnapshot } from '../components/TopBreakdownCard'
import RiskLevelBadge from '../components/RiskLevelBadge'
import ActionIncidentsModal from '../components/ActionIncidentsModal'
import HighRiskUsersModal from '../components/HighRiskUsersModal'
import ReportModal from '../components/ReportModal'
import Pagination from '../components/ui/Pagination'
import LoadingOverlay from '../components/ui/LoadingOverlay'
import GridExport, { ExportColumn } from '../components/ui/GridExport'
import { useTranslation } from '../components/LanguageProvider'
import { useTheme } from '../components/ThemeProvider'
import {
  BarChart3,
  TrendingUp,
  Target,
  Zap,
  Search,
  ShieldAlert,
  FileText,
  FileBarChart,
  ChevronRight,
  Activity,
  AlertCircle,
  RefreshCw,
  Download,
  FileDown
} from 'lucide-react'

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
  const { theme } = useTheme()

  // Plotly.js renders to SVG and cannot resolve CSS custom properties.
  // We must pass actual hex color values based on the current theme.
  const plotTextPrimary = theme === 'dark' ? '#e8eaed' : '#1e293b'
  const plotTextMuted = theme === 'dark' ? '#8a9199' : '#94a3b8'
  const [dailySummary, setDailySummary] = useState<DailySummary[]>([])
  const [deptSummary, setDeptSummary] = useState<DepartmentSummary[]>([])
  const [topRules, setTopRules] = useState<TopRule[]>([])
  const [topUsers24h, setTopUsers24h] = useState<TopUser[]>([])
  const [topUsersPeriod, setTopUsersPeriod] = useState<TopUser[]>([])
  const [highImpactAlerts, setHighImpactAlerts] = useState<HighImpactAlert[]>([])
  const [highImpactPagination, setHighImpactPagination] = useState({ page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
  const [highImpactLoading, setHighImpactLoading] = useState(false)
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
  const [actionDataDateRange, setActionDataDateRange] = useState<{ min: string; max: string } | null>(null)
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

  // Manual collect state
  const [collectMode, setCollectMode] = useState<'date' | 'hours'>('date')
  const [collectDateRange, setCollectDateRange] = useState({
    start: format(subDays(new Date(), 7), 'yyyy-MM-dd'),
    end: format(new Date(), 'yyyy-MM-dd')
  })
  const [collectHours, setCollectHours] = useState<number>(24)
  const [manualCollectJobId, setManualCollectJobId] = useState<string | null>(null)
  const [manualCollectStatus, setManualCollectStatus] = useState<any>(null)
  const [isCollecting, setIsCollecting] = useState(false)
  const [collectError, setCollectError] = useState<string | null>(null)
  const [showManualCollect, setShowManualCollect] = useState(false)

  // Snapshots of the four summary grids, kept in sync by each grid's onDataChange
  // so the PDF record prints exactly the rows and filters that are on screen.
  const [channelSnapshot, setChannelSnapshot] = useState<ChannelActivitySnapshot | null>(null)
  const [userSnapshot, setUserSnapshot] = useState<BreakdownSnapshot | null>(null)
  const [deptSnapshot, setDeptSnapshot] = useState<BreakdownSnapshot | null>(null)
  const [pdfLoading, setPdfLoading] = useState(false)
  const [pdfError, setPdfError] = useState<string | null>(null)

  const handleChannelData = useCallback((snapshot: ChannelActivitySnapshot) => setChannelSnapshot(snapshot), [])
  const handleUserData = useCallback((snapshot: BreakdownSnapshot) => setUserSnapshot(snapshot), [])
  const handleDeptData = useCallback((snapshot: BreakdownSnapshot) => setDeptSnapshot(snapshot), [])

  // Restore active manual collect job from localStorage on mount
  useEffect(() => {
    const savedJobId = localStorage.getItem('manualCollectJobId')
    if (savedJobId) {
      // Verify the job still exists before restoring
      const apiUrl = getApiUrlDynamic()
      axios.get(`${apiUrl}/api/collector/manual-collect/status/${savedJobId}`)
        .then((response) => {
          const status = response.data
          if (status.status === 'Completed' || status.status === 'Failed') {
            // Job already finished, show result but don't poll
            localStorage.removeItem('manualCollectJobId')
            setManualCollectStatus(status)
            setShowManualCollect(true)
          } else {
            // Job still active, resume polling
            setManualCollectJobId(savedJobId)
            setIsCollecting(true)
            setShowManualCollect(true)
            setManualCollectStatus(status)
          }
        })
        .catch(() => {
          // Job not found or API unreachable — clear stale data
          localStorage.removeItem('manualCollectJobId')
        })
    }
  }, [])

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
      const [deptRes, topUsers24hRes, topUsersPeriodRes, highImpactRes, actionRes, topRulesRes] = await Promise.all([
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
        // High impact alert - potential data exfiltration
        axios.get(`${apiUrl}/api/risk-trends/high-impact-alerts`, {
          params: { days: 30, minMaxMatches: 100, minDailyRiskScore: 80, page: 1, pageSize: 20 }
        }).catch(() => ({ data: { data: [], pagination: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } } })),
        axios.get(`${apiUrl}/api/risk/action-summary?days=${days}`).catch(() => ({ data: null })),
        // Optimized: Fetch aggregated top rules directly from backend
        axios.get(`${apiUrl}/api/risk-trends/top-rules`, {
          params: {
            startDate: currentStart,
            endDate: currentEnd,
            limit: 10
          }
        }).catch(() => ({ data: [] }))
      ])

      setDeptSummary(deptRes.data)
      setActionSummary(actionRes.data)

      // Set actual data date range from action-summary response
      if (actionRes.data?.min_date && actionRes.data?.max_date) {
        setActionDataDateRange({ min: actionRes.data.min_date, max: actionRes.data.max_date })
      }

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

      // Set top rules directly from API response
      setTopRules(topRulesRes.data || [])

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

  // Manual collect: start collection
  const startManualCollect = async () => {
    setCollectError(null)
    try {
      const apiUrl = getApiUrlDynamic()
      let body: any = {}

      if (collectMode === 'hours') {
        if (collectHours < 1 || collectHours > 2160) {
          setCollectError(t('dashboard.collectHoursError'))
          return
        }
        body = { lookback_hours: collectHours }
      } else {
        if (collectDateRange.start >= collectDateRange.end) {
          setCollectError(t('dashboard.collectDateError'))
          return
        }
        body = { start_date: collectDateRange.start, end_date: collectDateRange.end }
      }

      setIsCollecting(true)
      const response = await axios.post(`${apiUrl}/api/collector/manual-collect`, body)

      if (response.data?.job_id) {
        const jobId = response.data.job_id
        setManualCollectJobId(jobId)
        setManualCollectStatus({ status: 'Queued', progress: 0, message: t('dashboard.collectQueued') })
        // Persist to localStorage so we can resume after navigation
        localStorage.setItem('manualCollectJobId', jobId)
      }
    } catch (error: any) {
      console.error('Error starting manual collection:', error)
      setCollectError(error.response?.data?.detail || 'Failed to start collection')
      setIsCollecting(false)
    }
  }

  // Polling for manual collect status
  useEffect(() => {
    if (!manualCollectJobId) return

    let failCount = 0
    const pollInterval = setInterval(async () => {
      try {
        const apiUrl = getApiUrlDynamic()
        const response = await axios.get(`${apiUrl}/api/collector/manual-collect/status/${manualCollectJobId}`)
        const status = response.data
        setManualCollectStatus(status)
        failCount = 0 // Reset on success

        if (status.status === 'Completed' || status.status === 'Failed') {
          clearInterval(pollInterval)
          setIsCollecting(false)
          // Clear localStorage - job is done
          localStorage.removeItem('manualCollectJobId')
          // Refresh dashboard data if completed
          if (status.status === 'Completed') {
            setTimeout(() => fetchData(), 2000)
          }
        }
      } catch (error: any) {
        failCount++
        console.error(`Error polling collect status (attempt ${failCount}):`, error)
        // After 5 consecutive failures, stop polling and clean up
        if (failCount >= 5) {
          clearInterval(pollInterval)
          setIsCollecting(false)
          setManualCollectJobId(null)
          localStorage.removeItem('manualCollectJobId')
          setManualCollectStatus({ status: 'Failed', progress: 0, message: 'Bağlantı hatası — durum takip edilemiyor. Çekim arka planda devam ediyor olabilir.' })
        }
      }
    }, 2000)

    return () => clearInterval(pollInterval)
  }, [manualCollectJobId])

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
    line: { color: '#5c6bc0', width: 2 },
    marker: { size: 4 }
  }

  const totalAlerts = topRules.reduce((sum, r) => sum + r.total_alerts, 0)

  // ── PDF record of the four summary grids ──────────────────────────────────
  const periodLabel = (days: number) => {
    switch (days) {
      case 7: return t('dashboard.last1Week')
      case 14: return t('dashboard.last2Weeks')
      case 30: return t('dashboard.lastMonth')
      case 90: return t('dashboard.last3Months')
      case 180: return t('dashboard.last6Months')
      default: return `${days} ${t('dashboard.days')}`
    }
  }

  const actionLabel = (action: string) => action === 'TOTAL' ? t('dashboard.allActions') : action

  // Percentages are carried over from the grids rather than recomputed, so the
  // printed figures match the screen cell for cell.
  const buildReportSections = () => {
    const sections: any[] = []
    const REPORT_DESTINATION_LIMIT = 10

    if (channelSnapshot) {
      const periodInfo = `${t('dashboard.reportPeriod')}: ${periodLabel(channelSnapshot.days)}`

      sections.push({
        title: `${t('dashboard.dataMovement')} - ${t('channel.channel')}`,
        subtitle: periodInfo,
        label_header: t('channel.channel'),
        value_header: t('dashboard.reportIncidentCount'),
        percentage_header: t('dashboard.reportShare'),
        rows: channelSnapshot.channels.map(c => ({
          name: c.channel,
          count: c.total_incidents,
          percentage: c.percentage
        }))
      })

      const destinations = channelSnapshot.destinations.slice(0, REPORT_DESTINATION_LIMIT)
      sections.push({
        title: `${t('dashboard.dataMovement')} - ${t('channel.destination')}`,
        subtitle: `${periodInfo} (${t('dashboard.reportFirstN').replace('{n}', String(REPORT_DESTINATION_LIMIT))})`,
        label_header: t('channel.destination'),
        value_header: t('dashboard.reportIncidentCount'),
        percentage_header: t('dashboard.reportShare'),
        rows: destinations.map(d => ({
          name: d.destination,
          count: d.total_incidents,
          percentage: d.percentage
        }))
      })
    }

    if (topRules.length > 0) {
      sections.push({
        title: t('dashboard.topRules'),
        subtitle: `${t('dashboard.reportPeriod')}: ${periodLabel(topRulesDays)}`,
        label_header: t('dashboard.topRules'),
        value_header: t('dashboard.reportAlertCount'),
        percentage_header: t('dashboard.reportShare'),
        rows: topRules.map(r => ({
          name: r.rule_name,
          count: r.total_alerts,
          percentage: totalAlerts > 0 ? (r.total_alerts / totalAlerts) * 100 : 0
        }))
      })
    }

    const breakdownSection = (snapshot: BreakdownSnapshot | null, title: string, labelHeader: string) => {
      if (!snapshot || snapshot.items.length === 0) return
      const total = snapshot.items.reduce((sum, i) => sum + i.total_alerts, 0)
      sections.push({
        title,
        subtitle: `${t('dashboard.reportPeriod')}: ${periodLabel(snapshot.days)} | ${t('dashboard.reportAction')}: ${actionLabel(snapshot.action)}`,
        label_header: labelHeader,
        value_header: t('dashboard.reportAlertCount'),
        percentage_header: t('dashboard.reportShare'),
        rows: snapshot.items.map(i => ({
          name: i.name,
          count: i.total_alerts,
          percentage: total > 0 ? (i.total_alerts / total) * 100 : 0
        }))
      })
    }

    breakdownSection(userSnapshot, t('dashboard.topMatchedUsers'), t('common.user'))
    breakdownSection(deptSnapshot, t('dashboard.topMatchedDepartments'), t('dashboard.department'))

    return sections
  }

  const downloadDashboardPdf = async () => {
    setPdfError(null)
    setPdfLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()
      const response = await axios.post(
        `${apiUrl}/api/reports/dashboard-summary/pdf`,
        {
          title: t('dashboard.pdfReportTitle'),
          subtitle: t('dashboard.pdfReportSubtitle'),
          sections: buildReportSections()
        },
        { responseType: 'blob' }
      )

      const url = URL.createObjectURL(new Blob([response.data], { type: 'application/pdf' }))
      const link = document.createElement('a')
      link.href = url
      link.download = `anasayfa-ozet-raporu-${format(new Date(), 'yyyyMMdd-HHmm')}.pdf`
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch (error) {
      console.error('Error generating dashboard PDF:', error)
      setPdfError(t('dashboard.pdfReportError'))
    } finally {
      setPdfLoading(false)
    }
  }

  return (
    <div className="dashboard-page" style={{ position: 'relative' }}>
      <LoadingOverlay isLoading={loading} message={t('common.loading')} fullScreen />
      <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1>{t('dashboard.title')}</h1>
          <p className="dashboard-subtitle">{t('dashboard.subtitle')}</p>
        </div>
        <button
          onClick={() => setShowReportModal(true)}
          style={{
            padding: '8px 16px',
            background: 'var(--surface)',
            color: 'var(--text-primary)',
            border: '1px solid var(--border)',
            borderRadius: '8px',
            cursor: 'pointer',
            fontWeight: '500',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            transition: 'all 0.2s',
            fontFamily: 'Inter, sans-serif',
          }}
        >
          <FileBarChart size={16} /> {t('dashboard.dailyReport')}
        </button>
      </div>

      {/* Action Summary - Donut Chart + Cards */}
      {actionSummary && (
        <div className="card" style={{ marginBottom: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h2>{t('dashboard.actionAnalysis')}</h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '13px', color: 'var(--text-muted)', fontWeight: '500' }}>
                {actionDataDateRange ? `${actionDataDateRange.min} — ${actionDataDateRange.max}` : `${dateRange.start} — ${dateRange.end}`}
              </span>
              <button
                onClick={() => setShowManualCollect(!showManualCollect)}
                style={{
                  padding: '6px 14px',
                  borderRadius: '8px',
                  border: '1px solid var(--border)',
                  background: showManualCollect ? 'rgba(59, 130, 246, 0.1)' : 'var(--surface)',
                  color: showManualCollect ? '#3b82f6' : 'var(--text-primary)',
                  fontSize: '12px',
                  fontWeight: '600',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  transition: 'all 0.2s',
                }}
              >
                <Download size={14} /> {t('dashboard.manualCollect')}
              </button>
            </div>
          </div>

          {/* Manual Collect Panel */}
          {showManualCollect && (
            <div style={{
              marginTop: '16px',
              padding: '16px',
              background: 'var(--background)',
              borderRadius: '12px',
              border: '1px solid var(--border)',
            }}>
              {/* Mode selector */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: '12px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer', fontSize: '13px', fontWeight: '500' }}>
                  <input
                    type="radio"
                    name="collectMode"
                    checked={collectMode === 'date'}
                    onChange={() => setCollectMode('date')}
                    style={{ accentColor: '#3b82f6' }}
                  />
                  {t('dashboard.dateBased')}
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer', fontSize: '13px', fontWeight: '500' }}>
                  <input
                    type="radio"
                    name="collectMode"
                    checked={collectMode === 'hours'}
                    onChange={() => setCollectMode('hours')}
                    style={{ accentColor: '#3b82f6' }}
                  />
                  {t('dashboard.hourBased')}
                </label>
              </div>

              {/* Inputs based on mode */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
                {collectMode === 'date' ? (
                  <>
                    <input
                      type="date"
                      className="filter-input"
                      value={collectDateRange.start}
                      max={collectDateRange.end}
                      onChange={(e) => setCollectDateRange(prev => ({ ...prev, start: e.target.value }))}
                      style={{ padding: '6px 10px', minWidth: '140px', fontSize: '13px' }}
                      disabled={isCollecting}
                    />
                    <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>{t('common.to')}</span>
                    <input
                      type="date"
                      className="filter-input"
                      value={collectDateRange.end}
                      min={collectDateRange.start}
                      max={todayStr}
                      onChange={(e) => setCollectDateRange(prev => ({ ...prev, end: e.target.value }))}
                      style={{ padding: '6px 10px', minWidth: '140px', fontSize: '13px' }}
                      disabled={isCollecting}
                    />
                  </>
                ) : (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>{t('dashboard.lookbackHours')}:</span>
                    <input
                      type="number"
                      className="filter-input"
                      value={collectHours}
                      min={1}
                      max={2160}
                      onChange={(e) => setCollectHours(Math.max(1, Math.min(2160, parseInt(e.target.value) || 1)))}
                      style={{ padding: '6px 10px', width: '80px', fontSize: '13px' }}
                      disabled={isCollecting}
                    />
                  </div>
                )}

                <button
                  onClick={startManualCollect}
                  disabled={isCollecting}
                  style={{
                    padding: '7px 16px',
                    borderRadius: '8px',
                    border: 'none',
                    background: isCollecting ? 'var(--surface-hover)' : '#3b82f6',
                    color: isCollecting ? 'var(--text-muted)' : 'white',
                    fontSize: '13px',
                    fontWeight: '600',
                    cursor: isCollecting ? 'not-allowed' : 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px',
                    transition: 'all 0.2s',
                  }}
                >
                  <RefreshCw size={14} className={isCollecting ? 'animate-spin' : ''} />
                  {t('dashboard.startCollection')}
                </button>
              </div>

              {/* Error message */}
              {collectError && (
                <div style={{
                  marginTop: '10px',
                  padding: '8px 12px',
                  background: 'rgba(239, 68, 68, 0.1)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  borderRadius: '8px',
                  color: '#ef4444',
                  fontSize: '13px',
                }}>
                  {collectError}
                </div>
              )}

              {/* Progress bar */}
              {manualCollectStatus && isCollecting && (
                <div style={{ marginTop: '12px' }}>
                  <div style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginBottom: '6px',
                    fontSize: '12px',
                  }}>
                    <span style={{
                      color: manualCollectStatus.status === 'Failed' ? '#ef4444' : 'var(--text-muted)',
                      fontWeight: '500',
                    }}>
                      {manualCollectStatus.message || t('dashboard.collectRunning')}
                    </span>
                    <span style={{ color: 'var(--text-muted)', fontWeight: '600' }}>
                      {manualCollectStatus.progress}%
                    </span>
                  </div>
                  <div style={{
                    width: '100%',
                    height: '8px',
                    background: 'var(--surface-hover)',
                    borderRadius: '4px',
                    overflow: 'hidden',
                  }}>
                    <div style={{
                      width: `${manualCollectStatus.progress}%`,
                      height: '100%',
                      background: manualCollectStatus.status === 'Failed' ? '#ef4444' :
                        manualCollectStatus.status === 'Queued' ? '#f59e0b' : '#3b82f6',
                      borderRadius: '4px',
                      transition: 'width 0.5s ease',
                      animation: manualCollectStatus.status === 'Queued' ? 'pulse 1.5s infinite' : 'none',
                    }} />
                  </div>
                </div>
              )}

              {/* Completed message */}
              {manualCollectStatus && !isCollecting && manualCollectStatus.status === 'Completed' && (
                <div style={{
                  marginTop: '10px',
                  padding: '8px 12px',
                  background: 'rgba(16, 185, 129, 0.1)',
                  border: '1px solid rgba(16, 185, 129, 0.3)',
                  borderRadius: '8px',
                  color: '#10b981',
                  fontSize: '13px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                }}>
                  ✅ {manualCollectStatus.message || t('dashboard.collectCompleted')}
                  {manualCollectStatus.total_incidents > 0 && (
                    <span style={{ fontWeight: '600' }}>
                      ({manualCollectStatus.total_incidents} {t('dashboard.incidentsFound')})
                    </span>
                  )}
                </div>
              )}

              {/* Failed message */}
              {manualCollectStatus && !isCollecting && manualCollectStatus.status === 'Failed' && (
                <div style={{
                  marginTop: '10px',
                  padding: '8px 12px',
                  background: 'rgba(239, 68, 68, 0.1)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  borderRadius: '8px',
                  color: '#ef4444',
                  fontSize: '13px',
                }}>
                  ❌ {manualCollectStatus.message || t('dashboard.collectFailed')}
                </div>
              )}
            </div>
          )}
          <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: '32px', alignItems: 'center' }}>
            {/* Donut Chart */}
            <div style={{ height: '300px', position: 'relative' }}>
              {(() => {
                const totalVal = actionSummary.total || (actionSummary.authorized + actionSummary.block + actionSummary.quarantine + (actionSummary.released || 0))
                return (
                  <Plot
                    data={[{
                      values: [
                        actionSummary.authorized,
                        actionSummary.block,
                        actionSummary.quarantine,
                        actionSummary.released || 0,
                      ],
                      labels: ['Authorized', 'Block', 'Quarantine', 'Released'],
                      type: 'pie',
                      hole: 0.6,
                      marker: {
                        colors: ['#10b981', '#ef4444', '#8b5cf6', '#f59e0b'],
                        line: { color: 'white', width: 3 }
                      },
                      textinfo: 'percent',
                      textposition: 'inside',
                      textfont: { size: 13, color: '#ffffff', family: 'Inter, sans-serif' },
                      hoverinfo: 'label+value+percent',
                      sort: false,
                      direction: 'clockwise',
                      rotation: 90,
                    }]}
                    layout={{
                      margin: { t: 10, b: 10, l: 10, r: 10 },
                      showlegend: false,
                      paper_bgcolor: 'transparent',
                      plot_bgcolor: 'transparent',
                      annotations: [{
                        text: `<b style="font-size:28px">${totalVal}</b><br><span style="font-size:12px;color:${plotTextMuted}">Toplam</span>`,
                        showarrow: false,
                        font: { size: 28, color: plotTextPrimary, family: 'Inter, sans-serif' },
                        x: 0.5, y: 0.5,
                      }],
                      height: 300,
                    }}
                    config={{ displayModeBar: false, responsive: true }}
                    style={{ width: '100%', height: '100%' }}
                  />
                )
              })()}
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

            {/* Action Cards Grid — Figma colored left-bar pattern */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px' }}>
              {[
                { action: 'AUTHORIZED', value: actionSummary.authorized, color: '#10b981' },
                { action: 'BLOCK', value: actionSummary.block, color: '#ef4444' },
                { action: 'QUARANTINE', value: actionSummary.quarantine, color: '#a855f7' },
                { action: 'RELEASED', value: actionSummary.released || 0, color: '#f59e0b' },
              ].map(({ action, value, color }) => (
                <div
                  key={action}
                  onClick={() => fetchActionIncidents(action)}
                  style={{
                    background: 'var(--background)',
                    padding: '16px 16px 16px 20px',
                    borderRadius: '12px',
                    cursor: 'pointer',
                    transition: 'all 0.2s',
                    borderLeft: `4px solid ${color}`,
                    border: '1px solid var(--border)',
                    borderLeftWidth: '4px',
                    borderLeftColor: color,
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.boxShadow = '0 2px 8px rgba(0,0,0,0.06)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.boxShadow = 'none'
                  }}
                >
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '4px', fontWeight: '500' }}>{action}</div>
                  <div style={{ fontSize: '28px', fontWeight: '700', color: 'var(--text-primary)' }}>{value}</div>
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
            <h2 style={{ margin: 0, fontSize: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <TrendingUp size={20} style={{ color: '#3b82f6' }} /> {t('dashboard.dailyTrends')}
            </h2>
            <p className="chart-subtitle" style={{ margin: '4px 0 0 0', fontSize: '13px' }}>
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
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '300px', color: 'var(--text-muted)' }}>
            {t('common.loading')}
          </div>
        ) : dailySummary.length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '300px', color: 'var(--text-muted)' }}>
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
                  tickfont: { size: 12, color: plotTextMuted },
                  tickangle: -45,
                  showgrid: true,
                },
                yaxis: {
                  title: { text: t('dashboard.incidents'), font: { size: 13, color: plotTextMuted } },
                  gridcolor: 'rgba(128,128,128,0.08)',
                  tickfont: { size: 12, color: plotTextMuted },
                  zeroline: false,
                },
                yaxis2: {
                  title: { text: t('dashboard.avgRiskScore'), font: { size: 13, color: plotTextMuted } },
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
                  font: { size: 13, color: plotTextMuted },
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

      {/* Two Column Layout - Period Top Users & 24h Top Users — STACKED */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '24px', marginBottom: '24px' }}>
        {/* Top Risky Users with Period Selector */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', paddingRight: '14px' }}>
            <h2 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '8px', fontSize: '16px' }}>
              <Target size={18} style={{ color: '#ef4444' }} /> {t('dashboard.topRiskyUsers')}
            </h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <select
                value={selectedPeriod}
                onChange={(e) => setSelectedPeriod(e.target.value)}
                style={{
                  padding: '8px 12px',
                  borderRadius: '6px',
                  border: '1px solid var(--border)',
                  background: 'var(--surface)',
                  color: 'var(--text-primary)',
                  fontSize: '13px',
                  fontWeight: '500',
                  cursor: 'pointer',
                  height: '36px'
                }}
              >
                <option value="weekly">{t('dashboard.lastWeek')}</option>
                <option value="monthly">{t('dashboard.lastMonth')}</option>
                <option value="quarterly">{t('dashboard.last3Months')}</option>
                <option value="6month">{t('dashboard.last6Months')}</option>
                <option value="yearly">{t('dashboard.lastYear')}</option>
              </select>
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
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '35%', minWidth: '120px' }}>{t('common.user')}</th>
                <th className="text-center" style={{ width: '12%', minWidth: '70px', textAlign: 'center' }}>{t('dashboard.riskScore')}</th>
                <th className="text-center" style={{ width: '14%', minWidth: '80px', textAlign: 'center' }}>{t('dashboard.daysActive')}</th>
                <th className="text-center" style={{ width: '14%', minWidth: '70px', textAlign: 'center' }}>{t('dashboard.incidents')}</th>
                <th className="text-right" style={{ width: '25%', minWidth: '110px', textAlign: 'right' }}>{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="loading-cell">{t('common.loading')}</td>
                </tr>
              ) : topUsersPeriod.length === 0 ? (
                <tr>
                  <td colSpan={5} className="empty-cell">{t('common.noData')}</td>
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
                        backgroundColor: user.risk_score >= 75 ? '#ef4444' :
                          user.risk_score >= 50 ? '#f59e0b' :
                            user.risk_score >= 25 ? '#eab308' : '#10b981'
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
                          padding: '6px 12px',
                          borderRadius: '6px',
                          border: '1px solid rgba(59, 130, 246, 0.3)',
                          background: 'rgba(59, 130, 246, 0.1)',
                          color: '#3b82f6',
                          fontSize: '12px',
                          fontWeight: '600',
                          cursor: 'pointer',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          transition: 'all 0.2s',
                          whiteSpace: 'nowrap'
                        }}
                        onMouseEnter={(e) => { e.currentTarget.style.background = 'rgba(59, 130, 246, 0.2)' }}
                        onMouseLeave={(e) => { e.currentTarget.style.background = 'rgba(59, 130, 246, 0.1)' }}
                      >
                        <Search size={14} /> {t('dashboard.investigate')}
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pagination for Top Risky Users */}
          {topUsersPeriod.length > 0 && (
            <Pagination
              currentPage={topUsersPeriodPage}
              totalPages={Math.ceil(topUsersPeriod.length / usersPerPage)}
              totalItems={topUsersPeriod.length}
              onPageChange={setTopUsersPeriodPage}
              compact
              labels={{ totalItems: t('pagination.totalItems') }}
            />
          )}
        </div>

        {/* 24-Hour Top Users - Today's Activity */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', paddingRight: '14px' }}>
            <h2 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '8px', fontSize: '16px' }}>
              <Zap size={18} style={{ color: '#f59e0b' }} /> {t('dashboard.todayActiveUsers')}
            </h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '12px', backgroundColor: 'var(--surface-hover)', padding: '6px 12px', borderRadius: '6px', color: 'var(--text-muted)', fontWeight: '500', border: '1px solid var(--border)' }}>{t('dashboard.last24h')}</span>
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
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '35%', minWidth: '120px' }}>{t('common.user')}</th>
                <th className="text-center" style={{ width: '12%', minWidth: '70px', textAlign: 'center' }}>{t('dashboard.riskScore')}</th>
                <th className="text-center" style={{ width: '14%', minWidth: '80px', textAlign: 'center' }}>{t('dashboard.blocks')}</th>
                <th className="text-center" style={{ width: '14%', minWidth: '70px', textAlign: 'center' }}>{t('dashboard.incidents')}</th>
                <th className="text-right" style={{ width: '25%', minWidth: '110px', textAlign: 'right' }}>{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="loading-cell">{t('common.loading')}</td>
                </tr>
              ) : topUsers24h.length === 0 ? (
                <tr>
                  <td colSpan={5} className="empty-cell">{t('common.noData')}</td>
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
                        backgroundColor: user.risk_score >= 75 ? '#ef4444' :
                          user.risk_score >= 50 ? '#f59e0b' :
                            user.risk_score >= 25 ? '#eab308' : '#10b981'
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
                          padding: '6px 12px',
                          borderRadius: '6px',
                          border: '1px solid rgba(59, 130, 246, 0.3)',
                          background: 'rgba(59, 130, 246, 0.1)',
                          color: '#3b82f6',
                          fontSize: '12px',
                          fontWeight: '600',
                          cursor: 'pointer',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          transition: 'all 0.2s',
                          whiteSpace: 'nowrap'
                        }}
                        onMouseEnter={(e) => { e.currentTarget.style.background = 'rgba(59, 130, 246, 0.2)' }}
                        onMouseLeave={(e) => { e.currentTarget.style.background = 'rgba(59, 130, 246, 0.1)' }}
                      >
                        <Search size={14} /> {t('dashboard.investigate')}
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pagination for Today's Active Users */}
          {topUsers24h.length > 0 && (
            <Pagination
              currentPage={topUsers24hPage}
              totalPages={Math.ceil(topUsers24h.length / usersPerPage)}
              totalItems={topUsers24h.length}
              onPageChange={setTopUsers24hPage}
              compact
              labels={{ totalItems: t('pagination.totalItems') }}
            />
          )}
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
              <option value={7}>{t('dashboard.last1Week')}</option>
              <option value={14}>{t('dashboard.last2Weeks')}</option>
              <option value={30}>{t('dashboard.lastMonth')}</option>
              <option value={90}>{t('dashboard.last3Months')}</option>
              <option value={180}>{t('dashboard.last6Months')}</option>
            </select>
          </div>
          <div style={{ position: 'relative' }}>
            <ChannelActivity days={dataMovementDays} onDataChange={handleChannelData} />
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
                        ; (incidentsRes.data || []).forEach((incident: any) => {
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
                <option value={7}>{t('dashboard.last1Week')}</option>
                <option value={14}>{t('dashboard.last2Weeks')}</option>
                <option value={30}>{t('dashboard.lastMonth')}</option>
                <option value={90}>{t('dashboard.last3Months')}</option>
                <option value={180}>{t('dashboard.last6Months')}</option>
              </select>
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
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px' }}>
              {topRules.map((rule, idx) => {
                const maxCount = Math.max(...topRules.map(r => r.total_alerts), 1)
                // Bar length stays relative to the busiest rule so the chart is readable,
                // but the label reports the rule's share of the total (same as Data Movements).
                const barWidth = (rule.total_alerts / maxCount) * 100
                const percentage = totalAlerts > 0 ? (rule.total_alerts / totalAlerts) * 100 : 0
                return (
                  <div key={idx} style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={{ width: '220px', flexShrink: 0, fontSize: '13px', color: 'var(--text-primary)', fontWeight: '500', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={rule.rule_name}>
                      {rule.rule_name}
                    </div>
                    <div style={{ flex: 1, height: '32px', background: 'var(--background-secondary)', borderRadius: '4px', position: 'relative', overflow: 'hidden', border: '1px solid var(--border)' }}>
                      <div style={{ height: '100%', width: `${barWidth}%`, background: 'linear-gradient(90deg, #3b82f6 0%, #2563eb 100%)', borderRadius: '4px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', paddingRight: '8px', transition: 'width 0.3s ease', minWidth: 'fit-content' }}>
                        <span style={{ fontSize: '12px', fontWeight: '600', color: '#fff', whiteSpace: 'nowrap' }}>{rule.total_alerts}</span>
                      </div>
                    </div>
                    <div style={{ minWidth: '50px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500', textAlign: 'right' }}>
                      {percentage.toFixed(1)}%
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>

      {/* Top Matched Users & Departments - Side by Side, filterable by action */}
      <div className="dashboard-grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <TopBreakdownCard
          dimension="user"
          title={t('dashboard.topMatchedUsers')}
          limit={3}
          barColors={['#8b5cf6', '#7c3aed']}
          onDataChange={handleUserData}
        />
        <TopBreakdownCard
          dimension="department"
          title={t('dashboard.topMatchedDepartments')}
          limit={3}
          barColors={['#10b981', '#059669']}
          onDataChange={handleDeptData}
        />
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
                color: 'white'
              }}><ShieldAlert size={24} /></div>
              <div>
                <h2 style={{ margin: 0, color: 'var(--text-primary)', fontSize: '16px', fontWeight: '600' }}>{t('dashboard.potentialExfiltration')}</h2>
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

          {/* Accordion List */}\r
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', opacity: highImpactLoading ? 0.5 : 1, transition: 'opacity 0.3s', pointerEvents: highImpactLoading ? 'none' : 'auto' }}>
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
                      <ChevronRight size={18} style={{
                        transition: 'transform 0.2s',
                        transform: isExpanded ? 'rotate(90deg)' : 'rotate(0deg)',
                        color: 'var(--text-muted)'
                      }} />

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
                        {alert.user_email}
                      </div>

                      <div style={{ fontSize: '13px', color: 'var(--text-muted)', display: 'flex', gap: '16px', alignItems: 'center' }}>
                        <span><strong style={{ color: 'var(--text-primary)' }}>{alert.max_max_matches}</strong> {t('dashboard.matches')}</span>
                        <span>{t('dashboard.score')}: <strong style={{ color: 'var(--text-primary)' }}>{Math.round(alert.daily_risk_score)}</strong></span>
                        <span style={{ color: 'var(--text-secondary)' }}>{alert.highest_risk_date}</span>
                        {alert.is_single_day_event && (
                          <span style={{ color: '#f59e0b', fontWeight: '500', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                            <Zap size={14} /> {t('dashboard.singleDay')}
                          </span>
                        )}
                      </div>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation()
                        router.push(`/investigation?user=${encodeURIComponent(alert.user_email)}`)
                      }}
                      style={{
                        padding: '8px 16px',
                        borderRadius: '8px',
                        border: '1px solid rgba(99, 102, 241, 0.3)',
                        background: 'rgba(99, 102, 241, 0.1)',
                        color: '#6366f1',
                        fontSize: '13px',
                        fontWeight: '600',
                        cursor: 'pointer',
                        whiteSpace: 'nowrap',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        transition: 'all 0.2s'
                      }}
                      onMouseEnter={(e) => { e.currentTarget.style.background = 'rgba(99, 102, 241, 0.2)' }}
                      onMouseLeave={(e) => { e.currentTarget.style.background = 'rgba(99, 102, 241, 0.1)' }}
                    >
                      <Search size={14} /> {t('dashboard.investigate')}
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
                          <h4 style={{ margin: '0 0 8px 0', fontSize: '15px', color: 'var(--text-primary)', fontWeight: '600', display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <FileText size={18} style={{ color: '#6366f1' }} /> {t('dashboard.incidentDetails')} — {alert.highest_risk_date}
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
          {highImpactPagination.totalPages > 1 && (
            <Pagination
              currentPage={highImpactPagination.page}
              totalPages={highImpactPagination.totalPages}
              totalItems={highImpactPagination.totalCount}
              onPageChange={async (newPage: number) => {
                if (highImpactLoading) return
                setHighImpactLoading(true)
                try {
                  const apiUrl = getApiUrlDynamic()
                  const res = await axios.get(`${apiUrl}/api/risk-trends/high-impact-alerts`, {
                    params: { days: 30, minMaxMatches: 100, minDailyRiskScore: 80, page: newPage, pageSize: 20 }
                  })
                  const data = res.data as HighImpactAlertsResponse
                  setHighImpactAlerts(data?.data || [])
                  if (data?.pagination) {
                    setHighImpactPagination(data.pagination)
                  }
                  setExpandedAlerts(new Set())
                } catch (error) {
                  console.error('Error fetching high impact alerts page:', error)
                } finally {
                  setHighImpactLoading(false)
                }
              }}
              labels={{
                totalItems: t('pagination.totalItems'),
              }}
            />
          )}
        </div>
      )}

      {/* PDF record of the four summary grids - bottom right of the page */}
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '8px', marginTop: '8px', marginBottom: '24px' }}>
        {pdfError && (
          <span style={{ fontSize: '12px', color: '#ef4444' }}>{pdfError}</span>
        )}
        <button
          onClick={downloadDashboardPdf}
          disabled={pdfLoading}
          style={{
            padding: '10px 18px',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            background: pdfLoading ? 'var(--surface-hover)' : 'var(--surface)',
            color: pdfLoading ? 'var(--text-muted)' : 'var(--text-primary)',
            fontSize: '13px',
            fontWeight: '600',
            cursor: pdfLoading ? 'wait' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            transition: 'all 0.2s',
            boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
          }}
          onMouseEnter={(e) => { if (!pdfLoading) e.currentTarget.style.background = 'var(--surface-hover)' }}
          onMouseLeave={(e) => { if (!pdfLoading) e.currentTarget.style.background = 'var(--surface)' }}
        >
          <FileDown size={16} style={{ color: '#ef4444' }} />
          {pdfLoading ? t('dashboard.pdfReportPreparing') : t('dashboard.pdfReport')}
        </button>
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
          font-size: 20px;
          font-weight: 600;
          color: var(--text-primary);
          margin: 0 0 4px 0;
          letter-spacing: -0.01em;
        }

        .dashboard-subtitle {
          font-size: 13px;
          color: var(--text-muted);
          margin: 0;
        }

        .dashboard-filters {
          display: flex;
          gap: 12px;
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
          color: var(--text-muted);
          font-weight: 500;
        }

        .filter-input,
        .filter-select {
          padding: 8px 12px;
          border: 1px solid var(--border);
          border-radius: 8px;
          font-size: 14px;
          background: var(--surface);
          color: var(--text-primary);
          min-width: 140px;
          transition: all 0.2s;
          font-family: 'Inter', sans-serif;
        }

        .filter-input:focus,
        .filter-select:focus {
          outline: none;
          border-color: var(--text-muted);
          box-shadow: 0 0 0 3px rgba(15, 23, 42, 0.06);
        }

        .card {
          background: var(--surface);
          border-radius: 16px;
          padding: 24px;
          box-shadow: 0 4px 20px rgba(15, 23, 42, 0.06);
          border: none;
          margin-bottom: 24px;
          transition: box-shadow 0.2s;
        }

        .card:hover {
          box-shadow: 0 4px 12px -1px rgba(15, 23, 42, 0.08);
        }

        .card h2 {
          margin: 0 0 16px 0;
          color: var(--text-primary);
          font-size: 16px;
          font-weight: 600;
          letter-spacing: -0.01em;
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
          font-size: 13px;
          color: var(--text-muted);
        }

        .data-table {
          width: 100%;
          border-collapse: separate;
          border-spacing: 0;
        }

        .card {
          overflow-x: auto;
        }

        .data-table th {
          background: var(--surface-hover);
          padding: 10px 16px;
          text-align: left;
          font-size: 12px;
          font-weight: 600;
          color: var(--text-muted);
          text-transform: none;
          letter-spacing: 0;
          border-bottom: 1px solid var(--border);
        }

        .data-table td {
          padding: 10px 16px;
          border-bottom: 1px solid var(--border);
          font-size: 14px;
          color: var(--text-primary);
        }

        .data-table tr:hover {
          background: var(--surface-hover);
        }

        .data-table tr:last-child td {
          border-bottom: none;
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
          color: var(--text-muted);
          padding: 40px !important;
        }

        .chart-header {
          margin-bottom: 16px;
        }

        .chart-subtitle {
          font-size: 13px;
          color: var(--text-muted);
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
            font-size: 18px;
          }

          .dashboard-filters {
            flex-direction: column;
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