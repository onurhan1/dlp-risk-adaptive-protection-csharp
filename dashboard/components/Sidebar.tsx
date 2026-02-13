'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useAuth } from './AuthProvider'
import { useTheme } from './ThemeProvider'
import { useTranslation } from './LanguageProvider'

export default function Sidebar() {
  const pathname = usePathname()
  const { isAdmin } = useAuth()
  const { theme } = useTheme()
  const { locale, setLocale, t } = useTranslation()

  return (
    <div className="sidebar">
      <div className="sidebar-logo">
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center'
        }}>
          <img
            src={theme === 'dark' ? '/radar-karanlik.png' : '/radar-aydinlik.png'}
            alt="RADAR"
            style={{
              width: '100px',
              height: '100px',
              objectFit: 'contain'
            }}
          />
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

            <Link href="/exceptions" className={`sidebar-icon ${pathname === '/exceptions' ? 'active' : ''}`}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="20" x2="18" y2="10"></line>
                <line x1="12" y1="20" x2="12" y2="4"></line>
                <line x1="6" y1="20" x2="6" y2="14"></line>
              </svg>
              <span>{t('nav.exceptions')}</span>
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

      <div style={{ padding: '16px', borderTop: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: '8px' }}>
        {/* Language Toggle */}
        <button
          onClick={() => setLocale(locale === 'tr' ? 'en' : 'tr')}
          className="sidebar-icon"
          style={{
            background: 'none',
            border: '1px solid var(--border)',
            borderRadius: '8px',
            padding: '8px 12px',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            color: 'var(--text-primary)',
            fontSize: '13px',
            fontWeight: '500',
            transition: 'all 0.2s ease',
            width: '100%',
            justifyContent: 'center',
          }}
        >
          <span style={{ fontSize: '18px' }}>{locale === 'tr' ? '🇬🇧' : '🇹🇷'}</span>
          <span>{locale === 'tr' ? 'English' : 'Türkçe'}</span>
        </button>

        {/* Help */}
        <div className="sidebar-icon" style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10"></circle>
            <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
          <span>{t('nav.help')}</span>
        </div>
      </div>
    </div >
  )
}