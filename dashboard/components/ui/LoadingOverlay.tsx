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
                inset: 0,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                background: transparent
                    ? 'rgba(0, 0, 0, 0.1)'
                    : 'rgba(var(--background-rgb, 15, 23, 42), 0.75)',
                backdropFilter: transparent ? 'none' : 'blur(2px)',
                zIndex: fullScreen ? 99999 : 9999,
                transition: 'opacity 0.2s ease',
            }}
        >
            {/* Spinner */}
            <div
                style={{
                    width: size.spinner,
                    height: size.spinner,
                    border: `${size.border}px solid rgba(59, 130, 246, 0.15)`,
                    borderTop: `${size.border}px solid #3b82f6`,
                    borderRadius: '50%',
                    animation: 'dlp-spin 0.8s linear infinite',
                }}
            />

            {/* Message */}
            {message && (
                <div
                    style={{
                        marginTop: '12px',
                        color: 'var(--text-muted)',
                        fontSize: '13px',
                        fontWeight: '500',
                        letterSpacing: '0.02em',
                    }}
                >
                    {message}
                </div>
            )}

            {/* CSS Animation - injected via style tag */}
            <style>{`
        @keyframes dlp-spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `}</style>
        </div>
    )
}
