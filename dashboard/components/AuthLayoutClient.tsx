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
      <div style={{ marginLeft: '240px', height: '100vh', background: 'var(--background)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <div style={{ flexShrink: 0, zIndex: 900 }}>
          <Navigation />
        </div>
        <div style={{ padding: '24px', flex: 1, overflowY: 'auto' }}>
          {children}
        </div>
      </div>
    </>
  )
}

