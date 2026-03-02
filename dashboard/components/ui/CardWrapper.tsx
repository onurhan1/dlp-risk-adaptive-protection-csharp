'use client'

import React from 'react'

interface CardWrapperProps {
    children: React.ReactNode
    title?: string
    titleRight?: React.ReactNode
    className?: string
    style?: React.CSSProperties
    noPadding?: boolean
}

export default function CardWrapper({
    children,
    title,
    titleRight,
    className = '',
    style,
    noPadding = false,
}: CardWrapperProps) {
    return (
        <div
            className={className}
            style={{
                background: 'var(--surface)',
                borderRadius: '16px',
                boxShadow: '0 4px 20px rgba(15, 23, 42, 0.06)',
                padding: noPadding ? 0 : '24px',
                transition: 'all 0.2s ease',
                color: 'var(--text-primary)',
                overflow: 'hidden',
                ...style,
            }}
        >
            {title && (
                <div style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    marginBottom: '20px',
                    padding: noPadding ? '24px 24px 0 24px' : undefined,
                }}>
                    <h2 style={{
                        fontSize: '16px',
                        fontWeight: 600,
                        color: 'var(--text-primary)',
                        margin: 0,
                        letterSpacing: '-0.01em',
                    }}>
                        {title}
                    </h2>
                    {titleRight && (
                        <div>{titleRight}</div>
                    )}
                </div>
            )}
            {children}
        </div>
    )
}
