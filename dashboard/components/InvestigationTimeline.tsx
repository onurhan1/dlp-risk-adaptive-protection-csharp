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
      <div className="flex-1 flex items-center justify-center text-gray-500">
        <p>Select a user from the list to view timeline</p>
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col overflow-hidden">
      {/* User Profile Header */}
      {userInfo && (
        <div className="px-4 py-3 bg-gray-50 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="relative w-12 h-12">
                <svg className="w-12 h-12 transform -rotate-90">
                  <circle cx="24" cy="24" r="20" fill="none" stroke="#e5e7eb" strokeWidth="4" />
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
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-sm font-bold" style={{ color: getRiskColorForScore(userInfo.risk) }}>
                    {userInfo.risk}
                  </span>
                </div>
              </div>
              <div>
                <h3 className="font-semibold text-gray-900">{userInfo.name}</h3>
                <p className="text-xs text-gray-600">{userInfo.title}</p>
              </div>
            </div>
            <button className="px-3 py-1.5 bg-teal-50 text-teal-700 rounded-md text-sm font-medium hover:bg-teal-100 transition-colors flex items-center gap-2">
              <span>📊</span>
              User Insights
            </button>
          </div>
        </div>
      )}

      {/* Timeline Content */}
      <div className="flex-1 overflow-y-auto p-4">
        {loading ? (
          <div className="flex items-center justify-center py-8 text-gray-500">
            Loading timeline...
          </div>
        ) : Object.keys(groupedEvents).length === 0 ? (
          <div className="flex items-center justify-center py-8 text-gray-500">
            No events found
          </div>
        ) : (
          Object.entries(groupedEvents).map(([date, dateEvents]) => (
            <div key={date} className="mb-6">
              <div className="text-sm font-semibold text-gray-700 mb-3 pb-2 border-b border-gray-200">
                {date} ({dateEvents.length} {dateEvents.length === 1 ? 'alert' : 'alerts'})
              </div>

              {dateEvents.map((event) => (
                <div
                  key={event.id}
                  onClick={() => onEventSelect(event)}
                  className={`flex gap-4 p-3 rounded-lg cursor-pointer transition-colors mb-2 ${
                    selectedEventId === event.id
                      ? 'bg-teal-50 border-l-4 border-l-teal-500'
                      : 'hover:bg-gray-50'
                  }`}
                >
                  <div className="flex flex-col items-center pt-1">
                    <div
                      className="w-2 h-2 rounded-full"
                      style={{ backgroundColor: getSeverityColor(event.severity) }}
                    />
                    <div className="w-0.5 h-full bg-gray-200 mt-1" />
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="text-xs text-gray-500 font-mono">
                        {format(new Date(event.timestamp), 'HH:mm')} UTC
                      </span>
                    </div>
                    <div className="text-sm text-gray-900 font-medium mb-2">
                      {event.description}
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {event.tags.map((tag, idx) => (
                        <span
                          key={idx}
                          className="px-2 py-0.5 rounded text-xs font-medium text-white"
                          style={{ backgroundColor: getTagColor(tag) }}
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

