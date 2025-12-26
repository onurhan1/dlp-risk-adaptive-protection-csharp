'use client'

import { useEffect, useState } from 'react'
import axios from 'axios'

interface HighRiskUser {
    user_email: string
    login_name: string
    department: string
    max_risk_score: number
    incident_count: number
    risk_level: string
}

interface HighRiskUsersModalProps {
    isOpen: boolean
    onClose: () => void
    date: string
}

export default function HighRiskUsersModal({ isOpen, onClose, date }: HighRiskUsersModalProps) {
    const [users, setUsers] = useState<HighRiskUser[]>([])
    const [loading, setLoading] = useState(false)

    useEffect(() => {
        if (isOpen && date) {
            fetchHighRiskUsers()
        }
    }, [isOpen, date])

    const fetchHighRiskUsers = async () => {
        setLoading(true)
        try {
            const apiUrl = `${window.location.protocol}//${window.location.hostname}:5001`
            const response = await axios.get(`${apiUrl}/api/risk/high-risk-users`, {
                params: { date }
            })
            // Normalize scores: if > 100, it's on 1000-scale, divide by 10
            const normalizedUsers = response.data
                .map((user: any) => ({
                    ...user,
                    max_risk_score: user.max_risk_score > 100
                        ? Math.round(user.max_risk_score / 10)
                        : user.max_risk_score
                }))
                .sort((a: any, b: any) => b.max_risk_score - a.max_risk_score)  // Sort by score descending
            setUsers(normalizedUsers)
        } catch (error) {
            console.error('Error fetching high-risk users:', error)
            setUsers([])
        } finally {
            setLoading(false)
        }
    }

    // Close on ESC
    useEffect(() => {
        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onClose()
        }
        if (isOpen) {
            window.addEventListener('keydown', handleEscape)
            return () => window.removeEventListener('keydown', handleEscape)
        }
    }, [isOpen, onClose])

    if (!isOpen) return null

    const getRiskColor = (score: number): string => {
        if (score >= 91) return '#ef4444'
        if (score >= 61) return '#f59e0b'
        return '#10b981'
    }

    // Format date for display
    let formattedDate = date
    try {
        const d = new Date(date)
        formattedDate = d.toLocaleDateString('en-US', { weekday: 'short', day: '2-digit', month: 'short', year: 'numeric' })
    } catch { }

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
                    maxWidth: '800px',
                    maxHeight: '80vh',
                    display: 'flex',
                    flexDirection: 'column'
                }}
            >
                {/* Header */}
                <div style={{
                    padding: '20px 24px',
                    borderBottom: '1px solid var(--border)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center'
                }}>
                    <div>
                        <h2 style={{
                            margin: 0,
                            fontSize: '18px',
                            fontWeight: '600',
                            color: 'var(--text-primary)',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '10px'
                        }}>
                            <span style={{
                                width: '8px',
                                height: '8px',
                                borderRadius: '50%',
                                backgroundColor: '#ef4444'
                            }} />
                            High Risk Users
                        </h2>
                        <p style={{
                            margin: '4px 0 0 0',
                            fontSize: '13px',
                            color: 'var(--text-muted)'
                        }}>
                            {formattedDate} • {users.length} user{users.length !== 1 ? 's' : ''} with risk score ≥ 50
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
                            borderRadius: '6px'
                        }}
                    >
                        ×
                    </button>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '16px 24px' }}>
                    {loading ? (
                        <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            padding: '40px',
                            color: 'var(--text-muted)'
                        }}>
                            Loading high-risk users...
                        </div>
                    ) : users.length === 0 ? (
                        <div style={{
                            display: 'flex',
                            flexDirection: 'column',
                            alignItems: 'center',
                            justifyContent: 'center',
                            padding: '40px',
                            color: 'var(--text-muted)'
                        }}>
                            <div style={{ fontSize: '48px', marginBottom: '16px' }}>✅</div>
                            <div style={{ fontSize: '16px', fontWeight: '500' }}>No high-risk users</div>
                            <div style={{ fontSize: '13px', marginTop: '4px' }}>on {formattedDate}</div>
                        </div>
                    ) : (
                        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead>
                                <tr style={{ backgroundColor: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>User</th>
                                    <th style={{ padding: '12px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Department</th>
                                    <th style={{ padding: '12px', textAlign: 'center', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Risk Score</th>
                                    <th style={{ padding: '12px', textAlign: 'right', fontSize: '11px', fontWeight: '700', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Incidents</th>
                                </tr>
                            </thead>
                            <tbody>
                                {users.map((user, idx) => (
                                    <tr
                                        key={idx}
                                        style={{
                                            borderBottom: '1px solid var(--border)',
                                            transition: 'background 0.2s'
                                        }}
                                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--surface-hover)'}
                                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                    >
                                        <td style={{ padding: '12px' }}>
                                            <div style={{ fontSize: '14px', color: 'var(--text-primary)', fontWeight: '500' }}>
                                                {user.login_name}
                                            </div>
                                            <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                                                {user.user_email}
                                            </div>
                                        </td>
                                        <td style={{ padding: '12px', fontSize: '13px', color: 'var(--text-primary)' }}>
                                            {user.department || '—'}
                                        </td>
                                        <td style={{ padding: '12px', textAlign: 'center' }}>
                                            <span style={{
                                                display: 'inline-block',
                                                padding: '4px 12px',
                                                borderRadius: '12px',
                                                fontSize: '13px',
                                                fontWeight: '600',
                                                color: 'white',
                                                backgroundColor: getRiskColor(user.max_risk_score)
                                            }}>
                                                {user.max_risk_score}
                                            </span>
                                        </td>
                                        <td style={{ padding: '12px', textAlign: 'right', fontSize: '14px', fontWeight: '500', color: 'var(--text-primary)' }}>
                                            {user.incident_count}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </div>
            </div>
        </>
    )
}
