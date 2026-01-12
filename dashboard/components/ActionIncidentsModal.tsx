'use client'

import { useEffect, useState, useCallback } from 'react'
import axios from 'axios'
import { format, subDays } from 'date-fns'
import { getApiUrlDynamic } from '@/lib/api-config'

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

export default function ActionIncidentsModal({
    isOpen,
    onClose,
    action,
    initialDate  // Single day mode for Reports page
}: ActionIncidentsModalProps) {
    // Single day mode when initialDate is provided
    const isSingleDayMode = !!initialDate

    // Date range state
    const [dateRange, setDateRange] = useState({
        start: initialDate || format(subDays(new Date(), 30), 'yyyy-MM-dd'),
        end: initialDate || format(new Date(), 'yyyy-MM-dd')
    })

    // Update date range when initialDate changes
    useEffect(() => {
        if (initialDate) {
            setDateRange({ start: initialDate, end: initialDate })
        }
    }, [initialDate])

    // Pagination state
    const [page, setPage] = useState(1)
    const [pageSize] = useState(100)
    const [totalCount, setTotalCount] = useState(0)
    const [totalPages, setTotalPages] = useState(0)

    // Filter states
    const [filters, setFilters] = useState({
        search: '',
        channel: '',
        policy: ''
    })

    // Debounced filters for server-side search
    const debouncedSearch = useDebounce(filters.search, 500)
    const debouncedChannel = useDebounce(filters.channel, 500)
    const debouncedPolicy = useDebounce(filters.policy, 500)

    const [incidents, setIncidents] = useState<ActionIncident[]>([])
    const [loading, setLoading] = useState(false)

    // Fetch incidents when modal opens or filters/pagination change
    useEffect(() => {
        if (isOpen) {
            fetchIncidents()
        }
    }, [isOpen, dateRange.start, dateRange.end, action, page, debouncedSearch, debouncedChannel, debouncedPolicy])

    // Reset page when filters change
    useEffect(() => {
        setPage(1)
    }, [debouncedSearch, debouncedChannel, debouncedPolicy, dateRange.start, dateRange.end])

    // Reset all when modal opens
    useEffect(() => {
        if (isOpen) {
            setFilters({ search: '', channel: '', policy: '' })
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
                    search: debouncedSearch || undefined,
                    channel: debouncedChannel || undefined,
                    policy: debouncedPolicy || undefined
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
    const hasActiveFilters = filters.search || filters.channel || filters.policy

    // Clear all filters
    const clearFilters = () => {
        setFilters({ search: '', channel: '', policy: '' })
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

    const inputStyle = {
        width: '100%',
        padding: '8px 12px',
        fontSize: '13px',
        border: '1px solid var(--border)',
        borderRadius: '6px',
        backgroundColor: 'var(--background)',
        color: 'var(--text-primary)',
        outline: 'none',
        transition: 'border-color 0.2s'
    }

    const dateInputStyle = {
        padding: '8px 12px',
        fontSize: '13px',
        border: '1px solid var(--border)',
        borderRadius: '6px',
        backgroundColor: 'var(--background)',
        color: 'var(--text-primary)',
        outline: 'none'
    }

    const pageButtonStyle = (disabled: boolean) => ({
        padding: '8px 16px',
        fontSize: '13px',
        border: '1px solid var(--border)',
        borderRadius: '6px',
        backgroundColor: disabled ? 'var(--background-secondary)' : 'var(--surface)',
        color: disabled ? 'var(--text-muted)' : 'var(--text-primary)',
        cursor: disabled ? 'not-allowed' : 'pointer',
        fontWeight: '500'
    })

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

                    {/* Filters Row */}
                    <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'center' }}>
                        {/* Date - Single day mode shows label, otherwise shows date range picker */}
                        {isSingleDayMode ? (
                            <div style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '8px',
                                backgroundColor: 'var(--background-secondary)',
                                padding: '8px 16px',
                                borderRadius: '8px'
                            }}>
                                <span style={{ fontSize: '14px', color: 'var(--text-muted)' }}>📅</span>
                                <span style={{ fontWeight: '600', color: 'var(--text-primary)' }}>
                                    {new Date(dateRange.start).toLocaleDateString('en-US', {
                                        weekday: 'long',
                                        year: 'numeric',
                                        month: 'long',
                                        day: 'numeric'
                                    })}
                                </span>
                            </div>
                        ) : (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>📅</span>
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
                            </div>
                        )}

                        <div style={{ width: '1px', height: '24px', backgroundColor: 'var(--border)' }} />

                        {/* Search */}
                        <div style={{ flex: 1, minWidth: '200px', maxWidth: '300px' }}>
                            <input
                                type="text"
                                placeholder="🔍 Search user, destination..."
                                value={filters.search}
                                onChange={(e) => setFilters(f => ({ ...f, search: e.target.value }))}
                                style={inputStyle}
                            />
                        </div>

                        {/* Channel */}
                        <div style={{ minWidth: '150px' }}>
                            <input
                                type="text"
                                placeholder="Channel"
                                value={filters.channel}
                                onChange={(e) => setFilters(f => ({ ...f, channel: e.target.value }))}
                                style={inputStyle}
                            />
                        </div>

                        {/* Policy */}
                        <div style={{ minWidth: '150px' }}>
                            <input
                                type="text"
                                placeholder="Policy"
                                value={filters.policy}
                                onChange={(e) => setFilters(f => ({ ...f, policy: e.target.value }))}
                                style={inputStyle}
                            />
                        </div>

                        {hasActiveFilters && (
                            <button
                                onClick={clearFilters}
                                style={{
                                    padding: '8px 16px',
                                    fontSize: '12px',
                                    backgroundColor: 'var(--warning)',
                                    color: '#000',
                                    border: 'none',
                                    borderRadius: '6px',
                                    cursor: 'pointer',
                                    fontWeight: '600'
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
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        flexShrink: 0,
                        backgroundColor: 'var(--background-secondary)'
                    }}>
                        <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                            Showing {((page - 1) * pageSize) + 1} - {Math.min(page * pageSize, totalCount)} of {totalCount.toLocaleString()}
                        </div>
                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                            <button
                                onClick={() => setPage(1)}
                                disabled={page === 1}
                                style={pageButtonStyle(page === 1)}
                            >
                                ⏮ First
                            </button>
                            <button
                                onClick={() => setPage(p => Math.max(1, p - 1))}
                                disabled={page === 1}
                                style={pageButtonStyle(page === 1)}
                            >
                                ← Prev
                            </button>
                            <span style={{
                                padding: '8px 16px',
                                backgroundColor: 'var(--primary)',
                                color: 'white',
                                borderRadius: '6px',
                                fontWeight: '600',
                                fontSize: '13px'
                            }}>
                                {page} / {totalPages}
                            </span>
                            <button
                                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                                disabled={page === totalPages}
                                style={pageButtonStyle(page === totalPages)}
                            >
                                Next →
                            </button>
                            <button
                                onClick={() => setPage(totalPages)}
                                disabled={page === totalPages}
                                style={pageButtonStyle(page === totalPages)}
                            >
                                Last ⏭
                            </button>
                        </div>
                    </div>
                )}
            </div>
        </>
    )
}
