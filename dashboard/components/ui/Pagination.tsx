'use client'

import React, { useState, useCallback } from 'react'
import {
    ChevronLeft,
    ChevronRight,
    ChevronsLeft,
    ChevronsRight,
} from 'lucide-react'

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
    first: 'İlk',
    previous: 'Önceki',
    next: 'Sonraki',
    last: 'Son',
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

    // Button base classes
    const btnBase = `p-2 rounded-lg transition-colors flex items-center justify-center`
    const btnEnabled = `text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] cursor-pointer`
    const btnDisabled = `opacity-30 cursor-not-allowed text-[#64748B] dark:text-[#94A3B8]`

    const iconSize = compact ? 14 : 16

    return (
        <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: '12px',
            paddingTop: '16px',
            borderTop: '1px solid var(--border)',
            marginTop: '16px',
            flexWrap: 'wrap',
        }}>
            {/* Left: Page info & total */}
            <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: '12px',
                fontSize: '13px',
                color: 'var(--text-muted)',
            }}>
                <span>
                    {labels.page} {currentPage} {labels.of} {totalPages}
                </span>
                {showTotalItems && totalItems !== undefined && (
                    <span>
                        · <strong style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{totalItems.toLocaleString()}</strong> {labels.totalItems}
                    </span>
                )}
            </div>

            {/* Right: Navigation */}
            <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
            }}>
                {/* Page size selector */}
                {showPageSizeSelector && onPageSizeChange && pageSize && (
                    <select
                        value={pageSize}
                        onChange={(e) => onPageSizeChange(Number(e.target.value))}
                        style={{
                            padding: '4px 8px',
                            borderRadius: '8px',
                            border: '1px solid var(--border)',
                            background: 'var(--surface)',
                            color: 'var(--text-primary)',
                            fontSize: '13px',
                            cursor: 'pointer',
                            outline: 'none',
                            marginRight: '8px',
                            fontFamily: 'Inter, sans-serif',
                        }}
                    >
                        {pageSizeOptions.map((size) => (
                            <option key={size} value={size}>
                                {size} / {labels.page}
                            </option>
                        ))}
                    </select>
                )}

                {/* First page */}
                {showFirstLast && !compact && (
                    <button
                        disabled={currentPage <= 1}
                        onClick={() => onPageChange(1)}
                        className={`${btnBase} ${currentPage <= 1 ? btnDisabled : btnEnabled}`}
                        title={labels.first}
                    >
                        <ChevronsLeft size={iconSize} />
                    </button>
                )}

                {/* Previous */}
                <button
                    disabled={currentPage <= 1}
                    onClick={() => onPageChange(currentPage - 1)}
                    className={`${btnBase} ${currentPage <= 1 ? btnDisabled : btnEnabled}`}
                    title={labels.previous}
                >
                    <ChevronLeft size={iconSize} />
                </button>

                {/* Page input */}
                {showPageInput && totalPages > 1 && (
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        margin: '0 4px',
                    }}>
                        <span style={{
                            fontSize: '13px',
                            color: 'var(--text-muted)',
                        }}>
                            {labels.goToPage}
                        </span>
                        <input
                            type="number"
                            min={1}
                            max={totalPages}
                            placeholder={`${currentPage}`}
                            value={pageInput}
                            onChange={(e) => setPageInput(e.target.value)}
                            onKeyDown={handleKeyDown}
                            onBlur={handlePageInput}
                            style={{
                                width: '48px',
                                padding: '4px 6px',
                                borderRadius: '8px',
                                border: '1px solid var(--border)',
                                background: 'var(--surface)',
                                color: 'var(--text-primary)',
                                fontSize: '13px',
                                textAlign: 'center',
                                outline: 'none',
                                fontFamily: 'Inter, sans-serif',
                            }}
                        />
                    </div>
                )}

                {/* Compact: Page X / Y display */}
                {compact && !showPageInput && (
                    <span style={{
                        padding: '0 8px',
                        color: 'var(--text-muted)',
                        fontSize: '13px',
                        whiteSpace: 'nowrap',
                    }}>
                        {currentPage} {labels.of} {totalPages}
                    </span>
                )}

                {/* Next */}
                <button
                    disabled={currentPage >= totalPages}
                    onClick={() => onPageChange(currentPage + 1)}
                    className={`${btnBase} ${currentPage >= totalPages ? btnDisabled : btnEnabled}`}
                    title={labels.next}
                >
                    <ChevronRight size={iconSize} />
                </button>

                {/* Last page */}
                {showFirstLast && !compact && (
                    <button
                        disabled={currentPage >= totalPages}
                        onClick={() => onPageChange(totalPages)}
                        className={`${btnBase} ${currentPage >= totalPages ? btnDisabled : btnEnabled}`}
                        title={labels.last}
                    >
                        <ChevronsRight size={iconSize} />
                    </button>
                )}
            </div>
        </div>
    )
}
