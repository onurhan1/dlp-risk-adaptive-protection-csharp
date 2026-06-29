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
  Pin,
  RotateCcw,
  ShieldCheck,
  BrainCircuit,
  Mail,
  CalendarClock
} from 'lucide-react'

export default function Sidebar() {
  const pathname = usePathname()
  const router = useRouter()
  const { isAdmin } = useAuth()
  const { theme } = useTheme()
  const { t } = useTranslation()

  const isExceptionsPage = pathname?.startsWith('/exceptions')
  const isAiBehavioralPage = pathname?.startsWith('/ai-behavioral')
  const isInvestigationPage = pathname?.startsWith('/investigation')
  const [exceptionsOpen, setExceptionsOpen] = useState(false)
  const [aiBehavioralOpen, setAiBehavioralOpen] = useState(false)
  const [investigationOpen, setInvestigationOpen] = useState(false)

  useEffect(() => {
    if (isExceptionsPage) setExceptionsOpen(true)
  }, [isExceptionsPage])

  useEffect(() => {
    if (isAiBehavioralPage) setAiBehavioralOpen(true)
  }, [isAiBehavioralPage])

  useEffect(() => {
    if (isInvestigationPage) setInvestigationOpen(true)
  }, [isInvestigationPage])

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
            {/* Investigation - expandable menu */}
            <div>
              <div
                className={`sidebar-icon ${isInvestigationPage ? 'active' : ''}`}
                onClick={() => {
                  setInvestigationOpen(!investigationOpen)
                  if (!isInvestigationPage) router.push('/investigation')
                }}
                style={{ cursor: 'pointer' }}
              >
                <Search size={20} />
                <span style={{ flex: 1 }}>{t('nav.investigation')}</span>
                <ChevronDown
                  size={16}
                  style={{
                    transition: 'transform 0.2s',
                    transform: investigationOpen ? 'rotate(180deg)' : 'rotate(0deg)',
                    flexShrink: 0,
                    opacity: 0.6,
                  }}
                />
              </div>
              {investigationOpen && (
                <div className="sidebar-submenu">
                  <Link
                    href="/investigation"
                    className={`sidebar-subitem ${pathname === '/investigation' ? 'active' : ''}`}
                  >
                    <Search size={16} />
                    <span>{t('nav.investigationOverview')}</span>
                  </Link>
                  <Link
                    href="/investigation/weekly-review"
                    className={`sidebar-subitem ${pathname === '/investigation/weekly-review' ? 'active' : ''}`}
                  >
                    <CalendarClock size={16} />
                    <span>{t('nav.investigationWeekly')}</span>
                  </Link>
                  <Link
                    href="/investigation/mail-templates"
                    className={`sidebar-subitem ${pathname === '/investigation/mail-templates' ? 'active' : ''}`}
                  >
                    <Mail size={16} />
                    <span>{t('nav.mailTemplates')}</span>
                  </Link>
                </div>
              )}
            </div>

            {/* AI Behavioral - expandable menu */}
            <div>
              <div
                className={`sidebar-icon ${isAiBehavioralPage ? 'active' : ''}`}
                onClick={() => {
                  setAiBehavioralOpen(!aiBehavioralOpen)
                  if (!isAiBehavioralPage) router.push('/ai-behavioral')
                }}
                style={{ cursor: 'pointer' }}
              >
                <Layers size={20} />
                <span style={{ flex: 1 }}>{t('nav.aiBehavioral')}</span>
                <ChevronDown
                  size={16}
                  style={{
                    transition: 'transform 0.2s',
                    transform: aiBehavioralOpen ? 'rotate(180deg)' : 'rotate(0deg)',
                    flexShrink: 0,
                    opacity: 0.6,
                  }}
                />
              </div>
              {aiBehavioralOpen && (
                <div className="sidebar-submenu">
                  <Link
                    href="/ai-behavioral"
                    className={`sidebar-subitem ${pathname === '/ai-behavioral' ? 'active' : ''}`}
                  >
                    <BarChart3 size={16} />
                    <span>Genel Bakış</span>
                  </Link>
                  <Link
                    href="/ai-behavioral/rule-based"
                    className={`sidebar-subitem ${pathname === '/ai-behavioral/rule-based' ? 'active' : ''}`}
                  >
                    <ShieldCheck size={16} />
                    <span>Kural Tabanlı</span>
                  </Link>
                  <Link
                    href="/ai-behavioral/ai-model"
                    className={`sidebar-subitem ${pathname === '/ai-behavioral/ai-model' ? 'active' : ''}`}
                  >
                    <BrainCircuit size={16} />
                    <span>AI Risk Model</span>
                  </Link>
                </div>
              )}
            </div>

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
                  <Link
                    href="/exceptions/exception-list/permanent"
                    className={`sidebar-subitem ${pathname === '/exceptions/exception-list/permanent' ? 'active' : ''}`}
                    style={{ paddingLeft: '48px', fontSize: '13px' }}
                  >
                    <Pin size={16} />
                    <span>{t('nav.permanentExceptions')}</span>
                  </Link>
                  <Link
                    href="/exceptions/exception-list/removal"
                    className={`sidebar-subitem ${pathname === '/exceptions/exception-list/removal' ? 'active' : ''}`}
                    style={{ paddingLeft: '48px', fontSize: '13px' }}
                  >
                    <RotateCcw size={16} />
                    <span>{t('nav.exceptionRemovals')}</span>
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