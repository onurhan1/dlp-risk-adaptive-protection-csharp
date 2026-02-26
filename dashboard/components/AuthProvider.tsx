'use client'

import { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { useRouter, usePathname } from 'next/navigation'

interface AuthContextType {
  isAuthenticated: boolean
  token: string | null
  username: string | null
  role: string | null
  isAdmin: boolean
  login: (token: string, username: string, role: string) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [token, setToken] = useState<string | null>(null)
  const [username, setUsername] = useState<string | null>(null)
  const [role, setRole] = useState<string | null>(null)
  const router = useRouter()
  const pathname = usePathname()

  useEffect(() => {
    // Check for stored token on mount
    if (typeof window !== 'undefined') {
      const storedToken = localStorage.getItem('authToken')
      const storedUsername = localStorage.getItem('username')
      let storedRole = localStorage.getItem('userRole')

      // Fallback: If role is missing but username is admin, set role to admin
      if (storedToken && storedUsername === 'admin' && !storedRole) {
        storedRole = 'admin'
        localStorage.setItem('userRole', 'admin')
      }

      if (storedToken) {
        setToken(storedToken)
        setUsername(storedUsername)
        setRole(storedRole)
        setIsAuthenticated(true)
      }
    }
  }, [])

  useEffect(() => {
    // Protect routes except login page
    if (typeof window !== 'undefined') {
      if (pathname !== '/login') {
        const storedToken = localStorage.getItem('authToken')
        const storedRole = localStorage.getItem('userRole')
        
        if (!storedToken) {
          router.push('/login')
          return
        }

        // Standard users can only access dashboard
        if (storedRole === 'standard' && pathname !== '/') {
          router.push('/')
          return
        }
      } else if (pathname === '/login' && isAuthenticated) {
        // Small delay to prevent flicker
        setTimeout(() => {
          router.push('/')
        }, 100)
      }
    }
  }, [pathname, isAuthenticated, router])

  const login = (newToken: string, newUsername: string, newRole: string) => {
    if (typeof window !== 'undefined') {
      localStorage.setItem('authToken', newToken)
      localStorage.setItem('username', newUsername)
      localStorage.setItem('userRole', newRole)
    }
    setToken(newToken)
    setUsername(newUsername)
    setRole(newRole)
    setIsAuthenticated(true)
  }

  const logout = () => {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('authToken')
      localStorage.removeItem('username')
      localStorage.removeItem('userRole')
      localStorage.removeItem('rememberMe')
    }
    setToken(null)
    setUsername(null)
    setRole(null)
    setIsAuthenticated(false)
    router.push('/login')
  }

  const isAdmin = role === 'admin'

  return (
    <AuthContext.Provider value={{ isAuthenticated, token, username, role, isAdmin, login, logout }}>
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

