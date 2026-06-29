'use client'

import React, { useState, useRef, useEffect } from 'react'
import { FileText, FileSpreadsheet, FileImage, Download, ChevronDown } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'

export interface ExportColumn {
    key: string
    header: string
    width?: number
    formatter?: (value: any) => string
}

export interface GridExportProps {
    data: any[]
    columns: ExportColumn[]
    fileName: string
    formats?: ('csv' | 'xlsx' | 'pdf')[]
    disabled?: boolean
    labels?: {
        csv?: string
        xlsx?: string
        pdf?: string
        exporting?: string
    }
}



export default function GridExport({
    data,
    columns,
    fileName,
    formats = ['csv', 'xlsx', 'pdf'],
    disabled = false,
    labels: customLabels,
}: GridExportProps) {
    const { t } = useTranslation()
    const defaultLabels = {
        csv: 'CSV',
        xlsx: 'Excel',
        pdf: 'PDF',
        exporting: t('gridExport.exporting'),
    }
    const labels = { ...defaultLabels, ...customLabels }
    const [exporting, setExporting] = useState<string | null>(null)

    const getFormattedValue = (row: any, col: ExportColumn): string => {
        const value = row[col.key]
        if (col.formatter) return col.formatter(value)
        if (value === null || value === undefined) return ''
        return String(value)
    }

    // ========== CSV Export ==========
    const exportCSV = () => {
        setExporting('csv')
        try {
            const header = columns.map(c => `"${c.header}"`).join(',')
            const rows = data.map(row =>
                columns.map(col => {
                    const val = getFormattedValue(row, col)
                    return `"${val.replace(/"/g, '""')}"`
                }).join(',')
            )
            const csvContent = '\uFEFF' + [header, ...rows].join('\n') // BOM for Turkish chars
            const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
            downloadBlob(blob, `${fileName}.csv`)
        } finally {
            setExporting(null)
        }
    }

    // ========== XLSX Export (using exceljs) ==========
    const exportXLSX = async () => {
        setExporting('xlsx')
        try {
            const ExcelJS = (await import('exceljs')).default
            const workbook = new ExcelJS.Workbook()
            const worksheet = workbook.addWorksheet(fileName)

            // Header
            worksheet.columns = columns.map(col => ({
                header: col.header,
                key: col.key,
                width: col.width || 20,
            }))

            // Style header row
            const headerRow = worksheet.getRow(1)
            headerRow.font = { bold: true, size: 11, color: { argb: 'FFFFFFFF' } }
            headerRow.fill = {
                type: 'pattern',
                pattern: 'solid',
                fgColor: { argb: 'FF3B82F6' },
            }
            headerRow.alignment = { vertical: 'middle', horizontal: 'center' }

            // Data rows
            data.forEach(row => {
                const rowData: Record<string, any> = {}
                columns.forEach(col => {
                    rowData[col.key] = getFormattedValue(row, col)
                })
                worksheet.addRow(rowData)
            })

            // Auto filter
            worksheet.autoFilter = {
                from: { row: 1, column: 1 },
                to: { row: data.length + 1, column: columns.length },
            }

            // Alternate row colors
            worksheet.eachRow((row, rowNumber) => {
                if (rowNumber > 1 && rowNumber % 2 === 0) {
                    row.fill = {
                        type: 'pattern',
                        pattern: 'solid',
                        fgColor: { argb: 'FFF8FAFC' },
                    }
                }
            })

            const buffer = await workbook.xlsx.writeBuffer()
            const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
            downloadBlob(blob, `${fileName}.xlsx`)
        } finally {
            setExporting(null)
        }
    }

    // ========== PDF Export (using jspdf + jspdf-autotable) ==========
    const exportPDF = async () => {
        setExporting('pdf')
        try {
            const { default: jsPDF } = await import('jspdf')
            // Import autoTable and extend jsPDF
            const autoTable = (await import('jspdf-autotable')).default

            const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })

            // Title
            doc.setFontSize(14)
            doc.setTextColor(59, 130, 246) // Blue
            doc.text(fileName, 14, 15)

            // Date
            doc.setFontSize(8)
            doc.setTextColor(128, 128, 128)
            doc.text(`${new Date().toLocaleDateString('tr-TR')} ${new Date().toLocaleTimeString('tr-TR')}`, 14, 21)

            // Table
            const headers = columns.map(c => c.header)
            const rows = data.map(row =>
                columns.map(col => getFormattedValue(row, col))
            )

            // Use autoTable with proper typing
            autoTable(doc, {
                head: [headers],
                body: rows,
                startY: 25,
                styles: {
                    fontSize: 7,
                    cellPadding: 2,
                    overflow: 'linebreak',
                },
                headStyles: {
                    fillColor: [59, 130, 246],
                    textColor: [255, 255, 255],
                    fontStyle: 'bold',
                    fontSize: 8,
                },
                alternateRowStyles: {
                    fillColor: [248, 250, 252],
                },
                margin: { top: 25, left: 10, right: 10 },
            })

            doc.save(`${fileName}.pdf`)
        } finally {
            setExporting(null)
        }
    }

    // ========== Download Helper ==========
    const downloadBlob = (blob: Blob, filename: string) => {
        const url = URL.createObjectURL(blob)
        const link = document.createElement('a')
        link.href = url
        link.download = filename
        document.body.appendChild(link)
        link.click()
        document.body.removeChild(link)
        URL.revokeObjectURL(url)
    }

    const exportHandlers: Record<string, () => void | Promise<void>> = {
        csv: exportCSV,
        xlsx: exportXLSX,
        pdf: exportPDF,
    }

    const [isOpen, setIsOpen] = useState(false)
    const dropdownRef = useRef<HTMLDivElement>(null)

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
                setIsOpen(false)
            }
        }
        document.addEventListener('mousedown', handleClickOutside)
        return () => {
            document.removeEventListener('mousedown', handleClickOutside)
        }
    }, [])

    const isDisabled = disabled || data.length === 0

    return (
        <div ref={dropdownRef} style={{ position: 'relative', display: 'inline-block' }}>
            <button
                onClick={() => setIsOpen(!isOpen)}
                disabled={isDisabled}
                style={{
                    padding: '8px 16px',
                    borderRadius: '6px',
                    border: '1px solid var(--border)',
                    background: 'var(--surface)',
                    color: isDisabled ? 'var(--text-muted)' : 'var(--text-primary)',
                    fontSize: '13px',
                    fontWeight: '500',
                    cursor: isDisabled ? 'not-allowed' : 'pointer',
                    transition: 'all 0.15s ease',
                    opacity: isDisabled ? 0.5 : 1,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
                }}
                onMouseEnter={(e) => {
                    if (!isDisabled) {
                        e.currentTarget.style.background = 'var(--surface-hover)'
                        e.currentTarget.style.borderColor = 'var(--border-hover)'
                    }
                }}
                onMouseLeave={(e) => {
                    if (!isDisabled) {
                        e.currentTarget.style.background = 'var(--surface)'
                        e.currentTarget.style.borderColor = 'var(--border)'
                    }
                }}
            >
                <Download size={16} style={{ color: 'var(--text-secondary)' }} />
                <span>Dışa aktar</span>
                <ChevronDown size={14} style={{ color: 'var(--text-muted)', marginLeft: '4px', transform: isOpen ? 'rotate(180deg)' : 'none', transition: 'transform 0.2s' }} />
            </button>

            {isOpen && (
                <div style={{
                    position: 'absolute',
                    top: '100%',
                    right: 0,
                    marginTop: '4px',
                    background: 'var(--surface)',
                    border: '1px solid var(--border)',
                    borderRadius: '8px',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
                    minWidth: '150px',
                    zIndex: 100,
                    padding: '4px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '2px'
                }}>
                    {formats.map(format => (
                        <button
                            key={format}
                            onClick={() => {
                                setIsOpen(false)
                                exportHandlers[format]?.()
                            }}
                            disabled={exporting !== null}
                            style={{
                                padding: '8px 12px',
                                borderRadius: '4px',
                                border: 'none',
                                background: exporting === format ? 'rgba(59, 130, 246, 0.1)' : 'transparent',
                                color: 'var(--text-primary)',
                                fontSize: '13px',
                                fontWeight: '500',
                                cursor: exporting !== null ? 'wait' : 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '10px',
                                textAlign: 'left',
                                width: '100%'
                            }}
                            onMouseEnter={(e) => { if (exporting === null) e.currentTarget.style.background = 'var(--surface-hover)' }}
                            onMouseLeave={(e) => { if (exporting === null) e.currentTarget.style.background = 'transparent' }}
                        >
                            {exporting === format ? (
                                <span style={{
                                    display: 'inline-block',
                                    width: '14px',
                                    height: '14px',
                                    border: '2px solid rgba(59, 130, 246, 0.2)',
                                    borderTop: '2px solid #3b82f6',
                                    borderRadius: '50%',
                                    animation: 'dlp-spin 0.8s linear infinite',
                                    flexShrink: 0
                                }} />
                            ) : (
                                <>
                                    {format === 'csv' && <FileText size={16} style={{ color: '#10b981' }} />}
                                    {format === 'xlsx' && <FileSpreadsheet size={16} style={{ color: '#059669' }} />}
                                    {format === 'pdf' && <FileImage size={16} style={{ color: '#ef4444' }} />}
                                </>
                            )}
                            <span style={{ flex: 1 }}>{labels[format as keyof typeof labels] || format.toUpperCase()}</span>
                        </button>
                    ))}
                </div>
            )}

            {/* Spinner animation space */}
            <style>{`
        @keyframes dlp-spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `}</style>
        </div>
    )
}
