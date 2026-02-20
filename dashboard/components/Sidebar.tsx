'use client'

import { useState, useEffect } from 'react'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useAuth } from './AuthProvider'
import { useTheme } from './ThemeProvider'
import { useTranslation } from './LanguageProvider'

export default function Sidebar() {
  const pathname = usePathname()
  const router = useRouter()
  const { isAdmin } = useAuth()
  const { theme } = useTheme()
  const { t } = useTranslation()

  const isExceptionsPage = pathname === '/exceptions'
  const [exceptionsOpen, setExceptionsOpen] = useState(false)
  const [currentView, setCurrentView] = useState<string | null>(null)

  const readViewParam = () => {
    const params = new URLSearchParams(window.location.search)
    return params.get('view')
  }

  useEffect(() => {
    setCurrentView(readViewParam())
    if (isExceptionsPage) {
      setExceptionsOpen(true)
    }
  }, [isExceptionsPage])

  // Poll for URL changes to sync sidebar active state
  useEffect(() => {
    let lastSearch = window.location.search
    const interval = setInterval(() => {
      if (window.location.search !== lastSearch) {
        lastSearch = window.location.search
        setCurrentView(readViewParam())
      }
    }, 200)

    const handlePopState = () => {
      lastSearch = window.location.search
      setCurrentView(readViewParam())
    }
    window.addEventListener('popstate', handlePopState)

    return () => {
      clearInterval(interval)
      window.removeEventListener('popstate', handlePopState)
    }
  }, [])

  return (
    <div className="sidebar">
      <div className="sidebar-logo">
        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: '12px'
        }}>
          <img
            src="/radar-karanlik.png"
            alt="RADAR"
            style={{
              width: '44px',
              height: '44px',
              objectFit: 'contain'
            }}
          />
          <span style={{
            fontSize: '22px',
            fontWeight: '700',
            color: 'var(--sidebar-text-hover)',
            letterSpacing: '-0.02em'
          }}>RADAR</span>
        </div>
      </div>

      <div style={{ padding: '8px 0', flex: 1 }}>
        <Link href="/" className={`sidebar-icon ${pathname === '/' ? 'active' : ''}`}>
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
            <polyline points="9 22 9 12 15 12 15 22"></polyline>
          </svg>
          <span>{t('nav.dashboard')}</span>
        </Link>

        {isAdmin && (
          <>
            <Link href="/investigation" className={`sidebar-icon ${pathname === '/investigation' ? 'active' : ''}`}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="11" cy="11" r="8"></circle>
                <path d="m21 21-4.35-4.35"></path>
              </svg>
              <span>{t('nav.investigation')}</span>
            </Link>

            <Link href="/ai-behavioral" className={`sidebar-icon ${pathname === '/ai-behavioral' ? 'active' : ''}`}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M12 2L2 7l10 5 10-5-10-5z"></path>
                <path d="M2 17l10 5 10-5"></path>
                <path d="M2 12l10 5 10-5"></path>
              </svg>
              <span>{t('nav.aiBehavioral')}</span>
            </Link>

            {/* Exceptions - expandable menu */}
            <div>
              <div
                className={`sidebar-icon ${isExceptionsPage ? 'active' : ''}`}
                onClick={() => {
                  setExceptionsOpen(!exceptionsOpen)
                  if (!isExceptionsPage) {
                    router.push('/exceptions')
                  }
                }}
                style={{ cursor: 'pointer' }}
              >
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <line x1="18" y1="20" x2="18" y2="10"></line>
                  <line x1="12" y1="20" x2="12" y2="4"></line>
                  <line x1="6" y1="20" x2="6" y2="14"></line>
                </svg>
                <span style={{ flex: 1 }}>{t('nav.exceptions')}</span>
                <svg
                  width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
                  style={{ transition: 'transform 0.2s', transform: exceptionsOpen ? 'rotate(180deg)' : 'rotate(0deg)', flexShrink: 0 }}
                >
                  <polyline points="6 9 12 15 18 9"></polyline>
                </svg>
              </div>
              {exceptionsOpen && (
                <div className="sidebar-submenu">
                  <Link
                    href="/exceptions"
                    className={`sidebar-subitem ${isExceptionsPage && !currentView ? 'active' : ''}`}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                    </svg>
                    <span>{t('nav.exceptionsAnalytics')}</span>
                  </Link>
                  <Link
                    href="/exceptions?view=domain-features"
                    className={`sidebar-subitem ${isExceptionsPage && currentView === 'domain-features' ? 'active' : ''}`}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <circle cx="12" cy="12" r="10"></circle>
                      <line x1="2" y1="12" x2="22" y2="12"></line>
                      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
                    </svg>
                    <span>{t('nav.domainFeatures')}</span>
                  </Link>
                  <Link
                    href="/exceptions?view=mercek-analiz"
                    className={`sidebar-subitem ${isExceptionsPage && currentView === 'mercek-analiz' ? 'active' : ''}`}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                      <polyline points="14 2 14 8 20 8"></polyline>
                      <line x1="16" y1="13" x2="8" y2="13"></line>
                      <line x1="16" y1="17" x2="8" y2="17"></line>
                    </svg>
                    <span>{t('nav.mercekAnaliz')}</span>
                  </Link>
                </div>
              )}
            </div>

            <Link href="/user-management" className={`sidebar-icon ${pathname === '/user-management' ? 'active' : ''}`}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                <circle cx="9" cy="7" r="4"></circle>
                <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
                <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
              </svg>
              <span>{t('nav.userManagement')}</span>
            </Link>

            <Link href="/settings" className={`sidebar-icon ${pathname === '/settings' ? 'active' : ''}`}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="3"></circle>
                <path d="M12 1v6m0 6v6m9-9h-6m-6 0H3m8.8-8.8l-4.24 4.24m8.48 8.48l-4.24 4.24m0-16.96l4.24 4.24m-8.48 8.48l4.24 4.24"></path>
              </svg>
              <span>{t('nav.settings')}</span>
            </Link>

            <Link href="/release-notes" className={`sidebar-icon ${pathname === '/release-notes' ? 'active' : ''}`}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                <polyline points="14 2 14 8 20 8"></polyline>
                <line x1="16" y1="13" x2="8" y2="13"></line>
                <line x1="16" y1="17" x2="8" y2="17"></line>
                <polyline points="10 9 9 9 8 9"></polyline>
              </svg>
              <span>{t('nav.releaseNotes')}</span>
            </Link>
          </>
        )}

        {/* FAQ - accessible to all users */}
        <Link href="/faq" className={`sidebar-icon ${pathname === '/faq' ? 'active' : ''}`}>
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10"></circle>
            <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
          <span>{t('nav.faq')}</span>
        </Link>
      </div>
    </div >
  )
}