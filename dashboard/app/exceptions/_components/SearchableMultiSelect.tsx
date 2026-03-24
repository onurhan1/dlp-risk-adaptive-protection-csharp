'use client'

import React, { useState, useEffect, useRef, memo } from 'react'
import { ChevronDown, X, Check, Search } from 'lucide-react'

interface SearchableMultiSelectProps {
  label: string
  options: string[]
  selectedValues: string[]
  onChange: (values: string[]) => void
  placeholder?: string
  compact?: boolean
}

export default memo(function SearchableMultiSelect({ label, options, selectedValues, onChange, placeholder, compact = false }: SearchableMultiSelectProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const dropdownRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handleClickOutside = (event: Event) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false)
        setSearchQuery('')
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const filteredOptions = options.filter(opt =>
    opt.toLowerCase().includes(searchQuery.toLowerCase())
  )

  const toggleValue = (value: string) => {
    if (selectedValues.includes(value)) {
      onChange(selectedValues.filter(v => v !== value))
    } else {
      onChange([...selectedValues, value])
    }
  }

  const toggleAll = () => {
    if (selectedValues.length === filteredOptions.length && filteredOptions.length > 0) {
      onChange([])
    } else {
      onChange([...filteredOptions])
    }
  }

  const displayText = selectedValues.length === 0
    ? (placeholder || 'All')
    : selectedValues.length === 1
      ? selectedValues[0]
      : `${selectedValues.length} selected`

  return (
    <div ref={dropdownRef} style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: compact ? '2px' : '8px' }}>
      <label style={{
        fontSize: compact ? '10px' : '12px',
        fontWeight: '600',
        color: 'var(--text-secondary)',
        textTransform: compact ? 'uppercase' : 'none' as any,
        display: 'block',
        letterSpacing: compact ? '0.5px' : '0.3px'
      }}>
        {label}
      </label>
      <button
        onClick={() => {
          setIsOpen(!isOpen)
          if (!isOpen) setSearchQuery('')
        }}
        style={{
          width: '100%',
          padding: compact ? '5px 8px' : '10px 12px',
          borderRadius: compact ? '6px' : '8px',
          border: selectedValues.length > 0 ? '1.5px solid #3b82f6' : '1px solid var(--border)',
          background: selectedValues.length > 0 ? 'rgba(59, 130, 246, 0.04)' : 'var(--surface)',
          color: 'var(--text-primary)',
          fontSize: compact ? '12px' : '13px',
          cursor: 'pointer',
          textAlign: 'left',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: '6px',
          transition: 'all 0.2s ease',
          minWidth: compact ? '130px' : undefined,
          minHeight: compact ? undefined : '20px',
          boxShadow: isOpen ? '0 0 0 3px rgba(59, 130, 246, 0.12)' : 'none'
        }}
      >
        <span style={{
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          flex: 1,
          color: selectedValues.length > 0 ? '#3b82f6' : 'var(--text-primary)'
        }}>
          {displayText}
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '4px', flexShrink: 0 }}>
          {selectedValues.length > 0 && (
            <span style={{
              background: 'linear-gradient(135deg, #3b82f6, #2563eb)',
              color: '#fff',
              borderRadius: '10px',
              padding: '1px 6px',
              fontSize: '10px',
              fontWeight: '700',
              minWidth: '18px',
              textAlign: 'center',
              lineHeight: '16px'
            }}>
              {selectedValues.length}
            </span>
          )}
          <ChevronDown size={compact ? 12 : 14} style={{
            transform: isOpen ? 'rotate(180deg)' : 'rotate(0deg)',
            transition: 'transform 0.2s ease',
            color: 'var(--text-secondary)'
          }} />
        </div>
      </button>

      {isOpen && (
        <div style={{
          position: 'absolute',
          top: '100%',
          left: 0,
          right: compact ? undefined : 0,
          zIndex: 100,
          background: 'var(--surface)',
          border: '1px solid var(--border)',
          borderRadius: '10px',
          boxShadow: '0 12px 28px rgba(0,0,0,0.18), 0 4px 10px rgba(0,0,0,0.08)',
          minWidth: compact ? '240px' : '100%',
          maxHeight: '340px',
          marginTop: '4px',
          overflow: 'hidden'
        }}>
          <div style={{
            padding: '10px 10px 8px',
            borderBottom: '1px solid var(--border)',
            position: 'sticky',
            top: 0,
            background: 'var(--surface)',
            zIndex: 1
          }}>
            <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
              <Search size={14} style={{ position: 'absolute', left: '10px', color: 'var(--text-secondary)', pointerEvents: 'none' }} />
              <input
                type="text"
                placeholder="Ara..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                autoFocus
                style={{
                  width: '100%',
                  padding: '8px 32px 8px 32px',
                  borderRadius: '6px',
                  border: '1px solid var(--border)',
                  background: 'var(--background)',
                  color: 'var(--text-primary)',
                  fontSize: '12px',
                  outline: 'none',
                  boxSizing: 'border-box',
                  transition: 'border-color 0.2s'
                }}
                onFocus={(e) => e.currentTarget.style.borderColor = '#3b82f6'}
                onBlur={(e) => e.currentTarget.style.borderColor = 'var(--border)'}
                onClick={(e) => e.stopPropagation()}
              />
              {searchQuery && (
                <X size={14}
                  style={{ position: 'absolute', right: '10px', color: 'var(--text-secondary)', cursor: 'pointer' }}
                  onClick={(e) => { e.stopPropagation(); setSearchQuery('') }}
                />
              )}
            </div>
          </div>

          <div
            onClick={(e) => { e.stopPropagation(); toggleAll() }}
            style={{
              padding: '8px 12px',
              borderBottom: '1px solid var(--border)',
              cursor: 'pointer',
              fontSize: '11px',
              fontWeight: '600',
              color: '#3b82f6',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              transition: 'background 0.15s',
              background: 'transparent'
            }}
            onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(59, 130, 246, 0.05)')}
            onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
          >
            {selectedValues.length === filteredOptions.length && filteredOptions.length > 0 ? (
              <><Check size={12} /> Seçimi Kaldır</>
            ) : (
              <>Tümünü Seç ({filteredOptions.length})</>
            )}
          </div>

          <div style={{ maxHeight: '240px', overflowY: 'auto' }}>
            {filteredOptions.map(option => {
              const isSelected = selectedValues.includes(option)
              return (
                <label
                  key={option}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '10px',
                    padding: '7px 12px',
                    cursor: 'pointer',
                    fontSize: '12px',
                    color: 'var(--text-primary)',
                    borderBottom: '1px solid var(--border)',
                    background: isSelected ? 'rgba(59, 130, 246, 0.06)' : 'transparent',
                    transition: 'background 0.12s'
                  }}
                  onClick={(e) => e.stopPropagation()}
                  onMouseEnter={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'var(--surface-hover)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = isSelected ? 'rgba(59, 130, 246, 0.06)' : 'transparent'
                  }}
                >
                  <div style={{
                    width: '16px',
                    height: '16px',
                    borderRadius: '4px',
                    border: isSelected ? '1.5px solid #3b82f6' : '1.5px solid var(--border)',
                    background: isSelected ? '#3b82f6' : 'transparent',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    transition: 'all 0.15s',
                    flexShrink: 0
                  }}>
                    {isSelected && <Check size={10} color="#fff" strokeWidth={3} />}
                  </div>
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={() => toggleValue(option)}
                    style={{ display: 'none' }}
                  />
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', lineHeight: '1.4' }}>
                    {option}
                  </span>
                </label>
              )
            })}
            {filteredOptions.length === 0 && (
              <div style={{ padding: '20px', fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center', fontStyle: 'italic' }}>
                Sonuç bulunamadı
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
})
