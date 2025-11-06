'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'

export default function Sidebar() {
  const pathname = usePathname()

  return (
    <div className="sidebar">
      <div className="sidebar-logo">
        <div style={{ 
          display: 'flex',
          alignItems: 'center',
          gap: '12px'
        }}>
          <div style={{ 
            width: '36px', 
            height: '36px', 
            background: 'linear-gradient(135deg, #00a8e8 0%, #0066cc 100%)', 
            borderRadius: '8px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 'bold',
            color: 'white',
            fontSize: '18px',
            boxShadow: '0 2px 8px rgba(0, 168, 232, 0.3)'
          }}>
            F
          </div>
          <div style={{ color: 'var(--text-primary)', fontSize: '16px', fontWeight: '600' }}>
            Forcepoint RAP
          </div>
        </div>
      </div>
      
      <div style={{ padding: '8px 0', flex: 1 }}>
        <Link href="/" className={`sidebar-icon ${pathname === '/' ? 'active' : ''}`}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
            <polyline points="9 22 9 12 15 12 15 22"></polyline>
          </svg>
          <span>Dashboard</span>
        </Link>

        <Link href="/investigation" className={`sidebar-icon ${pathname === '/investigation' ? 'active' : ''}`}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="11" cy="11" r="8"></circle>
            <path d="m21 21-4.35-4.35"></path>
          </svg>
          <span>Investigation</span>
        </Link>

        <Link href="/reports" className={`sidebar-icon ${pathname === '/reports' ? 'active' : ''}`}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="9" y1="3" x2="9" y2="21"></line>
            <line x1="15" y1="3" x2="15" y2="21"></line>
          </svg>
          <span>Reports</span>
        </Link>

        <Link href="/settings" className={`sidebar-icon ${pathname === '/settings' ? 'active' : ''}`}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="3"></circle>
            <path d="M12 1v6m0 6v6m9-9h-6m-6 0H3m8.8-8.8l-4.24 4.24m8.48 8.48l-4.24 4.24m0-16.96l4.24 4.24m-8.48 8.48l4.24 4.24"></path>
          </svg>
          <span>Settings</span>
        </Link>
      </div>

      <div style={{ padding: '16px', borderTop: '1px solid var(--border)' }}>
        <div className="sidebar-icon" style={{ color: 'var(--text-muted)', fontSize: '12px' }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10"></circle>
            <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
          <span>Help & Support</span>
        </div>
      </div>
    </div>
  )
}

