'use client'

import { useState, useEffect, useMemo } from 'react'
import apiClient from '@/lib/axios'

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
  const [allUsers, setAllUsers] = useState<UserRisk[]>([])  // All users from API
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const pageSize = 15

  // Fetch ALL users once on mount (no pagination for client-side filtering)
  useEffect(() => {
    fetchAllUsers()
  }, [])

  const fetchAllUsers = async () => {
    setLoading(true)
    try {
      // Fetch large page to get all users
      const response = await apiClient.get('/api/risk/user-list', {
        params: {
          page: 1,
          page_size: 5000  // Get all users at once
        }
      })
      const usersData = (response.data.users || []).map((user: any) => {
        const rawScore = user.risk_score || user.maxRiskScore || 0
        const normalizedScore = rawScore > 100 ? Math.round(rawScore / 10) : rawScore
        return {
          user_email: user.user_email || user.userEmail || '',
          risk_score: normalizedScore,
          total_incidents: user.total_incidents || user.totalIncidents || 0
        }
      })
      setAllUsers(usersData)
    } catch (error) {
      console.error('Error fetching users:', error)
      setAllUsers([])
    } finally {
      setLoading(false)
    }
  }

  // CLIENT-SIDE filtering with useMemo (like ActionIncidentsModal)
  const filteredUsers = useMemo(() => {
    return allUsers.filter(user => {
      // Search filter
      const matchSearch = !searchQuery ||
        user.user_email.toLowerCase().includes(searchQuery.toLowerCase())

      // Risk filter  
      let matchRisk = true
      if (filterRisk === 'critical' && user.risk_score < 80) matchRisk = false
      if (filterRisk === 'high' && (user.risk_score < 50 || user.risk_score >= 80)) matchRisk = false
      if (filterRisk === 'medium' && (user.risk_score < 30 || user.risk_score >= 50)) matchRisk = false
      if (filterRisk === 'low' && user.risk_score >= 30) matchRisk = false

      return matchSearch && matchRisk
    })
  }, [allUsers, searchQuery, filterRisk])

  // Reset to page 1 when search or filter changes
  useEffect(() => {
    setPage(1)
  }, [searchQuery, filterRisk])

  // Paginate the filtered results
  const paginatedUsers = useMemo(() => {
    const start = (page - 1) * pageSize
    return filteredUsers.slice(start, start + pageSize)
  }, [filteredUsers, page, pageSize])

  const total = filteredUsers.length
  const startItem = total === 0 ? 0 : (page - 1) * pageSize + 1
  const endItem = Math.min(page * pageSize, total)

  const getRiskColor = (score: number): string => {
    if (score >= 80) return '#ef4444' // Red - Critical
    if (score >= 50) return '#f59e0b' // Orange - High
    if (score >= 30) return '#fbbf24' // Yellow - Medium
    return '#10b981' // Green - Low
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0, flex: 1 }}>
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
        ) : paginatedUsers.length === 0 ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', color: 'var(--text-muted)' }}>
            No users found
          </div>
        ) : (
          paginatedUsers.map((user, idx) => (
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
        <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', background: 'var(--background-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', flexShrink: 0 }}>
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