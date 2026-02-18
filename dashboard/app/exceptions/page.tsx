'use client'

import React, { useState, useEffect, useMemo, useRef, useCallback, Suspense, ChangeEvent, MouseEvent } from 'react'
import dynamic from 'next/dynamic'
import { useSearchParams, useRouter } from 'next/navigation'
import apiClient from '@/lib/axios'
import { format, isWithinInterval, parseISO, startOfDay, endOfDay } from 'date-fns'
import DomainFeaturesManager from '@/components/DomainFeaturesManager'
import Pagination from '@/components/ui/Pagination'

const Plot = dynamic(() => import('react-plotly.js'), { ssr: false })

interface Incident {
  id: number
  timestamp: string
  userEmail?: string
  policy?: string
  action?: string
  severity: string
  destination?: string
  domain?: string
  department?: string
  team?: string
  fullName?: string
  maxMatches?: number
  loginName?: string
  emailAddress?: string
  violationTriggers?: string
  channel?: string
}

interface DomainFeature {
  domain: string
  gizlilikSozlesmesi: string
  egitim: string
  noterlik: string
  denetim: string
  banka: string
  hukukFirmasi: string
  istirak: string
}

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

export default function AnalyticsPage() {
  return (
    <Suspense fallback={<div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', color: 'var(--text-secondary)' }}>Loading...</div>}>
      <AnalyticsPageContent />
    </Suspense>
  )
}

function AnalyticsPageContent() {
  const searchParams = useSearchParams()
  const router = useRouter()
  const [incidents, setIncidents] = useState<Incident[]>([])
  const [loading, setLoading] = useState(true)
  const [currentPage, setCurrentPage] = useState(1)
  const itemsPerPage = 10

  // Heatmap pagination
  const [heatmapTeamPage, setHeatmapTeamPage] = useState(1)
  const teamsPerPage = 8

  // Table sorting
  const [sortColumn, setSortColumn] = useState<string | null>(null)
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')

  // Column filter dropdowns
  const [openColumnFilter, setOpenColumnFilter] = useState<string | null>(null)
  const [columnFilterSearch, setColumnFilterSearch] = useState<Record<string, string>>({})
  const [columnFilters, setColumnFilters] = useState<Record<string, string[]>>({})

  // Filter States (pending - form values, not yet applied)
  const [dateRange, setDateRange] = useState({ start: '', end: '' })
  const [dateError, setDateError] = useState('')
  const [selectedDepartment, setSelectedDepartment] = useState('')
  const [selectedTeam, setSelectedTeam] = useState('')
  const [selectedUser, setSelectedUser] = useState('')
  const [selectedFullName, setSelectedFullName] = useState('')
  const [selectedPolicy, setSelectedPolicy] = useState('')
  const [selectedDomain, setSelectedDomain] = useState('')
  const [selectedAction, setSelectedAction] = useState('')

  // Applied filter snapshot - filteredIncidents uses this
  const [appliedFilters, setAppliedFilters] = useState({
    dateRange: { start: '', end: '' },
    selectedDepartment: '',
    selectedTeam: '',
    selectedUser: '',
    selectedFullName: '',
    selectedPolicy: '',
    selectedDomain: '',
    selectedAction: ''
  })

  const applyFilters = () => {
    setAppliedFilters({
      dateRange: { ...dateRange },
      selectedDepartment,
      selectedTeam,
      selectedUser,
      selectedFullName,
      selectedPolicy,
      selectedDomain,
      selectedAction
    })
  }

  const resetFilters = () => {
    setDateRange({ start: '', end: '' })
    setDateError('')
    setSelectedDepartment('')
    setSelectedTeam('')
    setSelectedUser('')
    setSelectedFullName('')
    setSelectedPolicy('')
    setSelectedDomain('')
    setSelectedAction('')
    setAppliedFilters({
      dateRange: { start: '', end: '' },
      selectedDepartment: '',
      selectedTeam: '',
      selectedUser: '',
      selectedFullName: '',
      selectedPolicy: '',
      selectedDomain: '',
      selectedAction: ''
    })
  }


  // Domain Features States
  const [showDomainFeatures, setShowDomainFeatures] = useState(false)
  const [domainFeatures, setDomainFeatures] = useState<DomainFeature[]>([])
  const [domainFilter, setDomainFilter] = useState('')
  const [gizlilikFilter, setGizlilikFilter] = useState('')
  const [istirakFilter, setIstirakFilter] = useState('')
  const [egitimFilter, setEgitimFilter] = useState('')
  const [noterlikFilter, setNoterlikFilter] = useState('')
  const [hukukFirmasiFilter, setHukukFirmasiFilter] = useState('')
  const [denetimFilter, setDenetimFilter] = useState('')
  const [bankaFilter, setBankaFilter] = useState('')

  // Released Incidents States
  const [releasedIncidents, setReleasedIncidents] = useState<ReleasedIncident[]>([])
  const [loadingReleasedIncidents, setLoadingReleasedIncidents] = useState(false)

  // CSV Analysis States
  const [showCSVAnalysis, setShowCSVAnalysis] = useState(false)
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
  const [mercekColumnFilters, setMercekColumnFilters] = useState<Record<string, string>>({})
  const [userChartPage, setUserChartPage] = useState(1)
  const [assignedUserChartPage, setAssignedUserChartPage] = useState(1)

  // User Incident Analysis States
  const [userSearchQuery, setUserSearchQuery] = useState('')
  const [exceptionDomainFilter, setExceptionDomainFilter] = useState('')
  const [exceptionActionFilter, setExceptionActionFilter] = useState<string[]>([])
  const [exceptionChannelFilter, setExceptionChannelFilter] = useState<string[]>([])
  const [exceptionPolicyFilter, setExceptionPolicyFilter] = useState<string[]>([])
  const [userIncidents, setUserIncidents] = useState<Incident[]>([])
  const [loadingUserIncidents, setLoadingUserIncidents] = useState(false)

  // Multi-select dropdown states
  const [actionDropdownOpen, setActionDropdownOpen] = useState(false)
  const [channelDropdownOpen, setChannelDropdownOpen] = useState(false)
  const [policyDropdownOpen, setPolicyDropdownOpen] = useState(false)

  // Accordion states for exception recommendation
  const [expandedPolicies, setExpandedPolicies] = useState<Set<number>>(new Set())
  const [expandedRules, setExpandedRules] = useState<Set<string>>(new Set())
  const [expandedClassifiers, setExpandedClassifiers] = useState<Set<string>>(new Set())
  const [expandedExceptions, setExpandedExceptions] = useState<Set<string>>(new Set())

  useEffect(() => {
    fetchIncidents()
    fetchReleasedIncidents()
  }, [])

  const fetchReleasedIncidents = async () => {
    setLoadingReleasedIncidents(true)
    try {
      // Tüm released incidents'ları çekmek için pageSize'ı büyük tutuyoruz
      const response = await apiClient.get('/api/released-incidents', {
        params: {
          page: 1,
          pageSize: 10000
        }
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

  // Clear heatmap page when page changes
  useEffect(() => {
  }, [heatmapTeamPage])

  // Clear page when page changes
  useEffect(() => {
  }, [currentPage])

  // Get unique values for each column for filtering
  const getUniqueColumnValues = (column: string): string[] => {
    const values = new Set<string>()
    incidents.forEach(incident => {
      let value: string | undefined
      switch (column) {
        case 'user':
          value = incident.userEmail || incident.loginName || incident.emailAddress
          break
        case 'fullName':
          value = incident.fullName
          break
        case 'department':
          value = incident.department
          break
        case 'team':
          value = normalizeTeamName(incident.team)
          break
        case 'policy':
          value = incident.policy
          break
        case 'domain':
          value = incident.domain
          break
        case 'action':
          value = incident.action
          break
        default:
          return
      }
      if (value) values.add(value)
    })
    return Array.from(values).sort()
  }

  const handleSort = (column: string) => {
    if (sortColumn === column) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setSortColumn(column)
      setSortDirection('asc')
    }
  }

  const handleMercekSort = (column: string) => {
    if (mercekSortColumn === column) {
      setMercekSortDirection(mercekSortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setMercekSortColumn(column)
      setMercekSortDirection('asc')
    }
  }

  const handleMercekColumnFilter = (column: string, value: string) => {
    setMercekColumnFilters(prev => ({
      ...prev,
      [column]: value
    }))
  }

  const getFilteredAndSortedMercekData = (data: any[]) => {
    let filtered = [...data]

    // Apply column filters
    Object.entries(mercekColumnFilters).forEach(([column, filterValue]) => {
      if (filterValue.trim()) {
        filtered = filtered.filter(row => {
          const cellValue = String(row[column] || '').toLowerCase()
          return cellValue.includes(filterValue.toLowerCase().trim())
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

  const toggleColumnFilter = (column: string) => {
    if (openColumnFilter === column) {
      setOpenColumnFilter(null)
      setColumnFilterSearch(prev => ({ ...prev, [column]: '' }))
    } else {
      setOpenColumnFilter(column)
      setColumnFilterSearch(prev => ({ ...prev, [column]: prev[column] || '' }))
    }
  }

  const toggleColumnFilterValue = (column: string, value: string) => {
    setColumnFilters(prev => {
      const current = prev[column] || []
      if (current.includes(value)) {
        return { ...prev, [column]: current.filter(v => v !== value) }
      } else {
        return { ...prev, [column]: [...current, value] }
      }
    })
  }

  // Close dropdowns when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: Event) => {
      const target = event.target as HTMLElement
      if (!target.closest('[data-dropdown]')) {
        setActionDropdownOpen(false)
        setChannelDropdownOpen(false)
        setPolicyDropdownOpen(false)
      }
      if (!target.closest('[data-column-filter]')) {
        setOpenColumnFilter(null)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [])

  const fetchIncidents = async () => {
    setLoading(true)
    try {
      const response = await apiClient.get('/api/incidents', {
        params: {
          limit: 1000000000, // Fetch significantly more records to approximate "all" for analytics
          order_by: 'timestamp_desc'
        }
      })

      const data = Array.isArray(response.data) ? response.data : []

      const mappedIncidents: Incident[] = data.map((item: any) => {
        const dest = item.destination || ''

        // Birden fazla email adresi varsa (noktalı virgülle ayrılmış), her birinden domain çıkar
        let domain = 'Unknown'
        if (dest.includes(';')) {
          // Noktalı virgülle ayrılmış email adresleri
          const emails = dest.split(';').map((e: string) => e.trim()).filter((e: string) => e)
          const domains: string[] = []
          emails.forEach((email: string) => {
            if (email.includes('@')) {
              const parts = email.split('@')
              if (parts.length > 1) {
                const extractedDomain = parts[1].trim()
                if (extractedDomain && !domains.includes(extractedDomain)) {
                  domains.push(extractedDomain)
                }
              }
            }
          })
          domain = domains.length > 0 ? domains.join(', ') : 'Unknown'
        } else if (dest.includes('@')) {
          // Tek email adresi
          const parts = dest.split('@')
          domain = parts.length > 1 ? parts[1].trim() : dest
        } else {
          domain = dest
        }

        return {
          id: item.id,
          timestamp: item.timestamp,
          userEmail: item.userEmail,
          policy: item.policy,
          action: item.action || 'Permit',
          severity: item.severity >= 4 ? 'High' : item.severity >= 3 ? 'Medium' : 'Low',
          destination: dest,
          domain: domain || 'Unknown',
          department: item.department || item.user_department,
          team: item.team,
          fullName: item.fullName,
          maxMatches: item.maxMatches || 0,
          loginName: item.loginName,
          emailAddress: item.emailAddress,
          violationTriggers: item.violationTriggers || item.violation_triggers || item.ViolationTriggers || undefined,
          channel: item.channel
        }
      })

      setIncidents(mappedIncidents)
    } catch (error) {
      console.error('Error fetching incidents:', error)
    } finally {
      setLoading(false)
    }
  }

  // Helper function to normalize team names - if ends with "Şubesi", normalize to "Şube"
  // If team is "Unknown", return "Hesap Araştırmaları"
  const normalizeTeamName = (team: string | undefined | null): string => {
    if (!team) return 'Hesap Araştırmaları'
    const trimmed = team.trim()
    if (trimmed === 'Unknown' || trimmed === '') return 'Hesap Araştırmaları'
    return trimmed.endsWith('Şubesi') ? 'Şube' : trimmed
  }



  // Filter Logic with column filters and sorting
  const filteredIncidents = useMemo(() => {
    let filtered = incidents.filter(incident => {
      // Date Filter
      if (appliedFilters.dateRange.start && appliedFilters.dateRange.end) {
        try {
          const incidentDate = parseISO(incident.timestamp)
          const start = startOfDay(parseISO(appliedFilters.dateRange.start))
          const end = endOfDay(parseISO(appliedFilters.dateRange.end))

          if (isNaN(start.getTime()) || isNaN(end.getTime())) {
            return true
          }
          if (!isWithinInterval(incidentDate, { start, end })) return false
        } catch (e) {
          console.warn("Invalid date encountered during filtering", e);
          return true;
        }
      }

      // Department Filter
      if (appliedFilters.selectedDepartment && incident.department !== appliedFilters.selectedDepartment) return false

      // Team Filter - normalize team names for comparison
      if (appliedFilters.selectedTeam) {
        const normalizedIncidentTeam = normalizeTeamName(incident.team)
        const normalizedSelectedTeam = normalizeTeamName(appliedFilters.selectedTeam)
        if (normalizedIncidentTeam !== normalizedSelectedTeam) return false
      }

      // User Filter (Broad search)
      if (appliedFilters.selectedUser) {
        const search = appliedFilters.selectedUser.toLowerCase()
        const match = incident.userEmail?.toLowerCase().includes(search) ||
          incident.loginName?.toLowerCase().includes(search) ||
          incident.emailAddress?.toLowerCase().includes(search)
        if (!match) return false
      }

      // Full Name Filter
      if (appliedFilters.selectedFullName && !incident.fullName?.toLowerCase().includes(appliedFilters.selectedFullName.toLowerCase())) return false

      // Policy Filter (Text search)
      if (appliedFilters.selectedPolicy && !incident.policy?.toLowerCase().includes(appliedFilters.selectedPolicy.toLowerCase())) return false

      // Domain Filter (Text search)
      if (appliedFilters.selectedDomain && !incident.domain?.toLowerCase().includes(appliedFilters.selectedDomain.toLowerCase())) return false

      // Action Filter
      if (appliedFilters.selectedAction && incident.action !== appliedFilters.selectedAction) return false

      // Column filters
      if (columnFilters.user && columnFilters.user.length > 0) {
        const userMatch = columnFilters.user.some(filter =>
          incident.userEmail?.toLowerCase().includes(filter.toLowerCase()) ||
          incident.loginName?.toLowerCase().includes(filter.toLowerCase()) ||
          incident.emailAddress?.toLowerCase().includes(filter.toLowerCase())
        )
        if (!userMatch) return false
      }
      if (columnFilters.fullName && columnFilters.fullName.length > 0) {
        if (!columnFilters.fullName.some(filter => incident.fullName?.toLowerCase().includes(filter.toLowerCase()))) return false
      }
      if (columnFilters.department && columnFilters.department.length > 0) {
        if (!columnFilters.department.includes(incident.department || '')) return false
      }
      if (columnFilters.team && columnFilters.team.length > 0) {
        const normalizedIncidentTeam = normalizeTeamName(incident.team)
        if (!columnFilters.team.some(filter => normalizeTeamName(filter) === normalizedIncidentTeam)) return false
      }
      if (columnFilters.policy && columnFilters.policy.length > 0) {
        if (!columnFilters.policy.some(filter => incident.policy?.toLowerCase().includes(filter.toLowerCase()))) return false
      }
      if (columnFilters.domain && columnFilters.domain.length > 0) {
        if (!columnFilters.domain.some(filter => incident.domain?.toLowerCase().includes(filter.toLowerCase()))) return false
      }
      if (columnFilters.action && columnFilters.action.length > 0) {
        if (!columnFilters.action.includes(incident.action || '')) return false
      }

      return true
    })

    // Apply sorting
    if (sortColumn) {
      filtered = [...filtered].sort((a, b) => {
        let aVal: any
        let bVal: any

        switch (sortColumn) {
          case 'time':
            aVal = new Date(a.timestamp).getTime()
            bVal = new Date(b.timestamp).getTime()
            break
          case 'user':
            aVal = (a.userEmail || '').toLowerCase()
            bVal = (b.userEmail || '').toLowerCase()
            break
          case 'fullName':
            aVal = (a.fullName || '').toLowerCase()
            bVal = (b.fullName || '').toLowerCase()
            break
          case 'department':
            aVal = (a.department || '').toLowerCase()
            bVal = (b.department || '').toLowerCase()
            break
          case 'team':
            aVal = normalizeTeamName(a.team).toLowerCase()
            bVal = normalizeTeamName(b.team).toLowerCase()
            break
          case 'policy':
            aVal = (a.policy || '').toLowerCase()
            bVal = (b.policy || '').toLowerCase()
            break
          case 'domain':
            aVal = (a.domain || '').toLowerCase()
            bVal = (b.domain || '').toLowerCase()
            break
          case 'max':
            aVal = a.maxMatches || 0
            bVal = b.maxMatches || 0
            break
          case 'action':
            aVal = (a.action || '').toLowerCase()
            bVal = (b.action || '').toLowerCase()
            break
          default:
            return 0
        }

        if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1
        if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1
        return 0
      })
    }

    return filtered
  }, [incidents, appliedFilters, columnFilters, sortColumn, sortDirection])

  // Mercek API Functions
  const fetchMercekData = async (page: number = 1, customPageSize?: number) => {
    setMercekLoading(true)
    setMercekError(null)
    
    try {
      const params: any = {
        page: page,
        pageSize: customPageSize || csvPageSize
      }
      
      if (csvSelectedUser) {
        params.userName = csvSelectedUser
      }
      if (mercekAssignedUserFilter) {
        params.assignedUserCode = mercekAssignedUserFilter
      }
      if (csvDateFrom) {
        params.startDate = csvDateFrom
      }
      if (csvDateTo) {
        params.endDate = csvDateTo
      }
      if (csvSearchQuery) {
        params.searchTerm = csvSearchQuery
      }
      
      const response = await apiClient.get('/api/mercek', { params })
      const data = response.data
      
      // Map API response to table format with Turkish column names
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
      
      // Set headers from first item if available
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

  // Auto-load Mercek data when CSV Analysis is opened and refresh every 30 seconds
  // Use refs to avoid stale closures - so interval always uses latest filter values
  const fetchMercekDataRef = useRef(fetchMercekData)
  const fetchMercekStatisticsRef = useRef(fetchMercekStatistics)
  const csvCurrentPageRef = useRef(csvCurrentPage)
  const csvPageSizeRef = useRef(csvPageSize)

  useEffect(() => { fetchMercekDataRef.current = fetchMercekData }, [fetchMercekData])
  useEffect(() => { fetchMercekStatisticsRef.current = fetchMercekStatistics }, [fetchMercekStatistics])
  useEffect(() => { csvCurrentPageRef.current = csvCurrentPage }, [csvCurrentPage])
  useEffect(() => { csvPageSizeRef.current = csvPageSize }, [csvPageSize])

  useEffect(() => {
    if (showCSVAnalysis) {
      // Initial load
      fetchMercekFilters()
      fetchMercekData(1, csvPageSize)
      fetchMercekStatistics()

      // Auto-refresh every 30 seconds - uses refs to always get latest filter values
      const intervalId = setInterval(() => {
        fetchMercekDataRef.current(csvCurrentPageRef.current, csvPageSizeRef.current)
        fetchMercekStatisticsRef.current()
      }, 30000) // 30 seconds

      // Cleanup interval on unmount or when CSV Analysis is closed
      return () => clearInterval(intervalId)
    }
  }, [showCSVAnalysis])

  // Sync URL search params with view state
  useEffect(() => {
    const view = searchParams.get('view')
    if (view === 'domain-features') {
      setShowDomainFeatures(true)
      setShowCSVAnalysis(false)
    } else if (view === 'mercek-analiz') {
      setShowCSVAnalysis(true)
      setShowDomainFeatures(false)
    } else {
      setShowDomainFeatures(false)
      setShowCSVAnalysis(false)
    }
  }, [searchParams])

  // Helper to update URL when buttons are clicked
  const navigateToView = (view: string | null) => {
    if (view) {
      router.push(`/exceptions?view=${view}`)
    } else {
      router.push('/exceptions')
    }
  }

  // Reset page when filters change
  useEffect(() => {
    setCurrentPage(1)
  }, [filteredIncidents])

  // Pagination Logic
  const paginatedIncidents = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage
    return filteredIncidents.slice(startIndex, startIndex + itemsPerPage)
  }, [filteredIncidents, currentPage])

  const totalPages = Math.ceil(filteredIncidents.length / itemsPerPage)

  // Get Unique Values for Selects
  const uniqueDepartments = useMemo(() => Array.from(new Set(incidents.map(i => i.department).filter(Boolean))).sort(), [incidents])
  const uniqueTeams = useMemo(() => {
    const normalizedTeams = new Set<string>()
    incidents.forEach(i => {
      if (i.team) {
        normalizedTeams.add(normalizeTeamName(i.team))
      }
    })
    return Array.from(normalizedTeams).sort()
  }, [incidents])
  const uniqueActions = useMemo(() => Array.from(new Set(incidents.map(i => i.action || 'Permit'))).sort(), [incidents])
  const uniqueChannels = useMemo(() => Array.from(new Set(incidents.map(i => i.channel).filter((c): c is string => Boolean(c)))).sort(), [incidents])

  // Get unique policies using the same logic as exception recommendation report
  // Extract policies from violationTriggers first, then fallback to incident.policy
  const uniquePolicies = useMemo(() => {
    const policySet = new Set<string>()

    incidents.forEach(incident => {
      let triggers: any[] = []
      if (incident.violationTriggers) {
        try {
          triggers = typeof incident.violationTriggers === 'string'
            ? JSON.parse(incident.violationTriggers)
            : incident.violationTriggers
        } catch {
          triggers = []
        }
      }

      // If triggers exist, extract policy names from them
      if (triggers.length > 0) {
        triggers.forEach((t: any) => {
          const policyName = t.PolicyName || t.policy_name || incident.policy
          if (policyName) {
            policySet.add(policyName)
          }
        })
      } else {
        // If no triggers, use incident.policy as fallback
        if (incident.policy) {
          policySet.add(incident.policy)
        }
      }
    })

    return Array.from(policySet).sort()
  }, [incidents])

  // Heatmap Data Calculation (using filtered incidents - ALL matching records, not just current page)
  const heatmapData = useMemo(() => {
    const teams = new Set<string>()
    const domains = new Set<string>()
    const counts: Record<string, Record<string, number>> = {}
    const breakdown: Record<string, Record<string, { block: number, permit: number, authorized: number, quarantine: number, maxMatchTotal: number, incidentCount: number }>> = {}
    const domainTotalCounts: Record<string, number> = {}
    const teamTotalCounts: Record<string, number> = {}

    filteredIncidents.forEach(incident => {
      // Use team, fallback to department if team is missing
      const rawTeam = incident.team || incident.department || 'Unknown'
      const team = normalizeTeamName(rawTeam) || 'Hesap Araştırmaları'
      const domain = incident.domain || 'Unknown'
      const action = incident.action?.toLowerCase() || 'permit'

      teams.add(team)
      domains.add(domain)

      if (!counts[team]) counts[team] = {}
      counts[team][domain] = (counts[team][domain] || 0) + 1

      if (!breakdown[team]) breakdown[team] = {}
      if (!breakdown[team][domain]) breakdown[team][domain] = { block: 0, permit: 0, authorized: 0, quarantine: 0, maxMatchTotal: 0, incidentCount: 0 }

      // Add maxMatches to total for average calculation
      breakdown[team][domain].maxMatchTotal += (incident.maxMatches || 0)
      breakdown[team][domain].incidentCount++

      // Action matching with includes for variations like BLOCKED, Block, blocked
      if (action.includes('block')) breakdown[team][domain].block++
      else if (action.includes('permit') || action.includes('released')) breakdown[team][domain].permit++
      else if (action.includes('authorized') || action.includes('allow')) breakdown[team][domain].authorized++
      else if (action.includes('quarantine')) breakdown[team][domain].quarantine++

      domainTotalCounts[domain] = (domainTotalCounts[domain] || 0) + 1
      teamTotalCounts[team] = (teamTotalCounts[team] || 0) + 1
    })

    // Sort teams by total count descending
    const sortedTeams = Array.from(teams).sort((a, b) => (teamTotalCounts[b] || 0) - (teamTotalCounts[a] || 0))
    // Sort domains by total count descending - top 10 + "Diğer" (top 10 dışındaki tüm domainler)
    const allSortedDomains = Array.from(domains).sort((a, b) => (domainTotalCounts[b] || 0) - (domainTotalCounts[a] || 0))
    const topDomains = allSortedDomains.slice(0, 10)
    const otherDomains = allSortedDomains.slice(10)
    const sortedDomains = otherDomains.length > 0 ? [...topDomains, 'Diğer'] : topDomains
    if (otherDomains.length > 0) {
      sortedTeams.forEach(t => {
        let otherCount = 0
        const otherBd = { block: 0, permit: 0, authorized: 0, quarantine: 0, maxMatchTotal: 0, incidentCount: 0 }
        otherDomains.forEach(d => {
          otherCount += counts[t]?.[d] || 0
          const bd = breakdown[t]?.[d]
          if (bd) {
            otherBd.block += bd.block
            otherBd.permit += bd.permit
            otherBd.authorized += bd.authorized
            otherBd.quarantine += bd.quarantine
            otherBd.maxMatchTotal += bd.maxMatchTotal
            otherBd.incidentCount += bd.incidentCount
          }
        })
        if (!counts[t]) counts[t] = {}
        counts[t]['Diğer'] = otherCount
        if (!breakdown[t]) breakdown[t] = {}
        breakdown[t]['Diğer'] = otherBd
      })
    }
    let maxCount = 0
    sortedTeams.forEach(t => {
      sortedDomains.forEach(d => {
        if (counts[t]?.[d] > maxCount) maxCount = counts[t][d]
      })
    })

    return { teams: sortedTeams, domains: sortedDomains, counts, breakdown, maxCount }
  }, [filteredIncidents])


  const getHeatmapColor = (count: number, max: number) => {
    if (count === 0) return 'transparent'
    const intensity = max > 0 ? count / max : 0
    // Daha koyu renkler: 85'ten 25'e kadar (açık maviden koyu maviye)
    const lightness = 85 - (intensity * 60)
    return `hsl(210, 90%, ${lightness}%)`
  }

  const getTextColor = (count: number, max: number) => {
    if (count === 0) return 'var(--text-primary)'
    const intensity = max > 0 ? count / max : 0
    // Açık arka planlarda (intensity < 0.4) siyah, koyu arka planlarda beyaz
    return intensity > 0.4 ? '#ffffff' : '#1e293b'
  }

  const getYesNoColor = (value: string) => {
    const normalized = value?.toLowerCase().trim() || ''
    const isYes = normalized === 'evet' || normalized === 'yes' || normalized === '1' || normalized === 'var'
    return {
      background: isYes ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
      color: isYes ? '#10b981' : '#ef4444'
    }
  }

  // Manual fetch function - called when Recommend button is clicked
  // Allow recommendation if user search query OR any filter is selected (policy, channel, action, domain)
  const handleRecommend = () => {
    const hasUserQuery = userSearchQuery.trim()
    const hasFilters = exceptionActionFilter.length > 0 ||
      exceptionChannelFilter.length > 0 ||
      exceptionPolicyFilter.length > 0 ||
      exceptionDomainFilter.trim()

    if (hasUserQuery || hasFilters) {
      fetchUserIncidents()
    } else {
      setUserIncidents([])
    }
  }

  // Clear all filters function
  const handleClearFilters = () => {
    setUserSearchQuery('')
    setExceptionDomainFilter('')
    setExceptionActionFilter([])
    setExceptionChannelFilter([])
    setExceptionPolicyFilter([])
    setUserIncidents([])
    setExpandedPolicies(new Set())
    setExpandedRules(new Set())
    setExpandedClassifiers(new Set())
    setExpandedExceptions(new Set())
  }

  // Accordion toggle functions
  const togglePolicy = (pIdx: number) => {
    setExpandedPolicies(prev => {
      const newSet = new Set(prev)
      if (newSet.has(pIdx)) {
        newSet.delete(pIdx)
      } else {
        newSet.add(pIdx)
      }
      return newSet
    })
  }

  const toggleRule = (pIdx: number, rIdx: number) => {
    const key = `${pIdx}-${rIdx}`
    setExpandedRules(prev => {
      const newSet = new Set(prev)
      if (newSet.has(key)) {
        newSet.delete(key)
      } else {
        newSet.add(key)
      }
      return newSet
    })
  }

  const toggleClassifier = (pIdx: number, rIdx: number, cIdx: number) => {
    const key = `${pIdx}-${rIdx}-${cIdx}`
    setExpandedClassifiers(prev => {
      const newSet = new Set(prev)
      if (newSet.has(key)) {
        newSet.delete(key)
      } else {
        newSet.add(key)
      }
      return newSet
    })
  }

  const toggleException = (pIdx: number, rIdx: number, eIdx: number) => {
    const key = `${pIdx}-${rIdx}-${eIdx}`
    setExpandedExceptions(prev => {
      const newSet = new Set(prev)
      if (newSet.has(key)) {
        newSet.delete(key)
      } else {
        newSet.add(key)
      }
      return newSet
    })
  }

  const fetchUserIncidents = () => {
    setLoadingUserIncidents(true)
    const query = userSearchQuery.toLowerCase().trim()
    const domainQuery = exceptionDomainFilter.toLowerCase().trim()

    const filtered = incidents.filter(incident => {
      // Date filter
      if (appliedFilters.dateRange.start && appliedFilters.dateRange.end) {
        const incidentDate = parseISO(incident.timestamp)
        const start = startOfDay(parseISO(appliedFilters.dateRange.start))
        const end = endOfDay(parseISO(appliedFilters.dateRange.end))
        if (!isWithinInterval(incidentDate, { start, end })) return false
      }

      // User match (userEmail, loginName, fullName) - only if user query is provided
      if (query) {
        const userMatch = (
          (incident.userEmail && incident.userEmail.toLowerCase().includes(query)) ||
          (incident.loginName && incident.loginName.toLowerCase().includes(query)) ||
          (incident.fullName && incident.fullName.toLowerCase().includes(query))
        )
        if (!userMatch) return false
      }

      // Domain filter
      if (domainQuery && incident.domain && !incident.domain.toLowerCase().includes(domainQuery)) {
        return false
      }

      // Action filter (multiple selection)
      if (exceptionActionFilter.length > 0 && incident.action && !exceptionActionFilter.includes(incident.action)) {
        return false
      }

      // Channel filter (multiple selection)
      if (exceptionChannelFilter.length > 0 && incident.channel && !exceptionChannelFilter.includes(incident.channel)) {
        return false
      }

      // Policy filter (multiple selection) - use same logic as exception recommendation report
      if (exceptionPolicyFilter.length > 0) {
        let triggers: any[] = []
        if (incident.violationTriggers) {
          try {
            triggers = typeof incident.violationTriggers === 'string'
              ? JSON.parse(incident.violationTriggers)
              : incident.violationTriggers
          } catch {
            triggers = []
          }
        }

        let policyMatch = false
        if (triggers.length > 0) {
          // Check policies from violationTriggers
          triggers.forEach((t: any) => {
            const policyName = t.PolicyName || t.policy_name || incident.policy
            if (policyName && exceptionPolicyFilter.includes(policyName)) {
              policyMatch = true
            }
          })
        } else {
          // Fallback to incident.policy
          if (incident.policy && exceptionPolicyFilter.includes(incident.policy)) {
            policyMatch = true
          }
        }

        if (!policyMatch) {
          return false
        }
      }

      return true
    })

    setUserIncidents(filtered)
    setLoadingUserIncidents(false)
  }

  // Helper function to calculate percentile
  const calculatePercentile = (arr: number[], percentile: number): number => {
    if (arr.length === 0) return 0
    const sorted = [...arr].sort((a, b) => a - b)
    const index = Math.ceil((percentile / 100) * sorted.length) - 1
    return sorted[Math.max(0, index)]
  }

  // Calculate report data: Policy > Rule > Classifier/Exception with incident counts, average matches, and percentiles
  const userReportData = useMemo(() => {
    if (!userIncidents.length) return []

    // Structure: Policy > Rule > Classifier/Exception
    // Rule structure: { classifiers: Map, exceptions: Map }
    const policyMap = new Map<string, Map<string, {
      classifiers: Map<string, { incidentIds: Set<number>, matches: number[] }>,
      exceptions: Map<string, Map<string, { incidentIds: Set<number>, matches: number[] }>>
    }>>()

    userIncidents.forEach(incident => {
      let triggers: any[] = []
      let exceptionTriggers: any[] = []

      if (incident.violationTriggers) {
        try {
          const allTriggers = typeof incident.violationTriggers === 'string'
            ? JSON.parse(incident.violationTriggers)
            : incident.violationTriggers

          // Separate regular triggers from exception triggers
          allTriggers.forEach((t: any) => {
            const parentRuleName = t.parent_rule_name || t.ParentRuleName
            if (parentRuleName) {
              // This is an exception trigger
              exceptionTriggers.push(t)
            } else {
              // This is a regular rule trigger
              triggers.push(t)
            }
          })
        } catch {
          triggers = []
          exceptionTriggers = []
        }
      }

      // If no triggers, use policy as fallback
      if (triggers.length === 0 && exceptionTriggers.length === 0 && incident.policy) {
        const policyName = incident.policy
        if (!policyMap.has(policyName)) {
          policyMap.set(policyName, new Map())
        }
        const ruleMap = policyMap.get(policyName)!
        const ruleName = 'Unknown Rule'
        if (!ruleMap.has(ruleName)) {
          ruleMap.set(ruleName, { classifiers: new Map(), exceptions: new Map() })
        }
        const ruleData = ruleMap.get(ruleName)!
        const classifierName = 'No Classifier'
        if (!ruleData.classifiers.has(classifierName)) {
          ruleData.classifiers.set(classifierName, { incidentIds: new Set(), matches: [] })
        }
        const data = ruleData.classifiers.get(classifierName)!
        data.incidentIds.add(incident.id)
        data.matches.push(incident.maxMatches || 0)
      } else {
        // Process regular rule triggers
        triggers.forEach((t: any) => {
          const policyName = t.PolicyName || t.policy_name || incident.policy || 'Unknown Policy'
          const ruleName = t.RuleName || t.rule_name || 'Unknown Rule'

          if (!policyMap.has(policyName)) {
            policyMap.set(policyName, new Map())
          }
          const ruleMap = policyMap.get(policyName)!

          if (!ruleMap.has(ruleName)) {
            ruleMap.set(ruleName, { classifiers: new Map(), exceptions: new Map() })
          }
          const ruleData = ruleMap.get(ruleName)!

          // Process classifiers
          const classifiers = t.Classifiers || t.classifiers || []
          if (classifiers.length > 0) {
            classifiers.forEach((c: any) => {
              const classifierName = c.ClassifierName || c.classifier_name || 'Unknown Classifier'
              const matches = c.NumberMatches || c.number_matches || 0

              if (!ruleData.classifiers.has(classifierName)) {
                ruleData.classifiers.set(classifierName, { incidentIds: new Set(), matches: [] })
              }
              const data = ruleData.classifiers.get(classifierName)!
              data.incidentIds.add(incident.id)
              data.matches.push(matches)
            })
          } else {
            // No classifiers, use rule level
            const classifierName = 'No Classifier'
            if (!ruleData.classifiers.has(classifierName)) {
              ruleData.classifiers.set(classifierName, { incidentIds: new Set(), matches: [] })
            }
            const data = ruleData.classifiers.get(classifierName)!
            data.incidentIds.add(incident.id)
            data.matches.push(incident.maxMatches || 0)
          }
        })

        // Process exception triggers
        exceptionTriggers.forEach((t: any) => {
          const policyName = t.PolicyName || t.policy_name || incident.policy || 'Unknown Policy'
          const exceptionName = t.RuleName || t.rule_name || 'Unknown Exception'
          const parentRuleName = t.parent_rule_name || t.ParentRuleName

          if (!policyName || !exceptionName) return

          if (!policyMap.has(policyName)) {
            policyMap.set(policyName, new Map())
          }
          const ruleMap = policyMap.get(policyName)!

          // If parent rule exists, attach exception under it
          if (parentRuleName && ruleMap.has(parentRuleName)) {
            const ruleData = ruleMap.get(parentRuleName)!

            if (!ruleData.exceptions.has(exceptionName)) {
              ruleData.exceptions.set(exceptionName, new Map())
            }
            const exceptionClassifierMap = ruleData.exceptions.get(exceptionName)!

            // Process exception classifiers
            const classifiers = t.Classifiers || t.classifiers || []
            if (classifiers.length > 0) {
              classifiers.forEach((c: any) => {
                const classifierName = c.ClassifierName || c.classifier_name || 'Unknown Classifier'
                const matches = c.NumberMatches || c.number_matches || 0

                if (!exceptionClassifierMap.has(classifierName)) {
                  exceptionClassifierMap.set(classifierName, { incidentIds: new Set(), matches: [] })
                }
                const data = exceptionClassifierMap.get(classifierName)!
                data.incidentIds.add(incident.id)
                data.matches.push(matches)
              })
            } else {
              // No classifiers for exception, use exception level
              const classifierName = 'No Classifier'
              if (!exceptionClassifierMap.has(classifierName)) {
                exceptionClassifierMap.set(classifierName, { incidentIds: new Set(), matches: [] })
              }
              const data = exceptionClassifierMap.get(classifierName)!
              data.incidentIds.add(incident.id)
              data.matches.push(incident.maxMatches || 0)
            }
          } else if (parentRuleName) {
            // Parent rule not in triggers — create placeholder entry for it
            ruleMap.set(parentRuleName, { classifiers: new Map(), exceptions: new Map() })
            const ruleData = ruleMap.get(parentRuleName)!

            if (!ruleData.exceptions.has(exceptionName)) {
              ruleData.exceptions.set(exceptionName, new Map())
            }
            const exceptionClassifierMap = ruleData.exceptions.get(exceptionName)!

            const classifiers = t.Classifiers || t.classifiers || []
            if (classifiers.length > 0) {
              classifiers.forEach((c: any) => {
                const classifierName = c.ClassifierName || c.classifier_name || 'Unknown Classifier'
                const matches = c.NumberMatches || c.number_matches || 0

                if (!exceptionClassifierMap.has(classifierName)) {
                  exceptionClassifierMap.set(classifierName, { incidentIds: new Set(), matches: [] })
                }
                const data = exceptionClassifierMap.get(classifierName)!
                data.incidentIds.add(incident.id)
                data.matches.push(matches)
              })
            } else {
              const classifierName = 'No Classifier'
              if (!exceptionClassifierMap.has(classifierName)) {
                exceptionClassifierMap.set(classifierName, { incidentIds: new Set(), matches: [] })
              }
              const data = exceptionClassifierMap.get(classifierName)!
              data.incidentIds.add(incident.id)
              data.matches.push(incident.maxMatches || 0)
            }
          } else {
            // No parent rule — show exception as standalone under policy
            const ruleName = exceptionName
            if (!ruleMap.has(ruleName)) {
              ruleMap.set(ruleName, { classifiers: new Map(), exceptions: new Map() })
            }
            const ruleData = ruleMap.get(ruleName)!

            const classifiers = t.Classifiers || t.classifiers || []
            if (classifiers.length > 0) {
              classifiers.forEach((c: any) => {
                const classifierName = c.ClassifierName || c.classifier_name || 'Unknown Classifier'
                const matches = c.NumberMatches || c.number_matches || 0

                if (!ruleData.classifiers.has(classifierName)) {
                  ruleData.classifiers.set(classifierName, { incidentIds: new Set(), matches: [] })
                }
                const data = ruleData.classifiers.get(classifierName)!
                data.incidentIds.add(incident.id)
                data.matches.push(matches)
              })
            } else {
              const classifierName = 'No Classifier'
              if (!ruleData.classifiers.has(classifierName)) {
                ruleData.classifiers.set(classifierName, { incidentIds: new Set(), matches: [] })
              }
              const data = ruleData.classifiers.get(classifierName)!
              data.incidentIds.add(incident.id)
              data.matches.push(incident.maxMatches || 0)
            }
          }
        })
      }
    })

    // Convert to array structure for rendering
    return Array.from(policyMap.entries()).map(([policyName, ruleMap]) => {
      const rules = Array.from(ruleMap.entries()).map(([ruleName, ruleData]) => {
        // Process classifiers
        const classifiers = Array.from(ruleData.classifiers.entries()).map(([classifierName, data]) => {
          const sortedMatches = [...data.matches].sort((a, b) => a - b)
          const avgMatches = data.matches.length > 0 ? data.matches.reduce((a, b) => a + b, 0) / data.matches.length : 0
          const p25 = calculatePercentile(data.matches, 25)
          const p70 = calculatePercentile(data.matches, 70)
          const p75 = calculatePercentile(data.matches, 75)
          const p90 = calculatePercentile(data.matches, 90)

          // Calculate recommendations
          // Medium (Audit): P70 or P25 üstü
          // High (Block): P90 or P75 üstü
          const mediumThreshold = Math.max(p70, p25)
          const highThreshold = Math.max(p90, p75)

          return {
            name: classifierName,
            incidentCount: data.incidentIds.size,
            avgMatches,
            p25,
            p70,
            p75,
            p90,
            recommendations: {
              medium: {
                threshold: mediumThreshold,
                label: 'Medium (Audit)',
                action: 'Audit'
              },
              high: {
                threshold: highThreshold,
                label: 'High (Block)',
                action: 'Block'
              }
            }
          }
        })

        // Process exceptions
        const exceptions = Array.from(ruleData.exceptions.entries()).map(([exceptionName, exceptionClassifierMap]) => {
          const exceptionClassifiers = Array.from(exceptionClassifierMap.entries()).map(([classifierName, data]) => {
            const sortedMatches = [...data.matches].sort((a, b) => a - b)
            const avgMatches = data.matches.length > 0 ? data.matches.reduce((a, b) => a + b, 0) / data.matches.length : 0
            const p25 = calculatePercentile(data.matches, 25)
            const p70 = calculatePercentile(data.matches, 70)
            const p75 = calculatePercentile(data.matches, 75)
            const p90 = calculatePercentile(data.matches, 90)

            const mediumThreshold = Math.max(p70, p25)
            const highThreshold = Math.max(p90, p75)

            return {
              name: classifierName,
              incidentCount: data.incidentIds.size,
              avgMatches,
              p25,
              p70,
              p75,
              p90,
              recommendations: {
                medium: {
                  threshold: mediumThreshold,
                  label: 'Medium (Audit)',
                  action: 'Audit'
                },
                high: {
                  threshold: highThreshold,
                  label: 'High (Block)',
                  action: 'Block'
                }
              }
            }
          })

          // Calculate exception-level stats
          const allExceptionIncidentIds = new Set<number>()
          const allExceptionMatches: number[] = []
          exceptionClassifierMap.forEach((data) => {
            data.incidentIds.forEach(id => allExceptionIncidentIds.add(id))
            allExceptionMatches.push(...data.matches)
          })

          const exceptionAvgMatches = allExceptionMatches.length > 0 ? allExceptionMatches.reduce((a, b) => a + b, 0) / allExceptionMatches.length : 0
          const exceptionP25 = calculatePercentile(allExceptionMatches, 25)
          const exceptionP75 = calculatePercentile(allExceptionMatches, 75)
          const exceptionP90 = calculatePercentile(allExceptionMatches, 90)

          return {
            name: exceptionName,
            incidentCount: allExceptionIncidentIds.size,
            avgMatches: exceptionAvgMatches,
            p25: exceptionP25,
            p75: exceptionP75,
            p90: exceptionP90,
            classifiers: exceptionClassifiers
          }
        })

        // Calculate rule-level stats (including both classifiers and exceptions)
        const allRuleIncidentIds = new Set<number>()
        const allRuleMatches: number[] = []

        ruleData.classifiers.forEach((data) => {
          data.incidentIds.forEach(id => allRuleIncidentIds.add(id))
          allRuleMatches.push(...data.matches)
        })

        ruleData.exceptions.forEach((exceptionClassifierMap) => {
          exceptionClassifierMap.forEach((data) => {
            data.incidentIds.forEach(id => allRuleIncidentIds.add(id))
            allRuleMatches.push(...data.matches)
          })
        })

        const ruleAvgMatches = allRuleMatches.length > 0 ? allRuleMatches.reduce((a, b) => a + b, 0) / allRuleMatches.length : 0
        const ruleP25 = calculatePercentile(allRuleMatches, 25)
        const ruleP75 = calculatePercentile(allRuleMatches, 75)
        const ruleP90 = calculatePercentile(allRuleMatches, 90)

        return {
          name: ruleName,
          incidentCount: allRuleIncidentIds.size,
          avgMatches: ruleAvgMatches,
          p25: ruleP25,
          p75: ruleP75,
          p90: ruleP90,
          classifiers,
          exceptions
        }
      })

      // Calculate policy-level stats (including classifiers and exceptions from all rules)
      const allPolicyIncidentIds = new Set<number>()
      const allPolicyMatches: number[] = []
      ruleMap.forEach((ruleData) => {
        ruleData.classifiers.forEach((data) => {
          data.incidentIds.forEach(id => allPolicyIncidentIds.add(id))
          allPolicyMatches.push(...data.matches)
        })
        ruleData.exceptions.forEach((exceptionClassifierMap) => {
          exceptionClassifierMap.forEach((data) => {
            data.incidentIds.forEach(id => allPolicyIncidentIds.add(id))
            allPolicyMatches.push(...data.matches)
          })
        })
      })

      return {
        name: policyName,
        incidentCount: allPolicyIncidentIds.size,
        avgMatches: allPolicyMatches.length > 0 ? allPolicyMatches.reduce((a, b) => a + b, 0) / allPolicyMatches.length : 0,
        rules
      }
    })
  }, [userIncidents])

  // Calculate unique channels and policies from filtered user incidents
  const uniqueUserChannels = useMemo(() => {
    return Array.from(new Set(userIncidents.map(i => i.channel).filter(Boolean))).sort()
  }, [userIncidents])

  // Get unique policies using the same logic as exception recommendation report
  // Extract policies from violationTriggers first, then fallback to incident.policy
  const uniqueUserPolicies = useMemo(() => {
    const policySet = new Set<string>()

    userIncidents.forEach(incident => {
      let triggers: any[] = []
      if (incident.violationTriggers) {
        try {
          triggers = typeof incident.violationTriggers === 'string'
            ? JSON.parse(incident.violationTriggers)
            : incident.violationTriggers
        } catch {
          triggers = []
        }
      }

      // If triggers exist, extract policy names from them
      if (triggers.length > 0) {
        triggers.forEach((t: any) => {
          const policyName = t.PolicyName || t.policy_name || incident.policy
          if (policyName) {
            policySet.add(policyName)
          }
        })
      } else {
        // If no triggers, use incident.policy as fallback
        if (incident.policy) {
          policySet.add(incident.policy)
        }
      }
    })

    return Array.from(policySet).sort()
  }, [userIncidents])

  const handleCSVUpload = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = (e) => {
      const text = e.target?.result as string
      const lines = text.split('\n').filter(line => line.trim())

      if (lines.length < 2) {
        alert('CSV dosyası en az bir başlık satırı ve bir veri satırı içermelidir.')
        return
      }

      // İlk satır başlık satırı, onu atla veya kontrol et
      const headers = lines[0].split(',').map(h => h.trim().toLowerCase())
      const dataLines = lines.slice(1)

      const features: DomainFeature[] = dataLines.map((line, index) => {
        const values = line.split(',').map(v => v.trim())

        // CSV formatı: domain, gizlilik sözleşmesi, eğitim, noterlik, denetim, banka, hukuk firması
        return {
          domain: values[0] || '',
          gizlilikSozlesmesi: values[1] || '',
          egitim: values[2] || '',
          noterlik: values[3] || '',
          denetim: values[4] || '',
          banka: values[5] || '',
          hukukFirmasi: values[6] || '',
          istirak: values[7] || ''
        }
      }).filter(f => f.domain) // Boş domain'leri filtrele

      setDomainFeatures(features)
      navigateToView('domain-features')
    }

    reader.readAsText(file, 'UTF-8')
  }

  // Filtered Domain Features
  const filteredDomainFeatures = useMemo(() => {
    return domainFeatures.filter(feature => {
      if (domainFilter && !feature.domain.toLowerCase().includes(domainFilter.toLowerCase())) return false
      if (gizlilikFilter && !feature.gizlilikSozlesmesi.toLowerCase().includes(gizlilikFilter.toLowerCase())) return false
      if (istirakFilter && !feature.istirak.toLowerCase().includes(istirakFilter.toLowerCase())) return false
      if (egitimFilter && !feature.egitim.toLowerCase().includes(egitimFilter.toLowerCase())) return false
      if (noterlikFilter && !feature.noterlik.toLowerCase().includes(noterlikFilter.toLowerCase())) return false
      if (hukukFirmasiFilter && !feature.hukukFirmasi.toLowerCase().includes(hukukFirmasiFilter.toLowerCase())) return false
      if (denetimFilter && !feature.denetim.toLowerCase().includes(denetimFilter.toLowerCase())) return false
      if (bankaFilter && !feature.banka.toLowerCase().includes(bankaFilter.toLowerCase())) return false
      return true
    })
  }, [domainFeatures, domainFilter, gizlilikFilter, istirakFilter, egitimFilter, noterlikFilter, hukukFirmasiFilter, denetimFilter, bankaFilter])

  // Released Incidents Statistics
  const releasedIncidentsStats = useMemo(() => {
    // Admin bazlı sayılar
    const adminCounts: Record<string, number> = {}
    let incStartingCount = 0
    let nonIncStartingCount = 0

    releasedIncidents.forEach(incident => {
      // Admin bazlı sayım
      const admin = incident.admin_name || 'Unknown'
      adminCounts[admin] = (adminCounts[admin] || 0) + 1

      // Comment'te INC ile başlayan/başlamayan sayım
      const comments = incident.comments || ''
      if (comments.trim().toUpperCase().startsWith('INC')) {
        incStartingCount++
      } else {
        nonIncStartingCount++
      }
    })

    // Admin bazlı verileri sırala (en çok sayıya göre)
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

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <h1 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Analytics Report</h1>
        </div>


        {/* Domain Features - New API-based Manager */}
        {showDomainFeatures && (
          <div style={{ marginBottom: '24px' }}>
            <DomainFeaturesManager onClose={() => navigateToView(null)} />
          </div>
        )}

        {/* Mercek Analiz (en üst) + Released Incidents (ikinci) - order ile sıra */}
        {showCSVAnalysis && (
          <div style={{ display: 'flex', flexDirection: 'column', marginBottom: '24px' }}>
        {/* Released Incidents Section - order: 2 ile ikinci sırada */}
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

        {/* Mercek Analiz Section - order: 1 ile en üstte */}
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
                    onClick={() => fetchMercekData(csvCurrentPage, csvPageSize)}
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
                  <button
                    onClick={() => navigateToView(null)}
                    style={{
                      padding: '8px 16px',
                      borderRadius: '6px',
                      border: '1px solid var(--border)',
                      background: 'var(--surface-hover)',
                      color: 'var(--text-primary)',
                      fontSize: '14px',
                      fontWeight: '500',
                      cursor: 'pointer'
                    }}
                  >
                    Kapat
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

                      {/* Trend Chart - full width (Plotly) - Filtre varsa o aralık, yoksa son 90 gün */}
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
                // For API data, use server pagination
                const totalPages = mercekDataLoaded ? mercekTotalPages : Math.ceil(csvData.length / csvPageSize)
                const totalItems = mercekDataLoaded ? mercekTotalCount : csvData.length
                const rawPaginatedData = mercekDataLoaded ? csvData : csvData.slice((csvCurrentPage - 1) * csvPageSize, csvCurrentPage * csvPageSize)
                const paginatedData = getFilteredAndSortedMercekData(rawPaginatedData)
                const hasActiveFilters = Object.values(mercekColumnFilters).some(v => v.trim())

                return (
                  <>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                      <h3 style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', margin: 0 }}>Veri Tablosu</h3>
                      {hasActiveFilters && (
                        <button
                          onClick={() => { setMercekColumnFilters({}); setMercekSortColumn(null); }}
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
                          {/* Sort headers */}
                          <tr style={{ background: 'var(--background-secondary)' }}>
                            {csvHeaders.map((header, index) => {
                              const isSorted = mercekSortColumn === header
                              return (
                                <th
                                  key={index}
                                  onClick={() => handleMercekSort(header)}
                                  style={{
                                    padding: '10px 12px',
                                    textAlign: 'left',
                                    borderBottom: '1px solid var(--border)',
                                    color: isSorted ? '#3b82f6' : 'var(--text-primary)',
                                    fontWeight: '600',
                                    fontSize: '13px',
                                    whiteSpace: 'nowrap',
                                    cursor: 'pointer',
                                    userSelect: 'none',
                                    position: 'relative'
                                  }}
                                >
                                  <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                    {header}
                                    <span style={{ fontSize: '11px', opacity: isSorted ? 1 : 0.3 }}>
                                      {isSorted ? (mercekSortDirection === 'asc' ? ' ↑' : ' ↓') : ' ↕'}
                                    </span>
                                  </div>
                                </th>
                              )
                            })}
                          </tr>
                          {/* Column search row */}
                          <tr style={{ background: 'var(--background-secondary)' }}>
                            {csvHeaders.map((header, index) => (
                              <th key={`search-${index}`} style={{ padding: '4px 8px', borderBottom: '2px solid var(--border)' }}>
                                <input
                                  type="text"
                                  value={mercekColumnFilters[header] || ''}
                                  onChange={(e) => handleMercekColumnFilter(header, e.target.value)}
                                  placeholder="🔍"
                                  style={{
                                    width: '100%',
                                    padding: '4px 6px',
                                    borderRadius: '3px',
                                    border: `1px solid ${mercekColumnFilters[header] ? '#3b82f6' : 'var(--border)'}`,
                                    background: 'var(--background)',
                                    color: 'var(--text-primary)',
                                    fontSize: '12px',
                                    outline: 'none',
                                    boxSizing: 'border-box'
                                  }}
                                />
                              </th>
                            ))}
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
                        if (mercekDataLoaded) {
                          fetchMercekData(page)
                        }
                      }}
                      onPageSizeChange={(size) => {
                        setCsvPageSize(size)
                        setCsvCurrentPage(1)
                        if (mercekDataLoaded) {
                          fetchMercekData(1, size)
                        }
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
        )}


        {/* Heatmap Section */}
        {
          !showDomainFeatures && !showCSVAnalysis && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', padding: '24px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginBottom: '24px' }}>
                <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '12px' }}>Domain vs Team Heatmap</h2>

                {/* Inline Filters Row */}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', alignItems: 'flex-end', marginBottom: '16px', padding: '12px', background: 'var(--background)', borderRadius: '6px', border: '1px solid var(--border)' }}>
                  {/* Date Range */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Start</label>
                    <input
                      type="date"
                      value={dateRange.start}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => {
                        const val = e.target.value
                        setDateRange(prev => {
                          const newer = { ...prev, start: val }
                          if (newer.start && newer.end && newer.start > newer.end) {
                            setDateError('Start > End')
                          } else {
                            setDateError('')
                          }
                          return newer
                        })
                      }}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: dateError ? '1px solid #ef4444' : '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '130px' }}
                    />
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>End</label>
                    <input
                      type="date"
                      value={dateRange.end}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => {
                        const val = e.target.value
                        setDateRange(prev => {
                          const newer = { ...prev, end: val }
                          if (newer.start && newer.end && newer.start > newer.end) {
                            setDateError('Start > End')
                          } else {
                            setDateError('')
                          }
                          return newer
                        })
                      }}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: dateError ? '1px solid #ef4444' : '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '130px' }}
                    />
                  </div>

                  {/* Department */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Department</label>
                    <select
                      value={selectedDepartment}
                      onChange={(e: ChangeEvent<HTMLSelectElement>) => setSelectedDepartment(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', minWidth: '120px' }}
                    >
                      <option value="">All</option>
                      {uniqueDepartments.map(dept => (
                        <option key={dept} value={dept}>{dept}</option>
                      ))}
                    </select>
                  </div>

                  {/* Team */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Team</label>
                    <select
                      value={selectedTeam}
                      onChange={(e: ChangeEvent<HTMLSelectElement>) => setSelectedTeam(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', minWidth: '120px' }}
                    >
                      <option value="">All</option>
                      {uniqueTeams.map(team => (
                        <option key={team} value={team}>{team}</option>
                      ))}
                    </select>
                  </div>

                  {/* User */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>User</label>
                    <input
                      type="text"
                      placeholder="User..."
                      value={selectedUser}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedUser(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '100px' }}
                    />
                  </div>

                  {/* Full Name */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Full Name</label>
                    <input
                      type="text"
                      placeholder="Name..."
                      value={selectedFullName}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedFullName(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '100px' }}
                    />
                  </div>

                  {/* Policy */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Policy</label>
                    <input
                      type="text"
                      placeholder="Policy..."
                      value={selectedPolicy}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedPolicy(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '100px' }}
                    />
                  </div>

                  {/* Domain */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Domain</label>
                    <input
                      type="text"
                      placeholder="Domain..."
                      value={selectedDomain}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedDomain(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '100px' }}
                    />
                  </div>

                  {/* Action */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                    <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Action</label>
                    <select
                      value={selectedAction}
                      onChange={(e: ChangeEvent<HTMLSelectElement>) => setSelectedAction(e.target.value)}
                      style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', minWidth: '100px' }}
                    >
                      <option value="">All</option>
                      {uniqueActions.map(action => (
                        <option key={action} value={action}>{action}</option>
                      ))}
                    </select>
                  </div>

                  {/* Buttons */}
                  <div style={{ display: 'flex', gap: '6px', alignItems: 'flex-end' }}>
                    <button
                      onClick={applyFilters}
                      style={{
                        padding: '5px 14px',
                        borderRadius: '4px',
                        border: 'none',
                        background: '#3b82f6',
                        color: '#fff',
                        fontSize: '12px',
                        fontWeight: '600',
                        cursor: 'pointer',
                        whiteSpace: 'nowrap'
                      }}
                    >
                      Filtrele
                    </button>
                    <button
                      onClick={resetFilters}
                      style={{
                        padding: '5px 14px',
                        borderRadius: '4px',
                        border: '1px solid var(--border)',
                        background: 'var(--surface-hover)',
                        color: 'var(--text-primary)',
                        fontSize: '12px',
                        fontWeight: '500',
                        cursor: 'pointer',
                        whiteSpace: 'nowrap'
                      }}
                    >
                      Temizle
                    </button>
                  </div>
                  {dateError && <span style={{ color: '#ef4444', fontSize: '10px', alignSelf: 'center' }}>{dateError}</span>}
                </div>

                {/* Heatmap Grid - only show when data available */}
                {!loading && filteredIncidents.length > 0 && (() => {
                  const totalTeamPages = Math.ceil(heatmapData.teams.length / teamsPerPage)
                  const startIndex = (heatmapTeamPage - 1) * teamsPerPage
                  const endIndex = startIndex + teamsPerPage
                  const paginatedTeams = heatmapData.teams.slice(startIndex, endIndex)
                  return (
                <div style={{ position: 'relative', overflowX: 'auto', maxWidth: '100%' }}>
                  <div style={{
                    display: 'grid',
                    gridTemplateColumns: `180px repeat(${paginatedTeams.length}, 100px)`,
                    gap: '2px',
                    width: 'fit-content',
                    margin: '0 auto'
                  }}>
                    {/* Header Row */}
                    <div style={{
                      padding: '8px',
                      fontWeight: '600',
                      color: 'var(--text-secondary)',
                      fontSize: '12px',
                      position: 'sticky',
                      left: 0,
                      zIndex: 10,
                      background: 'var(--background-secondary)'
                    }}>Domain \ Team</div>
                    {paginatedTeams.map(team => (
                      <div key={team} style={{
                        padding: '8px 4px',
                        fontWeight: '600',
                        color: 'var(--text-secondary)',
                        fontSize: '13px',
                        textAlign: 'center',
                        background: 'var(--background-secondary)',
                        borderRadius: '2px',
                        minHeight: '60px',
                        maxHeight: '60px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        width: '100px'
                      }} title={team}>
                        <span style={{
                          wordWrap: 'break-word',
                          wordBreak: 'break-word',
                          overflowWrap: 'break-word',
                          lineHeight: '1.3',
                          overflow: 'hidden',
                          display: '-webkit-box',
                          WebkitLineClamp: 2,
                          WebkitBoxOrient: 'vertical',
                          textAlign: 'center',
                          width: '100%'
                        }}>
                          {team}
                        </span>
                      </div>
                    ))}

                    {/* Data Rows */}
                    {heatmapData.domains.map(domain => (
                      <React.Fragment key={domain}>
                        {/* Row Header - Sticky */}
                        <div key={`row-${domain}`} style={{
                          padding: '8px',
                          fontWeight: '600',
                          color: 'var(--text-primary)',
                          fontSize: '12px',
                          background: 'var(--background-secondary)',
                          borderRadius: '2px',
                          display: 'flex',
                          alignItems: 'center',
                          whiteSpace: 'nowrap',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          position: 'sticky',
                          left: 0,
                          zIndex: 5
                        }} title={domain}>
                          {domain}
                        </div>

                        {/* Cells */}
                        {paginatedTeams.map(team => {
                          const count = heatmapData.counts[team]?.[domain] || 0
                          const bd = heatmapData.breakdown[team]?.[domain] || { block: 0, permit: 0, authorized: 0, quarantine: 0 }
                          return (
                            <div key={`${team}-${domain}`} style={{
                              height: '30px',
                              width: '100px',
                              textAlign: 'center',
                              background: getHeatmapColor(count, heatmapData.maxCount),
                              color: getTextColor(count, heatmapData.maxCount),
                              borderRadius: '2px',
                              fontSize: '10px',
                              fontWeight: count > 0 ? '600' : '400',
                              transition: 'transform 0.2s',
                              cursor: 'pointer',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              position: 'relative'
                            }}
                              className="group"
                            >
                              {count > 0 ? count : ''}

                              {/* Popup Tooltip */}
                              {count > 0 && (
                                <div className="hidden group-hover:block" style={{
                                  position: 'absolute',
                                  bottom: '100%',
                                  left: '50%',
                                  transform: 'translateX(-50%)',
                                  background: 'var(--surface)',
                                  border: '1px solid var(--border)',
                                  padding: '8px',
                                  borderRadius: '4px',
                                  boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)',
                                  zIndex: 10,
                                  minWidth: '150px',
                                  pointerEvents: 'none',
                                  marginBottom: '4px'
                                }}>
                                  <div style={{ fontSize: '11px', fontWeight: '600', marginBottom: '4px', borderBottom: '1px solid var(--border)', paddingBottom: '4px', color: 'var(--text-primary)' }}>
                                    {domain} / {team}
                                  </div>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '2px' }}>
                                    <span>Block:</span> <span style={{ color: '#ef4444', fontWeight: '600' }}>{bd.block}</span>
                                  </div>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '2px' }}>
                                    <span>Quarantine:</span> <span style={{ color: '#f59e0b', fontWeight: '600' }}>{bd.quarantine}</span>
                                  </div>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '2px' }}>
                                    <span>Authorized:</span> <span style={{ color: '#3b82f6', fontWeight: '600' }}>{bd.authorized}</span>
                                  </div>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '2px' }}>
                                    <span>Permit:</span> <span style={{ color: '#10b981', fontWeight: '600' }}>{bd.permit}</span>
                                  </div>
                                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: 'var(--text-secondary)', marginTop: '4px', paddingTop: '4px', borderTop: '1px solid var(--border)' }}>
                                    <span>Avg Max Match:</span> <span style={{ color: '#8b5cf6', fontWeight: '600' }}>{bd.incidentCount > 0 ? (bd.maxMatchTotal / bd.incidentCount).toFixed(1) : 0}</span>
                                  </div>
                                </div>
                              )}
                            </div>
                          )
                        })}
                      </React.Fragment>
                    ))}
                  </div>

                  {/* Pagination Controls */}
                  {totalTeamPages > 1 && (
                    <div style={{
                      marginTop: '16px',
                      paddingTop: '16px',
                      borderTop: '1px solid var(--border)'
                    }}>
                      <Pagination
                        currentPage={heatmapTeamPage}
                        totalPages={totalTeamPages}
                        totalItems={heatmapData.teams.length}
                        pageSize={teamsPerPage}
                        onPageChange={setHeatmapTeamPage}
                        showPageInput={true}
                        showFirstLast={true}
                        showTotalItems={true}
                      />
                    </div>
                  )}
                </div>
                  )
                })()}
              </div>
          )
        }

        {/* Incidents Table */}
        {
          !showDomainFeatures && !showCSVAnalysis && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginBottom: '32px' }}>
              <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Incidents List</h2>
                <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                  Showing {filteredIncidents.length > 0 ? (currentPage - 1) * itemsPerPage + 1 : 0} - {Math.min(currentPage * itemsPerPage, filteredIncidents.length)} of {filteredIncidents.length} incidents
                </span>
              </div>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
                    {(['time', 'user', 'fullName', 'department', 'team', 'policy', 'domain', 'max', 'action'] as const).map(column => {
                      const columnLabels: Record<string, string> = {
                        time: 'Time',
                        user: 'User',
                        fullName: 'Full Name',
                        department: 'Department',
                        team: 'Team',
                        policy: 'Policy',
                        domain: 'Domain',
                        max: 'Max',
                        action: 'Action'
                      }
                      const isSorted = sortColumn === column
                      const uniqueValues = getUniqueColumnValues(column)
                      const searchQuery = columnFilterSearch[column] || ''
                      const filteredValues = uniqueValues.filter(v =>
                        v.toLowerCase().includes(searchQuery.toLowerCase())
                      )
                      const selectedValues = columnFilters[column] || []

                      return (
                        <th
                          key={column}
                          data-column-filter
                          style={{
                            padding: '16px',
                            textAlign: 'left',
                            fontSize: '13px',
                            fontWeight: '600',
                            color: 'var(--text-secondary)',
                            width: column === 'time' ? '150px' : column === 'action' ? '100px' : 'auto',
                            position: 'relative'
                          }}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <span
                              onClick={() => handleSort(column)}
                              style={{
                                cursor: 'pointer',
                                userSelect: 'none',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px',
                                flex: 1
                              }}
                            >
                              {columnLabels[column]}
                              {isSorted && (
                                <span style={{ fontSize: '10px' }}>
                                  {sortDirection === 'asc' ? '↑' : '↓'}
                                </span>
                              )}
                            </span>
                            <span
                              onClick={(e) => {
                                e.stopPropagation()
                                toggleColumnFilter(column)
                              }}
                              style={{
                                cursor: 'pointer',
                                fontSize: '12px',
                                padding: '2px 6px',
                                borderRadius: '4px',
                                background: selectedValues.length > 0 ? '#3b82f6' : 'transparent',
                                color: selectedValues.length > 0 ? '#ffffff' : 'var(--text-secondary)',
                                border: selectedValues.length > 0 ? 'none' : '1px solid var(--border)'
                              }}
                              title={`Filter ${columnLabels[column]}`}
                            >
                              {selectedValues.length > 0 ? `${selectedValues.length}` : '⋯'}
                            </span>
                          </div>
                          {openColumnFilter === column && (
                            <div
                              style={{
                                position: 'absolute',
                                top: '100%',
                                left: 0,
                                right: 0,
                                marginTop: '4px',
                                background: 'var(--background)',
                                border: '1px solid var(--border)',
                                borderRadius: '6px',
                                boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                                zIndex: 1000,
                                maxHeight: '300px',
                                overflowY: 'auto',
                                padding: '8px'
                              }}
                              onClick={(e) => e.stopPropagation()}
                            >
                              <input
                                type="text"
                                placeholder="Search..."
                                value={searchQuery}
                                onChange={(e) => setColumnFilterSearch(prev => ({ ...prev, [column]: e.target.value }))}
                                style={{
                                  width: '100%',
                                  padding: '6px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid var(--border)',
                                  background: 'var(--surface)',
                                  color: 'var(--text-primary)',
                                  fontSize: '12px',
                                  marginBottom: '8px'
                                }}
                                onClick={(e) => e.stopPropagation()}
                              />
                              <div
                                onClick={() => {
                                  if (selectedValues.length === filteredValues.length) {
                                    setColumnFilters(prev => ({ ...prev, [column]: [] }))
                                  } else {
                                    setColumnFilters(prev => ({ ...prev, [column]: [...filteredValues] }))
                                  }
                                }}
                                style={{
                                  padding: '6px 8px',
                                  borderBottom: '1px solid var(--border)',
                                  cursor: 'pointer',
                                  fontSize: '11px',
                                  fontWeight: '600',
                                  color: 'var(--text-primary)',
                                  background: selectedValues.length === filteredValues.length ? 'var(--surface-hover)' : 'transparent',
                                  marginBottom: '4px'
                                }}
                              >
                                {selectedValues.length === filteredValues.length ? '✓ Deselect All' : 'Select All'}
                              </div>
                              {filteredValues.map(value => (
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
                                      toggleColumnFilterValue(column, value)
                                    }}
                                    style={{ marginRight: '8px', cursor: 'pointer' }}
                                  />
                                  <span>{value}</span>
                                </label>
                              ))}
                            </div>
                          )}
                        </th>
                      )
                    })}
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={9} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>Loading incidents...</td>
                    </tr>
                  ) : paginatedIncidents.length === 0 ? (
                    <tr>
                      <td colSpan={9} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>No incidents found matching filters</td>
                    </tr>
                  ) : (
                    paginatedIncidents.map((incident) => (
                      <tr key={incident.id} style={{ borderBottom: '1px solid var(--border)', transition: 'background 0.2s' }} className="hover:bg-[var(--surface-hover)]">
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {format(new Date(incident.timestamp), 'dd MMM yyyy HH:mm:ss')}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.userEmail || 'Unknown'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.fullName || '-'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.department || '-'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {normalizeTeamName(incident.team) || 'Hesap Araştırmaları'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.policy || '-'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.domain || '-'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.maxMatches || 0}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          <span style={{
                            padding: '4px 10px',
                            borderRadius: '12px',
                            ...(() => {
                              const action = incident.action?.toLowerCase() || ''
                              if (action.includes('block')) return { background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444' }
                              if (action.includes('quarantine')) return { background: 'rgba(245, 158, 11, 0.1)', color: '#f59e0b' }
                              if (action.includes('authorized') || action.includes('allow')) return { background: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6' }
                              return { background: 'rgba(16, 185, 129, 0.1)', color: '#10b981' } // Permit, Released, etc.
                            })(),
                            fontSize: '12px',
                            fontWeight: '600',
                            display: 'inline-block'
                          }}>
                            {incident.action}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>

              {/* Pagination Controls */}
              {!loading && filteredIncidents.length > 0 && (
                <div style={{ padding: '16px', borderTop: '1px solid var(--border)' }}>
                  <Pagination
                    currentPage={currentPage}
                    totalPages={totalPages}
                    totalItems={filteredIncidents.length}
                    pageSize={itemsPerPage}
                    onPageChange={setCurrentPage}
                    showPageInput={true}
                    showFirstLast={true}
                    showTotalItems={true}
                  />
                </div>
              )}
            </div>
          )
        }

        {/* User Incident Report Section */}
        {
          !showDomainFeatures && !showCSVAnalysis && !loading && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', padding: '24px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginTop: '24px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '20px' }}>Exception Recommendation</h2>

              <div style={{ marginBottom: '20px', display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 1fr', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Search User
                  </label>
                  <input
                    type="text"
                    placeholder="Enter user email, login name, or full name..."
                    value={userSearchQuery}
                    onChange={(e) => setUserSearchQuery(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '10px',
                      borderRadius: '6px',
                      border: '1px solid var(--border)',
                      background: 'var(--background)',
                      color: 'var(--text-primary)',
                      fontSize: '13px'
                    }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Filter by Domain
                  </label>
                  <input
                    type="text"
                    placeholder="Enter domain to filter..."
                    value={exceptionDomainFilter}
                    onChange={(e) => setExceptionDomainFilter(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '10px',
                      borderRadius: '6px',
                      border: '1px solid var(--border)',
                      background: 'var(--background)',
                      color: 'var(--text-primary)',
                      fontSize: '13px'
                    }}
                  />
                </div>
                <div style={{ position: 'relative' }} data-dropdown>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Filter by Action
                  </label>
                  <div
                    onClick={() => {
                      setActionDropdownOpen(!actionDropdownOpen)
                      setChannelDropdownOpen(false)
                      setPolicyDropdownOpen(false)
                    }}
                    style={{
                      width: '100%',
                      padding: '10px',
                      borderRadius: '6px',
                      border: '1px solid var(--border)',
                      background: 'var(--background)',
                      color: 'var(--text-primary)',
                      fontSize: '13px',
                      cursor: 'pointer',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      minHeight: '20px'
                    }}
                  >
                    <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {exceptionActionFilter.length === 0
                        ? 'All Actions'
                        : exceptionActionFilter.length === 1
                          ? exceptionActionFilter[0]
                          : `${exceptionActionFilter.length} selected`}
                    </span>
                    <span style={{ marginLeft: '8px' }}>{actionDropdownOpen ? '▲' : '▼'}</span>
                  </div>
                  {actionDropdownOpen && (
                    <div
                      style={{
                        position: 'absolute',
                        top: '100%',
                        left: 0,
                        right: 0,
                        marginTop: '4px',
                        background: 'var(--background)',
                        border: '1px solid var(--border)',
                        borderRadius: '6px',
                        boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                        zIndex: 1000,
                        maxHeight: '200px',
                        overflowY: 'auto'
                      }}
                    >
                      <div
                        onClick={() => {
                          if (exceptionActionFilter.length === uniqueActions.length) {
                            setExceptionActionFilter([])
                          } else {
                            setExceptionActionFilter([...uniqueActions])
                          }
                        }}
                        style={{
                          padding: '8px 12px',
                          borderBottom: '1px solid var(--border)',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '600',
                          color: 'var(--text-primary)',
                          background: exceptionActionFilter.length === uniqueActions.length ? 'var(--surface-hover)' : 'transparent'
                        }}
                      >
                        {exceptionActionFilter.length === uniqueActions.length ? '✓ Deselect All' : 'Select All'}
                      </div>
                      {uniqueActions.map(action => (
                        <label
                          key={action}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            padding: '8px 12px',
                            cursor: 'pointer',
                            fontSize: '13px',
                            color: 'var(--text-primary)',
                            borderBottom: '1px solid var(--border)',
                            background: exceptionActionFilter.includes(action) ? 'var(--surface-hover)' : 'transparent'
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <input
                            type="checkbox"
                            checked={exceptionActionFilter.includes(action)}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setExceptionActionFilter([...exceptionActionFilter, action])
                              } else {
                                setExceptionActionFilter(exceptionActionFilter.filter(a => a !== action))
                              }
                            }}
                            style={{ marginRight: '8px', cursor: 'pointer' }}
                          />
                          <span>{action}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
                <div style={{ position: 'relative' }} data-dropdown>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Filter by Channel
                  </label>
                  <div
                    onClick={() => {
                      setChannelDropdownOpen(!channelDropdownOpen)
                      setActionDropdownOpen(false)
                      setPolicyDropdownOpen(false)
                    }}
                    style={{
                      width: '100%',
                      padding: '10px',
                      borderRadius: '6px',
                      border: '1px solid var(--border)',
                      background: 'var(--background)',
                      color: 'var(--text-primary)',
                      fontSize: '13px',
                      cursor: 'pointer',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      minHeight: '20px'
                    }}
                  >
                    <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {exceptionChannelFilter.length === 0
                        ? 'All Channels'
                        : exceptionChannelFilter.length === 1
                          ? exceptionChannelFilter[0]
                          : `${exceptionChannelFilter.length} selected`}
                    </span>
                    <span style={{ marginLeft: '8px' }}>{channelDropdownOpen ? '▲' : '▼'}</span>
                  </div>
                  {channelDropdownOpen && (
                    <div
                      style={{
                        position: 'absolute',
                        top: '100%',
                        left: 0,
                        right: 0,
                        marginTop: '4px',
                        background: 'var(--background)',
                        border: '1px solid var(--border)',
                        borderRadius: '6px',
                        boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                        zIndex: 1000,
                        maxHeight: '200px',
                        overflowY: 'auto'
                      }}
                    >
                      <div
                        onClick={() => {
                          if (exceptionChannelFilter.length === uniqueChannels.length) {
                            setExceptionChannelFilter([])
                          } else {
                            setExceptionChannelFilter([...uniqueChannels])
                          }
                        }}
                        style={{
                          padding: '8px 12px',
                          borderBottom: '1px solid var(--border)',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '600',
                          color: 'var(--text-primary)',
                          background: exceptionChannelFilter.length === uniqueChannels.length ? 'var(--surface-hover)' : 'transparent'
                        }}
                      >
                        {exceptionChannelFilter.length === uniqueChannels.length ? '✓ Deselect All' : 'Select All'}
                      </div>
                      {uniqueChannels.map(channel => (
                        <label
                          key={channel}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            padding: '8px 12px',
                            cursor: 'pointer',
                            fontSize: '13px',
                            color: 'var(--text-primary)',
                            borderBottom: '1px solid var(--border)',
                            background: exceptionChannelFilter.includes(channel) ? 'var(--surface-hover)' : 'transparent'
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <input
                            type="checkbox"
                            checked={exceptionChannelFilter.includes(channel)}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setExceptionChannelFilter([...exceptionChannelFilter, channel])
                              } else {
                                setExceptionChannelFilter(exceptionChannelFilter.filter(c => c !== channel))
                              }
                            }}
                            style={{ marginRight: '8px', cursor: 'pointer' }}
                          />
                          <span>{channel}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
                <div style={{ position: 'relative' }} data-dropdown>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Filter by Policy
                  </label>
                  <div
                    onClick={() => {
                      setPolicyDropdownOpen(!policyDropdownOpen)
                      setActionDropdownOpen(false)
                      setChannelDropdownOpen(false)
                    }}
                    style={{
                      width: '100%',
                      padding: '10px',
                      borderRadius: '6px',
                      border: '1px solid var(--border)',
                      background: 'var(--background)',
                      color: 'var(--text-primary)',
                      fontSize: '13px',
                      cursor: 'pointer',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      minHeight: '20px'
                    }}
                  >
                    <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {exceptionPolicyFilter.length === 0
                        ? 'All Policies'
                        : exceptionPolicyFilter.length === 1
                          ? exceptionPolicyFilter[0]
                          : `${exceptionPolicyFilter.length} selected`}
                    </span>
                    <span style={{ marginLeft: '8px' }}>{policyDropdownOpen ? '▲' : '▼'}</span>
                  </div>
                  {policyDropdownOpen && (
                    <div
                      style={{
                        position: 'absolute',
                        top: '100%',
                        left: 0,
                        right: 0,
                        marginTop: '4px',
                        background: 'var(--background)',
                        border: '1px solid var(--border)',
                        borderRadius: '6px',
                        boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                        zIndex: 1000,
                        maxHeight: '200px',
                        overflowY: 'auto'
                      }}
                    >
                      <div
                        onClick={() => {
                          if (exceptionPolicyFilter.length === uniquePolicies.length) {
                            setExceptionPolicyFilter([])
                          } else {
                            setExceptionPolicyFilter([...uniquePolicies])
                          }
                        }}
                        style={{
                          padding: '8px 12px',
                          borderBottom: '1px solid var(--border)',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '600',
                          color: 'var(--text-primary)',
                          background: exceptionPolicyFilter.length === uniquePolicies.length ? 'var(--surface-hover)' : 'transparent'
                        }}
                      >
                        {exceptionPolicyFilter.length === uniquePolicies.length ? '✓ Deselect All' : 'Select All'}
                      </div>
                      {uniquePolicies.map(policy => (
                        <label
                          key={policy}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            padding: '8px 12px',
                            cursor: 'pointer',
                            fontSize: '13px',
                            color: 'var(--text-primary)',
                            borderBottom: '1px solid var(--border)',
                            background: exceptionPolicyFilter.includes(policy) ? 'var(--surface-hover)' : 'transparent'
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <input
                            type="checkbox"
                            checked={exceptionPolicyFilter.includes(policy)}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setExceptionPolicyFilter([...exceptionPolicyFilter, policy])
                              } else {
                                setExceptionPolicyFilter(exceptionPolicyFilter.filter(p => p !== policy))
                              }
                            }}
                            style={{ marginRight: '8px', cursor: 'pointer' }}
                          />
                          <span>{policy}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
              </div>

              {/* Recommend Button and Clear Filters */}
              {(() => {
                const hasUserQuery = userSearchQuery.trim()
                const hasFilters = exceptionActionFilter.length > 0 ||
                  exceptionChannelFilter.length > 0 ||
                  exceptionPolicyFilter.length > 0 ||
                  exceptionDomainFilter.trim()
                const canRecommend = hasUserQuery || hasFilters

                return (
                  <div style={{ marginBottom: '20px', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                    <button
                      onClick={handleClearFilters}
                      style={{
                        padding: '12px 24px',
                        borderRadius: '6px',
                        border: '1px solid var(--border)',
                        background: 'var(--surface)',
                        color: 'var(--text-primary)',
                        fontSize: '14px',
                        fontWeight: '600',
                        cursor: 'pointer',
                        transition: 'all 0.2s'
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.background = 'var(--surface-hover)'
                        e.currentTarget.style.transform = 'translateY(-1px)'
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.background = 'var(--surface)'
                        e.currentTarget.style.transform = 'translateY(0)'
                      }}
                    >
                      Filtreleri Temizle
                    </button>
                    <button
                      onClick={handleRecommend}
                      disabled={!canRecommend}
                      style={{
                        padding: '12px 24px',
                        borderRadius: '6px',
                        border: 'none',
                        background: canRecommend ? '#3b82f6' : '#93c5fd',
                        color: '#ffffff',
                        fontSize: '14px',
                        fontWeight: '600',
                        cursor: canRecommend ? 'pointer' : 'not-allowed',
                        transition: 'all 0.2s',
                        boxShadow: canRecommend ? '0 2px 4px rgba(59, 130, 246, 0.3)' : 'none',
                        opacity: canRecommend ? 1 : 0.7
                      }}
                      onMouseEnter={(e) => {
                        if (canRecommend) {
                          e.currentTarget.style.background = '#2563eb'
                          e.currentTarget.style.transform = 'translateY(-1px)'
                          e.currentTarget.style.boxShadow = '0 4px 6px rgba(59, 130, 246, 0.4)'
                        } else {
                          e.currentTarget.style.background = '#93c5fd'
                        }
                      }}
                      onMouseLeave={(e) => {
                        if (canRecommend) {
                          e.currentTarget.style.background = '#3b82f6'
                          e.currentTarget.style.transform = 'translateY(0)'
                          e.currentTarget.style.boxShadow = '0 2px 4px rgba(59, 130, 246, 0.3)'
                        } else {
                          e.currentTarget.style.background = '#93c5fd'
                        }
                      }}
                    >
                      Recommend
                    </button>
                  </div>
                )
              })()}

              {/* Summary Statistics */}
              {userIncidents.length > 0 && userReportData.length > 0 && (
                <div style={{
                  display: 'flex',
                  gap: '24px',
                  marginBottom: '24px',
                  padding: '16px',
                  background: 'var(--background-secondary)',
                  borderRadius: '8px',
                  border: '1px solid var(--border)'
                }}>
                  <div
                    style={{
                      display: 'flex',
                      flexDirection: 'column',
                      gap: '4px',
                      position: 'relative',
                      cursor: 'pointer'
                    }}
                    className="group"
                  >
                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: '500' }}>
                      Total Policies
                    </div>
                    <div style={{ fontSize: '20px', fontWeight: '600', color: 'var(--text-primary)' }}>
                      {userReportData.length}
                    </div>
                    {/* Tooltip */}
                    {uniqueUserPolicies.length > 0 && (
                      <div className="hidden group-hover:block" style={{
                        position: 'absolute',
                        bottom: '100%',
                        left: 0,
                        marginBottom: '8px',
                        background: 'var(--surface)',
                        border: '1px solid var(--border)',
                        padding: '12px',
                        borderRadius: '6px',
                        boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                        zIndex: 1000,
                        minWidth: '200px',
                        maxWidth: '400px',
                        pointerEvents: 'none'
                      }}>
                        <div style={{ fontSize: '11px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)', paddingBottom: '4px' }}>
                          Policies ({uniqueUserPolicies.length}):
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.6' }}>
                          {uniqueUserPolicies.map((policy, idx) => (
                            <div key={idx} style={{ marginBottom: '4px' }}>
                              • {policy}
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: '500' }}>
                      Total Incidents
                    </div>
                    <div style={{ fontSize: '20px', fontWeight: '600', color: '#3b82f6' }}>
                      {userIncidents.length}
                    </div>
                  </div>
                  <div
                    style={{
                      display: 'flex',
                      flexDirection: 'column',
                      gap: '4px',
                      position: 'relative',
                      cursor: 'pointer'
                    }}
                    className="group"
                  >
                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: '500' }}>
                      Total Channels
                    </div>
                    <div style={{ fontSize: '20px', fontWeight: '600', color: '#10b981' }}>
                      {uniqueUserChannels.length}
                    </div>
                    {/* Tooltip */}
                    {uniqueUserChannels.length > 0 && (
                      <div className="hidden group-hover:block" style={{
                        position: 'absolute',
                        bottom: '100%',
                        left: 0,
                        marginBottom: '8px',
                        background: 'var(--surface)',
                        border: '1px solid var(--border)',
                        padding: '12px',
                        borderRadius: '6px',
                        boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                        zIndex: 1000,
                        minWidth: '200px',
                        maxWidth: '400px',
                        pointerEvents: 'none'
                      }}>
                        <div style={{ fontSize: '11px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)', paddingBottom: '4px' }}>
                          Channels ({uniqueUserChannels.length}):
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.6' }}>
                          {uniqueUserChannels.map((channel, idx) => (
                            <div key={idx} style={{ marginBottom: '4px' }}>
                              • {channel}
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {loadingUserIncidents ? (
                <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  Loading incidents...
                </div>
              ) : userIncidents.length === 0 ? (
                <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  No incidents found matching the selected filters
                </div>
              ) : userReportData.length === 0 ? (
                <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  No policy data available
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                  {userReportData.map((policy, pIdx) => {
                    const isPolicyExpanded = expandedPolicies.has(pIdx)
                    return (
                      <div key={pIdx} style={{
                        background: 'var(--background-secondary)',
                        borderRadius: '8px',
                        border: '1px solid var(--border)',
                        padding: '20px',
                        boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
                      }}>
                        {/* Policy Header - Clickable */}
                        <div
                          onClick={() => togglePolicy(pIdx)}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            marginBottom: isPolicyExpanded ? '16px' : '0',
                            paddingBottom: isPolicyExpanded ? '12px' : '0',
                            borderBottom: isPolicyExpanded ? '2px solid var(--border)' : 'none',
                            cursor: 'pointer',
                            transition: 'all 0.2s'
                          }}
                          onMouseEnter={(e) => {
                            e.currentTarget.style.opacity = '0.8'
                          }}
                          onMouseLeave={(e) => {
                            e.currentTarget.style.opacity = '1'
                          }}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                            <div style={{
                              width: '24px',
                              height: '24px',
                              borderRadius: '50%',
                              background: '#3b82f6',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              color: 'white',
                              fontSize: '16px',
                              fontWeight: '600',
                              flexShrink: 0
                            }}>
                              {isPolicyExpanded ? '−' : '+'}
                            </div>
                            <span style={{ width: '12px', height: '12px', borderRadius: '50%', background: '#3b82f6' }} />
                            <h3 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>
                              {policy.name}
                            </h3>
                          </div>
                          <div style={{ display: 'flex', gap: '24px', fontSize: '13px' }}>
                            <div style={{ textAlign: 'right' }}>
                              <div style={{ color: 'var(--text-secondary)', fontSize: '11px', marginBottom: '4px' }}>Total Incidents</div>
                              <div style={{ color: 'var(--text-primary)', fontWeight: '600', fontSize: '16px' }}>{policy.incidentCount}</div>
                            </div>
                            <div style={{ textAlign: 'right' }}>
                              <div style={{ color: 'var(--text-secondary)', fontSize: '11px', marginBottom: '4px' }}>Avg Matches</div>
                              <div style={{ color: '#3b82f6', fontWeight: '600', fontSize: '16px' }}>{policy.avgMatches.toFixed(1)}</div>
                            </div>
                          </div>
                        </div>

                        {/* Rules - Accordion Content */}
                        {isPolicyExpanded && policy.rules.length > 0 && (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', paddingLeft: '12px' }}>
                            {policy.rules.map((rule, rIdx) => {
                              const ruleKey = `${pIdx}-${rIdx}`
                              const isRuleExpanded = expandedRules.has(ruleKey)
                              return (
                                <div key={rIdx} style={{
                                  borderLeft: '3px solid #ef4444',
                                  paddingLeft: '16px',
                                  paddingTop: '12px',
                                  paddingBottom: '12px',
                                  background: 'var(--surface)',
                                  borderRadius: '6px'
                                }}>
                                  {/* Rule Header - Clickable */}
                                  <div
                                    onClick={() => toggleRule(pIdx, rIdx)}
                                    style={{
                                      display: 'flex',
                                      alignItems: 'center',
                                      justifyContent: 'space-between',
                                      marginBottom: isRuleExpanded ? '12px' : '0',
                                      cursor: 'pointer',
                                      transition: 'all 0.2s'
                                    }}
                                    onMouseEnter={(e) => {
                                      e.currentTarget.style.opacity = '0.8'
                                    }}
                                    onMouseLeave={(e) => {
                                      e.currentTarget.style.opacity = '1'
                                    }}
                                  >
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                      <div style={{
                                        width: '20px',
                                        height: '20px',
                                        borderRadius: '50%',
                                        background: '#3b82f6',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        color: 'white',
                                        fontSize: '14px',
                                        fontWeight: '600',
                                        flexShrink: 0
                                      }}>
                                        {isRuleExpanded ? '−' : '+'}
                                      </div>
                                      <span style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)' }}>
                                        📋 {rule.name}
                                      </span>
                                    </div>
                                    <div style={{ display: 'flex', gap: '16px', fontSize: '12px' }}>
                                      <div style={{ textAlign: 'right' }}>
                                        <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '2px' }}>Incidents</div>
                                        <div style={{ color: 'var(--text-primary)', fontWeight: '600' }}>{rule.incidentCount}</div>
                                      </div>
                                      <div style={{ textAlign: 'right' }}>
                                        <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '2px' }}>Avg Matches</div>
                                        <div style={{ color: '#3b82f6', fontWeight: '600' }}>{rule.avgMatches.toFixed(1)}</div>
                                      </div>
                                      <div style={{ textAlign: 'right' }}>
                                        <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '2px' }}>P25</div>
                                        <div style={{ color: '#10b981', fontWeight: '600' }}>{rule.p25.toFixed(1)}</div>
                                      </div>
                                      <div style={{ textAlign: 'right' }}>
                                        <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '2px' }}>P75</div>
                                        <div style={{ color: '#f59e0b', fontWeight: '600' }}>{rule.p75.toFixed(1)}</div>
                                      </div>
                                      <div style={{ textAlign: 'right' }}>
                                        <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '2px' }}>P90</div>
                                        <div style={{ color: '#ef4444', fontWeight: '600' }}>{rule.p90.toFixed(1)}</div>
                                      </div>
                                    </div>
                                  </div>

                                  {/* Rule Content - Accordion */}
                                  {isRuleExpanded && (
                                    <>
                                      {/* Classifiers Statistics */}
                                      {rule.classifiers.length > 0 && (
                                        <div style={{
                                          paddingLeft: '8px',
                                          marginTop: '12px',
                                          marginBottom: '12px'
                                        }}>
                                          <div style={{
                                            padding: '16px',
                                            background: 'var(--background-secondary)',
                                            borderRadius: '8px',
                                            border: '1px solid var(--border)'
                                          }}>
                                            <h5 style={{
                                              fontSize: '13px',
                                              fontWeight: '600',
                                              color: 'var(--text-primary)',
                                              marginBottom: '12px',
                                              paddingBottom: '8px',
                                              borderBottom: '1px solid var(--border)'
                                            }}>
                                              Classifier Statistics
                                            </h5>
                                            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                              {rule.classifiers.map((classifier, cIdx) => {
                                                const classifierKey = `${pIdx}-${rIdx}-${cIdx}`
                                                const isClassifierExpanded = expandedClassifiers.has(classifierKey)
                                                return (
                                                  <div key={cIdx} style={{
                                                    padding: '12px',
                                                    background: 'var(--surface)',
                                                    border: '1px solid var(--border)',
                                                    borderRadius: '6px'
                                                  }}>
                                                    {/* Classifier Header - Clickable */}
                                                    <div
                                                      onClick={() => toggleClassifier(pIdx, rIdx, cIdx)}
                                                      style={{
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        justifyContent: 'space-between',
                                                        marginBottom: isClassifierExpanded ? '8px' : '0',
                                                        cursor: 'pointer',
                                                        transition: 'all 0.2s'
                                                      }}
                                                      onMouseEnter={(e) => {
                                                        e.currentTarget.style.opacity = '0.8'
                                                      }}
                                                      onMouseLeave={(e) => {
                                                        e.currentTarget.style.opacity = '1'
                                                      }}
                                                    >
                                                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                        <div style={{
                                                          width: '18px',
                                                          height: '18px',
                                                          borderRadius: '50%',
                                                          background: '#3b82f6',
                                                          display: 'flex',
                                                          alignItems: 'center',
                                                          justifyContent: 'center',
                                                          color: 'white',
                                                          fontSize: '12px',
                                                          fontWeight: '600',
                                                          flexShrink: 0
                                                        }}>
                                                          {isClassifierExpanded ? '−' : '+'}
                                                        </div>
                                                        <div style={{
                                                          fontSize: '12px',
                                                          fontWeight: '600',
                                                          color: 'var(--text-primary)'
                                                        }}>
                                                          {classifier.name}
                                                        </div>
                                                      </div>
                                                    </div>
                                                    {/* Classifier Content */}
                                                    {isClassifierExpanded && (
                                                      <div style={{
                                                        display: 'grid',
                                                        gridTemplateColumns: 'repeat(5, 1fr)',
                                                        gap: '12px',
                                                        fontSize: '11px',
                                                        marginTop: '8px'
                                                      }}>
                                                        <div style={{ textAlign: 'center' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '4px' }}>Incidents</div>
                                                          <div style={{ color: 'var(--text-primary)', fontWeight: '600' }}>{classifier.incidentCount}</div>
                                                        </div>
                                                        <div style={{ textAlign: 'center' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '4px' }}>Avg Matches</div>
                                                          <div style={{ color: '#3b82f6', fontWeight: '600' }}>{classifier.avgMatches.toFixed(1)}</div>
                                                        </div>
                                                        <div style={{ textAlign: 'center' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '4px' }}>P25</div>
                                                          <div style={{ color: '#10b981', fontWeight: '600' }}>{classifier.p25.toFixed(1)}</div>
                                                        </div>
                                                        <div style={{ textAlign: 'center' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '4px' }}>P75</div>
                                                          <div style={{ color: '#f59e0b', fontWeight: '600' }}>{classifier.p75.toFixed(1)}</div>
                                                        </div>
                                                        <div style={{ textAlign: 'center' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginBottom: '4px' }}>P90</div>
                                                          <div style={{ color: '#ef4444', fontWeight: '600' }}>{classifier.p90.toFixed(1)}</div>
                                                        </div>
                                                      </div>
                                                    )}
                                                  </div>
                                                )
                                              })}
                                            </div>
                                          </div>
                                        </div>
                                      )}

                                      {/* Exceptions Statistics */}
                                      {rule.exceptions && rule.exceptions.length > 0 && (
                                        <div style={{
                                          paddingLeft: '8px',
                                          marginTop: '12px',
                                          marginBottom: '12px'
                                        }}>
                                          <div style={{
                                            padding: '16px',
                                            background: 'var(--background-secondary)',
                                            borderRadius: '8px',
                                            border: '1px solid var(--border)'
                                          }}>
                                            <h5 style={{
                                              fontSize: '13px',
                                              fontWeight: '600',
                                              color: 'var(--text-primary)',
                                              marginBottom: '12px',
                                              paddingBottom: '8px',
                                              borderBottom: '1px solid var(--border)'
                                            }}>
                                              Exception Statistics
                                            </h5>
                                            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                              {rule.exceptions.map((exception, eIdx) => {
                                                const exceptionKey = `${pIdx}-${rIdx}-${eIdx}`
                                                const isExceptionExpanded = expandedExceptions.has(exceptionKey)
                                                return (
                                                  <div key={eIdx} style={{
                                                    padding: '12px',
                                                    background: 'var(--surface)',
                                                    border: '2px solid #f59e0b',
                                                    borderRadius: '6px'
                                                  }}>
                                                    {/* Exception Header - Clickable */}
                                                    <div
                                                      onClick={() => toggleException(pIdx, rIdx, eIdx)}
                                                      style={{
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        justifyContent: 'space-between',
                                                        marginBottom: isExceptionExpanded ? '8px' : '0',
                                                        cursor: 'pointer',
                                                        transition: 'all 0.2s'
                                                      }}
                                                      onMouseEnter={(e) => {
                                                        e.currentTarget.style.opacity = '0.8'
                                                      }}
                                                      onMouseLeave={(e) => {
                                                        e.currentTarget.style.opacity = '1'
                                                      }}
                                                    >
                                                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                        <div style={{
                                                          width: '18px',
                                                          height: '18px',
                                                          borderRadius: '50%',
                                                          background: '#3b82f6',
                                                          display: 'flex',
                                                          alignItems: 'center',
                                                          justifyContent: 'center',
                                                          color: 'white',
                                                          fontSize: '12px',
                                                          fontWeight: '600',
                                                          flexShrink: 0
                                                        }}>
                                                          {isExceptionExpanded ? '−' : '+'}
                                                        </div>
                                                        <span style={{
                                                          fontSize: '12px',
                                                          fontWeight: '600',
                                                          color: '#f59e0b'
                                                        }}>
                                                          ⚠️ {exception.name}
                                                        </span>
                                                        <span style={{
                                                          fontSize: '10px',
                                                          fontWeight: '600',
                                                          padding: '2px 6px',
                                                          borderRadius: '4px',
                                                          background: 'rgba(245, 158, 11, 0.15)',
                                                          color: '#f59e0b',
                                                          textTransform: 'uppercase' as const
                                                        }}>
                                                          Exception
                                                        </span>
                                                      </div>
                                                      <div style={{ display: 'flex', gap: '12px', fontSize: '11px' }}>
                                                        <div style={{ textAlign: 'right' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>Incidents</div>
                                                          <div style={{ color: 'var(--text-primary)', fontWeight: '600' }}>{exception.incidentCount}</div>
                                                        </div>
                                                        <div style={{ textAlign: 'right' }}>
                                                          <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>Avg Matches</div>
                                                          <div style={{ color: '#3b82f6', fontWeight: '600' }}>{exception.avgMatches.toFixed(1)}</div>
                                                        </div>
                                                      </div>
                                                    </div>
                                                    {/* Exception Content */}
                                                    {isExceptionExpanded && exception.classifiers && exception.classifiers.length > 0 && (
                                                      <div style={{
                                                        marginTop: '8px',
                                                        paddingLeft: '8px'
                                                      }}>
                                                        <div style={{
                                                          padding: '12px',
                                                          background: 'var(--background-secondary)',
                                                          borderRadius: '6px',
                                                          border: '1px solid var(--border)'
                                                        }}>
                                                          <h6 style={{
                                                            fontSize: '12px',
                                                            fontWeight: '600',
                                                            color: 'var(--text-primary)',
                                                            marginBottom: '8px',
                                                            paddingBottom: '6px',
                                                            borderBottom: '1px solid var(--border)'
                                                          }}>
                                                            Exception Classifiers
                                                          </h6>
                                                          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                                            {exception.classifiers.map((classifier, cIdx) => (
                                                              <div key={cIdx} style={{
                                                                padding: '8px',
                                                                background: 'var(--surface)',
                                                                border: '1px solid var(--border)',
                                                                borderRadius: '4px'
                                                              }}>
                                                                <div style={{
                                                                  fontSize: '11px',
                                                                  fontWeight: '600',
                                                                  color: 'var(--text-primary)',
                                                                  marginBottom: '6px'
                                                                }}>
                                                                  {classifier.name}
                                                                </div>
                                                                <div style={{
                                                                  display: 'grid',
                                                                  gridTemplateColumns: 'repeat(5, 1fr)',
                                                                  gap: '8px',
                                                                  fontSize: '10px'
                                                                }}>
                                                                  <div style={{ textAlign: 'center' }}>
                                                                    <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>Incidents</div>
                                                                    <div style={{ color: 'var(--text-primary)', fontWeight: '600' }}>{classifier.incidentCount}</div>
                                                                  </div>
                                                                  <div style={{ textAlign: 'center' }}>
                                                                    <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>Avg Matches</div>
                                                                    <div style={{ color: '#3b82f6', fontWeight: '600' }}>{classifier.avgMatches.toFixed(1)}</div>
                                                                  </div>
                                                                  <div style={{ textAlign: 'center' }}>
                                                                    <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>P25</div>
                                                                    <div style={{ color: '#10b981', fontWeight: '600' }}>{classifier.p25.toFixed(1)}</div>
                                                                  </div>
                                                                  <div style={{ textAlign: 'center' }}>
                                                                    <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>P75</div>
                                                                    <div style={{ color: '#f59e0b', fontWeight: '600' }}>{classifier.p75.toFixed(1)}</div>
                                                                  </div>
                                                                  <div style={{ textAlign: 'center' }}>
                                                                    <div style={{ color: 'var(--text-secondary)', fontSize: '9px', marginBottom: '2px' }}>P90</div>
                                                                    <div style={{ color: '#ef4444', fontWeight: '600' }}>{classifier.p90.toFixed(1)}</div>
                                                                  </div>
                                                                </div>
                                                              </div>
                                                            ))}
                                                          </div>
                                                        </div>
                                                      </div>
                                                    )}
                                                  </div>
                                                )
                                              })}
                                            </div>
                                          </div>
                                        </div>
                                      )}

                                      {/* Recommendations Box - Rule based recommendations */}
                                      {rule.classifiers.length > 0 && (
                                        <div style={{
                                          paddingLeft: '8px',
                                          marginTop: '12px'
                                        }}>
                                          <div style={{
                                            padding: '16px',
                                            background: 'var(--background-secondary)',
                                            borderRadius: '8px',
                                            border: '1px solid var(--border)'
                                          }}>
                                            <h5 style={{
                                              fontSize: '13px',
                                              fontWeight: '600',
                                              color: 'var(--text-primary)',
                                              marginBottom: '12px',
                                              paddingBottom: '8px',
                                              borderBottom: '1px solid var(--border)'
                                            }}>
                                              Exception Recommendations
                                            </h5>
                                            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                              {/* Medium (Audit) Recommendation */}
                                              <div style={{
                                                padding: '12px',
                                                background: 'rgba(245, 158, 11, 0.1)',
                                                border: '1px solid rgba(245, 158, 11, 0.3)',
                                                borderRadius: '6px'
                                              }}>
                                                <div style={{
                                                  display: 'flex',
                                                  alignItems: 'center',
                                                  gap: '8px',
                                                  marginBottom: '8px'
                                                }}>
                                                  <span style={{ width: '10px', height: '10px', borderRadius: '50%', background: '#f59e0b' }} />
                                                  <strong style={{ color: '#f59e0b', fontSize: '13px' }}>Medium (Audit):</strong>
                                                </div>
                                                <div style={{
                                                  fontSize: '12px',
                                                  color: 'var(--text-primary)',
                                                  lineHeight: '1.6',
                                                  paddingLeft: '18px'
                                                }}>
                                                  {rule.classifiers.map((classifier, cIdx) => (
                                                    <span key={cIdx}>
                                                      {classifier.name}: &gt;{classifier.recommendations.medium.threshold.toFixed(0)}
                                                      {cIdx < rule.classifiers.length - 1 ? ' ' : ''}
                                                    </span>
                                                  ))}
                                                </div>
                                              </div>

                                              {/* High (Block) Recommendation */}
                                              <div style={{
                                                padding: '12px',
                                                background: 'rgba(239, 68, 68, 0.1)',
                                                border: '1px solid rgba(239, 68, 68, 0.3)',
                                                borderRadius: '6px'
                                              }}>
                                                <div style={{
                                                  display: 'flex',
                                                  alignItems: 'center',
                                                  gap: '8px',
                                                  marginBottom: '8px'
                                                }}>
                                                  <span style={{ width: '10px', height: '10px', borderRadius: '50%', background: '#ef4444' }} />
                                                  <strong style={{ color: '#ef4444', fontSize: '13px' }}>High (Block):</strong>
                                                </div>
                                                <div style={{
                                                  fontSize: '12px',
                                                  color: 'var(--text-primary)',
                                                  lineHeight: '1.6',
                                                  paddingLeft: '18px'
                                                }}>
                                                  {rule.classifiers.map((classifier, cIdx) => (
                                                    <span key={cIdx}>
                                                      {classifier.name}: &gt;{classifier.recommendations.high.threshold.toFixed(0)}
                                                      {cIdx < rule.classifiers.length - 1 ? ' ' : ''}
                                                    </span>
                                                  ))}
                                                </div>
                                              </div>
                                            </div>
                                          </div>
                                        </div>
                                      )}
                                    </>
                                  )}
                                </div>
                              )
                            })}
                          </div>
                        )}
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          )
        }

      </div>
    </div>
  )
}
