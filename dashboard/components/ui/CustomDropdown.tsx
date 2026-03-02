'use client'

import React, { useState, useRef, useEffect } from 'react'
import { ChevronDown } from 'lucide-react'

interface DropdownOption {
    value: string
    label: string
}

interface CustomDropdownProps {
    options: DropdownOption[]
    value: string
    onChange: (value: string) => void
    icon?: React.ReactNode
    placeholder?: string
    className?: string
}

export default function CustomDropdown({
    options,
    value,
    onChange,
    icon,
    placeholder = 'Select...',
    className = '',
}: CustomDropdownProps) {
    const [isOpen, setIsOpen] = useState(false)
    const dropdownRef = useRef<HTMLDivElement>(null)

    const selectedOption = options.find(o => o.value === value)

    useEffect(() => {
        function handleClickOutside(e: MouseEvent) {
            if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
                setIsOpen(false)
            }
        }
        document.addEventListener('mousedown', handleClickOutside)
        return () => document.removeEventListener('mousedown', handleClickOutside)
    }, [])

    return (
        <div ref={dropdownRef} className={className} style={{ position: 'relative' }}>
            <button
                onClick={() => setIsOpen(!isOpen)}
                style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '6px 12px',
                    fontSize: '14px',
                    color: 'var(--text-muted)',
                    border: '1px solid var(--border)',
                    borderRadius: '8px',
                    background: 'transparent',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                    fontFamily: 'Inter, sans-serif',
                    whiteSpace: 'nowrap',
                }}
                onMouseEnter={(e) => {
                    e.currentTarget.style.color = 'var(--text-primary)'
                    e.currentTarget.style.borderColor = 'var(--border-hover)'
                }}
                onMouseLeave={(e) => {
                    e.currentTarget.style.color = 'var(--text-muted)'
                    e.currentTarget.style.borderColor = 'var(--border)'
                }}
            >
                {icon}
                {selectedOption?.label || placeholder}
                <ChevronDown size={16} />
            </button>

            {isOpen && (
                <div style={{
                    position: 'absolute',
                    right: 0,
                    marginTop: '8px',
                    width: '192px',
                    background: 'var(--surface)',
                    border: '1px solid var(--border)',
                    borderRadius: '8px',
                    boxShadow: '0 4px 16px rgba(15, 23, 42, 0.1)',
                    zIndex: 50,
                    overflow: 'hidden',
                }}>
                    {options.map((option) => (
                        <button
                            key={option.value}
                            onClick={() => {
                                onChange(option.value)
                                setIsOpen(false)
                            }}
                            style={{
                                width: '100%',
                                textAlign: 'left',
                                padding: '8px 16px',
                                fontSize: '14px',
                                color: value === option.value ? 'var(--text-primary)' : 'var(--text-muted)',
                                fontWeight: value === option.value ? 600 : 400,
                                background: 'transparent',
                                border: 'none',
                                cursor: 'pointer',
                                transition: 'all 0.15s ease',
                                fontFamily: 'Inter, sans-serif',
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.background = 'var(--surface-hover)'
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.background = 'transparent'
                            }}
                        >
                            {option.label}
                        </button>
                    ))}
                </div>
            )}
        </div>
    )
}
