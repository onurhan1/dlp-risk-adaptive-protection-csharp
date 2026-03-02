'use client'

import React from 'react'

type RiskLevel = 'critical' | 'high' | 'medium' | 'low' | 'safe'

interface StatusBadgeProps {
    level: RiskLevel
    children: React.ReactNode
    size?: 'sm' | 'md'
    className?: string
}

const colorMap: Record<RiskLevel, {
    lightBg: string
    darkBg: string
    lightText: string
    darkText: string
}> = {
    critical: {
        lightBg: '#FEF2F2',
        darkBg: '#450a0a',
        lightText: '#EF4444',
        darkText: '#FCA5A5',
    },
    high: {
        lightBg: '#FEF3C7',
        darkBg: '#451a03',
        lightText: '#F59E0B',
        darkText: '#FCD34D',
    },
    medium: {
        lightBg: '#EEF2FF',
        darkBg: '#1e1b4b',
        lightText: '#6366F1',
        darkText: '#A5B4FC',
    },
    low: {
        lightBg: '#F0FDF4',
        darkBg: '#052e16',
        lightText: '#10B981',
        darkText: '#86EFAC',
    },
    safe: {
        lightBg: '#F9FAFB',
        darkBg: '#1f2937',
        lightText: '#6B7280',
        darkText: '#9CA3AF',
    },
}

export default function StatusBadge({ level, children, size = 'md', className = '' }: StatusBadgeProps) {
    const colors = colorMap[level] || colorMap.safe
    const isDark = typeof document !== 'undefined' && document.documentElement.classList.contains('dark-theme') === false

    const fontSize = size === 'sm' ? '10px' : '11px'
    const padding = size === 'sm' ? '2px 6px' : '4px 8px'

    return (
        <span
            className={`status-badge status-badge-${level} ${className}`}
            style={{
                display: 'inline-flex',
                alignItems: 'center',
                padding,
                borderRadius: '6px',
                fontSize,
                fontWeight: 600,
                whiteSpace: 'nowrap',
                lineHeight: 1.4,
            }}
        >
            {children}
        </span>
    )
}

// CSS classes are defined in globals.css (badge-soft-critical, etc.)
// This component also provides inline styles as fallback
export function getStatusColors(level: RiskLevel, isDark: boolean) {
    const colors = colorMap[level] || colorMap.safe
    return {
        backgroundColor: isDark ? colors.darkBg : colors.lightBg,
        color: isDark ? colors.darkText : colors.lightText,
    }
}
