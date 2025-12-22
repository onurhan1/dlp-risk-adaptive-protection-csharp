'use client'

import { useEffect, useState, useMemo } from 'react'

interface ActionIncident {
    login_name: string
    destination: string
    channel: string
    policy: string
    rule_name: string
    timestamp: string
}

interface ActionIncidentsModalProps {
    isOpen: boolean
    onClose: () => void
    action: string
    date: string
    incidents: ActionIncident[]
    loading?: boolean
}

export default function ActionIncidentsModal({
    isOpen,
    onClose,
    action,
    date,
    incidents,
    loading = false
}: ActionIncidentsModalProps) {
    // Search filter states
    const [filters, setFilters] = useState({
        login_name: '',
        destination: '',
        channel: '',
        policy: ''
    })

    // Reset filters when modal opens
    useEffect(() => {
        if (isOpen) {
            setFilters({
                login_name: '',
                destination: '',
                channel: '',
                policy: ''
            })
        }
    }, [isOpen])

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

    // Filtered incidents based on search filters
    const filteredIncidents = useMemo(() => {
        return incidents.filter(incident => {
            const matchLoginName = !filters.login_name ||
                incident.login_name?.toLowerCase().includes(filters.login_name.toLowerCase())
            const matchDestination = !filters.destination ||
                incident.destination?.toLowerCase().includes(filters.destination.toLowerCase())
            const matchChannel = !filters.channel ||
                incident.channel?.toLowerCase().includes(filters.channel.toLowerCase())
            const matchPolicy = !filters.policy ||
                incident.policy?.toLowerCase().includes(filters.policy.toLowerCase()) ||
                incident.rule_name?.toLowerCase().includes(filters.policy.toLowerCase())

            return matchLoginName && matchDestination && matchChannel && matchPolicy
        })
    }, [incidents, filters])

    // Check if any filter is active
    const hasActiveFilters = Object.values(filters).some(f => f.length > 0)

    // Clear all filters
    const clearFilters = () => {
        setFilters({
            login_name: '',
            destination: '',
            channel: '',
            policy: ''
        })
    }

    if (!isOpen) return null

    const actionColors = {
        BLOCK: '#ef4444',
        QUARANTINE: '#9013ff'
    }

    const actionColor = actionColors[action as keyof typeof actionColors] || '#3b82f6'

    const inputStyle = {
        width: '100%',
        padding: '6px 10px',
        fontSize: '12px',
        border: '1px solid var(--border)',
        borderRadius: '4px',
        backgroundColor: 'var(--background)',
        color: 'var(--text-primary)',
        outline: 'none',
        transition: 'border-color 0.2s'
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
                    padding: '24px',
                    borderBottom: '1px solid var(--border)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    flexShrink: 0
                }}>
                    <div>
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
                        </h2>
                        <p style={{
                            margin: '4px 0 0 0',
                            fontSize: '13px',
                            color: 'var(--text-muted)'
                        }}>
                            {date} • Showing {filteredIncidents.length} of {incidents.length} incident{incidents.length !== 1 ? 's' : ''}
                            {hasActiveFilters && (
                                <button
                                    onClick={clearFilters}
                                    style={{
                                        marginLeft: '12px',
                                        padding: '2px 8px',
                                        fontSize: '11px',
                                        backgroundColor: 'var(--warning)',
                                        color: '#000',
                                        border: 'none',
                                        borderRadius: '4px',
                                        cursor: 'pointer',
                                        fontWeight: '600'
                                    }}
                                >
                                    Clear Filters
                                </button>
                            )}
                        </p>
                    </div>
                    <button
                        onClick={onClose}
                        style={{
                            background: 'transparent',
                            border: 'none',
                            fontSize: '24px',
                            cursor: 'pointer',
                            color: 'var(--text-secondary)',
                            padding: '4px 8px',
                            borderRadius: '6px',
                            transition: 'all 0.2s'
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.backgroundColor = 'var(--surface-hover)'
                            e.currentTarget.style.color = 'var(--text-primary)'
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.backgroundColor = 'transparent'
                            e.currentTarget.style.color = 'var(--text-secondary)'
                        }}
                    >
                        ×
                    </button>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '24px' }}>
                    {loading ? (
                        <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            padding: '60px',
                            color: 'var(--text-muted)'
                        }}>
                            Loading incidents...
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
                            <div style={{ fontSize: '16px', fontWeight: '500' }}>No {action.toLowerCase()} incidents</div>
                            <div style={{ fontSize: '13px', marginTop: '4px' }}>on {date}</div>
                        </div>
                    ) : (
                        <table style={{
                            width: '100%',
                            borderCollapse: 'collapse'
                        }}>
                            <thead>
                                {/* Column Headers */}
                                <tr style={{
                                    backgroundColor: 'var(--background-secondary)',
                                    borderBottom: '1px solid var(--border)'
                                }}>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase', width: '40px' }}>#</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Login Name</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Destination</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Channel</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Policy/Rule</th>
                                    <th style={{ padding: '12px', textAlign: 'right', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Date/Time</th>
                                </tr>
                                {/* Search Filter Row */}
                                <tr style={{
                                    backgroundColor: 'var(--surface)',
                                    borderBottom: '2px solid var(--border)'
                                }}>
                                    <td style={{ padding: '8px 12px' }}>
                                        <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>🔍</span>
                                    </td>
                                    <td style={{ padding: '8px 12px' }}>
                                        <input
                                            type="text"
                                            placeholder="Search login..."
                                            value={filters.login_name}
                                            onChange={(e) => setFilters(f => ({ ...f, login_name: e.target.value }))}
                                            style={inputStyle}
                                        />
                                    </td>
                                    <td style={{ padding: '8px 12px' }}>
                                        <input
                                            type="text"
                                            placeholder="Search destination..."
                                            value={filters.destination}
                                            onChange={(e) => setFilters(f => ({ ...f, destination: e.target.value }))}
                                            style={inputStyle}
                                        />
                                    </td>
                                    <td style={{ padding: '8px 12px' }}>
                                        <input
                                            type="text"
                                            placeholder="Search channel..."
                                            value={filters.channel}
                                            onChange={(e) => setFilters(f => ({ ...f, channel: e.target.value }))}
                                            style={inputStyle}
                                        />
                                    </td>
                                    <td style={{ padding: '8px 12px' }}>
                                        <input
                                            type="text"
                                            placeholder="Search policy/rule..."
                                            value={filters.policy}
                                            onChange={(e) => setFilters(f => ({ ...f, policy: e.target.value }))}
                                            style={inputStyle}
                                        />
                                    </td>
                                    <td style={{ padding: '8px 12px' }}></td>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredIncidents.length === 0 ? (
                                    <tr>
                                        <td colSpan={6} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                                            No incidents match your filter criteria
                                        </td>
                                    </tr>
                                ) : (
                                    filteredIncidents.map((incident, idx) => (
                                        <tr
                                            key={idx}
                                            style={{
                                                borderBottom: '1px solid var(--border)',
                                                transition: 'background 0.2s'
                                            }}
                                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--surface-hover)'}
                                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                        >
                                            <td style={{ padding: '12px', fontSize: '14px', color: 'var(--text-secondary)' }}>{idx + 1}</td>
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
                                            <td style={{ padding: '12px', fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'right', whiteSpace: 'nowrap', minWidth: '120px' }}>
                                                <div>{incident.timestamp.split(' ')[0]}</div>
                                                <div style={{ fontWeight: '600', color: 'var(--text-primary)' }}>
                                                    {incident.timestamp.split(' ')[1] || incident.timestamp}
                                                </div>
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    )}
                </div>
            </div>
        </>
    )
}
