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

interface BulkImportStatus {
  job_id: string
  status: 'queued' | 'running' | 'completed' | 'completed_with_errors' | 'failed'
  total_files: number
  processed_files: number
  success_files: number
  failed_files: number
  policies: number
  rules: number
  exceptions: number
  current_file?: string
  message?: string
  errors?: string[]
}

export default function ImportExport({
  onImportSuccess,
  searchResults = [],
  searchQuery = '',
  searchFilter = 'all'
}: ImportExportProps) {
  const { t } = useTranslation()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [exportOpen, setExportOpen] = useState(false)
  const [isUploading, setIsUploading] = useState(false)
  const [bulkStatus, setBulkStatus] = useState<BulkImportStatus | null>(null)

  const handleImportClick = () => {
    fileInputRef.current?.click()
  }

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || [])
    if (!files.length) return

    setIsUploading(true)
    setBulkStatus(null)
    const formData = new FormData()
    files.forEach((file) => formData.append('files', file))

    try {
      const res = await apiClient.post('/api/policy-inventory/import/bulk', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 0
      })

      const startedStatus = res.data?.data as BulkImportStatus | undefined
      if (!res.data?.success || !startedStatus?.job_id) {
        throw new Error(res.data?.message || 'Bulk import could not be started.')
      }

      setBulkStatus(startedStatus)

      let latestStatus = startedStatus
      while (['queued', 'running'].includes(latestStatus.status)) {
        await new Promise(resolve => setTimeout(resolve, 1500))
        const statusRes = await apiClient.get(`/api/policy-inventory/import/bulk/${latestStatus.job_id}`, { timeout: 0 })
        latestStatus = statusRes.data?.data as BulkImportStatus
        setBulkStatus(latestStatus)
      }

      onImportSuccess()

      if (latestStatus.status === 'completed') {
        alert(`Toplu import tamamlandi. ${latestStatus.success_files}/${latestStatus.total_files} dosya basarili, ${latestStatus.policies} politika eklendi.`)
      } else if (latestStatus.status === 'completed_with_errors') {
        alert(`Toplu import bazi hatalarla tamamlandi. Basarili: ${latestStatus.success_files}, Hatali: ${latestStatus.failed_files}`)
      } else {
        alert(`Toplu import basarisiz: ${latestStatus.message || 'Bilinmeyen hata'}`)
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
      window.URL.revokeObjectURL(url)
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

  const progressPercent = bulkStatus?.total_files
    ? Math.round((bulkStatus.processed_files / bulkStatus.total_files) * 100)
    : 0

  return (
    <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
      <input
        type="file"
        ref={fileInputRef}
        style={{ display: 'none' }}
        accept=".xlsx,.xls,.json"
        multiple
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
        {isUploading ? 'Import...' : t('policyInventory.import')}
      </button>

      {bulkStatus && (
        <div style={{
          minWidth: '260px',
          maxWidth: '360px',
          padding: '8px 10px',
          borderRadius: '8px',
          border: '1px solid var(--border-color)',
          background: 'var(--card-bg)',
          color: 'var(--text-primary)',
          fontSize: '12px'
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: '8px', marginBottom: '6px' }}>
            <span style={{ fontWeight: 700 }}>
              {bulkStatus.processed_files}/{bulkStatus.total_files} dosya
            </span>
            <span style={{ color: bulkStatus.failed_files > 0 ? '#ef4444' : '#10b981', fontWeight: 700 }}>
              {bulkStatus.status}
            </span>
          </div>
          <div style={{ height: '6px', borderRadius: '999px', background: 'rgba(100,100,100,0.14)', overflow: 'hidden' }}>
            <div style={{ width: `${progressPercent}%`, height: '100%', background: 'linear-gradient(135deg, #10b981, #8b5cf6)' }} />
          </div>
          <div style={{ marginTop: '6px', color: 'var(--text-secondary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {bulkStatus.current_file || bulkStatus.message || 'Sirada bekliyor'}
          </div>
          <div style={{ marginTop: '4px', color: 'var(--text-secondary)' }}>
            Basarili {bulkStatus.success_files} - Hatali {bulkStatus.failed_files} - Policy {bulkStatus.policies}
          </div>
        </div>
      )}

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
            minWidth: '180px'
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
