'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import type { MouseEvent, ReactNode } from 'react'
import {
  CheckCircle2,
  Download,
  Edit3,
  FileSpreadsheet,
  Filter,
  Inbox,
  Plus,
  RefreshCw,
  Save,
  Search,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import apiClient from '@/lib/axios'

type QueryRow = {
  id?: number
  client_key: string
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
  correlation_code?: string
  first_sent_at?: string
  reply_received_at?: string
  reply_message_id?: string
  reply_preview?: string
  review_note?: string
  reminder_sent_at?: string
  reminder_count?: number
  [key: string]: any
}

type WorkflowMailRow = {
  id: number
  run_id: number
  playbook_id: number
  playbook_name: string
  to_email: string
  cc_email?: string
  subject: string
  mail_date: string
  status: string
  source: string
  trigger_count: number
  error_message?: string
  [key: string]: any
}

type Filters = {
  search: string
  user: string
  mail: string
  subject: string
  response: string
  action: string
  status: string
  source: string
  team: string
  dateFrom: string
  dateTo: string
}

const DEFAULT_COLUMNS = [
  { key: 'user_code', label: 'Kullanici Kodu' },
  { key: 'full_name', label: 'Ad Soyad' },
  { key: 'mail_address', label: 'Mail Adresi' },
  { key: 'subject', label: 'Konu' },
  { key: 'query_date', label: 'Sorgu Tarihi' },
  { key: 'response_status', label: 'Geri Donus Durumu' },
  { key: 'action', label: 'Aksiyon' },
  { key: 'query_status', label: 'Sorgu Durumu' },
  { key: 'team', label: 'Ekip' },
  { key: 'source', label: 'Kaynak' },
  { key: 'notes', label: 'Notlar' },
]

const STATUS_OPTIONS = [
  { value: 'bekliyor', label: 'Bekliyor', color: '#64748b' },
  { value: 'sorgulandi', label: 'Sorgulandi', color: '#3b82f6' },
  { value: 'cevap_inceleme_bekliyor', label: 'Cevap Inceleme Bekliyor', color: '#d97706' },
  { value: 'hatirlatma_yanitsiz', label: 'Hatirlatma Yanitsiz', color: '#dc2626' },
  { value: 'tamamlandi', label: 'Tamamlandi', color: '#10b981' },
]

const WORKFLOW_STATUSES = [
  { value: 'sent', label: 'Gonderildi' },
  { value: 'pending', label: 'Manuel bekliyor' },
  { value: 'failed', label: 'Basarisiz' },
  { value: 'skipped', label: 'Atlandi' },
]

const emptyFilters = (): Filters => ({
  search: '',
  user: '',
  mail: '',
  subject: '',
  response: '',
  action: '',
  status: '',
  source: '',
  team: '',
  dateFrom: '',
  dateTo: '',
})

function createClientKey(prefix = 'query') {
  const randomId = globalThis.crypto?.randomUUID?.()
  return `${prefix}-${randomId || `${Date.now()}-${Math.random().toString(36).slice(2)}`}`
}

const emptyRow = (): QueryRow => ({
  client_key: createClientKey('draft'),
  user_code: '',
  full_name: '',
  mail_address: '',
  subject: 'DLP Blocklanmis Islemler Hk.',
  query_date: new Date().toISOString().slice(0, 10),
  response_status: '',
  action: '',
  query_status: 'bekliyor',
  source: 'manual',
  team: '',
  notes: '',
  extra_json: '{}',
})

const turkishTokenMap: Record<string, string> = {
  saglam: 'saglam',
  caglar: 'caglar',
  cagatay: 'cagatay',
  gokhan: 'gokhan',
  gokce: 'gokce',
  gungor: 'gungor',
  gunes: 'gunes',
  ozgur: 'ozgur',
  ozge: 'ozge',
  ozlem: 'ozlem',
  cigdem: 'cigdem',
  yagmur: 'yagmur',
  yilmaz: 'yilmaz',
  yildiz: 'yildiz',
  isik: 'isik',
}

function inferNameFromMail(value: string) {
  const local = value.split('@')[0] || value
  return local
    .split(/[._\-\s]+/)
    .filter(Boolean)
    .map((token) => turkishTokenMap[token.toLowerCase()] || token.toLowerCase())
    .map((token) => token.charAt(0).toLocaleUpperCase('tr-TR') + token.slice(1).toLocaleLowerCase('tr-TR'))
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

function toDisplayDate(value: any) {
  const text = toInputDate(value)
  if (!text) return '-'
  const date = new Date(text)
  if (Number.isNaN(date.getTime())) return text
  return date.toLocaleDateString('tr-TR')
}

function toDisplayDateTime(value: any) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return date.toLocaleString('tr-TR', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
}

function queryDuplicateKey(row: Pick<QueryRow, 'user_code' | 'full_name' | 'query_date'>) {
  const userCode = String(row.user_code || '').trim().toLocaleLowerCase('tr-TR')
  const fullName = String(row.full_name || '').trim().toLocaleLowerCase('tr-TR')
  const queryDate = toInputDate(row.query_date)
  return userCode && fullName && queryDate ? `${userCode}|${fullName}|${queryDate}` : null
}

function validApiDate(value: any): string | null {
  const normalized = toInputDate(value)
  if (!/^\d{4}-\d{2}-\d{2}$/.test(normalized)) return null
  return Number.isNaN(new Date(`${normalized}T00:00:00`).getTime()) ? null : normalized
}

function queryRequest(row: QueryRow) {
  return {
    id: typeof row.id === 'number' && row.id > 0 ? row.id : null,
    user_code: String(row.user_code || '').trim(),
    full_name: String(row.full_name || '').trim(),
    mail_address: String(row.mail_address || '').trim(),
    subject: String(row.subject || '').trim(),
    query_date: validApiDate(row.query_date),
    response_status: String(row.response_status || '').trim(),
    action: String(row.action || '').trim(),
    query_status: String(row.query_status || 'bekliyor').trim(),
    source: String(row.source || '').trim() || null,
    team: String(row.team || '').trim() || null,
    notes: String(row.notes || '').trim() || null,
    playbook_mail_log_id: typeof row.playbook_mail_log_id === 'number' ? row.playbook_mail_log_id : null,
    extra_json: String(row.extra_json || '{}'),
  }
}

function saveErrorMessage(error: any) {
  const data = error?.response?.data
  if (data?.detail) return data.detail
  if (data?.title && data?.errors) {
    const details = Object.values(data.errors).flat().filter(Boolean).join(' ')
    return details ? `${data.title}: ${details}` : data.title
  }
  return data?.title || error?.message || 'Kaydetme basarisiz'
}

function statusMeta(value: string) {
  return STATUS_OPTIONS.find((option) => option.value === value) || STATUS_OPTIONS[0]
}

function workflowMailStatusLabel(value: any) {
  const status = String(value || '').toLowerCase()
  return WORKFLOW_STATUSES.find((option) => option.value === status)?.label || String(value || '-')
}

function includesText(value: any, filter: string) {
  const needle = filter.trim().toLocaleLowerCase('tr-TR')
  if (!needle) return true
  return String(value || '').toLocaleLowerCase('tr-TR').includes(needle)
}

export default function InvestigationQueriesPage() {
  const [rows, setRows] = useState<QueryRow[]>([])
  const [workflowMailRows, setWorkflowMailRows] = useState<WorkflowMailRow[]>([])
  const [filters, setFilters] = useState<Filters>(emptyFilters())
  const [workflowFilters, setWorkflowFilters] = useState({ search: '', status: '', workflow: '', dateFrom: '', dateTo: '' })
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null)
  const [selectedRowKeys, setSelectedRowKeys] = useState<Set<string>>(new Set())
  const [bulkDeleteMode, setBulkDeleteMode] = useState(false)
  const [editor, setEditor] = useState<QueryRow | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [replyNotificationCount, setReplyNotificationCount] = useState(0)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const fileRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    void loadRows()
  }, [])

  useEffect(() => {
    const refreshReplyNotification = async () => {
      try {
        const { data } = await apiClient.get('/api/investigation/queries/reply-review-count')
        setReplyNotificationCount(Number(data?.count) || 0)
      } catch {
        // The page remains usable when the notification endpoint is temporarily unavailable.
      }
    }
    void refreshReplyNotification()
    const interval = window.setInterval(() => void refreshReplyNotification(), 60_000)
    return () => window.clearInterval(interval)
  }, [])

  const loadRows = async () => {
    setLoading(true)
    try {
      const [queryRes, workflowMailRes] = await Promise.all([
        apiClient.get('/api/investigation/queries'),
        apiClient.get('/api/investigation/queries/workflow-mails'),
      ])

      const nextRows = (Array.isArray(queryRes.data) ? queryRes.data : []).map((row: any, index: number) => ({
        ...row,
        client_key: createClientKey(`stored-${row.id ?? index}`),
        query_date: toInputDate(row.query_date),
      }))
      setRows(nextRows)
      setSelectedRowKeys(new Set())
      setBulkDeleteMode(false)
      setReplyNotificationCount(nextRows.filter((row: QueryRow) => row.query_status === 'cevap_inceleme_bekliyor').length)
      setWorkflowMailRows((Array.isArray(workflowMailRes.data) ? workflowMailRes.data : []).map((row: any) => ({ ...row, mail_date: row.mail_date || '' })))
      setSelectedIndex(nextRows.length ? 0 : null)
      setEditor(nextRows.length ? { ...nextRows[0] } : null)
    } catch (error: any) {
      flash('error', error?.response?.data?.detail || 'Sorgulamalar yuklenemedi')
    } finally {
      setLoading(false)
    }
  }

  const suggestions = useMemo(() => ({
    user: unique(rows.flatMap((row) => [row.full_name, row.user_code])),
    mail: unique(rows.map((row) => row.mail_address)),
    subject: unique(rows.map((row) => row.subject)),
    response: unique(rows.map((row) => row.response_status)),
    action: unique(rows.map((row) => row.action)),
    source: unique(rows.map((row) => row.source)),
    team: unique(rows.map((row) => row.team)),
    workflow: unique(workflowMailRows.map((row) => row.playbook_name)),
  }), [rows, workflowMailRows])

  const filteredRows = useMemo(() => {
    return rows.filter((row) => {
      const allText = DEFAULT_COLUMNS.map((column) => row[column.key]).join(' ')
      const date = toInputDate(row.query_date)
      return includesText(allText, filters.search)
        && (includesText(row.full_name, filters.user) || includesText(row.user_code, filters.user))
        && includesText(row.mail_address, filters.mail)
        && includesText(row.subject, filters.subject)
        && includesText(row.response_status, filters.response)
        && includesText(row.action, filters.action)
        && (!filters.status || row.query_status === filters.status)
        && includesText(row.source, filters.source)
        && includesText(row.team, filters.team)
        && (!filters.dateFrom || date >= filters.dateFrom)
        && (!filters.dateTo || date <= filters.dateTo)
    })
  }, [rows, filters])

  const filteredWorkflowRows = useMemo(() => {
    return workflowMailRows.filter((row) => {
      const allText = [row.playbook_name, row.to_email, row.cc_email, row.subject, row.source, row.error_message].join(' ')
      const date = toInputDate(row.mail_date)
      return includesText(allText, workflowFilters.search)
        && (!workflowFilters.status || String(row.status).toLowerCase() === workflowFilters.status)
        && includesText(row.playbook_name, workflowFilters.workflow)
        && (!workflowFilters.dateFrom || date >= workflowFilters.dateFrom)
        && (!workflowFilters.dateTo || date <= workflowFilters.dateTo)
    })
  }, [workflowMailRows, workflowFilters])

  const stats = useMemo(() => {
    return STATUS_OPTIONS.map((option) => ({
      ...option,
      count: rows.filter((row) => row.query_status === option.value).length,
    }))
  }, [rows])

  const repliesAwaitingReview = useMemo(
    () => rows.filter((row) => row.query_status === 'cevap_inceleme_bekliyor').length,
    [rows]
  )

  const selectRow = (row: QueryRow, index: number) => {
    setSelectedIndex(index)
    setEditor({ ...row })
  }

  const addRow = () => {
    const next = emptyRow()
    setRows((prev) => [next, ...prev])
    setSelectedIndex(0)
    setEditor(next)
  }

  const applyEditor = () => {
    if (!editor || selectedIndex == null) return
    setRows((prev) => prev.map((row, index) => index === selectedIndex ? editor : row))
    flash('success', 'Degisiklikler listeye islendi. Kalici kayit icin Kaydet kullanin.')
  }

  const removeRow = async (row: QueryRow, index: number) => {
    if (row.id) {
      try {
        await apiClient.delete(`/api/investigation/queries/${row.id}`)
        flash('success', 'Kayit silindi')
      } catch (error: any) {
        flash('error', error?.response?.data?.detail || 'Kayit silinemedi')
        return
      }
    }

    setRows((prev) => prev.filter((_, rowIndex) => rowIndex !== index))
    setSelectedRowKeys((previous) => {
      const next = new Set(previous)
      next.delete(row.client_key)
      return next
    })
    setSelectedIndex(null)
    setEditor(null)
  }

  const toggleRowSelection = (clientKey: string) => {
    setSelectedRowKeys((previous) => {
      const next = new Set(previous)
      if (next.has(clientKey)) next.delete(clientKey)
      else next.add(clientKey)
      return next
    })
  }

  const toggleSelectAllFiltered = () => {
    const visibleKeys = filteredRows.map((row) => row.client_key)
    const allVisibleSelected = visibleKeys.length > 0 && visibleKeys.every((key) => selectedRowKeys.has(key))
    setSelectedRowKeys((previous) => {
      const next = new Set(previous)
      visibleKeys.forEach((key) => allVisibleSelected ? next.delete(key) : next.add(key))
      return next
    })
  }

  const deleteSelectedRows = async () => {
    const selectedRows = rows.filter((row) => selectedRowKeys.has(row.client_key))
    if (selectedRows.length === 0) return
    if (!window.confirm(`${selectedRows.length} sorgu kaydi silinecek. Devam etmek istiyor musunuz?`)) return

    setSaving(true)
    try {
      const ids = selectedRows.flatMap((row) => typeof row.id === 'number' && row.id > 0 ? [row.id] : [])
      if (ids.length > 0)
        await apiClient.post('/api/investigation/queries/bulk-delete', { ids })

      setRows((previous) => previous.filter((row) => !selectedRowKeys.has(row.client_key)))
      setSelectedRowKeys(new Set())
      setBulkDeleteMode(false)
      setSelectedIndex(null)
      setEditor(null)
      flash('success', `${selectedRows.length} sorgu kaydi silindi.`)
    } catch (error: any) {
      flash('error', error?.response?.data?.detail || 'Secilen kayitlar silinemedi')
    } finally {
      setSaving(false)
    }
  }

  const reviewReply = async (isSufficient: boolean) => {
    if (!editor?.id) return
    setSaving(true)
    try {
      await apiClient.post(`/api/investigation/queries/${editor.id}/review-reply`, {
        is_sufficient: isSufficient,
        note: editor.review_note || '',
      })
      flash('success', isSufficient ? 'Cevap yeterli bulundu, sorgu tamamlandi.' : 'Cevap yetersiz olarak kaydedildi.')
      await loadRows()
    } catch (error: any) {
      flash('error', error?.response?.data?.detail || 'Cevap incelemesi kaydedilemedi')
    } finally {
      setSaving(false)
    }
  }

  const updateEditor = (key: keyof QueryRow, value: string) => {
    setEditor((prev) => {
      const next = { ...(prev || emptyRow()), [key]: value }
      if (key === 'mail_address' && !next.full_name) next.full_name = inferNameFromMail(value)
      return next
    })
  }

  const saveRows = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const nextRows = editor && selectedIndex != null
        ? rows.map((row, index) => index === selectedIndex ? editor : row)
        : rows
      const payload = nextRows
        .filter((row) => DEFAULT_COLUMNS.some((column) => String(row[column.key] || '').trim()))
        .map(queryRequest)
      const result = await apiClient.post('/api/investigation/queries/bulk', { rows: payload })
      const skipped = Number(result.data?.skippedDuplicates || 0)
      flash('success', skipped > 0
        ? `Sorgulamalar kaydedildi. ${skipped} yinelenen kayit atlandi.`
        : 'Sorgulamalar kaydedildi')
      await loadRows()
    } catch (error: any) {
      flash('error', saveErrorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  const importExcel = async (file: File) => {
    const { Workbook } = await import('exceljs')
    const workbook = new Workbook()
    await workbook.xlsx.load(await file.arrayBuffer())
    const ws = workbook.worksheets[0]
    if (!ws) return

    const header = ws.getRow(1).values as any[]
    const importedColumns = header.slice(1).map((headerValue, index) => ({
      key: DEFAULT_COLUMNS[index]?.key || `custom_${index}`,
      label: String(headerValue?.text || headerValue || `Kolon ${index + 1}`),
    }))

    const importedRows: QueryRow[] = []
    for (let i = 2; i <= ws.rowCount; i++) {
      const sheetRow = ws.getRow(i)
      const values = sheetRow.values as any[]
      const next = emptyRow()
      importedColumns.forEach((column, index) => {
        const raw = values[index + 1]
        next[column.key] = column.key === 'query_date' ? toInputDate(raw) : String(raw?.text || raw || '')
      })
      if (!next.full_name && next.mail_address) next.full_name = inferNameFromMail(next.mail_address)
      importedRows.push(next)
    }

    const knownKeys = new Set(rows.map(queryDuplicateKey).filter((key): key is string => Boolean(key)))
    const newRows: QueryRow[] = []
    let skippedDuplicates = 0

    importedRows.forEach((row) => {
      const key = queryDuplicateKey(row)
      if (key && knownKeys.has(key)) {
        skippedDuplicates++
        return
      }
      if (key) knownKeys.add(key)
      newRows.push(row)
    })

    if (newRows.length > 0) {
      setRows((previous) => [...newRows, ...previous])
      setSelectedIndex(0)
      setEditor({ ...newRows[0] })
    }

    flash('success', newRows.length > 0
      ? `${newRows.length} yeni kayit eklendi${skippedDuplicates > 0 ? `, ${skippedDuplicates} yinelenen kayit atlandi` : ''}.`
      : 'Excelde eklenecek yeni kayit bulunamadi.')
  }

  const exportExcel = async () => {
    const { Workbook } = await import('exceljs')
    const workbook = new Workbook()
    const ws = workbook.addWorksheet('Sorgulamalar')
    ws.addRow(DEFAULT_COLUMNS.map((column) => column.label))
    rows.forEach((row) => ws.addRow(DEFAULT_COLUMNS.map((column) => row[column.key] || '')))
    ws.getRow(1).font = { bold: true }
    ws.columns.forEach((column) => { column.width = 28 })
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

  const clearFilters = () => setFilters(emptyFilters())
  const clearWorkflowFilters = () => setWorkflowFilters({ search: '', status: '', workflow: '', dateFrom: '', dateTo: '' })

  const flash = (type: 'success' | 'error', text: string) => {
    setMessage({ type, text })
    window.setTimeout(() => setMessage(null), 4500)
  }

  return (
    <div className="container page-enter" style={{ maxWidth: '100%', padding: '16px 18px 32px' }}>
      <div style={headerStyle}>
        <div>
          <h1 style={titleStyle}>Sorgulamalar</h1>
          <p style={subtitleStyle}>Manuel sorgu kayitlarini ve agentic workflow mail ciktilarini yonetin.</p>
        </div>
        <div style={toolbarStyle}>
          <input
            ref={fileRef}
            type="file"
            accept=".xlsx,.xls"
            style={{ display: 'none' }}
            onChange={(event) => event.target.files?.[0] && importExcel(event.target.files[0])}
          />
          <ToolbarButton onClick={() => fileRef.current?.click()} icon={<Upload size={15} />} label="Excel Yukle" />
          <ToolbarButton onClick={exportExcel} icon={<Download size={15} />} label="Excel Indir" />
          <ToolbarButton onClick={loadRows} icon={<RefreshCw size={15} />} label="Yenile" />
          <PrimaryButton onClick={addRow} icon={<Plus size={15} />} label="Yeni Kayit" />
          <PrimaryButton onClick={saveRows} icon={<Save size={15} />} label={saving ? 'Kaydediliyor' : 'Kaydet'} disabled={saving} />
        </div>
      </div>

      {message && (
        <div style={{
          ...messageStyle,
          borderColor: message.type === 'success' ? '#86efac' : '#fca5a5',
          background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
          color: message.type === 'success' ? '#166534' : '#991b1b',
        }}>
          {message.text}
        </div>
      )}

      {Math.max(repliesAwaitingReview, replyNotificationCount) > 0 && (
        <div style={{ ...messageStyle, borderColor: '#f59e0b', background: '#fffbeb', color: '#92400e', display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center' }}>
          <span><strong>{Math.max(repliesAwaitingReview, replyNotificationCount)} kullanicidan cevap geldi.</strong> Cevaplari inceleyip yeterli veya yetersiz olarak karar verin.</span>
          <ToolbarButton onClick={() => { setFilters({ ...filters, status: 'cevap_inceleme_bekliyor' }); void loadRows() }} icon={<Filter size={14} />} label="Cevaplari Goster" />
        </div>
      )}

      <div style={statsGridStyle}>
        <StatCard label="Toplam Kayit" value={rows.length} />
        {stats.map((item) => <StatCard key={item.value} label={item.label} value={item.count} color={item.color} />)}
        <StatCard label="Workflow Mail" value={workflowMailRows.length} color="#8b5cf6" />
        <StatCard label="Cevap Inceleme" value={repliesAwaitingReview} color="#d97706" />
      </div>

      <section style={sectionStyle}>
        <div style={sectionHeaderStyle}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Filter size={17} color="var(--accent)" />
            <h2 style={sectionTitleStyle}>Filtreler</h2>
          </div>
          <ToolbarButton onClick={clearFilters} icon={<X size={14} />} label="Temizle" />
        </div>
        <div style={filterGridStyle}>
          <FilterInput label="Genel arama" value={filters.search} onChange={(value) => setFilters({ ...filters, search: value })} placeholder="Kullanici, konu, aksiyon..." icon={<Search size={14} />} />
          <FilterInput label="Kullanici" value={filters.user} onChange={(value) => setFilters({ ...filters, user: value })} options={suggestions.user} />
          <FilterInput label="Mail" value={filters.mail} onChange={(value) => setFilters({ ...filters, mail: value })} options={suggestions.mail} />
          <FilterInput label="Konu" value={filters.subject} onChange={(value) => setFilters({ ...filters, subject: value })} options={suggestions.subject} />
          <FilterInput label="Geri donus" value={filters.response} onChange={(value) => setFilters({ ...filters, response: value })} options={suggestions.response} />
          <FilterInput label="Aksiyon" value={filters.action} onChange={(value) => setFilters({ ...filters, action: value })} options={suggestions.action} />
          <SelectField label="Durum" value={filters.status} onChange={(value) => setFilters({ ...filters, status: value })} options={STATUS_OPTIONS} emptyLabel="Tumu" />
          <FilterInput label="Kaynak" value={filters.source} onChange={(value) => setFilters({ ...filters, source: value })} options={suggestions.source} />
          <FilterInput label="Ekip" value={filters.team} onChange={(value) => setFilters({ ...filters, team: value })} options={suggestions.team} />
          <Field label="Baslangic"><input type="date" style={inputStyle} value={filters.dateFrom} onChange={(event) => setFilters({ ...filters, dateFrom: event.target.value })} /></Field>
          <Field label="Bitis"><input type="date" style={inputStyle} value={filters.dateTo} onChange={(event) => setFilters({ ...filters, dateTo: event.target.value })} /></Field>
        </div>
      </section>

      <div style={contentGridStyle}>
        <section style={sectionStyle}>
          <div style={sectionHeaderStyle}>
            <div>
              <h2 style={sectionTitleStyle}>Sorgu Kayitlari</h2>
              <p style={sectionHintStyle}>{filteredRows.length} / {rows.length} kayit gosteriliyor</p>
            </div>
            {bulkDeleteMode ? (
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <span style={{ color: 'var(--text-secondary)', fontSize: 12, fontWeight: 700 }}>{selectedRowKeys.size} secili</span>
                <ToolbarButton
                  onClick={toggleSelectAllFiltered}
                  icon={<CheckCircle2 size={14} />}
                  label={filteredRows.length > 0 && filteredRows.every((row) => selectedRowKeys.has(row.client_key)) ? 'Secimi Kaldir' : 'Tumunu Sec'}
                  disabled={filteredRows.length === 0}
                />
                <ToolbarButton onClick={deleteSelectedRows} icon={<Trash2 size={14} />} label="Secilenleri Sil" disabled={selectedRowKeys.size === 0 || saving} />
                <ToolbarButton onClick={() => { setSelectedRowKeys(new Set()); setBulkDeleteMode(false) }} icon={<X size={14} />} label="Iptal" />
              </div>
            ) : (
              <ToolbarButton onClick={() => setBulkDeleteMode(true)} icon={<Trash2 size={14} />} label="Toplu Sil" disabled={filteredRows.length === 0} />
            )}
          </div>

          <div style={listStyle}>
            {loading ? (
              <EmptyState icon={<RefreshCw size={20} />} text="Yukleniyor..." />
            ) : filteredRows.length === 0 ? (
              <EmptyState icon={<Inbox size={20} />} text="Filtreyle eslesen kayit yok." />
            ) : filteredRows.map((row) => {
              const realIndex = rows.findIndex((candidate) => candidate.client_key === row.client_key)
              const meta = statusMeta(row.query_status)
              return (
                <div
                  key={row.client_key}
                  role="button"
                  tabIndex={0}
                  onClick={() => selectRow(row, realIndex)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') selectRow(row, realIndex)
                  }}
                  style={{
                    ...rowCardStyle,
                    borderColor: selectedIndex === realIndex ? 'var(--accent)' : 'var(--border)',
                    background: selectedIndex === realIndex ? 'rgba(59,130,246,.06)' : 'var(--surface)',
                  }}
                >
                  {bulkDeleteMode && (
                    <input
                      type="checkbox"
                      checked={selectedRowKeys.has(row.client_key)}
                      aria-label={`${row.full_name || row.mail_address || 'Sorgu kaydi'} kaydini sec`}
                      onClick={(event) => event.stopPropagation()}
                      onChange={() => toggleRowSelection(row.client_key)}
                      style={{ width: 15, height: 15, margin: '2px 2px 0 0', flexShrink: 0, accentColor: 'var(--accent)' }}
                    />
                  )}
                  <div style={{ minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                      <strong style={rowTitleStyle}>{row.full_name || inferNameFromMail(row.mail_address) || 'Isimsiz Kullanici'}</strong>
                      <Badge label={meta.label} color={meta.color} />
                    </div>
                    <div style={rowMetaStyle}>{row.mail_address || '-'} · {row.subject || '-'}</div>
                    <div style={rowMetaStyle}>{toDisplayDate(row.query_date)} · {row.action || 'Aksiyon yok'}</div>
                  </div>
                  <div style={{ display: 'flex', gap: 6, flexShrink: 0 }}>
                    <IconButton title="Duzenle" onClick={(event) => { event.stopPropagation(); selectRow(row, realIndex) }}><Edit3 size={15} /></IconButton>
                    <IconButton title="Sil" onClick={(event) => { event.stopPropagation(); void removeRow(row, realIndex) }}><Trash2 size={15} /></IconButton>
                  </div>
                </div>
              )
            })}
          </div>
        </section>

        <section style={sectionStyle}>
          <div style={sectionHeaderStyle}>
            <div>
              <h2 style={sectionTitleStyle}>Kayit Detayi</h2>
              <p style={sectionHintStyle}>Satir secerek veya yeni kayit olusturarak duzenleyin.</p>
            </div>
            <ToolbarButton onClick={applyEditor} icon={<CheckCircle2 size={14} />} label="Listeye Isle" disabled={!editor} />
          </div>

          {!editor ? (
            <EmptyState icon={<Edit3 size={20} />} text="Duzenlemek icin bir kayit secin." />
          ) : (
            <div style={editorGridStyle}>
              <Field label="Kullanici Kodu"><input style={inputStyle} value={editor.user_code || ''} onChange={(event) => updateEditor('user_code', event.target.value)} /></Field>
              <Field label="Ad Soyad"><input style={inputStyle} value={editor.full_name || ''} onChange={(event) => updateEditor('full_name', event.target.value)} /></Field>
              <Field label="Mail Adresi"><input style={inputStyle} value={editor.mail_address || ''} onChange={(event) => updateEditor('mail_address', event.target.value)} /></Field>
              <Field label="Sorgu Tarihi"><input type="date" style={inputStyle} value={editor.query_date || ''} onChange={(event) => updateEditor('query_date', event.target.value)} /></Field>
              <Field label="Konu"><input style={inputStyle} value={editor.subject || ''} onChange={(event) => updateEditor('subject', event.target.value)} /></Field>
              <SelectField label="Sorgu Durumu" value={editor.query_status || 'bekliyor'} onChange={(value) => updateEditor('query_status', value)} options={STATUS_OPTIONS} />
              <Field label="Geri Donus Durumu"><input style={inputStyle} value={editor.response_status || ''} onChange={(event) => updateEditor('response_status', event.target.value)} /></Field>
              <Field label="Aksiyon"><input style={inputStyle} value={editor.action || ''} onChange={(event) => updateEditor('action', event.target.value)} /></Field>
              <Field label="Ekip"><input style={inputStyle} value={editor.team || ''} onChange={(event) => updateEditor('team', event.target.value)} /></Field>
              <Field label="Kaynak"><input style={inputStyle} value={editor.source || ''} onChange={(event) => updateEditor('source', event.target.value)} /></Field>
              <Field label="Notlar" wide>
                <textarea style={{ ...inputStyle, height: 94, resize: 'vertical' }} value={editor.notes || ''} onChange={(event) => updateEditor('notes', event.target.value)} />
              </Field>
              {editor.reply_received_at && (
                <>
                  <Field label="Gelen Cevap" wide>
                    <textarea readOnly style={{ ...inputStyle, height: 120, resize: 'vertical', background: 'var(--surface-hover)' }} value={editor.reply_preview || 'Mail govdesi okunamadi.'} />
                  </Field>
                  <Field label="Analist Degerlendirmesi" wide>
                    <textarea style={{ ...inputStyle, height: 72, resize: 'vertical' }} value={editor.review_note || ''} onChange={(event) => updateEditor('review_note', event.target.value)} />
                  </Field>
                  {editor.query_status === 'cevap_inceleme_bekliyor' && (
                    <div style={{ display: 'flex', gap: 8, gridColumn: '1 / -1' }}>
                      <PrimaryButton onClick={() => void reviewReply(true)} icon={<CheckCircle2 size={15} />} label="Yeterli, Tamamla" disabled={saving} />
                      <ToolbarButton onClick={() => void reviewReply(false)} icon={<Edit3 size={15} />} label="Yetersiz, Acik Tut" disabled={saving} />
                    </div>
                  )}
                </>
              )}
            </div>
          )}
        </section>
      </div>

      <section style={sectionStyle}>
        <div style={sectionHeaderStyle}>
          <div>
            <h2 style={sectionTitleStyle}>Kullanici Sorgusu Olmayan Workflow Mailleri</h2>
            <p style={sectionHintStyle}>Agentic workflow rapor, metrik ve kurum toplami mailleri ayri takip edilir.</p>
          </div>
          <ToolbarButton onClick={clearWorkflowFilters} icon={<X size={14} />} label="Filtreleri Temizle" />
        </div>

        <div style={workflowFilterGridStyle}>
          <FilterInput label="Arama" value={workflowFilters.search} onChange={(value) => setWorkflowFilters({ ...workflowFilters, search: value })} placeholder="Workflow, alici, konu..." icon={<Search size={14} />} />
          <FilterInput label="Workflow" value={workflowFilters.workflow} onChange={(value) => setWorkflowFilters({ ...workflowFilters, workflow: value })} options={suggestions.workflow} />
          <SelectField label="Durum" value={workflowFilters.status} onChange={(value) => setWorkflowFilters({ ...workflowFilters, status: value })} options={WORKFLOW_STATUSES} emptyLabel="Tumu" />
          <Field label="Baslangic"><input type="date" style={inputStyle} value={workflowFilters.dateFrom} onChange={(event) => setWorkflowFilters({ ...workflowFilters, dateFrom: event.target.value })} /></Field>
          <Field label="Bitis"><input type="date" style={inputStyle} value={workflowFilters.dateTo} onChange={(event) => setWorkflowFilters({ ...workflowFilters, dateTo: event.target.value })} /></Field>
        </div>

        <div style={{ overflowX: 'auto', marginTop: 14 }}>
          <table className="table-modern" style={{ minWidth: 980 }}>
            <thead>
              <tr>
                <th>Workflow</th>
                <th>Alici</th>
                <th>Konu</th>
                <th>Tarih</th>
                <th>Durum</th>
                <th>Kaynak</th>
                <th>Deger</th>
              </tr>
            </thead>
            <tbody>
              {filteredWorkflowRows.length === 0 ? (
                <tr><td colSpan={7} style={emptyCellStyle}>Workflow mail kaydi yok.</td></tr>
              ) : filteredWorkflowRows.map((row) => (
                <tr key={row.id}>
                  <td>{row.playbook_name}</td>
                  <td>{row.to_email}</td>
                  <td title={row.subject}>{row.subject}</td>
                  <td>{toDisplayDateTime(row.mail_date)}</td>
                  <td><Badge label={workflowMailStatusLabel(row.status)} color={row.status === 'sent' ? '#10b981' : row.status === 'failed' ? '#ef4444' : '#64748b'} /></td>
                  <td>{row.source}</td>
                  <td>{row.trigger_count}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <div style={footnoteStyle}>
        <FileSpreadsheet size={16} /> Excel yukleme/indirme korunur; gunluk kullanimda kayitlar filtrelenebilir liste ve detay paneliyle yonetilir.
      </div>
    </div>
  )
}

function unique(values: Array<string | undefined | null>) {
  return Array.from(new Set(values.map((value) => String(value || '').trim()).filter(Boolean))).sort((a, b) => a.localeCompare(b, 'tr-TR'))
}

function Field({ label, children, wide }: { label: string; children: ReactNode; wide?: boolean }) {
  return (
    <label style={{ display: 'block', gridColumn: wide ? '1 / -1' : undefined }}>
      <span style={labelStyle}>{label}</span>
      {children}
    </label>
  )
}

function FilterInput({ label, value, onChange, options, placeholder, icon }: { label: string; value: string; onChange: (value: string) => void; options?: string[]; placeholder?: string; icon?: ReactNode }) {
  const listId = `filter-${label.replace(/\s+/g, '-').toLowerCase()}`
  return (
    <Field label={label}>
      <div style={{ position: 'relative' }}>
        {icon && <span style={{ position: 'absolute', left: 10, top: 9, color: 'var(--text-muted)' }}>{icon}</span>}
        <input
          style={{ ...inputStyle, paddingLeft: icon ? 32 : 10 }}
          value={value}
          list={options?.length ? listId : undefined}
          placeholder={placeholder || 'Yaz veya sec'}
          onChange={(event) => onChange(event.target.value)}
        />
        {options?.length ? (
          <datalist id={listId}>
            {options.map((option) => <option key={option} value={option} />)}
          </datalist>
        ) : null}
      </div>
    </Field>
  )
}

function SelectField({ label, value, onChange, options, emptyLabel }: { label: string; value: string; onChange: (value: string) => void; options: Array<{ value: string; label: string }>; emptyLabel?: string }) {
  return (
    <Field label={label}>
      <select style={inputStyle} value={value} onChange={(event) => onChange(event.target.value)}>
        {emptyLabel && <option value="">{emptyLabel}</option>}
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </Field>
  )
}

function ToolbarButton({ icon, label, onClick, disabled }: { icon: ReactNode; label: string; onClick: () => void; disabled?: boolean }) {
  return <button type="button" onClick={onClick} disabled={disabled} style={{ ...buttonStyle, opacity: disabled ? .6 : 1 }}>{icon}{label}</button>
}

function PrimaryButton({ icon, label, onClick, disabled }: { icon: ReactNode; label: string; onClick: () => void; disabled?: boolean }) {
  return <button type="button" onClick={onClick} disabled={disabled} style={{ ...primaryButtonStyle, opacity: disabled ? .6 : 1 }}>{icon}{label}</button>
}

function IconButton({ title, children, onClick }: { title: string; children: ReactNode; onClick: (event: MouseEvent<HTMLButtonElement>) => void }) {
  return <button type="button" title={title} onClick={onClick} style={iconButtonStyle}>{children}</button>
}

function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', padding: '3px 8px', borderRadius: 999, background: `${color}1f`, color, fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap' }}>
      {label}
    </span>
  )
}

function StatCard({ label, value, color = 'var(--accent)' }: { label: string; value: number; color?: string }) {
  return (
    <div style={statCardStyle}>
      <div style={{ color: 'var(--text-secondary)', fontSize: 12, fontWeight: 700 }}>{label}</div>
      <div style={{ color, fontSize: 24, fontWeight: 850, lineHeight: 1.1 }}>{value}</div>
    </div>
  )
}

function EmptyState({ icon, text }: { icon: ReactNode; text: string }) {
  return (
    <div style={{ display: 'grid', placeItems: 'center', gap: 8, minHeight: 180, color: 'var(--text-muted)', textAlign: 'center' }}>
      {icon}
      <span>{text}</span>
    </div>
  )
}

const headerStyle = {
  display: 'flex',
  alignItems: 'flex-start',
  justifyContent: 'space-between',
  gap: 16,
  marginBottom: 16,
  flexWrap: 'wrap' as const,
}

const titleStyle = {
  margin: 0,
  color: 'var(--text-primary)',
  fontSize: 24,
  fontWeight: 850,
}

const subtitleStyle = {
  margin: '3px 0 0',
  color: 'var(--text-secondary)',
  fontSize: 13,
}

const toolbarStyle = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'flex-end',
  gap: 8,
  flexWrap: 'wrap' as const,
}

const sectionStyle = {
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: 16,
  marginBottom: 16,
  boxShadow: 'var(--shadow-sm)',
}

const sectionHeaderStyle = {
  display: 'flex',
  alignItems: 'flex-start',
  justifyContent: 'space-between',
  gap: 12,
  marginBottom: 12,
  flexWrap: 'wrap' as const,
}

const sectionTitleStyle = {
  margin: 0,
  color: 'var(--text-primary)',
  fontSize: 16,
  fontWeight: 850,
}

const sectionHintStyle = {
  margin: '3px 0 0',
  color: 'var(--text-secondary)',
  fontSize: 12,
}

const statsGridStyle = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
  gap: 10,
  marginBottom: 16,
}

const statCardStyle = {
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: 14,
  boxShadow: 'var(--shadow-sm)',
}

const filterGridStyle = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))',
  gap: 12,
}

const workflowFilterGridStyle = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))',
  gap: 12,
}

const contentGridStyle = {
  display: 'grid',
  gridTemplateColumns: 'minmax(360px, .95fr) minmax(420px, 1.05fr)',
  gap: 16,
  alignItems: 'start',
}

const listStyle = {
  display: 'grid',
  gap: 10,
  maxHeight: 'calc(100vh - 360px)',
  minHeight: 260,
  overflow: 'auto',
  paddingRight: 4,
}

const rowCardStyle = {
  width: '100%',
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  gap: 12,
  textAlign: 'left' as const,
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: 12,
  cursor: 'pointer',
  color: 'var(--text-primary)',
}

const rowTitleStyle = {
  display: 'block',
  minWidth: 0,
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap' as const,
}

const rowMetaStyle = {
  marginTop: 3,
  color: 'var(--text-secondary)',
  fontSize: 12,
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap' as const,
}

const editorGridStyle = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
  gap: 12,
}

const inputStyle = {
  width: '100%',
  minHeight: 34,
  padding: '7px 10px',
  border: '1px solid var(--border)',
  borderRadius: 6,
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  outline: 'none',
  fontSize: 13,
  fontFamily: 'Inter, sans-serif',
} as const

const labelStyle = {
  display: 'block',
  marginBottom: 6,
  color: 'var(--text-primary)',
  fontSize: 12,
  fontWeight: 800,
}

const buttonStyle = {
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 6,
  minHeight: 34,
  padding: '7px 11px',
  borderRadius: 6,
  border: '1px solid var(--border)',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  cursor: 'pointer',
  fontSize: 13,
  fontWeight: 800,
  fontFamily: 'Inter, sans-serif',
} as const

const primaryButtonStyle = {
  ...buttonStyle,
  borderColor: 'var(--primary)',
  background: 'var(--primary)',
  color: '#fff',
}

const iconButtonStyle = {
  ...buttonStyle,
  width: 32,
  height: 32,
  padding: 0,
  flex: '0 0 auto',
}

const messageStyle = {
  marginBottom: 12,
  padding: '10px 14px',
  borderRadius: 8,
  border: '1px solid',
  fontSize: 13,
  fontWeight: 800,
}

const emptyCellStyle = {
  padding: 30,
  textAlign: 'center' as const,
  color: 'var(--text-muted)',
}

const footnoteStyle = {
  marginTop: 12,
  display: 'flex',
  alignItems: 'center',
  gap: 8,
  color: 'var(--text-muted)',
  fontSize: 13,
}
