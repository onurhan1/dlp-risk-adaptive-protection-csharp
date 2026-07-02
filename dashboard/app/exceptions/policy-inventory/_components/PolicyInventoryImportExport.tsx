import React, { useRef, useState } from 'react'
import { Upload, Download, FileJson, FileSpreadsheet, ChevronDown } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'
import apiClient from '@/lib/axios'
import { PolicyInventorySearchResult } from '../_lib/types'

interface ImportExportProps {
  onImportSuccess: () => void
  searchResults?: PolicyInventorySearchResult[]
  searchQuery?: string
  searchFilter?: string
}

export default function ImportExport({ onImportSuccess, searchResults = [], searchQuery = '', searchFilter = 'all' }: ImportExportProps) {
  const { t } = useTranslation()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [exportOpen, setExportOpen] = useState(false)
  const [isUploading, setIsUploading] = useState(false)

  const handleImportClick = () => {
    fileInputRef.current?.click()
  }

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    setIsUploading(true)
    const formData = new FormData()
    formData.append('file', file)

    try {
      // Endpoint to handle the intelligent matching and insertion
      const res = await apiClient.post('/api/policy-inventory/import', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      if (res.data.success) {
        alert(`${t('policyInventory.import')} Başarılı! (${res.data.stats?.policies || 0} politika eklendi)`)
        onImportSuccess()
      }
    } catch (err) {
      console.error('Import failed', err)
      alert('Import failed!')
    } finally {
      setIsUploading(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  const handleExport = async (format: 'excel' | 'json') => {
    setExportOpen(false)
    try {
      const res = await apiClient.get(`/api/policy-inventory/export/${format}`, { responseType: 'blob' })
      const url = window.URL.createObjectURL(new Blob([res.data]))
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', `policy_inventory.${format === 'excel' ? 'xlsx' : 'json'}`)
      document.body.appendChild(link)
      link.click()
      link.remove()
    } catch (err) {
      console.error('Export failed', err)
      alert('Export failed!')
    }
  }

  const handleFilteredExport = async (format: 'excel' | 'json') => {
    setExportOpen(false)

    if (!searchResults.length) {
      alert('Filtre sonucu bulunamadi.')
      return
    }

    const safeQuery = searchQuery.trim().replace(/[^a-z0-9-_]+/gi, '_').slice(0, 40) || 'filtered'

    const rows = searchResults.map((result) => ({
      policy: result.policy_name,
      rule: result.rule_name || '',
      scope: result.scope,
      exception: result.exception_rule_name || '',
      match_area: result.match_area,
      match_field: result.match_field,
      matched_value: result.matched_value,
      destination_type: result.destination_type || '',
      resource_type: result.resource_type || '',
      include: result.include || '',
      enabled: result.enabled || '',
      search_query: searchQuery,
      search_filter: searchFilter,
    }))

    try {
      if (format === 'json') {
        const blob = new Blob([JSON.stringify(rows, null, 2)], { type: 'application/json' })
        const url = window.URL.createObjectURL(blob)
        const link = document.createElement('a')
        link.href = url
        link.setAttribute('download', `policy_inventory_filtered_${safeQuery}.json`)
        document.body.appendChild(link)
        link.click()
        link.remove()
        window.URL.revokeObjectURL(url)
        return
      }

      const { Workbook } = await import('exceljs')
      const workbook = new Workbook()
      const worksheet = workbook.addWorksheet('Filtre Sonucu')

      worksheet.columns = [
        { header: 'Policy', key: 'policy', width: 34 },
        { header: 'Rule', key: 'rule', width: 34 },
        { header: 'Scope', key: 'scope', width: 14 },
        { header: 'Exception', key: 'exception', width: 34 },
        { header: 'Match Area', key: 'match_area', width: 18 },
        { header: 'Match Field', key: 'match_field', width: 28 },
        { header: 'Matched Value', key: 'matched_value', width: 42 },
        { header: 'Destination Type', key: 'destination_type', width: 24 },
        { header: 'Resource Type', key: 'resource_type', width: 22 },
        { header: 'Include', key: 'include', width: 12 },
        { header: 'Enabled', key: 'enabled', width: 12 },
        { header: 'Search Query', key: 'search_query', width: 24 },
        { header: 'Search Filter', key: 'search_filter', width: 18 },
      ]
      rows.forEach((row) => worksheet.addRow(row))
      worksheet.getRow(1).font = { bold: true }
      worksheet.views = [{ state: 'frozen', ySplit: 1 }]

      const buffer = await workbook.xlsx.writeBuffer()
      const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', `policy_inventory_filtered_${safeQuery}.xlsx`)
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(url)
    } catch (err) {
      console.error('Filtered export failed', err)
      alert('Filtered export failed!')
    }
  }

  return (
    <div style={{ display: 'flex', gap: '12px' }}>
      <input
        type="file"
        ref={fileInputRef}
        style={{ display: 'none' }}
        accept=".xlsx,.xls,.json"
        onChange={handleFileChange}
      />
      
      <button
        onClick={handleImportClick}
        disabled={isUploading}
        className="glass-button"
        style={{
          background: 'var(--card-bg)',
          color: 'var(--text-primary)',
          border: '1px solid var(--border-color)',
          padding: '10px 16px',
          borderRadius: '8px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          cursor: isUploading ? 'not-allowed' : 'pointer',
          fontWeight: '500',
          fontSize: '14px',
          opacity: isUploading ? 0.7 : 1
        }}
      >
        <Upload size={16} />
        {isUploading ? '...' : t('policyInventory.import')}
      </button>

      <div style={{ position: 'relative' }}>
        <button
          onClick={() => setExportOpen(!exportOpen)}
          className="glass-button"
          style={{
            background: 'var(--card-bg)',
            color: 'var(--text-primary)',
            border: '1px solid var(--border-color)',
            padding: '10px 16px',
            borderRadius: '8px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            cursor: 'pointer',
            fontWeight: '500',
            fontSize: '14px'
          }}
        >
          <Download size={16} />
          {t('policyInventory.export')}
          <ChevronDown size={14} />
        </button>

        {exportOpen && (
          <div style={{
            position: 'absolute',
            top: 'calc(100% + 8px)',
            right: 0,
            background: 'var(--card-bg)',
            border: '1px solid var(--border-color)',
            borderRadius: '8px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.1)',
            padding: '8px',
            zIndex: 50,
            minWidth: '160px'
          }}>
            <div
              onClick={() => handleExport('excel')}
              style={{ padding: '8px 12px', display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', borderRadius: '6px', color: 'var(--text-primary)', fontSize: '14px' }}
              className="hover-bg-gray"
            >
              <FileSpreadsheet size={16} color="#10b981" />
              Tum Envanter Excel
            </div>
            <div
              onClick={() => handleExport('json')}
              style={{ padding: '8px 12px', display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', borderRadius: '6px', color: 'var(--text-primary)', fontSize: '14px' }}
              className="hover-bg-gray"
            >
              <FileJson size={16} color="#f59e0b" />
              Tum Envanter JSON
            </div>
            {searchQuery.trim().length > 0 && (
              <>
                <div style={{ height: '1px', background: 'var(--border-color)', margin: '6px 0' }} />
                <div
                  onClick={() => handleFilteredExport('excel')}
                  style={{ padding: '8px 12px', display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', borderRadius: '6px', color: 'var(--text-primary)', fontSize: '14px' }}
                  className="hover-bg-gray"
                >
                  <FileSpreadsheet size={16} color="#8b5cf6" />
                  Filtre Sonucu Excel
                </div>
                <div
                  onClick={() => handleFilteredExport('json')}
                  style={{ padding: '8px 12px', display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', borderRadius: '6px', color: 'var(--text-primary)', fontSize: '14px' }}
                  className="hover-bg-gray"
                >
                  <FileJson size={16} color="#8b5cf6" />
                  Filtre Sonucu JSON
                </div>
              </>
            )}
          </div>
        )}
      </div>
      <style dangerouslySetInnerHTML={{__html: `
        .hover-bg-gray:hover { background: var(--bg-color); }
      `}} />
    </div>
  )
}
