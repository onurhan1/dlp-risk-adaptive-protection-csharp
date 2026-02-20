'use client'

import { useEffect, useState, useCallback, useMemo } from 'react'
import axios from 'axios'
import { format, subDays } from 'date-fns'
import { getApiUrlDynamic } from '@/lib/api-config'
import Pagination from './ui/Pagination'

interface ActionIncident {
    login_name: string
    destination: string
    channel: string
    policy: string
    rule_name: string
    action?: string
    timestamp: string
    max_matches?: number
    violation_triggers?: string
}

interface PaginatedResponse {
    items: ActionIncident[]
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
    hasNextPage: boolean
    hasPreviousPage: boolean
}

interface FilterOptions {
    users: string[]
    destinations: string[]
    channels: string[]
    policies: string[]
    rules: string[]
    dateRange: {
        minDate: string
        maxDate: string
    }
}

interface ActionIncidentsModalProps {
    isOpen: boolean
    onClose: () => void
    action: string
    initialDate?: string  // For single-day mode (Reports page)
}

// Debounce hook
function useDebounce<T>(value: T, delay: number): T {
    const [debouncedValue, setDebouncedValue] = useState<T>(value)

    useEffect(() => {
        const handler = setTimeout(() => {
            setDebouncedValue(value)
        }, delay)

        return () => {
            clearTimeout(handler)
        }
    }, [value, delay])

    return debouncedValue
}

// Autocomplete Filter Component
function FilterDropdown({
    label,
    value,
    onChange,
    options,
    placeholder
}: {
    label: string
    value: string
    onChange: (val: string) => void
    options: string[]
    placeholder: string
}) {
    const [showDropdown, setShowDropdown] = useState(false)
    const DISPLAY_LIMIT = 100 // Performance optimization

    const { displayOptions, totalCount, hasMore } = useMemo(() => {
        let filtered = options
        if (value.trim()) {
            filtered = options.filter(opt =>
                opt.toLowerCase().includes(value.toLowerCase())
            )
        }
        return {
            displayOptions: filtered.slice(0, DISPLAY_LIMIT),
            totalCount: filtered.length,
            hasMore: filtered.length > DISPLAY_LIMIT
        }
    }, [options, value])

    return (
        <div style={{ position: 'relative', minWidth: '140px', flex: 1 }}>
            <label style={{
                fontSize: '10px',
                color: 'var(--text-muted)',
                textTransform: 'uppercase',
                display: 'block',
                marginBottom: '4px'
            }}>
                {label} {totalCount > 0 && <span style={{ opacity: 0.7 }}>({totalCount})</span>}
            </label>
            <input
                type="text"
                placeholder={placeholder}
                value={value}
                onChange={(e) => onChange(e.target.value)}
                onFocus={() => setShowDropdown(true)}
                onBlur={() => setTimeout(() => setShowDropdown(false), 200)}
                style={{
                    width: '100%',
                    padding: '8px 12px',
                    fontSize: '13px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    backgroundColor: 'var(--background)',
                    color: 'var(--text-primary)',
                    outline: 'none',
                    transition: 'border-color 0.2s'
                }}
            />
            {showDropdown && displayOptions.length > 0 && (
                <div style={{
                    position: 'absolute',
                    top: '100%',
                    left: 0,
                    right: 0,
                    maxHeight: '200px',
                    overflowY: 'auto',
                    background: 'var(--surface)',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                    zIndex: 100
                }}>
                    {displayOptions.map((option, idx) => (
                        <div
                            key={idx}
                            onClick={() => { onChange(option); setShowDropdown(false) }}
                            style={{
                                padding: '8px 12px',
                                cursor: 'pointer',
                                fontSize: '12px',
                                borderBottom: '1px solid var(--border)',
                                color: 'var(--text-primary)'
                            }}
                            onMouseEnter={(e) => e.currentTarget.style.background = 'var(--primary)'}
                            onMouseLeave={(e) => e.currentTarget.style.background = 'transparent'}
                        >
                            {option}
                        </div>
                    ))}
                    {hasMore && (
                        <div style={{
                            padding: '8px 12px',
                            fontSize: '11px',
                            color: 'var(--text-muted)',
                            textAlign: 'center',
                            fontStyle: 'italic',
                            backgroundColor: 'var(--background-secondary)'
                        }}>
                            +{totalCount - DISPLAY_LIMIT} more... (type to filter)
                        </div>
                    )}
                </div>
            )}
        </div>
    )
}

export default function ActionIncidentsModal({
    isOpen,
    onClose,
    action,
    initialDate  // Single day mode for Reports page
}: ActionIncidentsModalProps) {
    // Single day mode when initialDate is provided
    const isSingleDayMode = !!initialDate

    // Filter options from API
    const [filterOptions, setFilterOptions] = useState<FilterOptions | null>(null)

    // Date range state
    const [dateRange, setDateRange] = useState({
        start: initialDate || format(subDays(new Date(), 30), 'yyyy-MM-dd'),
        end: initialDate || format(new Date(), 'yyyy-MM-dd')
    })

    // Pagination state
    const [page, setPage] = useState(1)
    const [pageSize] = useState(100)
    const [totalCount, setTotalCount] = useState(0)
    const [totalPages, setTotalPages] = useState(0)

    // Filter states
    const [filters, setFilters] = useState({
        user: '',
        destination: '',
        channel: '',
        policy: '',
        rule: ''
    })

    // Debounced filters for server-side search
    const debouncedUser = useDebounce(filters.user, 500)
    const debouncedDestination = useDebounce(filters.destination, 500)
    const debouncedChannel = useDebounce(filters.channel, 500)
    const debouncedPolicy = useDebounce(filters.policy, 500)
    const debouncedRule = useDebounce(filters.rule, 500)

    const [incidents, setIncidents] = useState<ActionIncident[]>([])
    const [loading, setLoading] = useState(false)

    // Fetch filter options when modal opens
    useEffect(() => {
        if (isOpen) {
            fetchFilterOptions()
        }
    }, [isOpen, action])

    const fetchFilterOptions = async () => {
        try {
            const apiUrl = getApiUrlDynamic()
            const response = await axios.get<FilterOptions>(`${apiUrl}/api/risk/incidents/filter-options`, {
                params: { action }
            })
            setFilterOptions(response.data)

            // Set default date range from API if not single day mode
            if (!isSingleDayMode && response.data.dateRange) {
                setDateRange({
                    start: response.data.dateRange.minDate,
                    end: response.data.dateRange.maxDate
                })
            }
        } catch (error) {
            console.error('Error fetching filter options:', error)
        }
    }

    // Update date range when initialDate changes
    useEffect(() => {
        if (initialDate) {
            setDateRange({ start: initialDate, end: initialDate })
        }
    }, [initialDate])

    // Fetch incidents when modal opens or filters/pagination change
    useEffect(() => {
        if (isOpen) {
            fetchIncidents()
        }
    }, [isOpen, dateRange.start, dateRange.end, action, page, debouncedUser, debouncedDestination, debouncedChannel, debouncedPolicy, debouncedRule])

    // Reset page when filters change
    useEffect(() => {
        setPage(1)
    }, [debouncedUser, debouncedDestination, debouncedChannel, debouncedPolicy, debouncedRule, dateRange.start, dateRange.end])

    // Reset all when modal opens
    useEffect(() => {
        if (isOpen) {
            setFilters({ user: '', destination: '', channel: '', policy: '', rule: '' })
            setPage(1)
        }
    }, [isOpen])

    const fetchIncidents = async () => {
        setLoading(true)
        try {
            const apiUrl = getApiUrlDynamic()
            const response = await axios.get<PaginatedResponse>(`${apiUrl}/api/risk/incidents/by-action`, {
                params: {
                    action: action,
                    start_date: dateRange.start,
                    end_date: dateRange.end,
                    page: page,
                    pageSize: pageSize,
                    user: debouncedUser || undefined,
                    destination: debouncedDestination || undefined,
                    channel: debouncedChannel || undefined,
                    policy: debouncedPolicy || undefined,
                    rule: debouncedRule || undefined
                }
            })
            setIncidents(response.data.items || [])
            setTotalCount(response.data.totalCount || 0)
            setTotalPages(response.data.totalPages || 0)
        } catch (error) {
            console.error('Error fetching action incidents:', error)
            setIncidents([])
            setTotalCount(0)
            setTotalPages(0)
        } finally {
            setLoading(false)
        }
    }

    // Close modal on ESC key
    useEffect(() => {
        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onClose()
        }
        if (isOpen) {
            window.addEventListener('keydown', handleEscape)
            return () => window.removeEventListener('keydown', handleEscape)
        }
    }, [isOpen, onClose])

    // Check if any filter is active
    const hasActiveFilters = filters.user || filters.destination || filters.channel || filters.policy || filters.rule

    // Clear all filters
    const clearFilters = () => {
        setFilters({ user: '', destination: '', channel: '', policy: '', rule: '' })
        setPage(1)
    }

    if (!isOpen) return null

    const actionColors: Record<string, string> = {
        BLOCK: '#ef4444',
        QUARANTINE: '#9013ff',
        AUTHORIZED: '#10b981',
        RELEASED: '#f59e0b',
        TOTAL: '#3b82f6'
    }

    const actionColor = actionColors[action] || '#3b82f6'

    const dateInputStyle = {
        padding: '8px 12px',
        fontSize: '13px',
        border: '1px solid var(--border)',
        borderRadius: '6px',
        backgroundColor: 'var(--background)',
        color: 'var(--text-primary)',
        outline: 'none'
    }

    return (
        <>
            {/* Backdrop */}
            <div
                onClick={onClose}
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: 'rgba(0, 0, 0, 0.7)',
                    zIndex: 9998,
                    backdropFilter: 'blur(4px)'
                }}
            />

            {/* Modal */}
            <div
                onClick={(e) => e.stopPropagation()}
                style={{
                    position: 'fixed',
                    top: '50%',
                    left: '50%',
                    transform: 'translate(-50%, -50%)',
                    backgroundColor: 'var(--surface)',
                    borderRadius: '12px',
                    border: '1px solid var(--border)',
                    boxShadow: '0 20px 60px rgba(0, 0, 0, 0.4)',
                    zIndex: 9999,
                    width: '90%',
                    maxWidth: '1200px',
                    maxHeight: '90vh',
                    display: 'flex',
                    flexDirection: 'column'
                }}
            >
                {/* Header */}
                <div style={{
                    padding: '20px 24px',
                    borderBottom: '1px solid var(--border)',
                    flexShrink: 0
                }}>
                    {/* Title Row */}
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                        <h2 style={{
                            margin: 0,
                            fontSize: '20px',
                            fontWeight: '600',
                            color: 'var(--text-primary)',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '12px'
                        }}>
                            <span style={{
                                width: '8px',
                                height: '8px',
                                borderRadius: '50%',
                                backgroundColor: actionColor
                            }} />
                            {action} Incidents
                            <span style={{
                                fontSize: '14px',
                                fontWeight: '400',
                                color: 'var(--text-muted)',
                                backgroundColor: 'var(--background-secondary)',
                                padding: '4px 12px',
                                borderRadius: '20px'
                            }}>
                                {totalCount.toLocaleString()} total
                            </span>
                        </h2>
                        <button
                            onClick={onClose}
                            style={{
                                background: 'transparent',
                                border: 'none',
                                fontSize: '24px',
                                cursor: 'pointer',
                                color: 'var(--text-secondary)',
                                padding: '4px 8px',
                                borderRadius: '6px'
                            }}
                        >
                            ×
                        </button>
                    </div>

                    {/* Date Range Row */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '12px',
                        marginBottom: '16px',
                        padding: '12px 16px',
                        backgroundColor: 'var(--background-secondary)',
                        borderRadius: '8px'
                    }}>
                        <span style={{ fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>📅 Date Range:</span>
                        {isSingleDayMode ? (
                            <span style={{ fontWeight: '600', color: 'var(--text-primary)' }}>
                                {new Date(dateRange.start).toLocaleDateString('en-US', {
                                    weekday: 'long',
                                    year: 'numeric',
                                    month: 'long',
                                    day: 'numeric'
                                })}
                            </span>
                        ) : (
                            <>
                                <input
                                    type="date"
                                    value={dateRange.start}
                                    onChange={(e) => setDateRange({ ...dateRange, start: e.target.value })}
                                    style={dateInputStyle}
                                />
                                <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>to</span>
                                <input
                                    type="date"
                                    value={dateRange.end}
                                    onChange={(e) => setDateRange({ ...dateRange, end: e.target.value })}
                                    style={dateInputStyle}
                                />
                            </>
                        )}
                    </div>

                    {/* Filters Row */}
                    <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'flex-end' }}>
                        <FilterDropdown
                            label="User"
                            value={filters.user}
                            onChange={(val) => setFilters(f => ({ ...f, user: val }))}
                            options={filterOptions?.users || []}
                            placeholder="Filter user..."
                        />
                        <FilterDropdown
                            label="Destination"
                            value={filters.destination}
                            onChange={(val) => setFilters(f => ({ ...f, destination: val }))}
                            options={filterOptions?.destinations || []}
                            placeholder="Filter destination..."
                        />
                        <FilterDropdown
                            label="Channel"
                            value={filters.channel}
                            onChange={(val) => setFilters(f => ({ ...f, channel: val }))}
                            options={filterOptions?.channels || []}
                            placeholder="Filter channel..."
                        />
                        <FilterDropdown
                            label="Policy"
                            value={filters.policy}
                            onChange={(val) => setFilters(f => ({ ...f, policy: val }))}
                            options={filterOptions?.policies || []}
                            placeholder="Filter policy..."
                        />
                        <FilterDropdown
                            label="Rule"
                            value={filters.rule}
                            onChange={(val) => setFilters(f => ({ ...f, rule: val }))}
                            options={filterOptions?.rules || []}
                            placeholder="Filter rule..."
                        />

                        {hasActiveFilters && (
                            <button
                                onClick={clearFilters}
                                style={{
                                    padding: '8px 16px',
                                    fontSize: '12px',
                                    backgroundColor: 'var(--warning)',
                                    color: '#fff',
                                    border: 'none',
                                    borderRadius: '6px',
                                    cursor: 'pointer',
                                    fontWeight: '600',
                                    marginBottom: '2px'
                                }}
                            >
                                Clear Filters
                            </button>
                        )}
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '0' }}>
                    {loading ? (
                        <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            padding: '60px',
                            color: 'var(--text-muted)'
                        }}>
                            <div style={{ textAlign: 'center' }}>
                                <div style={{ fontSize: '32px', marginBottom: '8px' }}>⏳</div>
                                Loading incidents...
                            </div>
                        </div>
                    ) : incidents.length === 0 ? (
                        <div style={{
                            display: 'flex',
                            flexDirection: 'column',
                            alignItems: 'center',
                            justifyContent: 'center',
                            padding: '60px',
                            color: 'var(--text-muted)'
                        }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>📭</div>
                            <div style={{ fontSize: '16px', fontWeight: '500' }}>No {action.toLowerCase()} incidents found</div>
                            <div style={{ fontSize: '13px', marginTop: '4px' }}>Try adjusting your filters or date range</div>
                        </div>
                    ) : (
                        <table style={{
                            width: '100%',
                            borderCollapse: 'collapse'
                        }}>
                            <thead>
                                <tr style={{
                                    backgroundColor: 'var(--background-secondary)',
                                    borderBottom: '1px solid var(--border)',
                                    position: 'sticky',
                                    top: 0
                                }}>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase', width: '40px' }}>#</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Login Name</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Destination</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Channel</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Policy/Rule</th>
                                    <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Matches</th>
                                    {action === 'TOTAL' && <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Status</th>}
                                    <th style={{ padding: '12px', textAlign: 'right', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Date/Time</th>
                                </tr>
                            </thead>
                            <tbody>
                                {incidents.map((incident, idx) => (
                                    <tr
                                        key={idx}
                                        style={{
                                            borderBottom: '1px solid var(--border)',
                                            transition: 'background 0.2s'
                                        }}
                                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--surface-hover)'}
                                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                    >
                                        <td style={{ padding: '12px', fontSize: '14px', color: 'var(--text-secondary)' }}>
                                            {(page - 1) * pageSize + idx + 1}
                                        </td>
                                        <td style={{ padding: '12px', fontSize: '14px', color: 'var(--text-primary)', fontWeight: '500' }}>
                                            {incident.login_name}
                                        </td>
                                        <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                                            <div style={{
                                                maxWidth: '250px',
                                                overflow: 'hidden',
                                                textOverflow: 'ellipsis',
                                                whiteSpace: 'nowrap'
                                            }} title={incident.destination}>
                                                {incident.destination}
                                            </div>
                                        </td>
                                        <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                                            <span style={{
                                                padding: '4px 8px',
                                                backgroundColor: 'var(--background-secondary)',
                                                borderRadius: '4px',
                                                fontSize: '11px',
                                                fontWeight: '600',
                                                textTransform: 'uppercase'
                                            }}>
                                                {incident.channel}
                                            </span>
                                        </td>
                                        <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                                            <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{incident.policy}</div>
                                            <div style={{ fontSize: '12px', color: 'var(--text-primary)', fontWeight: '500', marginTop: '2px' }}>
                                                {incident.rule_name}
                                            </div>
                                        </td>
                                        <td style={{ padding: '12px', textAlign: 'center' }}>
                                            {(incident.max_matches ?? 0) > 0 ? (
                                                <span style={{
                                                    padding: '4px 10px',
                                                    borderRadius: '12px',
                                                    fontSize: '12px',
                                                    fontWeight: '600',
                                                    color: 'white',
                                                    backgroundColor: (incident.max_matches ?? 0) >= 10 ? '#dc2626' : (incident.max_matches ?? 0) >= 5 ? '#f59e0b' : '#10b981'
                                                }}>
                                                    {incident.max_matches}
                                                </span>
                                            ) : (
                                                <span style={{ color: 'var(--text-muted)' }}>—</span>
                                            )}
                                        </td>
                                        {action === 'TOTAL' && (
                                            <td style={{ padding: '12px', textAlign: 'center' }}>
                                                <span style={{
                                                    padding: '4px 10px',
                                                    borderRadius: '4px',
                                                    fontSize: '10px',
                                                    fontWeight: '700',
                                                    textTransform: 'uppercase',
                                                    color: 'white',
                                                    backgroundColor:
                                                        incident.action?.toUpperCase() === 'BLOCK' || incident.action?.toUpperCase() === 'BLOCKED' ? '#ef4444' :
                                                            incident.action?.toUpperCase() === 'QUARANTINE' || incident.action?.toUpperCase() === 'QUARANTINED' ? '#9013ff' :
                                                                incident.action?.toUpperCase() === 'AUTHORIZED' ? '#10b981' :
                                                                    incident.action?.toUpperCase() === 'RELEASED' ? '#f59e0b' : '#6b7280'
                                                }}>
                                                    {incident.action || 'N/A'}
                                                </span>
                                            </td>
                                        )}
                                        <td style={{ padding: '12px', fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'right', whiteSpace: 'nowrap', minWidth: '120px' }}>
                                            <div>{incident.timestamp.split(' ')[0]}</div>
                                            <div style={{ fontWeight: '600', color: 'var(--text-primary)' }}>
                                                {incident.timestamp.split(' ')[1] || incident.timestamp}
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </div>

                {/* Pagination Footer */}
                {totalPages > 1 && (
                    <div style={{
                        padding: '16px 24px',
                        borderTop: '1px solid var(--border)',
                        flexShrink: 0,
                        backgroundColor: 'var(--background-secondary)'
                    }}>
                        <Pagination
                            currentPage={page}
                            totalPages={totalPages}
                            totalItems={totalCount}
                            pageSize={pageSize}
                            onPageChange={setPage}
                            showPageInput={true}
                            showFirstLast={true}
                            showTotalItems={true}
                        />
                    </div>
                )}
            </div>
        </>
    )
}