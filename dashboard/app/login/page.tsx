'use client'

import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import axios from 'axios'
import { useAuth } from '@/components/AuthProvider'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001'

export default function LoginPage() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [rememberMe, setRememberMe] = useState(false)
  const router = useRouter()
  const { login } = useAuth()

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
        // Get role from response, or default to 'admin' if username is 'admin'
        let role = response.data.role
        if (!role) {
          // Fallback: if username is admin and no role in response, set to admin
          role = username.trim().toLowerCase() === 'admin' ? 'admin' : 'standard'
        }
        
        console.log('Login - Role:', role, 'Response:', response.data)
        
        // Use AuthProvider login function
        login(response.data.token, response.data.username || username.trim(), role)
        
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
        setError(`Cannot connect to API. Please check if the API is running on ${API_URL}`)
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
          padding: 20px;
          position: relative;
          overflow: hidden;
          transition: background 0.3s ease;
        }

        :global(.dark-theme) .login-container {
          background: linear-gradient(135deg, #1a1f2e 0%, #252b3d 50%, #1a1f2e 100%);
        }

        :global(.light-theme) .login-container {
          background: linear-gradient(135deg, #f5f7fa 0%, #e2e8f0 50%, #f5f7fa 100%);
        }

        .login-container::before {
          content: '';
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          pointer-events: none;
          transition: background 0.3s ease;
        }

        :global(.dark-theme) .login-container::before {
          background: radial-gradient(circle at 20% 50%, rgba(0, 168, 232, 0.1) 0%, transparent 50%),
                      radial-gradient(circle at 80% 80%, rgba(0, 212, 255, 0.1) 0%, transparent 50%);
        }

        :global(.light-theme) .login-container::before {
          background: radial-gradient(circle at 20% 50%, rgba(0, 168, 232, 0.05) 0%, transparent 50%),
                      radial-gradient(circle at 80% 80%, rgba(0, 212, 255, 0.05) 0%, transparent 50%);
        }

        .login-card {
          background: var(--surface);
          border-radius: 12px;
          box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
          border: 1px solid var(--border);
          padding: 48px;
          width: 100%;
          max-width: 420px;
          position: relative;
          z-index: 1;
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
          color: var(--text-primary);
          margin: 0 0 8px 0;
          letter-spacing: -0.02em;
        }

        .login-subtitle {
          font-size: 14px;
          color: var(--text-secondary);
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
          font-size: 12px;
          font-weight: 600;
          color: var(--text-secondary);
          text-transform: uppercase;
          letter-spacing: 0.5px;
        }

        .form-group input {
          padding: 12px 16px;
          border: 1px solid var(--border);
          border-radius: 8px;
          font-size: 16px;
          background: var(--background-secondary);
          color: var(--text-primary);
          transition: all 0.3s;
        }

        .form-group input:focus {
          outline: none;
          border-color: var(--primary);
          box-shadow: 0 0 0 3px rgba(0, 168, 232, 0.1);
        }

        .form-group input::placeholder {
          color: var(--text-muted);
        }

        .form-group input:disabled {
          background-color: #f5f5f5;
          cursor: not-allowed;
        }

        .error-message {
          background-color: rgba(217, 83, 79, 0.1);
          color: #d9534f;
          padding: 12px;
          border-radius: 8px;
          font-size: 14px;
          text-align: center;
          border: 1px solid rgba(217, 83, 79, 0.3);
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
          background-color: var(--primary);
          color: white;
          padding: 14px;
          border: none;
          border-radius: 8px;
          font-size: 16px;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.3s;
          margin-top: 8px;
          box-shadow: 0 4px 12px rgba(0, 168, 232, 0.3);
        }

        .login-button:hover:not(:disabled) {
          background-color: var(--primary-dark);
          transform: translateY(-2px);
          box-shadow: 0 6px 16px rgba(0, 168, 232, 0.4);
        }

        .login-button:disabled {
          background-color: #9fa8da;
          cursor: not-allowed;
        }

        .login-footer {
          margin-top: 24px;
          text-align: center;
          font-size: 12px;
          color: var(--text-muted);
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

