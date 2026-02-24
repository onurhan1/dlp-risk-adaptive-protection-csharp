'use client'

import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react'
import dynamic from 'next/dynamic'
import apiClient from '@/lib/axios'
import Pagination from '@/components/ui/Pagination'

const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface ReleasedIncident {
  id: number
  incident_id: number
  incident_timestamp: string
  action: string
  task_name: string
  admin_name: string
  comments: string
  update_time: string
  created_at: string
}

export default function MercekAnalyzePage() {
  // Released Incidents States
  const [releasedIncidents, setReleasedIncidents] = useState<ReleasedIncident[]>([])
  const [loadingReleasedIncidents, setLoadingReleasedIncidents] = useState(false)

  // CSV / Mercek Analysis States
  const [csvData, setCsvData] = useState<any[]>([])
  const [csvHeaders, setCsvHeaders] = useState<string[]>([])
  const [csvSearchQuery, setCsvSearchQuery] = useState('')
  const [csvPageSize, setCsvPageSize] = useState(25)
  const [csvCurrentPage, setCsvCurrentPage] = useState(1)
  const [csvDateFrom, setCsvDateFrom] = useState('')
  const [csvDateTo, setCsvDateTo] = useState('')
  const [csvSelectedUser, setCsvSelectedUser] = useState('')
  const [mercekLoading, setMercekLoading] = useState(false)
  const [mercekError, setMercekError] = useState<string | null>(null)
  const [mercekTotalCount, setMercekTotalCount] = useState(0)
  const [mercekTotalPages, setMercekTotalPages] = useState(0)
  const [mercekAvailableUsers, setMercekAvailableUsers] = useState<string[]>([])
  const [mercekAssignedUsers, setMercekAssignedUsers] = useState<string[]>([])
  const [mercekAssignedUserFilter, setMercekAssignedUserFilter] = useState('')
  const [mercekDataLoaded, setMercekDataLoaded] = useState(false)
  const [mercekStatistics, setMercekStatistics] = useState<any>(null)
  const [mercekSortColumn, setMercekSortColumn] = useState<string | null>(null)
  const [mercekSortDirection, setMercekSortDirection] = useState<'asc' | 'desc'>('asc')
  const [mercekColumnFilters, setMercekColumnFilters] = useState<Record<string, string[]>>({})
  const [mercekTextFilters, setMercekTextFilters] = useState<Record<string, string>>({})
  const [mercekTagFilters, setMercekTagFilters] = useState<Record<string, string[]>>({})
  const [tagSearchInput, setTagSearchInput] = useState<Record<string, string>>({})
  const [mercekDateFilters, setMercekDateFilters] = useState<Record<string, { from: string; to: string }>>({})
  const [openDropdownColumn, setOpenDropdownColumn] = useState<string | null>(null)
  const [dropdownSearchQuery, setDropdownSearchQuery] = useState<Record<string, string>>({})
  const [userChartPage, setUserChartPage] = useState(1)
  const [assignedUserChartPage, setAssignedUserChartPage] = useState(1)

  // ─── Data Fetching ───────────────────────────────────────────

  const fetchReleasedIncidents = async () => {
    setLoadingReleasedIncidents(true)
    try {
      const response = await apiClient.get('/api/released-incidents', {
        params: { page: 1, pageSize: 10000 }
      })
      const data = response.data?.data || []
      setReleasedIncidents(data)
    } catch (error) {
      console.error('Error fetching released incidents:', error)
      setReleasedIncidents([])
    } finally {
      setLoadingReleasedIncidents(false)
    }
  }

  const fetchMercekData = async (page: number = 1, customPageSize?: number) => {
    setMercekLoading(true)
    setMercekError(null)

    try {
      const params: any = {
        page: page,
        pageSize: customPageSize || csvPageSize
      }

      if (csvSelectedUser) params.userName = csvSelectedUser
      if (mercekAssignedUserFilter) params.assignedUserCode = mercekAssignedUserFilter
      if (csvDateFrom) params.startDate = csvDateFrom
      if (csvDateTo) params.endDate = csvDateTo
      if (csvSearchQuery) params.searchTerm = csvSearchQuery

      const response = await apiClient.get('/api/mercek', { params })
      const data = response.data

      const mappedData = data.items.map((item: any) => ({
        'Olay No': item.incidentId,
        'Olay Açıklaması': item.incidentDescription,
        'Atanan Kullanıcı': item.assignedUserCode,
        'Sistem Tarihi': item.systemDate ? new Date(item.systemDate).toLocaleString('tr-TR') : '',
        'Açılış Tarihi': item.openDate ? new Date(item.openDate).toLocaleString('tr-TR') : '',
        'Kapanış Tarihi': item.closeDate ? new Date(item.closeDate).toLocaleString('tr-TR') : '',
        'Başlangıç Tarihi': item.startDate ? new Date(item.startDate).toLocaleString('tr-TR') : '',
        'Çözüm Yöntemi': item.solutionMethod,
        'Kullanıcı Adı': item.userName
      }))

      setCsvData(mappedData)
      setMercekTotalCount(data.totalCount)
      setMercekTotalPages(data.totalPages)
      setCsvCurrentPage(data.page)
      setMercekDataLoaded(true)

      if (mappedData.length > 0) {
        setCsvHeaders(Object.keys(mappedData[0]))
      }
    } catch (error: any) {
      setMercekError(error.response?.data?.error || 'Mercek verileri yüklenirken hata oluştu')
      console.error('Mercek fetch error:', error)
    } finally {
      setMercekLoading(false)
    }
  }

  const fetchMercekFilters = async () => {
    try {
      const response = await apiClient.get('/api/mercek/filters')
      const data = response.data
      setMercekAvailableUsers(data.users || [])
      setMercekAssignedUsers(data.assignedUsers || [])
    } catch (error) {
      console.error('Mercek filters fetch error:', error)
    }
  }

  const fetchMercekStatistics = async () => {
    try {
      const params: any = {}
      if (csvSelectedUser) params.userName = csvSelectedUser
      if (mercekAssignedUserFilter) params.assignedUserCode = mercekAssignedUserFilter
      if (csvDateFrom) params.startDate = csvDateFrom
      if (csvDateTo) params.endDate = csvDateTo

      const response = await apiClient.get('/api/mercek/statistics', { params })
      setMercekStatistics(response.data)
    } catch (error) {
      console.error('Mercek statistics fetch error:', error)
    }
  }

  // ─── Sort / Filter helpers ──────────────────────────────────

  const handleMercekSort = (column: string) => {
    if (mercekSortColumn === column) {
      setMercekSortDirection(mercekSortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setMercekSortColumn(column)
      setMercekSortDirection('asc')
    }
  }

  const handleMercekColumnFilter = (column: string, values: string[]) => {
    setMercekColumnFilters((prev: Record<string, string[]>) => ({
      ...prev,
      [column]: values
    }))
    setCsvCurrentPage(1) // Reset to first page when filter changes
  }

  const handleMercekTextFilter = (column: string, value: string) => {
    setMercekTextFilters((prev: Record<string, string>) => ({
      ...prev,
      [column]: value
    }))
    setCsvCurrentPage(1) // Reset to first page when filter changes
  }

  const handleMercekDateFilter = (column: string, type: 'from' | 'to', value: string) => {
    setMercekDateFilters((prev: Record<string, { from: string; to: string }>) => ({
      ...prev,
      [column]: { ...(prev[column] || { from: '', to: '' }), [type]: value }
    }))
    setCsvCurrentPage(1) // Reset to first page when filter changes
  }

  const toggleDropdownValue = (column: string, value: string) => {
    setMercekColumnFilters((prev: Record<string, string[]>) => {
      const current = prev[column] || []
      if (current.includes(value)) {
        return { ...prev, [column]: current.filter((v: string) => v !== value) }
      } else {
        return { ...prev, [column]: [...current, value] }
      }
    })
    setCsvCurrentPage(1) // Reset to first page when filter changes
  }

  // Column type definitions
  const listboxColumns = ['Olay No', 'Atanan Kullanıcı', 'Kullanıcı Adı', 'Çözüm Yöntemi']
  const tagSearchColumns = ['Olay Açıklaması'] // Multi-select tag-based search
  const textSearchColumns: string[] = [] // Regular text search (empty - all use tags or listbox)
  const dateColumns = ['Sistem Tarihi', 'Açılış Tarihi', 'Kapanış Tarihi', 'Başlangıç Tarihi']

  // Tag filter handlers
  const addTagFilter = (column: string, tag: string) => {
    const trimmedTag = tag.trim()
    if (!trimmedTag) return
    setMercekTagFilters((prev: Record<string, string[]>) => {
      const current = prev[column] || []
      if (current.includes(trimmedTag)) return prev
      return { ...prev, [column]: [...current, trimmedTag] }
    })
    setTagSearchInput((prev: Record<string, string>) => ({ ...prev, [column]: '' }))
    setCsvCurrentPage(1)
  }

  const removeTagFilter = (column: string, tag: string) => {
    setMercekTagFilters((prev: Record<string, string[]>) => {
      const current = prev[column] || []
      return { ...prev, [column]: current.filter((t: string) => t !== tag) }
    })
    setCsvCurrentPage(1)
  }

  const clearTagFilters = (column: string) => {
    setMercekTagFilters((prev: Record<string, string[]>) => ({ ...prev, [column]: [] }))
    setTagSearchInput((prev: Record<string, string>) => ({ ...prev, [column]: '' }))
    setCsvCurrentPage(1)
  }

  // Get unique values for listbox columns
  const uniqueColumnValues = useMemo(() => {
    const values: Record<string, string[]> = {}
    listboxColumns.forEach((col: string) => {
      const uniqueSet = new Set<string>()
      csvData.forEach((row: Record<string, unknown>) => {
        const val = row[col]
        if (val !== null && val !== undefined && String(val).trim()) {
          uniqueSet.add(String(val))
        }
      })
      values[col] = Array.from(uniqueSet).sort((a, b) => a.localeCompare(b, 'tr'))
    })
    return values
  }, [csvData])

  const getFilteredAndSortedMercekData = (data: Record<string, unknown>[]) => {
    let filtered = [...data]

    // Apply listbox column filters (multi-select)
    for (const column of Object.keys(mercekColumnFilters)) {
      const selectedValues = mercekColumnFilters[column]
      if (selectedValues && selectedValues.length > 0) {
        filtered = filtered.filter(row => {
          const cellValue = String(row[column] || '')
          return selectedValues.includes(cellValue)
        })
      }
    }

    // Apply text filters
    for (const column of Object.keys(mercekTextFilters)) {
      const filterValue = mercekTextFilters[column]
      if (filterValue && filterValue.trim()) {
        filtered = filtered.filter(row => {
          const cellValue = String(row[column] || '').toLowerCase()
          return cellValue.includes(filterValue.toLowerCase().trim())
        })
      }
    }

    // Apply tag filters (OR logic - match any tag)
    for (const column of Object.keys(mercekTagFilters)) {
      const tags = mercekTagFilters[column]
      if (tags && tags.length > 0) {
        filtered = filtered.filter(row => {
          const cellValue = String(row[column] || '').toLowerCase()
          // Return true if cell contains ANY of the tags
          return tags.some((tag: string) => cellValue.includes(tag.toLowerCase()))
        })
      }
    }

    // Apply date filters
    for (const column of Object.keys(mercekDateFilters)) {
      const dateRange = mercekDateFilters[column]
      if (dateRange.from || dateRange.to) {
        filtered = filtered.filter(row => {
          const cellValue = row[column]
          if (!cellValue) return false
          
          // Parse the Turkish formatted date (dd.MM.yyyy HH:mm:ss)
          const parts = String(cellValue).match(/(\d{2})\.(\d{2})\.(\d{4})/)
          if (!parts) return false
          
          const rowDate = new Date(`${parts[3]}-${parts[2]}-${parts[1]}`)
          if (isNaN(rowDate.getTime())) return false
          
          if (dateRange.from) {
            const fromDate = new Date(dateRange.from)
            if (rowDate < fromDate) return false
          }
          if (dateRange.to) {
            const toDate = new Date(dateRange.to)
            toDate.setHours(23, 59, 59, 999)
            if (rowDate > toDate) return false
          }
          return true
        })
      }
    })

    // Apply sorting
    if (mercekSortColumn) {
      filtered.sort((a, b) => {
        const aVal = String(a[mercekSortColumn!] || '')
        const bVal = String(b[mercekSortColumn!] || '')

        // Try numeric comparison
        const aNum = parseFloat(aVal.replace(/[^0-9.-]/g, ''))
        const bNum = parseFloat(bVal.replace(/[^0-9.-]/g, ''))
        if (!isNaN(aNum) && !isNaN(bNum)) {
          return mercekSortDirection === 'asc' ? aNum - bNum : bNum - aNum
        }

        // Try date comparison
        const aDate = Date.parse(aVal)
        const bDate = Date.parse(bVal)
        if (!isNaN(aDate) && !isNaN(bDate)) {
          return mercekSortDirection === 'asc' ? aDate - bDate : bDate - aDate
        }

        // String comparison
        const cmp = aVal.localeCompare(bVal, 'tr')
        return mercekSortDirection === 'asc' ? cmp : -cmp
      })
    }

    return filtered
  }

  // ─── Released Incidents Stats ───────────────────────────────

  const releasedIncidentsStats = useMemo(() => {
    const adminCounts: Record<string, number> = {}
    let incStartingCount = 0
    let nonIncStartingCount = 0

    releasedIncidents.forEach((incident: { admin_name?: string; comments?: string }) => {
      const admin = incident.admin_name || 'Unknown'
      adminCounts[admin] = (adminCounts[admin] || 0) + 1

      const comments = incident.comments || ''
      if (comments.trim().toUpperCase().startsWith('INC')) {
        incStartingCount++
      } else {
        nonIncStartingCount++
      }
    })

    const adminData = Object.entries(adminCounts)
      .map(([admin, count]) => ({ admin, count }))
      .sort((a, b) => b.count - a.count)

    return {
      adminData,
      incStartingCount,
      nonIncStartingCount,
      totalCount: releasedIncidents.length
    }
  }, [releasedIncidents])

  // ─── Auto-load on mount ─────────────────────────────────────

  useEffect(() => {
    fetchMercekFilters()
    fetchMercekData(1, 10000) // Fetch all data for client-side filtering
    fetchMercekStatistics()
    fetchReleasedIncidents()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as HTMLElement
      if (openDropdownColumn && !target.closest('[data-column-filter]')) {
        setOpenDropdownColumn(null)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [openDropdownColumn])

  // ─── Render ──────────────────────────────────────────────────

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <h1 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Mercek Analysis</h1>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', marginBottom: '24px' }}>

          {/* ══════════════ Released Incidents Section (order: 2) ══════════════ */}
          <div style={{ order: 2, marginBottom: '24px' }}>
            <div style={{
              background: 'var(--surface)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '24px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.1)'
            }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '20px' }}>
                Released Incidents İstatistikleri
              </h2>

              {loadingReleasedIncidents ? (
                <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  Yükleniyor...
                </div>
              ) : (
                <>
                  {/* Toplam Sayı ve INC İstatistikleri */}
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '6px',
                      padding: '16px',
                      border: '1px solid var(--border)'
                    }}>
                      <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
                        Toplam Released Incident
                      </div>
                      <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)' }}>
                        {releasedIncidentsStats.totalCount}
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '6px',
                      padding: '16px',
                      border: '1px solid var(--border)'
                    }}>
                      <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
                        INC ile Başlayan
                      </div>
                      <div style={{ fontSize: '24px', fontWeight: '700', color: '#3b82f6' }}>
                        {releasedIncidentsStats.incStartingCount}
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '6px',
                      padding: '16px',
                      border: '1px solid var(--border)'
                    }}>
                      <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
                        INC ile Başlamayan
                      </div>
                      <div style={{ fontSize: '24px', fontWeight: '700', color: '#ef4444' }}>
                        {releasedIncidentsStats.nonIncStartingCount}
                      </div>
                    </div>
                  </div>

                  {/* Admin Bazlı Sütun Grafik */}
                  {releasedIncidentsStats.adminData.length > 0 && (
                    <div style={{ marginTop: '24px' }}>
                      <h3 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '16px' }}>
                        Admin Bazlı Released Incident Sayıları
                      </h3>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                        {releasedIncidentsStats.adminData.map(({ admin, count }: { admin: string, count: number }) => {
                          const maxCount = Math.max(...releasedIncidentsStats.adminData.map((d: { admin: string, count: number }) => d.count))
                          const percentage = maxCount > 0 ? (count / maxCount) * 100 : 0

                          return (
                            <div key={admin} style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                              <div style={{
                                minWidth: '150px',
                                fontSize: '13px',
                                color: 'var(--text-primary)',
                                fontWeight: '500'
                              }}>
                                {admin}
                              </div>
                              <div style={{
                                flex: 1,
                                height: '32px',
                                background: 'var(--background-secondary)',
                                borderRadius: '4px',
                                position: 'relative',
                                overflow: 'hidden',
                                border: '1px solid var(--border)'
                              }}>
                                <div style={{
                                  height: '100%',
                                  width: `${percentage}%`,
                                  background: 'linear-gradient(90deg, #3b82f6 0%, #2563eb 100%)',
                                  borderRadius: '4px',
                                  display: 'flex',
                                  alignItems: 'center',
                                  justifyContent: 'flex-end',
                                  paddingRight: '8px',
                                  transition: 'width 0.3s ease',
                                  minWidth: 'fit-content'
                                }}>
                                  <span style={{
                                    fontSize: '12px',
                                    fontWeight: '600',
                                    color: '#fff',
                                    whiteSpace: 'nowrap'
                                  }}>
                                    {count}
                                  </span>
                                </div>
                              </div>
                              <div style={{
                                minWidth: '50px',
                                textAlign: 'right',
                                fontSize: '13px',
                                fontWeight: '600',
                                color: 'var(--text-primary)'
                              }}>
                                {count}
                              </div>
                            </div>
                          )
                        })}
                      </div>
                    </div>
                  )}

                  {releasedIncidentsStats.adminData.length === 0 && (
                    <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                      Released incident verisi bulunamadı.
                    </div>
                  )}
                </>
              )}
            </div>
          </div>

          {/* ══════════════ Mercek Analiz Section (order: 1) ══════════════ */}
          <div style={{ order: 1, marginBottom: '24px' }}>
            <div style={{
              background: 'var(--surface)',
              borderRadius: '8px',
              padding: '24px'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <h2 style={{ fontSize: '22px', fontWeight: '700', color: 'var(--text-primary)', margin: 0 }}>Mercek Analiz</h2>
                  {mercekLoading && (
                    <div style={{
                      width: '16px',
                      height: '16px',
                      border: '2px solid var(--border)',
                      borderTopColor: 'var(--primary)',
                      borderRadius: '50%',
                      animation: 'spin 0.6s linear infinite'
                    }} />
                  )}
                  {mercekDataLoaded && !mercekLoading && (
                    <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                      (Otomatik güncelleniyor - Her 30 saniye)
                    </span>
                  )}
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <button
                    onClick={() => fetchMercekData(1, 10000)} // Fetch all data for client-side filtering
                    disabled={mercekLoading}
                    style={{
                      padding: '8px 16px',
                      borderRadius: '6px',
                      border: 'none',
                      background: mercekLoading ? 'var(--border)' : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                      color: 'white',
                      fontSize: '13px',
                      fontWeight: '600',
                      cursor: mercekLoading ? 'not-allowed' : 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px'
                    }}
                  >
                    🔄 Yenile
                  </button>
                </div>
              </div>
              <style jsx>{`
                @keyframes spin {
                  to { transform: rotate(360deg); }
                }
              `}</style>

              {/* Error Message */}
              {mercekError && (
                <div style={{
                  padding: '12px 16px',
                  borderRadius: '6px',
                  background: 'rgba(239, 68, 68, 0.1)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  color: '#ef4444',
                  fontSize: '14px',
                  marginBottom: '16px'
                }}>
                  ⚠️ {mercekError}
                </div>
              )}

              {/* Number Cards */}
              {mercekStatistics && (() => {
                const lastWeekCount = mercekStatistics.lastWeekCount || 0
                const previousWeekCount = mercekStatistics.previousWeekCount || 0
                const weekChange = previousWeekCount > 0
                  ? ((lastWeekCount - previousWeekCount) / previousWeekCount * 100).toFixed(1)
                  : '0'
                const weekChangePositive = Number(weekChange) >= 0

                const toDateKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
                const todayKey = toDateKey(new Date())
                const dailyCounts = mercekStatistics.dailyCounts || []
                const todayCount = dailyCounts.find((d: any) => {
                  const dt = typeof d.date === 'string' && /^\d{4}-\d{2}-\d{2}/.test(d.date) ? d.date.slice(0, 10) : toDateKey(new Date(d.date))
                  return dt === todayKey
                })?.count ?? 0
                const weekdayEntries = dailyCounts.filter((d: any) => {
                  const dt = typeof d.date === 'string' ? new Date(d.date + 'T12:00:00') : new Date(d.date)
                  const day = dt.getDay()
                  return day >= 1 && day <= 5
                })
                const weekdayTotal = weekdayEntries.reduce((s: number, d: any) => s + (d.count || 0), 0)
                const weekdayDailyAvg = weekdayEntries.length > 0 ? (weekdayTotal / weekdayEntries.length).toFixed(1) : '0'

                return (
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '20px', marginBottom: '28px' }}>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>📊</div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Toplam Kayıt</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.totalIncidents}</div>
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>🟢</div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Açık İnsident</div>
                        <div style={{ color: '#10b981', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.openIncidents}</div>
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>🔴</div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Kapatılmış İnsident</div>
                        <div style={{ color: '#ef4444', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.closedIncidents}</div>
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>⏱️</div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Ort. Çözüm Süresi</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.averageResolutionDays} gün</div>
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>📈</div>
                      <div style={{ flex: 1 }}>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Son 1 Haftadaki Kayıt</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{lastWeekCount}</div>
                        {previousWeekCount > 0 && (
                          <div style={{ color: 'var(--text-secondary)', fontSize: '11px', marginTop: '4px' }}>
                            {previousWeekCount} (önceki hafta)
                            <span style={{
                              color: weekChangePositive ? '#10b981' : '#ef4444',
                              marginLeft: '8px',
                              fontWeight: '600'
                            }}>
                              {weekChangePositive ? '+' : ''}{weekChange}%
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>📅</div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Bugün Gelen</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{todayCount}</div>
                      </div>
                    </div>
                    <div style={{
                      background: 'var(--background-secondary)',
                      borderRadius: '10px',
                      padding: '24px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '16px'
                    }}>
                      <div style={{ fontSize: '36px' }}>📆</div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Haftaiçi Günlük Ort.</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{weekdayDailyAvg}</div>
                      </div>
                    </div>
                  </div>
                )
              })()}

              {/* Charts Section */}
              {mercekStatistics && (() => {
                const userDist: Array<{label: string, count: number}> = mercekStatistics.userDistribution || []
                const assignedDist: Array<{label: string, count: number}> = mercekStatistics.assignedUserDistribution || []
                const userChartPageSize = 5
                const assignedChartPageSize = 5
                const userTotalPages = Math.ceil(userDist.length / userChartPageSize)
                const assignedTotalPages = Math.ceil(assignedDist.length / assignedChartPageSize)
                const userPage = Math.min(userChartPage, userTotalPages || 1)
                const assignedPage = Math.min(assignedUserChartPage, assignedTotalPages || 1)
                const paginatedUsers = userDist.slice((userPage - 1) * userChartPageSize, userPage * userChartPageSize)
                const paginatedAssigned = assignedDist.slice((assignedPage - 1) * assignedChartPageSize, assignedPage * assignedChartPageSize)
                const userMaxCount = Math.max(...userDist.map(d => d.count), 1)
                const assignedMaxCount = Math.max(...assignedDist.map(d => d.count), 1)

                return (
                  <div style={{ marginBottom: '28px' }}>
                    <h3 style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', marginBottom: '20px' }}>Grafik Görselleştirme</h3>

                    {/* Filters */}
                    <div style={{
                      display: 'flex',
                      gap: '16px',
                      marginBottom: '20px',
                      flexWrap: 'wrap',
                      alignItems: 'flex-end'
                    }}>
                      <div style={{ flex: '1', minWidth: '180px' }}>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '6px', fontWeight: '500' }}>Kullanıcı</label>
                        <select value={csvSelectedUser} onChange={(e) => { setCsvSelectedUser(e.target.value); setCsvCurrentPage(1) }}
                          style={{ width: '100%', padding: '10px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}>
                          <option value="">Tümü</option>
                          {mercekAvailableUsers.map((user, idx) => (<option key={idx} value={user}>{user}</option>))}
                        </select>
                      </div>
                      <div style={{ flex: '1', minWidth: '180px' }}>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '6px', fontWeight: '500' }}>Atanan Kullanıcı</label>
                        <select value={mercekAssignedUserFilter} onChange={(e) => { setMercekAssignedUserFilter(e.target.value); setCsvCurrentPage(1) }}
                          style={{ width: '100%', padding: '10px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}>
                          <option value="">Tümü</option>
                          {mercekAssignedUsers.map((user, idx) => (<option key={idx} value={user}>{user}</option>))}
                        </select>
                      </div>
                      <div style={{ flex: '1', minWidth: '140px' }}>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '6px', fontWeight: '500' }}>Başlangıç Tarihi</label>
                        <input type="date" value={csvDateFrom} onChange={(e) => setCsvDateFrom(e.target.value)}
                          style={{ width: '100%', padding: '10px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                      </div>
                      <div style={{ flex: '1', minWidth: '140px' }}>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '6px', fontWeight: '500' }}>Bitiş Tarihi</label>
                        <input type="date" value={csvDateTo} onChange={(e) => setCsvDateTo(e.target.value)}
                          style={{ width: '100%', padding: '10px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                      </div>
                      <button onClick={() => { setCsvCurrentPage(1); fetchMercekData(1); fetchMercekStatistics() }} disabled={mercekLoading}
                        style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', background: mercekLoading ? 'var(--border)' : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', color: 'white', fontSize: '14px', fontWeight: '600', cursor: mercekLoading ? 'not-allowed' : 'pointer', height: 'fit-content', alignSelf: 'flex-end', marginBottom: '2px' }}>
                        {mercekLoading ? 'Yükleniyor...' : 'Filtrele'}
                      </button>
                      {(csvDateFrom || csvDateTo || csvSelectedUser || mercekAssignedUserFilter) && (
                        <button onClick={() => { setCsvDateFrom(''); setCsvDateTo(''); setCsvSelectedUser(''); setMercekAssignedUserFilter(''); setCsvCurrentPage(1); fetchMercekData(1); fetchMercekStatistics() }}
                          style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface-hover)', color: 'var(--text-primary)', fontSize: '14px', cursor: 'pointer', height: 'fit-content' }}>
                          Filtreleri Temizle
                        </button>
                      )}
                    </div>

                    {/* Charts Grid */}
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '24px', marginBottom: '28px' }}>

                      {/* Kullanıcı Dağılımı */}
                      {userDist.length > 0 && (
                        <div style={{ background: 'var(--background-secondary)', borderRadius: '10px', padding: '24px', overflow: 'hidden' }}>
                          <h4 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '20px' }}>
                            Kullanıcı Dağılımı <span style={{ color: 'var(--text-secondary)', fontWeight: '400', fontSize: '13px' }}>({userDist.length} kullanıcı)</span>
                          </h4>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                            {paginatedUsers.map((item, idx) => {
                              const percentage = (item.count / userMaxCount) * 100
                              return (
                                <div key={idx}>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                                    <span style={{ color: 'var(--text-primary)', fontSize: '14px', fontWeight: '500' }}>{item.label}</span>
                                    <span style={{ color: 'var(--text-secondary)', fontSize: '14px', fontWeight: '600' }}>{item.count}</span>
                                  </div>
                                  <div style={{ width: '100%', height: '26px', background: 'var(--background)', borderRadius: '6px', overflow: 'hidden' }}>
                                    <div style={{ width: `${percentage}%`, height: '100%', background: 'linear-gradient(90deg, #3b82f6, #60a5fa)', borderRadius: '6px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', paddingRight: percentage > 15 ? '8px' : '0', transition: 'width 0.3s' }}>
                                      {percentage > 15 && <span style={{ color: 'white', fontSize: '12px', fontWeight: '600' }}>{item.count}</span>}
                                    </div>
                                  </div>
                                </div>
                              )
                            })}
                          </div>
                          {userTotalPages > 1 && (
                            <div style={{ marginTop: '16px' }}>
                              <Pagination
                                currentPage={userPage}
                                totalPages={userTotalPages}
                                totalItems={userDist.length}
                                pageSize={userChartPageSize}
                                onPageChange={setUserChartPage}
                                showPageInput={true}
                                showFirstLast={true}
                                showTotalItems={true}
                                compact={true}
                              />
                            </div>
                          )}
                        </div>
                      )}

                      {/* Atanan Kullanıcı Dağılımı */}
                      {assignedDist.length > 0 && (
                        <div style={{ background: 'var(--background-secondary)', borderRadius: '10px', padding: '24px', overflow: 'hidden' }}>
                          <h4 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '20px' }}>
                            Atanan Kullanıcı Dağılımı <span style={{ color: 'var(--text-secondary)', fontWeight: '400', fontSize: '13px' }}>({assignedDist.length} kullanıcı)</span>
                          </h4>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                            {paginatedAssigned.map((item, idx) => {
                              const percentage = (item.count / assignedMaxCount) * 100
                              return (
                                <div key={idx}>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                                    <span style={{ color: 'var(--text-primary)', fontSize: '14px', fontWeight: '500' }}>{item.label}</span>
                                    <span style={{ color: 'var(--text-secondary)', fontSize: '14px', fontWeight: '600' }}>{item.count}</span>
                                  </div>
                                  <div style={{ width: '100%', height: '26px', background: 'var(--background)', borderRadius: '6px', overflow: 'hidden' }}>
                                    <div style={{ width: `${percentage}%`, height: '100%', background: 'linear-gradient(90deg, #8b5cf6, #a78bfa)', borderRadius: '6px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', paddingRight: percentage > 15 ? '8px' : '0', transition: 'width 0.3s' }}>
                                      {percentage > 15 && <span style={{ color: 'white', fontSize: '12px', fontWeight: '600' }}>{item.count}</span>}
                                    </div>
                                  </div>
                                </div>
                              )
                            })}
                          </div>
                          {assignedTotalPages > 1 && (
                            <div style={{ marginTop: '16px' }}>
                              <Pagination
                                currentPage={assignedPage}
                                totalPages={assignedTotalPages}
                                totalItems={assignedDist.length}
                                pageSize={assignedChartPageSize}
                                onPageChange={setAssignedUserChartPage}
                                showPageInput={true}
                                showFirstLast={true}
                                showTotalItems={true}
                                compact={true}
                              />
                            </div>
                          )}
                        </div>
                      )}

                      {/* Trend Chart - full width (Plotly) */}
                      {mercekStatistics && (() => {
                        const toDateKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
                        const getMonday = (d: Date) => {
                          const x = new Date(d);
                          const day = x.getDay();
                          const diff = day === 0 ? -6 : 1 - day;
                          x.setDate(x.getDate() + diff);
                          return toDateKey(x);
                        };
                        const today = new Date();
                        const hasFilter = !!(csvDateFrom || csvDateTo);
                        let rangeStart: Date;
                        let rangeEnd: Date;
                        if (csvDateFrom && csvDateTo) {
                          const d1 = new Date(csvDateFrom);
                          const d2 = new Date(csvDateTo);
                          rangeStart = d1 <= d2 ? d1 : d2;
                          rangeEnd = d1 <= d2 ? d2 : d1;
                        } else if (csvDateFrom) {
                          rangeStart = new Date(csvDateFrom);
                          rangeEnd = new Date(today);
                        } else if (csvDateTo) {
                          rangeEnd = new Date(csvDateTo);
                          rangeStart = new Date(rangeEnd);
                          rangeStart.setDate(rangeStart.getDate() - 89);
                        } else {
                          rangeStart = new Date(today);
                          rangeStart.setDate(today.getDate() - 89);
                          rangeEnd = new Date(today);
                        }
                        const days: Date[] = [];
                        for (const d = new Date(rangeStart); d <= rangeEnd; d.setDate(d.getDate() + 1)) {
                          days.push(new Date(d));
                        }
                        const countMap = new Map<string, number>();
                        (mercekStatistics.dailyCounts || []).forEach((d: any) => {
                          const dt = typeof d.date === 'string' && /^\d{4}-\d{2}-\d{2}/.test(d.date) ? d.date.slice(0, 10) : toDateKey(new Date(d.date));
                          countMap.set(dt, d.count);
                        });
                        const dateEntries = days.map((d) => {
                          const key = toDateKey(d);
                          return { date: key, count: countMap.get(key) ?? 0 };
                        });
                        const totalTrend = dateEntries.reduce((sum: number, d: any) => sum + d.count, 0);
                        const formatDateLabel = (s: string) => {
                          try { return new Date(s).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' }) } catch { return s }
                        };
                        const dates = dateEntries.map((d: any) => formatDateLabel(d.date));
                        const maxCount = Math.max(...dateEntries.map((d: any) => d.count), 1);
                        const WEEK_COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#06b6d4', '#84cc16', '#f97316', '#6366f1', '#14b8a6', '#a855f7', '#eab308'];
                        const weekGroups = new Map<string, typeof dateEntries>();
                        dateEntries.forEach((e: any) => {
                          const mondayKey = getMonday(new Date(e.date));
                          if (!weekGroups.has(mondayKey)) weekGroups.set(mondayKey, []);
                          weekGroups.get(mondayKey)!.push(e);
                        });
                        const sortedWeekKeys = [...weekGroups.keys()].sort();
                        const traces: any[] = [];
                        const annotations: any[] = [];
                        sortedWeekKeys.forEach((mondayKey, colorIdx) => {
                          const weekEntries = weekGroups.get(mondayKey)!;
                          const prevMondayKey = colorIdx > 0 ? sortedWeekKeys[colorIdx - 1] : null;
                          const prevWeekEntries = prevMondayKey ? weekGroups.get(prevMondayKey)! : [];
                          const bridgeEntry = prevWeekEntries.length > 0 ? prevWeekEntries[prevWeekEntries.length - 1] : null;
                          const weekDatesExt = bridgeEntry ? [formatDateLabel(bridgeEntry.date), ...weekEntries.map((d: any) => formatDateLabel(d.date))] : weekEntries.map((d: any) => formatDateLabel(d.date));
                          const weekCountsExt = bridgeEntry ? [bridgeEntry.count, ...weekEntries.map((d: any) => d.count)] : weekEntries.map((d: any) => d.count);
                          const weekTotal = weekEntries.reduce((a: number, e: any) => a + e.count, 0);
                          const color = WEEK_COLORS[colorIdx % WEEK_COLORS.length];
                          const fillRgba = color.replace('#', '').match(/.{2}/g)!.reduce((acc: string, hex: string) => acc + parseInt(hex, 16) + ',', 'rgba(').slice(0, -1) + ', 0.2)';
                          traces.push({
                            x: weekDatesExt,
                            y: weekCountsExt,
                            type: 'scatter',
                            mode: 'lines+markers',
                            fill: 'tozeroy',
                            fillcolor: fillRgba,
                            line: { color, width: 2.5, shape: 'spline' },
                            marker: { size: 5, color },
                            connectgaps: true,
                            hovertemplate: '<b>%{x}</b><br>Kayıt: %{y}<extra></extra>',
                            showlegend: false,
                          });
                          const midIdx = Math.floor(weekEntries.length / 2);
                          const annotX = formatDateLabel(weekEntries[midIdx].date);
                          annotations.push({
                            x: annotX,
                            y: 1,
                            text: `<b>${weekTotal}</b>`,
                            showarrow: false,
                            xref: 'x',
                            yref: 'y',
                            font: { size: 13, color, family: 'inherit' },
                            bgcolor: 'rgba(255,255,255,0.7)',
                            bordercolor: color,
                            borderwidth: 1,
                            borderpad: 4,
                          });
                        });

                        return (
                          <div style={{ background: 'var(--background-secondary)', borderRadius: '10px', padding: '28px', gridColumn: '1 / -1' }}>
                            <h4 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '16px' }}>
                              Tarih Trend Grafiği - Toplam: {totalTrend} kayıt ({dateEntries.length} gün) • Haftalar Pazartesi-Pazar
                            </h4>
                            <Plot
                              data={traces}
                              layout={{
                                margin: { t: 10, b: 120, l: 50, r: 20 },
                                paper_bgcolor: 'transparent',
                                plot_bgcolor: 'transparent',
                                height: 400,
                                annotations,
                                xaxis: {
                                  gridcolor: 'rgba(128,128,128,0.1)',
                                  tickvals: dates,
                                  tickmode: 'array',
                                  ticktext: dates,
                                  tickfont: { size: 10, color: '#888' },
                                  tickangle: -90,
                                  showgrid: true,
                                },
                                yaxis: {
                                  title: { text: 'Kayıt Sayısı', font: { size: 13, color: '#888' } },
                                  gridcolor: 'rgba(128,128,128,0.1)',
                                  tickfont: { size: 12, color: '#888' },
                                  zeroline: true,
                                  zerolinecolor: 'rgba(128,128,128,0.3)',
                                  rangemode: 'tozero',
                                },
                                showlegend: false,
                                hovermode: 'x unified',
                              }}
                              config={{ displayModeBar: false, responsive: true }}
                              style={{ width: '100%', height: '400px' }}
                            />
                          </div>
                        );
                      })()}
                    </div>
                  </div>
                )
              })()}

              {/* CSV Data Table */}
              {csvData.length > 0 && (() => {
                // First filter and sort ALL data, then paginate
                const filteredSortedData = getFilteredAndSortedMercekData(csvData)
                const totalItems = filteredSortedData.length
                const totalPages = Math.ceil(filteredSortedData.length / csvPageSize)
                const paginatedData = filteredSortedData.slice((csvCurrentPage - 1) * csvPageSize, csvCurrentPage * csvPageSize)
                const hasActiveFilters = 
                  Object.values(mercekColumnFilters).some(v => v && v.length > 0) ||
                  Object.values(mercekTextFilters).some(v => v && v.trim()) ||
                  Object.values(mercekTagFilters).some(v => v && v.length > 0) ||
                  Object.values(mercekDateFilters).some(v => v && (v.from || v.to))

                return (
                  <>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                      <h3 style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', margin: 0 }}>Veri Tablosu</h3>
                      {hasActiveFilters && (
                        <button
                          onClick={() => { 
                            setMercekColumnFilters({})
                            setMercekTextFilters({})
                            setMercekTagFilters({})
                            setTagSearchInput({})
                            setMercekDateFilters({})
                            setMercekSortColumn(null)
                            setOpenDropdownColumn(null)
                          }}
                          style={{
                            padding: '6px 14px',
                            borderRadius: '4px',
                            border: '1px solid var(--border)',
                            background: 'var(--background)',
                            color: 'var(--text-secondary)',
                            fontSize: '12px',
                            cursor: 'pointer'
                          }}
                        >
                          ✕ Filtreleri Temizle
                        </button>
                      )}
                    </div>

                    {/* Table */}
                    <div style={{ overflowX: 'auto', marginBottom: '16px' }}>
                      <table style={{
                        width: '100%',
                        borderCollapse: 'collapse',
                        background: 'var(--background)',
                        borderRadius: '4px',
                        overflow: 'hidden'
                      }}>
                        <thead>
                          {/* Combined header row with sort + filter */}
                          <tr style={{ background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
                            {csvHeaders.map((header, index) => {
                              const isSorted = mercekSortColumn === header
                              const isListbox = listboxColumns.includes(header)
                              const isTextSearch = textSearchColumns.includes(header)
                              const isTagSearch = tagSearchColumns.includes(header)
                              const isDate = dateColumns.includes(header)
                              const isDropdownOpen = openDropdownColumn === header
                              const selectedValues = mercekColumnFilters[header] || []
                              const searchQuery = dropdownSearchQuery[header] || ''
                              const dateFilter = mercekDateFilters[header] || { from: '', to: '' }
                              const textFilterValue = mercekTextFilters[header] || ''
                              const tagFilterValues = mercekTagFilters[header] || []
                              const tagInput = tagSearchInput[header] || ''
                              
                              // Check if this column has an active filter
                              const hasActiveFilter = isListbox 
                                ? selectedValues.length > 0 
                                : isTagSearch
                                  ? tagFilterValues.length > 0
                                  : isDate 
                                    ? (dateFilter.from || dateFilter.to) 
                                  : textFilterValue.length > 0
                              
                              // Filter unique values by search query
                              const filteredOptions = (uniqueColumnValues[header] || []).filter(v => 
                                v.toLowerCase().includes(searchQuery.toLowerCase())
                              )

                              return (
                                <th
                                  key={index}
                                  data-column-filter
                                  style={{
                                    padding: '12px 16px',
                                    textAlign: 'left',
                                    borderBottom: '2px solid var(--border)',
                                    color: isSorted ? '#3b82f6' : 'var(--text-primary)',
                                    fontWeight: '600',
                                    fontSize: '13px',
                                    whiteSpace: 'nowrap',
                                    position: 'relative'
                                  }}
                                >
                                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                    <span
                                      onClick={() => handleMercekSort(header)}
                                      style={{
                                        cursor: 'pointer',
                                        userSelect: 'none',
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '4px',
                                        flex: 1
                                      }}
                                    >
                                      {header}
                                      {isSorted && (
                                        <span style={{ fontSize: '10px' }}>
                                          {mercekSortDirection === 'asc' ? '↑' : '↓'}
                                        </span>
                                      )}
                                    </span>
                                    <span
                                      onClick={(e) => {
                                        e.stopPropagation()
                                        setOpenDropdownColumn(isDropdownOpen ? null : header)
                                      }}
                                      style={{
                                        cursor: 'pointer',
                                        fontSize: '12px',
                                        padding: '2px 6px',
                                        borderRadius: '4px',
                                        background: hasActiveFilter ? '#3b82f6' : 'transparent',
                                        color: hasActiveFilter ? '#ffffff' : 'var(--text-secondary)',
                                        border: hasActiveFilter ? 'none' : '1px solid var(--border)'
                                      }}
                                      title={`Filtrele: ${header}`}
                                    >
                                      {isListbox && selectedValues.length > 0 
                                        ? `${selectedValues.length}` 
                                        : isTagSearch && tagFilterValues.length > 0
                                          ? `${tagFilterValues.length}`
                                          : hasActiveFilter 
                                            ? '✓' 
                                            : '⋯'}
                                    </span>
                                  </div>
                                  {isDropdownOpen && (
                                    <div
                                      style={{
                                        position: 'absolute',
                                        top: '100%',
                                        left: 0,
                                        marginTop: '4px',
                                        background: 'var(--background)',
                                        border: '1px solid var(--border)',
                                        borderRadius: '6px',
                                        boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                                        zIndex: 1000,
                                        minWidth: isDate ? '280px' : '200px',
                                        maxHeight: '300px',
                                        overflowY: 'auto',
                                        padding: '8px'
                                      }}
                                      onClick={(e) => e.stopPropagation()}
                                    >
                                      {isListbox ? (
                                        /* Listbox filter */
                                        <>
                                          <input
                                            type="text"
                                            placeholder="Ara..."
                                            value={searchQuery}
                                            onChange={(e) => setDropdownSearchQuery(prev => ({ ...prev, [header]: e.target.value }))}
                                            autoFocus
                                            style={{
                                              width: '100%',
                                              padding: '6px 8px',
                                              borderRadius: '4px',
                                              border: '1px solid var(--border)',
                                              background: 'var(--surface)',
                                              color: 'var(--text-primary)',
                                              fontSize: '12px',
                                              marginBottom: '8px',
                                              boxSizing: 'border-box'
                                            }}
                                            onClick={(e) => e.stopPropagation()}
                                          />
                                          <div
                                            onClick={() => {
                                              if (selectedValues.length === filteredOptions.length) {
                                                handleMercekColumnFilter(header, [])
                                              } else {
                                                handleMercekColumnFilter(header, [...filteredOptions])
                                              }
                                            }}
                                            style={{
                                              padding: '6px 8px',
                                              borderBottom: '1px solid var(--border)',
                                              cursor: 'pointer',
                                              fontSize: '11px',
                                              fontWeight: '600',
                                              color: 'var(--text-primary)',
                                              background: selectedValues.length === filteredOptions.length ? 'var(--surface-hover)' : 'transparent',
                                              marginBottom: '4px'
                                            }}
                                          >
                                            {selectedValues.length === filteredOptions.length ? '✓ Seçimi Kaldır' : 'Tümünü Seç'}
                                          </div>
                                          {filteredOptions.slice(0, 100).map(value => (
                                            <label
                                              key={value}
                                              style={{
                                                display: 'flex',
                                                alignItems: 'center',
                                                padding: '6px 8px',
                                                cursor: 'pointer',
                                                fontSize: '12px',
                                                color: 'var(--text-primary)',
                                                borderBottom: '1px solid var(--border)',
                                                background: selectedValues.includes(value) ? 'var(--surface-hover)' : 'transparent'
                                              }}
                                              onClick={(e) => e.stopPropagation()}
                                            >
                                              <input
                                                type="checkbox"
                                                checked={selectedValues.includes(value)}
                                                onChange={(e) => {
                                                  e.stopPropagation()
                                                  toggleDropdownValue(header, value)
                                                }}
                                                style={{ marginRight: '8px', cursor: 'pointer' }}
                                              />
                                              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{value}</span>
                                            </label>
                                          ))}
                                          {filteredOptions.length > 100 && (
                                            <div style={{ padding: '6px 8px', fontSize: '10px', color: 'var(--text-secondary)', textAlign: 'center' }}>
                                              +{filteredOptions.length - 100} daha...
                                            </div>
                                          )}
                                        </>
                                      ) : isTagSearch ? (
                                        /* Tag-based multi-select search filter */
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                          <div style={{ display: 'flex', gap: '4px' }}>
                                            <input
                                              type="text"
                                              value={tagInput}
                                              onChange={(e) => setTagSearchInput((prev: Record<string, string>) => ({ ...prev, [header]: e.target.value }))}
                                              onKeyDown={(e) => {
                                                if (e.key === 'Enter' && tagInput.trim()) {
                                                  addTagFilter(header, tagInput)
                                                }
                                              }}
                                              placeholder="Kelime girin + Enter"
                                              autoFocus
                                              style={{
                                                flex: 1,
                                                padding: '6px 8px',
                                                borderRadius: '4px',
                                                border: '1px solid var(--border)',
                                                background: 'var(--surface)',
                                                color: 'var(--text-primary)',
                                                fontSize: '12px',
                                                boxSizing: 'border-box'
                                              }}
                                            />
                                            <button
                                              onClick={() => addTagFilter(header, tagInput)}
                                              disabled={!tagInput.trim()}
                                              style={{
                                                padding: '6px 10px',
                                                borderRadius: '4px',
                                                border: 'none',
                                                background: tagInput.trim() ? '#3b82f6' : 'var(--border)',
                                                color: 'white',
                                                fontSize: '11px',
                                                cursor: tagInput.trim() ? 'pointer' : 'not-allowed'
                                              }}
                                            >
                                              Ekle
                                            </button>
                                          </div>
                                          {tagFilterValues.length > 0 && (
                                            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                                              {tagFilterValues.map((tag: string, tagIdx: number) => (
                                                <span
                                                  key={tagIdx}
                                                  style={{
                                                    display: 'inline-flex',
                                                    alignItems: 'center',
                                                    gap: '4px',
                                                    padding: '4px 8px',
                                                    background: 'rgba(59, 130, 246, 0.15)',
                                                    color: '#3b82f6',
                                                    borderRadius: '12px',
                                                    fontSize: '11px',
                                                    fontWeight: '500'
                                                  }}
                                                >
                                                  {tag}
                                                  <button
                                                    onClick={() => removeTagFilter(header, tag)}
                                                    style={{
                                                      background: 'none',
                                                      border: 'none',
                                                      color: '#3b82f6',
                                                      cursor: 'pointer',
                                                      padding: '0',
                                                      fontSize: '14px',
                                                      lineHeight: '1',
                                                      display: 'flex',
                                                      alignItems: 'center'
                                                    }}
                                                  >
                                                    ×
                                                  </button>
                                                </span>
                                              ))}
                                            </div>
                                          )}
                                          {tagFilterValues.length > 0 && (
                                            <button
                                              onClick={() => clearTagFilters(header)}
                                              style={{
                                                padding: '6px 8px',
                                                borderRadius: '4px',
                                                border: 'none',
                                                background: 'rgba(239, 68, 68, 0.1)',
                                                color: '#ef4444',
                                                fontSize: '11px',
                                                cursor: 'pointer'
                                              }}
                                            >
                                              ✕ Tümünü Temizle
                                            </button>
                                          )}
                                          <div style={{ fontSize: '10px', color: 'var(--text-secondary)', fontStyle: 'italic' }}>
                                            Birden fazla kelime ekleyebilirsiniz. Herhangi birini içeren kayıtlar gösterilir.
                                          </div>
                                        </div>
                                      ) : isDate ? (
                                        /* Date range filter */
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                          <div>
                                            <label style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '4px', display: 'block' }}>Başlangıç</label>
                                            <input
                                              type="date"
                                              value={dateFilter.from}
                                              onChange={(e) => handleMercekDateFilter(header, 'from', e.target.value)}
                                              style={{
                                                width: '100%',
                                                padding: '6px 8px',
                                                borderRadius: '4px',
                                                border: '1px solid var(--border)',
                                                background: 'var(--surface)',
                                                color: 'var(--text-primary)',
                                                fontSize: '12px',
                                                boxSizing: 'border-box'
                                              }}
                                            />
                                          </div>
                                          <div>
                                            <label style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '4px', display: 'block' }}>Bitiş</label>
                                            <input
                                              type="date"
                                              value={dateFilter.to}
                                              onChange={(e) => handleMercekDateFilter(header, 'to', e.target.value)}
                                              style={{
                                                width: '100%',
                                                padding: '6px 8px',
                                                borderRadius: '4px',
                                                border: '1px solid var(--border)',
                                                background: 'var(--surface)',
                                                color: 'var(--text-primary)',
                                                fontSize: '12px',
                                                boxSizing: 'border-box'
                                              }}
                                            />
                                          </div>
                                          {(dateFilter.from || dateFilter.to) && (
                                            <button
                                              onClick={() => {
                                                handleMercekDateFilter(header, 'from', '')
                                                handleMercekDateFilter(header, 'to', '')
                                              }}
                                              style={{
                                                padding: '6px 8px',
                                                borderRadius: '4px',
                                                border: 'none',
                                                background: 'rgba(239, 68, 68, 0.1)',
                                                color: '#ef4444',
                                                fontSize: '11px',
                                                cursor: 'pointer'
                                              }}
                                            >
                                              ✕ Tarihleri Temizle
                                            </button>
                                          )}
                                        </div>
                                      ) : (
                                        /* Text search filter */
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                          <input
                                            type="text"
                                            value={textFilterValue}
                                            onChange={(e) => handleMercekTextFilter(header, e.target.value)}
                                            placeholder="Ara..."
                                            autoFocus
                                            style={{
                                              width: '100%',
                                              padding: '6px 8px',
                                              borderRadius: '4px',
                                              border: '1px solid var(--border)',
                                              background: 'var(--surface)',
                                              color: 'var(--text-primary)',
                                              fontSize: '12px',
                                              boxSizing: 'border-box'
                                            }}
                                          />
                                          {textFilterValue && (
                                            <button
                                              onClick={() => handleMercekTextFilter(header, '')}
                                              style={{
                                                padding: '6px 8px',
                                                borderRadius: '4px',
                                                border: 'none',
                                                background: 'rgba(239, 68, 68, 0.1)',
                                                color: '#ef4444',
                                                fontSize: '11px',
                                                cursor: 'pointer'
                                              }}
                                            >
                                              ✕ Temizle
                                            </button>
                                          )}
                                        </div>
                                      )}
                                    </div>
                                  )}
                                </th>
                              )
                            })}
                          </tr>
                        </thead>
                        <tbody>
                          {paginatedData.length === 0 ? (
                            <tr>
                              <td colSpan={csvHeaders.length} style={{ padding: '24px', textAlign: 'center', color: 'var(--text-secondary)', fontSize: '13px' }}>
                                Filtre sonucu kayıt bulunamadı
                              </td>
                            </tr>
                          ) : paginatedData.map((row, rowIndex) => (
                            <tr
                              key={rowIndex}
                              style={{
                                borderBottom: '1px solid var(--border)',
                                background: rowIndex % 2 === 0 ? 'var(--surface)' : 'var(--background)'
                              }}
                            >
                              {csvHeaders.map((header, colIndex) => (
                                <td
                                  key={colIndex}
                                  style={{
                                    padding: '12px',
                                    color: 'var(--text-primary)',
                                    fontSize: '13px',
                                    maxWidth: '300px',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap'
                                  }}
                                  title={String(row[header] || '')}
                                >
                                  {row[header] || ''}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>

                    {/* Pagination */}
                    <Pagination
                      currentPage={csvCurrentPage}
                      totalPages={totalPages || 1}
                      totalItems={totalItems}
                      pageSize={csvPageSize}
                      onPageChange={(page) => {
                        setCsvCurrentPage(page)
                      }}
                      onPageSizeChange={(size) => {
                        setCsvPageSize(size)
                        setCsvCurrentPage(1)
                      }}
                      showPageInput={true}
                      showFirstLast={true}
                      showTotalItems={true}
                    />
                  </>
                )
              })()}

              {csvData.length === 0 && !mercekLoading && mercekDataLoaded && (
                <div style={{
                  padding: '40px',
                  textAlign: 'center',
                  color: 'var(--text-secondary)',
                  fontSize: '14px'
                }}>
                  Mercek veritabanında kayıt bulunamadı
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
