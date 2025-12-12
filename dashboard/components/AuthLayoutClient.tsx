'use client'

import { usePathname } from 'next/navigation'
import Sidebar from './Sidebar'
import Navigation from './Navigation'

export default function AuthLayoutClient({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const isLoginPage = pathname === '/login'

  if (isLoginPage) {
    return <>{children}</>
  }

  return (
    <>
      <Sidebar />
      <div style={{ marginLeft: '240px', minHeight: '100vh', background: 'var(--background)' }}>
        <Navigation />
        <div style={{ padding: '24px' }}>
          {children}
        </div>
      </div>
    </>
  )
}

