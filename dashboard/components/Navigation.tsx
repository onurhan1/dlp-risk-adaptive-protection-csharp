'use client'

import React, { useEffect, useState } from 'react'
import { useAuth } from './AuthProvider'
import { useTheme } from './ThemeProvider'
import { useTranslation } from './LanguageProvider'
import { usePathname } from 'next/navigation'

const getPageTitleKey = (pathname: string, search: string): string => {
  if (pathname === '/' || pathname === '') return 'nav.dashboard'
  if (pathname.startsWith('/investigation')) return 'nav.investigation'
  if (pathname.startsWith('/ai-behavioral')) return 'nav.aiBehavioral'
  if (pathname.startsWith('/exceptions')) {
    if (search.includes('view=domain-features')) return 'nav.domainFeatures'
    if (search.includes('view=mercek-analiz')) return 'nav.mercekAnaliz'
    return 'nav.exceptionsAnalytics'
  }
  if (pathname.startsWith('/user-management')) return 'nav.userManagement'
  if (pathname.startsWith('/settings')) return 'nav.settings'
  if (pathname.startsWith('/faq')) return 'nav.faq'
  if (pathname.startsWith('/release-notes')) return 'nav.releaseNotes'
  return 'nav.dashboard'
}

export default function Navigation() {
  const [mounted, setMounted] = useState(false)
  const [currentSearch, setCurrentSearch] = useState('')
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [settingsMenuOpen, setSettingsMenuOpen] = useState(false)
  const { username, logout } = useAuth()
  const { theme, toggleTheme } = useTheme()
  const { locale, setLocale, t } = useTranslation()
  const pathname = usePathname()

  useEffect(() => {
    setMounted(true)
    setCurrentSearch(window.location.search)

    const interval = setInterval(() => {
      setCurrentSearch(window.location.search)
    }, 200)
    return () => clearInterval(interval)
  }, [])

  // Close menus when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as HTMLElement
      if (!target.closest('.user-menu-wrapper')) {
        setUserMenuOpen(false)
      }
      if (!target.closest('.settings-menu-wrapper')) {
        setSettingsMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  // Prevent hydration mismatch by not rendering until mounted
  if (!mounted) {
    return null
  }

  const pageTitle = t(getPageTitleKey(pathname, currentSearch))

  return (
    <nav className="main-header">
      <div className="header-content">
        <div className="header-brand">
          {pageTitle}
        </div>
        <div className="header-nav">
          <button
            onClick={() => setLocale(locale === 'tr' ? 'en' : 'tr')}
            className="lang-toggle-btn"
            title={locale === 'tr' ? 'Switch to English' : 'Türkçe\'ye geç'}
          >
            <span className="lang-label">{locale === 'tr' ? 'TR' : 'EN'}</span>
          </button>
          <button onClick={toggleTheme} className="theme-toggle-btn" title={theme === 'dark' ? 'Switch to Light Theme' : 'Switch to Dark Theme'}>
            {theme === 'dark' ? (
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="5"></circle>
                <line x1="12" y1="1" x2="12" y2="3"></line>
                <line x1="12" y1="21" x2="12" y2="23"></line>
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
                <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
                <line x1="1" y1="12" x2="3" y2="12"></line>
                <line x1="21" y1="12" x2="23" y2="12"></line>
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
                <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
              </svg>
            ) : (
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
              </svg>
            )}
          </button>
          <div className="settings-menu-wrapper">
            <button className="settings-btn" title={t('nav.settings')} onClick={() => setSettingsMenuOpen(!settingsMenuOpen)}>
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="3"></circle>
                <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
              </svg>
            </button>
            {settingsMenuOpen && (
              <div className="settings-dropdown">
                <a href="/settings" className="dropdown-item" onClick={() => setSettingsMenuOpen(false)}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="3"></circle>
                    <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
                  </svg>
                  <span>{t('nav.settings')}</span>
                </a>
                <a href="/release-notes" className="dropdown-item" onClick={() => setSettingsMenuOpen(false)}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                    <polyline points="14 2 14 8 20 8"></polyline>
                    <line x1="16" y1="13" x2="8" y2="13"></line>
                    <line x1="16" y1="17" x2="8" y2="17"></line>
                  </svg>
                  <span>{t('nav.releaseNotes')}</span>
                </a>
                <a href="/faq" className="dropdown-item" onClick={() => setSettingsMenuOpen(false)}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path>
                    <line x1="12" y1="17" x2="12.01" y2="17"></line>
                  </svg>
                  <span>{t('nav.faq')}</span>
                </a>
              </div>
            )}
          </div>
          {username && (
            <div className="user-menu-wrapper">
              <button className="user-menu-trigger" onClick={() => setUserMenuOpen(!userMenuOpen)}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                  <circle cx="12" cy="7" r="4"></circle>
                </svg>
                <span>{username}</span>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="chevron-icon">
                  <polyline points="6 9 12 15 18 9"></polyline>
                </svg>
              </button>
              {userMenuOpen && (
                <div className="user-dropdown">
                  <button onClick={() => { logout(); setUserMenuOpen(false); }} className="dropdown-item">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
                      <polyline points="16 17 21 12 16 7"></polyline>
                      <line x1="21" y1="12" x2="9" y2="12"></line>
                    </svg>
                    <span>Logout</span>
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
      <style jsx>{`
        .main-header {
          background: var(--surface);
          color: var(--text-primary);
          padding: 0;
          box-shadow: var(--shadow);
          border-bottom: 1px solid var(--border);
        }
        
        .header-content {
          max-width: 100%;
          margin: 0;
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 12px 24px;
        }
        
        .header-brand {
          font-size: 18px;
          font-weight: 600;
          color: var(--text-primary);
          letter-spacing: -0.02em;
        }
        
        .header-nav {
          display: flex;
          gap: 8px;
          align-items: center;
        }
        
        .nav-item {
          display: flex;
          align-items: center;
          gap: 8px;
          color: var(--text-secondary);
          text-decoration: none;
          font-size: 14px;
          font-weight: 500;
          padding: 8px 16px;
          border-radius: 6px;
          transition: all 0.2s;
        }
        
        .nav-item:hover {
          background: var(--surface-hover);
          color: var(--text-primary);
        }
        
        .nav-item.active {
          background: var(--surface-active);
          color: var(--primary);
        }
        
        .nav-item svg {
          width: 18px;
          height: 18px;
        }

        .theme-toggle-btn {
          display: flex;
          align-items: center;
          justify-content: center;
          width: 40px;
          height: 40px;
          background: var(--surface-hover);
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          color: var(--text-primary);
          transition: all 0.2s;
          margin-right: 12px;
        }

        .theme-toggle-btn:hover {
          background: var(--surface-active);
          border-color: var(--primary);
          color: var(--primary);
          transform: rotate(15deg);
        }

        .theme-toggle-btn svg {
          width: 20px;
          height: 20px;
        }

        .lang-toggle-btn {
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 6px;
          height: 40px;
          padding: 0 14px;
          background: var(--surface-hover);
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          color: var(--text-primary);
          font-size: 13px;
          font-weight: 600;
          transition: all 0.2s;
          margin-right: 4px;
        }

        .lang-toggle-btn:hover {
          background: var(--surface-active);
          border-color: var(--primary);
          color: var(--primary);
        }

        .lang-label {
          font-size: 13px;
          font-weight: 600;
          letter-spacing: 0.02em;
        }

        .settings-menu-wrapper {
          position: relative;
          margin-right: 4px;
        }

        .settings-btn {
          display: flex;
          align-items: center;
          justify-content: center;
          width: 40px;
          height: 40px;
          background: var(--surface-hover);
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          color: var(--text-primary);
          transition: all 0.2s;
        }

        .settings-btn:hover {
          background: var(--surface-active);
          border-color: var(--primary);
          color: var(--primary);
        }

        .settings-btn svg {
          width: 20px;
          height: 20px;
        }

        .settings-dropdown {
          position: absolute;
          top: calc(100% + 6px);
          right: 0;
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 8px;
          box-shadow: var(--shadow-lg);
          min-width: 200px;
          padding: 4px;
          z-index: 1000;
          animation: dropdownFade 0.15s ease;
        }

        .settings-dropdown .dropdown-item {
          text-decoration: none;
        }

        .settings-dropdown .dropdown-item:hover {
          background: var(--surface-hover);
          color: var(--primary);
        }

        .user-menu-wrapper {
          position: relative;
          margin-left: 16px;
          padding-left: 16px;
          border-left: 1px solid var(--border);
        }

        .user-menu-trigger {
          display: flex;
          align-items: center;
          gap: 8px;
          background: var(--surface-hover);
          border: 1px solid var(--border);
          color: var(--text-primary);
          padding: 8px 14px;
          border-radius: 8px;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.2s;
        }

        .user-menu-trigger:hover {
          background: var(--surface-active);
          border-color: var(--primary);
        }

        .user-menu-trigger svg {
          width: 20px;
          height: 20px;
          flex-shrink: 0;
        }

        .chevron-icon {
          width: 14px !important;
          height: 14px !important;
          opacity: 0.6;
        }

        .user-dropdown {
          position: absolute;
          top: calc(100% + 6px);
          right: 0;
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 8px;
          box-shadow: var(--shadow-lg);
          min-width: 160px;
          padding: 4px;
          z-index: 1000;
          animation: dropdownFade 0.15s ease;
        }

        @keyframes dropdownFade {
          from { opacity: 0; transform: translateY(-4px); }
          to { opacity: 1; transform: translateY(0); }
        }

        .dropdown-item {
          display: flex;
          align-items: center;
          gap: 8px;
          width: 100%;
          background: none;
          border: none;
          color: var(--text-primary);
          padding: 10px 14px;
          border-radius: 6px;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.2s;
        }

        .dropdown-item:hover {
          background: var(--surface-hover);
          color: var(--danger);
        }

        .dropdown-item svg {
          width: 16px;
          height: 16px;
        }

        @media (max-width: 768px) {
          .header-content {
            padding: 12px 16px;
          }
          
          .header-nav {
            gap: 6px;
          }
          
          .nav-item span {
            display: none;
          }

          .user-menu-wrapper {
            margin-left: 8px;
            padding-left: 8px;
          }

          .user-menu-trigger span {
            display: none;
          }

          .chevron-icon {
            display: none;
          }

          .lang-label {
            display: none;
          }
        }
      `}</style>
    </nav>
  )
}
