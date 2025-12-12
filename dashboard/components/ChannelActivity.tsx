'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

import { getApiUrlDynamic } from '@/lib/api-config'

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
        const apiUrl = getApiUrlDynamic()
        // Calculate date range for incidents
        const endDate = new Date()
        const startDate = new Date()
        startDate.setDate(startDate.getDate() - days)

        const [channelRes, incidentsRes] = await Promise.all([
          axios.get(`${apiUrl}/api/risk/channel-activity`, {
            params: { days },
            timeout: 5000
          }).catch(() => ({ data: null })),
          axios.get(`${apiUrl}/api/incidents`, {
            params: {
              start_date: startDate.toISOString().split('T')[0],
              end_date: endDate.toISOString().split('T')[0],
              limit: 10000 // Increased limit to handle large datasets
            },
            timeout: 10000 // Increased timeout for large datasets
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
            let dest = incident.destination

            // If destination is empty/null, try to derive from channel
            if (!dest) {
              const channel = (incident.channel || '').toLowerCase()
              if (channel.includes('email')) {
                dest = 'Unknown Email'
              } else if (channel.includes('web')) {
                dest = 'Unknown Web Site'
              } else if (channel.includes('cloud')) {
                dest = 'Unknown Cloud Service'
              } else if (channel.includes('removable') || channel.includes('usb')) {
                dest = 'USB Device'
              } else if (channel === 'endpoint_printing' || channel === 'printer') {
                dest = 'Printer'
              } else {
                dest = 'Unknown'
              }
            } else {
              // Clean up destination if needed (e.g., remove common prefixes if user improved it before)
              // For now, use the raw destination as it seems to contain valid domains/paths
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
              .slice(0, 100) // Increase limit to show more destinations
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
        borderBottom: '1px solid var(--border)'
      }}>
        <button
          type="button"
          onClick={() => setActiveTab('channel')}
          style={{
            padding: '8px 16px',
            fontSize: '14px',
            fontWeight: '500',
            color: activeTab === 'channel' ? 'var(--primary)' : 'var(--text-secondary)',
            cursor: 'pointer',
            backgroundColor: 'transparent',
            border: 'none',
            borderBottom: activeTab === 'channel' ? '2px solid var(--primary)' : '2px solid transparent',
            marginBottom: activeTab === 'channel' ? '-1px' : '0',
            userSelect: 'none',
            transition: 'all 0.2s'
          }}
          onMouseEnter={(e) => {
            if (activeTab !== 'channel') {
              e.currentTarget.style.color = 'var(--text-primary)'
            }
          }}
          onMouseLeave={(e) => {
            if (activeTab !== 'channel') {
              e.currentTarget.style.color = 'var(--text-secondary)'
            }
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
            color: activeTab === 'destination' ? 'var(--primary)' : 'var(--text-secondary)',
            cursor: 'pointer',
            backgroundColor: 'transparent',
            border: 'none',
            borderBottom: activeTab === 'destination' ? '2px solid var(--primary)' : '2px solid transparent',
            marginBottom: activeTab === 'destination' ? '-1px' : '0',
            userSelect: 'none',
            transition: 'all 0.2s'
          }}
          onMouseEnter={(e) => {
            if (activeTab !== 'destination') {
              e.currentTarget.style.color = 'var(--text-primary)'
            }
          }}
          onMouseLeave={(e) => {
            if (activeTab !== 'destination') {
              e.currentTarget.style.color = 'var(--text-secondary)'
            }
          }}
        >
          Destination
        </button>
      </div>

      {/* Content Grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: '16px',
        maxHeight: '400px', // Add max height for scrolling
        overflowY: 'auto',   // Enable vertical scrolling
        paddingRight: '8px'  // Add padding for scrollbar
      }}>
        {loading ? (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
            Loading {activeTab} data...
          </div>
        ) : currentData.length === 0 ? (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
            No {activeTab} data available
          </div>
        ) : (
          currentData.slice(0, 100).map((item: any, idx: number) => {
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
                  background: 'var(--background-secondary)',
                  borderRadius: '8px',
                  border: '1px solid var(--border)',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'var(--surface-hover)'
                  e.currentTarget.style.borderColor = 'var(--border-hover)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'var(--background-secondary)'
                  e.currentTarget.style.borderColor = 'var(--border)'
                }}
              >
                <div style={{ fontSize: '24px', flexShrink: 0 }}>{icon}</div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    title={name}
                    style={{
                      fontSize: '14px',
                      fontWeight: '500',
                      color: 'var(--text-primary)',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap'
                    }}>
                    {name}
                  </div>
                  <div style={{ fontSize: '18px', fontWeight: '700', color, margin: '2px 0' }}>
                    {percentage}%
                  </div>
                  <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
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
                      stroke="var(--border)"
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