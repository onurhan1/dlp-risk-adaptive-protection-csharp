'use client'

import { useState } from 'react'
import {
  CheckCircle2,
  XCircle,
  Clock,
  AlertTriangle,
  ChevronRight,
  ChevronDown,
  Loader2,
  RefreshCw,
} from 'lucide-react'
import type { PlaybookRun, PlaybookRunStatus } from './types'
import { secondaryButtonStyle } from './formStyles'

interface Props {
  runs: PlaybookRun[]
  loading: boolean
  /** Full run (with node_log) for the row the user expanded. */
  expandedRun: PlaybookRun | null
  onExpand: (runId: number | null) => void
  onRefresh: () => void
}

export const RUN_STATUS_META: Record<PlaybookRunStatus, { label: string; color: string; bg: string }> = {
  running: { label: 'Çalışıyor', color: '#2563eb', bg: 'rgba(37,99,235,0.12)' },
  success: { label: 'Başarılı', color: '#059669', bg: 'rgba(5,150,105,0.12)' },
  failed: { label: 'Başarısız', color: '#dc2626', bg: 'rgba(220,38,38,0.12)' },
  awaiting_approval: { label: 'Onay Bekliyor', color: '#d97706', bg: 'rgba(217,119,6,0.14)' },
}

export function RunStatusBadge({ status }: { status: PlaybookRunStatus }) {
  const meta = RUN_STATUS_META[status] ?? RUN_STATUS_META.success
  const Icon =
    status === 'success' ? CheckCircle2 :
    status === 'failed' ? XCircle :
    status === 'awaiting_approval' ? Clock : Loader2

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '5px',
        padding: '3px 9px',
        borderRadius: '20px',
        fontSize: '11px',
        fontWeight: 600,
        background: meta.bg,
        color: meta.color,
        whiteSpace: 'nowrap',
      }}
    >
      <Icon size={12} /> {meta.label}
    </span>
  )
}

export default function RunHistoryPanel({ runs, loading, expandedRun, onExpand, onRefresh }: Props) {
  const [openId, setOpenId] = useState<number | null>(null)

  const toggle = (runId: number) => {
    const next = openId === runId ? null : runId
    setOpenId(next)
    onExpand(next)
  }

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
        <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
          {loading ? 'Yükleniyor...' : `${runs.length} çalıştırma kaydı`}
        </div>
        <button onClick={onRefresh} disabled={loading} style={{ ...secondaryButtonStyle, padding: '6px 12px', fontSize: '12px' }}>
          <RefreshCw size={13} style={{ animation: loading ? 'spin 1s linear infinite' : undefined }} /> Yenile
        </button>
      </div>

      {runs.length === 0 && !loading ? (
        <div style={{ padding: '20px 0', color: 'var(--text-muted)', fontSize: '13px' }}>
          Bu playbook henüz çalıştırılmadı.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {runs.map(run => {
            const isOpen = openId === run.id
            const detail = isOpen ? expandedRun : null

            return (
              <div key={run.id} style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                <div
                  onClick={() => toggle(run.id)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '12px',
                    padding: '10px 12px',
                    cursor: 'pointer',
                  }}
                >
                  <span style={{ color: 'var(--text-muted)', display: 'flex', flexShrink: 0 }}>
                    {isOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </span>

                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-primary)' }}>
                      {new Date(run.started_at).toLocaleString('tr-TR')}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      {run.trigger_type === 'schedule' ? 'Zamanlanmış' : 'Manuel'}
                      {run.dry_run ? ' · prova' : ' · canlı gönderim'}
                      {run.finished_at
                        ? ` · ${Math.max(0, Math.round((new Date(run.finished_at).getTime() - new Date(run.started_at).getTime()) / 1000))} sn`
                        : ''}
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', justifyContent: 'flex-end', flexShrink: 0 }}>
                    <MailCount label="gönderildi" value={run.mails_sent} color="#059669" />
                    <MailCount label="bekliyor" value={run.mails_pending} color="#d97706" />
                    <MailCount label="hata" value={run.mails_failed} color="#dc2626" />
                    <MailCount label="atlandı" value={run.mails_skipped} color="#64748b" />
                  </div>

                  <RunStatusBadge status={run.status} />
                </div>

                {isOpen && (
                  <div style={{ borderTop: '1px solid var(--border)', background: 'var(--surface-hover)', padding: '10px 12px' }}>
                    {run.error_message && (
                      <div
                        style={{
                          display: 'flex',
                          gap: '8px',
                          alignItems: 'flex-start',
                          padding: '8px 10px',
                          borderRadius: '6px',
                          background: '#fee2e2',
                          border: '1px solid #fca5a5',
                          color: '#991b1b',
                          fontSize: '12px',
                          marginBottom: '10px',
                        }}
                      >
                        <AlertTriangle size={14} style={{ flexShrink: 0, marginTop: '1px' }} />
                        <span>{run.error_message}</span>
                      </div>
                    )}

                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px' }}>
                      Adım Logu
                    </div>

                    {!detail ? (
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Yükleniyor...</div>
                    ) : (detail.node_log?.length ?? 0) === 0 ? (
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Adım kaydı yok.</div>
                    ) : (
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '5px' }}>
                        {detail.node_log!.map((step, index) => (
                          <div
                            key={`${step.node_id}-${index}`}
                            style={{
                              display: 'flex',
                              alignItems: 'flex-start',
                              gap: '8px',
                              fontSize: '12px',
                              padding: '6px 8px',
                              borderRadius: '6px',
                              background: 'var(--surface)',
                              border: '1px solid var(--border)',
                            }}
                          >
                            <span
                              style={{
                                width: '7px',
                                height: '7px',
                                borderRadius: '50%',
                                marginTop: '5px',
                                flexShrink: 0,
                                background:
                                  step.status === 'success' ? '#10b981' :
                                  step.status === 'failed' ? '#ef4444' : '#94a3b8',
                              }}
                            />
                            <div style={{ flex: 1, minWidth: 0 }}>
                              <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{step.label}</div>
                              {step.message && (
                                <div style={{ color: 'var(--text-secondary)', marginTop: '2px' }}>{step.message}</div>
                              )}
                            </div>
                            <div style={{ color: 'var(--text-muted)', fontSize: '11px', whiteSpace: 'nowrap', flexShrink: 0 }}>
                              {step.items_in} → {step.items_out} · {step.duration_ms} ms
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

function MailCount({ label, value, color }: { label: string; value: number; color: string }) {
  if (!value) return null
  return (
    <span style={{ fontSize: '11px', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
      <strong style={{ color }}>{value}</strong> {label}
    </span>
  )
}
