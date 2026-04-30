'use client'

import { ReactNode } from 'react'
import { AuthProvider } from './AuthProvider'
import { ThemeProvider } from './ThemeProvider'
import { LanguageProvider } from './LanguageProvider'
import { ActivityTracker } from './ActivityTracker'

export function Providers({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider>
      <LanguageProvider>
        <AuthProvider>
          <ActivityTracker />
          {children}
        </AuthProvider>
      </LanguageProvider>
    </ThemeProvider>
  )
}

