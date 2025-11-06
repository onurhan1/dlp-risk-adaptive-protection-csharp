'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

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
      setUsers(response.data.users || [])
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
    <div className="flex flex-col h-full">
      {/* Table Header */}
      <div className="grid grid-cols-[80px_1fr] px-4 py-3 bg-gray-50 border-b border-gray-200">
        <span className="text-xs font-semibold text-gray-600 uppercase">Risk</span>
        <span className="text-xs font-semibold text-gray-600 uppercase">User</span>
      </div>

      {/* User List */}
      <div className="flex-1 overflow-y-auto">
        {loading ? (
          <div className="flex items-center justify-center py-8 text-gray-500">
            Loading users...
          </div>
        ) : filteredUsers.length === 0 ? (
          <div className="flex items-center justify-center py-8 text-gray-500">
            No users found
          </div>
        ) : (
          filteredUsers.map((user, idx) => (
            <div
              key={idx}
              onClick={() => onUserSelect(user.user_email, user.risk_score)}
              className={`grid grid-cols-[80px_1fr] px-4 py-3 cursor-pointer border-b border-gray-100 hover:bg-gray-50 transition-colors ${
                selectedUser === user.user_email ? 'bg-teal-50 border-l-4 border-l-teal-500' : ''
              }`}
            >
              <div className="flex items-center justify-center">
                <div className="relative w-10 h-10">
                  <svg className="w-10 h-10 transform -rotate-90">
                    <circle
                      cx="20"
                      cy="20"
                      r="16"
                      fill="none"
                      stroke="#e5e7eb"
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
                  <div className="absolute inset-0 flex items-center justify-center">
                    <span
                      className="text-xs font-bold"
                      style={{ color: getRiskColor(user.risk_score) }}
                    >
                      {user.risk_score}
                    </span>
                  </div>
                </div>
              </div>
              <div className="flex items-center">
                <span className="text-sm text-gray-900 font-medium">{user.user_email}</span>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Pagination */}
      {total > 0 && (
        <div className="px-4 py-3 border-t border-gray-200 bg-gray-50 flex items-center justify-between text-xs text-gray-600">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Previous
          </button>
          <span>{startItem}-{endItem} of {total} items</span>
          <button
            onClick={() => setPage(p => p + 1)}
            disabled={endItem >= total}
            className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Next
          </button>
        </div>
      )}
    </div>
  )
}

