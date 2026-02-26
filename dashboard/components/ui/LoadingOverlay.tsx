'use client'

import React from 'react'

export interface LoadingOverlayProps {
    isLoading: boolean
    message?: string
    fullScreen?: boolean
    spinnerSize?: 'sm' | 'md' | 'lg'
    transparent?: boolean
}

export default function LoadingOverlay({
    isLoading,
    message,
    fullScreen = false,
    spinnerSize = 'md',
    transparent = false,
}: LoadingOverlayProps) {
    if (!isLoading) return null

    const sizes = {
        sm: { spinner: 24, border: 3 },
        md: { spinner: 40, border: 4 },
        lg: { spinner: 56, border: 5 },
    }

    const size = sizes[spinnerSize]

    return (
        <div
            style={{
                position: fullScreen ? 'fixed' : 'absolute',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                background: transparent ? 'rgba(0,0,0,0.1)' : 'var(--background)',
                zIndex: fullScreen ? 99999 : 9999,
                transition: 'opacity 0.2s ease',
            }}
        >
            <div style={{
                backgroundColor: 'var(--surface)',
                padding: '32px 48px',
                borderRadius: '16px',
                boxShadow: 'var(--shadow-xl)',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: '24px',
                border: '1px solid var(--border)',
                backdropFilter: 'blur(10px)',
            }}>
                {/* Glow effect wrapper */}
                <div style={{
                    position: 'relative',
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center'
                }}>
                    {/* Animated glow background */}
                    <div style={{
                        position: 'absolute',
                        width: '64px',
                        height: '64px',
                        background: 'var(--primary)',
                        borderRadius: '50%',
                        filter: 'blur(20px)',
                        opacity: 0.5,
                        animation: 'dlp-pulse-glow 2s cubic-bezier(0.4, 0, 0.6, 1) infinite'
                    }} />

                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        width: '64px',
                        height: '64px',
                        borderRadius: '50%',
                        backgroundColor: 'var(--background-secondary)',
                        border: '2px solid var(--border)',
                        position: 'relative',
                        zIndex: 10,
                        boxShadow: 'var(--shadow-md)'
                    }}>
                        {/* Animated Radar Icon */}
                        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="var(--primary)" strokeWidth="2" style={{ animation: 'dlp-spin-pulse 3s infinite linear' }}>
                            <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" />
                        </svg>
                    </div>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '8px' }}>
                    <h2 style={{
                        fontSize: '18px',
                        fontWeight: '600',
                        color: 'var(--text-primary)',
                        margin: 0,
                        letterSpacing: '-0.02em'
                    }}>RADAR</h2>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--primary)" strokeWidth="2" style={{ animation: 'dlp-spin 1.5s linear infinite' }}>
                            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
                        </svg>
                        <span style={{ fontSize: '14px', color: 'var(--text-secondary)', fontWeight: '500' }}>
                            {message || 'Yükleniyor...'}
                        </span>
                    </div>
                </div>
            </div>

            {/* CSS Animation */}
            <style>{`
        @keyframes dlp-pulse-glow {
          0%, 100% { opacity: 0.5; transform: scale(1); }
          50% { opacity: 0.8; transform: scale(1.3); }
        }
        @keyframes dlp-spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
        @keyframes dlp-spin-pulse {
          0% { transform: rotate(0deg) scale(1); }
          50% { transform: rotate(180deg) scale(1.1); }
          100% { transform: rotate(360deg) scale(1); }
        }
      `}</style>
        </div>
    )
}
