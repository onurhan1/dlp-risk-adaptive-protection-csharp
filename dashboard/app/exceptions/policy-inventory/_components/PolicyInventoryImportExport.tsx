import React, { useRef, useState } from 'react'
import { Upload, Download, FileJson, FileSpreadsheet, ChevronDown } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'
import apiClient from '@/lib/axios'

interface ImportExportProps {
  onImportSuccess: () => void
}

export default function ImportExport({ onImportSuccess }: ImportExportProps) {
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
        alert(`${t('policyInventory.import')} Başarılı! (${res.data.parsedPolicies} politika eklendi)`)
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
              Excel (.xlsx)
            </div>
            <div
              onClick={() => handleExport('json')}
              style={{ padding: '8px 12px', display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', borderRadius: '6px', color: 'var(--text-primary)', fontSize: '14px' }}
              className="hover-bg-gray"
            >
              <FileJson size={16} color="#f59e0b" />
              JSON (.json)
            </div>
          </div>
        )}
      </div>
      <style dangerouslySetInnerHTML={{__html: `
        .hover-bg-gray:hover { background: var(--bg-color); }
      `}} />
    </div>
  )
}
