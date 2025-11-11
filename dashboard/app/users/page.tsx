'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { useAuth } from '@/components/AuthProvider'
import { useRouter } from 'next/navigation'

import { getApiUrlDynamic } from '@/lib/api-config'

interface User {
  id: number
  username: string
  email: string
  role: string
  createdAt?: string
  created_at?: string
  isActive?: boolean
  is_active?: boolean
}

export default function UsersPage() {
  const { isAdmin } = useAuth()
  const router = useRouter()
  const [users, setUsers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)
  const [showModal, setShowModal] = useState(false)
  const [editingUser, setEditingUser] = useState<User | null>(null)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    role: 'standard'
  })

  useEffect(() => {
    if (!isAdmin) {
      router.push('/')
      return
    }
    fetchUsers()
  }, [isAdmin, router])

  const fetchUsers = async () => {
    setLoading(true)
    try {
      const token = localStorage.getItem('authToken')
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/users`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      })
      setUsers(response.data.users || [])
    } catch (error: any) {
      console.error('Error fetching users:', error)
      setMessage({ 
        type: 'error', 
        text: error.response?.data?.detail || 'Failed to load users' 
      })
      setTimeout(() => setMessage(null), 5000)
    } finally {
      setLoading(false)
    }
  }

  const handleCreate = () => {
    setEditingUser(null)
    setFormData({ username: '', email: '', password: '', role: 'standard' })
    setShowModal(true)
  }

  const handleEdit = (user: User) => {
    setEditingUser(user)
    setFormData({ 
      username: user.username, 
      email: user.email, 
      password: '', 
      role: user.role 
    })
    setShowModal(true)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setMessage(null)

    try {
      const token = localStorage.getItem('authToken')
      
      if (editingUser) {
        // Update user
        const apiUrl = getApiUrlDynamic()
        await axios.put(
          `${apiUrl}/api/users/${editingUser.id}`,
          {
            username: formData.username,
            email: formData.email,
            role: formData.role
          },
          {
            headers: token ? { Authorization: `Bearer ${token}` } : {}
          }
        )
        setMessage({ type: 'success', text: 'User updated successfully' })
      } else {
        // Create user
        const apiUrl = getApiUrlDynamic()
        await axios.post(
          `${apiUrl}/api/users`,
          formData,
          {
            headers: token ? { Authorization: `Bearer ${token}` } : {}
          }
        )
        setMessage({ type: 'success', text: 'User created successfully' })
      }

      setShowModal(false)
      await fetchUsers()
      setTimeout(() => setMessage(null), 3000)
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to save user'
      setMessage({ type: 'error', text: errorMessage })
      setTimeout(() => setMessage(null), 5000)
    }
  }

  const handleDelete = async (id: number) => {
    if (!confirm('Are you sure you want to delete this user?')) {
      return
    }

    try {
      const token = localStorage.getItem('authToken')
      const apiUrl = getApiUrlDynamic()
      await axios.delete(`${apiUrl}/api/users/${id}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      })
      setMessage({ type: 'success', text: 'User deleted successfully' })
      await fetchUsers()
      setTimeout(() => setMessage(null), 3000)
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to delete user'
      setMessage({ type: 'error', text: errorMessage })
      setTimeout(() => setMessage(null), 5000)
    }
  }

  if (!isAdmin) {
    return null
  }

  if (loading) {
    return (
      <div className="dashboard-page">
        <div className="loading">Loading users...</div>
      </div>
    )
  }

  return (
    <div className="dashboard-page">
      <div className="dashboard-header">
        <div>
          <h1>User Management</h1>
          <p className="dashboard-subtitle">Manage system users and their roles</p>
        </div>
        <button
          onClick={handleCreate}
          style={{
            padding: '12px 24px',
            background: 'var(--primary)',
            color: 'white',
            border: 'none',
            borderRadius: '6px',
            cursor: 'pointer',
            fontWeight: '600',
            fontSize: '14px',
            boxShadow: '0 2px 8px rgba(0, 168, 232, 0.3)'
          }}
        >
          + Add User
        </button>
      </div>

      {message && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '6px',
            marginBottom: '24px',
            background: message.type === 'success' ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
            color: message.type === 'success' ? '#5cb85c' : '#d9534f',
            border: `1px solid ${message.type === 'success' ? 'rgba(92, 184, 92, 0.3)' : 'rgba(217, 83, 79, 0.3)'}`
          }}
        >
          {message.text}
        </div>
      )}

      <div className="card">
        <div className="table-container">
          <table className="data-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Username</th>
                <th>Email</th>
                <th>Role</th>
                <th>Created At</th>
                <th>Status</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    No users found. Create your first user.
                  </td>
                </tr>
              ) : (
                users.map((user) => (
                  <tr key={user.id}>
                    <td>{user.id}</td>
                    <td>{user.username}</td>
                    <td>{user.email}</td>
                    <td>
                      <span
                        style={{
                          padding: '4px 12px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: '600',
                          background: user.role === 'admin' ? 'rgba(0, 168, 232, 0.2)' : 'rgba(100, 116, 139, 0.2)',
                          color: user.role === 'admin' ? 'var(--primary)' : 'var(--text-secondary)'
                        }}
                      >
                        {user.role === 'admin' ? 'Admin' : 'Standard'}
                      </span>
                    </td>
                    <td>{new Date(user.createdAt || user.created_at || Date.now()).toLocaleDateString()}</td>
                    <td>
                      <span
                        style={{
                          padding: '4px 12px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: '600',
                          background: (user.isActive ?? user.is_active ?? true) ? 'rgba(92, 184, 92, 0.2)' : 'rgba(217, 83, 79, 0.2)',
                          color: (user.isActive ?? user.is_active ?? true) ? '#5cb85c' : '#d9534f'
                        }}
                      >
                        {(user.isActive ?? user.is_active ?? true) ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        onClick={() => handleEdit(user)}
                        style={{
                          padding: '6px 12px',
                          marginRight: '8px',
                          background: 'var(--surface-hover)',
                          color: 'var(--text-primary)',
                          border: '1px solid var(--border)',
                          borderRadius: '6px',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '500'
                        }}
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => handleDelete(user.id)}
                        style={{
                          padding: '6px 12px',
                          background: 'rgba(217, 83, 79, 0.1)',
                          color: '#d9534f',
                          border: '1px solid rgba(217, 83, 79, 0.3)',
                          borderRadius: '6px',
                          cursor: 'pointer',
                          fontSize: '12px',
                          fontWeight: '500'
                        }}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal */}
      {showModal && (
        <div
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0, 0, 0, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000
          }}
          onClick={() => setShowModal(false)}
        >
          <div
            style={{
              background: 'var(--surface)',
              borderRadius: '8px',
              padding: '24px',
              width: '90%',
              maxWidth: '500px',
              boxShadow: 'var(--shadow-lg)'
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <h2 style={{ margin: '0 0 20px 0', color: 'var(--text-primary)' }}>
              {editingUser ? 'Edit User' : 'Create New User'}
            </h2>
            <form onSubmit={handleSubmit}>
              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  Username
                </label>
                <input
                  type="text"
                  value={formData.username}
                  onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                  required
                  style={{
                    width: '100%',
                    padding: '10px 14px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--background)',
                    color: 'var(--text-primary)',
                    outline: 'none'
                  }}
                />
              </div>
              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  Email
                </label>
                <input
                  type="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  required
                  style={{
                    width: '100%',
                    padding: '10px 14px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--background)',
                    color: 'var(--text-primary)',
                    outline: 'none'
                  }}
                />
              </div>
              {!editingUser && (
                <div style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                    Password
                  </label>
                  <input
                    type="password"
                    value={formData.password}
                    onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                    required={!editingUser}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      border: '1px solid var(--border)',
                      borderRadius: '6px',
                      fontSize: '14px',
                      background: 'var(--background)',
                      color: 'var(--text-primary)',
                      outline: 'none'
                    }}
                  />
                </div>
              )}
              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: 'var(--text-primary)', fontSize: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  Role
                </label>
                <select
                  value={formData.role}
                  onChange={(e) => setFormData({ ...formData, role: e.target.value })}
                  style={{
                    width: '100%',
                    padding: '10px 14px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    fontSize: '14px',
                    background: 'var(--background)',
                    color: 'var(--text-primary)',
                    outline: 'none'
                  }}
                >
                  <option value="standard">Standard</option>
                  <option value="admin">Admin</option>
                </select>
              </div>
              <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  style={{
                    padding: '10px 20px',
                    background: 'var(--surface-hover)',
                    color: 'var(--text-primary)',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    fontSize: '14px',
                    fontWeight: '500'
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{
                    padding: '10px 20px',
                    background: 'var(--primary)',
                    color: 'white',
                    border: 'none',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    fontSize: '14px',
                    fontWeight: '600'
                  }}
                >
                  {editingUser ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <style jsx>{`
        .dashboard-page {
          background: transparent;
          min-height: calc(100vh - 64px);
          padding: 0;
        }

        .dashboard-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 24px;
        }

        .dashboard-header h1 {
          font-size: 28px;
          font-weight: 700;
          color: var(--text-primary);
          margin: 0 0 8px 0;
          letter-spacing: -0.02em;
        }

        .dashboard-subtitle {
          font-size: 14px;
          color: var(--text-secondary);
          margin: 0;
        }

        .card {
          background: var(--surface);
          border-radius: 6px;
          padding: 24px;
          box-shadow: var(--shadow);
          border: 1px solid var(--border);
          transition: all 0.2s;
        }

        .card:hover {
          box-shadow: var(--shadow-md);
          border-color: var(--border-hover);
        }

        .table-container {
          overflow-x: auto;
        }

        .data-table {
          width: 100%;
          border-collapse: collapse;
        }

        .data-table th {
          background: var(--background-secondary);
          padding: 12px;
          text-align: left;
          font-size: 11px;
          font-weight: 700;
          color: var(--text-secondary);
          text-transform: uppercase;
          letter-spacing: 0.5px;
          border-bottom: 2px solid var(--border);
        }

        .data-table td {
          padding: 12px;
          border-bottom: 1px solid var(--border);
          font-size: 14px;
          color: var(--text-primary);
        }

        .data-table tr:hover {
          background: var(--surface-hover);
        }

        .loading {
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 40px;
          color: var(--text-secondary);
        }
      `}</style>
    </div>
  )
}

