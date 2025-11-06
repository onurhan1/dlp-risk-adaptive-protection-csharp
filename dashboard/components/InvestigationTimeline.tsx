'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format } from 'date-fns'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

interface TimelineEvent {
  id: number
  timestamp: string
  alert_type: string
  severity: string
  description: string
  tags: string[]
  channel?: string
  action?: string
}

interface InvestigationTimelineProps {
  userEmail?: string
  userRiskScore?: number | null
  onEventSelect: (event: TimelineEvent) => void
  selectedEventId?: number
}

export default function InvestigationTimeline({
  userEmail,
  userRiskScore,
  onEventSelect,
  selectedEventId
}: InvestigationTimelineProps) {
  const [events, setEvents] = useState<TimelineEvent[]>([])
  const [loading, setLoading] = useState(false)
  const [userInfo, setUserInfo] = useState<{ name: string; title: string; risk: number } | null>(null)

  useEffect(() => {
    if (userEmail) {
      fetchTimeline()
      // Use risk score from props, or fetch from API, or calculate from incidents
      const riskScore = userRiskScore ?? null
      
      // If risk score not provided, try to calculate from incidents
      if (riskScore === null) {
        fetchUserRiskScore()
      } else {
        setUserInfo({
          name: userEmail.split('@')[0].split('.').map(n => n.charAt(0).toUpperCase() + n.slice(1)).join(' '),
          title: 'Sr. QA Manager',
          risk: riskScore
        })
      }
    } else {
      setEvents([])
      setUserInfo(null)
    }
  }, [userEmail, userRiskScore])

  const fetchUserRiskScore = async () => {
    if (!userEmail) return
    
    try {
      // Fetch user's risk score from user-list endpoint
      const response = await axios.get(`${API_URL}/api/risk/user-list`, {
        params: { page: 1, page_size: 100 }
      })
      
      const user = response.data.users?.find((u: any) => u.user_email === userEmail)
      const riskScore = user?.risk_score ?? 0
      
      setUserInfo({
        name: userEmail.split('@')[0].split('.').map(n => n.charAt(0).toUpperCase() + n.slice(1)).join(' '),
        title: 'Sr. QA Manager',
        risk: riskScore
      })
    } catch (error) {
      console.error('Error fetching user risk score:', error)
      // Fallback: calculate from incidents
      const response = await axios.get(`${API_URL}/api/incidents`, {
        params: { user: userEmail, limit: 100 }
      })
      
      const maxRiskScore = response.data.length > 0
        ? Math.max(...response.data.map((inc: any) => inc.risk_score || 0))
        : 0
      
      setUserInfo({
        name: userEmail.split('@')[0].split('.').map(n => n.charAt(0).toUpperCase() + n.slice(1)).join(' '),
        title: 'Sr. QA Manager',
        risk: maxRiskScore
      })
    }
  }

  const fetchTimeline = async () => {
    if (!userEmail) return

    setLoading(true)
    try {
      const response = await axios.get(`${API_URL}/api/incidents`, {
        params: {
          user: userEmail,
          limit: 50,
          order_by: 'timestamp_desc'
        }
      })

      const timelineEvents = response.data.map((incident: any) => ({
        id: incident.id,
        timestamp: incident.timestamp,
        alert_type: incident.data_type || 'Unknown',
        severity: incident.severity >= 4 ? 'High' : incident.severity >= 3 ? 'Medium' : 'Low',
        description: getDescription(incident),
        tags: getTags(incident),
        channel: incident.channel,
        action: 'Permit'
      }))

      setEvents(timelineEvents)
    } catch (error) {
      console.error('Error fetching timeline:', error)
      // Fallback sample data
      const now = new Date()
      setEvents([
        {
          id: 1,
          timestamp: new Date(now.getTime() - 3600000).toISOString(),
          alert_type: 'Email',
          severity: 'High',
          description: 'Email sent to personal email domain',
          tags: ['Data exfiltration'],
          channel: 'Email',
          action: 'Permit'
        },
        {
          id: 2,
          timestamp: new Date(now.getTime() - 7200000).toISOString(),
          alert_type: 'Storage',
          severity: 'High',
          description: 'Suspicious number of files copied to removable storage',
          tags: ['Data exfiltration'],
          channel: 'Removable Storage',
          action: 'Permit'
        }
      ])
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

  // Group events by date
  const groupedEvents = events.reduce((acc, event) => {
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
        <p>Select a user from the list to view timeline</p>
      </div>
    )
  }

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      {/* User Profile Header */}
      {userInfo && (
        <div style={{ padding: '12px 16px', background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
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
              <div>
                <h3 style={{ fontWeight: '600', color: 'var(--text-primary)' }}>{userInfo.name}</h3>
                <p style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{userInfo.title}</p>
              </div>
            </div>
            <button style={{
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
              <span>📊</span>
              User Insights
            </button>
          </div>
        </div>
      )}

      {/* Timeline Content */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px' }}>
        {loading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            Loading timeline...
          </div>
        ) : Object.keys(groupedEvents).length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            No events found
          </div>
        ) : (
          Object.entries(groupedEvents).map(([date, dateEvents]) => (
            <div key={date} style={{ marginBottom: '24px' }}>
              <div style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '12px', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>
                {date} ({dateEvents.length} {dateEvents.length === 1 ? 'alert' : 'alerts'})
              </div>

              {dateEvents.map((event) => (
                <div
                  key={event.id}
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
                      style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: getSeverityColor(event.severity) }}
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
          ))
        )}
      </div>
    </div>
  )
}

