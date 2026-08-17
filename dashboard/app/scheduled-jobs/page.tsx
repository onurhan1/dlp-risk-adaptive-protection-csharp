'use client'

import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import apiClient from '@/lib/axios'
import {
  CalendarClock,
  CheckCircle2,
  Clock3,
  Pause,
  Play,
  Plus,
  RefreshCw,
  Save,
  XCircle,
} from 'lucide-react'

interface Job {
  id: number
  name: string
  description?: string
  handler_key: string
  handler_label: string
  handler_payload?: Record<string, unknown>
  cron_expression: string
  schedule_summary: string
  enabled: boolean
  last_run_at?: string | null
  next_run_at?: string | null
  last_status: string
  last_message?: string | null
}

interface Run {
  id: number
  scheduled_job_id: number
  started_at: string
  finished_at?: string | null
  trigger_type: string
  status: string
  message?: string | null
}

interface HandlerOption {
  key: string
  label: string
}

export default function ScheduledJobsPage() {
  const [jobs, setJobs] = useState<Job[]>([])
  const [runs, setRuns] = useState<Run[]>([])
  const [handlers, setHandlers] = useState<HandlerOption[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [runningId, setRunningId] = useState<number | null>(null)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [form, setForm] = useState({
    name: '',
    description: '',
    handler_key: 'released_incident_sync',
    cron_expression: '0 2 * * *',
    enabled: true,
    lookback_hours: 24,
    retention_days: 90,
    lookback_days: 7,
    top_limit: 25,
    min_risk_score: 80,
    max_match_threshold: 300,
    recipient_email: '',
    cc_email: '',
  })

  useEffect(() => {
    void load()
  }, [])

  const load = async () => {
    setLoading(true)
    try {
      const [jobsRes, runsRes, catalogRes] = await Promise.all([
        apiClient.get('/api/scheduled-jobs'),
        apiClient.get('/api/scheduled-jobs/runs', { params: { limit: 30 } }),
        apiClient.get('/api/scheduled-jobs/catalog'),
      ])
      setJobs(jobsRes.data || [])
      setRuns(runsRes.data || [])
      setHandlers(catalogRes.data?.handlers || [])
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Zamanlanmis isler yuklenemedi')
    } finally {
      setLoading(false)
    }
  }

  const createJob = async () => {
    setSaving(true)
    try {
      await apiClient.post('/api/scheduled-jobs', {
        name: form.name.trim(),
        description: form.description.trim(),
        handler_key: form.handler_key,
        cron_expression: form.cron_expression.trim(),
        enabled: form.enabled,
        lookback_hours: form.handler_key === 'released_incident_sync' ? form.lookback_hours : null,
        retention_days: form.handler_key === 'log_cleanup' ? form.retention_days : null,
        lookback_days: isReportHandler(form.handler_key) ? form.lookback_days : null,
        top_limit: isReportHandler(form.handler_key) ? form.top_limit : null,
        min_risk_score: form.handler_key === 'report_weekly_high_score_users' ? form.min_risk_score : null,
        max_match_threshold: form.handler_key === 'report_high_max_match_transfers' ? form.max_match_threshold : null,
        recipient_email: isReportHandler(form.handler_key) ? form.recipient_email.trim() || null : null,
        cc_email: isReportHandler(form.handler_key) ? form.cc_email.trim() || null : null,
      })
      setForm((prev) => ({ ...prev, name: '', description: '' }))
      flash('success', 'Zamanlanmis is eklendi')
      await load()
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Is eklenemedi')
    } finally {
      setSaving(false)
    }
  }

  const toggleJob = async (job: Job) => {
    try {
      await apiClient.post(`/api/scheduled-jobs/${job.id}/toggle`)
      await load()
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Durum degistirilemedi')
    }
  }

  const runJob = async (job: Job) => {
    setRunningId(job.id)
    try {
      await apiClient.post(`/api/scheduled-jobs/${job.id}/run`, null, { timeout: 120000 })
      flash('success', `${job.name} calistirildi`)
      await load()
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Is calistirilamadi')
    } finally {
      setRunningId(null)
    }
  }

  const flash = (type: 'success' | 'error', text: string) => {
    setMessage({ type, text })
    window.setTimeout(() => setMessage(null), 4500)
  }

  return (
    <div className="container page-enter" style={{ maxWidth: '100%', padding: '16px 18px 32px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 16, marginBottom: 16 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 22, fontWeight: 800, color: 'var(--text-primary)' }}>Zamanlanmis Isler</h1>
          <p style={{ margin: '2px 0 0', color: 'var(--text-secondary)', fontSize: 13 }}>Otomatik calisan sistem ve agentic workflow islerini yonetin.</p>
        </div>
        <button onClick={load} style={buttonStyle} disabled={loading}>
          <RefreshCw size={15} style={{ animation: loading ? 'spin 1s linear infinite' : 'none' }} /> Yenile
        </button>
      </div>

      {message && (
        <div style={{
          marginBottom: 12,
          padding: '10px 14px',
          borderRadius: 8,
          border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`,
          background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
          color: message.type === 'success' ? '#166534' : '#991b1b',
          fontWeight: 700,
          fontSize: 13,
        }}>
          {message.text}
        </div>
      )}

      <section style={sectionStyle}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
          <Plus size={17} color="var(--accent)" />
          <h2 style={{ margin: 0, fontSize: 16, fontWeight: 800 }}>Yeni Is</h2>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, alignItems: 'end' }}>
          <Field label="Is Adi"><input style={inputStyle} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="Is Tipi">
            <select style={inputStyle} value={form.handler_key} onChange={(e) => setForm({ ...form, handler_key: e.target.value })}>
              {handlers.map((handler) => <option key={handler.key} value={handler.key}>{handler.label}</option>)}
            </select>
          </Field>
          <Field label="Cron"><input style={inputStyle} value={form.cron_expression} onChange={(e) => setForm({ ...form, cron_expression: e.target.value })} /></Field>
          {form.handler_key === 'released_incident_sync' ? (
            <Field label="Lookback saat"><input type="number" min={1} style={inputStyle} value={form.lookback_hours} onChange={(e) => setForm({ ...form, lookback_hours: Number(e.target.value) || 24 })} /></Field>
          ) : form.handler_key === 'log_cleanup' ? (
            <Field label="Saklama gunu"><input type="number" min={1} style={inputStyle} value={form.retention_days} onChange={(e) => setForm({ ...form, retention_days: Number(e.target.value) || 90 })} /></Field>
          ) : isReportHandler(form.handler_key) ? (
            <Field label="Rapor donemi gun"><input type="number" min={1} style={inputStyle} value={form.lookback_days} onChange={(e) => setForm({ ...form, lookback_days: Number(e.target.value) || 7 })} /></Field>
          ) : (
            <div />
          )}
          {isReportHandler(form.handler_key) ? (
            <Field label="Top limit"><input type="number" min={1} style={inputStyle} value={form.top_limit} onChange={(e) => setForm({ ...form, top_limit: Number(e.target.value) || 25 })} /></Field>
          ) : <div />}
          <label style={{ display: 'inline-flex', alignItems: 'center', gap: 8, height: 34, fontWeight: 700, fontSize: 13 }}>
            <input type="checkbox" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} />
            Aktif
          </label>
          <button onClick={createJob} disabled={saving || !form.name.trim()} style={{ ...primaryButtonStyle, opacity: saving || !form.name.trim() ? .6 : 1 }}>
            <Save size={15} /> {saving ? 'Kaydediliyor...' : 'Ekle'}
          </button>
        </div>
        <div style={{ marginTop: 12 }}>
          <Field label="Aciklama"><input style={inputStyle} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
        </div>
        {isReportHandler(form.handler_key) && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, marginTop: 12 }}>
            <Field label="Rapor alicisi"><input type="email" style={inputStyle} placeholder="Bos kalirsa Yonetici E-postasi kullanilir" value={form.recipient_email} onChange={(e) => setForm({ ...form, recipient_email: e.target.value })} /></Field>
            <Field label="CC"><input type="email" style={inputStyle} value={form.cc_email} onChange={(e) => setForm({ ...form, cc_email: e.target.value })} /></Field>
            {form.handler_key === 'report_weekly_high_score_users' && (
              <Field label="Min risk skoru"><input type="number" min={0} max={100} style={inputStyle} value={form.min_risk_score} onChange={(e) => setForm({ ...form, min_risk_score: Number(e.target.value) || 80 })} /></Field>
            )}
            {form.handler_key === 'report_high_max_match_transfers' && (
              <Field label="Max Match alt siniri"><input type="number" min={1} style={inputStyle} value={form.max_match_threshold} onChange={(e) => setForm({ ...form, max_match_threshold: Number(e.target.value) || 300 })} /></Field>
            )}
          </div>
        )}
      </section>

      <section style={sectionStyle}>
        <div style={{ overflowX: 'auto' }}>
          <table className="table-modern" style={{ minWidth: 1100 }}>
            <thead>
              <tr>
                <th>Is Adi</th>
                <th>Zamanlama</th>
                <th>Durum</th>
                <th>Son Calisma</th>
                <th>Sonraki Calisma</th>
                <th>Islemler</th>
              </tr>
            </thead>
            <tbody>
              {jobs.length === 0 ? (
                <tr><td colSpan={6} style={{ textAlign: 'center', padding: 36, color: 'var(--text-muted)' }}>Kayitli zamanlanmis is yok</td></tr>
              ) : jobs.map((job) => (
                <tr key={job.id}>
                  <td>
                    <div style={{ fontWeight: 800 }}>{job.name}</div>
                    <div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{job.description || job.handler_label}</div>
                  </td>
                  <td>
                    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '4px 8px', border: '1px solid var(--border)', borderRadius: 6 }}>
                      <Clock3 size={13} /> {job.cron_expression}
                    </div>
                    <div style={{ color: 'var(--text-secondary)', fontSize: 12, marginTop: 4 }}>{job.schedule_summary}</div>
                  </td>
                  <td><StatusBadge status={job.enabled ? job.last_status : 'passive'} /></td>
                  <td>{formatDate(job.last_run_at)}<div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{job.last_message || '-'}</div></td>
                  <td>{job.enabled ? formatDate(job.next_run_at) : '-'}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <button title="Calistir" onClick={() => runJob(job)} disabled={runningId === job.id} style={iconButtonStyle}>
                        <Play size={15} />
                      </button>
                      <button title={job.enabled ? 'Duraklat' : 'Aktif et'} onClick={() => toggleJob(job)} style={iconButtonStyle}>
                        {job.enabled ? <Pause size={15} /> : <Play size={15} />}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section style={sectionStyle}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
          <CalendarClock size={17} color="var(--accent)" />
          <h2 style={{ margin: 0, fontSize: 16, fontWeight: 800 }}>Calisma Ozeti</h2>
          <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}>Son 30 kayit</span>
        </div>
        <div style={{ overflowX: 'auto' }}>
          <table className="table-modern" style={{ minWidth: 900 }}>
            <thead>
              <tr>
                <th>Run</th>
                <th>Is</th>
                <th>Tarih</th>
                <th>Tetikleme</th>
                <th>Durum</th>
                <th>Mesaj</th>
              </tr>
            </thead>
            <tbody>
              {runs.length === 0 ? (
                <tr><td colSpan={6} style={{ textAlign: 'center', padding: 30, color: 'var(--text-muted)' }}>Calisma gecmisi yok</td></tr>
              ) : runs.map((run) => (
                <tr key={run.id}>
                  <td>#{run.id}</td>
                  <td>{jobs.find((job) => job.id === run.scheduled_job_id)?.name || run.scheduled_job_id}</td>
                  <td>{formatDate(run.started_at)}</td>
                  <td>{run.trigger_type}</td>
                  <td><StatusBadge status={run.status} /></td>
                  <td>{run.message || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: 'block' }}>
      <span style={{ display: 'block', marginBottom: 6, fontSize: 12, fontWeight: 800 }}>{label}</span>
      {children}
    </label>
  )
}

function StatusBadge({ status }: { status: string }) {
  const isSuccess = status === 'success'
  const isFailed = status === 'failed'
  const isRunning = status === 'running'
  const isPassive = status === 'passive'
  const color = isSuccess ? '#10b981' : isFailed ? '#ef4444' : isRunning ? '#3b82f6' : isPassive ? '#94a3b8' : '#64748b'
  const label = isSuccess ? 'Success' : isFailed ? 'Failed' : isRunning ? 'Running' : isPassive ? 'Pasif' : 'Bekliyor'
  const Icon = isSuccess ? CheckCircle2 : isFailed ? XCircle : Clock3

  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color, fontWeight: 800, fontSize: 13 }}>
      <Icon size={14} /> {label}
    </span>
  )
}

function formatDate(value?: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleString('tr-TR')
}

function isReportHandler(handlerKey: string) {
  return handlerKey.startsWith('report_')
}

const sectionStyle = {
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: 16,
  marginBottom: 16,
  boxShadow: 'var(--shadow-sm)',
} as const

const inputStyle = {
  width: '100%',
  height: 34,
  padding: '7px 11px',
  border: '1px solid var(--border)',
  borderRadius: 6,
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  outline: 'none',
} as const

const buttonStyle = {
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 6,
  minHeight: 34,
  padding: '7px 12px',
  borderRadius: 6,
  border: '1px solid var(--border)',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  cursor: 'pointer',
  fontWeight: 800,
} as const

const primaryButtonStyle = {
  ...buttonStyle,
  borderColor: 'var(--primary)',
  background: 'var(--primary)',
  color: '#fff',
} as const

const iconButtonStyle = {
  ...buttonStyle,
  width: 34,
  padding: 0,
} as const
