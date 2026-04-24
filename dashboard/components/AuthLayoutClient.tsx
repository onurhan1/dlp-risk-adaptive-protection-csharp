'use client'

import { usePathname } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
import Sidebar from './Sidebar'
import Navigation from './Navigation'
import ChatBot from './ChatBot'

export default function AuthLayoutClient({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const isLoginPage = pathname === '/login'
  const [displayChildren, setDisplayChildren] = useState(children)
  const [transitionStage, setTransitionStage] = useState<'enter' | 'exit'>('enter')
  const prevPathname = useRef(pathname)

  useEffect(() => {
    if (prevPathname.current !== pathname) {
      // Start exit animation
      setTransitionStage('exit')
      const timer = setTimeout(() => {
        setDisplayChildren(children)
        setTransitionStage('enter')
        prevPathname.current = pathname
      }, 120)
      return () => clearTimeout(timer)
    } else {
      setDisplayChildren(children)
    }
  }, [pathname, children])

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
        <div
          style={{
            padding: '24px',
            flex: 1,
            overflowY: 'auto',
            opacity: transitionStage === 'exit' ? 0 : 1,
            transform: transitionStage === 'exit' ? 'translateY(6px)' : 'translateY(0)',
            transition: 'opacity 120ms ease, transform 120ms ease',
          }}
        >
          {displayChildren}
        </div>
      </div>
      <ChatBot />
    </>
  )
}
