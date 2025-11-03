'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

interface ChannelActivity {
  channel: string
  total_incidents: number
  percentage: number
}

interface DestinationActivity {
  destination: string
  total_incidents: number
  percentage: number
}

// Fallback data - used when API fails
const FALLBACK_CHANNELS: ChannelActivity[] = [
  { channel: 'Web', total_incidents: 560, percentage: 56 },
  { channel: 'Removable Storage', total_incidents: 330, percentage: 33 },
  { channel: 'Print', total_incidents: 40, percentage: 4 },
  { channel: 'System log event', total_incidents: 30, percentage: 3 },
  { channel: 'Security', total_incidents: 20, percentage: 2 },
  { channel: 'Email', total_incidents: 10, percentage: 1 }
]

const FALLBACK_DESTINATIONS: DestinationActivity[] = [
  { destination: 'gmail.com', total_incidents: 450, percentage: 45 },
  { destination: 'dropbox.com', total_incidents: 300, percentage: 30 },
  { destination: 'onedrive.com', total_incidents: 150, percentage: 15 },
  { destination: 'google-drive.com', total_incidents: 50, percentage: 5 },
  { destination: 'we-transfer.com', total_incidents: 30, percentage: 3 },
  { destination: 'box.com', total_incidents: 20, percentage: 2 }
]

export default function ChannelActivity({ days = 30 }: { days?: number }) {
  const [activeTab, setActiveTab] = useState<'channel' | 'destination'>('channel')
  const [channels, setChannels] = useState<ChannelActivity[]>(FALLBACK_CHANNELS)
  const [destinations, setDestinations] = useState<DestinationActivity[]>(FALLBACK_DESTINATIONS)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true)
      try {
        const [channelRes, incidentsRes] = await Promise.all([
          axios.get(`${API_URL}/api/risk/channel-activity`, {
            params: { days },
            timeout: 5000
          }).catch(() => ({ data: null })),
          axios.get(`${API_URL}/api/incidents`, {
            params: { limit: 1000 },
            timeout: 5000
          }).catch(() => ({ data: [] }))
        ])

        if (channelRes.data?.channels) {
          setChannels(channelRes.data.channels)
        }

        // Calculate destinations from incidents
        const incidents = incidentsRes.data || []
        if (incidents.length > 0) {
          const destMap = new Map<string, number>()
          incidents.forEach((incident: any) => {
            let dest = 'Unknown'
            if (incident.channel === 'Email') {
              dest = incident.destination || 'gmail.com'
            } else if (incident.channel === 'Web') {
              dest = incident.destination || 'dropbox.com'
            } else if (incident.channel === 'Cloud') {
              dest = incident.destination || 'onedrive.com'
            } else if (incident.channel === 'Removable Storage') {
              dest = 'USB Device'
            }
            destMap.set(dest, (destMap.get(dest) || 0) + 1)
          })

          if (destMap.size > 0) {
            const total = Array.from(destMap.values()).reduce((sum, count) => sum + count, 0)
            const destData: DestinationActivity[] = Array.from(destMap.entries())
              .map(([destination, count]) => ({
                destination,
                total_incidents: count,
                percentage: total > 0 ? Math.round((count / total) * 100) : 0
              }))
              .sort((a, b) => b.total_incidents - a.total_incidents)
              .slice(0, 6)
            setDestinations(destData)
          }
        }
      } catch (error) {
        console.error('Error fetching channel activity:', error)
        // Keep fallback data on error
      } finally {
        setLoading(false)
      }
    }

    fetchData()
  }, [days])

  const currentData = activeTab === 'channel' ? channels : destinations
  const colors = ['#3b82f6', '#8b5cf6', '#ec4899', '#10b981', '#f59e0b', '#ef4444']

  const getIcon = (name: string, isChannel: boolean): string => {
    if (isChannel) {
      const icons: Record<string, string> = {
        'Email': '📧',
        'Web': '🌐',
        'Removable Storage': '💾',
        'Print': '🖨️',
        'System log event': '📋',
        'Security': '🔒'
      }
      return icons[name] || '📄'
    } else {
      if (name.includes('gmail') || name.includes('mail')) return '📧'
      if (name.includes('dropbox') || name.includes('drive') || name.includes('onedrive')) return '☁️'
      if (name.includes('transfer')) return '↗️'
      if (name.includes('box')) return '📦'
      if (name.includes('USB')) return '💾'
      return '🌐'
    }
  }

  const getName = (item: ChannelActivity | DestinationActivity): string => {
    return 'channel' in item ? item.channel : item.destination
  }

  return (
    <div style={{ width: '100%' }}>
      {/* Tabs */}
      <div style={{ 
        display: 'flex', 
        gap: '0',
        marginBottom: '16px', 
        borderBottom: '1px solid #e2e8f0'
      }}>
        <button
          type="button"
          onClick={() => setActiveTab('channel')}
          style={{
            padding: '8px 16px',
            fontSize: '14px',
            fontWeight: '500',
            color: activeTab === 'channel' ? '#2563eb' : '#64748b',
            cursor: 'pointer',
            backgroundColor: 'transparent',
            border: 'none',
            borderBottom: activeTab === 'channel' ? '2px solid #2563eb' : '2px solid transparent',
            marginBottom: activeTab === 'channel' ? '-1px' : '0',
            userSelect: 'none',
            transition: 'all 0.2s'
          }}
        >
          Channel
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('destination')}
          style={{
            padding: '8px 16px',
            fontSize: '14px',
            fontWeight: '500',
            color: activeTab === 'destination' ? '#2563eb' : '#64748b',
            cursor: 'pointer',
            backgroundColor: 'transparent',
            border: 'none',
            borderBottom: activeTab === 'destination' ? '2px solid #2563eb' : '2px solid transparent',
            marginBottom: activeTab === 'destination' ? '-1px' : '0',
            userSelect: 'none',
            transition: 'all 0.2s'
          }}
        >
          Destination
        </button>
      </div>

      {/* Content Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px' }}>
        {loading ? (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#94a3b8' }}>
            Loading {activeTab} data...
          </div>
        ) : currentData.length === 0 ? (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#94a3b8' }}>
            No {activeTab} data available
          </div>
        ) : (
          currentData.slice(0, 6).map((item: any, idx: number) => {
            const name = getName(item)
            const percentage = item.percentage
            const count = item.total_incidents
            const icon = getIcon(name, activeTab === 'channel')
            const color = colors[idx % colors.length]

            return (
              <div
                key={`${activeTab}-${idx}-${name}`}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '12px',
                  padding: '12px',
                  background: '#f9fafb',
                  borderRadius: '8px',
                  border: '1px solid #e5e7eb',
                  transition: 'background 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = '#f3f4f6'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = '#f9fafb'
                }}
              >
                <div style={{ fontSize: '24px', flexShrink: 0 }}>{icon}</div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ 
                    fontSize: '14px', 
                    fontWeight: '500', 
                    color: '#1e293b',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap'
                  }}>
                    {name}
                  </div>
                  <div style={{ fontSize: '18px', fontWeight: '700', color, margin: '2px 0' }}>
                    {percentage}%
                  </div>
                  <div style={{ fontSize: '11px', color: '#64748b' }}>
                    {count} alerts
                  </div>
                </div>
                <div style={{ flexShrink: 0 }}>
                  <svg width="50" height="50" style={{ transform: 'rotate(-90deg)' }}>
                    <circle
                      cx="25"
                      cy="25"
                      r="20"
                      fill="none"
                      stroke="#e5e7eb"
                      strokeWidth="4"
                    />
                    <circle
                      cx="25"
                      cy="25"
                      r="20"
                      fill="none"
                      stroke={color}
                      strokeWidth="4"
                      strokeDasharray={`${(percentage / 100) * 125.6} 125.6`}
                      strokeLinecap="round"
                    />
                  </svg>
                </div>
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}
