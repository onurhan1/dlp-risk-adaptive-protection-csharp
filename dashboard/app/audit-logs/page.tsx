'use client'

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import { Bot, ChevronLeft, ChevronRight, ClipboardList, RefreshCw, Search, ShieldCheck, UserRound } from 'lucide-react'
import apiClient from '@/lib/axios'

type Tab = 'user' | 'audit' | 'agent' | 'workflow'

interface AuditLog {
  id: number
  timestamp: string
  eventType: string
  userName: string
  userRole?: string | null
  action: string
  resource?: string | null
  details?: string | null
  success: boolean
  errorMessage?: string | null
  statusCode?: number | null
  durationMs?: number | null
}

interface UserActivityLog {
  id: number
  timestamp: string
  userName: string
  authSource: string
  activityType: string
  pagePath?: string | null
  pageTitle?: string | null
  actionDetail?: string | null
  sessionDurationSeconds?: number | null
}

interface WorkflowRun {
  id: number
  playbook_id: number
  playbook_name: string
  started_at: string
  finished_at?: string | null
  status: string
  trigger_type: string
  dry_run: boolean
  mails_sent: number
  mails_pending: number
  mails_failed: number
  mails_skipped: number
  error_message?: string | null
}

function normalizeAuditLog(row: any): AuditLog {
  return {
    id: row.id,
    timestamp: row.timestamp,
    eventType: row.eventType ?? row.event_type ?? '-',
    userName: row.userName ?? row.user_name ?? '-',
    userRole: row.userRole ?? row.user_role ?? null,
    action: row.action ?? '-',
    resource: row.resource ?? null,
    details: row.details ?? null,
    success: Boolean(row.success),
    errorMessage: row.errorMessage ?? row.error_message ?? null,
    statusCode: row.statusCode ?? row.status_code ?? null,
    durationMs: row.durationMs ?? row.duration_ms ?? null,
  }
}

function normalizeUserActivityLog(row: any): UserActivityLog {
  return {
    id: row.id,
    timestamp: row.timestamp,
    userName: row.userName ?? row.user_name ?? '-',
    authSource: row.authSource ?? row.auth_source ?? '-',
    activityType: row.activityType ?? row.activity_type ?? '-',
    pagePath: row.pagePath ?? row.page_path ?? null,
    pageTitle: row.pageTitle ?? row.page_title ?? null,
    actionDetail: row.actionDetail ?? row.action_detail ?? null,
    sessionDurationSeconds: row.sessionDurationSeconds ?? row.session_duration_seconds ?? null,
  }
}

function normalizeWorkflowRun(row: any): WorkflowRun {
  return {
    id: row.id,
    playbook_id: row.playbook_id ?? row.playbookId,
    playbook_name: row.playbook_name ?? row.playbookName ?? '-',
    started_at: row.started_at ?? row.startedAt,
    finished_at: row.finished_at ?? row.finishedAt ?? null,
    status: row.status ?? 'unknown',
    trigger_type: row.trigger_type ?? row.triggerType ?? 'manual',
    dry_run: Boolean(row.dry_run ?? row.dryRun),
    mails_sent: Number(row.mails_sent ?? row.mailsSent ?? 0),
    mails_pending: Number(row.mails_pending ?? row.mailsPending ?? 0),
    mails_failed: Number(row.mails_failed ?? row.mailsFailed ?? 0),
    mails_skipped: Number(row.mails_skipped ?? row.mailsSkipped ?? 0),
    error_message: row.error_message ?? row.errorMessage ?? null,
  }
}

const inputStyle = {
  height: 36,
  padding: '7px 10px',
  border: '1px solid var(--border)',
  borderRadius: 6,
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontSize: 13,
} as const

const tabMeta: Record<Tab, { label: string; icon: typeof UserRound; description: string }> = {
  user: { label: 'Kullanıcı Hareketleri', icon: UserRound, description: 'Sisteme giren kullanıcılar, gezindikleri sayfalar ve oturum süreleri.' },
  audit: { label: 'Sistem Denetimi', icon: ShieldCheck, description: 'API işlemleri, ayar değişiklikleri, kullanıcı yönetimi ve başarısız istekler.' },
  agent: { label: 'Yapay Zeka İşlemleri', icon: Bot, description: 'Yerel LLM sohbetleri, analizler, taslaklar ve kullanıcı onayları.' },
  workflow: { label: 'Agentic Workflow', icon: ClipboardList, description: 'Manuel, zamanlanmış ve e-posta tetiklemeli workflow çalıştırmaları.' },
}

function localTime(value?: string | null) {
  if (!value) return '-'
  return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'medium', timeZone: 'Europe/Istanbul' }).format(new Date(value))
}

function statusStyle(success: boolean) {
  return { background: success ? '#dcfce7' : '#fee2e2', color: success ? '#15803d' : '#b91c1c' }
}

function runStatusStyle(status: string) {
  const ok = status === 'success'
  const waiting = status === 'awaiting_approval' || status === 'running'
  return { background: ok ? '#dcfce7' : waiting ? '#fef3c7' : '#fee2e2', color: ok ? '#15803d' : waiting ? '#a16207' : '#b91c1c' }
}

export default function AuditLogsPage() {
  const [tab, setTab] = useState<Tab>('user')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [search, setSearch] = useState('')
  const [eventType, setEventType] = useState('')
  const [workflowStatus, setWorkflowStatus] = useState('all')
  const [eventTypes, setEventTypes] = useState<string[]>([])
  const [auditLogs, setAuditLogs] = useState<AuditLog[]>([])
  const [userLogs, setUserLogs] = useState<UserActivityLog[]>([])
  const [workflowRuns, setWorkflowRuns] = useState<WorkflowRun[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const current = tabMeta[tab]
  const CurrentIcon = current.icon
  const clearRows = () => {
    setAuditLogs([])
    setUserLogs([])
    setWorkflowRuns([])
  }

  const load = useCallback(async (targetPage = page) => {
    setLoading(true)
    setError(null)
    try {
      const params: Record<string, string | number> = { page: targetPage, pageSize: 50 }
      if (startDate) params.startDate = new Date(startDate).toISOString()
      if (endDate) params.endDate = new Date(endDate).toISOString()
      if (search.trim()) params.userName = search.trim()

      if (tab === 'user') {
        if (eventType) params.activityType = eventType
        const { data } = await apiClient.get('/api/activity/logs', { params })
        setUserLogs((data.logs ?? []).map(normalizeUserActivityLog))
        setTotal(data.total ?? 0)
        setTotalPages(data.totalPages ?? 1)
      } else if (tab === 'workflow') {
        delete params.userName
        if (search.trim()) params.search = search.trim()
        if (workflowStatus !== 'all') params.status = workflowStatus
        const { data } = await apiClient.get('/api/playbooks/runs/audit', { params })
        setWorkflowRuns((data.logs ?? []).map(normalizeWorkflowRun))
        setTotal(data.total ?? 0)
        setTotalPages(data.totalPages ?? 1)
      } else {
        delete params.userName
        if (search.trim()) params.userName = search.trim()
        params.eventType = tab === 'agent' ? 'AgentAction' : eventType
        const { data } = await apiClient.get('/api/logs/audit', { params })
        setAuditLogs((data.logs ?? []).map(normalizeAuditLog))
        setTotal(data.total ?? 0)
        setTotalPages(data.totalPages ?? 1)
      }
    } catch (requestError: any) {
      clearRows()
      setTotal(0)
      setTotalPages(1)
      setError(requestError?.response?.data?.detail || 'Denetim kayıtları alınamadı.')
    } finally {
      setLoading(false)
    }
  }, [endDate, eventType, page, search, startDate, tab, workflowStatus])

  useEffect(() => {
    apiClient.get('/api/logs/audit/event-types')
      .then(response => setEventTypes(response.data ?? []))
      .catch(() => setEventTypes([]))
  }, [])

  useEffect(() => {
    void load(1)
    setPage(1)
  // Load again only when the active data source changes; filters apply with Ara.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab])

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setPage(1)
    void load(1)
  }

  const reset = () => {
    setStartDate('')
    setEndDate('')
    setSearch('')
    setEventType('')
    setWorkflowStatus('all')
    setPage(1)
    window.setTimeout(() => void load(1), 0)
  }

  const tabButtons = useMemo(() => (Object.keys(tabMeta) as Tab[]).map(key => {
    const item = tabMeta[key]
    const Icon = item.icon
    return <button key={key} onClick={() => { setTab(key); setEventType(''); setWorkflowStatus('all'); clearRows() }} style={{ display: 'inline-flex', alignItems: 'center', gap: 7, border: 'none', borderBottom: tab === key ? '2px solid var(--primary)' : '2px solid transparent', background: 'transparent', color: tab === key ? 'var(--primary)' : 'var(--text-secondary)', padding: '11px 12px', cursor: 'pointer', fontSize: 13, fontWeight: 700 }}><Icon size={16} />{item.label}</button>
  }), [tab])

  return <main className="container" style={{ maxWidth: 1480, paddingTop: 24, paddingBottom: 36 }}>
    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap', marginBottom: 18 }}>
      <div>
        <h1 style={{ margin: 0, fontSize: 24, letterSpacing: 0 }}>Denetim Kayıtları</h1>
        <p style={{ margin: '6px 0 0', color: 'var(--text-secondary)', fontSize: 13 }}>{current.description}</p>
      </div>
      <button onClick={() => void load()} disabled={loading} title="Yenile" style={{ display: 'inline-flex', alignItems: 'center', gap: 7, ...inputStyle, cursor: loading ? 'wait' : 'pointer', fontWeight: 700 }}><RefreshCw size={15} style={{ animation: loading ? 'spin 1s linear infinite' : undefined }} /> Yenile</button>
    </div>

    <section style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
      <div style={{ display: 'flex', gap: 4, borderBottom: '1px solid var(--border)', padding: '0 12px', overflowX: 'auto' }}>{tabButtons}</div>
      <form onSubmit={submit} style={{ padding: 16, borderBottom: '1px solid var(--border)', background: 'var(--background)' }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'end', flexWrap: 'wrap' }}>
          <label style={{ display: 'grid', gap: 5, fontSize: 12, fontWeight: 700 }}>Başlangıç<input type="datetime-local" value={startDate} onChange={e => setStartDate(e.target.value)} style={inputStyle} /></label>
          <label style={{ display: 'grid', gap: 5, fontSize: 12, fontWeight: 700 }}>Bitiş<input type="datetime-local" value={endDate} onChange={e => setEndDate(e.target.value)} style={inputStyle} /></label>
          <label style={{ display: 'grid', gap: 5, fontSize: 12, fontWeight: 700, minWidth: 230 }}>{tab === 'workflow' ? 'Workflow veya tetikleyici ara' : 'Kullanıcı ara'}<input value={search} onChange={e => setSearch(e.target.value)} placeholder={tab === 'workflow' ? 'Workflow adı, manual, schedule...' : 'Kullanıcı adı'} style={inputStyle} /></label>
          {tab === 'audit' && <label style={{ display: 'grid', gap: 5, fontSize: 12, fontWeight: 700 }}>Olay türü<select value={eventType} onChange={e => setEventType(e.target.value)} style={inputStyle}><option value="">Tümü</option>{eventTypes.map(type => <option key={type} value={type}>{type}</option>)}</select></label>}
          {tab === 'user' && <label style={{ display: 'grid', gap: 5, fontSize: 12, fontWeight: 700 }}>Hareket türü<input value={eventType} onChange={e => setEventType(e.target.value)} placeholder="PageVisit, Login..." style={inputStyle} /></label>}
          {tab === 'workflow' && <label style={{ display: 'grid', gap: 5, fontSize: 12, fontWeight: 700 }}>Durum<select value={workflowStatus} onChange={e => setWorkflowStatus(e.target.value)} style={inputStyle}><option value="all">Tümü</option><option value="success">Başarılı</option><option value="awaiting_approval">Onay bekliyor</option><option value="running">Çalışıyor</option><option value="failed">Başarısız</option></select></label>}
          <button type="submit" disabled={loading} style={{ ...inputStyle, cursor: 'pointer', background: 'var(--primary)', color: 'white', borderColor: 'var(--primary)', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: 6 }}><Search size={15} /> Ara</button>
          <button type="button" onClick={reset} style={{ ...inputStyle, cursor: 'pointer', fontWeight: 700 }}>Temizle</button>
        </div>
      </form>

      {error && <div style={{ margin: 16, padding: 12, borderRadius: 6, color: '#b91c1c', background: '#fee2e2', fontSize: 13 }}>{error}</div>}
      <div style={{ overflowX: 'auto' }}>
        {tab === 'user' ? <UserTable rows={userLogs} /> : tab === 'workflow' ? <WorkflowTable rows={workflowRuns} /> : <AuditTable rows={auditLogs} agent={tab === 'agent'} />}
      </div>
      <footer style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: 14, borderTop: '1px solid var(--border)', color: 'var(--text-secondary)', fontSize: 13 }}>
        <span>{loading ? 'Kayıtlar yükleniyor...' : `${total.toLocaleString('tr-TR')} kayıt`}</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <button onClick={() => { const next = Math.max(1, page - 1); setPage(next); void load(next) }} disabled={loading || page <= 1} title="Önceki sayfa" style={{ ...inputStyle, cursor: page <= 1 ? 'not-allowed' : 'pointer', padding: 7 }}><ChevronLeft size={16} /></button>
          <span>{page} / {Math.max(totalPages, 1)}</span>
          <button onClick={() => { const next = Math.min(totalPages, page + 1); setPage(next); void load(next) }} disabled={loading || page >= totalPages} title="Sonraki sayfa" style={{ ...inputStyle, cursor: page >= totalPages ? 'not-allowed' : 'pointer', padding: 7 }}><ChevronRight size={16} /></button>
        </div>
      </footer>
    </section>
  </main>
}

function Empty({ columns }: { columns: number }) { return <tr><td colSpan={columns} style={{ padding: '44px 18px', color: 'var(--text-secondary)', textAlign: 'center' }}>Bu filtrelerde kayıt bulunamadı.</td></tr> }
const th = { padding: '11px 14px', textAlign: 'left' as const, fontSize: 11, color: 'var(--text-secondary)', borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' as const }
const td = { padding: '12px 14px', fontSize: 13, borderBottom: '1px solid var(--border)', verticalAlign: 'top' as const }

function AuditTable({ rows, agent }: { rows: AuditLog[]; agent: boolean }) { return <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 880 }}><thead><tr><th style={th}>Zaman</th><th style={th}>Kullanıcı</th><th style={th}>{agent ? 'Agent işlemi' : 'Olay türü'}</th><th style={th}>İşlem</th><th style={th}>Sonuç</th><th style={th}>Süre</th></tr></thead><tbody>{rows.length === 0 ? <Empty columns={6} /> : rows.map(row => <tr key={row.id}><td style={td}>{localTime(row.timestamp)}</td><td style={td}><strong>{row.userName}</strong>{row.userRole && <div style={{ color: 'var(--text-secondary)', fontSize: 11, marginTop: 3 }}>{row.userRole}</div>}</td><td style={td}><span style={{ padding: '3px 7px', borderRadius: 4, background: '#dbeafe', color: '#1d4ed8', fontWeight: 700, fontSize: 11 }}>{row.eventType}</span></td><td style={{ ...td, maxWidth: 440 }}><div style={{ fontFamily: 'monospace', fontSize: 12 }}>{row.action}</div>{row.resource && <div style={{ marginTop: 4, color: 'var(--text-secondary)', fontSize: 11 }}>{row.resource}</div>}{row.errorMessage && <div style={{ marginTop: 5, color: '#b91c1c', fontSize: 11 }}>{row.errorMessage}</div>}</td><td style={td}><span style={{ ...statusStyle(row.success), padding: '3px 7px', borderRadius: 4, fontSize: 11, fontWeight: 700 }}>{row.success ? 'Başarılı' : 'Başarısız'}{row.statusCode ? ` (${row.statusCode})` : ''}</span></td><td style={td}>{row.durationMs == null ? '-' : `${row.durationMs} ms`}</td></tr>)}</tbody></table> }

function UserTable({ rows }: { rows: UserActivityLog[] }) { return <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 820 }}><thead><tr><th style={th}>Zaman</th><th style={th}>Kullanıcı</th><th style={th}>Doğrulama</th><th style={th}>Hareket</th><th style={th}>Sayfa / Detay</th><th style={th}>Oturum</th></tr></thead><tbody>{rows.length === 0 ? <Empty columns={6} /> : rows.map(row => <tr key={row.id}><td style={td}>{localTime(row.timestamp)}</td><td style={{ ...td, fontWeight: 700 }}>{row.userName}</td><td style={td}>{row.authSource || '-'}</td><td style={td}><span style={{ padding: '3px 7px', borderRadius: 4, background: '#e0e7ff', color: '#4338ca', fontWeight: 700, fontSize: 11 }}>{row.activityType}</span></td><td style={{ ...td, maxWidth: 420 }}><div>{row.pageTitle || row.pagePath || '-'}</div>{row.actionDetail && <div style={{ color: 'var(--text-secondary)', fontSize: 11, marginTop: 4 }}>{row.actionDetail}</div>}</td><td style={td}>{row.sessionDurationSeconds ? `${row.sessionDurationSeconds} sn` : '-'}</td></tr>)}</tbody></table> }

function WorkflowTable({ rows }: { rows: WorkflowRun[] }) { return <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 1040 }}><thead><tr><th style={th}>Başlangıç</th><th style={th}>Workflow</th><th style={th}>Tetikleyici</th><th style={th}>Durum</th><th style={th}>Mail sonucu</th><th style={th}>Bitiş / Hata</th></tr></thead><tbody>{rows.length === 0 ? <Empty columns={6} /> : rows.map(row => <tr key={row.id}><td style={td}>{localTime(row.started_at)}</td><td style={{ ...td, fontWeight: 700 }}>{row.playbook_name}<div style={{ color: 'var(--text-secondary)', fontSize: 11, marginTop: 3 }}>Çalıştırma #{row.id}</div></td><td style={td}>{row.trigger_type === 'schedule' ? 'Zamanlanmış' : row.trigger_type === 'email_request' ? 'E-posta isteği' : 'Manuel'}{row.dry_run && <div style={{ color: '#a16207', fontSize: 11, marginTop: 3 }}>Önizleme / onaylı gönderim</div>}</td><td style={td}><span style={{ ...runStatusStyle(row.status), padding: '3px 7px', borderRadius: 4, fontSize: 11, fontWeight: 700 }}>{row.status === 'success' ? 'Başarılı' : row.status === 'awaiting_approval' ? 'Onay bekliyor' : row.status === 'running' ? 'Çalışıyor' : 'Başarısız'}</span></td><td style={td}>Gönderilen: {row.mails_sent} <span style={{ color: '#a16207' }}>Bekleyen: {row.mails_pending}</span><div style={{ color: row.mails_failed ? '#b91c1c' : 'var(--text-secondary)', fontSize: 11, marginTop: 3 }}>Hata: {row.mails_failed}, Atlanan: {row.mails_skipped}</div></td><td style={td}>{localTime(row.finished_at)}{row.error_message && <div style={{ color: '#b91c1c', fontSize: 11, marginTop: 4, maxWidth: 300 }}>{row.error_message}</div>}</td></tr>)}</tbody></table> }
