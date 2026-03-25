'use client'

import { useEffect, useState } from 'react'

export default function Loading() {
    const [progress, setProgress] = useState(0)
    const [statusText, setStatusText] = useState('Sistem başlatılıyor')

    useEffect(() => {
        const statusMessages = [
            'Sistem başlatılıyor',
            'Veriler yükleniyor',
            'Güvenlik kontrolleri',
            'Dashboard hazırlanıyor',
        ]

        let step = 0
        const interval = setInterval(() => {
            step++
            setProgress(prev => {
                const next = prev + Math.random() * 18 + 8
                return Math.min(next, 95)
            })
            if (step < statusMessages.length) {
                setStatusText(statusMessages[step])
            }
        }, 500)

        return () => clearInterval(interval)
    }, [])

    return (
        <div
            id="radar-global-loading"
            style={{
                position: 'fixed',
                top: 0,
                left: 0,
                width: '100vw',
                height: '100vh',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: 'var(--background)',
                zIndex: 999999, // Chatbot dahil her şeyin üstünde
                isolation: 'isolate',
            }}
        >
            {/* Ambient background effects */}
            <div style={{
                position: 'absolute',
                inset: 0,
                overflow: 'hidden',
                pointerEvents: 'none',
            }}>
                {/* Top-right glow */}
                <div style={{
                    position: 'absolute',
                    top: '-20%',
                    right: '-10%',
                    width: '600px',
                    height: '600px',
                    borderRadius: '50%',
                    background: 'radial-gradient(circle, rgba(59, 130, 246, 0.08) 0%, transparent 70%)',
                    animation: 'ambientFloat 8s ease-in-out infinite',
                }} />
                {/* Bottom-left glow */}
                <div style={{
                    position: 'absolute',
                    bottom: '-15%',
                    left: '-10%',
                    width: '500px',
                    height: '500px',
                    borderRadius: '50%',
                    background: 'radial-gradient(circle, rgba(99, 102, 241, 0.06) 0%, transparent 70%)',
                    animation: 'ambientFloat 10s ease-in-out infinite reverse',
                }} />
                {/* Center subtle grid pattern */}
                <div style={{
                    position: 'absolute',
                    inset: 0,
                    backgroundImage: `
                        linear-gradient(rgba(59, 130, 246, 0.03) 1px, transparent 1px),
                        linear-gradient(90deg, rgba(59, 130, 246, 0.03) 1px, transparent 1px)
                    `,
                    backgroundSize: '60px 60px',
                    maskImage: 'radial-gradient(ellipse 50% 50% at 50% 50%, black 30%, transparent 70%)',
                    WebkitMaskImage: 'radial-gradient(ellipse 50% 50% at 50% 50%, black 30%, transparent 70%)',
                }} />
            </div>

            {/* Main loading card */}
            <div style={{
                position: 'relative',
                zIndex: 2,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: '32px',
                animation: 'loadingFadeIn 0.6s cubic-bezier(0.16, 1, 0.3, 1)',
            }}>
                {/* Logo / Shield Icon */}
                <div style={{
                    position: 'relative',
                    width: '88px',
                    height: '88px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                }}>
                    {/* Outer rotating ring */}
                    <svg
                        width="88"
                        height="88"
                        viewBox="0 0 88 88"
                        style={{
                            position: 'absolute',
                            animation: 'orbitSpin 3s linear infinite',
                        }}
                    >
                        <defs>
                            <linearGradient id="ringGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                                <stop offset="0%" stopColor="#3B82F6" stopOpacity="1" />
                                <stop offset="50%" stopColor="#6366F1" stopOpacity="0.6" />
                                <stop offset="100%" stopColor="#3B82F6" stopOpacity="0" />
                            </linearGradient>
                        </defs>
                        <circle
                            cx="44"
                            cy="44"
                            r="40"
                            fill="none"
                            stroke="url(#ringGrad)"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeDasharray="180 72"
                        />
                    </svg>

                    {/* Middle pulsing ring */}
                    <div style={{
                        position: 'absolute',
                        width: '72px',
                        height: '72px',
                        borderRadius: '50%',
                        border: '1px solid rgba(59, 130, 246, 0.15)',
                        animation: 'pulseRing 2s ease-in-out infinite',
                    }} />

                    {/* Inner icon container */}
                    <div style={{
                        width: '56px',
                        height: '56px',
                        borderRadius: '16px',
                        background: 'linear-gradient(135deg, rgba(59, 130, 246, 0.12) 0%, rgba(99, 102, 241, 0.08) 100%)',
                        border: '1px solid rgba(59, 130, 246, 0.2)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        backdropFilter: 'blur(10px)',
                        position: 'relative',
                        zIndex: 2,
                    }}>
                        {/* Shield/Radar icon */}
                        <svg width="28" height="28" viewBox="0 0 24 24" fill="none">
                            <path
                                d="M12 2L4 6v5c0 5.55 3.84 10.74 8 12 4.16-1.26 8-6.45 8-12V6l-8-4z"
                                fill="none"
                                stroke="#3B82F6"
                                strokeWidth="1.5"
                                strokeLinejoin="round"
                            />
                            <path
                                d="M12 6L8 8v3c0 3.33 2.3 6.45 4 7.2 1.7-.75 4-3.87 4-7.2V8l-4-2z"
                                fill="rgba(59, 130, 246, 0.2)"
                                stroke="#6366F1"
                                strokeWidth="1"
                                strokeLinejoin="round"
                            />
                            <circle cx="12" cy="11" r="2" fill="#3B82F6" />
                        </svg>
                    </div>

                    {/* Orbiting dot */}
                    <div style={{
                        position: 'absolute',
                        width: '6px',
                        height: '6px',
                        borderRadius: '50%',
                        background: '#3B82F6',
                        boxShadow: '0 0 12px rgba(59, 130, 246, 0.6)',
                        animation: 'orbitDot 3s linear infinite',
                        transformOrigin: '0 0',
                        top: '50%',
                        left: '50%',
                    }} />
                </div>

                {/* Text content */}
                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: '12px',
                }}>
                    {/* Brand name */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                    }}>
                        <span style={{
                            fontSize: '22px',
                            fontWeight: '700',
                            color: 'var(--text-primary)',
                            letterSpacing: '-0.03em',
                            fontFamily: "'Inter', sans-serif",
                        }}>RADAR</span>
                        <span style={{
                            fontSize: '11px',
                            fontWeight: '500',
                            color: '#3B82F6',
                            letterSpacing: '0.08em',
                            textTransform: 'uppercase',
                            padding: '2px 8px',
                            borderRadius: '4px',
                            background: 'rgba(59, 130, 246, 0.1)',
                            border: '1px solid rgba(59, 130, 246, 0.15)',
                        }}>Security</span>
                    </div>

                    {/* Status text */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                    }}>
                        <div style={{
                            width: '5px',
                            height: '5px',
                            borderRadius: '50%',
                            background: '#3B82F6',
                            animation: 'statusPulse 1.5s ease-in-out infinite',
                        }} />
                        <span style={{
                            fontSize: '13px',
                            color: 'var(--text-muted)',
                            fontWeight: '500',
                            letterSpacing: '-0.01em',
                            fontFamily: "'Inter', sans-serif",
                        }}>{statusText}...</span>
                    </div>
                </div>

                {/* Progress bar */}
                <div style={{
                    width: '240px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                    alignItems: 'center',
                }}>
                    <div style={{
                        width: '100%',
                        height: '3px',
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
                            transition: 'width 0.5s cubic-bezier(0.4, 0, 0.2, 1)',
                            position: 'relative',
                        }}>
                            {/* Shimmer effect on progress bar */}
                            <div style={{
                                position: 'absolute',
                                inset: 0,
                                background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.3), transparent)',
                                animation: 'shimmer 1.5s infinite',
                            }} />
                        </div>
                    </div>
                    <span style={{
                        fontSize: '11px',
                        color: 'var(--text-light)',
                        fontWeight: '500',
                        fontVariantNumeric: 'tabular-nums',
                        fontFamily: "'Inter', sans-serif",
                    }}>{Math.round(progress)}%</span>
                </div>
            </div>

            <style>{`
                @keyframes loadingFadeIn {
                    from {
                        opacity: 0;
                        transform: translateY(12px) scale(0.97);
                    }
                    to {
                        opacity: 1;
                        transform: translateY(0) scale(1);
                    }
                }

                @keyframes orbitSpin {
                    from { transform: rotate(0deg); }
                    to { transform: rotate(360deg); }
                }

                @keyframes orbitDot {
                    from {
                        transform: rotate(0deg) translateX(40px) rotate(0deg);
                    }
                    to {
                        transform: rotate(360deg) translateX(40px) rotate(-360deg);
                    }
                }

                @keyframes pulseRing {
                    0%, 100% {
                        transform: scale(1);
                        opacity: 0.4;
                    }
                    50% {
                        transform: scale(1.08);
                        opacity: 0.15;
                    }
                }

                @keyframes statusPulse {
                    0%, 100% { opacity: 1; transform: scale(1); }
                    50% { opacity: 0.4; transform: scale(0.8); }
                }

                @keyframes shimmer {
                    0% { transform: translateX(-100%); }
                    100% { transform: translateX(200%); }
                }

                @keyframes ambientFloat {
                    0%, 100% { transform: translate(0, 0) scale(1); }
                    33% { transform: translate(20px, -20px) scale(1.05); }
                    66% { transform: translate(-15px, 15px) scale(0.95); }
                }
            `}</style>
        </div>
    )
}
