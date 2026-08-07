'use client'

import { useState, useEffect, useRef } from 'react'
import axios from 'axios'
import { Mail, Globe, Usb, Printer, ClipboardList, ShieldCheck, Cloud, ArrowUpRight, Package, FileText } from 'lucide-react'

import { getApiUrlDynamic } from '@/lib/api-config'
import { useTranslation } from '@/components/LanguageProvider'

export interface ChannelActivity {
  channel: string
  total_incidents: number
  percentage: number
}

export interface DestinationActivity {
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

export interface ChannelActivitySnapshot {
  channels: ChannelActivity[]
  destinations: DestinationActivity[]
  days: number
}

export default function ChannelActivity({
  days = 30,
  startDate,
  endDate,
  onDataChange
}: {
  days?: number
  startDate?: string
  endDate?: string
  onDataChange?: (snapshot: ChannelActivitySnapshot) => void
}) {
  const { t } = useTranslation()
  const [activeTab, setActiveTab] = useState<'channel' | 'destination'>('channel')
  const [channels, setChannels] = useState<ChannelActivity[]>([])
  const [destinations, setDestinations] = useState<DestinationActivity[]>([])
  const [loading, setLoading] = useState(true)

  // Kept in a ref so a new callback identity from the parent never re-triggers the fetch.
  const onDataChangeRef = useRef(onDataChange)
  onDataChangeRef.current = onDataChange

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true)
      let nextChannels: ChannelActivity[] = []
      let nextDestinations: DestinationActivity[] = []
      try {
        const apiUrl = getApiUrlDynamic()
        const channelRes = await axios.get(`${apiUrl}/api/risk/channel-activity`, {
          params: { days, startDate, endDate },
          timeout: 10000
        }).catch(() => ({ data: null }))

        if (channelRes.data?.channels) {
          nextChannels = channelRes.data.channels
          setChannels(nextChannels)
        }

        if (channelRes.data?.destinations) {
          nextDestinations = channelRes.data.destinations
          setDestinations(nextDestinations)
        }
      } catch (error) {
        console.error('Error fetching channel activity:', error)
        // Use fallback data only when API fails
        nextChannels = FALLBACK_CHANNELS
        nextDestinations = FALLBACK_DESTINATIONS
        setChannels(FALLBACK_CHANNELS)
        setDestinations(FALLBACK_DESTINATIONS)
      } finally {
        setLoading(false)
        // Hand the rendered rows to the parent so the PDF record can reuse them.
        onDataChangeRef.current?.({ channels: nextChannels, destinations: nextDestinations, days })
      }
    }

    fetchData()
  }, [days, startDate, endDate])

  const currentData = activeTab === 'channel' ? channels : destinations
  const colors = ['#3b82f6', '#8b5cf6', '#ec4899', '#10b981', '#f59e0b', '#ef4444']

  const getIcon = (name: string, isChannel: boolean): React.ReactNode => {
    const iconProps = { size: 20, strokeWidth: 1.5 }
    if (isChannel) {
      const icons: Record<string, React.ReactNode> = {
        'Email': <Mail {...iconProps} />,
        'Web': <Globe {...iconProps} />,
        'Removable Storage': <Usb {...iconProps} />,
        'Print': <Printer {...iconProps} />,
        'System log event': <ClipboardList {...iconProps} />,
        'Security': <ShieldCheck {...iconProps} />
      }
      return icons[name] || <FileText {...iconProps} />
    } else {
      if (name.includes('gmail') || name.includes('mail')) return <Mail {...iconProps} />
      if (name.includes('dropbox') || name.includes('drive') || name.includes('onedrive')) return <Cloud {...iconProps} />
      if (name.includes('transfer')) return <ArrowUpRight {...iconProps} />
      if (name.includes('box')) return <Package {...iconProps} />
      if (name.includes('USB')) return <Usb {...iconProps} />
      return <Globe {...iconProps} />
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
          {t('channel.channel')}
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
          {t('channel.destination')}
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
            {activeTab === 'channel' ? t('channel.channel') : t('channel.destination')} {t('channel.loadingData')}
          </div>
        ) : currentData.length === 0 ? (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
            {activeTab === 'channel' ? t('channel.channel') : t('channel.destination')} {t('channel.noData')}
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
                <div style={{ width: '32px', height: '32px', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, color: color, background: `${color}15`, borderRadius: '8px' }}>{icon}</div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    title={name}
                    style={{
                      fontSize: '14px',
                      fontWeight: '500',
                      color: 'var(--text-primary)',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                      maxWidth: '160px'
                    }}>
                    {name.length > 25 ? name.substring(0, 22) + '...' : name}
                  </div>
                  <div style={{ fontSize: '18px', fontWeight: '700', color, margin: '2px 0' }}>
                    {percentage}%
                  </div>
                  <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
                    {count} {t('channel.alerts')}
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
