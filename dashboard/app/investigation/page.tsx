'use client'

import { useState, useEffect, Suspense } from 'react'
import { useSearchParams } from 'next/navigation'
import apiClient from '@/lib/axios'
import { format } from 'date-fns'
import InvestigationUsersList from '@/components/InvestigationUsersList'
import InvestigationTimeline from '@/components/InvestigationTimeline'
import InvestigationAlertDetails from '@/components/InvestigationAlertDetails'
import UserInsightsModal from '@/components/UserInsightsModal'
import SendMailModal from '@/components/investigation/SendMailModal'
import type { WeeklyFlagUser } from '@/components/investigation/types'
import { useTranslation } from '@/components/LanguageProvider'
import { Zap } from 'lucide-react'

interface TimelineEvent {
  id: number
  timestamp: string
  alert_type: string
  severity: string
  description: string
  tags: string[]
  channel?: string
  action?: string
  destination?: string
  classification?: string[]
  matched_rules?: string[]
  files?: Array<{
    name: string
    size: string
    protected: boolean
    classification: string[]
  }>
  source_application?: string
  email_subject?: string
  recipients?: string
  iob_number?: string
  userEmail?: string
  riskScore?: number
  policy?: string
  fullName?: string
  managerName?: string
  team?: string
  department?: string
  loginName?: string
  emailAddress?: string
  violationTriggers?: string
}

interface UserDirectoryInfo {
  name: string
  email: string
  loginName?: string
  department?: string
  managerName?: string
  managerEmail?: string
  risk: number
  isDirectoryEnriched: boolean
}

export default function InvestigationPage() {
  return (
    <Suspense fallback={<div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', color: 'var(--text-secondary)' }}>...</div>}>
      <InvestigationPageContent />
    </Suspense>
  )
}

function InvestigationPageContent() {
  const { t } = useTranslation()
  const searchParams = useSearchParams()
  const [selectedUser, setSelectedUser] = useState<string>()
  const [selectedUserRiskScore, setSelectedUserRiskScore] = useState<number | null>(null)
  const [selectedEvent, setSelectedEvent] = useState<TimelineEvent>()
  const [activeTab, setActiveTab] = useState<'users' | 'alerts'>('users')
  const [searchQuery, setSearchQuery] = useState('')
  const [initialUserLoaded, setInitialUserLoaded] = useState(false)
  const [filterRisk, setFilterRisk] = useState<string>('all')
  const [filterClassification, setFilterClassification] = useState<string>('all')
  const [userInsightsOpen, setUserInsightsOpen] = useState(false)
  const [mailModalOpen, setMailModalOpen] = useState(false)
  const [selectedUserEvents, setSelectedUserEvents] = useState<TimelineEvent[]>([])
  const [selectedUserDirectory, setSelectedUserDirectory] = useState<UserDirectoryInfo | null>(null)

  // Alerts tab state
  const [alerts, setAlerts] = useState<TimelineEvent[]>([])
  const [alertsLoading, setAlertsLoading] = useState(false)
  const [alertsPage, setAlertsPage] = useState(1)
  const [alertsTotal, setAlertsTotal] = useState(0)
  const alertsPageSize = 2000

  // Read user from URL query parameter on initial load
  useEffect(() => {
    if (!initialUserLoaded) {
      const userFromUrl = searchParams.get('user')
      if (userFromUrl) {
        setSearchQuery(userFromUrl)
        setSelectedUser(userFromUrl)
        // Fetch risk score for URL user
        fetchUserRiskScoreFromUrl(userFromUrl)
      }
      setInitialUserLoaded(true)
    }
  }, [searchParams, initialUserLoaded])

  // Fetch risk score when user comes from URL
  const fetchUserRiskScoreFromUrl = async (userEmail: string) => {
    try {
      const response = await apiClient.get('/api/risk/user-list', {
        params: { page: 1, page_size: 5000 }
      })
      const user = response.data.users?.find((u: any) => u.user_email === userEmail)
      setSelectedUserRiskScore(user?.risk_score ?? 0)
    } catch (error) {
      console.error('Error fetching user risk score:', error)
      setSelectedUserRiskScore(0)
    }
  }

  const handleEventSelect = (event: TimelineEvent) => {
    // Extract IOB number from IOBs array if available (from API)
    const iobNumber = (event as any).iobs && Array.isArray((event as any).iobs) && (event as any).iobs.length > 0
      ? (event as any).iobs[0].replace('IOB-', '').replace('IoB-', '')
      : event.iob_number || undefined

    // Use DataType from API if available, otherwise infer from tags
    const dataType = (event as any).data_type || (event as any).dataType || event.alert_type
    const classification = dataType
      ? (dataType === 'PII' || dataType === 'PCI' || dataType === 'CCN' ? [dataType] : [])
      : (event.tags && event.tags.includes('Data exfiltration')
        ? ['PCI', 'CCN', 'PII']
        : event.classification || [])

    // Use IOBs from API if available, otherwise use policy as matched rule
    const matchedRules = (event as any).iobs && Array.isArray((event as any).iobs) && (event as any).iobs.length > 0
      ? (event as any).iobs.map((iob: string) => `NEO ${iob} ${event.description}`)
      : (event as any).policy
        ? [(event as any).policy]
        : event.matched_rules || []

    // Enrich event with additional details from API - only use real data, no mock data
    const enrichedEvent: TimelineEvent = {
      ...event,
      // Only set destination if it exists in event data, otherwise leave undefined
      destination: event.destination || undefined,
      classification: classification,
      matched_rules: matchedRules,
      // Only set source_application if it exists in event data, otherwise leave undefined
      source_application: event.source_application || undefined,
      // Only set email_subject if it exists in event data, otherwise leave undefined
      email_subject: event.email_subject || undefined,
      // Only set recipients if it exists in event data, otherwise leave undefined
      recipients: event.recipients || undefined,
      iob_number: iobNumber,
      // Only show files if they exist in event data, otherwise leave undefined (no mock files)
      files: event.files || undefined
    }
    setSelectedEvent(enrichedEvent)
  }

  // Handle automatic selection of first event when timeline loads
  const handleEventsLoaded = (events: TimelineEvent[]) => {
    setSelectedUserEvents(events)
    // Only auto-select if no event is currently selected
    if (!selectedEvent && events.length > 0) {
      handleEventSelect(events[0])
    }
  }

  // Reset selected event when user changes
  useEffect(() => {
    setSelectedEvent(undefined)
    setSelectedUserEvents([])
    setSelectedUserDirectory(null)
    setMailModalOpen(false)
  }, [selectedUser])

  const selectedMailEvents = selectedEvent
    ? [
        selectedEvent,
        ...selectedUserEvents.filter(event => String(event.id) !== String(selectedEvent.id)),
      ]
    : selectedUserEvents
  const directoryEvent = selectedMailEvents.find(event =>
    event.fullName || event.emailAddress || event.team || event.department || event.managerName || event.loginName
  )

  const selectedUserMailData: WeeklyFlagUser | null = selectedUser ? {
    user_email: selectedUser,
    full_name: selectedUserDirectory?.name || directoryEvent?.fullName || null,
    team: selectedUserDirectory?.department || directoryEvent?.team || directoryEvent?.department || null,
    contact_email: selectedUserDirectory?.email || directoryEvent?.emailAddress || selectedUser,
    trigger_count: selectedUserEvents.length,
    first_seen: selectedUserEvents.length > 0
      ? selectedUserEvents.reduce((earliest, event) => event.timestamp < earliest ? event.timestamp : earliest, selectedUserEvents[0].timestamp)
      : new Date().toISOString(),
    last_seen: selectedMailEvents.length > 0
      ? selectedMailEvents[0].timestamp
      : new Date().toISOString(),
    sample_incidents: selectedMailEvents.slice(0, 10).map(event => ({
      timestamp: event.timestamp,
      policy: event.policy || event.matched_rules?.[0] || null,
      max_matches: (event as any).max_matches || (event as any).maxMatches || 0,
      destination: event.destination || null,
      channel: event.channel || null,
    })),
  } : null

  // Fetch alerts when Alerts tab is active
  useEffect(() => {
    if (activeTab === 'alerts') {
      fetchAlerts()
    }
  }, [activeTab, alertsPage, searchQuery, filterRisk])

  const fetchAlerts = async () => {
    setAlertsLoading(true)
    try {
      const params: any = {
        limit: alertsPageSize,
        order_by: 'timestamp_desc'
      }

      if (searchQuery) {
        params.user = searchQuery
      }

      const response = await apiClient.get('/api/incidents', { params })
      const incidents = Array.isArray(response.data) ? response.data : []

      // Calculate risk score from severity if not provided
      const calculateRiskScore = (incident: any): number => {
        if (incident.risk_score != null) return incident.risk_score
        if (incident.riskScore != null) return incident.riskScore
        // Fallback: calculate from severity (1-5 scale to 0-100)
        const severity = incident.severity || 1
        return Math.min(100, severity * 20)
      }

      // Transform incidents to TimelineEvent format
      const timelineEvents: TimelineEvent[] = incidents.map((incident: any) => ({
        id: incident.id,
        timestamp: incident.timestamp,
        alert_type: incident.dataType || incident.data_type || 'Unknown',
        severity: incident.severity >= 4 ? 'High' : incident.severity >= 3 ? 'Medium' : 'Low',
        description: getDescription(incident),
        tags: getTags(incident),
        channel: incident.channel,
        action: incident.action || 'Permit',
        destination: incident.destination,
        dataType: incident.dataType || incident.data_type,
        iobs: incident.ioBs || incident.iobs || [],
        policy: incident.policy,
        violationTriggers: incident.violation_triggers || incident.violationTriggers,
        riskLevel: incident.risk_level || incident.riskLevel,
        riskScore: calculateRiskScore(incident),
        userEmail: incident.user_email || incident.userEmail,
        fullName: incident.fullName || incident.full_name,
        managerName: incident.managerName || incident.manager_name,
        team: incident.team,
        department: incident.department,
        loginName: incident.loginName || incident.login_name,
        emailAddress: incident.emailAddress || incident.email_address
      }))

      // Apply risk filter
      let filteredEvents = timelineEvents
      if (filterRisk !== 'all') {
        filteredEvents = timelineEvents.filter(event => {
          const score = event.riskScore || 0
          if (filterRisk === 'critical' && score >= 80) return true
          if (filterRisk === 'high' && score >= 50 && score < 80) return true
          if (filterRisk === 'medium' && score >= 30 && score < 50) return true
          if (filterRisk === 'low' && score < 30) return true
          return false
        })
      }

      setAlerts(filteredEvents)
      setAlertsTotal(filteredEvents.length)
    } catch (error) {
      console.error('Error fetching alerts:', error)
      setAlerts([])
      setAlertsTotal(0)
    } finally {
      setAlertsLoading(false)
    }
  }

  const getDescription = (incident: any): string => {
    if (incident.channel === 'Email' && incident.data_type) {
      return `Email sent to ${incident.data_type}`
    }
    if (incident.channel === 'Removable Storage') {
      return 'Suspicious number of files copied to removable storage'
    }
    if (incident.policy) {
      return incident.policy
    }
    return `Security incident detected`
  }

  const getTags = (incident: any): string[] => {
    const tags: string[] = []
    if (incident.data_type === 'PII' || incident.data_type === 'PCI' || incident.data_type === 'CCN') {
      tags.push('Data exfiltration')
    }
    if (incident.severity >= 4) {
      tags.push('High severity')
    }
    return tags
  }

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)' }}>
      {/* Header */}
      <div style={{ background: 'var(--surface)', borderBottom: '1px solid var(--border)', padding: '16px 24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <h1 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)' }}>{t('investigation.title')}</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', border: '1px solid var(--border)', borderRadius: '8px', padding: '6px 12px', background: 'var(--surface)' }}>
              <button
                onClick={() => setActiveTab('users')}
                style={{
                  padding: '6px 16px',
                  borderRadius: '6px',
                  fontSize: '14px',
                  fontWeight: '500',
                  transition: 'all 0.2s',
                  background: activeTab === 'users' ? 'var(--primary)' : 'transparent',
                  color: activeTab === 'users' ? 'white' : 'var(--text-secondary)',
                  border: 'none',
                  cursor: 'pointer'
                }}
                onMouseEnter={(e) => {
                  if (activeTab !== 'users') {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }
                }}
                onMouseLeave={(e) => {
                  if (activeTab !== 'users') {
                    e.currentTarget.style.color = 'var(--text-secondary)'
                  }
                }}
              >
                {t('investigation.users')}
              </button>
              <button
                onClick={() => setActiveTab('alerts')}
                style={{
                  padding: '6px 16px',
                  borderRadius: '6px',
                  fontSize: '14px',
                  fontWeight: '500',
                  transition: 'all 0.2s',
                  background: activeTab === 'alerts' ? 'var(--primary)' : 'transparent',
                  color: activeTab === 'alerts' ? 'white' : 'var(--text-secondary)',
                  border: 'none',
                  cursor: 'pointer'
                }}
                onMouseEnter={(e) => {
                  if (activeTab !== 'alerts') {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }
                }}
                onMouseLeave={(e) => {
                  if (activeTab !== 'alerts') {
                    e.currentTarget.style.color = 'var(--text-secondary)'
                  }
                }}
              >
                {t('investigation.alerts')}
              </button>
            </div>
            <div style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
              {format(new Date(), 'HH:mm dd-MMM-yyyy')}
            </div>
          </div>
        </div>
      </div>

      {/* Main Content - Three Column Layout */}
      <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr 400px', height: 'calc(100vh - 73px)' }}>
        {/* Left Panel - Users List or Alerts List */}
        <div style={{ background: 'var(--surface)', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', overflow: 'hidden', minHeight: 0 }}>
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)' }}>
            <h2 style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '12px' }}>
              {activeTab === 'users' ? t('investigation.title') : `${t('investigation.alerts')} (${alertsTotal})`}
            </h2>
            <input
              type="text"
              placeholder={activeTab === 'users' ? t('investigation.searchUsers') : t('investigation.searchByEmail')}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px',
                background: 'var(--background)',
                color: 'var(--text-primary)',
                outline: 'none'
              }}
              onFocus={(e) => {
                e.target.style.borderColor = 'var(--primary)'
                e.target.style.boxShadow = '0 0 0 2px rgba(0, 168, 232, 0.2)'
              }}
              onBlur={(e) => {
                e.target.style.borderColor = 'var(--border)'
                e.target.style.boxShadow = 'none'
              }}
            />
            <div style={{ marginTop: '12px' }}>
              <select
                value={filterRisk}
                onChange={(e) => setFilterRisk(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  border: '1px solid var(--border)',
                  borderRadius: '6px',
                  fontSize: '14px',
                  background: 'var(--background)',
                  color: 'var(--text-primary)',
                  outline: 'none'
                }}
                onFocus={(e) => {
                  e.currentTarget.style.borderColor = 'var(--primary)'
                  e.currentTarget.style.boxShadow = '0 0 0 2px rgba(0, 168, 232, 0.2)'
                }}
                onBlur={(e) => {
                  e.currentTarget.style.borderColor = 'var(--border)'
                  e.currentTarget.style.boxShadow = 'none'
                }}
              >
                <option value="all">{t('investigation.allRiskLevels')}</option>
                <option value="critical">{t('investigation.critical')}</option>
                <option value="high">{t('investigation.high')}</option>
                <option value="medium">{t('investigation.medium')}</option>
                <option value="low">{t('investigation.low')}</option>
              </select>
            </div>
          </div>

          {activeTab === 'users' ? (
            <InvestigationUsersList
              onUserSelect={(email, riskScore) => {
                setSelectedUser(email)
                setSelectedUserRiskScore(riskScore)
              }}
              selectedUser={selectedUser}
              searchQuery={searchQuery}
              filterRisk={filterRisk}
            />
          ) : (
            <div style={{ flex: 1, overflowY: 'auto' }}>
              {alertsLoading ? (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
                  {t('investigation.loadingAlerts')}
                </div>
              ) : alerts.length === 0 ? (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
                  {t('investigation.noAlerts')}
                </div>
              ) : (
                <>
                  <div style={{ padding: '12px 16px', background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '60px 1fr 80px', gap: '12px' }}>
                      <span>{t('investigation.risk')}</span>
                      <span>{t('common.user')}</span>
                      <span style={{ textAlign: 'right' }}>{t('investigation.time')}</span>
                    </div>
                  </div>
                  {alerts.map((alert) => {
                    const riskScore = alert.riskScore || 0
                    const getRiskColor = (score: number): string => {
                      if (score >= 80) return '#ef4444'
                      if (score >= 50) return '#f59e0b'
                      if (score >= 30) return '#fbbf24'
                      return '#10b981'
                    }
                    return (
                      <div
                        key={alert.id}
                        onClick={() => handleEventSelect(alert)}
                        style={{
                          display: 'grid',
                          gridTemplateColumns: '60px 1fr 80px',
                          gap: '12px',
                          padding: '12px 16px',
                          cursor: 'pointer',
                          borderBottom: '1px solid var(--border)',
                          background: selectedEvent?.id === alert.id ? 'rgba(0, 168, 232, 0.1)' : 'transparent',
                          borderLeft: selectedEvent?.id === alert.id ? '4px solid var(--primary)' : 'none',
                          transition: 'all 0.2s'
                        }}
                        onMouseEnter={(e) => {
                          if (selectedEvent?.id !== alert.id) {
                            e.currentTarget.style.background = 'var(--surface-hover)'
                          }
                        }}
                        onMouseLeave={(e) => {
                          if (selectedEvent?.id !== alert.id) {
                            e.currentTarget.style.background = 'transparent'
                          }
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                          <div style={{ position: 'relative', width: '32px', height: '32px' }}>
                            <svg style={{ width: '32px', height: '32px', transform: 'rotate(-90deg)' }}>
                              <circle cx="16" cy="16" r="14" fill="none" stroke="var(--border)" strokeWidth="3" />
                              <circle
                                cx="16"
                                cy="16"
                                r="14"
                                fill="none"
                                stroke={getRiskColor(riskScore)}
                                strokeWidth="3"
                                strokeDasharray={`${(riskScore / 100) * 87.96} 87.96`}
                                strokeLinecap="round"
                              />
                            </svg>
                            <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                              <span style={{ fontSize: '10px', fontWeight: 'bold', color: getRiskColor(riskScore) }}>
                                {riskScore}
                              </span>
                            </div>
                          </div>
                        </div>
                        <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center', minWidth: 0 }}>
                          <span style={{ fontSize: '13px', color: 'var(--text-primary)', fontWeight: '500', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {alert.userEmail || 'Unknown'}
                          </span>
                          <span style={{ fontSize: '11px', color: 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {alert.description}
                          </span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', fontSize: '11px', color: 'var(--text-secondary)' }}>
                          {format(new Date(alert.timestamp), 'HH:mm')}
                        </div>
                      </div>
                    )
                  })}
                  {alertsTotal > alertsPageSize && (
                    <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', background: 'var(--background-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)' }}>
                      <button
                        onClick={() => setAlertsPage(p => Math.max(1, p - 1))}
                        disabled={alertsPage === 1}
                        style={{
                          padding: '4px 12px',
                          border: '1px solid var(--border)',
                          borderRadius: '6px',
                          background: 'var(--surface)',
                          color: 'var(--text-primary)',
                          cursor: alertsPage === 1 ? 'not-allowed' : 'pointer',
                          opacity: alertsPage === 1 ? 0.5 : 1
                        }}
                      >
                        {t('investigation.previous')}
                      </button>
                      <span>{t('investigation.page')} {alertsPage}</span>
                      <button
                        onClick={() => setAlertsPage(p => p + 1)}
                        disabled={alerts.length < alertsPageSize}
                        style={{
                          padding: '4px 12px',
                          border: '1px solid var(--border)',
                          borderRadius: '6px',
                          background: 'var(--surface)',
                          color: 'var(--text-primary)',
                          cursor: alerts.length < alertsPageSize ? 'not-allowed' : 'pointer',
                          opacity: alerts.length < alertsPageSize ? 0.5 : 1
                        }}
                      >
                        {t('investigation.next')}
                      </button>
                    </div>
                  )}
                </>
              )}
            </div>
          )}
        </div>

        {/* Center Panel - Timeline or Alerts Timeline */}
        <div style={{ background: 'var(--surface)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)' }}>
                {activeTab === 'users' ? t('investigation.timeline') : t('investigation.alertsTimeline')}
              </h2>
              {activeTab === 'users' && selectedUser && (
                <button
                  onClick={() => setSelectedUser(undefined)}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: 'var(--text-muted)',
                    fontSize: '20px',
                    lineHeight: '1',
                    cursor: 'pointer',
                    padding: '4px'
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.color = 'var(--text-muted)'
                  }}
                >
                  ×
                </button>
              )}
              {activeTab === 'alerts' && selectedEvent && (
                <button
                  onClick={() => setSelectedEvent(undefined)}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: 'var(--text-muted)',
                    fontSize: '20px',
                    lineHeight: '1',
                    cursor: 'pointer',
                    padding: '4px'
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.color = 'var(--text-muted)'
                  }}
                >
                  ×
                </button>
              )}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <button
                style={{
                  padding: '8px',
                  borderRadius: '6px',
                  background: 'transparent',
                  border: 'none',
                  color: 'var(--text-secondary)',
                  cursor: 'pointer',
                  transition: 'all 0.2s'
                }}
                title="Filter"
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'var(--surface-hover)'
                  e.currentTarget.style.color = 'var(--text-primary)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'transparent'
                  e.currentTarget.style.color = 'var(--text-secondary)'
                }}
              >
                <Zap size={14} />
              </button>
              <button
                style={{
                  padding: '8px',
                  borderRadius: '6px',
                  background: 'transparent',
                  border: 'none',
                  color: 'var(--text-secondary)',
                  cursor: 'pointer',
                  transition: 'all 0.2s'
                }}
                title="Sort"
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'var(--surface-hover)'
                  e.currentTarget.style.color = 'var(--text-primary)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'transparent'
                  e.currentTarget.style.color = 'var(--text-secondary)'
                }}
              >
                ⇅
              </button>
            </div>
          </div>

          {activeTab === 'users' ? (
            <InvestigationTimeline
              userEmail={selectedUser}
              userRiskScore={selectedUserRiskScore}
              onEventSelect={handleEventSelect}
              selectedEventId={selectedEvent?.id}
              onEventsLoaded={handleEventsLoaded}
              onUserDirectoryLoaded={setSelectedUserDirectory}
              onUserInsightsClick={() => setUserInsightsOpen(true)}
              onSendMailClick={() => setMailModalOpen(true)}
            />
          ) : (
            <div style={{ flex: 1, overflowY: 'auto', padding: '16px' }}>
              {alertsLoading ? (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
                  {t('investigation.loadingTimeline')}
                </div>
              ) : alerts.length === 0 ? (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
                  {t('investigation.noAlerts')}
                </div>
              ) : (
                (() => {
                  // Group alerts by date
                  const groupedAlerts = alerts.reduce((acc, alert) => {
                    const date = format(new Date(alert.timestamp), 'dd-MMM-yyyy')
                    if (!acc[date]) {
                      acc[date] = []
                    }
                    acc[date].push(alert)
                    return acc
                  }, {} as Record<string, TimelineEvent[]>)

                  const getSeverityColor = (severity: string): string => {
                    switch (severity) {
                      case 'High': return '#ef4444'
                      case 'Medium': return '#f59e0b'
                      case 'Low': return '#10b981'
                      default: return '#6b7280'
                    }
                  }

                  return Object.entries(groupedAlerts).map(([date, dateAlerts]) => (
                    <div key={date} style={{ marginBottom: '24px' }}>
                      <div style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '12px', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>
                        {date} ({dateAlerts.length} {dateAlerts.length === 1 ? 'alert' : 'alerts'})
                      </div>
                      {dateAlerts.map((alert) => (
                        <div
                          key={alert.id}
                          onClick={() => handleEventSelect(alert)}
                          style={{
                            display: 'flex',
                            gap: '16px',
                            padding: '12px',
                            borderRadius: '8px',
                            cursor: 'pointer',
                            marginBottom: '8px',
                            background: selectedEvent?.id === alert.id ? 'rgba(0, 168, 232, 0.1)' : 'transparent',
                            borderLeft: selectedEvent?.id === alert.id ? '4px solid var(--primary)' : 'none',
                            transition: 'all 0.2s'
                          }}
                          onMouseEnter={(e) => {
                            if (selectedEvent?.id !== alert.id) {
                              e.currentTarget.style.background = 'var(--surface-hover)'
                            }
                          }}
                          onMouseLeave={(e) => {
                            if (selectedEvent?.id !== alert.id) {
                              e.currentTarget.style.background = 'transparent'
                            }
                          }}
                        >
                          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: '4px' }}>
                            <div
                              style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: getSeverityColor(alert.severity) }}
                            />
                            <div style={{ width: '2px', height: '100%', background: 'var(--border)', marginTop: '4px' }} />
                          </div>
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                              <span style={{ fontSize: '12px', color: 'var(--text-muted)', fontFamily: 'monospace' }}>
                                {format(new Date(alert.timestamp), 'HH:mm')} UTC
                              </span>
                            </div>
                            <div style={{ fontSize: '14px', color: 'var(--text-primary)', fontWeight: '500', marginBottom: '8px' }}>
                              {alert.description}
                            </div>
                            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                              {alert.tags.map((tag, idx) => (
                                <span
                                  key={idx}
                                  style={{ padding: '2px 8px', borderRadius: '4px', fontSize: '12px', fontWeight: '500', color: 'white', backgroundColor: '#3b82f6' }}
                                >
                                  {tag}
                                </span>
                              ))}
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  ))
                })()
              )}
            </div>
          )}
        </div>

        {/* Right Panel - Alert Details */}
        <div style={{ background: 'var(--surface)', borderLeft: '1px solid var(--border)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)' }}>Alert details</h2>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                {selectedEvent && (
                  <>
                    <span style={{
                      padding: '4px 8px',
                      borderRadius: '4px',
                      fontSize: '12px',
                      fontWeight: '600',
                      background: selectedEvent.severity === 'High'
                        ? 'rgba(217, 83, 79, 0.2)'
                        : selectedEvent.severity === 'Medium'
                          ? 'rgba(240, 173, 78, 0.2)'
                          : 'rgba(92, 184, 92, 0.2)',
                      color: selectedEvent.severity === 'High'
                        ? '#d9534f'
                        : selectedEvent.severity === 'Medium'
                          ? '#f0ad4e'
                          : '#5cb85c'
                    }}>
                      ⚡ {selectedEvent.severity}
                    </span>
                    <button
                      style={{
                        padding: '4px',
                        borderRadius: '6px',
                        background: 'transparent',
                        border: 'none',
                        color: 'var(--text-secondary)',
                        cursor: 'pointer'
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.background = 'var(--surface-hover)'
                        e.currentTarget.style.color = 'var(--text-primary)'
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.background = 'transparent'
                        e.currentTarget.style.color = 'var(--text-secondary)'
                      }}
                    >
                      ▶
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>

          <InvestigationAlertDetails event={selectedEvent} />
        </div>
      </div>

      {/* User Insights Modal */}
      <UserInsightsModal
        isOpen={userInsightsOpen}
        onClose={() => setUserInsightsOpen(false)}
        userEmail={selectedUser || ''}
        userName={selectedUserDirectory?.name || directoryEvent?.fullName}
      />

      {mailModalOpen && selectedUserMailData && (
        <SendMailModal
          user={selectedUserMailData}
          onClose={() => setMailModalOpen(false)}
        />
      )}
    </div>
  )
}
