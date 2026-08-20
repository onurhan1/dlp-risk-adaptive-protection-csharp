'use client'

import { useState, useEffect } from 'react'
import type { ReactNode } from 'react'
import { format } from 'date-fns'
import { BarChart3, Building2, Mail, ShieldCheck, UserRound, UserRoundCheck } from 'lucide-react'

import apiClient from '@/lib/axios'
import { useTranslation } from '@/components/LanguageProvider'

interface TimelineEvent {
  id: number
  timestamp: string
  alert_type: string
  severity: string
  description: string
  tags: string[]
  channel?: string
  action?: string
  // Additional fields from API
  dataType?: string
  iobs?: string[]
  policy?: string
  riskLevel?: string
  riskScore?: number
  maxMatches?: number
  // New extended fields
  destination?: string
  fileName?: string
  loginName?: string
  emailAddress?: string
  fullName?: string
  managerName?: string
  team?: string
  department?: string
  violationTriggers?: string
}

interface UserDirectoryInfo {
  name: string
  email: string
  loginName?: string
  department?: string
  managerName?: string
  managerEmail?: string
  gender?: string | null
  risk: number
  isDirectoryEnriched: boolean
}

function firstText(...values: unknown[]) {
  for (const value of values) {
    if (typeof value === 'string' && value.trim().length > 0) {
      return value.trim()
    }
  }
  return ''
}

function buildNameFromEmail(email: string) {
  return email
    .split('@')[0]
    .split(/[._-]/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ') || email
}

interface InvestigationTimelineProps {
  userEmail?: string
  userRiskScore?: number | null
  onEventSelect: (event: TimelineEvent) => void
  selectedEventId?: number
  onEventsLoaded?: (events: TimelineEvent[]) => void
  onUserDirectoryLoaded?: (info: UserDirectoryInfo | null) => void
  onUserInsightsClick?: () => void
  onSendMailClick?: () => void
}

export default function InvestigationTimeline({
  userEmail,
  userRiskScore,
  onEventSelect,
  selectedEventId,
  onEventsLoaded,
  onUserDirectoryLoaded,
  onUserInsightsClick,
  onSendMailClick
}: InvestigationTimelineProps) {
  const { t } = useTranslation()
  const [events, setEvents] = useState<TimelineEvent[]>([])
  const [loading, setLoading] = useState(false)
  const [userInfo, setUserInfo] = useState<UserDirectoryInfo | null>(null)
  // Pagination state for timeline events
  const [currentPage, setCurrentPage] = useState(1)
  const eventsPerPage = 20

  useEffect(() => {
    if (userEmail) {
      setCurrentPage(1) // Reset pagination when user changes
      fetchTimeline()
      // Always use risk score from props (from InvestigationUsersList)
      // This ensures consistency between the user list and timeline header
      const riskScore = userRiskScore ?? 0
      fetchUserInfo(riskScore)
    } else {
      setEvents([])
      setUserInfo(null)
      onUserDirectoryLoaded?.(null)
      setCurrentPage(1)
    }
  }, [userEmail, userRiskScore])

  const fetchUserInfo = async (riskScore: number) => {
    if (!userEmail) return

    try {
      const response = await apiClient.get('/api/incidents/user-directory', {
        params: { user: userEmail }
      })

      const data = response.data || {}
      const fullName = firstText(data.full_name, data.fullName)
      const email = firstText(data.email_address, data.emailAddress, data.email, userEmail)
      const loginName = firstText(data.login_name, data.loginName, data.user_name, data.userName)
      const department = firstText(data.team, data.department)
      const managerName = firstText(data.manager_name, data.managerName)
      const managerEmail = firstText(data.manager_email, data.managerEmail)
      const gender = firstText(data.gender)
      const directoryFlag = data.is_directory_enriched ?? data.isDirectoryEnriched
      const info = {
        name: fullName || buildNameFromEmail(userEmail),
        email: email || userEmail,
        loginName: loginName || undefined,
        department: department || undefined,
        managerName: managerName || undefined,
        managerEmail: managerEmail || undefined,
        gender: gender || null,
        risk: riskScore,
        isDirectoryEnriched: typeof directoryFlag === 'boolean'
          ? directoryFlag
          : Boolean(fullName || loginName || department || managerName)
      }

      setUserInfo(info)
      onUserDirectoryLoaded?.(info)
    } catch (error) {
      if (process.env.NODE_ENV !== 'production') {
        console.error('Error fetching user info:', error)
      }

      const info = {
        name: buildNameFromEmail(userEmail),
        email: userEmail,
        risk: riskScore,
        isDirectoryEnriched: false
      }

      setUserInfo(info)
      onUserDirectoryLoaded?.(info)
    }
  }

  const fetchTimeline = async () => {
    if (!userEmail) return

    setLoading(true)
    try {
      const response = await apiClient.get('/api/incidents', {
        params: {
          user: userEmail,
          limit: 200,
          order_by: 'timestamp_desc',
          include_directory: false
        },
        timeout: 45000 // 45 second timeout for larger data loads
      })

      // Check if response.data is valid and not empty
      const incidents = Array.isArray(response.data) ? response.data : []

      // If no incidents found, use fallback sample data
      if (incidents.length === 0) {
        if (process.env.NODE_ENV !== 'production') {
          console.log('No incidents found in API response')
        }
        setEvents([])
        return
      }

      const timelineEvents = incidents.map((incident: any) => ({
        id: incident.id,
        timestamp: incident.timestamp,
        alert_type: incident.data_type || incident.dataType || 'Unknown',
        severity: incident.severity >= 4 ? 'High' : incident.severity >= 3 ? 'Medium' : 'Low',
        description: getDescription(incident),
        tags: getTags(incident),
        channel: incident.channel,
        action: incident.action || incident.recommendedAction || incident.recommended_action || 'Permit',
        // Include additional fields from API for enrichment
        dataType: incident.data_type || incident.dataType,
        iobs: incident.iobs || incident.iOBs || [],
        policy: incident.policy || incident.policy,
        riskLevel: incident.riskLevel || incident.risk_level,
        riskScore: incident.riskScore || incident.risk_score,
        maxMatches: incident.maxMatches ?? incident.max_matches ?? 0,
        // New extended fields
        destination: incident.destination,
        fileName: incident.fileName || incident.file_name,
        loginName: incident.loginName || incident.login_name,
        emailAddress: incident.emailAddress || incident.email_address,
        fullName: incident.fullName || incident.full_name,
        managerName: incident.managerName || incident.manager_name,
        team: incident.team,
        department: incident.department,
        violationTriggers: incident.violationTriggers || incident.violation_triggers,
        // Remediation fields
        isRemediated: incident.isRemediated || incident.is_remediated || false,
        remediatedAt: incident.remediatedAt || incident.remediated_at,
        remediatedBy: incident.remediatedBy || incident.remediated_by,
        remediationAction: incident.remediationAction || incident.remediation_action,
        remediationNotes: incident.remediationNotes || incident.remediation_notes
      }))

      setEvents(timelineEvents)
      // Notify parent component that events are loaded
      if (onEventsLoaded && timelineEvents.length > 0) {
        onEventsLoaded(timelineEvents)
      }
    } catch (error) {
      if (process.env.NODE_ENV !== 'production') {
        console.error('Error fetching timeline:', error)
      }
      setEvents([])
    } finally {
      setLoading(false)
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
    return 'Security incident detected'
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

  const getSeverityColor = (severity: string): string => {
    switch (severity) {
      case 'High': return '#ef4444'
      case 'Medium': return '#f59e0b'
      case 'Low': return '#10b981'
      default: return '#6b7280'
    }
  }

  const getTagColor = (tag: string): string => {
    if (tag === 'Data exfiltration') return '#14b8a6'
    if (tag === 'System modification') return '#10b981'
    if (tag === 'Defense evasion') return '#8b5cf6'
    return '#3b82f6'
  }

  const getRiskColorForScore = (score: number): string => {
    if (score >= 80) return '#ef4444' // Red - Critical
    if (score >= 50) return '#f59e0b' // Orange - High
    if (score >= 30) return '#fbbf24' // Yellow - Medium
    return '#10b981' // Green - Low
  }

  const renderDirectoryField = (label: string, value?: string, icon?: ReactNode) => (
    <div style={{
      minWidth: 0,
      padding: '8px 10px',
      border: '1px solid var(--border)',
      borderRadius: '8px',
      background: 'var(--surface)'
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '4px' }}>
        {icon}
        <span>{label}</span>
      </div>
      <div title={value || '-'} style={{
        fontSize: '13px',
        color: value ? 'var(--text-primary)' : 'var(--text-muted)',
        fontWeight: 600,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap'
      }}>
        {value || '-'}
      </div>
    </div>
  )

  // Pagination calculations
  const totalPages = Math.ceil(events.length / eventsPerPage)
  const paginatedEvents = events.slice((currentPage - 1) * eventsPerPage, currentPage * eventsPerPage)

  // Group paginated events by date
  const groupedEvents = paginatedEvents.reduce((acc, event) => {
    const date = format(new Date(event.timestamp), 'dd-MMM-yyyy')
    if (!acc[date]) {
      acc[date] = []
    }
    acc[date].push(event)
    return acc
  }, {} as Record<string, TimelineEvent[]>)

  if (!userEmail) {
    return (
      <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-muted)' }}>
        <p>{t('timeline.selectUser')}</p>
      </div>
    )
  }

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      {/* User Directory Header */}
      {userInfo && (
        <div style={{ padding: '14px 16px', background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '12px', minWidth: 0, flex: 1 }}>
              <div style={{ position: 'relative', width: '48px', height: '48px' }}>
                <svg style={{ width: '48px', height: '48px', transform: 'rotate(-90deg)' }}>
                  <circle cx="24" cy="24" r="20" fill="none" stroke="var(--border)" strokeWidth="4" />
                  <circle
                    cx="24"
                    cy="24"
                    r="20"
                    fill="none"
                    stroke={getRiskColorForScore(userInfo.risk)}
                    strokeWidth="4"
                    strokeDasharray={`${(userInfo.risk / 100) * 125.6} 125.6`}
                    strokeLinecap="round"
                  />
                </svg>
                <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <span style={{ fontSize: '14px', fontWeight: 'bold', color: getRiskColorForScore(userInfo.risk) }}>
                    {userInfo.risk}
                  </span>
                </div>
              </div>
              <div style={{ minWidth: 0, flex: 1 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', marginBottom: '10px' }}>
                  <h3 style={{ margin: 0, fontWeight: '700', color: 'var(--text-primary)', fontSize: '16px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{userInfo.name}</h3>
                  <span style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '3px 8px',
                    borderRadius: '999px',
                    background: userInfo.isDirectoryEnriched ? 'rgba(16, 185, 129, 0.12)' : 'rgba(148, 163, 184, 0.14)',
                    color: userInfo.isDirectoryEnriched ? '#059669' : 'var(--text-secondary)',
                    fontSize: '11px',
                    fontWeight: 700
                  }}>
                    <ShieldCheck size={12} /> {userInfo.isDirectoryEnriched ? 'LDAP' : 'Fallback'}
                  </span>
                </div>
                <div style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
                  gap: '8px'
                }}>
                  {renderDirectoryField('E-posta', userInfo.email, <Mail size={12} />)}
                  {renderDirectoryField('Login', userInfo.loginName, <UserRound size={12} />)}
                  {renderDirectoryField('Ekip / Departman', userInfo.department, <Building2 size={12} />)}
                  {renderDirectoryField('Manager', userInfo.managerName, <UserRoundCheck size={12} />)}
                </div>
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexShrink: 0 }}>
              {onSendMailClick && (
                <button
                  onClick={onSendMailClick}
                  style={{
                    padding: '6px 12px',
                    background: 'transparent',
                    color: 'var(--primary)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    fontWeight: '500',
                    border: '1px solid var(--primary)',
                    cursor: 'pointer',
                    transition: 'all 0.2s',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px'
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = 'rgba(0, 168, 232, 0.1)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = 'transparent'
                  }}
                >
                  <Mail size={14} />
                  {t('investigation.sendMail')}
                </button>
              )}
              <button
                onClick={() => {
                  if (onUserInsightsClick) {
                    onUserInsightsClick()
                  }
                }}
                style={{
                  padding: '6px 12px',
                  background: 'rgba(0, 168, 232, 0.1)',
                  color: 'var(--primary)',
                  borderRadius: '6px',
                  fontSize: '14px',
                  fontWeight: '500',
                  border: 'none',
                  cursor: 'pointer',
                  transition: 'all 0.2s',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'rgba(0, 168, 232, 0.2)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'rgba(0, 168, 232, 0.1)'
                }}
              >
                <BarChart3 size={14} />
                {t('timeline.userInsights')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Timeline Content */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px' }}>
        {loading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            {t('timeline.loading')}
          </div>
        ) : Object.keys(groupedEvents).length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            {t('timeline.noEvents')}
          </div>
        ) : (
          <>
            {Object.entries(groupedEvents).map(([date, dateEvents]) => (
              <div key={date} style={{ marginBottom: '24px' }}>
                <div style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '12px', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>
                  {date} ({dateEvents.length} {dateEvents.length === 1 ? 'alert' : 'alerts'})
                </div>

                {dateEvents.map((event, eventIdx) => (
                  <div
                    key={`${event.id}-${event.timestamp}-${eventIdx}`}
                    onClick={() => onEventSelect(event)}
                    style={{
                      display: 'flex',
                      gap: '16px',
                      padding: '12px',
                      borderRadius: '8px',
                      cursor: 'pointer',
                      transition: 'all 0.2s',
                      marginBottom: '8px',
                      background: selectedEventId === event.id ? 'rgba(0, 168, 232, 0.1)' : 'transparent',
                      borderLeft: selectedEventId === event.id ? '4px solid var(--primary)' : 'none'
                    }}
                    onMouseEnter={(e) => {
                      if (selectedEventId !== event.id) {
                        e.currentTarget.style.background = 'var(--surface-hover)'
                      }
                    }}
                    onMouseLeave={(e) => {
                      if (selectedEventId !== event.id) {
                        e.currentTarget.style.background = 'transparent'
                      }
                    }}
                  >
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: '4px' }}>
                      <div
                        style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: getSeverityColor(event.severity as any) }}
                      />
                      <div style={{ width: '2px', height: '100%', background: 'var(--border)', marginTop: '4px' }} />
                    </div>

                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                        <span style={{ fontSize: '12px', color: 'var(--text-muted)', fontFamily: 'monospace' }}>
                          {format(new Date(event.timestamp), 'HH:mm')} UTC
                        </span>
                      </div>
                      <div style={{ fontSize: '14px', color: 'var(--text-primary)', fontWeight: '500', marginBottom: '8px' }}>
                        {event.description}
                      </div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                        {event.tags.map((tag, idx) => (
                          <span
                            key={idx}
                            style={{ padding: '2px 8px', borderRadius: '4px', fontSize: '12px', fontWeight: '500', color: 'white', backgroundColor: getTagColor(tag) }}
                          >
                            {tag}
                          </span>
                        ))}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ))}

            {events.length > eventsPerPage && (
              <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '16px', marginTop: '24px', paddingBottom: '16px' }}>
                <button
                  onClick={(e) => { e.stopPropagation(); setCurrentPage(p => Math.max(1, p - 1)); }}
                  disabled={currentPage === 1}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '6px',
                    border: '1px solid var(--border)',
                    background: currentPage === 1 ? 'var(--surface)' : 'var(--background)',
                    color: currentPage === 1 ? 'var(--text-muted)' : 'var(--text-primary)',
                    cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
                    fontSize: '12px'
                  }}
                >
                  {t('timeline.previous')}
                </button>
                <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>
                  {t('pagination.page')} {currentPage} {t('pagination.of')} {totalPages}
                </span>
                <button
                  onClick={(e) => { e.stopPropagation(); setCurrentPage(p => Math.min(totalPages, p + 1)); }}
                  disabled={currentPage === totalPages}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '6px',
                    border: '1px solid var(--border)',
                    background: currentPage === totalPages ? 'var(--surface)' : 'var(--background)',
                    color: currentPage === totalPages ? 'var(--text-muted)' : 'var(--text-primary)',
                    cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
                    fontSize: '12px'
                  }}
                >
                  {t('timeline.next')}
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
