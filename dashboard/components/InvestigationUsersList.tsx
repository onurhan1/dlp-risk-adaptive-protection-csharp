'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001'

interface UserRisk {
  user_email: string
  risk_score: number
  total_incidents: number
}

interface InvestigationUsersListProps {
  onUserSelect: (user: string, riskScore: number) => void
  selectedUser?: string
  searchQuery: string
  filterRisk: string
}

export default function InvestigationUsersList({
  onUserSelect,
  selectedUser,
  searchQuery,
  filterRisk
}: InvestigationUsersListProps) {
  const [users, setUsers] = useState<UserRisk[]>([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const pageSize = 15

  useEffect(() => {
    fetchUsers()
  }, [page])

  const fetchUsers = async () => {
    setLoading(true)
    try {
      const response = await axios.get(`${API_URL}/api/risk/user-list`, {
        params: { page, page_size: pageSize }
      })
      // Handle both old format (userEmail, maxRiskScore) and new format (user_email, risk_score)
      const usersData = (response.data.users || []).map((user: any) => ({
        user_email: user.user_email || user.userEmail || '',
        risk_score: user.risk_score || user.maxRiskScore || 0,
        total_incidents: user.total_incidents || user.totalIncidents || 0
      }))
      setUsers(usersData)
      setTotal(response.data.total || 0)
    } catch (error) {
      console.error('Error fetching users:', error)
      // Fallback sample data
      setUsers([
        { user_email: 'fabiano.cese@example.com', risk_score: 98, total_incidents: 15 },
        { user_email: 'william.rodrigues@example.com', risk_score: 84, total_incidents: 12 },
        { user_email: 'jenny.wilson@example.com', risk_score: 75, total_incidents: 8 },
        { user_email: 'elizabeth.taylor@example.com', risk_score: 68, total_incidents: 6 },
        { user_email: 'agustin.moreno@example.com', risk_score: 44, total_incidents: 4 },
        { user_email: 'becky.goodhair@example.com', risk_score: 20, total_incidents: 2 }
      ])
      setTotal(6)
    } finally {
      setLoading(false)
    }
  }

  const getRiskColor = (score: number): string => {
    if (score >= 80) return '#ef4444' // Red - Critical
    if (score >= 50) return '#f59e0b' // Orange - High
    if (score >= 30) return '#fbbf24' // Yellow - Medium
    return '#10b981' // Green - Low
  }

  // Filter users
  const filteredUsers = users.filter(user => {
    // Search filter
    if (searchQuery && !user.user_email.toLowerCase().includes(searchQuery.toLowerCase())) {
      return false
    }
    // Risk filter
    if (filterRisk === 'critical' && user.risk_score < 80) return false
    if (filterRisk === 'high' && (user.risk_score < 50 || user.risk_score >= 80)) return false
    if (filterRisk === 'medium' && (user.risk_score < 30 || user.risk_score >= 50)) return false
    if (filterRisk === 'low' && user.risk_score >= 30) return false
    return true
  })

  const startItem = (page - 1) * pageSize + 1
  const endItem = Math.min(page * pageSize, total)

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Table Header */}
      <div style={{ display: 'grid', gridTemplateColumns: '80px 1fr', padding: '12px 16px', background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
        <span style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Risk</span>
        <span style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>User</span>
      </div>

      {/* User List */}
      <div style={{ flex: 1, overflowY: 'auto' }}>
        {loading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            Loading users...
          </div>
        ) : filteredUsers.length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            No users found
          </div>
        ) : (
          filteredUsers.map((user, idx) => (
            <div
              key={idx}
              onClick={() => onUserSelect(user.user_email, user.risk_score)}
              style={{
                display: 'grid',
                gridTemplateColumns: '80px 1fr',
                padding: '12px 16px',
                cursor: 'pointer',
                borderBottom: '1px solid var(--border)',
                background: selectedUser === user.user_email ? 'rgba(0, 168, 232, 0.1)' : 'transparent',
                borderLeft: selectedUser === user.user_email ? '4px solid var(--primary)' : 'none',
                transition: 'all 0.2s'
              }}
              onMouseEnter={(e) => {
                if (selectedUser !== user.user_email) {
                  e.currentTarget.style.background = 'var(--surface-hover)'
                }
              }}
              onMouseLeave={(e) => {
                if (selectedUser !== user.user_email) {
                  e.currentTarget.style.background = 'transparent'
                }
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <div style={{ position: 'relative', width: '40px', height: '40px' }}>
                  <svg style={{ width: '40px', height: '40px', transform: 'rotate(-90deg)' }}>
                    <circle
                      cx="20"
                      cy="20"
                      r="16"
                      fill="none"
                      stroke="var(--border)"
                      strokeWidth="4"
                    />
                    <circle
                      cx="20"
                      cy="20"
                      r="16"
                      fill="none"
                      stroke={getRiskColor(user.risk_score)}
                      strokeWidth="4"
                      strokeDasharray={`${(user.risk_score / 100) * 100.5} 100.5`}
                      strokeLinecap="round"
                    />
                  </svg>
                  <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <span
                      style={{ fontSize: '12px', fontWeight: 'bold', color: getRiskColor(user.risk_score) }}
                    >
                      {user.risk_score}
                    </span>
                  </div>
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center' }}>
                <span style={{ fontSize: '14px', color: 'var(--text-primary)', fontWeight: '500' }}>{user.user_email}</span>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Pagination */}
      {total > 0 && (
        <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', background: 'var(--background-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)' }}>
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            style={{
              padding: '4px 12px',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              background: 'var(--surface)',
              color: 'var(--text-primary)',
              cursor: page === 1 ? 'not-allowed' : 'pointer',
              opacity: page === 1 ? 0.5 : 1,
              transition: 'all 0.2s'
            }}
            onMouseEnter={(e) => {
              if (page !== 1) {
                e.currentTarget.style.background = 'var(--surface-hover)'
              }
            }}
            onMouseLeave={(e) => {
              if (page !== 1) {
                e.currentTarget.style.background = 'var(--surface)'
              }
            }}
          >
            Previous
          </button>
          <span>{startItem}-{endItem} of {total} items</span>
          <button
            onClick={() => setPage(p => p + 1)}
            disabled={endItem >= total}
            style={{
              padding: '4px 12px',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              background: 'var(--surface)',
              color: 'var(--text-primary)',
              cursor: endItem >= total ? 'not-allowed' : 'pointer',
              opacity: endItem >= total ? 0.5 : 1,
              transition: 'all 0.2s'
            }}
            onMouseEnter={(e) => {
              if (endItem < total) {
                e.currentTarget.style.background = 'var(--surface-hover)'
              }
            }}
            onMouseLeave={(e) => {
              if (endItem < total) {
                e.currentTarget.style.background = 'var(--surface)'
              }
            }}
          >
            Next
          </button>
        </div>
      )}
    </div>
  )
}

