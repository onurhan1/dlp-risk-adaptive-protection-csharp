'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { Download, FileSpreadsheet, Plus, Save, Trash2, Upload } from 'lucide-react'
import apiClient from '@/lib/axios'

type QueryRow = {
  id?: number
  full_name: string
  mail_address: string
  subject: string
  query_date: string
  response_status: string
  action: string
  query_status: string
  source?: string
  team?: string
  notes?: string
  extra_json?: string
  playbook_mail_log_id?: number
  [key: string]: any
}

const DEFAULT_COLUMNS = [
  { key: 'full_name', label: 'Kullanıcı Adı Soyadı' },
  { key: 'mail_address', label: 'Mail Adresi' },
  { key: 'subject', label: 'Mail Konu Başlığı' },
  { key: 'query_date', label: 'Sorgu Tarihi' },
  { key: 'response_status', label: 'Kullanıcıdan Geri Dönüş Yapılma Durumu' },
  { key: 'action', label: 'Aksiyon' },
  { key: 'query_status', label: 'Sorgu Durumu' }
]

const STATUS_OPTIONS = [
  { value: 'bekliyor', label: 'Bekliyor' },
  { value: 'sorgulandi', label: 'Sorgulandı' },
  { value: 'tamamlandi', label: 'Tamamlandı' }
]

const emptyRow = (): QueryRow => ({
  full_name: '',
  mail_address: '',
  subject: 'DLP Blocklanmış İşlemler Hk.',
  query_date: new Date().toISOString().slice(0, 10),
  response_status: '',
  action: '',
  query_status: 'bekliyor',
  extra_json: '{}'
})

const turkishTokenMap: Record<string, string> = {
  saglam: 'sağlam',
  caglar: 'çağlar',
  cagatay: 'çağatay',
  gokhan: 'gökhan',
  gokce: 'gökçe',
  gungor: 'güngör',
  gunes: 'güneş',
  ozgur: 'özgür',
  ozge: 'özge',
  ozlem: 'özlem',
  cigdem: 'çiğdem',
  yagmur: 'yağmur',
  yilmaz: 'yılmaz',
  yildiz: 'yıldız',
  isik: 'ışık'
}

function inferNameFromMail(value: string) {
  const local = value.split('@')[0] || value
  return local
    .split(/[._\-\s]+/)
    .filter(Boolean)
    .map(t => turkishTokenMap[t.toLowerCase()] || t.toLowerCase())
    .map(t => t.charAt(0).toLocaleUpperCase('tr-TR') + t.slice(1).toLocaleLowerCase('tr-TR'))
    .join(' ')
}

function toInputDate(value: any) {
  if (!value) return ''
  if (value instanceof Date) return value.toISOString().slice(0, 10)
  const text = String(value)
  const parts = text.match(/^(\d{1,2})\.(\d{1,2})\.(\d{4})$/)
  if (parts) return `${parts[3]}-${parts[2].padStart(2, '0')}-${parts[1].padStart(2, '0')}`
  return text.slice(0, 10)
}

export default function InvestigationQueriesPage() {
  const [columns, setColumns] = useState(DEFAULT_COLUMNS)
  const [rows, setRows] = useState<QueryRow[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement | null>(null)
  const fillRef = useRef<{ row: number; key: string; value: string } | null>(null)

  const loadRows = async () => {
    setLoading(true)
    try {
      const res = await apiClient.get('/api/investigation/queries')
      setRows((Array.isArray(res.data) ? res.data : []).map((r: any) => ({
        ...r,
        query_date: toInputDate(r.query_date)
      })))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadRows()
  }, [])

  const visibleRows = useMemo(() => rows.length ? rows : [emptyRow()], [rows])

  const updateCell = (rowIndex: number, key: string, value: string) => {
    setRows(prev => {
      const next = prev.length ? [...prev] : [emptyRow()]
      const row = { ...next[rowIndex], [key]: value }
      if (key === 'mail_address' && !row.full_name) row.full_name = inferNameFromMail(value)
      next[rowIndex] = row
      return next
    })
  }

  const saveRows = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const payload = rows.filter(r => columns.some(c => String(r[c.key] || '').trim()))
      await apiClient.post('/api/investigation/queries/bulk', { rows: payload })
      setMessage('Sorgulamalar kaydedildi')
      await loadRows()
    } catch (e: any) {
      setMessage(e?.response?.data?.detail || 'Kaydetme başarısız')
    } finally {
      setSaving(false)
    }
  }

  const addColumn = () => {
    const index = columns.length + 1
    setColumns(prev => [...prev, { key: `custom_${Date.now()}`, label: `Yeni Kolon ${index}` }])
  }

  const removeColumn = (key: string) => {
    if (columns.length <= 1) return
    setColumns(prev => prev.filter(c => c.key !== key))
  }

  const importExcel = async (file: File) => {
    const { Workbook } = await import('exceljs')
    const workbook = new Workbook()
    await workbook.xlsx.load(await file.arrayBuffer())
    const ws = workbook.worksheets[0]
    if (!ws) return

    const header = ws.getRow(1).values as any[]
    const importedColumns = header.slice(1).map((h, i) => ({
      key: DEFAULT_COLUMNS[i]?.key || `custom_${i}`,
      label: String(h?.text || h || `Kolon ${i + 1}`)
    }))

    const importedRows: QueryRow[] = []
    for (let i = 2; i <= ws.rowCount; i++) {
      const row = ws.getRow(i)
      const values = row.values as any[]
      const next = emptyRow()
      importedColumns.forEach((col, idx) => {
        const raw = values[idx + 1]
        next[col.key] = col.key === 'query_date' ? toInputDate(raw) : String(raw?.text || raw || '')
      })
      if (!next.full_name && next.mail_address) next.full_name = inferNameFromMail(next.mail_address)
      importedRows.push(next)
    }

    setColumns(importedColumns.length ? importedColumns : DEFAULT_COLUMNS)
    setRows(importedRows.length ? importedRows : [emptyRow()])
    setMessage('Excel içeriği yüklendi')
  }

  const exportExcel = async () => {
    const { Workbook } = await import('exceljs')
    const workbook = new Workbook()
    const ws = workbook.addWorksheet('Sorgulamalar')
    ws.addRow(columns.map(c => c.label))
    rows.forEach(row => ws.addRow(columns.map(c => row[c.key] || '')))
    ws.getRow(1).font = { bold: true }
    ws.columns.forEach(col => { col.width = 28 })
    const buffer = await workbook.xlsx.writeBuffer()
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = `sorgulamalar_${new Date().toISOString().slice(0, 10)}.xlsx`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(link.href)
  }

  return (
    <div className="dashboard-page">
      <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', gap: 16, alignItems: 'flex-start' }}>
        <div>
          <h1>Sorgulamalar</h1>
          <p className="text-muted">Haftalık sorgu Excel formatı, manuel kayıtlar ve agentic workflow çıktıları</p>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          <input
            ref={fileRef}
            type="file"
            accept=".xlsx,.xls"
            style={{ display: 'none' }}
            onChange={e => e.target.files?.[0] && importExcel(e.target.files[0])}
          />
          <button className="btn-secondary" onClick={() => fileRef.current?.click()}><Upload size={16} /> Yükle</button>
          <button className="btn-secondary" onClick={exportExcel}><Download size={16} /> İndir</button>
          <button className="btn-secondary" onClick={addColumn}><Plus size={16} /> Kolon</button>
          <button className="btn-secondary" onClick={() => setRows(prev => [...prev, emptyRow()])}><Plus size={16} /> Satır</button>
          <button className="btn-primary" disabled={saving} onClick={saveRows}><Save size={16} /> {saving ? 'Kaydediliyor' : 'Kaydet'}</button>
        </div>
      </div>

      {message && <div style={{ marginBottom: 12, color: 'var(--text-secondary)' }}>{message}</div>}

      <div className="card" style={{ overflow: 'auto', padding: 0 }}>
        {loading ? (
          <div style={{ padding: 24, color: 'var(--text-muted)' }}>Yükleniyor...</div>
        ) : (
          <table style={{ width: '100%', minWidth: 1100, borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={thStyle}>#</th>
                {columns.map(col => (
                  <th key={col.key} style={thStyle}>
                    <input
                      value={col.label}
                      onChange={e => setColumns(prev => prev.map(c => c.key === col.key ? { ...c, label: e.target.value } : c))}
                      style={headerInputStyle}
                    />
                    <button title="Kolonu sil" onClick={() => removeColumn(col.key)} style={iconButtonStyle}><Trash2 size={13} /></button>
                  </th>
                ))}
                <th style={thStyle}></th>
              </tr>
            </thead>
            <tbody onMouseLeave={() => { fillRef.current = null }} onMouseUp={() => { fillRef.current = null }}>
              {visibleRows.map((row, rowIndex) => (
                <tr key={row.id ?? rowIndex}>
                  <td style={indexStyle}>{rowIndex + 1}</td>
                  {columns.map(col => (
                    <td
                      key={col.key}
                      style={tdStyle}
                      onMouseEnter={() => {
                        if (fillRef.current?.key === col.key && rowIndex > fillRef.current.row) {
                          updateCell(rowIndex, col.key, fillRef.current.value)
                        }
                      }}
                    >
                      <div style={{ position: 'relative' }}>
                        {col.key === 'query_status' ? (
                          <select
                            value={row[col.key] || 'bekliyor'}
                            onChange={e => updateCell(rowIndex, col.key, e.target.value)}
                            style={cellInputStyle}
                          >
                            {STATUS_OPTIONS.map(option => (
                              <option key={option.value} value={option.value}>{option.label}</option>
                            ))}
                          </select>
                        ) : (
                          <input
                            type={col.key === 'query_date' ? 'date' : 'text'}
                            value={row[col.key] || ''}
                            onChange={e => updateCell(rowIndex, col.key, e.target.value)}
                            style={cellInputStyle}
                          />
                        )}
                        <span
                          title="Aşağı sürükleyerek doldur"
                          onMouseDown={() => { fillRef.current = { row: rowIndex, key: col.key, value: row[col.key] || '' } }}
                          style={fillHandleStyle}
                        />
                      </div>
                    </td>
                  ))}
                  <td style={tdStyle}>
                    <button
                      title="Satırı sil"
                      onClick={() => setRows(prev => prev.filter((_, i) => i !== rowIndex))}
                      style={iconButtonStyle}
                    >
                      <Trash2 size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div style={{ marginTop: 12, display: 'flex', alignItems: 'center', gap: 8, color: 'var(--text-muted)', fontSize: 13 }}>
        <FileSpreadsheet size={16} /> Agentic workflow mail kayıtları bu tabloya otomatik düşer.
      </div>
    </div>
  )
}

const thStyle = {
  padding: 8,
  borderBottom: '1px solid var(--border)',
  background: 'var(--surface-hover)',
  color: 'var(--text-primary)',
  fontSize: 12,
  textAlign: 'left' as const,
  whiteSpace: 'nowrap' as const
}

const tdStyle = {
  padding: 6,
  borderBottom: '1px solid var(--border)',
  borderRight: '1px solid var(--border)'
}

const indexStyle = {
  ...tdStyle,
  width: 42,
  color: 'var(--text-muted)',
  textAlign: 'center' as const,
  background: 'var(--surface-hover)'
}

const cellInputStyle = {
  width: '100%',
  minWidth: 150,
  border: '1px solid transparent',
  background: 'transparent',
  color: 'var(--text-primary)',
  padding: '7px 9px',
  borderRadius: 4,
  outline: 'none'
}

const headerInputStyle = {
  ...cellInputStyle,
  minWidth: 170,
  fontWeight: 600
}

const iconButtonStyle = {
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  width: 26,
  height: 26,
  border: '1px solid var(--border)',
  background: 'var(--surface)',
  color: 'var(--text-muted)',
  borderRadius: 4,
  cursor: 'pointer',
  marginLeft: 4
}

const fillHandleStyle = {
  position: 'absolute' as const,
  right: 1,
  bottom: 1,
  width: 8,
  height: 8,
  borderRadius: 2,
  background: 'var(--primary)',
  cursor: 'crosshair'
}
