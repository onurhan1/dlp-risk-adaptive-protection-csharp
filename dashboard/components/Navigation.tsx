'use client'

import React, { useEffect, useState, Suspense } from 'react'
import Link from 'next/link'
import { useAuth } from './AuthProvider'
import { useTheme } from './ThemeProvider'
import { useTranslation } from './LanguageProvider'
import { usePathname, useSearchParams } from 'next/navigation'
import {
  Sun,
  Moon,
  Settings,
  FileText,
  HelpCircle,
  User,
  LogOut,
  ChevronDown,
} from 'lucide-react'

const getPageTitleKey = (pathname: string, search: string): string => {
  if (pathname === '/' || pathname === '') return 'nav.dashboard'
  if (pathname.startsWith('/investigation')) return 'nav.investigation'
  if (pathname.startsWith('/ai-behavioral')) return 'nav.aiBehavioral'
  if (pathname.startsWith('/exceptions')) {
    if (pathname.startsWith('/exceptions/policy-inventory')) return 'nav.policyInventory'
    if (pathname.startsWith('/exceptions/exception-list')) return 'nav.exceptionList'
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
  return (
    <Suspense fallback={null}>
      <NavigationContent />
    </Suspense>
  )
}

function NavigationContent() {
  const [mounted, setMounted] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [settingsMenuOpen, setSettingsMenuOpen] = useState(false)
  const { username, logout } = useAuth()
  const { theme, toggleTheme } = useTheme()
  const { locale, setLocale, t } = useTranslation()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const currentSearch = searchParams.toString() ? `?${searchParams.toString()}` : ''

  useEffect(() => {
    setMounted(true)
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
            {theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
          </button>
          <div className="settings-menu-wrapper">
            <button className="settings-btn" title={t('nav.settings')} onClick={() => setSettingsMenuOpen(!settingsMenuOpen)}>
              <Settings size={18} />
            </button>
            {settingsMenuOpen && (
              <div className="settings-dropdown">
                <Link href="/settings" className="dropdown-item" onClick={() => setSettingsMenuOpen(false)}>
                  <Settings size={16} />
                  <span>{t('nav.settings')}</span>
                </Link>
                <Link href="/release-notes" className="dropdown-item" onClick={() => setSettingsMenuOpen(false)}>
                  <FileText size={16} />
                  <span>{t('nav.releaseNotes')}</span>
                </Link>
                <Link href="/faq" className="dropdown-item" onClick={() => setSettingsMenuOpen(false)}>
                  <HelpCircle size={16} />
                  <span>{t('nav.faq')}</span>
                </Link>
              </div>
            )}
          </div>
          {username && (
            <div className="user-menu-wrapper">
              <button className="user-menu-trigger" onClick={() => setUserMenuOpen(!userMenuOpen)}>
                <User size={18} />
                <span>{username}</span>
                <ChevronDown size={14} style={{ opacity: 0.6 }} />
              </button>
              {userMenuOpen && (
                <div className="user-dropdown">
                  <button onClick={() => { logout(); setUserMenuOpen(false); }} className="dropdown-item">
                    <LogOut size={16} />
                    <span>{t('login.logout')}</span>
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
          box-shadow: none;
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
          font-size: 16px;
          font-weight: 600;
          color: var(--text-primary);
          letter-spacing: -0.01em;
        }
        
        .header-nav {
          display: flex;
          gap: 6px;
          align-items: center;
        }

        .theme-toggle-btn {
          display: flex;
          align-items: center;
          justify-content: center;
          width: 36px;
          height: 36px;
          background: transparent;
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          color: var(--text-muted);
          transition: all 0.2s;
        }

        .theme-toggle-btn:hover {
          background: var(--surface-hover);
          color: var(--text-primary);
          border-color: var(--border-hover);
        }

        .lang-toggle-btn {
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 6px;
          height: 36px;
          padding: 0 12px;
          background: transparent;
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          color: var(--text-muted);
          font-size: 13px;
          font-weight: 600;
          transition: all 0.2s;
          font-family: 'Inter', sans-serif;
        }

        .lang-toggle-btn:hover {
          background: var(--surface-hover);
          color: var(--text-primary);
          border-color: var(--border-hover);
        }

        .lang-label {
          font-size: 13px;
          font-weight: 600;
          letter-spacing: 0.02em;
        }

        .settings-menu-wrapper {
          position: relative;
        }

        .settings-btn {
          display: flex;
          align-items: center;
          justify-content: center;
          width: 36px;
          height: 36px;
          background: transparent;
          border: 1px solid var(--border);
          border-radius: 8px;
          cursor: pointer;
          color: var(--text-muted);
          transition: all 0.2s;
        }

        .settings-btn:hover {
          background: var(--surface-hover);
          color: var(--text-primary);
          border-color: var(--border-hover);
        }

        .settings-dropdown {
          position: absolute;
          top: calc(100% + 8px);
          right: 0;
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 8px;
          box-shadow: 0 4px 16px rgba(15, 23, 42, 0.1);
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
          color: var(--text-primary);
        }

        .user-menu-wrapper {
          position: relative;
          margin-left: 12px;
          padding-left: 12px;
          border-left: 1px solid var(--border);
        }

        .user-menu-trigger {
          display: flex;
          align-items: center;
          gap: 8px;
          background: transparent;
          border: 1px solid var(--border);
          color: var(--text-primary);
          padding: 6px 12px;
          border-radius: 8px;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.2s;
          font-family: 'Inter', sans-serif;
        }

        .user-menu-trigger:hover {
          background: var(--surface-hover);
          border-color: var(--border-hover);
        }

        .user-dropdown {
          position: absolute;
          top: calc(100% + 8px);
          right: 0;
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 8px;
          box-shadow: 0 4px 16px rgba(15, 23, 42, 0.1);
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
          padding: 8px 12px;
          border-radius: 6px;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.15s;
          font-family: 'Inter', sans-serif;
        }

        .dropdown-item:hover {
          background: var(--surface-hover);
          color: var(--danger, #EF4444);
        }

        @media (max-width: 768px) {
          .header-content {
            padding: 12px 16px;
          }
          
          .header-nav {
            gap: 4px;
          }

          .user-menu-wrapper {
            margin-left: 8px;
            padding-left: 8px;
          }

          .user-menu-trigger span {
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
