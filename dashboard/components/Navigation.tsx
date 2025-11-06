'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import React from 'react'
import { useAuth } from './AuthProvider'

export default function Navigation() {
  const pathname = usePathname()
  const { username, logout } = useAuth()

  return (
    <nav className="main-header">
      <div className="header-content">
        <div className="header-brand">
          Forcepoint RAP
        </div>
        <div className="header-nav">
          <Link href="/" className={`nav-item ${pathname === '/' ? 'active' : ''}`}>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
              <polyline points="9 22 9 12 15 12 15 22"></polyline>
            </svg>
            <span>Dashboard</span>
          </Link>
          <Link href="/investigation" className={`nav-item ${pathname === '/investigation' ? 'active' : ''}`}>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8"></circle>
              <path d="m21 21-4.35-4.35"></path>
            </svg>
            <span>Investigation</span>
          </Link>
          {username && (
            <div className="user-menu">
              <span className="username">{username}</span>
              <button onClick={logout} className="logout-btn">
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
      </div>
      <style jsx>{`
        .main-header {
          background: #1a237e;
          color: white;
          padding: 0;
          box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        }
        
        .header-content {
          max-width: 1400px;
          margin: 0 auto;
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 16px 24px;
        }
        
        .header-brand {
          font-size: 20px;
          font-weight: 600;
          color: white;
        }
        
        .header-nav {
          display: flex;
          gap: 24px;
        }
        
        .nav-item {
          display: flex;
          align-items: center;
          gap: 8px;
          color: white;
          text-decoration: none;
          font-size: 14px;
          font-weight: 500;
          padding: 6px 12px;
          border-radius: 4px;
          transition: background 0.2s;
        }
        
        .nav-item:hover {
          background: rgba(255, 255, 255, 0.1);
        }
        
        .nav-item.active {
          background: rgba(255, 255, 255, 0.2);
        }
        
        .nav-item svg {
          width: 18px;
          height: 18px;
        }

        .user-menu {
          display: flex;
          align-items: center;
          gap: 12px;
          margin-left: 24px;
          padding-left: 24px;
          border-left: 1px solid rgba(255, 255, 255, 0.2);
        }

        .username {
          font-size: 14px;
          color: rgba(255, 255, 255, 0.9);
        }

        .logout-btn {
          display: flex;
          align-items: center;
          gap: 6px;
          background: rgba(255, 255, 255, 0.1);
          border: 1px solid rgba(255, 255, 255, 0.2);
          color: white;
          padding: 6px 12px;
          border-radius: 4px;
          cursor: pointer;
          font-size: 14px;
          transition: background 0.2s;
        }

        .logout-btn:hover {
          background: rgba(255, 255, 255, 0.2);
        }

        .logout-btn svg {
          width: 16px;
          height: 16px;
        }

        @media (max-width: 768px) {
          .header-content {
            padding: 12px 16px;
          }
          
          .header-nav {
            gap: 12px;
          }
          
          .nav-item span {
            display: none;
          }

          .user-menu {
            margin-left: 12px;
            padding-left: 12px;
          }

          .username {
            display: none;
          }

          .logout-btn span {
            display: none;
          }
        }
      `}</style>
    </nav>
  )
}
