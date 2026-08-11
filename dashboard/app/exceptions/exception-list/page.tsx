'use client'

import React, { useState, useEffect, useMemo, Suspense } from 'react'
import apiClient from '@/lib/axios'
import { useTranslation } from '@/components/LanguageProvider'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import { format, parseISO, differenceInDays, subDays } from 'date-fns'
import {
    ChevronDown,
    ChevronRight,
    Search,
    X,
    Check,
    AlertTriangle,
    Shield,
    Activity,
    Clock,
    Trash2,
    RefreshCw,
    PowerOff,
    ListChecks,
    ChevronUp,
    Filter,
    Info,
} from 'lucide-react'

// ─── Types ─────────────────────────────────────────────────────────────────────

interface ExceptionData {
    policyName: string
    rules: {
        ruleName: string
        exceptions: ExceptionItem[]
    }[]
}

interface ExceptionItem {
    exceptionName: string
    enabled?: string | null
}

interface ExceptionStats {
    incidentCount: number
    lastIncidentDate: string | null
    daysIdle: number | null
    isStale: boolean
}

interface BackendExceptionStat {
    policy_name: string
    rule_name: string
    incident_count: number
    last_incident_date: string | null
}

interface FilteredExceptionRef {
    policyName: string
    ruleName: string
    exceptionName: string
    enabled?: string | null
}

const getExceptionName = (exception: ExceptionItem | string) =>
    typeof exception === 'string' ? exception : exception.exceptionName

const getExceptionEnabled = (exception: ExceptionItem | FilteredExceptionRef | string) => {
    if (typeof exception === 'string') return 'true'
    return exception.enabled ?? 'true'
}

const isExceptionEnabled = (exception: ExceptionItem | FilteredExceptionRef | string) =>
    String(getExceptionEnabled(exception)).toLowerCase() !== 'false'

const normalizeExceptionItem = (exception: any): ExceptionItem => {
    if (typeof exception === 'string') {
        return { exceptionName: exception, enabled: 'true' }
    }

    const enabledValue = exception?.enabled ?? exception?.is_enabled ?? exception?.isEnabled ?? 'true'
    return {
        exceptionName: exception?.exception_name || exception?.exceptionName || exception?.rule_name || exception?.ruleName || exception?.name || '',
        enabled: typeof enabledValue === 'boolean' ? String(enabledValue) : enabledValue
    }
}

const parseUtcDate = (value: string) => {
    const hasTimezone = /([zZ]|[+-]\d{2}:?\d{2}(?::?\d{2})?)$/.test(value)
    return new Date(hasTimezone ? value : `${value}Z`)
}

const formatIstanbulDateTime = (value: string) =>
    parseUtcDate(value).toLocaleString('tr-TR', {
        timeZone: 'Europe/Istanbul',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
    })

const formatIstanbulDate = (value: string) =>
    parseUtcDate(value).toLocaleDateString('tr-TR', {
        timeZone: 'Europe/Istanbul',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    })

// ─── SearchableMultiSelect (reused pattern from analytics page) ────────────────

interface SearchableMultiSelectProps {
    label: string
    options: string[]
    selectedValues: string[]
    onChange: (values: string[]) => void
    placeholder?: string
}

function SearchableMultiSelect({ label, options, selectedValues, onChange, placeholder }: SearchableMultiSelectProps) {
    const { t } = useTranslation()
    const [isOpen, setIsOpen] = useState(false)
    const [searchQuery, setSearchQuery] = useState('')
    const dropdownRef = React.useRef<HTMLDivElement>(null)

    useEffect(() => {
        const handleClickOutside = (event: Event) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
                setIsOpen(false)
                setSearchQuery('')
            }
        }
        document.addEventListener('mousedown', handleClickOutside)
        return () => document.removeEventListener('mousedown', handleClickOutside)
    }, [])

    const filteredOptions = options.filter(opt =>
        opt && opt.toLowerCase().includes(searchQuery.toLowerCase())
    )

    const toggleValue = (value: string) => {
        if (selectedValues.includes(value)) {
            onChange(selectedValues.filter(v => v !== value))
        } else {
            onChange([...selectedValues, value])
        }
    }

    const toggleAll = () => {
        if (selectedValues.length === filteredOptions.length && filteredOptions.length > 0) {
            onChange([])
        } else {
            onChange([...filteredOptions])
        }
    }

    const displayText = selectedValues.length === 0
        ? (placeholder || t('exceptionsList.statusAll'))
        : selectedValues.length === 1
            ? selectedValues[0]
            : `${selectedValues.length} ${t('common.selected')}`

    return (
        <div ref={dropdownRef} style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <label style={{
                fontSize: '11px',
                fontWeight: '600',
                color: 'var(--text-secondary)',
                letterSpacing: '0.3px'
            }}>
                {label}
            </label>
            <button
                onClick={() => {
                    setIsOpen(!isOpen)
                    if (!isOpen) setSearchQuery('')
                }}
                style={{
                    width: '100%',
                    padding: '8px 12px',
                    borderRadius: '8px',
                    border: selectedValues.length > 0 ? '1.5px solid #3b82f6' : '1px solid var(--border)',
                    background: selectedValues.length > 0 ? 'rgba(59, 130, 246, 0.04)' : 'var(--surface)',
                    color: 'var(--text-primary)',
                    fontSize: '13px',
                    cursor: 'pointer',
                    textAlign: 'left',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: '6px',
                    transition: 'all 0.2s ease',
                    minHeight: '20px',
                    boxShadow: isOpen ? '0 0 0 3px rgba(59, 130, 246, 0.12)' : 'none',
                    fontFamily: 'Inter, sans-serif',
                }}
            >
                <span style={{
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                    flex: 1,
                    color: selectedValues.length > 0 ? '#3b82f6' : 'var(--text-primary)'
                }}>
                    {displayText}
                </span>
                <div style={{ display: 'flex', alignItems: 'center', gap: '4px', flexShrink: 0 }}>
                    {selectedValues.length > 0 && (
                        <span style={{
                            background: 'linear-gradient(135deg, #3b82f6, #2563eb)',
                            color: '#fff',
                            borderRadius: '10px',
                            padding: '1px 6px',
                            fontSize: '10px',
                            fontWeight: '700',
                            minWidth: '18px',
                            textAlign: 'center',
                            lineHeight: '16px'
                        }}>
                            {selectedValues.length}
                        </span>
                    )}
                    <ChevronDown size={14} style={{
                        transform: isOpen ? 'rotate(180deg)' : 'rotate(0deg)',
                        transition: 'transform 0.2s ease',
                        color: 'var(--text-secondary)'
                    }} />
                </div>
            </button>

            {isOpen && (
                <div style={{
                    position: 'absolute',
                    top: '100%',
                    left: 0,
                    right: 0,
                    zIndex: 100,
                    background: 'var(--surface)',
                    border: '1px solid var(--border)',
                    borderRadius: '10px',
                    boxShadow: '0 12px 28px rgba(0,0,0,0.18), 0 4px 10px rgba(0,0,0,0.08)',
                    minWidth: '100%',
                    maxHeight: '340px',
                    marginTop: '4px',
                    overflow: 'hidden'
                }}>
                    <div style={{
                        padding: '10px 10px 8px',
                        borderBottom: '1px solid var(--border)',
                        position: 'sticky',
                        top: 0,
                        background: 'var(--surface)',
                        zIndex: 1
                    }}>
                        <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                            <Search size={14} style={{
                                position: 'absolute',
                                left: '10px',
                                color: 'var(--text-secondary)',
                                pointerEvents: 'none'
                            }} />
                            <input
                                type="text"
                                placeholder={`${t('common.search')}...`}
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                autoFocus
                                style={{
                                    width: '100%',
                                    padding: '8px 32px 8px 32px',
                                    borderRadius: '6px',
                                    border: '1px solid var(--border)',
                                    background: 'var(--background)',
                                    color: 'var(--text-primary)',
                                    fontSize: '12px',
                                    outline: 'none',
                                    boxSizing: 'border-box',
                                    transition: 'border-color 0.2s',
                                    fontFamily: 'Inter, sans-serif',
                                }}
                                onFocus={(e) => e.currentTarget.style.borderColor = '#3b82f6'}
                                onBlur={(e) => e.currentTarget.style.borderColor = 'var(--border)'}
                                onClick={(e) => e.stopPropagation()}
                            />
                            {searchQuery && (
                                <X size={14}
                                    style={{
                                        position: 'absolute',
                                        right: '10px',
                                        color: 'var(--text-secondary)',
                                        cursor: 'pointer'
                                    }}
                                    onClick={(e) => { e.stopPropagation(); setSearchQuery('') }}
                                />
                            )}
                        </div>
                    </div>

                    <div
                        onClick={(e) => { e.stopPropagation(); toggleAll() }}
                        style={{
                            padding: '8px 12px',
                            borderBottom: '1px solid var(--border)',
                            cursor: 'pointer',
                            fontSize: '11px',
                            fontWeight: '600',
                            color: '#3b82f6',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            transition: 'background 0.15s',
                            background: 'transparent'
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(59, 130, 246, 0.05)')}
                        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                    >
                        {selectedValues.length === filteredOptions.length && filteredOptions.length > 0 ? (
                            <><Check size={12} /> {t('common.clearSelection')}</>
                        ) : (
                            <>{t('common.selectAll')} ({filteredOptions.length})</>
                        )}
                    </div>

                    <div style={{ maxHeight: '240px', overflowY: 'auto' }}>
                        {filteredOptions.map(option => {
                            const isSelected = selectedValues.includes(option)
                            return (
                                <label
                                    key={option}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '10px',
                                        padding: '7px 12px',
                                        cursor: 'pointer',
                                        fontSize: '12px',
                                        color: 'var(--text-primary)',
                                        borderBottom: '1px solid var(--border)',
                                        background: isSelected ? 'rgba(59, 130, 246, 0.06)' : 'transparent',
                                        transition: 'background 0.12s'
                                    }}
                                    onClick={(e) => e.stopPropagation()}
                                    onMouseEnter={(e) => {
                                        if (!isSelected) e.currentTarget.style.background = 'var(--surface-hover)'
                                    }}
                                    onMouseLeave={(e) => {
                                        e.currentTarget.style.background = isSelected ? 'rgba(59, 130, 246, 0.06)' : 'transparent'
                                    }}
                                >
                                    <div style={{
                                        width: '16px',
                                        height: '16px',
                                        borderRadius: '4px',
                                        border: isSelected ? '1.5px solid #3b82f6' : '1.5px solid var(--border)',
                                        background: isSelected ? '#3b82f6' : 'transparent',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        transition: 'all 0.15s',
                                        flexShrink: 0
                                    }}>
                                        {isSelected && <Check size={10} color="#fff" strokeWidth={3} />}
                                    </div>
                                    <input
                                        type="checkbox"
                                        checked={isSelected}
                                        onChange={() => toggleValue(option)}
                                        style={{ display: 'none' }}
                                    />
                                    <span style={{
                                        overflow: 'hidden',
                                        textOverflow: 'ellipsis',
                                        whiteSpace: 'nowrap',
                                        lineHeight: '1.4'
                                    }}>
                                        {option}
                                    </span>
                                </label>
                            )
                        })}
                        {filteredOptions.length === 0 && (
                            <div style={{
                                padding: '20px',
                                fontSize: '12px',
                                color: 'var(--text-secondary)',
                                textAlign: 'center',
                                fontStyle: 'italic'
                            }}>
                                {t('common.noResults')}
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    )
}

// ─── Main Page ─────────────────────────────────────────────────────────────────

export default function ExceptionListPage() {
    const { t } = useTranslation()
    return (
        <Suspense fallback={<div style={{ minHeight: '100vh', background: 'var(--background)', position: 'relative' }}><LoadingOverlay isLoading={true} message={t('exceptionsList.loading')} /></div>}>
            <ExceptionListContent />
        </Suspense>
    )
}

const STALE_DAYS_THRESHOLD = 90

function ExceptionListContent() {
    const { t } = useTranslation()

    // Data states
    const [exceptionData, setExceptionData] = useState<ExceptionData[]>([])
    const [backendStats, setBackendStats] = useState<BackendExceptionStat[]>([])
    const [lastSyncedAt, setLastSyncedAt] = useState<string | null>(null)
    const [totalExceptionsCount, setTotalExceptionsCount] = useState(0)
    const [loading, setLoading] = useState(true)
    const [syncing, setSyncing] = useState(false)
    const [bulkDisabling, setBulkDisabling] = useState(false)
    const [disablingExceptionKey, setDisablingExceptionKey] = useState<string | null>(null)
    const [forcepointMessage, setForcepointMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

    // Filter states
    const defaultStart = format(subDays(new Date(), 365), 'yyyy-MM-dd')
    const defaultEnd = format(new Date(), 'yyyy-MM-dd')
    const [dateRange, setDateRange] = useState({ start: defaultStart, end: defaultEnd })
    const [selectedPolicies, setSelectedPolicies] = useState<string[]>([])
    const [selectedRules, setSelectedRules] = useState<string[]>([])
    const [exceptionSearch, setExceptionSearch] = useState('')

    // Tree states
    const [expandedPolicies, setExpandedPolicies] = useState<Set<number>>(new Set())
    const [expandedRules, setExpandedRules] = useState<Set<string>>(new Set())
    const [allExpanded, setAllExpanded] = useState(false)

    // ─── Data Fetching ─────────────────────────────────────────────────────────

    useEffect(() => {
        fetchData()
    }, [dateRange.start, dateRange.end])

    const [apiError, setApiError] = useState<string | null>(null)

    const fetchData = async () => {
        setLoading(true)
        setApiError(null)
        try {
            // Fetch exceptions and incidents independently — one failure should not block the other
            const exceptionsPromise = apiClient.get('/api/policy-exceptions', { timeout: 30000 })
                .then(res => {
                    console.log('--- POLICY EXCEPTIONS RAW RES ---', res.data)
                    // Bazen data doğrudan dizi dönebilir, bazen {success: true, data: [...]} dönebilir.
                    // C# backend'ine göre { success: true, data: [...] } formatında gelmesi lazım.
                    if (res.data?.success) {
                        const rawData = res.data.data || []
                        console.log('--- POLICY EXCEPTIONS SUCCESS DATA ---', rawData)

                        // Map snake_case from backend to camelCase for frontend
                        const mappedData = rawData.map((p: any) => ({
                            policyName: p.policy_name || p.policyName,
                            rules: (p.rules || []).map((r: any) => ({
                                ruleName: r.rule_name || r.ruleName,
                                exceptions: (r.exceptions || []).map(normalizeExceptionItem).filter((e: ExceptionItem) => e.exceptionName)
                            }))
                        }))

                        setExceptionData(mappedData)
                        setLastSyncedAt(res.data.last_synced_at || res.data.lastSyncedAt || null)
                        setTotalExceptionsCount(res.data.total_exceptions || res.data.totalExceptions || 0)
                    } else if (Array.isArray(res.data)) {
                        console.log('--- POLICY EXCEPTIONS DIRECT ARRAY ---', res.data)

                        // Map snake_case from backend to camelCase for frontend
                        const mappedData = res.data.map((p: any) => ({
                            policyName: p.policy_name || p.policyName,
                            rules: (p.rules || []).map((r: any) => ({
                                ruleName: r.rule_name || r.ruleName,
                                exceptions: (r.exceptions || []).map(normalizeExceptionItem).filter((e: ExceptionItem) => e.exceptionName)
                            }))
                        }))

                        setExceptionData(mappedData)
                        setTotalExceptionsCount(res.data.length || 0)
                    } else if (res.data?.error) {
                        setApiError(`Backend Error: ${res.data.error}`)
                    } else {
                        console.warn('--- UNEXPECTED POLICY EXCEPTIONS FORMAT ---', res.data)
                        setApiError(`${t('exceptionsList.unexpectedDataFormat')}: ${JSON.stringify(res.data).substring(0, 100)}...`)
                    }
                })
                .catch(err => {
                    console.error('Error fetching exceptions:', err)
                    setApiError(err.response?.data?.error || err.message || 'Unknown network error')
                })

            const incidentsPromise = apiClient.get('/api/incidents/exception-stats', {
                params: {
                    startDate: dateRange.start || undefined,
                    endDate: dateRange.end || undefined
                },
                timeout: 120000
            })
                .then(res => {
                    const statsArr = Array.isArray(res.data) ? res.data : []
                    setBackendStats(statsArr.map((item: any) => ({
                        policy_name: item.policy_name || item.policyName || '',
                        rule_name: item.rule_name || item.ruleName || '',
                        incident_count: item.incident_count || item.incidentCount || 0,
                        last_incident_date: item.last_incident_date || item.lastIncidentDate || null,
                    })))
                })
                .catch(err => {
                    console.error('Error fetching exception stats:', err)
                    setApiError(err.response?.data?.detail || err.message || 'Exception usage stats could not be loaded')
                    setBackendStats([])
                })

            await Promise.all([exceptionsPromise, incidentsPromise])
        } catch (error) {
            console.error('Error fetching data:', error)
        } finally {
            setLoading(false)
        }
    }

    const handleSync = async () => {
        setSyncing(true)
        try {
            const res = await apiClient.post('/api/policy-exceptions/sync')
            const syncedAt = res.data?.last_synced_at || res.data?.lastSyncedAt || res.data?.synced_at || res.data?.syncedAt
            if (syncedAt) {
                setLastSyncedAt(syncedAt)
            }
            await fetchData()
        } catch (error) {
            console.error('Error syncing:', error)
        } finally {
            setSyncing(false)
        }
    }

    const handleBulkDisableFiltered = async () => {
        const activeExceptionRefs = filteredExceptionRefs.filter(isExceptionEnabled)
        if (activeExceptionRefs.length === 0) return

        const confirmed = window.confirm(
            `Ekranda filtrelenen ${activeExceptionRefs.length} aktif exception Forcepoint uzerinde pasif edilecek.\n\n` +
            `Bu islem Forcepoint basarili donerse lokal envanter kaydini da gunceller. Devam etmek istiyor musunuz?`
        )
        if (!confirmed) return

        setBulkDisabling(true)
        setForcepointMessage(null)
        try {
            const res = await apiClient.post('/api/policy-exceptions/forcepoint-enabled/bulk', {
                exceptions: activeExceptionRefs,
                enabled: false
            })
            const success = Boolean(res.data?.success)
            setForcepointMessage({
                type: success ? 'success' : 'error',
                text: res.data?.message || `${activeExceptionRefs.length} exception icin toplu kapatma tamamlandi.`
            })
            await fetchData()
        } catch (error: any) {
            setForcepointMessage({
                type: 'error',
                text: error?.response?.data?.message || error?.response?.data?.error || error?.message || 'Toplu Forcepoint exception kapatma basarisiz.'
            })
        } finally {
            setBulkDisabling(false)
        }
    }

    // ─── Compute exception stats from incidents ────────────────────────────────

    const getExceptionRefKey = (ref: FilteredExceptionRef) =>
        `${ref.policyName.toLowerCase()}|${ref.ruleName.toLowerCase()}|${ref.exceptionName.toLowerCase()}`

    const handleDisableSingleException = async (ref: FilteredExceptionRef) => {
        const key = getExceptionRefKey(ref)
        const confirmed = window.confirm(
            `"${ref.exceptionName}" exception kaydi Forcepoint uzerinde pasif edilecek.\n\n` +
            `Policy: ${ref.policyName}\nRule: ${ref.ruleName}\n\nDevam etmek istiyor musunuz?`
        )
        if (!confirmed) return

        setDisablingExceptionKey(key)
        setForcepointMessage(null)
        try {
            const res = await apiClient.post('/api/policy-exceptions/forcepoint-enabled/bulk', {
                exceptions: [ref],
                enabled: false
            })
            const success = Boolean(res.data?.success)
            setForcepointMessage({
                type: success ? 'success' : 'error',
                text: res.data?.message || `"${ref.exceptionName}" exception icin kapatma tamamlandi.`
            })
            await fetchData()
        } catch (error: any) {
            setForcepointMessage({
                type: 'error',
                text: error?.response?.data?.message || error?.response?.data?.error || error?.message || 'Forcepoint exception kapatma basarisiz.'
            })
        } finally {
            setDisablingExceptionKey(null)
        }
    }

    const exceptionStatsMap = useMemo(() => {
        const map = new Map<string, ExceptionStats>()
        const now = new Date()

        // Build a lookup index from backend stats: "policyName|ruleName" (lowercased) → stats
        const statsIndex = new Map<string, { count: number; lastDate: string | null }>()
        for (const stat of backendStats) {
            const key = `${(stat.policy_name || '').toLowerCase()}|${(stat.rule_name || '').toLowerCase()}`
            statsIndex.set(key, {
                count: stat.incident_count,
                lastDate: stat.last_incident_date
            })
        }

        // For each exception, look up in the backend stats index
        exceptionData.forEach(policy => {
            policy.rules.forEach(rule => {
                rule.exceptions.forEach(exception => {
                    const excName = getExceptionName(exception)
                    const key = `${policy.policyName}|${rule.ruleName}|${excName}`
                    const lookupKey = `${policy.policyName.toLowerCase()}|${excName.toLowerCase()}`
                    const stat = statsIndex.get(lookupKey)

                    const incidentCount = stat?.count || 0
                    let lastIncidentDate: string | null = stat?.lastDate || null
                    let daysIdle: number | null = null

                    if (incidentCount > 0 && lastIncidentDate) {
                        daysIdle = differenceInDays(now, parseISO(lastIncidentDate))
                    } else {
                        daysIdle = 999 // no incidents at all
                    }

                    map.set(key, {
                        incidentCount,
                        lastIncidentDate,
                        daysIdle,
                        isStale: daysIdle !== null && daysIdle >= STALE_DAYS_THRESHOLD
                    })
                })
            })
        })

        return map
    }, [exceptionData, backendStats])

    // ─── Filtered & computed data ──────────────────────────────────────────────

    const filteredData = useMemo(() => {
        return exceptionData
            .filter(policy => {
                if (selectedPolicies.length > 0 && !selectedPolicies.includes(policy.policyName)) return false
                return true
            })
            .map(policy => ({
                ...policy,
                rules: policy.rules
                    .filter(rule => {
                        if (selectedRules.length > 0 && !selectedRules.includes(rule.ruleName)) return false
                        return true
                    })
                    .map(rule => ({
                        ...rule,
                        exceptions: rule.exceptions.filter(exc => {
                            const exceptionName = getExceptionName(exc)
                            if (exceptionSearch) {
                                return exceptionName.toLowerCase().includes(exceptionSearch.toLowerCase())
                            }
                            return true
                        })
                    }))
                    .filter(rule => rule.exceptions.length > 0)
            }))
            .filter(policy => policy.rules.length > 0)
    }, [exceptionData, selectedPolicies, selectedRules, exceptionSearch])

    const filteredExceptionRefs = useMemo(() => {
        const unique = new Map<string, FilteredExceptionRef>()

        filteredData.forEach(policy => {
            policy.rules.forEach(rule => {
                rule.exceptions.forEach(exception => {
                    const exceptionName = getExceptionName(exception)
                    const key = `${policy.policyName.toLowerCase()}|${rule.ruleName.toLowerCase()}|${exceptionName.toLowerCase()}`
                    if (!unique.has(key)) {
                        unique.set(key, {
                            policyName: policy.policyName,
                            ruleName: rule.ruleName,
                            exceptionName,
                            enabled: getExceptionEnabled(exception)
                        })
                    }
                })
            })
        })

        return Array.from(unique.values())
    }, [filteredData])

    const activeFilteredExceptionRefs = useMemo(
        () => filteredExceptionRefs.filter(isExceptionEnabled),
        [filteredExceptionRefs]
    )

    // ─── Summary computations ─────────────────────────────────────────────────

    const summaryStats = useMemo(() => {
        let staleCount = 0
        let mostActiveException = ''
        let mostActiveCount = 0
        let totalMappedIncidents = 0

        exceptionStatsMap.forEach((stats, key) => {
            if (stats.isStale) staleCount++
            totalMappedIncidents += stats.incidentCount
            if (stats.incidentCount > mostActiveCount) {
                mostActiveCount = stats.incidentCount
                const parts = key.split('|')
                mostActiveException = parts[2] || key
            }
        })

        return { staleCount, mostActiveException, mostActiveCount, totalMappedIncidents }
    }, [exceptionStatsMap])

    // ─── Unique lists for dropdowns ───────────────────────────────────────────

    const allPolicies = useMemo(() =>
        exceptionData.map(p => p.policyName).filter(Boolean).sort(),
        [exceptionData]
    )

    const allRules = useMemo(() => {
        const rules = new Set<string>()
        const relevantPolicies = selectedPolicies.length > 0
            ? exceptionData.filter(p => selectedPolicies.includes(p.policyName))
            : exceptionData
        relevantPolicies.forEach(p => p.rules.forEach(r => { if (r.ruleName) rules.add(r.ruleName) }))
        return Array.from(rules).sort()
    }, [exceptionData, selectedPolicies])

    // ─── Tree toggle helpers ──────────────────────────────────────────────────

    const togglePolicy = (idx: number) => {
        setExpandedPolicies(prev => {
            const next = new Set(prev)
            if (next.has(idx)) next.delete(idx); else next.add(idx)
            return next
        })
    }

    const toggleRule = (pIdx: number, rIdx: number) => {
        const key = `${pIdx}-${rIdx}`
        setExpandedRules(prev => {
            const next = new Set(prev)
            if (next.has(key)) next.delete(key); else next.add(key)
            return next
        })
    }

    const toggleExpandAll = () => {
        if (allExpanded) {
            setExpandedPolicies(new Set())
            setExpandedRules(new Set())
            setAllExpanded(false)
        } else {
            const pSet = new Set<number>()
            const rSet = new Set<string>()
            filteredData.forEach((p, pIdx) => {
                pSet.add(pIdx)
                p.rules.forEach((_r, rIdx) => {
                    rSet.add(`${pIdx}-${rIdx}`)
                })
            })
            setExpandedPolicies(pSet)
            setExpandedRules(rSet)
            setAllExpanded(true)
        }
    }

    // ─── Helper: get stats for a rule (aggregate from children) ───────────────

    const getRuleStats = (policyName: string, ruleName: string, exceptions: ExceptionItem[]) => {
        let totalIncidents = 0
        let latestDate: string | null = null
        let staleChildren = 0

        exceptions.forEach(exception => {
            const exceptionName = getExceptionName(exception)
            const key = `${policyName}|${ruleName}|${exceptionName}`
            const stats = exceptionStatsMap.get(key)
            if (stats) {
                totalIncidents += stats.incidentCount
                if (stats.lastIncidentDate) {
                    if (!latestDate || new Date(stats.lastIncidentDate) > new Date(latestDate)) {
                        latestDate = stats.lastIncidentDate
                    }
                }
                if (stats.isStale) staleChildren++
            }
        })

        return { totalIncidents, latestDate, staleChildren, exceptionCount: exceptions.length }
    }

    const getPolicyStats = (policy: ExceptionData) => {
        let totalIncidents = 0
        let latestDate: string | null = null
        let totalExceptions = 0
        let staleCount = 0

        policy.rules.forEach(rule => {
            const ruleStats = getRuleStats(policy.policyName, rule.ruleName, rule.exceptions)
            totalIncidents += ruleStats.totalIncidents
            totalExceptions += ruleStats.exceptionCount
            staleCount += ruleStats.staleChildren
            if (ruleStats.latestDate) {
                if (!latestDate || new Date(ruleStats.latestDate) > new Date(latestDate)) {
                    latestDate = ruleStats.latestDate
                }
            }
        })

        return { totalIncidents, latestDate, totalExceptions, staleCount }
    }

    // ─── Render ───────────────────────────────────────────────────────────────

    if (loading) {
        return (
            <div style={{ minHeight: '100vh', background: 'var(--background)', position: 'relative' }}>
                <LoadingOverlay isLoading={loading} message={t('exceptionsList.loading')} />
            </div>
        )
    }

    return (
        <div style={{ padding: '24px', maxWidth: '100%', fontFamily: 'Inter, sans-serif' }}>

            {/* ── Page Header ──────────────────────────────────────────────────── */}
            <div style={{ marginBottom: '24px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '4px' }}>
                    <ListChecks size={24} color="var(--accent)" />
                    <h1 style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', margin: 0, letterSpacing: '-0.02em' }}>
                        {t('exceptionList.title')}
                    </h1>
                </div>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)', margin: 0, paddingLeft: '36px' }}>
                    {t('exceptionList.subtitle')}
                </p>
            </div>

            {/* ── API Error Banner ─────────────────────────────────────────────── */}
            {apiError && (
                <div style={{
                    marginBottom: '24px',
                    padding: '16px',
                    borderRadius: '12px',
                    background: 'rgba(239, 68, 68, 0.1)',
                    border: '1px solid rgba(239, 68, 68, 0.3)',
                    color: '#ef4444',
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: '12px'
                }}>
                    <AlertTriangle size={20} style={{ flexShrink: 0, marginTop: '2px' }} />
                    <div>
                        <div style={{ fontWeight: '600', fontSize: '14px', marginBottom: '4px' }}>{t('exceptionsList.apiConnectionError')}</div>
                        <div style={{ fontSize: '13px', fontFamily: 'monospace' }}>{apiError}</div>
                    </div>
                </div>
            )}

            {/* ── Summary Cards ────────────────────────────────────────────────── */}
            {forcepointMessage && (
                <div style={{
                    marginBottom: '24px',
                    padding: '14px 16px',
                    borderRadius: '12px',
                    background: forcepointMessage.type === 'success' ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                    border: `1px solid ${forcepointMessage.type === 'success' ? 'rgba(16, 185, 129, 0.28)' : 'rgba(239, 68, 68, 0.3)'}`,
                    color: forcepointMessage.type === 'success' ? '#059669' : '#ef4444',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '10px',
                    fontSize: '13px',
                    fontWeight: '600',
                }}>
                    {forcepointMessage.type === 'success' ? <Check size={16} /> : <AlertTriangle size={16} />}
                    <span>{forcepointMessage.text}</span>
                </div>
            )}

            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                gap: '16px',
                marginBottom: '24px'
            }}>
                {/* Total Exceptions */}
                <div style={{
                    background: 'var(--surface)',
                    borderRadius: '16px',
                    padding: '20px',
                    boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '16px',
                }}>
                    <div style={{
                        width: '48px',
                        height: '48px',
                        borderRadius: '12px',
                        background: 'linear-gradient(135deg, #3b82f6, #2563eb)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                    }}>
                        <Shield size={22} color="#fff" />
                    </div>
                    <div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: '500', marginBottom: '2px' }}>
                            {t('exceptionList.totalExceptions')}
                        </div>
                        <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>
                            {totalExceptionsCount}
                        </div>
                    </div>
                </div>

                {/* Mapped Incidents (New Card) */}
                <div style={{
                    background: 'var(--surface)',
                    borderRadius: '16px',
                    padding: '20px',
                    boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '16px',
                }}>
                    <div style={{
                        width: '48px',
                        height: '48px',
                        borderRadius: '12px',
                        background: 'linear-gradient(135deg, #10b981, #059669)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                    }}>
                        <ListChecks size={22} color="#fff" />
                    </div>
                    <div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: '500', marginBottom: '2px' }}>
                            {t('exceptionsList.exceptionMatchedIncident')}
                        </div>
                        <div style={{ display: 'flex', alignItems: 'baseline', gap: '6px' }}>
                            <span style={{ fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>
                                {summaryStats.totalMappedIncidents}
                            </span>
                        </div>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                            {t('exceptionsList.sqlAggregationInfo')}
                        </div>
                    </div>
                </div>

                {/* Cleanup Candidates */}
                <div style={{
                    background: 'var(--surface)',
                    borderRadius: '16px',
                    padding: '20px',
                    boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '16px',
                }}>
                    <div style={{
                        width: '48px',
                        height: '48px',
                        borderRadius: '12px',
                        background: summaryStats.staleCount > 0 ? 'linear-gradient(135deg, #f59e0b, #d97706)' : 'linear-gradient(135deg, #10b981, #059669)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                    }}>
                        <Trash2 size={22} color="#fff" />
                    </div>
                    <div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: '500', marginBottom: '2px' }}>
                            {t('exceptionList.cleanupCandidates')}
                        </div>
                        <div style={{ fontSize: '24px', fontWeight: '700', color: summaryStats.staleCount > 0 ? '#f59e0b' : 'var(--text-primary)', letterSpacing: '-0.02em' }}>
                            {summaryStats.staleCount}
                        </div>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                            {'>'}{STALE_DAYS_THRESHOLD} {t('exceptionList.daysIdle')}
                        </div>
                    </div>
                </div>

                {/* Most Active */}
                <div style={{
                    background: 'var(--surface)',
                    borderRadius: '16px',
                    padding: '20px',
                    boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '16px',
                }}>
                    <div style={{
                        width: '48px',
                        height: '48px',
                        borderRadius: '12px',
                        background: 'linear-gradient(135deg, #6366f1, #4f46e5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                    }}>
                        <Activity size={22} color="#fff" />
                    </div>
                    <div style={{ overflow: 'hidden' }}>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: '500', marginBottom: '2px' }}>
                            {t('exceptionList.mostActive')}
                        </div>
                        <div style={{
                            fontSize: '14px',
                            fontWeight: '600',
                            color: 'var(--text-primary)',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                            maxWidth: '200px',
                        }}>
                            {summaryStats.mostActiveException || '—'}
                        </div>
                        {summaryStats.mostActiveCount > 0 && (
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                {summaryStats.mostActiveCount} {t('exceptionList.incidentCount').toLowerCase()}
                            </div>
                        )}
                    </div>
                </div>

                {/* Last Sync */}
                <div style={{
                    background: 'var(--surface)',
                    borderRadius: '16px',
                    padding: '20px',
                    boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '16px',
                }}>
                    <div style={{
                        width: '48px',
                        height: '48px',
                        borderRadius: '12px',
                        background: 'linear-gradient(135deg, #64748b, #475569)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                    }}>
                        <Clock size={22} color="#fff" />
                    </div>
                    <div style={{ flex: 1 }}>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: '500', marginBottom: '2px' }}>
                            {t('exceptionList.lastSync')}
                        </div>
                        <div style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)' }}>
                            {lastSyncedAt ? formatIstanbulDateTime(lastSyncedAt) : '—'}
                        </div>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                        <button
                            onClick={handleBulkDisableFiltered}
                            disabled={bulkDisabling || activeFilteredExceptionRefs.length === 0}
                            title="Filtrelenen exceptionlari Forcepoint uzerinde pasiflestir"
                            style={{
                                padding: '6px 12px',
                                borderRadius: '8px',
                                border: '1px solid rgba(239, 68, 68, 0.3)',
                                background: 'rgba(239, 68, 68, 0.08)',
                                color: '#ef4444',
                                fontSize: '12px',
                                fontWeight: '600',
                                cursor: bulkDisabling || activeFilteredExceptionRefs.length === 0 ? 'not-allowed' : 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '6px',
                                transition: 'all 0.2s',
                                opacity: bulkDisabling || activeFilteredExceptionRefs.length === 0 ? 0.55 : 1,
                                fontFamily: 'Inter, sans-serif',
                            }}
                        >
                            <PowerOff size={14} />
                            {bulkDisabling ? 'Kapatiliyor...' : `Aktifleri Kapat (${activeFilteredExceptionRefs.length})`}
                        </button>
                        <button
                            onClick={handleSync}
                            disabled={syncing}
                            style={{
                                padding: '6px 12px',
                                borderRadius: '8px',
                                border: '1px solid var(--border)',
                                background: 'transparent',
                                color: 'var(--text-primary)',
                                fontSize: '12px',
                                fontWeight: '500',
                                cursor: syncing ? 'not-allowed' : 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '6px',
                                transition: 'all 0.2s',
                                opacity: syncing ? 0.6 : 1,
                                fontFamily: 'Inter, sans-serif',
                            }}
                        >
                            <RefreshCw size={14} style={{ animation: syncing ? 'spin 1s linear infinite' : 'none' }} />
                            {t('exceptionList.syncNow')}
                        </button>
                    </div>
                </div>
            </div>

            {/* ── Filter Bar ───────────────────────────────────────────────────── */}
            <div style={{
                background: 'var(--surface)',
                borderRadius: '16px',
                padding: '20px',
                marginBottom: '20px',
                boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
            }}>
                <div style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    marginBottom: '16px',
                }}>
                    <Filter size={16} color="var(--text-muted)" />
                    <span style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)' }}>
                        {t('common.filter')}
                    </span>
                </div>

                <div style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                    gap: '16px',
                    alignItems: 'end',
                }}>
                    {/* Date Range */}
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            <label style={{ fontSize: '11px', fontWeight: '600', color: 'var(--text-secondary)', letterSpacing: '0.3px' }}>
                                {t('exceptionList.dateRange')}
                            </label>
                            {dateRange.start && dateRange.end && (
                                <span style={{ fontSize: '10px', color: '#3b82f6', fontWeight: '500', background: 'rgba(59, 130, 246, 0.1)', padding: '1px 6px', borderRadius: '8px' }}>
                                    {Math.abs(differenceInDays(parseISO(dateRange.end), parseISO(dateRange.start)))} Gün Seçili
                                </span>
                            )}
                        </div>
                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                            <input
                                type="date"
                                value={dateRange.start}
                                onChange={(e) => setDateRange(prev => ({ ...prev, start: e.target.value }))}
                                style={{
                                    flex: 1,
                                    padding: '8px 12px',
                                    borderRadius: '8px',
                                    border: '1px solid var(--border)',
                                    background: 'var(--surface)',
                                    color: 'var(--text-primary)',
                                    fontSize: '13px',
                                    fontFamily: 'Inter, sans-serif',
                                    outline: 'none',
                                }}
                            />
                            <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>—</span>
                            <input
                                type="date"
                                value={dateRange.end}
                                onChange={(e) => setDateRange(prev => ({ ...prev, end: e.target.value }))}
                                style={{
                                    flex: 1,
                                    padding: '8px 12px',
                                    borderRadius: '8px',
                                    border: '1px solid var(--border)',
                                    background: 'var(--surface)',
                                    color: 'var(--text-primary)',
                                    fontSize: '13px',
                                    fontFamily: 'Inter, sans-serif',
                                    outline: 'none',
                                }}
                            />
                        </div>
                    </div>

                    {/* Policy MultiSelect */}
                    <SearchableMultiSelect
                        label={t('exceptionList.filterByPolicy')}
                        options={allPolicies}
                        selectedValues={selectedPolicies}
                        onChange={setSelectedPolicies}
                        placeholder={t('common.filter')}
                    />

                    {/* Rule MultiSelect */}
                    <SearchableMultiSelect
                        label={t('exceptionList.filterByRule')}
                        options={allRules}
                        selectedValues={selectedRules}
                        onChange={setSelectedRules}
                        placeholder={t('common.filter')}
                    />

                    {/* Exception Search */}
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                        <label style={{ fontSize: '11px', fontWeight: '600', color: 'var(--text-secondary)', letterSpacing: '0.3px' }}>
                            {t('exceptionList.exception')}
                        </label>
                        <div style={{ position: 'relative' }}>
                            <Search size={14} style={{
                                position: 'absolute',
                                left: '10px',
                                top: '50%',
                                transform: 'translateY(-50%)',
                                color: 'var(--text-secondary)',
                                pointerEvents: 'none'
                            }} />
                            <input
                                type="text"
                                placeholder={t('exceptionList.searchException')}
                                value={exceptionSearch}
                                onChange={(e) => setExceptionSearch(e.target.value)}
                                style={{
                                    width: '100%',
                                    padding: '8px 32px 8px 32px',
                                    borderRadius: '8px',
                                    border: exceptionSearch ? '1.5px solid #3b82f6' : '1px solid var(--border)',
                                    background: exceptionSearch ? 'rgba(59, 130, 246, 0.04)' : 'var(--surface)',
                                    color: 'var(--text-primary)',
                                    fontSize: '13px',
                                    outline: 'none',
                                    fontFamily: 'Inter, sans-serif',
                                    transition: 'all 0.2s',
                                }}
                            />
                            {exceptionSearch && (
                                <X size={14}
                                    style={{
                                        position: 'absolute',
                                        right: '10px',
                                        top: '50%',
                                        transform: 'translateY(-50%)',
                                        color: 'var(--text-secondary)',
                                        cursor: 'pointer'
                                    }}
                                    onClick={() => setExceptionSearch('')}
                                />
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* Filtered exception breakdown */}
            <div style={{
                background: 'var(--surface)',
                borderRadius: '16px',
                marginBottom: '20px',
                boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                overflow: 'hidden',
            }}>
                <div style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: '12px',
                    padding: '14px 18px',
                    borderBottom: '1px solid var(--border)',
                    background: 'rgba(59, 130, 246, 0.03)'
                }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <ListChecks size={15} color="#3b82f6" />
                        <span style={{ fontSize: '14px', fontWeight: '700', color: 'var(--text-primary)' }}>
                            Filtrelenen exception kirilimi
                        </span>
                        <span style={{
                            fontSize: '11px',
                            color: 'var(--text-muted)',
                            background: 'var(--surface-hover)',
                            padding: '2px 8px',
                            borderRadius: '10px',
                            fontWeight: '600',
                        }}>
                            {filteredExceptionRefs.length} exception
                        </span>
                        <span style={{
                            fontSize: '11px',
                            color: '#059669',
                            background: '#F0FDF4',
                            padding: '2px 8px',
                            borderRadius: '10px',
                            fontWeight: '600',
                        }}>
                            {activeFilteredExceptionRefs.length} aktif
                        </span>
                    </div>
                    <button
                        onClick={handleBulkDisableFiltered}
                        disabled={bulkDisabling || activeFilteredExceptionRefs.length === 0}
                        style={{
                            padding: '7px 12px',
                            borderRadius: '8px',
                            border: '1px solid rgba(239, 68, 68, 0.3)',
                            background: 'rgba(239, 68, 68, 0.08)',
                            color: '#ef4444',
                            fontSize: '12px',
                            fontWeight: '700',
                            cursor: bulkDisabling || activeFilteredExceptionRefs.length === 0 ? 'not-allowed' : 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            opacity: bulkDisabling || activeFilteredExceptionRefs.length === 0 ? 0.55 : 1,
                            fontFamily: 'Inter, sans-serif',
                        }}
                    >
                        <PowerOff size={14} />
                        {bulkDisabling ? 'Kapatiliyor...' : 'Aktifleri Kapat'}
                    </button>
                </div>

                <div style={{ overflowX: 'auto' }}>
                    <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                        {filteredExceptionRefs.length === 0 ? (
                            <div style={{ padding: '28px 18px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>
                                Filtrelere uygun exception bulunamadi.
                            </div>
                        ) : (
                            <table style={{
                                width: 'max-content',
                                minWidth: '100%',
                                borderCollapse: 'collapse',
                                tableLayout: 'auto',
                            }}>
                                <thead style={{
                                    position: 'sticky',
                                    top: 0,
                                    zIndex: 1,
                                    background: 'var(--surface-hover)',
                                }}>
                                    <tr style={{
                                        borderBottom: '1px solid var(--border)',
                                        fontSize: '11px',
                                        fontWeight: '700',
                                        color: 'var(--text-muted)',
                                        textTransform: 'uppercase',
                                    }}>
                                        <th style={{ padding: '10px 18px', textAlign: 'left', whiteSpace: 'nowrap', minWidth: '220px' }}>Policy</th>
                                        <th style={{ padding: '10px 18px', textAlign: 'left', whiteSpace: 'nowrap', minWidth: '220px' }}>Rule</th>
                                        <th style={{ padding: '10px 18px', textAlign: 'left', whiteSpace: 'nowrap', minWidth: '260px' }}>Exception</th>
                                        <th style={{ padding: '10px 18px', textAlign: 'center', whiteSpace: 'nowrap', minWidth: '110px' }}>Incident</th>
                                        <th style={{ padding: '10px 18px', textAlign: 'center', whiteSpace: 'nowrap', minWidth: '160px' }}>Son kullanim</th>
                                        <th style={{ padding: '10px 18px', textAlign: 'center', whiteSpace: 'nowrap', minWidth: '110px' }}>Durum</th>
                                        <th style={{ padding: '10px 18px', textAlign: 'center', whiteSpace: 'nowrap', minWidth: '120px' }}>Aksiyon</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredExceptionRefs.map((ref) => {
                                const key = `${ref.policyName}|${ref.ruleName}|${ref.exceptionName}`
                                const stats = exceptionStatsMap.get(key)
                                const forcepointEnabled = isExceptionEnabled(ref)
                                const actionKey = getExceptionRefKey(ref)
                                const isDisabling = disablingExceptionKey === actionKey

                                return (
                                    <tr key={key} style={{
                                        borderBottom: '1px solid var(--border)',
                                        fontSize: '12px',
                                    }}>
                                        <td style={{ padding: '9px 18px', color: 'var(--text-primary)', fontWeight: '600', whiteSpace: 'nowrap' }}>{ref.policyName}</td>
                                        <td style={{ padding: '9px 18px', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>{ref.ruleName}</td>
                                        <td style={{ padding: '9px 18px', color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>{ref.exceptionName}</td>
                                        <td style={{ padding: '9px 18px', textAlign: 'center', color: (stats?.incidentCount || 0) > 0 ? '#3b82f6' : 'var(--text-muted)', fontWeight: '600', whiteSpace: 'nowrap' }}>{stats?.incidentCount || 0}</td>
                                        <td style={{ padding: '9px 18px', textAlign: 'center', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>
                                            {stats?.lastIncidentDate ? formatIstanbulDateTime(stats.lastIncidentDate) : t('exceptionList.noIncidents')}
                                        </td>
                                        <td style={{ padding: '9px 18px', textAlign: 'center', whiteSpace: 'nowrap' }}>
                                            <span style={{
                                                display: 'inline-flex',
                                                padding: '2px 8px',
                                                borderRadius: '6px',
                                                fontSize: '11px',
                                                fontWeight: '700',
                                                background: forcepointEnabled ? '#F0FDF4' : '#F3F4F6',
                                                color: forcepointEnabled ? '#059669' : '#6B7280',
                                            }}>
                                                {forcepointEnabled ? 'Aktif' : 'Pasif'}
                                            </span>
                                        </td>
                                        <td style={{ padding: '9px 18px', textAlign: 'center', whiteSpace: 'nowrap' }}>
                                            {forcepointEnabled ? (
                                                <button
                                                    onClick={() => handleDisableSingleException(ref)}
                                                    disabled={bulkDisabling || isDisabling}
                                                    title="Bu exception'i Forcepoint uzerinde pasiflestir"
                                                    style={{
                                                        padding: '5px 9px',
                                                        borderRadius: '7px',
                                                        border: '1px solid rgba(239, 68, 68, 0.3)',
                                                        background: 'rgba(239, 68, 68, 0.08)',
                                                        color: '#ef4444',
                                                        fontSize: '11px',
                                                        fontWeight: '700',
                                                        cursor: bulkDisabling || isDisabling ? 'not-allowed' : 'pointer',
                                                        display: 'inline-flex',
                                                        alignItems: 'center',
                                                        gap: '5px',
                                                        opacity: bulkDisabling || isDisabling ? 0.55 : 1,
                                                    }}
                                                >
                                                    <PowerOff size={12} />
                                                    {isDisabling ? '...' : 'Kapat'}
                                                </button>
                                            ) : (
                                                <span style={{
                                                    fontSize: '11px',
                                                    fontWeight: '700',
                                                    color: '#6B7280',
                                                    background: '#F3F4F6',
                                                    borderRadius: '7px',
                                                    padding: '5px 9px',
                                                }}>
                                                    Pasif
                                                </span>
                                            )}
                                        </td>
                                    </tr>
                                )
                                    })}
                                </tbody>
                            </table>
                        )}
                    </div>
                </div>
            </div>

            <div style={{
                background: 'var(--surface)',
                borderRadius: '16px',
                boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                overflow: 'hidden',
            }}>
                {/* Tree header */}
                <div style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '16px 20px',
                    borderBottom: '1px solid var(--border)',
                }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <Info size={16} color="var(--text-muted)" />
                        <span style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)' }}>
                            {t('exceptionList.policy')} → {t('exceptionList.rule')} → {t('exceptionList.exception')}
                        </span>
                        <span style={{
                            fontSize: '11px',
                            color: 'var(--text-muted)',
                            background: 'var(--surface-hover)',
                            padding: '2px 8px',
                            borderRadius: '10px',
                            fontWeight: '500',
                        }}>
                            {filteredData.length} {t('exceptionList.policy').toLowerCase()}
                        </span>
                    </div>
                    <button
                        onClick={toggleExpandAll}
                        style={{
                            padding: '6px 12px',
                            borderRadius: '8px',
                            border: '1px solid var(--border)',
                            background: 'transparent',
                            color: 'var(--text-primary)',
                            fontSize: '12px',
                            fontWeight: '500',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            transition: 'all 0.2s',
                            fontFamily: 'Inter, sans-serif',
                        }}
                    >
                        {allExpanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                        {allExpanded ? t('exceptionList.collapseAll') : t('exceptionList.expandAll')}
                    </button>
                </div>

                {/* Tree Table Header */}
                <div style={{
                    display: 'grid',
                    gridTemplateColumns: 'minmax(260px, 1fr) 120px 120px 140px 100px 120px',
                    padding: '10px 20px',
                    background: 'var(--surface-hover)',
                    borderBottom: '1px solid var(--border)',
                    fontSize: '12px',
                    fontWeight: '600',
                    color: 'var(--text-muted)',
                }}>
                    <div>{t('exceptionList.name')}</div>
                    <div style={{ textAlign: 'center' }}>{t('exceptionList.exceptionCount')}</div>
                    <div style={{ textAlign: 'center' }}>{t('exceptionList.incidentCount')}</div>
                    <div style={{ textAlign: 'center' }}>{t('exceptionList.lastIncident')}</div>
                    <div style={{ textAlign: 'center' }}>{t('exceptionList.status')}</div>
                    <div style={{ textAlign: 'center' }}>Aksiyon</div>
                </div>

                {/* Tree Content */}
                <div style={{ maxHeight: '600px', overflowY: 'auto' }}>
                    {filteredData.length === 0 ? (
                        <div style={{
                            padding: '60px',
                            textAlign: 'center',
                            color: 'var(--text-muted)',
                            fontSize: '14px'
                        }}>
                            {t('exceptionList.noData')}
                        </div>
                    ) : (
                        filteredData.map((policy, pIdx) => {
                            const policyStats = getPolicyStats(policy)
                            const isPolicyExpanded = expandedPolicies.has(pIdx)

                            return (
                                <div key={policy.policyName || `policy-${pIdx}`}>
                                    {/* Policy Row */}
                                    <div
                                        onClick={() => togglePolicy(pIdx)}
                                        style={{
                                            display: 'grid',
                                            gridTemplateColumns: 'minmax(260px, 1fr) 120px 120px 140px 100px 120px',
                                            padding: '12px 20px',
                                            cursor: 'pointer',
                                            borderBottom: '1px solid var(--border)',
                                            transition: 'background 0.15s',
                                            alignItems: 'center',
                                        }}
                                        onMouseEnter={(e) => e.currentTarget.style.background = 'var(--surface-hover)'}
                                        onMouseLeave={(e) => e.currentTarget.style.background = 'transparent'}
                                    >
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                            {isPolicyExpanded ? <ChevronDown size={16} color="var(--accent)" /> : <ChevronRight size={16} color="var(--text-muted)" />}
                                            <Shield size={16} color="#3b82f6" />
                                            <span style={{ fontWeight: '600', fontSize: '14px', color: 'var(--text-primary)' }}>
                                                {policy.policyName}
                                            </span>
                                            <span style={{
                                                fontSize: '11px',
                                                color: 'var(--text-muted)',
                                                background: 'var(--surface-hover)',
                                                padding: '1px 6px',
                                                borderRadius: '8px',
                                            }}>
                                                {policy.rules.length} {t('exceptionList.rule').toLowerCase()}
                                            </span>
                                        </div>
                                        <div style={{ textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-primary)' }}>
                                            {policyStats.totalExceptions}
                                        </div>
                                        <div style={{ textAlign: 'center', fontSize: '13px', fontWeight: '600', color: policyStats.totalIncidents > 0 ? '#3b82f6' : 'var(--text-muted)' }}>
                                            {policyStats.totalIncidents}
                                        </div>
                                        <div style={{ textAlign: 'center', fontSize: '12px', color: 'var(--text-secondary)' }}>
                                            {policyStats.latestDate ? formatIstanbulDate(policyStats.latestDate) : '—'}
                                        </div>
                                        <div style={{ textAlign: 'center' }}>
                                            {policyStats.staleCount > 0 && (
                                                <span style={{
                                                    display: 'inline-flex',
                                                    alignItems: 'center',
                                                    gap: '4px',
                                                    padding: '2px 8px',
                                                    borderRadius: '6px',
                                                    fontSize: '11px',
                                                    fontWeight: '600',
                                                    background: '#FEF3C7',
                                                    color: '#D97706',
                                                }}>
                                                    <AlertTriangle size={10} />
                                                    {policyStats.staleCount}
                                                </span>
                                            )}
                                        </div>
                                        <div />
                                    </div>

                                    {/* Rules - Expanded */}
                                    {isPolicyExpanded && policy.rules.map((rule, rIdx) => {
                                        const ruleStats = getRuleStats(policy.policyName, rule.ruleName, rule.exceptions)
                                        const isRuleExpanded = expandedRules.has(`${pIdx}-${rIdx}`)

                                        return (
                                            <div key={rule.ruleName || `rule-${pIdx}-${rIdx}`}>
                                                {/* Rule Row */}
                                                <div
                                                    onClick={() => toggleRule(pIdx, rIdx)}
                                                    style={{
                                                        display: 'grid',
                                                        gridTemplateColumns: 'minmax(260px, 1fr) 120px 120px 140px 100px 120px',
                                                        padding: '10px 20px 10px 48px',
                                                        cursor: 'pointer',
                                                        borderBottom: '1px solid var(--border)',
                                                        transition: 'background 0.15s',
                                                        alignItems: 'center',
                                                        background: 'rgba(59, 130, 246, 0.02)',
                                                    }}
                                                    onMouseEnter={(e) => e.currentTarget.style.background = 'rgba(59, 130, 246, 0.05)'}
                                                    onMouseLeave={(e) => e.currentTarget.style.background = 'rgba(59, 130, 246, 0.02)'}
                                                >
                                                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                        {isRuleExpanded ? <ChevronDown size={14} color="var(--accent)" /> : <ChevronRight size={14} color="var(--text-muted)" />}
                                                        <ListChecks size={14} color="#6366f1" />
                                                        <span style={{ fontWeight: '500', fontSize: '13px', color: 'var(--text-primary)' }}>
                                                            {rule.ruleName}
                                                        </span>
                                                        <span style={{
                                                            fontSize: '10px',
                                                            color: 'var(--text-muted)',
                                                            background: 'var(--surface-hover)',
                                                            padding: '1px 6px',
                                                            borderRadius: '8px',
                                                        }}>
                                                            {rule.exceptions.length} exc
                                                        </span>
                                                    </div>
                                                    <div style={{ textAlign: 'center', fontSize: '13px', fontWeight: '500', color: 'var(--text-primary)' }}>
                                                        {ruleStats.exceptionCount}
                                                    </div>
                                                    <div style={{ textAlign: 'center', fontSize: '13px', fontWeight: '500', color: ruleStats.totalIncidents > 0 ? '#3b82f6' : 'var(--text-muted)' }}>
                                                        {ruleStats.totalIncidents}
                                                    </div>
                                                    <div style={{ textAlign: 'center', fontSize: '12px', color: 'var(--text-secondary)' }}>
                                                        {ruleStats.latestDate ? formatIstanbulDate(ruleStats.latestDate) : '—'}
                                                    </div>
                                                    <div style={{ textAlign: 'center' }}>
                                                        {ruleStats.staleChildren > 0 && (
                                                            <span style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                gap: '4px',
                                                                padding: '2px 8px',
                                                                borderRadius: '6px',
                                                                fontSize: '11px',
                                                                fontWeight: '600',
                                                                background: '#FEF3C7',
                                                                color: '#D97706',
                                                            }}>
                                                                <AlertTriangle size={10} />
                                                                {ruleStats.staleChildren}
                                                            </span>
                                                        )}
                                                    </div>
                                                    <div />
                                                </div>

                                                {/* Exception Rows - Expanded */}
                                                {isRuleExpanded && rule.exceptions.map((exc, eIdx) => {
                                                    const exceptionName = getExceptionName(exc)
                                                    const key = `${policy.policyName}|${rule.ruleName}|${exceptionName}`
                                                    const stats = exceptionStatsMap.get(key)
                                                    const isStale = stats?.isStale || false
                                                    const ref = {
                                                        policyName: policy.policyName,
                                                        ruleName: rule.ruleName,
                                                        exceptionName,
                                                        enabled: getExceptionEnabled(exc)
                                                    }
                                                    const forcepointEnabled = isExceptionEnabled(ref)
                                                    const actionKey = getExceptionRefKey(ref)
                                                    const isDisabling = disablingExceptionKey === actionKey

                                                    return (
                                                        <div
                                                            key={`${exceptionName || 'exc'}-${pIdx}-${rIdx}-${eIdx}`}
                                                            style={{
                                                                display: 'grid',
                                                                gridTemplateColumns: 'minmax(260px, 1fr) 120px 120px 140px 100px 120px',
                                                                padding: '9px 20px 9px 80px',
                                                                borderBottom: '1px solid var(--border)',
                                                                alignItems: 'center',
                                                                transition: 'background 0.15s',
                                                                background: isStale ? 'rgba(245, 158, 11, 0.04)' : 'rgba(99, 102, 241, 0.02)',
                                                            }}
                                                            onMouseEnter={(e) => e.currentTarget.style.background = isStale ? 'rgba(245, 158, 11, 0.08)' : 'rgba(99, 102, 241, 0.05)'}
                                                            onMouseLeave={(e) => e.currentTarget.style.background = isStale ? 'rgba(245, 158, 11, 0.04)' : 'rgba(99, 102, 241, 0.02)'}
                                                        >
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                                <div style={{
                                                                    width: '6px',
                                                                    height: '6px',
                                                                    borderRadius: '50%',
                                                                    background: isStale ? '#f59e0b' : '#10b981',
                                                                    flexShrink: 0,
                                                                }} />
                                                                <span style={{
                                                                    fontSize: '13px',
                                                                    color: 'var(--text-primary)',
                                                                    overflow: 'hidden',
                                                                    textOverflow: 'ellipsis',
                                                                    whiteSpace: 'nowrap',
                                                                }}>
                                                                    {exceptionName}
                                                                </span>
                                                                {isStale && (
                                                                    <AlertTriangle size={12} color="#f59e0b" style={{ flexShrink: 0 }} />
                                                                )}
                                                            </div>
                                                            <div style={{ textAlign: 'center', fontSize: '12px', color: 'var(--text-muted)' }}>
                                                                —
                                                            </div>
                                                            <div style={{
                                                                textAlign: 'center',
                                                                fontSize: '13px',
                                                                fontWeight: '500',
                                                                color: (stats?.incidentCount || 0) > 0 ? '#3b82f6' : 'var(--text-muted)',
                                                            }}>
                                                                {stats?.incidentCount || 0}
                                                            </div>
                                                            <div style={{ textAlign: 'center', fontSize: '12px', color: 'var(--text-secondary)' }}>
                                                                {stats?.lastIncidentDate
                                                                    ? formatIstanbulDateTime(stats.lastIncidentDate)
                                                                    : t('exceptionList.noIncidents')
                                                                }
                                                            </div>
                                                            <div style={{ textAlign: 'center' }}>
                                                                {isStale ? (
                                                                    <span style={{
                                                                        display: 'inline-flex',
                                                                        alignItems: 'center',
                                                                        gap: '3px',
                                                                        padding: '2px 8px',
                                                                        borderRadius: '6px',
                                                                        fontSize: '11px',
                                                                        fontWeight: '600',
                                                                        background: '#FEF3C7',
                                                                        color: '#D97706',
                                                                    }}>
                                                                        {t('exceptionList.stale')}
                                                                    </span>
                                                                ) : (
                                                                    <span style={{
                                                                        display: 'inline-flex',
                                                                        alignItems: 'center',
                                                                        gap: '3px',
                                                                        padding: '2px 8px',
                                                                        borderRadius: '6px',
                                                                        fontSize: '11px',
                                                                        fontWeight: '600',
                                                                        background: '#F0FDF4',
                                                                        color: '#059669',
                                                                    }}>
                                                                        {t('exceptionList.active')}
                                                                    </span>
                                                                )}
                                                            </div>
                                                            <div style={{ display: 'flex', justifyContent: 'center' }}>
                                                                {forcepointEnabled ? (
                                                                    <button
                                                                        onClick={() => handleDisableSingleException(ref)}
                                                                        disabled={bulkDisabling || isDisabling}
                                                                        title="Bu exception'i Forcepoint uzerinde pasiflestir"
                                                                        style={{
                                                                            padding: '5px 9px',
                                                                            borderRadius: '7px',
                                                                            border: '1px solid rgba(239, 68, 68, 0.3)',
                                                                            background: 'rgba(239, 68, 68, 0.08)',
                                                                            color: '#ef4444',
                                                                            fontSize: '11px',
                                                                            fontWeight: '700',
                                                                            cursor: bulkDisabling || isDisabling ? 'not-allowed' : 'pointer',
                                                                            display: 'inline-flex',
                                                                            alignItems: 'center',
                                                                            gap: '5px',
                                                                            opacity: bulkDisabling || isDisabling ? 0.55 : 1,
                                                                        }}
                                                                    >
                                                                        <PowerOff size={12} />
                                                                        {isDisabling ? '...' : 'Kapat'}
                                                                    </button>
                                                                ) : (
                                                                    <span style={{
                                                                        fontSize: '11px',
                                                                        fontWeight: '700',
                                                                        color: '#6B7280',
                                                                        background: '#F3F4F6',
                                                                        borderRadius: '7px',
                                                                        padding: '5px 9px',
                                                                    }}>
                                                                        Pasif
                                                                    </span>
                                                                )}
                                                            </div>
                                                        </div>
                                                    )
                                                })}
                                            </div>
                                        )
                                    })}
                                </div>
                            )
                        })
                    )}
                </div>
            </div>

            {/* ── Info Footer ──────────────────────────────────────────────────── */}
            <div style={{
                marginTop: '16px',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                padding: '12px 16px',
                background: 'var(--surface)',
                borderRadius: '12px',
                boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
            }}>
                <Info size={14} color="var(--text-muted)" />
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                    {t('exceptionList.stale')}: {'>'}{STALE_DAYS_THRESHOLD} {t('exceptionList.daysIdle')}
                    {' · '}
                    {t('exceptionList.active')}: {'<'}{STALE_DAYS_THRESHOLD} {t('exceptionList.daysIdle')}
                </span>
            </div>

        </div>
    )
}
