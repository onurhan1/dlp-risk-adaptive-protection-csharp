'use client'

import { useState, useEffect } from 'react'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useAuth } from './AuthProvider'
import { useTheme } from './ThemeProvider'
import { useTranslation } from './LanguageProvider'
import {
  Home,
  Search,
  Layers,
  BarChart3,
  Users,
  ChevronDown,
  Globe,
  FileText as FileTextIcon,
  ListChecks,
} from 'lucide-react'

export default function Sidebar() {
  const pathname = usePathname()
  const router = useRouter()
  const { isAdmin } = useAuth()
  const { theme } = useTheme()
  const { t } = useTranslation()

  const isExceptionsPage = pathname?.startsWith('/exceptions')
  const [exceptionsOpen, setExceptionsOpen] = useState(false)

  useEffect(() => {
    if (isExceptionsPage) {
      setExceptionsOpen(true)
    }
  }, [isExceptionsPage])

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
              width: '36px',
              height: '36px',
              objectFit: 'contain'
            }}
          />
          <span style={{
            fontSize: '18px',
            fontWeight: '700',
            color: '#F1F5F9',
            letterSpacing: '-0.02em',
            fontFamily: 'Inter, sans-serif',
          }}>RADAR</span>
        </div>
      </div>

      <div style={{ padding: '8px 0', flex: 1, overflowY: 'auto' }}>
        <Link href="/" className={`sidebar-icon ${pathname === '/' ? 'active' : ''}`}>
          <Home size={20} />
          <span>{t('nav.dashboard')}</span>
        </Link>

        {isAdmin && (
          <>
            <Link href="/investigation" className={`sidebar-icon ${pathname === '/investigation' ? 'active' : ''}`}>
              <Search size={20} />
              <span>{t('nav.investigation')}</span>
            </Link>

            <Link href="/ai-behavioral" className={`sidebar-icon ${pathname === '/ai-behavioral' ? 'active' : ''}`}>
              <Layers size={20} />
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
                <BarChart3 size={20} />
                <span style={{ flex: 1 }}>{t('nav.exceptions')}</span>
                <ChevronDown
                  size={16}
                  style={{
                    transition: 'transform 0.2s',
                    transform: exceptionsOpen ? 'rotate(180deg)' : 'rotate(0deg)',
                    flexShrink: 0,
                    opacity: 0.6,
                  }}
                />
              </div>
              {exceptionsOpen && (
                <div className="sidebar-submenu">
                  <Link
                    href="/exceptions"
                    className={`sidebar-subitem ${pathname === '/exceptions' ? 'active' : ''}`}
                  >
                    <BarChart3 size={16} />
                    <span>{t('nav.exceptionsAnalytics')}</span>
                  </Link>
                  <Link
                    href="/exceptions/domain-features"
                    className={`sidebar-subitem ${pathname === '/exceptions/domain-features' ? 'active' : ''}`}
                  >
                    <Globe size={16} />
                    <span>{t('nav.domainFeatures')}</span>
                  </Link>
                  <Link
                    href="/exceptions/mercek-analiz"
                    className={`sidebar-subitem ${pathname === '/exceptions/mercek-analiz' ? 'active' : ''}`}
                  >
                    <FileTextIcon size={16} />
                    <span>{t('nav.mercekAnaliz')}</span>
                  </Link>
                  <Link
                    href="/exceptions/exception-list"
                    className={`sidebar-subitem ${pathname === '/exceptions/exception-list' ? 'active' : ''}`}
                  >
                    <ListChecks size={16} />
                    <span>{t('nav.exceptionList')}</span>
                  </Link>
                </div>
              )}
            </div>

            <Link href="/user-management" className={`sidebar-icon ${pathname === '/user-management' ? 'active' : ''}`}>
              <Users size={20} />
              <span>{t('nav.userManagement')}</span>
            </Link>
          </>
        )}
      </div>
    </div>
  )
}