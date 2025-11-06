'use client'

import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

export default function LoginPage() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [rememberMe, setRememberMe] = useState(false)
  const router = useRouter()

  useEffect(() => {
    // Check if already logged in
    const token = localStorage.getItem('authToken')
    if (token) {
      router.push('/')
    }
  }, [router])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      const response = await axios.post(`${API_URL}/api/auth/login`, {
        username: username.trim(),
        password: password
      })

      console.log('Login response:', response.data)

      if (response.data && response.data.token) {
        // Save token
        localStorage.setItem('authToken', response.data.token)
        localStorage.setItem('username', response.data.username || username.trim())
        
        if (rememberMe) {
          localStorage.setItem('rememberMe', 'true')
        } else {
          localStorage.removeItem('rememberMe')
        }

        // Wait a bit before redirect to ensure state is updated
        setTimeout(() => {
          router.push('/')
          router.refresh()
        }, 100)
      } else {
        setError('Invalid response from server')
        setLoading(false)
      }
    } catch (err: any) {
      console.error('Login error:', err)
      if (err.response?.status === 401 || err.response?.status === 404) {
        setError('Invalid username or password. Please check your credentials.')
      } else if (err.response?.status === 404) {
        setError('API endpoint not found. Please check if the API is running.')
      } else if (err.code === 'ERR_NETWORK' || err.message?.includes('Network Error')) {
        setError('Cannot connect to API. Please check if the API is running on http://localhost:8000')
      } else {
        setError(err.response?.data?.detail || err.message || 'An error occurred during login')
      }
      setLoading(false)
    }
  }

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <div className="login-icon">
            <svg width="64" height="64" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M12 2L2 7L12 12L22 7L12 2Z" stroke="#283593" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <path d="M2 17L12 22L22 17" stroke="#283593" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <path d="M2 12L12 17L22 12" stroke="#283593" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
          </div>
          <h1>Forcepoint DLP</h1>
          <p className="login-subtitle">Risk Adaptive Protection</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Enter your username"
              required
              autoFocus
              disabled={loading}
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Enter your password"
              required
              disabled={loading}
            />
          </div>

          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          <div className="form-options">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
                disabled={loading}
              />
              <span>Remember me</span>
            </label>
            <button type="button" className="forgot-password" onClick={() => alert('Please contact your system administrator to reset your password.')}>
              Forgot Password?
            </button>
          </div>

          <button type="submit" className="login-button" disabled={loading}>
            {loading ? 'Logging in...' : 'LOGIN'}
          </button>
        </form>

        <div className="login-footer">
          <p>Default credentials: admin / admin123</p>
        </div>
      </div>

      <style jsx>{`
        .login-container {
          min-height: 100vh;
          display: flex;
          align-items: center;
          justify-content: center;
          background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
          padding: 20px;
        }

        .login-card {
          background: white;
          border-radius: 12px;
          box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
          padding: 48px;
          width: 100%;
          max-width: 420px;
        }

        .login-header {
          text-align: center;
          margin-bottom: 32px;
        }

        .login-icon {
          margin-bottom: 16px;
          display: flex;
          justify-content: center;
        }

        .login-header h1 {
          font-size: 28px;
          font-weight: 700;
          color: #1a237e;
          margin: 0 0 8px 0;
        }

        .login-subtitle {
          font-size: 14px;
          color: #666;
          margin: 0;
        }

        .login-form {
          display: flex;
          flex-direction: column;
          gap: 20px;
        }

        .form-group {
          display: flex;
          flex-direction: column;
          gap: 8px;
        }

        .form-group label {
          font-size: 14px;
          font-weight: 500;
          color: #333;
        }

        .form-group input {
          padding: 12px 16px;
          border: 2px solid #e0e0e0;
          border-radius: 8px;
          font-size: 16px;
          transition: border-color 0.3s;
        }

        .form-group input:focus {
          outline: none;
          border-color: #283593;
        }

        .form-group input:disabled {
          background-color: #f5f5f5;
          cursor: not-allowed;
        }

        .error-message {
          background-color: #ffebee;
          color: #c62828;
          padding: 12px;
          border-radius: 8px;
          font-size: 14px;
          text-align: center;
        }

        .form-options {
          display: flex;
          justify-content: space-between;
          align-items: center;
          font-size: 14px;
        }

        .checkbox-label {
          display: flex;
          align-items: center;
          gap: 8px;
          cursor: pointer;
          color: #666;
        }

        .checkbox-label input {
          cursor: pointer;
        }

        .forgot-password {
          background: none;
          border: none;
          color: #283593;
          cursor: pointer;
          font-size: 14px;
          text-decoration: underline;
          padding: 0;
        }

        .forgot-password:hover {
          color: #3949ab;
        }

        .login-button {
          background-color: #283593;
          color: white;
          padding: 14px;
          border: none;
          border-radius: 8px;
          font-size: 16px;
          font-weight: 600;
          cursor: pointer;
          transition: background-color 0.3s;
          margin-top: 8px;
        }

        .login-button:hover:not(:disabled) {
          background-color: #3949ab;
        }

        .login-button:disabled {
          background-color: #9fa8da;
          cursor: not-allowed;
        }

        .login-footer {
          margin-top: 24px;
          text-align: center;
          font-size: 12px;
          color: #999;
        }

        @media (max-width: 480px) {
          .login-card {
            padding: 32px 24px;
          }

          .login-header h1 {
            font-size: 24px;
          }
        }
      `}</style>
    </div>
  )
}

