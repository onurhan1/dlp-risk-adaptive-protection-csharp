'use client'

import React, { useState, useRef, useEffect } from 'react'

export interface FilterConfig {
    key: string
    label: string
    type: 'text' | 'date' | 'dateRange' | 'select' | 'multiSelect'
    options?: { value: string; label: string }[]
    placeholder?: string
}

export interface GridFiltersProps {
    filters: FilterConfig[]
    values: Record<string, any>
    onChange: (key: string, value: any) => void
    onReset: () => void
    labels?: {
        reset?: string
        filter?: string
        from?: string
        to?: string
        all?: string
        selected?: string
    }
}

const defaultLabels = {
    reset: 'Sıfırla',
    filter: 'Filtrele',
    from: 'Başlangıç',
    to: 'Bitiş',
    all: 'Tümü',
    selected: 'seçili',
}

export default function GridFilters({
    filters,
    values,
    onChange,
    onReset,
    labels: customLabels,
}: GridFiltersProps) {
    const labels = { ...defaultLabels, ...customLabels }
    const [openDropdown, setOpenDropdown] = useState<string | null>(null)
    const dropdownRef = useRef<HTMLDivElement>(null)

    useEffect(() => {
        const handleClickOutside = (e: MouseEvent) => {
            if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
                setOpenDropdown(null)
            }
        }
        document.addEventListener('mousedown', handleClickOutside)
        return () => document.removeEventListener('mousedown', handleClickOutside)
    }, [])

    const hasActiveFilters = Object.values(values).some(v => {
        if (Array.isArray(v)) return v.length > 0
        if (typeof v === 'object' && v !== null) return Object.values(v).some(Boolean)
        return v !== '' && v !== null && v !== undefined
    })

    const inputStyle: React.CSSProperties = {
        padding: '6px 10px',
        borderRadius: '6px',
        border: '1px solid var(--border)',
        background: 'var(--surface)',
        color: 'var(--text-primary)',
        fontSize: '12px',
        outline: 'none',
        minWidth: '120px',
        transition: 'border-color 0.15s ease',
    }

    return (
        <div style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: '8px',
            alignItems: 'flex-end',
            padding: '12px 0',
        }}
            ref={dropdownRef}
        >
            {filters.map(filter => (
                <div key={filter.key} style={{ display: 'flex', flexDirection: 'column', gap: '3px' }}>
                    <label style={{
                        fontSize: '10px',
                        fontWeight: '600',
                        color: 'var(--text-muted)',
                        textTransform: 'none',
                        letterSpacing: '0.05em',
                    }}>
                        {filter.label}
                    </label>

                    {/* Text input */}
                    {filter.type === 'text' && (
                        <input
                            type="text"
                            value={values[filter.key] || ''}
                            onChange={(e) => onChange(filter.key, e.target.value)}
                            placeholder={filter.placeholder || filter.label}
                            style={inputStyle}
                        />
                    )}

                    {/* Date input */}
                    {filter.type === 'date' && (
                        <input
                            type="date"
                            value={values[filter.key] || ''}
                            onChange={(e) => onChange(filter.key, e.target.value)}
                            style={inputStyle}
                        />
                    )}

                    {/* Date range */}
                    {filter.type === 'dateRange' && (
                        <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
                            <input
                                type="date"
                                value={values[filter.key]?.start || ''}
                                onChange={(e) => onChange(filter.key, { ...values[filter.key], start: e.target.value })}
                                style={{ ...inputStyle, minWidth: '100px' }}
                                title={labels.from}
                            />
                            <span style={{ color: 'var(--text-muted)', fontSize: '11px' }}>-</span>
                            <input
                                type="date"
                                value={values[filter.key]?.end || ''}
                                onChange={(e) => onChange(filter.key, { ...values[filter.key], end: e.target.value })}
                                style={{ ...inputStyle, minWidth: '100px' }}
                                title={labels.to}
                            />
                        </div>
                    )}

                    {/* Select */}
                    {filter.type === 'select' && (
                        <select
                            value={values[filter.key] || ''}
                            onChange={(e) => onChange(filter.key, e.target.value)}
                            style={{ ...inputStyle, cursor: 'pointer' }}
                        >
                            <option value="">{labels.all}</option>
                            {filter.options?.map(opt => (
                                <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                        </select>
                    )}

                    {/* Multi-select dropdown */}
                    {filter.type === 'multiSelect' && (
                        <div style={{ position: 'relative' }}>
                            <button
                                onClick={() => setOpenDropdown(openDropdown === filter.key ? null : filter.key)}
                                style={{
                                    ...inputStyle,
                                    cursor: 'pointer',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'space-between',
                                    gap: '6px',
                                    minWidth: '140px',
                                }}
                            >
                                <span style={{
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap',
                                }}>
                                    {(values[filter.key] || []).length > 0
                                        ? `${(values[filter.key] || []).length} ${labels.selected}`
                                        : labels.all
                                    }
                                </span>
                                <span style={{ fontSize: '8px', opacity: 0.6 }}>▼</span>
                            </button>

                            {openDropdown === filter.key && (
                                <div style={{
                                    position: 'absolute',
                                    top: '100%',
                                    left: 0,
                                    zIndex: 1000,
                                    background: 'var(--surface)',
                                    border: '1px solid var(--border)',
                                    borderRadius: '6px',
                                    boxShadow: '0 4px 16px rgba(0,0,0,0.15)',
                                    maxHeight: '200px',
                                    overflowY: 'auto',
                                    minWidth: '160px',
                                    marginTop: '4px',
                                }}>
                                    {filter.options?.map(opt => {
                                        const selected = (values[filter.key] || []).includes(opt.value)
                                        return (
                                            <div
                                                key={opt.value}
                                                onClick={() => {
                                                    const current = values[filter.key] || []
                                                    const newValues = selected
                                                        ? current.filter((v: string) => v !== opt.value)
                                                        : [...current, opt.value]
                                                    onChange(filter.key, newValues)
                                                }}
                                                style={{
                                                    padding: '6px 10px',
                                                    fontSize: '12px',
                                                    cursor: 'pointer',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '6px',
                                                    background: selected ? 'rgba(59, 130, 246, 0.08)' : 'transparent',
                                                    transition: 'background 0.1s',
                                                }}
                                            >
                                                <span style={{
                                                    width: '14px',
                                                    height: '14px',
                                                    borderRadius: '3px',
                                                    border: selected ? '2px solid #3b82f6' : '2px solid var(--border)',
                                                    background: selected ? '#3b82f6' : 'transparent',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'center',
                                                    fontSize: '10px',
                                                    color: 'white',
                                                    flexShrink: 0,
                                                }}>
                                                    {selected && '✓'}
                                                </span>
                                                <span style={{
                                                    overflow: 'hidden',
                                                    textOverflow: 'ellipsis',
                                                    whiteSpace: 'nowrap',
                                                }}>
                                                    {opt.label}
                                                </span>
                                            </div>
                                        )
                                    })}
                                </div>
                            )}
                        </div>
                    )}
                </div>
            ))}

            {/* Reset button */}
            {hasActiveFilters && (
                <button
                    onClick={onReset}
                    style={{
                        padding: '6px 12px',
                        borderRadius: '6px',
                        border: '1px solid rgba(239, 68, 68, 0.3)',
                        background: 'rgba(239, 68, 68, 0.08)',
                        color: '#ef4444',
                        fontSize: '12px',
                        fontWeight: '500',
                        cursor: 'pointer',
                        transition: 'all 0.15s ease',
                        alignSelf: 'flex-end',
                    }}
                >
                    ✕ {labels.reset}
                </button>
            )}
        </div>
    )
}
