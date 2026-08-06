'use client'

import { useEffect, useRef, useState } from 'react'
import axios from 'axios'

import { getApiUrlDynamic } from '@/lib/api-config'
import { useTranslation } from '@/components/LanguageProvider'

export interface BreakdownItem {
  name: string
  total_alerts: number
}

export interface BreakdownSnapshot {
  items: BreakdownItem[]
  action: string
  days: number
}

// Mirrors the breakdown of the Action Analysis donut on the dashboard.
const ACTIONS = ['AUTHORIZED', 'BLOCK', 'QUARANTINE', 'RELEASED']

const selectStyle: React.CSSProperties = {
  padding: '6px 12px',
  borderRadius: '8px',
  border: '1px solid var(--border)',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontSize: '12px',
  fontWeight: '500',
  cursor: 'pointer'
}

export default function TopBreakdownCard({
  dimension,
  title,
  limit = 3,
  barColors = ['#3b82f6', '#2563eb'],
  onDataChange
}: {
  dimension: 'user' | 'department'
  title: string
  limit?: number
  barColors?: [string, string] | string[]
  onDataChange?: (snapshot: BreakdownSnapshot) => void
}) {
  const { t } = useTranslation()
  const [items, setItems] = useState<BreakdownItem[]>([])
  const [days, setDays] = useState<number>(30)
  const [action, setAction] = useState<string>('TOTAL')
  const [loading, setLoading] = useState(true)

  // Kept in a ref so a new callback identity from the parent never re-triggers the fetch.
  const onDataChangeRef = useRef(onDataChange)
  onDataChangeRef.current = onDataChange

  useEffect(() => {
    let cancelled = false

    const fetchData = async () => {
      setLoading(true)
      let nextItems: BreakdownItem[] = []
      try {
        const apiUrl = getApiUrlDynamic()
        const res = await axios.get(`${apiUrl}/api/risk/incidents/top-breakdown`, {
          params: { dimension, action, days, limit }
        })
        nextItems = res.data || []
      } catch (error) {
        console.error(`Error fetching top ${dimension} breakdown:`, error)
      } finally {
        if (!cancelled) {
          setItems(nextItems)
          setLoading(false)
          // Hand the rendered rows to the parent so the PDF record can reuse them.
          onDataChangeRef.current?.({ items: nextItems, action, days })
        }
      }
    }

    fetchData()
    return () => { cancelled = true }
  }, [dimension, action, days, limit])

  // Same rule as Top Matched Rules: the bar is relative to the busiest row so the
  // chart stays readable, the label reports the row's share of the listed total.
  const totalAlerts = items.reduce((sum, i) => sum + i.total_alerts, 0)
  const maxCount = Math.max(...items.map(i => i.total_alerts), 1)

  return (
    <div className="card">
      {/* .card-header-row lives in page.tsx's styled-jsx scope, so lay this out inline */}
      <div style={{ marginBottom: '8px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%', gap: '8px', flexWrap: 'wrap' }}>
          <h2 style={{ margin: 0 }}>{title}</h2>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <select value={action} onChange={(e) => setAction(e.target.value)} style={selectStyle}>
              <option value="TOTAL">{t('dashboard.allActions')}</option>
              {ACTIONS.map(a => (
                <option key={a} value={a}>{a}</option>
              ))}
            </select>
            <select value={days} onChange={(e) => setDays(Number(e.target.value))} style={selectStyle}>
              <option value={7}>{t('dashboard.last1Week')}</option>
              <option value={14}>{t('dashboard.last2Weeks')}</option>
              <option value={30}>{t('dashboard.lastMonth')}</option>
              <option value={90}>{t('dashboard.last3Months')}</option>
              <option value={180}>{t('dashboard.last6Months')}</option>
            </select>
          </div>
        </div>
      </div>
      {loading ? (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '160px', color: 'var(--text-muted)' }}>
          {t('common.loading')}
        </div>
      ) : items.length === 0 ? (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '160px', color: 'var(--text-muted)' }}>
          {t('common.noData')}
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px' }}>
          {items.map((item, idx) => {
            const barWidth = (item.total_alerts / maxCount) * 100
            const percentage = totalAlerts > 0 ? (item.total_alerts / totalAlerts) * 100 : 0
            return (
              <div key={`${item.name}-${idx}`} style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <div style={{ width: '220px', flexShrink: 0, fontSize: '13px', color: 'var(--text-primary)', fontWeight: '500', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={item.name}>
                  {item.name}
                </div>
                <div style={{ flex: 1, height: '32px', background: 'var(--background-secondary)', borderRadius: '4px', position: 'relative', overflow: 'hidden', border: '1px solid var(--border)' }}>
                  <div style={{ height: '100%', width: `${barWidth}%`, background: `linear-gradient(90deg, ${barColors[0]} 0%, ${barColors[1]} 100%)`, borderRadius: '4px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', paddingRight: '8px', transition: 'width 0.3s ease', minWidth: 'fit-content' }}>
                    <span style={{ fontSize: '12px', fontWeight: '600', color: '#fff', whiteSpace: 'nowrap' }}>{item.total_alerts}</span>
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
  )
}
