'use client'

import React, { useState, useEffect, useMemo, Suspense, ChangeEvent } from 'react'
import apiClient from '@/lib/axios'
import { format, isWithinInterval, parseISO, startOfDay, endOfDay, subDays } from 'date-fns'
import Pagination from '@/components/ui/Pagination'
import GridExport from '@/components/ui/GridExport'
import {
  RotateCcw,
  ChevronUp,
  ChevronDown,
  ChevronRight,
  Filter,
  X,
  Check,
  Search,
  Info,
  AlertCircle,
  FileText,
  ClipboardList,
  Plus,
  Minus,
  LayoutGrid
} from 'lucide-react'

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

export default function AnalyticsPage() {
  return (
    <Suspense fallback={<div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', color: 'var(--text-secondary)' }}>Loading...</div>}>
      <AnalyticsPageContent />
    </Suspense>
  )
}

function AnalyticsPageContent() {
  const [incidents, setIncidents] = useState<Incident[]>([])
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [totalLoaded, setTotalLoaded] = useState(0)
  const [allDataLoaded, setAllDataLoaded] = useState(false)
  const [currentPage, setCurrentPage] = useState(1)
  const itemsPerPage = 10

  // Heatmap pagination
  const [heatmapTeamPage, setHeatmapTeamPage] = useState(1)
  const teamsPerPage = 8

  // Heatmap domain count (initially show 10, click "Diğer" to load 10 more)
  const [heatmapDomainCount, setHeatmapDomainCount] = useState(10)

  // Heatmap hidden items - click to toggle visibility
  const [hiddenDomains, setHiddenDomains] = useState<Set<string>>(new Set())
  const [hiddenTeams, setHiddenTeams] = useState<Set<string>>(new Set())

  // Table sorting
  const [sortColumn, setSortColumn] = useState<string | null>(null)
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')

  // Column filter dropdowns
  const [openColumnFilter, setOpenColumnFilter] = useState<string | null>(null)
  const [columnFilterSearch, setColumnFilterSearch] = useState<Record<string, string>>({})
  const [columnFilters, setColumnFilters] = useState<Record<string, string[]>>({})

  // Default 1-week date range
  const defaultStart = format(subDays(new Date(), 7), 'yyyy-MM-dd')
  const defaultEnd = format(new Date(), 'yyyy-MM-dd')

  // Filter States (pending - form values, not yet applied)
  const [dateRange, setDateRange] = useState({ start: defaultStart, end: defaultEnd })
  const [dateError, setDateError] = useState('')
  const [selectedDepartments, setSelectedDepartments] = useState<string[]>([])
  const [selectedTeams, setSelectedTeams] = useState<string[]>([])
  const [selectedUser, setSelectedUser] = useState('')
  const [selectedFullName, setSelectedFullName] = useState('')
  const [selectedPolicy, setSelectedPolicy] = useState('')
  const [selectedDomain, setSelectedDomain] = useState('')
  const [selectedActions, setSelectedActions] = useState<string[]>([])

  // Multi-select dropdown open states for heatmap filters
  const [deptDropdownOpen, setDeptDropdownOpen] = useState(false)
  const [teamDropdownOpen2, setTeamDropdownOpen2] = useState(false)
  const [actionDropdownOpen2, setActionDropdownOpen2] = useState(false)

  // Applied filter snapshot - filteredIncidents uses this
  const [appliedFilters, setAppliedFilters] = useState({
    dateRange: { start: defaultStart, end: defaultEnd },
    selectedDepartments: [] as string[],
    selectedTeams: [] as string[],
    selectedUser: '',
    selectedFullName: '',
    selectedPolicy: '',
    selectedDomain: '',
    selectedActions: [] as string[]
  })

  // Filter loading overlay state
  const [filterLoading, setFilterLoading] = useState(false)

  const applyFilters = () => {
    setFilterLoading(true)
    setAppliedFilters({
      dateRange: { ...dateRange },
      selectedDepartments,
      selectedTeams,
      selectedUser,
      selectedFullName,
      selectedPolicy,
      selectedDomain,
      selectedActions
    })
    // Brief delay to show overlay while UI re-renders with new filter results
    setTimeout(() => setFilterLoading(false), 400)
  }

  const resetFilters = () => {
    setDateRange({ start: defaultStart, end: defaultEnd })
    setDateError('')
    setSelectedDepartments([])
    setSelectedTeams([])
    setSelectedUser('')
    setSelectedFullName('')
    setSelectedPolicy('')
    setSelectedDomain('')
    setSelectedActions([])
    setAppliedFilters({
      dateRange: { start: defaultStart, end: defaultEnd },
      selectedDepartments: [],
      selectedTeams: [],
      selectedUser: '',
      selectedFullName: '',
      selectedPolicy: '',
      selectedDomain: '',
      selectedActions: []
    })
  }




  // User Incident Analysis States
  // Exception Recommendation Team Filter
  const [exceptionTeamFilter, setExceptionTeamFilter] = useState<string[]>([])
  const [exceptionTeamDropdownOpen, setExceptionTeamDropdownOpen] = useState(false)
  const [userSearchQuery, setUserSearchQuery] = useState('')
  const [exceptionDomainFilter, setExceptionDomainFilter] = useState('')
  const [exceptionActionFilter, setExceptionActionFilter] = useState<string[]>([])
  const [exceptionChannelFilter, setExceptionChannelFilter] = useState<string[]>([])
  const [exceptionPolicyFilter, setExceptionPolicyFilter] = useState<string[]>([])
  const [exceptionDateRange, setExceptionDateRange] = useState({ start: defaultStart, end: defaultEnd })
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
  }, [])

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
      if (!target.closest('[data-dropdown]') && !target.closest('[data-heatmap-filter]')) {
        setActionDropdownOpen(false)
        setChannelDropdownOpen(false)
        setPolicyDropdownOpen(false)
        setDeptDropdownOpen(false)
        setTeamDropdownOpen2(false)
        setActionDropdownOpen2(false)
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

  const mapIncidentData = (item: any): Incident => {
    const dest = item.destination || ''

    // Birden fazla email adresi varsa (noktalı virgülle ayrılmış), her birinden domain çıkar
    let domain = 'Unknown'
    if (dest.includes(';')) {
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
      department: (() => {
        const dept = item.department || item.user_department
        if (dept && dept.trim().endsWith('Şubesi')) return 'Şube'
        return dept
      })(),
      team: item.team,
      fullName: item.fullName,
      maxMatches: item.maxMatches || 0,
      loginName: item.loginName,
      emailAddress: item.emailAddress,
      violationTriggers: item.violationTriggers || item.violation_triggers || item.ViolationTriggers || undefined,
      channel: item.channel
    }
  }

  const fetchIncidents = async () => {
    setLoading(true)
    setAllDataLoaded(false)
    try {
      // Phase 1: Fast initial load - first 500 records for immediate display
      const initialResponse = await apiClient.get('/api/incidents', {
        params: {
          limit: 500,
          order_by: 'timestamp_desc'
        }
      })

      const initialData = Array.isArray(initialResponse.data) ? initialResponse.data : []
      const initialMapped = initialData.map(mapIncidentData)
      setIncidents(initialMapped)
      setTotalLoaded(initialMapped.length)
      setLoading(false) // UI is now interactive

      // Phase 2: Load remaining data in background
      if (initialMapped.length >= 500) {
        setLoadingMore(true)
        try {
          const fullResponse = await apiClient.get('/api/incidents', {
            params: {
              limit: 1000000000,
              order_by: 'timestamp_desc'
            }
          })

          const fullData = Array.isArray(fullResponse.data) ? fullResponse.data : []
          const fullMapped = fullData.map(mapIncidentData)
          setIncidents(fullMapped)
          setTotalLoaded(fullMapped.length)
        } catch (error) {
          console.error('Error fetching remaining incidents:', error)
        } finally {
          setLoadingMore(false)
          setAllDataLoaded(true)
        }
      } else {
        setAllDataLoaded(true)
      }
    } catch (error) {
      console.error('Error fetching incidents:', error)
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



  // Filter Logic with column filters and sorting - INDEPENDENT from heatmap filters
  const filteredIncidents = useMemo(() => {
    let filtered = incidents.filter(incident => {
      // Only apply table column filters, NOT heatmap appliedFilters

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
  }, [incidents, columnFilters, sortColumn, sortDirection])

  // Heatmap uses only top-level applied filters, NOT table column filters
  const heatmapFilteredIncidents = useMemo(() => {
    return incidents.filter(incident => {
      // Date Filter
      if (appliedFilters.dateRange.start && appliedFilters.dateRange.end) {
        try {
          const incidentDate = parseISO(incident.timestamp)
          const start = startOfDay(parseISO(appliedFilters.dateRange.start))
          const end = endOfDay(parseISO(appliedFilters.dateRange.end))
          if (isNaN(start.getTime()) || isNaN(end.getTime())) return true
          if (!isWithinInterval(incidentDate, { start, end })) return false
        } catch { return true }
      }
      // Department Filter
      if (appliedFilters.selectedDepartments.length > 0 && !appliedFilters.selectedDepartments.includes(incident.department || '')) return false
      // Team Filter
      if (appliedFilters.selectedTeams.length > 0) {
        const normalizedIncidentTeam = normalizeTeamName(incident.team)
        if (!appliedFilters.selectedTeams.some(t => normalizeTeamName(t) === normalizedIncidentTeam)) return false
      }
      // User Filter
      if (appliedFilters.selectedUser) {
        const search = appliedFilters.selectedUser.toLowerCase()
        const match = incident.userEmail?.toLowerCase().includes(search) ||
          incident.loginName?.toLowerCase().includes(search) ||
          incident.emailAddress?.toLowerCase().includes(search)
        if (!match) return false
      }
      // Full Name Filter
      if (appliedFilters.selectedFullName && !incident.fullName?.toLowerCase().includes(appliedFilters.selectedFullName.toLowerCase())) return false
      // Policy Filter
      if (appliedFilters.selectedPolicy && !incident.policy?.toLowerCase().includes(appliedFilters.selectedPolicy.toLowerCase())) return false
      // Domain Filter
      if (appliedFilters.selectedDomain && !incident.domain?.toLowerCase().includes(appliedFilters.selectedDomain.toLowerCase())) return false
      // Action Filter
      if (appliedFilters.selectedActions.length > 0 && !appliedFilters.selectedActions.includes(incident.action || '')) return false
      return true
    })
  }, [incidents, appliedFilters])

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
  const uniqueDepartments = useMemo(() => Array.from(new Set(incidents.map(i => i.department).filter((d): d is string => Boolean(d)))).sort(), [incidents])
  const uniqueTeams = useMemo(() => {
    const normalizedTeams = new Set<string>()
    incidents.forEach(i => {
      if (i.team) {
        normalizedTeams.add(normalizeTeamName(i.team))
      }
    })
    return Array.from(normalizedTeams).sort() as string[]
  }, [incidents])
  const uniqueActions = useMemo(() => Array.from(new Set(incidents.map(i => i.action || 'Permit'))).sort() as string[], [incidents])
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

  // Heatmap Data Calculation (using heatmap filtered incidents - independent from table column filters)
  const heatmapData = useMemo(() => {
    const teams = new Set<string>()
    const domains = new Set<string>()
    const counts: Record<string, Record<string, number>> = {}
    const breakdown: Record<string, Record<string, { block: number, permit: number, authorized: number, quarantine: number, maxMatchTotal: number, incidentCount: number }>> = {}
    const domainTotalCounts: Record<string, number> = {}
    const teamTotalCounts: Record<string, number> = {}

    heatmapFilteredIncidents.forEach(incident => {
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
    // Sort domains by total count descending - top N + "Diğer" (top N dışındaki tüm domainler)
    const allSortedDomains = Array.from(domains).sort((a, b) => (domainTotalCounts[b] || 0) - (domainTotalCounts[a] || 0))
    const topDomains = allSortedDomains.slice(0, heatmapDomainCount)
    const otherDomains = allSortedDomains.slice(heatmapDomainCount)
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

    return { teams: sortedTeams, domains: sortedDomains, counts, breakdown, maxCount, hasMoreDomains: otherDomains.length > 0 }
  }, [heatmapFilteredIncidents, heatmapDomainCount])


  const getHeatmapColor = (count: number, max: number) => {
    if (count === 0) return 'transparent'
    // Tier bazlı renklendirme
    if (count > 100) return 'hsl(210, 90%, 20%)'  // En koyu
    if (count > 75) return 'hsl(210, 90%, 28%)'
    if (count > 50) return 'hsl(210, 90%, 35%)'
    if (count > 30) return 'hsl(210, 90%, 42%)'
    if (count > 20) return 'hsl(210, 90%, 50%)'
    if (count > 15) return 'hsl(210, 90%, 58%)'
    if (count > 10) return 'hsl(210, 90%, 65%)'
    if (count > 5) return 'hsl(210, 90%, 73%)'
    return 'hsl(210, 90%, 82%)'  // 1-5 arası en açık
  }

  const getTextColor = (count: number, max: number) => {
    if (count === 0) return 'var(--text-primary)'
    // Koyu arka planlarda (count > 20) beyaz, açıklarda siyah
    return count > 20 ? '#ffffff' : '#1e293b'
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
    setExceptionDateRange({ start: defaultStart, end: defaultEnd })
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
      // Date filter (independent from other sections)
      if (exceptionDateRange.start && exceptionDateRange.end) {
        const incidentDate = parseISO(incident.timestamp)
        const start = startOfDay(parseISO(exceptionDateRange.start))
        const end = endOfDay(parseISO(exceptionDateRange.end))
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

      // Team filter (multiple selection)
      if (exceptionTeamFilter.length > 0) {
        const normalizedIncidentTeam = incident.team ? incident.team.trim() : '';
        if (!exceptionTeamFilter.includes(normalizedIncidentTeam)) {
          return false;
        }
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
          const p50 = calculatePercentile(data.matches, 50)
          const p70 = calculatePercentile(data.matches, 70)
          const p75 = calculatePercentile(data.matches, 75)
          const p90 = calculatePercentile(data.matches, 90)

          // Calculate recommendations
          // Medium (Audit): P50 - 1
          // High (Block): P90 + 1
          const mediumThreshold = Math.max(p50 - 1, 1)
          const highThreshold = p90 + 1

          return {
            name: classifierName,
            incidentCount: data.incidentIds.size,
            avgMatches,
            p25,
            p50,
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
            const p50 = calculatePercentile(data.matches, 50)
            const p70 = calculatePercentile(data.matches, 70)
            const p75 = calculatePercentile(data.matches, 75)
            const p90 = calculatePercentile(data.matches, 90)

            // Medium (Audit): P50 - 1
            // High (Block): P90 + 1
            const mediumThreshold = Math.max(p50 - 1, 1)
            const highThreshold = p90 + 1

            return {
              name: classifierName,
              incidentCount: data.incidentIds.size,
              avgMatches,
              p25,
              p50,
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

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px', position: 'relative' }}>
      {/* Filter Loading Overlay */}
      {filterLoading && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0, 0, 0, 0.35)',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9999,
          backdropFilter: 'blur(2px)'
        }}>
          <div style={{
            background: 'var(--surface)',
            borderRadius: '12px',
            padding: '32px 48px',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            gap: '16px',
            boxShadow: '0 8px 32px rgba(0,0,0,0.2)'
          }}>
            <div style={{
              width: '40px',
              height: '40px',
              border: '3px solid var(--border)',
              borderTop: '3px solid #3b82f6',
              borderRadius: '50%',
              animation: 'spin 0.8s linear infinite'
            }} />
            <p style={{ color: 'var(--text-primary)', fontSize: '14px', fontWeight: '600', margin: 0 }}>
              Filtreler uygulanıyor...
            </p>
          </div>
        </div>
      )}

      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <h1 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Team Based Analysis</h1>
        </div>

        {/* Full Page Loading Screen */}
        {loading && (
          <div style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: '60vh',
            gap: '24px'
          }}>
            <div style={{
              width: '56px',
              height: '56px',
              border: '4px solid var(--border)',
              borderTop: '4px solid #3b82f6',
              borderRadius: '50%',
              animation: 'spin 0.9s linear infinite'
            }} />
            <div style={{ textAlign: 'center' }}>
              <p style={{ color: 'var(--text-primary)', fontSize: '16px', fontWeight: '600', margin: '0 0 6px 0' }}>
                Veriler yükleniyor...
              </p>
              <p style={{ color: 'var(--text-secondary)', fontSize: '13px', margin: 0 }}>
                Team Based Analysis verileri hazırlanıyor
              </p>
            </div>
          </div>
        )}

        {/* Main Content - Hidden during initial loading */}
        {!loading && (<>

          {/* Heatmap Section */}
          <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', padding: '24px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginBottom: '24px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '12px' }}>Team Based Analysis Heatmap</h2>

            {/* Inline Filters Row */}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', alignItems: 'flex-end', marginBottom: '16px', padding: '12px', background: 'var(--background)', borderRadius: '6px', border: '1px solid var(--border)' }}>
              {/* Date Range - Start */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Start</label>
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
              {/* Date Range - End */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>End</label>
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

              {/* Department - Multi Select */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px', position: 'relative' }} data-heatmap-filter>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Department</label>
                <button
                  onClick={() => { setDeptDropdownOpen(!deptDropdownOpen); setTeamDropdownOpen2(false); setActionDropdownOpen2(false) }}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: selectedDepartments.length > 0 ? '1px solid #3b82f6' : '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', minWidth: '130px', cursor: 'pointer', textAlign: 'left', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '4px' }}
                >
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '100px' }}>{selectedDepartments.length === 0 ? 'All' : `${selectedDepartments.length} selected`}</span>
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="6 9 12 15 18 9"></polyline></svg>
                </button>
                {deptDropdownOpen && (
                  <div style={{ position: 'absolute', top: '100%', left: 0, zIndex: 50, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '6px', boxShadow: '0 4px 12px rgba(0,0,0,0.3)', minWidth: '200px', maxHeight: '250px', overflowY: 'auto', marginTop: '2px' }}>
                    <div style={{ padding: '6px 8px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between' }}>
                      <button onClick={() => { if (selectedDepartments.length === uniqueDepartments.length) { setSelectedDepartments([]) } else { setSelectedDepartments([...uniqueDepartments]) } }} style={{ fontSize: '11px', color: '#3b82f6', background: 'none', border: 'none', cursor: 'pointer', fontWeight: '500' }}>
                        {selectedDepartments.length === uniqueDepartments.length ? 'Deselect All' : 'Select All'}
                      </button>
                    </div>
                    {uniqueDepartments.map(dept => (
                      <label key={dept} style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '6px 10px', fontSize: '12px', cursor: 'pointer', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)' }}>
                        <input type="checkbox" checked={selectedDepartments.includes(dept)} onChange={() => { setSelectedDepartments(prev => prev.includes(dept) ? prev.filter(d => d !== dept) : [...prev, dept]) }} style={{ accentColor: '#3b82f6' }} />
                        {dept}
                      </label>
                    ))}
                  </div>
                )}
              </div>

              {/* Team - Multi Select */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px', position: 'relative' }} data-heatmap-filter>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Team</label>
                <button
                  onClick={() => { setTeamDropdownOpen2(!teamDropdownOpen2); setDeptDropdownOpen(false); setActionDropdownOpen2(false) }}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: selectedTeams.length > 0 ? '1px solid #3b82f6' : '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', minWidth: '130px', cursor: 'pointer', textAlign: 'left', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '4px' }}
                >
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '100px' }}>{selectedTeams.length === 0 ? 'All' : `${selectedTeams.length} selected`}</span>
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="6 9 12 15 18 9"></polyline></svg>
                </button>
                {teamDropdownOpen2 && (
                  <div style={{ position: 'absolute', top: '100%', left: 0, zIndex: 50, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '6px', boxShadow: '0 4px 12px rgba(0,0,0,0.3)', minWidth: '200px', maxHeight: '250px', overflowY: 'auto', marginTop: '2px' }}>
                    <div style={{ padding: '6px 8px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between' }}>
                      <button onClick={() => { if (selectedTeams.length === uniqueTeams.length) { setSelectedTeams([]) } else { setSelectedTeams([...uniqueTeams]) } }} style={{ fontSize: '11px', color: '#3b82f6', background: 'none', border: 'none', cursor: 'pointer', fontWeight: '500' }}>
                        {selectedTeams.length === uniqueTeams.length ? 'Deselect All' : 'Select All'}
                      </button>
                    </div>
                    {uniqueTeams.map(team => (
                      <label key={team} style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '6px 10px', fontSize: '12px', cursor: 'pointer', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)' }}>
                        <input type="checkbox" checked={selectedTeams.includes(team)} onChange={() => { setSelectedTeams(prev => prev.includes(team) ? prev.filter(t => t !== team) : [...prev, team]) }} style={{ accentColor: '#3b82f6' }} />
                        {team}
                      </label>
                    ))}
                  </div>
                )}
              </div>

              {/* User */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>User</label>
                <input
                  type="text"
                  placeholder="User..."
                  value={selectedUser}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedUser(e.target.value)}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '110px' }}
                />
              </div>

              {/* Manager Name */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Manager Name</label>
                <input
                  type="text"
                  placeholder="Name..."
                  value={selectedFullName}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedFullName(e.target.value)}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '110px' }}
                />
              </div>

              {/* Policy */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Policy</label>
                <input
                  type="text"
                  placeholder="Policy..."
                  value={selectedPolicy}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedPolicy(e.target.value)}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '110px' }}
                />
              </div>

              {/* Domain */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Domain</label>
                <input
                  type="text"
                  placeholder="Domain..."
                  value={selectedDomain}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedDomain(e.target.value)}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', width: '110px' }}
                />
              </div>

              {/* Action - Multi Select */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '2px', position: 'relative' }} data-heatmap-filter>
                <label style={{ fontSize: '10px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'none' }}>Action</label>
                <button
                  onClick={() => { setActionDropdownOpen2(!actionDropdownOpen2); setDeptDropdownOpen(false); setTeamDropdownOpen2(false) }}
                  style={{ padding: '5px 8px', borderRadius: '4px', border: selectedActions.length > 0 ? '1px solid #3b82f6' : '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', minWidth: '110px', cursor: 'pointer', textAlign: 'left', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '4px' }}
                >
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '80px' }}>{selectedActions.length === 0 ? 'All' : `${selectedActions.length} selected`}</span>
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="6 9 12 15 18 9"></polyline></svg>
                </button>
                {actionDropdownOpen2 && (
                  <div style={{ position: 'absolute', top: '100%', left: 0, zIndex: 50, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '6px', boxShadow: '0 4px 12px rgba(0,0,0,0.3)', minWidth: '160px', maxHeight: '250px', overflowY: 'auto', marginTop: '2px' }}>
                    <div style={{ padding: '6px 8px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between' }}>
                      <button onClick={() => { if (selectedActions.length === uniqueActions.length) { setSelectedActions([]) } else { setSelectedActions([...uniqueActions]) } }} style={{ fontSize: '11px', color: '#3b82f6', background: 'none', border: 'none', cursor: 'pointer', fontWeight: '500' }}>
                        {selectedActions.length === uniqueActions.length ? 'Deselect All' : 'Select All'}
                      </button>
                    </div>
                    {uniqueActions.map(action => (
                      <label key={action} style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '6px 10px', fontSize: '12px', cursor: 'pointer', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)' }}>
                        <input type="checkbox" checked={selectedActions.includes(action)} onChange={() => { setSelectedActions(prev => prev.includes(action) ? prev.filter(a => a !== action) : [...prev, action]) }} style={{ accentColor: '#3b82f6' }} />
                        {action}
                      </label>
                    ))}
                  </div>
                )}
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

            {/* Heatmap Controls - Hidden items and collapse */}
            {(hiddenDomains.size > 0 || hiddenTeams.size > 0 || heatmapDomainCount > 10) && (
              <div style={{ display: 'flex', gap: '8px', marginBottom: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
                {hiddenDomains.size > 0 && (
                  <button
                    onClick={() => setHiddenDomains(new Set())}
                    style={{
                      padding: '4px 10px',
                      borderRadius: '4px',
                      border: '1px solid #f59e0b',
                      background: 'rgba(245, 158, 11, 0.1)',
                      color: '#f59e0b',
                      fontSize: '11px',
                      fontWeight: '500',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '4px'
                    }}
                  >
                    <RotateCcw size={14} /> {hiddenDomains.size} gizli domain göster
                  </button>
                )}
                {hiddenTeams.size > 0 && (
                  <button
                    onClick={() => setHiddenTeams(new Set())}
                    style={{
                      padding: '4px 10px',
                      borderRadius: '4px',
                      border: '1px solid #8b5cf6',
                      background: 'rgba(139, 92, 246, 0.1)',
                      color: '#8b5cf6',
                      fontSize: '11px',
                      fontWeight: '500',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '4px'
                    }}
                  >
                    <RotateCcw size={14} /> {hiddenTeams.size} gizli team göster
                  </button>
                )}
                {heatmapDomainCount > 10 && (
                  <button
                    onClick={() => setHeatmapDomainCount(10)}
                    style={{
                      padding: '4px 10px',
                      borderRadius: '4px',
                      border: '1px solid #3b82f6',
                      background: 'rgba(59, 130, 246, 0.1)',
                      color: '#3b82f6',
                      fontSize: '11px',
                      fontWeight: '500',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '4px'
                    }}
                  >
                    <ChevronUp size={14} /> Domain listesini daralt (ilk 10)
                  </button>
                )}
              </div>
            )}

            {/* Heatmap Grid - only show when data available */}
            {!loading && heatmapFilteredIncidents.length > 0 && (() => {
              const totalTeamPages = Math.ceil(heatmapData.teams.filter(t => !hiddenTeams.has(t)).length / teamsPerPage)
              const visibleTeams = heatmapData.teams.filter(t => !hiddenTeams.has(t))
              const startIndex = (heatmapTeamPage - 1) * teamsPerPage
              const endIndex = startIndex + teamsPerPage
              const paginatedTeams = visibleTeams.slice(startIndex, endIndex)
              const visibleDomains = heatmapData.domains.filter(d => !hiddenDomains.has(d))
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
                        width: '100px',
                        cursor: 'pointer',
                        transition: 'opacity 0.2s'
                      }} title={`${team} - Gizlemek için tıklayın`}
                        onClick={() => setHiddenTeams(prev => new Set([...Array.from(prev), team]))}>
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
                    {visibleDomains.map((domain, rowIndex) => (
                      <React.Fragment key={domain}>
                        {/* Row Header - Sticky */}
                        <div key={`row-${domain}`} style={{
                          padding: '8px',
                          fontWeight: '600',
                          color: domain === 'Diğer' ? '#3b82f6' : 'var(--text-primary)',
                          fontSize: '12px',
                          background: domain === 'Diğer' ? 'rgba(59, 130, 246, 0.08)' : 'var(--background-secondary)',
                          borderRadius: '2px',
                          display: 'flex',
                          alignItems: 'center',
                          whiteSpace: 'nowrap',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          position: 'sticky',
                          left: 0,
                          zIndex: 5,
                          cursor: 'pointer',
                          gap: '4px',
                          transition: 'opacity 0.2s'
                        }} title={domain === 'Diğer' ? 'Tıklayarak 10 domain daha göster' : `${domain} - Gizlemek için tıklayın`}
                          onClick={() => {
                            if (domain === 'Diğer') {
                              setHeatmapDomainCount(prev => prev + 10)
                            } else {
                              setHiddenDomains(prev => new Set([...Array.from(prev), domain]))
                            }
                          }}
                        >
                          {domain}
                          {domain === 'Diğer' && (
                            <ChevronDown size={12} style={{ marginLeft: '2px' }} />
                          )}
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
                                  ...(rowIndex < 3 ? { top: '100%', marginTop: '4px' } : { bottom: '100%', marginBottom: '4px' }),
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
                                    <span>Released:</span> <span style={{ color: '#10b981', fontWeight: '600' }}>{bd.permit}</span>
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

          {/* Incidents Table */}
          <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginBottom: '32px' }}>
            <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <h2 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>
                  Incidents List
                </h2>
                {Object.values(columnFilters).some(v => v && v.length > 0) && (
                  <button
                    onClick={() => {
                      setColumnFilters({})
                      setColumnFilterSearch({})
                      setOpenColumnFilter(null)
                    }}
                    style={{
                      padding: '6px 12px',
                      borderRadius: '4px',
                      border: '1px solid var(--border)',
                      background: 'var(--background)',
                      color: 'var(--text-secondary)',
                      fontSize: '12px',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '4px'
                    }}
                  >
                    <X size={14} /> Filtreleri Temizle
                  </button>
                )}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                  Showing {filteredIncidents.length > 0 ? (currentPage - 1) * itemsPerPage + 1 : 0} - {Math.min(currentPage * itemsPerPage, filteredIncidents.length)} of {filteredIncidents.length} incidents
                </span>
                <GridExport
                  data={filteredIncidents}
                  fileName="team-based-analysis"
                  columns={[
                    { key: 'timestamp', header: 'Time', formatter: (val) => new Date(val).toLocaleString('tr-TR') },
                    { key: 'userEmail', header: 'User' },
                    { key: 'fullName', header: 'Manager Name' },
                    { key: 'department', header: 'Department' },
                    { key: 'team', header: 'Team' },
                    { key: 'policy', header: 'Policy' },
                    { key: 'domain', header: 'Domain' },
                    { key: 'maxMatches', header: 'Max Matches' },
                    { key: 'action', header: 'Action' }
                  ]}
                />
              </div>
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
                  {(['time', 'user', 'fullName', 'department', 'team', 'policy', 'domain', 'max', 'action'] as const).map(column => {
                    const columnLabels: Record<string, string> = {
                      time: 'Time',
                      user: 'User',
                      fullName: 'Manager Name',
                      department: 'Department',
                      team: 'Team',
                      policy: 'Policy',
                      domain: 'Domain',
                      max: 'Max',
                      action: 'Action'
                    }
                    const isSorted = sortColumn === column

                    return (
                      <th
                        key={column}
                        style={{
                          padding: '12px 16px',
                          textAlign: 'left',
                          fontSize: '13px',
                          fontWeight: '600',
                          color: isSorted ? '#3b82f6' : 'var(--text-primary)',
                          width: column === 'time' ? '150px' : column === 'action' ? '100px' : 'auto',
                          borderBottom: 'none'
                        }}
                      >
                        <span
                          onClick={() => handleSort(column)}
                          style={{
                            cursor: 'pointer',
                            userSelect: 'none',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '4px',
                            width: '100%'
                          }}
                        >
                          {columnLabels[column]}
                          {isSorted && (
                            sortDirection === 'asc' ? <ChevronUp size={12} /> : <ChevronDown size={12} />
                          )}
                        </span>
                      </th>
                    )
                  })}
                </tr>
                {/* Second row for filters */}
                <tr style={{ background: 'var(--background-secondary)', borderBottom: '2px solid var(--border)' }}>
                  {(['time', 'user', 'fullName', 'department', 'team', 'policy', 'domain', 'max', 'action'] as const).map(column => {
                    const uniqueValues = getUniqueColumnValues(column)
                    const searchQuery = columnFilterSearch[column] || ''
                    const filteredValues = uniqueValues.filter(v =>
                      v.toLowerCase().includes(searchQuery.toLowerCase())
                    )
                    const selectedValues = columnFilters[column] || []
                    const hasActiveFilter = selectedValues.length > 0

                    // columns that use multiselect vs text search
                    const isMultiSelect = ['department', 'team', 'action'].includes(column)

                    return (
                      <th
                        key={`filter-${column}`}
                        data-column-filter
                        style={{
                          padding: '0 12px 12px 12px',
                          textAlign: 'left',
                          position: 'relative',
                          verticalAlign: 'top'
                        }}
                      >
                        {column !== 'time' && column !== 'max' ? (
                          isMultiSelect ? (
                            <>
                              <button
                                onClick={(e) => {
                                  e.stopPropagation()
                                  toggleColumnFilter(column)
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
                                  {hasActiveFilter ? `${selectedValues.length} selected` : 'Tümü'}
                                </span>
                                <ChevronDown size={12} />
                              </button>

                              {openColumnFilter === column && (
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
                                    maxHeight: '300px',
                                    overflowY: 'auto',
                                    padding: '8px'
                                  }}
                                  onClick={(e) => e.stopPropagation()}
                                >
                                  <input
                                    type="text"
                                    placeholder="Ara..."
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
                                      marginBottom: '8px',
                                      boxSizing: 'border-box'
                                    }}
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
                                    {selectedValues.length === filteredValues.length ? '✓ Seçimi Kaldır' : 'Tümünü Seç'}
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
                                    >
                                      <input
                                        type="checkbox"
                                        checked={selectedValues.includes(value)}
                                        onChange={() => toggleColumnFilterValue(column, value)}
                                        style={{ marginRight: '8px' }}
                                      />
                                      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{value}</span>
                                    </label>
                                  ))}
                                  {filteredValues.length === 0 && (
                                    <div style={{ padding: '8px', fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center' }}>
                                      Bulunamadı
                                    </div>
                                  )}
                                </div>
                              )}
                            </>
                          ) : (
                            <input
                              type="text"
                              placeholder="Ara..."
                              value={hasActiveFilter ? selectedValues[0] : ''}
                              onChange={(e) => {
                                const val = e.target.value
                                setColumnFilters(prev => ({ ...prev, [column]: val ? [val] : [] }))
                              }}
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
                          )
                        ) : null}
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

          {/* User Incident Report Section */}
          {!loading && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', padding: '24px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginTop: '24px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '20px' }}>Exception Recommendation</h2>

              <div style={{ marginBottom: '20px', display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 1fr 1fr', gap: '16px' }}>
                <div style={{ position: 'relative' }} data-dropdown>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Filter by Team
                  </label>
                  <div
                    onClick={() => {
                      setExceptionTeamDropdownOpen(!exceptionTeamDropdownOpen)
                      setActionDropdownOpen(false)
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
                      {exceptionTeamFilter.length === 0
                        ? 'All Teams'
                        : exceptionTeamFilter.length === 1
                          ? exceptionTeamFilter[0]
                          : `${exceptionTeamFilter.length} selected`}
                    </span>
                    <span style={{ marginLeft: '8px' }}>{exceptionTeamDropdownOpen ? <ChevronUp size={14} /> : <ChevronDown size={14} />}</span>
                  </div>
                  {exceptionTeamDropdownOpen && (
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
                          if (exceptionTeamFilter.length === uniqueTeams.length) {
                            setExceptionTeamFilter([])
                          } else {
                            setExceptionTeamFilter([...uniqueTeams])
                          }
                        }}
                        style={{
                          padding: '8px 12px',
                          borderBottom: '1px solid var(--border)',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '600',
                          color: 'var(--text-primary)',
                          background: exceptionTeamFilter.length === uniqueTeams.length ? 'var(--surface-hover)' : 'transparent'
                        }}
                      >
                        {exceptionTeamFilter.length === uniqueTeams.length ? <><Check size={12} /> Deselect All</> : 'Select All'}
                      </div>
                      {uniqueTeams.map(team => (
                        <label
                          key={team}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            padding: '8px 12px',
                            cursor: 'pointer',
                            fontSize: '13px',
                            color: 'var(--text-primary)',
                            borderBottom: '1px solid var(--border)',
                            background: exceptionTeamFilter.includes(team) ? 'var(--surface-hover)' : 'transparent'
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <input
                            type="checkbox"
                            checked={exceptionTeamFilter.includes(team)}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setExceptionTeamFilter([...exceptionTeamFilter, team])
                              } else {
                                setExceptionTeamFilter(exceptionTeamFilter.filter(t => t !== team))
                              }
                            }}
                            style={{ marginRight: '8px', cursor: 'pointer' }}
                          />
                          <span>{team}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '8px', display: 'block' }}>
                    Date Range
                  </label>
                  <div style={{ display: 'flex', gap: '4px' }}>
                    <input
                      type="date"
                      value={exceptionDateRange.start}
                      onChange={(e) => setExceptionDateRange(prev => ({ ...prev, start: e.target.value }))}
                      style={{
                        flex: 1,
                        padding: '9px 6px',
                        borderRadius: '6px',
                        border: '1px solid var(--border)',
                        background: 'var(--background)',
                        color: 'var(--text-primary)',
                        fontSize: '12px'
                      }}
                    />
                    <input
                      type="date"
                      value={exceptionDateRange.end}
                      onChange={(e) => setExceptionDateRange(prev => ({ ...prev, end: e.target.value }))}
                      style={{
                        flex: 1,
                        padding: '9px 6px',
                        borderRadius: '6px',
                        border: '1px solid var(--border)',
                        background: 'var(--background)',
                        color: 'var(--text-primary)',
                        fontSize: '12px'
                      }}
                    />
                  </div>
                </div>
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
                    <span style={{ marginLeft: '8px' }}>{actionDropdownOpen ? <ChevronUp size={14} /> : <ChevronDown size={14} />}</span>
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
                        {exceptionActionFilter.length === uniqueActions.length ? <><Check size={12} /> Deselect All</> : 'Select All'}
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
                    <span style={{ marginLeft: '8px' }}>{channelDropdownOpen ? <ChevronUp size={14} /> : <ChevronDown size={14} />}</span>
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
                        {exceptionChannelFilter.length === uniqueChannels.length ? <><Check size={12} /> Deselect All</> : 'Select All'}
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
                    <span style={{ marginLeft: '8px' }}>{policyDropdownOpen ? <ChevronUp size={14} /> : <ChevronDown size={14} />}</span>
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
                        {exceptionPolicyFilter.length === uniquePolicies.length ? <><Check size={12} /> Deselect All</> : 'Select All'}
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

              {/* Not Approved - Incident count <= 5 */}
              {userIncidents.length > 0 && userIncidents.length <= 5 && (
                <div style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '12px',
                  marginBottom: '24px',
                  padding: '20px',
                  background: 'rgba(239, 68, 68, 0.08)',
                  borderRadius: '8px',
                  border: '1px solid rgba(239, 68, 68, 0.3)'
                }}>
                  <div style={{
                    width: '40px',
                    height: '40px',
                    borderRadius: '50%',
                    background: 'rgba(239, 68, 68, 0.15)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    flexShrink: 0
                  }}>
                    <X size={24} color="#ef4444" />
                  </div>
                  <div>
                    <div style={{ fontSize: '16px', fontWeight: '600', color: '#ef4444', marginBottom: '4px' }}>
                      İstisna Uygun Görülmemiştir
                    </div>
                    <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                      Filtreleme sonucu bulunan incident sayısı ({userIncidents.length}) 5 ve altında olduğu için istisna uygun görülmemiştir.
                    </div>
                  </div>
                </div>
              )}

              {/* Summary Statistics */}
              {userIncidents.length > 5 && userReportData.length > 0 && (
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
                        top: '100%',
                        left: 0,
                        marginTop: '8px',
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
                        top: '100%',
                        left: 0,
                        marginTop: '8px',
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
              ) : userIncidents.length <= 5 ? (
                null
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
                              {isPolicyExpanded ? <Minus size={16} /> : <Plus size={16} />}
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
                                        {isRuleExpanded ? <Minus size={14} /> : <Plus size={14} />}
                                      </div>
                                      <span style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)' }}>
                                        <ClipboardList size={16} style={{ marginRight: '4px' }} /> {rule.name}
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
                                                          {isClassifierExpanded ? <Minus size={12} /> : <Plus size={12} />}
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
                                                          textTransform: 'none' as const
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
          )}

        </>)}

      </div>
    </div>
  )
}
