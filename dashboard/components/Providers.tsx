'use client'

import { ReactNode } from 'react'
import { AuthProvider } from './AuthProvider'
import { ThemeProvider } from './ThemeProvider'

export function Providers({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider>
      <AuthProvider>
        {children}
      </AuthProvider>
    </ThemeProvider>
  )
}

