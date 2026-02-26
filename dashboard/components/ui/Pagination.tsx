'use client'

import React, { useState, useCallback } from 'react'

export interface PaginationProps {
    currentPage: number
    totalPages: number
    totalItems?: number
    pageSize?: number
    onPageChange: (page: number) => void
    onPageSizeChange?: (size: number) => void
    pageSizeOptions?: number[]
    showPageInput?: boolean
    showFirstLast?: boolean
    showPageSizeSelector?: boolean
    showTotalItems?: boolean
    compact?: boolean
    labels?: {
        first?: string
        previous?: string
        next?: string
        last?: string
        page?: string
        of?: string
        totalItems?: string
        pageSize?: string
        goToPage?: string
    }
}

const defaultLabels = {
    first: '⏮',
    previous: '◀',
    next: '▶',
    last: '⏭',
    page: 'Sayfa',
    of: '/',
    totalItems: 'toplam kayıt',
    pageSize: 'Sayfa Boyutu',
    goToPage: 'Git',
}

export default function Pagination({
    currentPage,
    totalPages,
    totalItems,
    pageSize,
    onPageChange,
    onPageSizeChange,
    pageSizeOptions = [10, 25, 50, 100],
    showPageInput = true,
    showFirstLast = true,
    showPageSizeSelector = false,
    showTotalItems = true,
    compact = false,
    labels: customLabels,
}: PaginationProps) {
    const labels = { ...defaultLabels, ...customLabels }
    const [pageInput, setPageInput] = useState('')

    const handlePageInput = useCallback(() => {
        const page = parseInt(pageInput, 10)
        if (!isNaN(page) && page >= 1 && page <= totalPages) {
            onPageChange(page)
            setPageInput('')
        }
    }, [pageInput, totalPages, onPageChange])

    const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
        if (e.key === 'Enter') {
            handlePageInput()
        }
    }, [handlePageInput])

    if (totalPages <= 1 && !showTotalItems) return null

    // Generate visible page numbers
    const getPageNumbers = (): (number | '...')[] => {
        if (totalPages <= 7) {
            return Array.from({ length: totalPages }, (_, i) => i + 1)
        }

        const pages: (number | '...')[] = [1]

        if (currentPage > 3) pages.push('...')

        const start = Math.max(2, currentPage - 1)
        const end = Math.min(totalPages - 1, currentPage + 1)

        for (let i = start; i <= end; i++) {
            pages.push(i)
        }

        if (currentPage < totalPages - 2) pages.push('...')

        if (totalPages > 1) pages.push(totalPages)

        return pages
    }

    return (
        <div style={{
            display: 'flex',
            flexDirection: compact ? 'row' : 'row',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: compact ? '8px' : '16px',
            padding: '12px 0',
            borderTop: '1px solid var(--border)',
            marginTop: '12px',
            flexWrap: 'wrap',
            fontSize: compact ? '11px' : '12px',
        }}>
            {/* Left: Total items info */}
            {showTotalItems && totalItems !== undefined && (
                <div style={{
                    color: 'var(--text-muted)',
                    fontSize: compact ? '11px' : '12px',
                    whiteSpace: 'nowrap',
                }}>
                    <strong style={{ color: 'var(--text-primary)' }}>{totalItems.toLocaleString()}</strong> {labels.totalItems}
                </div>
            )}

            {/* Center: Navigation */}
            <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: compact ? '2px' : '4px',
            }}>
                {/* First page */}
                {showFirstLast && !compact && (
                    <button
                        disabled={currentPage <= 1}
                        onClick={() => onPageChange(1)}
                        style={navButtonStyle(currentPage <= 1, compact)}
                        title="İlk Sayfa"
                    >
                        {labels.first}
                    </button>
                )}

                {/* Previous */}
                <button
                    disabled={currentPage <= 1}
                    onClick={() => onPageChange(currentPage - 1)}
                    style={navButtonStyle(currentPage <= 1, compact)}
                    title="Önceki"
                >
                    {labels.previous}
                </button>

                {/* Page numbers */}
                {!compact && totalPages > 1 && getPageNumbers().map((page, idx) => (
                    page === '...' ? (
                        <span key={`dots-${idx}`} style={{
                            padding: '0 4px',
                            color: 'var(--text-muted)',
                            fontSize: '12px',
                        }}>…</span>
                    ) : (
                        <button
                            key={page}
                            onClick={() => onPageChange(page)}
                            style={{
                                padding: '4px 10px',
                                borderRadius: '6px',
                                border: currentPage === page ? '1px solid #3b82f6' : '1px solid transparent',
                                background: currentPage === page ? 'rgba(59, 130, 246, 0.15)' : 'transparent',
                                color: currentPage === page ? '#3b82f6' : 'var(--text-primary)',
                                fontWeight: currentPage === page ? '700' : '400',
                                fontSize: '12px',
                                cursor: 'pointer',
                                transition: 'all 0.15s ease',
                                minWidth: '32px',
                            }}
                        >
                            {page}
                        </button>
                    )
                ))}

                {/* Compact: Page X / Y display */}
                {compact && (
                    <span style={{
                        padding: '0 8px',
                        color: 'var(--text-muted)',
                        fontSize: '12px',
                        whiteSpace: 'nowrap',
                    }}>
                        {currentPage} {labels.of} {totalPages}
                    </span>
                )}

                {/* Next */}
                <button
                    disabled={currentPage >= totalPages}
                    onClick={() => onPageChange(currentPage + 1)}
                    style={navButtonStyle(currentPage >= totalPages, compact)}
                    title="Sonraki"
                >
                    {labels.next}
                </button>

                {/* Last page */}
                {showFirstLast && !compact && (
                    <button
                        disabled={currentPage >= totalPages}
                        onClick={() => onPageChange(totalPages)}
                        style={navButtonStyle(currentPage >= totalPages, compact)}
                        title="Son Sayfa"
                    >
                        {labels.last}
                    </button>
                )}
            </div>

            {/* Right: Page input & page size */}
            <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: '12px',
            }}>
                {/* Direct page input */}
                {showPageInput && totalPages > 1 && (
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                    }}>
                        <input
                            type="number"
                            min={1}
                            max={totalPages}
                            placeholder={`${currentPage}`}
                            value={pageInput}
                            onChange={(e) => setPageInput(e.target.value)}
                            onKeyDown={handleKeyDown}
                            style={{
                                width: compact ? '40px' : '50px',
                                padding: '4px 6px',
                                borderRadius: '6px',
                                border: '1px solid var(--border)',
                                background: 'var(--surface)',
                                color: 'var(--text-primary)',
                                fontSize: '12px',
                                textAlign: 'center',
                                outline: 'none',
                            }}
                        />
                        <button
                            onClick={handlePageInput}
                            style={{
                                padding: '4px 8px',
                                borderRadius: '6px',
                                border: '1px solid var(--border)',
                                background: 'var(--surface)',
                                color: 'var(--text-primary)',
                                fontSize: '11px',
                                cursor: 'pointer',
                                transition: 'all 0.15s ease',
                            }}
                        >
                            {labels.goToPage}
                        </button>
                    </div>
                )}

                {/* Page size selector */}
                {showPageSizeSelector && onPageSizeChange && pageSize && (
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                    }}>
                        <select
                            value={pageSize}
                            onChange={(e) => onPageSizeChange(Number(e.target.value))}
                            style={{
                                padding: '4px 8px',
                                borderRadius: '6px',
                                border: '1px solid var(--border)',
                                background: 'var(--surface)',
                                color: 'var(--text-primary)',
                                fontSize: '12px',
                                cursor: 'pointer',
                                outline: 'none',
                            }}
                        >
                            {pageSizeOptions.map((size) => (
                                <option key={size} value={size}>
                                    {size}
                                </option>
                            ))}
                        </select>
                        <span style={{ color: 'var(--text-muted)', fontSize: '11px' }}>
                            / {labels.page}
                        </span>
                    </div>
                )}
            </div>
        </div>
    )
}

// Navigation button style helper
function navButtonStyle(disabled: boolean, compact: boolean): React.CSSProperties {
    return {
        padding: compact ? '4px 6px' : '4px 10px',
        borderRadius: '6px',
        border: '1px solid var(--border)',
        background: disabled ? 'var(--surface)' : 'var(--background)',
        color: disabled ? 'var(--text-muted)' : 'var(--text-primary)',
        cursor: disabled ? 'not-allowed' : 'pointer',
        fontSize: compact ? '10px' : '12px',
        transition: 'all 0.15s ease',
        opacity: disabled ? 0.5 : 1,
        lineHeight: 1,
    }
}
