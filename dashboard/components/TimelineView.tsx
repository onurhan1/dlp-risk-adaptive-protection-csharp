'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format } from 'date-fns'

import { API_URL } from '@/lib/api-config'

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

interface TimelineViewProps {
  userEmail?: string
  onEventSelect: (event: TimelineEvent) => void
}

export default function TimelineView({ userEmail, onEventSelect }: TimelineViewProps) {
  const [events, setEvents] = useState<TimelineEvent[]>([])
  const [loading, setLoading] = useState(false)
  const [filterActive, setFilterActive] = useState(false)
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc')
  const [isClosed, setIsClosed] = useState(false)

  useEffect(() => {
    if (userEmail) {
      fetchTimeline()
    }
  }, [userEmail])

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
      
      // Transform incidents to timeline events
      const timelineEvents = response.data.map((incident: any) => ({
        id: incident.id,
        timestamp: incident.timestamp,
        alert_type: incident.data_type || 'Unknown',
        severity: incident.severity >= 4 ? 'High' : incident.severity >= 3 ? 'Medium' : 'Low',
        description: getDescription(incident),
        tags: getTags(incident),
        channel: incident.channel,
        action: 'Permit' // Default, can be from incident data
      }))
      
      setEvents(timelineEvents)
    } catch (error) {
      console.error('Error fetching timeline:', error)
    } finally {
      setLoading(false)
    }
  }

  const getDescription = (incident: any): string => {
    if (incident.channel === 'Email' && incident.data_type) {
      return `Email sent to ${incident.data_type}`
    }
    if (incident.channel === 'Removable Storage') {
      return `Suspicious number of files copied to removable storage`
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

  const getSeverityColor = (severity: string): string => {
    switch (severity) {
      case 'High': return '#f44336'
      case 'Medium': return '#ff9800'
      case 'Low': return '#4caf50'
      default: return '#9e9e9e'
    }
  }

  const getTagColor = (tag: string): string => {
    if (tag === 'Data exfiltration') return '#f44336'
    if (tag === 'System modification') return '#ff9800'
    if (tag === 'Defense evasion') return '#9c27b0'
    return '#2196f3'
  }

  const handleFilterClick = (e?: React.MouseEvent | React.KeyboardEvent) => {
    if (e) {
      e.preventDefault()
      e.stopPropagation()
    }
    console.log('Filter clicked, current:', filterActive)
    setFilterActive(!filterActive)
    console.log('Filter active set to:', !filterActive)
  }

  const handleSortClick = (e?: React.MouseEvent | React.KeyboardEvent) => {
    if (e) {
      e.preventDefault()
      e.stopPropagation()
    }
    const newOrder = sortOrder === 'desc' ? 'asc' : 'desc'
    console.log('Sort clicked, current:', sortOrder, 'new:', newOrder)
    setSortOrder(newOrder)
  }

  const handleCloseClick = (e?: React.MouseEvent | React.KeyboardEvent) => {
    if (e) {
      e.preventDefault()
      e.stopPropagation()
    }
    console.log('Timeline close clicked')
    setIsClosed(true)
    setEvents([])
  }

  // Filter events if filter is active (show only high severity)
  const filteredEvents = filterActive 
    ? events.filter(e => e.severity === 'High')
    : events

  // Sort events
  const sortedEvents = [...filteredEvents].sort((a, b) => {
    const dateA = new Date(a.timestamp).getTime()
    const dateB = new Date(b.timestamp).getTime()
    return sortOrder === 'desc' ? dateB - dateA : dateA - dateB
  })

  // Group events by date
  const groupedEvents = sortedEvents.reduce((acc, event) => {
    const date = format(new Date(event.timestamp), 'dd-MMM-yyyy')
    if (!acc[date]) {
      acc[date] = []
    }
    acc[date].push(event)
    return acc
  }, {} as Record<string, TimelineEvent[]>)

  if (isClosed) {
    return (
      <div className="timeline-view closed">
        <div className="closed-message">
          <p>Timeline closed</p>
          <button onClick={() => setIsClosed(false)} className="reopen-btn">
            Reopen Timeline
          </button>
        </div>
        <style jsx>{`
          .timeline-view.closed {
            background: white;
            border-radius: 8px;
            padding: 16px;
            height: 100%;
            display: flex;
            align-items: center;
            justify-content: center;
          }
          .closed-message {
            text-align: center;
          }
          .reopen-btn {
            margin-top: 12px;
            padding: 8px 16px;
            background: #2196f3;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
          }
        `}</style>
      </div>
    )
  }

  return (
    <div className="timeline-view">
      <div className="timeline-header">
        <h3>Timeline</h3>
        <div 
          className="close-btn" 
          onClick={handleCloseClick} 
          role="button"
          tabIndex={0}
          title="Close timeline"
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              handleCloseClick(e as any)
            }
          }}
        >
          ×
        </div>
      </div>

      {userEmail && (
        <div className="user-header">
          <div className="user-info">
            <h4>{userEmail}</h4>
            <span className="user-title">User Profile</span>
          </div>
          <button className="insights-btn">
            <span>📊</span> User Insights
          </button>
        </div>
      )}

      <div className="timeline-controls">
        <div 
          className={`filter-btn ${filterActive ? 'active' : ''}`}
          onClick={handleFilterClick}
          role="button"
          tabIndex={0}
          title={filterActive ? 'Show all alerts' : 'Show only high severity alerts'}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              handleFilterClick(e as any)
            }
          }}
        >
          ⚡
        </div>
        <div 
          className="sort-btn"
          onClick={handleSortClick}
          role="button"
          tabIndex={0}
          title={`Sort ${sortOrder === 'desc' ? 'ascending' : 'descending'}`}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              handleSortClick(e as any)
            }
          }}
        >
          ⇅
        </div>
      </div>

      {loading ? (
        <div className="loading">Loading timeline...</div>
      ) : (
        <div className="timeline-content">
          {Object.entries(groupedEvents).map(([date, dateEvents]) => (
            <div key={date} className="date-group">
              <div className="date-header">
                {date} ({dateEvents.length} {dateEvents.length === 1 ? 'alert' : 'alerts'})
              </div>
              
              {dateEvents.map((event) => (
                <div
                  key={event.id}
                  className="timeline-event"
                  onClick={() => onEventSelect(event)}
                >
                  <div className="event-time">
                    {format(new Date(event.timestamp), 'HH:mm')} UTC
                  </div>
                  <div className="event-content">
                    <div className="event-main">
                      <span
                        className="severity-dot"
                        style={{ backgroundColor: getSeverityColor(event.severity) }}
                      />
                      <span className="event-description">{event.description}</span>
                    </div>
                    <div className="event-tags">
                      {event.tags.map((tag, idx) => (
                        <span
                          key={idx}
                          className="tag"
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
          ))}
        </div>
      )}

      <style jsx>{`
        .timeline-view {
          background: white;
          border-radius: 8px;
          padding: 16px;
          height: 100%;
          display: flex;
          flex-direction: column;
        }

        .timeline-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 16px;
          border-bottom: 1px solid #e0e0e0;
          padding-bottom: 12px;
        }

        .timeline-header h3 {
          margin: 0;
          color: #333;
        }

        .close-btn {
          background: none;
          border: none;
          font-size: 24px;
          cursor: pointer;
          color: #666;
          padding: 4px 8px;
          position: relative;
          z-index: 1000;
          transition: all 0.2s;
          user-select: none;
          -webkit-user-select: none;
          pointer-events: auto !important;
          touch-action: manipulation;
        }

        .close-btn:hover {
          color: #f44336;
          background: rgba(244, 67, 54, 0.1);
          border-radius: 4px;
        }

        .close-btn:focus {
          outline: 2px solid #2196f3;
          outline-offset: 2px;
        }

        .user-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 12px;
          background: #f5f5f5;
          border-radius: 4px;
          margin-bottom: 16px;
        }

        .user-info h4 {
          margin: 0 0 4px 0;
          color: #333;
        }

        .user-title {
          font-size: 12px;
          color: #666;
        }

        .insights-btn {
          padding: 8px 16px;
          background: #4caf50;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          display: flex;
          align-items: center;
          gap: 6px;
        }

        .timeline-controls {
          display: flex;
          gap: 8px;
          margin-bottom: 16px;
        }

        .filter-btn, .sort-btn {
          padding: 6px 12px;
          background: white;
          border: 1px solid #ddd;
          border-radius: 4px;
          cursor: pointer;
          transition: all 0.2s;
          position: relative;
          z-index: 1000;
          font-size: 16px;
          min-width: 36px;
          min-height: 36px;
          user-select: none;
          -webkit-user-select: none;
          pointer-events: auto !important;
          touch-action: manipulation;
        }

        .filter-btn:hover, .sort-btn:hover {
          background: #f5f5f5;
          border-color: #2196f3;
          transform: scale(1.05);
        }

        .filter-btn:active, .sort-btn:active {
          transform: scale(0.95);
        }

        .filter-btn.active {
          background: #2196f3;
          color: white;
          border-color: #2196f3;
        }

        .filter-btn:focus, .sort-btn:focus {
          outline: 2px solid #2196f3;
          outline-offset: 2px;
        }

        .timeline-content {
          flex: 1;
          overflow-y: auto;
        }

        .date-group {
          margin-bottom: 24px;
        }

        .date-header {
          font-weight: 600;
          color: #666;
          margin-bottom: 12px;
          padding-bottom: 8px;
          border-bottom: 1px solid #e0e0e0;
        }

        .timeline-event {
          display: flex;
          padding: 12px;
          cursor: pointer;
          border-left: 3px solid transparent;
          transition: all 0.2s;
        }

        .timeline-event:hover {
          background: #f5f5f5;
          border-left-color: #2196f3;
        }

        .event-time {
          min-width: 80px;
          font-size: 12px;
          color: #666;
          font-weight: 500;
        }

        .event-content {
          flex: 1;
        }

        .event-main {
          display: flex;
          align-items: center;
          gap: 8px;
          margin-bottom: 6px;
        }

        .severity-dot {
          width: 8px;
          height: 8px;
          border-radius: 50%;
        }

        .event-description {
          color: #333;
          font-weight: 500;
        }

        .event-tags {
          display: flex;
          gap: 6px;
          flex-wrap: wrap;
        }

        .tag {
          padding: 2px 8px;
          border-radius: 12px;
          font-size: 11px;
          color: white;
          font-weight: 500;
        }

        .loading {
          text-align: center;
          padding: 40px;
          color: #666;
        }
      `}</style>
    </div>
  )
}

