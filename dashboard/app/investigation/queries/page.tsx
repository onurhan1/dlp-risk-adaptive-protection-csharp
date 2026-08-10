'use client'

import { useEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent } from 'react'
import { Download, FileSpreadsheet, Plus, Save, Trash2, Upload, X } from 'lucide-react'
import apiClient from '@/lib/axios'

type QueryRow = {
  id?: number
  user_code: string
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
  { key: 'user_code', label: 'Kullanıcı Kodu' },
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

const DEFAULT_COLUMN_WIDTHS: Record<string, number> = {
  user_code: 180,
  full_name: 230,
  mail_address: 260,
  subject: 320,
  query_date: 170,
  response_status: 330,
  action: 280,
  query_status: 190
}

const MIN_COLUMN_WIDTH = 120
const MAX_AUTO_COLUMN_WIDTH = 720
const MIN_ROW_HEIGHT = 38
const MAX_ROW_HEIGHT = 360

const emptyRow = (): QueryRow => ({
  user_code: '',
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

function estimateTextWidth(value: any) {
  const text = String(value || '')
  const longestLine = text.split(/\r?\n/).reduce((max, line) => Math.max(max, line.length), 0)
  return Math.min(MAX_AUTO_COLUMN_WIDTH, Math.max(MIN_COLUMN_WIDTH, longestLine * 8 + 58))
}

export default function InvestigationQueriesPage() {
  const [columns, setColumns] = useState(DEFAULT_COLUMNS)
  const [rows, setRows] = useState<QueryRow[]>([])
  const [columnFilters, setColumnFilters] = useState<Record<string, string>>({})
  const [columnWidths, setColumnWidths] = useState<Record<string, number>>(DEFAULT_COLUMN_WIDTHS)
  const [manualColumnWidths, setManualColumnWidths] = useState<Set<string>>(new Set())
  const [rowHeights, setRowHeights] = useState<Record<string, number>>({})
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement | null>(null)
  const resizeRef = useRef<{ type: 'column'; key: string; startX: number; startWidth: number } | { type: 'row'; key: string; startY: number; startHeight: number } | null>(null)

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

  useEffect(() => {
    const handleMove = (event: MouseEvent) => {
      const resize = resizeRef.current
      if (!resize) return

      if (resize.type === 'column') {
        const nextWidth = Math.max(MIN_COLUMN_WIDTH, resize.startWidth + event.clientX - resize.startX)
        setColumnWidths(prev => ({ ...prev, [resize.key]: nextWidth }))
        setManualColumnWidths(prev => new Set(prev).add(resize.key))
      } else {
        const nextHeight = Math.min(MAX_ROW_HEIGHT, Math.max(MIN_ROW_HEIGHT, resize.startHeight + event.clientY - resize.startY))
        setRowHeights(prev => ({ ...prev, [resize.key]: nextHeight }))
      }
    }

    const stopResize = () => {
      resizeRef.current = null
      document.body.style.cursor = ''
      document.body.style.userSelect = ''
    }

    window.addEventListener('mousemove', handleMove)
    window.addEventListener('mouseup', stopResize)
    return () => {
      window.removeEventListener('mousemove', handleMove)
      window.removeEventListener('mouseup', stopResize)
    }
  }, [])

  const rowKeyOf = (row: QueryRow, sourceIndex: number) => row.id ? `id:${row.id}` : `idx:${sourceIndex}`

  const visibleRows = useMemo(() => {
    const source = rows.length ? rows : [emptyRow()]
    return source
      .map((row, sourceIndex) => ({ row, sourceIndex }))
      .filter(({ row }) => columns.every(col => {
        const filter = (columnFilters[col.key] || '').trim().toLocaleLowerCase('tr-TR')
        if (!filter) return true
        const value = String(row[col.key] || '').toLocaleLowerCase('tr-TR')
        return value.includes(filter)
      }))
  }, [rows, columns, columnFilters])

  const totalTableWidth = useMemo(
    () => 58 + 58 + columns.reduce((total, col) => total + (columnWidths[col.key] || DEFAULT_COLUMN_WIDTHS[col.key] || 220), 0),
    [columns, columnWidths]
  )

  useEffect(() => {
    const source = rows.length ? rows : [emptyRow()]
    setColumnWidths(prev => {
      let changed = false
      const next = { ...prev }

      columns.forEach(col => {
        if (manualColumnWidths.has(col.key)) return
        const current = next[col.key] || DEFAULT_COLUMN_WIDTHS[col.key] || MIN_COLUMN_WIDTH
        const headerWidth = estimateTextWidth(col.label)
        const contentWidth = source.reduce(
          (max, row) => Math.max(max, estimateTextWidth(row[col.key])),
          headerWidth
        )
        const desired = Math.max(current, DEFAULT_COLUMN_WIDTHS[col.key] || MIN_COLUMN_WIDTH, contentWidth)
        if (desired > current) {
          next[col.key] = desired
          changed = true
        }
      })

      return changed ? next : prev
    })
  }, [rows, columns, manualColumnWidths])

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
    const key = `custom_${Date.now()}`
    setColumns(prev => [...prev, { key, label: `Yeni Kolon ${index}` }])
    setColumnWidths(prev => ({ ...prev, [key]: 220 }))
    setManualColumnWidths(prev => {
      const next = new Set(prev)
      next.delete(key)
      return next
    })
  }

  const removeColumn = (key: string) => {
    if (columns.length <= 1) return
    setColumns(prev => prev.filter(c => c.key !== key))
    setColumnFilters(prev => {
      const next = { ...prev }
      delete next[key]
      return next
    })
    setManualColumnWidths(prev => {
      const next = new Set(prev)
      next.delete(key)
      return next
    })
  }

  const startColumnResize = (event: ReactMouseEvent, key: string) => {
    event.preventDefault()
    resizeRef.current = {
      type: 'column',
      key,
      startX: event.clientX,
      startWidth: columnWidths[key] || DEFAULT_COLUMN_WIDTHS[key] || 220
    }
    document.body.style.cursor = 'col-resize'
    document.body.style.userSelect = 'none'
  }

  const startRowResize = (event: ReactMouseEvent, key: string, currentHeight: number) => {
    event.preventDefault()
    resizeRef.current = {
      type: 'row',
      key,
      startY: event.clientY,
      startHeight: currentHeight
    }
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'
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

      <div className="card" style={{ overflow: 'auto', padding: 0, maxHeight: 'calc(100vh - 260px)' }}>
        {loading ? (
          <div style={{ padding: 24, color: 'var(--text-muted)' }}>Yükleniyor...</div>
        ) : (
          <table style={{ width: totalTableWidth, minWidth: '100%', borderCollapse: 'collapse', tableLayout: 'fixed' }}>
            <colgroup>
              <col style={{ width: 58 }} />
              {columns.map(col => (
                <col key={col.key} style={{ width: columnWidths[col.key] || DEFAULT_COLUMN_WIDTHS[col.key] || 220 }} />
              ))}
              <col style={{ width: 58 }} />
            </colgroup>
            <thead>
              <tr>
                <th style={stickyThStyle}>#</th>
                {columns.map(col => (
                  <th key={col.key} style={thStyle}>
                    <div style={headerCellStyle}>
                      <input
                        value={col.label}
                        onChange={e => setColumns(prev => prev.map(c => c.key === col.key ? { ...c, label: e.target.value } : c))}
                        style={headerInputStyle}
                        title={col.label}
                      />
                      <button title="Kolonu sil" onClick={() => removeColumn(col.key)} style={iconButtonStyle}><Trash2 size={13} /></button>
                    </div>
                    <span
                      title="Sütunu genişlet"
                      onMouseDown={event => startColumnResize(event, col.key)}
                      style={columnResizeHandleStyle}
                    />
                  </th>
                ))}
                <th style={thStyle}></th>
              </tr>
              <tr>
                <th style={filterThStyle}></th>
                {columns.map(col => (
                  <th key={col.key} style={filterThStyle}>
                    <div style={filterCellStyle}>
                      {col.key === 'query_status' ? (
                        <select
                          value={columnFilters[col.key] || ''}
                          onChange={e => setColumnFilters(prev => ({ ...prev, [col.key]: e.target.value }))}
                          style={filterInputStyle}
                        >
                          <option value="">Tümü</option>
                          {STATUS_OPTIONS.map(option => (
                            <option key={option.value} value={option.value}>{option.label}</option>
                          ))}
                        </select>
                      ) : (
                        <input
                          value={columnFilters[col.key] || ''}
                          onChange={e => setColumnFilters(prev => ({ ...prev, [col.key]: e.target.value }))}
                          placeholder="Filtrele"
                          style={filterInputStyle}
                        />
                      )}
                      {columnFilters[col.key] && (
                        <button
                          title="Filtreyi temizle"
                          onClick={() => setColumnFilters(prev => ({ ...prev, [col.key]: '' }))}
                          style={clearFilterButtonStyle}
                        >
                          <X size={13} />
                        </button>
                      )}
                    </div>
                  </th>
                ))}
                <th style={filterThStyle}></th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map(({ row, sourceIndex }) => {
                const rowKey = rowKeyOf(row, sourceIndex)
                const rowHeight = rowHeights[rowKey] || MIN_ROW_HEIGHT
                return (
                <tr key={row.id ?? sourceIndex} style={{ height: rowHeight }}>
                  <td style={{ ...indexStyle, height: rowHeight }}>
                    {sourceIndex + 1}
                    <span
                      title="Satırı genişlet"
                      onMouseDown={event => startRowResize(event, rowKey, rowHeight)}
                      style={rowResizeHandleStyle}
                    />
                  </td>
                  {columns.map(col => (
                    <td
                      key={col.key}
                      style={{ ...tdStyle, height: rowHeight }}
                    >
                      <div style={{ position: 'relative', height: '100%' }}>
                        {col.key === 'query_status' ? (
                          <select
                            value={row[col.key] || 'bekliyor'}
                            onChange={e => updateCell(sourceIndex, col.key, e.target.value)}
                            style={{ ...cellInputStyle, height: Math.max(32, rowHeight - 10) }}
                            title={STATUS_OPTIONS.find(option => option.value === row[col.key])?.label || row[col.key] || ''}
                          >
                            {STATUS_OPTIONS.map(option => (
                              <option key={option.value} value={option.value}>{option.label}</option>
                            ))}
                          </select>
                        ) : col.key === 'query_date' ? (
                          <input
                            type="date"
                            value={row[col.key] || ''}
                            onChange={e => updateCell(sourceIndex, col.key, e.target.value)}
                            style={{ ...cellInputStyle, height: Math.max(32, rowHeight - 10) }}
                            title={String(row[col.key] || '')}
                          />
                        ) : (
                          <input
                            type="text"
                            value={row[col.key] || ''}
                            onChange={e => updateCell(sourceIndex, col.key, e.target.value)}
                            style={{ ...cellInputStyle, height: Math.max(32, rowHeight - 10) }}
                            title={String(row[col.key] || '')}
                          />
                        )}
                      </div>
                    </td>
                  ))}
                  <td style={{ ...tdStyle, height: rowHeight }}>
                    <button
                      title="Satırı sil"
                      onClick={() => setRows(prev => prev.filter((_, i) => i !== sourceIndex))}
                      style={iconButtonStyle}
                    >
                      <Trash2 size={15} />
                    </button>
                  </td>
                </tr>
              )})}
              {visibleRows.length === 0 && (
                <tr>
                  <td colSpan={columns.length + 2} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>
                    Filtreyle eşleşen kayıt yok.
                  </td>
                </tr>
              )}
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
  position: 'sticky' as const,
  top: 0,
  zIndex: 3,
  padding: '7px 8px',
  borderBottom: '1px solid var(--border)',
  borderRight: '1px solid var(--border)',
  background: 'var(--surface-hover)',
  color: 'var(--text-primary)',
  fontSize: 12,
  textAlign: 'left' as const,
  whiteSpace: 'nowrap' as const,
  verticalAlign: 'top' as const
}

const stickyThStyle = {
  ...thStyle,
  left: 0,
  zIndex: 4
}

const filterThStyle = {
  ...thStyle,
  top: 46,
  padding: '5px 8px',
  background: 'var(--surface)',
  zIndex: 2
}

const headerCellStyle = {
  position: 'relative' as const,
  display: 'flex',
  alignItems: 'center',
  gap: 6,
  minWidth: 0
}

const filterCellStyle = {
  display: 'flex',
  alignItems: 'center',
  gap: 6,
  minWidth: 0
}

const tdStyle = {
  position: 'relative' as const,
  padding: '4px 6px',
  borderBottom: '1px solid var(--border)',
  borderRight: '1px solid var(--border)',
  verticalAlign: 'middle' as const
}

const indexStyle = {
  ...tdStyle,
  position: 'sticky' as const,
  left: 0,
  zIndex: 1,
  width: 42,
  color: 'var(--text-muted)',
  textAlign: 'center' as const,
  background: 'var(--surface-hover)'
}

const cellInputStyle = {
  width: '100%',
  minWidth: 0,
  border: '1px solid transparent',
  background: 'transparent',
  color: 'var(--text-primary)',
  padding: '6px 8px',
  borderRadius: 4,
  outline: 'none',
  boxSizing: 'border-box' as const,
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap' as const
}

const headerInputStyle = {
  ...cellInputStyle,
  flex: 1,
  minWidth: 0,
  fontWeight: 600
}

const filterInputStyle = {
  ...cellInputStyle,
  height: 30,
  border: '1px solid var(--border)',
  background: 'var(--background)',
  fontSize: 12
}

const iconButtonStyle = {
  flex: '0 0 auto',
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
  marginLeft: 0
}

const clearFilterButtonStyle = {
  ...iconButtonStyle,
  width: 24,
  height: 24,
  marginLeft: 0
}

const columnResizeHandleStyle = {
  position: 'absolute' as const,
  top: 0,
  right: -3,
  width: 7,
  height: '100%',
  cursor: 'col-resize',
  zIndex: 5
}

const rowResizeHandleStyle = {
  position: 'absolute' as const,
  left: 0,
  right: 0,
  bottom: -3,
  height: 7,
  cursor: 'row-resize',
  zIndex: 2
}
