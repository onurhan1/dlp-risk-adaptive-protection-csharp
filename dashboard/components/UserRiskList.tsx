'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

import { API_URL } from '@/lib/api-config'

interface UserRisk {
  user_email: string
  risk_score: number
  total_incidents: number
}

interface UserRiskListProps {
  onUserSelect: (user: string) => void
  selectedUser?: string
}

export default function UserRiskList({ onUserSelect, selectedUser }: UserRiskListProps) {
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
    } finally {
      setLoading(false)
    }
  }

  const getRiskColor = (score: number): string => {
    if (score >= 80) return '#f44336' // Red
    if (score >= 50) return '#ff9800' // Orange
    return '#4caf50' // Green
  }

  const startItem = (page - 1) * pageSize + 1
  const endItem = Math.min(page * pageSize, total)

  return (
    <div className="user-risk-list">
      <div className="header">
        <span>Risk</span>
        <span>User</span>
      </div>
      
      <div className="user-list">
        {loading ? (
          <div className="loading">Loading users...</div>
        ) : users.length === 0 ? (
          <div className="empty">No users found</div>
        ) : (
          users.map((user, idx) => (
            <div
              key={idx}
              className={`user-item ${selectedUser === user.user_email ? 'selected' : ''}`}
              onClick={() => onUserSelect(user.user_email)}
            >
              <div className="risk-score">
                <svg width="40" height="40" className="risk-circle">
                  <circle
                    cx="20"
                    cy="20"
                    r="18"
                    fill="none"
                    stroke="#e0e0e0"
                    strokeWidth="3"
                  />
                  <circle
                    cx="20"
                    cy="20"
                    r="18"
                    fill="none"
                    stroke={getRiskColor(user.risk_score)}
                    strokeWidth="3"
                    strokeDasharray={`${(user.risk_score / 100) * 113} 113`}
                    strokeDashoffset="0"
                    transform="rotate(-90 20 20)"
                  />
                  <text
                    x="20"
                    y="25"
                    textAnchor="middle"
                    fontSize="12"
                    fontWeight="bold"
                    fill={getRiskColor(user.risk_score)}
                  >
                    {user.risk_score}
                  </text>
                </svg>
              </div>
              <div className="user-info">
                <div className="user-name">{user.user_email}</div>
              </div>
            </div>
          ))
        )}
      </div>

      {total > 0 && (
        <div className="pagination">
          <button 
            onClick={() => setPage(p => Math.max(1, p - 1))} 
            disabled={page === 1}
          >
            Previous
          </button>
          <span>{startItem}-{endItem} of {total} items</span>
          <button 
            onClick={() => setPage(p => p + 1)} 
            disabled={endItem >= total}
          >
            Next
          </button>
        </div>
      )}

      <style jsx>{`
        .user-risk-list {
          background: white;
          border-radius: 8px;
          padding: 16px;
          height: 100%;
          display: flex;
          flex-direction: column;
        }

        .header {
          display: flex;
          justify-content: space-between;
          padding: 8px 12px;
          font-weight: 600;
          color: #666;
          border-bottom: 1px solid #e0e0e0;
          margin-bottom: 8px;
        }

        .user-list {
          flex: 1;
          overflow-y: auto;
        }

        .user-item {
          display: flex;
          align-items: center;
          padding: 12px;
          cursor: pointer;
          border-radius: 4px;
          transition: background-color 0.2s;
        }

        .user-item:hover {
          background-color: #f5f5f5;
        }

        .user-item.selected {
          background-color: #e3f2fd;
          border-left: 3px solid #2196f3;
        }

        .risk-score {
          margin-right: 12px;
        }

        .risk-circle {
          width: 40px;
          height: 40px;
        }

        .user-info {
          flex: 1;
        }

        .user-name {
          font-weight: 500;
          color: #333;
        }

        .loading,
        .empty {
          text-align: center;
          padding: 20px;
          color: #666;
        }

        .pagination {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 12px;
          border-top: 1px solid #e0e0e0;
          margin-top: 8px;
          font-size: 12px;
        }

        .pagination button {
          padding: 6px 12px;
          border: 1px solid #ddd;
          background: white;
          border-radius: 4px;
          cursor: pointer;
          font-size: 12px;
        }

        .pagination button:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }
      `}</style>
    </div>
  )
}

