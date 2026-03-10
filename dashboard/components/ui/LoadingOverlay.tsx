'use client'

import React, { useEffect, useState } from 'react'

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
    const [progress, setProgress] = useState(0)
    const [statusText, setStatusText] = useState(message || 'Veriler yükleniyor')

    useEffect(() => {
        if (!isLoading) {
            setProgress(0)
            return
        }

        const statusMessages = [
            message || 'Veriler yükleniyor',
            'Analiz ediliyor',
            'Güvenlik verileri işleniyor',
            'Sayfa hazırlanıyor',
        ]

        let step = 0
        const interval = setInterval(() => {
            step++
            setProgress(prev => {
                const next = prev + Math.random() * 15 + 6
                return Math.min(next, 92)
            })
            if (step < statusMessages.length) {
                setStatusText(statusMessages[step])
            }
        }, 600)

        return () => clearInterval(interval)
    }, [isLoading, message])

    if (!isLoading) return null

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
                background: transparent
                    ? 'rgba(15, 23, 42, 0.6)'
                    : 'var(--background)',
                zIndex: fullScreen ? 999999 : 99999,
                backdropFilter: transparent ? 'blur(8px)' : 'none',
                transition: 'opacity 0.3s ease',
                isolation: 'isolate',
            }}
        >
            {/* Ambient background effects */}
            {!transparent && (
                <div style={{
                    position: 'absolute',
                    inset: 0,
                    overflow: 'hidden',
                    pointerEvents: 'none',
                }}>
                    {/* Top-right glow */}
                    <div style={{
                        position: 'absolute',
                        top: '-15%',
                        right: '-8%',
                        width: '400px',
                        height: '400px',
                        borderRadius: '50%',
                        background: 'radial-gradient(circle, rgba(59, 130, 246, 0.06) 0%, transparent 70%)',
                        animation: 'loAmbientFloat 8s ease-in-out infinite',
                    }} />
                    {/* Bottom-left glow */}
                    <div style={{
                        position: 'absolute',
                        bottom: '-10%',
                        left: '-8%',
                        width: '350px',
                        height: '350px',
                        borderRadius: '50%',
                        background: 'radial-gradient(circle, rgba(99, 102, 241, 0.05) 0%, transparent 70%)',
                        animation: 'loAmbientFloat 10s ease-in-out infinite reverse',
                    }} />
                    {/* Subtle grid */}
                    <div style={{
                        position: 'absolute',
                        inset: 0,
                        backgroundImage: `
                            linear-gradient(rgba(59, 130, 246, 0.02) 1px, transparent 1px),
                            linear-gradient(90deg, rgba(59, 130, 246, 0.02) 1px, transparent 1px)
                        `,
                        backgroundSize: '50px 50px',
                        maskImage: 'radial-gradient(ellipse 40% 40% at 50% 50%, black 20%, transparent 70%)',
                        WebkitMaskImage: 'radial-gradient(ellipse 40% 40% at 50% 50%, black 20%, transparent 70%)',
                    }} />
                </div>
            )}

            {/* Main loading content */}
            <div style={{
                position: 'relative',
                zIndex: 2,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: '28px',
                animation: 'loFadeIn 0.5s cubic-bezier(0.16, 1, 0.3, 1)',
            }}>
                {/* Animated icon */}
                <div style={{
                    position: 'relative',
                    width: '76px',
                    height: '76px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                }}>
                    {/* Outer rotating ring */}
                    <svg
                        width="76"
                        height="76"
                        viewBox="0 0 76 76"
                        style={{
                            position: 'absolute',
                            animation: 'loOrbitSpin 2.5s linear infinite',
                        }}
                    >
                        <defs>
                            <linearGradient id="loRingGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                                <stop offset="0%" stopColor="#3B82F6" stopOpacity="1" />
                                <stop offset="50%" stopColor="#6366F1" stopOpacity="0.5" />
                                <stop offset="100%" stopColor="#3B82F6" stopOpacity="0" />
                            </linearGradient>
                        </defs>
                        <circle
                            cx="38"
                            cy="38"
                            r="34"
                            fill="none"
                            stroke="url(#loRingGrad)"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeDasharray="150 64"
                        />
                    </svg>

                    {/* Pulsing ring */}
                    <div style={{
                        position: 'absolute',
                        width: '60px',
                        height: '60px',
                        borderRadius: '50%',
                        border: '1px solid rgba(59, 130, 246, 0.12)',
                        animation: 'loPulseRing 2s ease-in-out infinite',
                    }} />

                    {/* Inner icon */}
                    <div style={{
                        width: '46px',
                        height: '46px',
                        borderRadius: '14px',
                        background: 'linear-gradient(135deg, rgba(59, 130, 246, 0.1) 0%, rgba(99, 102, 241, 0.07) 100%)',
                        border: '1px solid rgba(59, 130, 246, 0.18)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        backdropFilter: 'blur(8px)',
                        position: 'relative',
                        zIndex: 2,
                    }}>
                        <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
                            <path
                                d="M12 2L4 6v5c0 5.55 3.84 10.74 8 12 4.16-1.26 8-6.45 8-12V6l-8-4z"
                                fill="none"
                                stroke="#3B82F6"
                                strokeWidth="1.5"
                                strokeLinejoin="round"
                            />
                            <path
                                d="M12 6L8 8v3c0 3.33 2.3 6.45 4 7.2 1.7-.75 4-3.87 4-7.2V8l-4-2z"
                                fill="rgba(59, 130, 246, 0.15)"
                                stroke="#6366F1"
                                strokeWidth="1"
                                strokeLinejoin="round"
                            />
                            <circle cx="12" cy="11" r="1.5" fill="#3B82F6" />
                        </svg>
                    </div>

                    {/* Orbiting dot */}
                    <div style={{
                        position: 'absolute',
                        width: '5px',
                        height: '5px',
                        borderRadius: '50%',
                        background: '#3B82F6',
                        boxShadow: '0 0 10px rgba(59, 130, 246, 0.5)',
                        animation: 'loOrbitDot 2.5s linear infinite',
                        transformOrigin: '0 0',
                        top: '50%',
                        left: '50%',
                    }} />
                </div>

                {/* Text */}
                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: '10px',
                }}>
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                    }}>
                        <span style={{
                            fontSize: '18px',
                            fontWeight: '700',
                            color: 'var(--text-primary)',
                            letterSpacing: '-0.03em',
                            fontFamily: "'Inter', sans-serif",
                        }}>RADAR</span>
                        <span style={{
                            fontSize: '10px',
                            fontWeight: '500',
                            color: '#3B82F6',
                            letterSpacing: '0.06em',
                            textTransform: 'uppercase' as const,
                            padding: '2px 6px',
                            borderRadius: '4px',
                            background: 'rgba(59, 130, 246, 0.08)',
                            border: '1px solid rgba(59, 130, 246, 0.12)',
                        }}>Security</span>
                    </div>

                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                    }}>
                        <div style={{
                            width: '4px',
                            height: '4px',
                            borderRadius: '50%',
                            background: '#3B82F6',
                            animation: 'loStatusPulse 1.5s ease-in-out infinite',
                        }} />
                        <span style={{
                            fontSize: '12px',
                            color: 'var(--text-muted)',
                            fontWeight: '500',
                            letterSpacing: '-0.01em',
                            fontFamily: "'Inter', sans-serif",
                        }}>{statusText}...</span>
                    </div>
                </div>

                {/* Progress bar */}
                <div style={{
                    width: '200px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '6px',
                    alignItems: 'center',
                }}>
                    <div style={{
                        width: '100%',
                        height: '2.5px',
                        borderRadius: '4px',
                        background: 'var(--border)',
                        overflow: 'hidden',
                        position: 'relative',
                    }}>
                        <div style={{
                            height: '100%',
                            borderRadius: '4px',
                            background: 'linear-gradient(90deg, #3B82F6, #6366F1)',
                            width: `${progress}%`,
                            transition: 'width 0.6s cubic-bezier(0.4, 0, 0.2, 1)',
                            position: 'relative',
                        }}>
                            <div style={{
                                position: 'absolute',
                                inset: 0,
                                background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.3), transparent)',
                                animation: 'loShimmer 1.5s infinite',
                            }} />
                        </div>
                    </div>
                    <span style={{
                        fontSize: '10px',
                        color: 'var(--text-light)',
                        fontWeight: '500',
                        fontVariantNumeric: 'tabular-nums',
                        fontFamily: "'Inter', sans-serif",
                    }}>{Math.round(progress)}%</span>
                </div>
            </div>

            <style>{`
                @keyframes loFadeIn {
                    from { opacity: 0; transform: translateY(10px) scale(0.97); }
                    to { opacity: 1; transform: translateY(0) scale(1); }
                }
                @keyframes loOrbitSpin {
                    from { transform: rotate(0deg); }
                    to { transform: rotate(360deg); }
                }
                @keyframes loOrbitDot {
                    from { transform: rotate(0deg) translateX(34px) rotate(0deg); }
                    to { transform: rotate(360deg) translateX(34px) rotate(-360deg); }
                }
                @keyframes loPulseRing {
                    0%, 100% { transform: scale(1); opacity: 0.4; }
                    50% { transform: scale(1.06); opacity: 0.12; }
                }
                @keyframes loStatusPulse {
                    0%, 100% { opacity: 1; transform: scale(1); }
                    50% { opacity: 0.3; transform: scale(0.7); }
                }
                @keyframes loShimmer {
                    0% { transform: translateX(-100%); }
                    100% { transform: translateX(200%); }
                }
                @keyframes loAmbientFloat {
                    0%, 100% { transform: translate(0, 0) scale(1); }
                    33% { transform: translate(15px, -15px) scale(1.03); }
                    66% { transform: translate(-10px, 10px) scale(0.97); }
                }
            `}</style>
        </div>
    )
}
