'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import React from 'react'

export default function Navigation() {
  const pathname = usePathname()

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
        }
      `}</style>
    </nav>
  )
}
