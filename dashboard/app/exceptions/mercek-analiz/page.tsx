'use client'

import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react'
import dynamic from 'next/dynamic'
import apiClient from '@/lib/axios'
import Pagination from '@/components/ui/Pagination'
import GridExport from '@/components/ui/GridExport'
import {
  BarChart2,
  BarChart3,
  Circle,
  CircleDot,
  Clock,
  Calendar,
  Sun,
  CalendarRange,
  Square,
  X,
  ChevronUp,
  ChevronDown,
  Filter,
  MoreHorizontal,
  LayoutDashboard,
  Search,
  TrendingUp,
  TableProperties,
  Activity,
  Shield,
  Sparkles,
  RefreshCw
} from 'lucide-react'

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
  const [syncingReleased, setSyncingReleased] = useState(false)
  const [releasedSyncResult, setReleasedSyncResult] = useState<string | null>(null)

  // CSV / Mercek Analysis States
  const [csvData, setCsvData] = useState<any[]>([])
  const [csvHeaders, setCsvHeaders] = useState<string[]>([])
  const [csvSearchQuery, setCsvSearchQuery] = useState('')
  const [csvPageSize, setCsvPageSize] = useState(10)
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
      const params: any = { page: 1, pageSize: 10000 }
      if (csvDateFrom) params.startDate = csvDateFrom
      if (csvDateTo) params.endDate = csvDateTo
      const response = await apiClient.get('/api/released-incidents', { params })
      const data = response.data?.data || []
      setReleasedIncidents(data)
    } catch (error) {
      console.error('Error fetching released incidents:', error)
      setReleasedIncidents([])
    } finally {
      setLoadingReleasedIncidents(false)
    }
  }

  const syncReleasedFromDlpApi = async (lookbackHours: number = 24) => {
    setSyncingReleased(true)
    setReleasedSyncResult(null)
    try {
      const response = await apiClient.post('/api/released-incidents/sync', null, {
        params: { lookbackHours }
      })
      const data = response.data
      setReleasedSyncResult(
        data.success
          ? `${data.inserted} yeni kayıt eklendi (${data.skipped} zaten mevcut)`
          : data.error || 'Released incident bulunamadı'
      )
      await fetchReleasedIncidents()
    } catch (error: any) {
      const msg = error?.response?.data?.error || error?.response?.data?.detail || error.message
      setReleasedSyncResult(`Hata: ${msg}`)
    } finally {
      setSyncingReleased(false)
      setTimeout(() => setReleasedSyncResult(null), 8000)
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

      const mappedData = (data.items || []).map((item: any) => ({
        'Olay No': item.incident_id ?? item.incidentId,
        'Olay Açıklaması': item.incident_description ?? item.incidentDescription,
        'Atanan Kullanıcı': item.assigned_user_code ?? item.assignedUserCode,
        'Sistem Tarihi': (item.system_date || item.systemDate) ? new Date(item.system_date || item.systemDate).toLocaleString('tr-TR') : '',
        'Açılış Tarihi': (item.open_date || item.openDate) ? new Date(item.open_date || item.openDate).toLocaleString('tr-TR') : '',
        'Kapanış Tarihi': (item.close_date || item.closeDate) ? new Date(item.close_date || item.closeDate).toLocaleString('tr-TR') : '',
        'Başlangıç Tarihi': (item.start_date || item.startDate) ? new Date(item.start_date || item.startDate).toLocaleString('tr-TR') : '',
        'Çözüm Yöntemi': item.solution_method ?? item.solutionMethod,
        'Kullanıcı Adı': item.user_name ?? item.userName
      }))

      setCsvData(mappedData)
      setMercekTotalCount(data.total_count ?? data.totalCount)
      setMercekTotalPages(data.total_pages ?? data.totalPages)
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
      setMercekAssignedUsers(data.assigned_users || data.assignedUsers || [])
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
    }

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

  // Released Incidents stats should update when filters change
  useEffect(() => {
    fetchReleasedIncidents()
  }, [csvSelectedUser, mercekAssignedUserFilter, csvDateFrom, csvDateTo, csvSearchQuery])

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
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div style={{
              width: '38px',
              height: '38px',
              borderRadius: '10px',
              background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 4px 12px rgba(99, 102, 241, 0.3)'
            }}>
              <Search size={20} color="#fff" />
            </div>
            <h1 style={{ fontSize: '24px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #6366f1, #8b5cf6)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>Mercek Analysis</h1>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', marginBottom: '24px' }}>

          {/* ══════════════ Released Incidents Section (order: 2) ══════════════ */}
          <div style={{ order: 2, marginBottom: '24px' }}>
            <div style={{
              background: 'var(--surface)',
              borderRadius: '10px',
              border: '1px solid var(--border)',
              padding: '24px',
              boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
              borderTop: '3px solid #f59e0b'
            }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '20px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <div style={{
                    width: '32px',
                    height: '32px',
                    borderRadius: '8px',
                    background: 'linear-gradient(135deg, #f59e0b, #ef4444)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxShadow: '0 3px 10px rgba(245, 158, 11, 0.25)'
                  }}>
                    <Activity size={17} color="#fff" />
                  </div>
                  <h2 style={{ fontSize: '18px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #f59e0b, #ef4444)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                    Released Incidents İstatistikleri
                  </h2>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  {releasedSyncResult && (
                    <span style={{ fontSize: '12px', color: releasedSyncResult.startsWith('Hata') ? '#ef4444' : '#10b981', fontWeight: '500' }}>
                      {releasedSyncResult}
                    </span>
                  )}
                  <button
                    onClick={() => syncReleasedFromDlpApi(24)}
                    disabled={syncingReleased}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px',
                      padding: '6px 14px',
                      borderRadius: '8px',
                      border: '1px solid rgba(245, 158, 11, 0.3)',
                      background: syncingReleased ? 'rgba(245, 158, 11, 0.08)' : 'rgba(245, 158, 11, 0.12)',
                      color: '#f59e0b',
                      fontSize: '13px',
                      fontWeight: '600',
                      cursor: syncingReleased ? 'not-allowed' : 'pointer',
                      opacity: syncingReleased ? 0.7 : 1,
                      transition: 'all 0.2s ease'
                    }}
                    title="DLP API'den released incident verilerini çek ve veritabanına kaydet"
                  >
                    <RefreshCw size={14} style={{ animation: syncingReleased ? 'spin 1s linear infinite' : 'none' }} />
                    {syncingReleased ? 'Senkronize ediliyor...' : 'DLP API Senkronize Et'}
                  </button>
                </div>
              </div>

              {loadingReleasedIncidents ? (
                <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  Yükleniyor...
                </div>
              ) : (
                <>
                  {/* Toplam Sayı ve INC İstatistikleri */}
                  <div style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
                    gap: '20px',
                    marginBottom: '28px'
                  }}>
                    <div style={{ background: 'rgba(99, 102, 241, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(99, 102, 241, 0.2)' }}>
                      <div style={{ color: '#4338ca' }}><BarChart3 size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Toplam Released Incident</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{releasedIncidentsStats.totalCount}</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(59, 130, 246, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(59, 130, 246, 0.2)' }}>
                      <div style={{ color: '#1d4ed8' }}><Circle size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>INC ile Başlayan</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{releasedIncidentsStats.incStartingCount}</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(239, 68, 68, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
                      <div style={{ color: '#b91c1c' }}><CircleDot size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>INC ile Başlamayan</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{releasedIncidentsStats.nonIncStartingCount}</div>
                      </div>
                    </div>
                  </div>
                  {/* Admin Bazlı Sütun Grafik */}
                  {releasedIncidentsStats.adminData.length > 0 && (
                    <div style={{ marginTop: '24px', background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.04), rgba(239, 68, 68, 0.04))', borderRadius: '12px', padding: '24px', border: '1px solid rgba(245, 158, 11, 0.12)' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
                        <Shield size={16} style={{ color: '#f59e0b' }} />
                        <h3 style={{ fontSize: '16px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #f59e0b, #ef4444)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                          Admin Bazlı Released Incident Sayıları
                        </h3>
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                        {releasedIncidentsStats.adminData.map(({ admin, count }: { admin: string, count: number }) => {
                          const maxCount = Math.max(...releasedIncidentsStats.adminData.map((d: { admin: string, count: number }) => d.count))
                          const percentage = maxCount > 0 ? (count / maxCount) * 100 : 0
                          return (
                            <div key={admin} style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                              <div style={{ minWidth: '150px', fontSize: '13px', color: 'var(--text-primary)', fontWeight: '500' }}>{admin}</div>
                              <div style={{ flex: 1, height: '32px', background: 'var(--background-secondary)', borderRadius: '4px', position: 'relative', overflow: 'hidden', border: '1px solid var(--border)' }}>
                                <div style={{ height: '100%', width: `${percentage}%`, background: 'linear-gradient(90deg, #3b82f6 0%, #2563eb 100%)', borderRadius: '4px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', paddingRight: '8px', transition: 'width 0.3s ease', minWidth: 'fit-content' }}>
                                  <span style={{ fontSize: '12px', fontWeight: '600', color: '#fff', whiteSpace: 'nowrap' }}>{count}</span>
                                </div>
                              </div>
                              <div style={{ minWidth: '50px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500', textAlign: 'right' }}>%{percentage.toFixed(1)}</div>
                            </div>
                          )
                        })}
                      </div>
                    </div>
                  )}
                  {releasedIncidentsStats.adminData.length === 0 && (
                    <div style={{ color: 'var(--text-secondary)', fontSize: '13px', marginTop: '16px' }}>
                      Admin bazlı veri bulunamadı.
                    </div>
                  )}
                </>
              )}
            </div>
          </div>

          {/* ══════════════ Mercek Statistics Section (order: 1) ══════════════ */}
          <div style={{ order: 1, marginBottom: '24px' }}>
            <div style={{
              background: 'var(--surface)',
              borderRadius: '10px',
              border: '1px solid var(--border)',
              padding: '24px',
              boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
              borderTop: '3px solid #3b82f6'
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '20px' }}>
                <div style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: '8px',
                  background: 'linear-gradient(135deg, #3b82f6, #06b6d4)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  boxShadow: '0 3px 10px rgba(59, 130, 246, 0.25)'
                }}>
                  <BarChart2 size={17} color="#fff" />
                </div>
                <h2 style={{ fontSize: '18px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #3b82f6, #06b6d4)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                  Mercek İstatistikleri
                </h2>
              </div>

              {mercekLoading ? (
                <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  Yükleniyor...
                </div>
              ) : mercekStatistics && (() => {
                const lastWeekCount = mercekStatistics.last_week_count ?? mercekStatistics.lastWeekCount ?? 0
                const previousWeekCount = mercekStatistics.previous_week_count ?? mercekStatistics.previousWeekCount ?? 0
                const weekChange = previousWeekCount > 0
                  ? ((lastWeekCount - previousWeekCount) / previousWeekCount * 100).toFixed(1)
                  : '0'
                const weekChangePositive = Number(weekChange) >= 0

                const toDateKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
                const todayKey = toDateKey(new Date())
                const dailyCounts = mercekStatistics.daily_counts || mercekStatistics.dailyCounts || []
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
                    <div style={{ background: 'rgba(99, 102, 241, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(99, 102, 241, 0.2)' }}>
                      <div style={{ color: '#4338ca' }}><BarChart2 size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Toplam Kayıt</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.total_incidents ?? mercekStatistics.totalIncidents}</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(16, 185, 129, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(16, 185, 129, 0.2)' }}>
                      <div style={{ color: '#047857' }}><CircleDot size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Açık Incident</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.open_incidents ?? mercekStatistics.openIncidents}</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(239, 68, 68, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
                      <div style={{ color: '#b91c1c' }}><CircleDot size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Kapatılmış Incident</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.closed_incidents ?? mercekStatistics.closedIncidents}</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(59, 130, 246, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(59, 130, 246, 0.2)' }}>
                      <div style={{ color: '#1d4ed8' }}><Clock size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Ort. Çözüm Süresi</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{mercekStatistics.average_resolution_days ?? mercekStatistics.averageResolutionDays} gün</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(139, 92, 246, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(139, 92, 246, 0.2)' }}>
                      <div style={{ color: '#6d28d9' }}><Calendar size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Son 1 Haftadaki Kayıt</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '24px', fontWeight: '700' }}>{lastWeekCount}</div>
                        {previousWeekCount > 0 && (
                          <div style={{ color: 'var(--text-secondary)', fontSize: '11px', marginTop: '4px' }}>
                            {previousWeekCount} (önceki hafta)
                            <span style={{ color: weekChangePositive ? '#10b981' : '#ef4444', marginLeft: '8px', fontWeight: '600' }}>
                              {weekChangePositive ? '+' : ''}{weekChange}%
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                    <div style={{ background: 'rgba(245, 158, 11, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(245, 158, 11, 0.2)' }}>
                      <div style={{ color: '#b45309' }}><Sun size={32} /></div>
                      <div>
                        <div style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '4px', fontWeight: '500' }}>Bugün Gelen</div>
                        <div style={{ color: 'var(--text-primary)', fontSize: '32px', fontWeight: '700' }}>{todayCount}</div>
                      </div>
                    </div>
                    <div style={{ background: 'rgba(6, 182, 212, 0.12)', borderRadius: '16px', padding: '24px', display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '16px', border: '1px solid rgba(6, 182, 212, 0.2)' }}>
                      <div style={{ color: '#0e7490' }}><CalendarRange size={32} /></div>
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
                const userDist: Array<{ label: string, count: number }> = mercekStatistics.user_distribution || mercekStatistics.userDistribution || []
                const assignedDist: Array<{ label: string, count: number }> = mercekStatistics.assigned_user_distribution || mercekStatistics.assignedUserDistribution || []
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
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '20px' }}>
                      <div style={{
                        width: '28px',
                        height: '28px',
                        borderRadius: '7px',
                        background: 'linear-gradient(135deg, #8b5cf6, #a855f7)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        boxShadow: '0 3px 8px rgba(139, 92, 246, 0.25)'
                      }}>
                        <BarChart3 size={15} color="#fff" />
                      </div>
                      <h3 style={{ fontSize: '20px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #8b5cf6, #a855f7)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>Kullanıcı Dağılımları</h3>
                    </div>

                    {/* Filters */}
                    <div style={{
                      display: 'grid',
                      gridTemplateColumns: 'repeat(5, 1fr)',
                      gap: '14px',
                      marginBottom: '20px',
                      alignItems: 'end',
                      padding: '16px',
                      background: 'rgba(139, 92, 246, 0.03)',
                      borderRadius: '10px',
                      border: '1px solid rgba(139, 92, 246, 0.1)'
                    }}>
                      <div>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '13px', marginBottom: '6px', fontWeight: '500' }}>Kullanıcı</label>
                        <select value={csvSelectedUser} onChange={(e) => { setCsvSelectedUser(e.target.value); setCsvCurrentPage(1) }}
                          style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}>
                          <option value="">Tümü</option>
                          {mercekAvailableUsers.map((user, idx) => (<option key={idx} value={user}>{user}</option>))}
                        </select>
                      </div>
                      <div>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '13px', marginBottom: '6px', fontWeight: '500' }}>Atanan Kullanıcı</label>
                        <select value={mercekAssignedUserFilter} onChange={(e) => { setMercekAssignedUserFilter(e.target.value); setCsvCurrentPage(1) }}
                          style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}>
                          <option value="">Tümü</option>
                          {mercekAssignedUsers.map((user, idx) => (<option key={idx} value={user}>{user}</option>))}
                        </select>
                      </div>
                      <div>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '13px', marginBottom: '6px', fontWeight: '500' }}>Başlangıç Tarihi</label>
                        <input type="date" value={csvDateFrom} onChange={(e) => setCsvDateFrom(e.target.value)}
                          style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }} />
                      </div>
                      <div>
                        <label style={{ display: 'block', color: 'var(--text-secondary)', fontSize: '13px', marginBottom: '6px', fontWeight: '500' }}>Bitiş Tarihi</label>
                        <input type="date" value={csvDateTo} onChange={(e) => setCsvDateTo(e.target.value)}
                          style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }} />
                      </div>
                      <div style={{ display: 'flex', gap: '8px' }}>
                        <button onClick={() => { setCsvCurrentPage(1); fetchMercekData(1); fetchMercekStatistics() }} disabled={mercekLoading}
                          style={{ flex: 1, padding: '9px 16px', borderRadius: '6px', border: 'none', background: mercekLoading ? 'var(--border)' : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', color: 'white', fontSize: '13px', fontWeight: '600', cursor: mercekLoading ? 'not-allowed' : 'pointer' }}>
                          {mercekLoading ? 'Yükleniyor...' : 'Filtrele'}
                        </button>
                        {(csvDateFrom || csvDateTo || csvSelectedUser || mercekAssignedUserFilter) && (
                          <button onClick={() => { setCsvDateFrom(''); setCsvDateTo(''); setCsvSelectedUser(''); setMercekAssignedUserFilter(''); setCsvCurrentPage(1); fetchMercekData(1); fetchMercekStatistics() }}
                            style={{ padding: '9px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface-hover)', color: 'var(--text-primary)', fontSize: '13px', cursor: 'pointer' }}>
                            Temizle
                          </button>
                        )}
                      </div>
                    </div>

                    {/* Charts Grid */}
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '24px', marginBottom: '28px' }}>

                      {/* Kullanıcı Dağılımı */}
                      {userDist.length > 0 && (
                        <div style={{ background: 'linear-gradient(135deg, rgba(59, 130, 246, 0.04), rgba(96, 165, 250, 0.04))', borderRadius: '12px', padding: '24px', overflow: 'hidden', border: '1px solid rgba(59, 130, 246, 0.1)' }}>
                          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '20px' }}>
                            <h4 style={{ fontSize: '16px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #3b82f6, #60a5fa)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                              Kullanıcı Dağılımı
                            </h4>
                            <span style={{ background: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6', padding: '3px 10px', borderRadius: '16px', fontSize: '12px', fontWeight: '600', border: '1px solid rgba(59, 130, 246, 0.2)' }}>{userDist.length} kullanıcı</span>
                          </div>
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
                        <div style={{ background: 'linear-gradient(135deg, rgba(139, 92, 246, 0.04), rgba(167, 139, 250, 0.04))', borderRadius: '12px', padding: '24px', overflow: 'hidden', border: '1px solid rgba(139, 92, 246, 0.1)' }}>
                          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '20px' }}>
                            <h4 style={{ fontSize: '16px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                              Atanan Kullanıcı Dağılımı
                            </h4>
                            <span style={{ background: 'rgba(139, 92, 246, 0.1)', color: '#8b5cf6', padding: '3px 10px', borderRadius: '16px', fontSize: '12px', fontWeight: '600', border: '1px solid rgba(139, 92, 246, 0.2)' }}>{assignedDist.length} kullanıcı</span>
                          </div>
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
                        (mercekStatistics.daily_counts || mercekStatistics.dailyCounts || []).forEach((d: any) => {
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
                        const sortedWeekKeys = Array.from(weekGroups.keys()).sort();
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
                          <div style={{
                            background: 'linear-gradient(135deg, rgba(99, 102, 241, 0.04) 0%, rgba(139, 92, 246, 0.04) 50%, rgba(59, 130, 246, 0.04) 100%)',
                            borderRadius: '14px',
                            padding: '28px',
                            gridColumn: '1 / -1',
                            border: '1px solid rgba(99, 102, 241, 0.12)',
                            boxShadow: '0 4px 16px rgba(99, 102, 241, 0.06)'
                          }}>
                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '20px' }}>
                              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                <div style={{
                                  width: '32px',
                                  height: '32px',
                                  borderRadius: '8px',
                                  background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
                                  display: 'flex',
                                  alignItems: 'center',
                                  justifyContent: 'center',
                                  boxShadow: '0 3px 10px rgba(99, 102, 241, 0.3)'
                                }}>
                                  <TrendingUp size={17} color="#fff" />
                                </div>
                                <h4 style={{ fontSize: '16px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #6366f1, #8b5cf6)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                                  Tarih Trend Grafiği
                                </h4>
                              </div>
                              <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                                <span style={{
                                  background: 'rgba(99, 102, 241, 0.1)',
                                  color: '#6366f1',
                                  padding: '5px 12px',
                                  borderRadius: '20px',
                                  fontSize: '12px',
                                  fontWeight: '600',
                                  border: '1px solid rgba(99, 102, 241, 0.2)'
                                }}>
                                  {totalTrend} kayıt
                                </span>
                                <span style={{
                                  background: 'rgba(139, 92, 246, 0.1)',
                                  color: '#8b5cf6',
                                  padding: '5px 12px',
                                  borderRadius: '20px',
                                  fontSize: '12px',
                                  fontWeight: '600',
                                  border: '1px solid rgba(139, 92, 246, 0.2)'
                                }}>
                                  {dateEntries.length} gün
                                </span>
                                <span style={{
                                  background: 'rgba(59, 130, 246, 0.1)',
                                  color: '#3b82f6',
                                  padding: '5px 12px',
                                  borderRadius: '20px',
                                  fontSize: '12px',
                                  fontWeight: '600',
                                  border: '1px solid rgba(59, 130, 246, 0.2)'
                                }}>
                                  Pzt-Paz
                                </span>
                              </div>
                            </div>
                            <div style={{
                              background: 'var(--surface)',
                              borderRadius: '10px',
                              padding: '16px 12px 8px 12px',
                              border: '1px solid var(--border)'
                            }}>
                              <Plot
                                data={traces}
                                layout={{
                                  margin: { t: 10, b: 120, l: 50, r: 20 },
                                  paper_bgcolor: 'transparent',
                                  plot_bgcolor: 'transparent',
                                  height: 420,
                                  annotations,
                                  xaxis: {
                                    gridcolor: 'rgba(128,128,128,0.08)',
                                    tickvals: dates,
                                    tickmode: 'array',
                                    ticktext: dates,
                                    tickfont: { size: 10, color: '#999', family: 'Inter, system-ui, sans-serif' },
                                    tickangle: -90,
                                    showgrid: true,
                                    showline: true,
                                    linecolor: 'rgba(128,128,128,0.15)',
                                    linewidth: 1,
                                  },
                                  yaxis: {
                                    title: { text: 'Kayıt Sayısı', font: { size: 13, color: '#999', family: 'Inter, system-ui, sans-serif' } },
                                    gridcolor: 'rgba(128,128,128,0.08)',
                                    tickfont: { size: 12, color: '#999', family: 'Inter, system-ui, sans-serif' },
                                    zeroline: true,
                                    zerolinecolor: 'rgba(128,128,128,0.2)',
                                    rangemode: 'tozero',
                                    showline: true,
                                    linecolor: 'rgba(128,128,128,0.15)',
                                    linewidth: 1,
                                  },
                                  showlegend: false,
                                  hovermode: 'x unified',
                                  hoverlabel: {
                                    bgcolor: 'rgba(255,255,255,0.95)',
                                    bordercolor: 'rgba(99,102,241,0.3)',
                                    font: { size: 13, color: '#333', family: 'Inter, system-ui, sans-serif' }
                                  },
                                }}
                                config={{ displayModeBar: false, responsive: true }}
                                style={{ width: '100%', height: '420px' }}
                              />
                            </div>
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
                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <div style={{
                          width: '28px',
                          height: '28px',
                          borderRadius: '7px',
                          background: 'linear-gradient(135deg, #10b981, #06b6d4)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          boxShadow: '0 3px 8px rgba(16, 185, 129, 0.25)'
                        }}>
                          <TableProperties size={15} color="#fff" />
                        </div>
                        <h3 style={{ fontSize: '20px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #10b981, #06b6d4)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>Veri Tablosu</h3>
                      </div>
                      <div style={{ display: 'flex', gap: '8px' }}>
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
                            <X size={14} /> Filtreleri Temizle
                          </button>
                        )}
                        <GridExport
                          data={filteredSortedData}
                          fileName="mercek-verileri"
                          columns={csvHeaders.map(h => ({ key: h, header: h, width: 25 }))}
                        />
                      </div>
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
                          {/* First Row: Sortable Headers */}
                          <tr style={{ background: 'rgba(16, 185, 129, 0.06)', borderBottom: '2px solid rgba(16, 185, 129, 0.15)' }}>
                            {csvHeaders.map((header, index) => {
                              const isSorted = mercekSortColumn === header

                              return (
                                <th
                                  key={`header-${index}`}
                                  style={{
                                    padding: '12px 16px',
                                    textAlign: 'left',
                                    borderBottom: 'none',
                                    color: isSorted ? '#10b981' : 'var(--text-primary)',
                                    fontWeight: '600',
                                    fontSize: '13px',
                                    whiteSpace: 'nowrap'
                                  }}
                                >
                                  <span
                                    onClick={() => handleMercekSort(header)}
                                    style={{
                                      cursor: 'pointer',
                                      userSelect: 'none',
                                      display: 'flex',
                                      alignItems: 'center',
                                      gap: '4px',
                                      width: '100%'
                                    }}
                                  >
                                    {header}
                                    {isSorted && (
                                      mercekSortDirection === 'asc' ? <ChevronUp size={12} /> : <ChevronDown size={12} />
                                    )}
                                  </span>
                                </th>
                              )
                            })}
                          </tr>

                          {/* Second Row: Filters */}
                          <tr style={{ background: 'rgba(16, 185, 129, 0.03)', borderBottom: '2px solid rgba(16, 185, 129, 0.12)' }}>
                            {csvHeaders.map((header, index) => {
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

                              const hasActiveFilter = isListbox
                                ? selectedValues.length > 0
                                : isTagSearch
                                  ? tagFilterValues.length > 0
                                  : isDate
                                    ? (dateFilter.from || dateFilter.to)
                                    : textFilterValue.length > 0

                              const filteredOptions = (uniqueColumnValues[header] || []).filter(v =>
                                v.toLowerCase().includes(searchQuery.toLowerCase())
                              )

                              return (
                                <th
                                  key={`filter-${index}`}
                                  data-column-filter
                                  style={{
                                    padding: '0 12px 12px 12px',
                                    textAlign: 'left',
                                    position: 'relative',
                                    verticalAlign: 'top',
                                    fontWeight: 'normal'
                                  }}
                                >
                                  {isListbox ? (
                                    <button
                                      onClick={(e) => {
                                        e.stopPropagation()
                                        setOpenDropdownColumn(isDropdownOpen ? null : header)
                                      }}
                                      style={{
                                        width: '100%',
                                        padding: '6px 8px',
                                        borderRadius: '4px',
                                        border: '1px solid var(--border)',
                                        background: hasActiveFilter ? 'rgba(59, 130, 246, 0.1)' : 'var(--surface)',
                                        color: hasActiveFilter ? '#3b82f6' : 'var(--text-secondary)',
                                        fontSize: '12px',
                                        textAlign: 'left',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        justifyContent: 'space-between',
                                        alignItems: 'center'
                                      }}
                                    >
                                      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                        {hasActiveFilter ? `${selectedValues.length} seçildi` : 'Tümü'}
                                      </span>
                                      <ChevronDown size={12} />
                                    </button>
                                  ) : isTextSearch ? (
                                    <input
                                      type="text"
                                      placeholder="Ara..."
                                      value={textFilterValue}
                                      onChange={(e) => handleMercekTextFilter(header, e.target.value)}
                                      style={{
                                        width: '100%',
                                        padding: '6px 8px',
                                        borderRadius: '4px',
                                        border: '1px solid var(--border)',
                                        background: hasActiveFilter ? 'rgba(59, 130, 246, 0.05)' : 'var(--surface)',
                                        color: 'var(--text-primary)',
                                        fontSize: '12px',
                                        boxSizing: 'border-box'
                                      }}
                                    />
                                  ) : (
                                    <button
                                      onClick={(e) => {
                                        e.stopPropagation()
                                        setOpenDropdownColumn(isDropdownOpen ? null : header)
                                      }}
                                      style={{
                                        width: '100%',
                                        padding: '6px 8px',
                                        borderRadius: '4px',
                                        border: '1px solid var(--border)',
                                        background: hasActiveFilter ? 'rgba(59, 130, 246, 0.1)' : 'var(--surface)',
                                        color: hasActiveFilter ? '#3b82f6' : 'var(--text-secondary)',
                                        fontSize: '12px',
                                        textAlign: 'left',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        justifyContent: 'space-between',
                                        alignItems: 'center'
                                      }}
                                    >
                                      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                        {hasActiveFilter ? 'Filtreli' : 'Tümü'}
                                      </span>
                                      <Filter size={12} />
                                    </button>
                                  )}
                                  {isDropdownOpen && (
                                    <div
                                      style={{
                                        position: 'absolute',
                                        top: 'calc(100% - 8px)',
                                        left: '12px',
                                        width: 'max(100%, 180px)',
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
                                  {String(row[header] || '')}
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
