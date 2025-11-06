'use client'

import { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { useRouter, usePathname } from 'next/navigation'

interface AuthContextType {
  isAuthenticated: boolean
  token: string | null
  username: string | null
  login: (token: string, username: string) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [token, setToken] = useState<string | null>(null)
  const [username, setUsername] = useState<string | null>(null)
  const router = useRouter()
  const pathname = usePathname()

  useEffect(() => {
    // Check for stored token on mount
    const storedToken = localStorage.getItem('authToken')
    const storedUsername = localStorage.getItem('username')

    if (storedToken) {
      setToken(storedToken)
      setUsername(storedUsername)
      setIsAuthenticated(true)
    }
  }, [])

  useEffect(() => {
    // Protect routes except login page
    if (typeof window !== 'undefined') {
      if (pathname !== '/login') {
        const storedToken = localStorage.getItem('authToken')
        if (!storedToken) {
          router.push('/login')
        }
      } else if (pathname === '/login' && isAuthenticated) {
        // Small delay to prevent flicker
        setTimeout(() => {
          router.push('/')
        }, 100)
      }
    }
  }, [pathname, isAuthenticated, router])

  const login = (newToken: string, newUsername: string) => {
    localStorage.setItem('authToken', newToken)
    localStorage.setItem('username', newUsername)
    setToken(newToken)
    setUsername(newUsername)
    setIsAuthenticated(true)
  }

  const logout = () => {
    localStorage.removeItem('authToken')
    localStorage.removeItem('username')
    localStorage.removeItem('rememberMe')
    setToken(null)
    setUsername(null)
    setIsAuthenticated(false)
    router.push('/login')
  }

  return (
    <AuthContext.Provider value={{ isAuthenticated, token, username, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

