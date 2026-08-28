'use client'

import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { RefreshCw, Send, Ban, Eye, X, CheckCircle2, XCircle, Clock, Pencil, Save } from 'lucide-react'
import apiClient from '@/lib/axios'
import GridExport, { type ExportColumn } from '@/components/ui/GridExport'
import Pagination from '@/components/ui/Pagination'
import type { PlaybookMailRow, PlaybookMailStatus } from './types'
import { secondaryButtonStyle } from './formStyles'

interface Props {
  playbookId: number
  playbookName: string
  /** Bumped by the parent after a run so the report refetches. */
  refreshKey: number
  onPendingCountChange?: (count: number) => void
}

const PAGE_SIZE = 20

const STATUS_META: Record<PlaybookMailStatus, { label: string; color: string; bg: string }> = {
  sent: { label: 'Gönderildi', color: '#059669', bg: 'rgba(5,150,105,0.12)' },
  pending: { label: 'Onay Bekliyor', color: '#d97706', bg: 'rgba(217,119,6,0.14)' },
  failed: { label: 'Hata', color: '#dc2626', bg: 'rgba(220,38,38,0.12)' },
  skipped: { label: 'Atlandı', color: '#64748b', bg: 'rgba(100,116,139,0.14)' },
}

/** Columns for the exported file — the "who was queried, when, with what subject" record. */
const EXPORT_COLUMNS: ExportColumn[] = [
  { key: 'created_at', header: 'Tarih', width: 20, formatter: v => (v ? new Date(v).toLocaleString('tr-TR') : '') },
  { key: 'user_email', header: 'Kullanıcı', width: 30 },
  { key: 'full_name', header: 'Ad Soyad', width: 28 },
  { key: 'team', header: 'Takım', width: 22 },
  { key: 'to_email', header: 'Alıcı', width: 30 },
  { key: 'cc_email', header: 'CC', width: 26 },
  { key: 'subject', header: 'Mail Konusu', width: 44 },
  { key: 'template_name', header: 'Sablon', width: 28 },
  { key: 'template_match_reason', header: 'Sablon Eslesmesi', width: 36 },
  { key: 'source_criterion_label', header: 'Kriter', width: 32 },
  { key: 'trigger_count', header: 'Olay Sayısı', width: 12 },
  { key: 'status_label', header: 'Durum', width: 14 },
  { key: 'sent_at', header: 'Gönderim Zamanı', width: 20, formatter: v => (v ? new Date(v).toLocaleString('tr-TR') : '') },
  { key: 'error_message', header: 'Not', width: 36 },
]

export default function PlaybookReport({ playbookId, playbookName, refreshKey, onPendingCountChange }: Props) {
  const [rows, setRows] = useState<PlaybookMailRow[]>([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState<'all' | PlaybookMailStatus>('all')
  const [search, setSearch] = useState('')
  const [templateFilter, setTemplateFilter] = useState('all')
  const [sourceFilter, setSourceFilter] = useState('all')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [reminderOnly, setReminderOnly] = useState(false)
  const [selectionMode, setSelectionMode] = useState(false)
  const [selectedIds, setSelectedIds] = useState<number[]>([])
  const [page, setPage] = useState(1)
  const [busyId, setBusyId] = useState<number | null>(null)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [previewRow, setPreviewRow] = useState<PlaybookMailRow | null>(null)

  const fetchReport = async () => {
    setLoading(true)
    try {
      const res = await apiClient.get(`/api/playbooks/${playbookId}/report`, {
        params: {
          status: statusFilter,
          search: search.trim() || undefined,
          template: templateFilter,
          source: sourceFilter,
          from: fromDate || undefined,
          to: toDate ? `${toDate}T23:59:59.999` : undefined,
          reminder_only: reminderOnly || undefined,
        },
      })
      const fetched: PlaybookMailRow[] = res.data?.rows ?? []
      setRows(fetched)
      onPendingCountChange?.(res.data?.pending ?? 0)
    } catch (e: any) {
      setRows([])
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Rapor alınamadı' })
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchReport()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [playbookId, statusFilter, search, templateFilter, sourceFilter, fromDate, toDate, reminderOnly, refreshKey])

  useEffect(() => {
    setPage(1)
  }, [statusFilter, search, templateFilter, sourceFilter, fromDate, toDate, reminderOnly, refreshKey])

  useEffect(() => {
    setSelectedIds([])
  }, [rows])

  const pendingRuns = useMemo(
    () => Array.from(new Set(rows.filter(r => r.status === 'pending').map(r => r.run_id))),
    [rows]
  )

  // GridExport writes raw field values, so flatten the label columns it needs.
  const exportRows = useMemo(
    () => rows.map(r => ({ ...r, status_label: STATUS_META[r.status]?.label ?? r.status })),
    [rows]
  )

  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE))
  const safePage = Math.min(page, totalPages)
  const pagedRows = rows.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE)
  const selectableRows = rows.filter(row => row.status === 'pending')
  const selectedCount = selectedIds.length
  const templates = useMemo(() => Array.from(new Set(rows.map(row => row.template_name).filter(Boolean) as string[])).sort(), [rows])
  const sources = useMemo(() => Array.from(new Set(rows.map(row => row.source_criterion).filter(Boolean) as string[])).sort(), [rows])

  const approveRun = async (runId: number) => {
    setBusyId(-runId)
    setMessage(null)
    try {
      const res = await apiClient.post(`/api/playbooks/runs/${runId}/approve`)
      setMessage({ type: res.data?.success ? 'success' : 'error', text: res.data?.message || 'Gönderim tamamlandı' })
      await fetchReport()
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Mailler gönderilemedi' })
    } finally {
      setBusyId(null)
    }
  }

  const approveOne = async (row: PlaybookMailRow) => {
    setBusyId(row.id)
    setMessage(null)
    try {
      const res = await apiClient.post(`/api/playbooks/mail-log/${row.id}/approve`)
      setMessage({ type: res.data?.success ? 'success' : 'error', text: res.data?.message || 'Mail gönderildi' })
      await fetchReport()
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Mail gönderilemedi' })
    } finally {
      setBusyId(null)
    }
  }

  const skipOne = async (row: PlaybookMailRow) => {
    setBusyId(row.id)
    setMessage(null)
    try {
      await apiClient.post(`/api/playbooks/mail-log/${row.id}/skip`)
      await fetchReport()
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Kayıt atlanamadı' })
    } finally {
      setBusyId(null)
    }
  }

  const toggleRow = (id: number) => setSelectedIds(current => current.includes(id) ? current.filter(value => value !== id) : [...current, id])
  const toggleAll = () => setSelectedIds(current => current.length === selectableRows.length ? [] : selectableRows.map(row => row.id))
  const bulkAction = async (action: 'approve' | 'skip') => {
    if (selectedIds.length === 0) return
    setBusyId(-999999)
    setMessage(null)
    try {
      const res = await apiClient.post(`/api/playbooks/mail-logs/bulk-${action}`, { ids: selectedIds })
      setMessage({ type: res.data?.success === false ? 'error' : 'success', text: res.data?.message || 'Toplu islem tamamlandi' })
      await fetchReport()
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Toplu islem tamamlanamadi' })
    } finally {
      setBusyId(null)
    }
  }

  const counts = useMemo(() => ({
    sent: rows.filter(r => r.status === 'sent').length,
    pending: rows.filter(r => r.status === 'pending').length,
    failed: rows.filter(r => r.status === 'failed').length,
    skipped: rows.filter(r => r.status === 'skipped').length,
  }), [rows])

  return (
    <div>
      {/* Pending approval banner */}
      {counts.pending > 0 && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
            flexWrap: 'wrap',
            padding: '11px 14px',
            borderRadius: '8px',
            background: 'rgba(217,119,6,0.12)',
            border: '1px solid rgba(217,119,6,0.35)',
            marginBottom: '14px',
          }}
        >
          <Clock size={16} style={{ color: '#d97706', flexShrink: 0 }} />
          <div style={{ flex: 1, minWidth: '200px', fontSize: '13px', color: 'var(--text-primary)' }}>
            <strong>{counts.pending} mail onay bekliyor.</strong> Prova çalıştırması alıcıları hesapladı, gönderim
            sizin onayınızı bekliyor.
          </div>
          {pendingRuns.map(runId => (
            <button
              key={runId}
              onClick={() => approveRun(runId)}
              disabled={busyId !== null}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '7px 14px',
                background: '#d97706',
                color: 'white',
                border: 'none',
                borderRadius: '6px',
                cursor: busyId !== null ? 'not-allowed' : 'pointer',
                fontSize: '12px',
                fontWeight: 600,
                opacity: busyId !== null ? 0.6 : 1,
              }}
            >
              <Send size={13} /> #{runId} Çalıştırmasını Onayla
            </button>
          ))}
        </div>
      )}

      {message && (
        <div
          style={{
            padding: '10px 14px',
            borderRadius: '6px',
            marginBottom: '14px',
            fontSize: '13px',
            background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
            color: message.type === 'success' ? '#166534' : '#991b1b',
            border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`,
          }}
        >
          {message.text}
        </div>
      )}

      {/* Toolbar */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap', marginBottom: '12px' }}>
        <input
          value={search}
          onChange={event => setSearch(event.target.value)}
          placeholder="Kullanici, ad soyad, birim, alici veya konu ara"
          style={{ padding: '7px 11px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px', minWidth: '260px' }}
        />
        <select
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value as any)}
          style={{
            padding: '7px 11px',
            border: '1px solid var(--border)',
            borderRadius: '6px',
            background: 'var(--surface)',
            color: 'var(--text-primary)',
            fontSize: '13px',
          }}
        >
          <option value="all">Tüm durumlar</option>
          <option value="sent">Gönderildi</option>
          <option value="pending">Onay bekliyor</option>
          <option value="failed">Hata</option>
          <option value="skipped">Atlandı</option>
        </select>

        <select value={templateFilter} onChange={event => setTemplateFilter(event.target.value)} style={{ padding: '7px 11px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px' }}>
          <option value="all">Tum sablonlar</option>
          {templates.map(template => <option key={template} value={template}>{template}</option>)}
        </select>
        <select value={sourceFilter} onChange={event => setSourceFilter(event.target.value)} style={{ padding: '7px 11px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px' }}>
          <option value="all">Tum kaynaklar</option>
          {sources.map(source => <option key={source} value={source}>{source}</option>)}
        </select>
        <input type="date" value={fromDate} onChange={event => setFromDate(event.target.value)} title="Baslangic tarihi" aria-label="Baslangic tarihi" style={{ padding: '6px 8px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)', color: 'var(--text-primary)' }} />
        <input type="date" value={toDate} onChange={event => setToDate(event.target.value)} title="Bitis tarihi" aria-label="Bitis tarihi" style={{ padding: '6px 8px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)', color: 'var(--text-primary)' }} />
        <label title="Yalnizca hatirlatma workflow maillerini goster" style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
          <input type="checkbox" checked={reminderOnly} onChange={event => setReminderOnly(event.target.checked)} /> Hatirlatmalar
        </label>

        <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', fontSize: '12px', color: 'var(--text-muted)' }}>
          <span><strong style={{ color: '#059669' }}>{counts.sent}</strong> gönderildi</span>
          <span><strong style={{ color: '#d97706' }}>{counts.pending}</strong> bekliyor</span>
          <span><strong style={{ color: '#dc2626' }}>{counts.failed}</strong> hata</span>
          <span><strong style={{ color: '#64748b' }}>{counts.skipped}</strong> atlandı</span>
        </div>

        <div style={{ marginLeft: 'auto', display: 'flex', gap: '8px' }}>
          <button onClick={() => setSelectionMode(current => !current)} style={{ ...secondaryButtonStyle, padding: '6px 12px', fontSize: '12px' }} title="Toplu gonderme veya atlama icin secim modunu acar">
            {selectionMode ? 'Secimi Kapat' : 'Coklu Secim'}
          </button>
          <button onClick={fetchReport} disabled={loading} style={{ ...secondaryButtonStyle, padding: '6px 12px', fontSize: '12px' }}>
            <RefreshCw size={13} style={{ animation: loading ? 'spin 1s linear infinite' : undefined }} /> Yenile
          </button>
          <GridExport
            data={exportRows}
            columns={EXPORT_COLUMNS}
            fileName={`${playbookName.replace(/[\\/:*?"<>|]/g, '-')} - Sorgu Raporu`}
            disabled={rows.length === 0}
          />
        </div>
      </div>

      {selectionMode && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', padding: '9px 11px', marginBottom: '12px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface-hover)' }}>
          <button onClick={toggleAll} disabled={selectableRows.length === 0 || busyId !== null} style={{ ...secondaryButtonStyle, padding: '6px 10px', fontSize: '12px' }}>
            {selectedCount === selectableRows.length && selectableRows.length > 0 ? 'Secimi Temizle' : 'Tum Filtreleneni Sec'}
          </button>
          <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{selectedCount} onay bekleyen mail secildi</span>
          <button onClick={() => bulkAction('approve')} disabled={selectedCount === 0 || busyId !== null} style={{ ...secondaryButtonStyle, padding: '6px 10px', fontSize: '12px', color: '#047857' }} title="Secilen mailleri gonderir">
            <Send size={13} /> Secilenleri Gonder
          </button>
          <button onClick={() => bulkAction('skip')} disabled={selectedCount === 0 || busyId !== null} style={{ ...secondaryButtonStyle, padding: '6px 10px', fontSize: '12px', color: '#b91c1c' }} title="Secilen mailleri silmeden Atlandi durumuna alir">
            <Ban size={13} /> Secilenleri Atla
          </button>
        </div>
      )}

      {/* Table */}
      {loading ? (
        <div style={{ padding: '20px 0', color: 'var(--text-muted)', fontSize: '13px' }}>Yükleniyor...</div>
      ) : rows.length === 0 ? (
        <div style={{ padding: '20px 0', color: 'var(--text-muted)', fontSize: '13px' }}>
          Bu workflow için henüz gönderim kaydı yok. "Prova Çalıştır" ile başlayın.
        </div>
      ) : (
        <>
          <div style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: '8px' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', minWidth: '900px' }}>
              <thead>
                <tr style={{ background: 'var(--surface-hover)' }}>
                  {selectionMode && <Th><input type="checkbox" checked={selectableRows.length > 0 && selectedCount === selectableRows.length} onChange={toggleAll} title="Tum filtreleneni sec" aria-label="Tum filtreleneni sec" /></Th>}
                  <Th>Tarih</Th>
                  <Th>Kullanıcı</Th>
                  <Th>Ad Soyad</Th>
                  <Th>Alıcı</Th>
                  <Th>Mail Konusu</Th>
                  <Th>Sablon</Th>
                  <Th>Kriter</Th>
                  <Th align="right">Olay</Th>
                  <Th>Durum</Th>
                  <Th align="right">İşlem</Th>
                </tr>
              </thead>
              <tbody>
                {pagedRows.map(row => {
                  const meta = STATUS_META[row.status] ?? STATUS_META.skipped
                  const busy = busyId === row.id

                  return (
                    <tr key={row.id} style={{ borderTop: '1px solid var(--border)' }}>
                      {selectionMode && (
                        <Td>
                          {row.status === 'pending' && <input type="checkbox" checked={selectedIds.includes(row.id)} onChange={() => toggleRow(row.id)} aria-label={`${row.user_email} mailini sec`} />}
                        </Td>
                      )}
                      <Td nowrap>{new Date(row.created_at).toLocaleString('tr-TR')}</Td>
                      <Td>
                        <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{row.user_email}</div>
                        {row.team && <div style={{ color: 'var(--text-muted)', fontSize: '11px' }}>{row.team}</div>}
                      </Td>
                      <Td>{row.full_name || '-'}</Td>
                      <Td>
                        <div>{row.to_email}</div>
                        {row.cc_email && <div style={{ color: 'var(--text-muted)', fontSize: '11px' }}>CC: {row.cc_email}</div>}
                      </Td>
                      <Td>{row.subject}</Td>
                      <Td>
                        <div title={row.template_match_reason ?? undefined}>{row.template_name ?? '-'}</div>
                        {row.template_match_reason && (
                          <div style={{ color: 'var(--text-muted)', fontSize: '11px', marginTop: '2px' }}>
                            {row.template_match_reason}
                          </div>
                        )}
                      </Td>
                      <Td>{row.source_criterion_label ?? '-'}</Td>
                      <Td align="right">{row.trigger_count}</Td>
                      <Td nowrap>
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '4px',
                            padding: '2px 8px',
                            borderRadius: '20px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background: meta.bg,
                            color: meta.color,
                          }}
                          title={row.error_message ?? undefined}
                        >
                          {row.status === 'sent' ? <CheckCircle2 size={11} /> :
                            row.status === 'failed' ? <XCircle size={11} /> :
                              row.status === 'pending' ? <Clock size={11} /> : <Ban size={11} />}
                          {meta.label}
                        </span>
                        {row.sent_at && (
                          <div style={{ color: 'var(--text-muted)', fontSize: '10px', marginTop: '2px' }}>
                            {new Date(row.sent_at).toLocaleString('tr-TR')}
                          </div>
                        )}
                      </Td>
                      <Td align="right" nowrap>
                        <div style={{ display: 'inline-flex', gap: '5px' }}>
                          <IconAction title="Mail içeriğini gör" onClick={() => setPreviewRow(row)}>
                            <Eye size={13} />
                          </IconAction>
                          {row.status === 'pending' && (
                            <>
                              <IconAction title="Onayla ve gönder" disabled={busy} onClick={() => approveOne(row)} accent="#059669">
                                <Send size={13} />
                              </IconAction>
                              <IconAction title="Atla" disabled={busy} onClick={() => skipOne(row)} accent="#dc2626">
                                <Ban size={13} />
                              </IconAction>
                            </>
                          )}
                        </div>
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div style={{ marginTop: '12px' }}>
              <Pagination
                currentPage={safePage}
                totalPages={totalPages}
                totalItems={rows.length}
                pageSize={PAGE_SIZE}
                onPageChange={setPage}
              />
            </div>
          )}
        </>
      )}

      {previewRow && (
        <EditableMailPreviewModal
          row={previewRow}
          onClose={() => setPreviewRow(null)}
          onSaved={async () => {
            await fetchReport()
            setPreviewRow(null)
          }}
        />
      )}
    </div>
  )
}

function MailPreviewModal({ row, onClose }: { row: PlaybookMailRow; onClose: () => void }) {
  const incident = parseIncidentSummary(row.incident_summary_json)

  return (
    <div
      onClick={onClose}
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(15,23,42,0.55)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
        padding: '20px',
      }}
    >
      <div
        onClick={e => e.stopPropagation()}
        style={{
          background: 'var(--surface)',
          borderRadius: '14px',
          width: '100%',
          maxWidth: '620px',
          maxHeight: '88vh',
          overflowY: 'auto',
          boxShadow: '0 12px 40px rgba(15,23,42,0.25)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
          <h2 style={{ margin: 0, fontSize: '16px', fontWeight: 600, color: 'var(--text-primary)' }}>Mail Kaydı</h2>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-muted)' }}>
            <X size={19} />
          </button>
        </div>

        <div style={{ padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
          <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
            <PreviewRow label="Kullanıcı" value={`${row.user_email}${row.team ? ` · ${row.team}` : ''}`} />
            <PreviewRow label="Alıcı" value={row.to_email} />
            {row.cc_email && <PreviewRow label="CC" value={row.cc_email} />}
            <PreviewRow label="Konu" value={row.subject} />
            <PreviewRow label="Sablon" value={row.template_name ?? 'Node icerigi'} />
            <PreviewRow label="Eslesme" value={row.template_match_reason ?? 'Kayitli eslesme bilgisi yok'} />
            <PreviewRow label="Kriter" value={row.source_criterion_label ?? '-'} />
            <PreviewRow label="Oluşturma" value={new Date(row.created_at).toLocaleString('tr-TR')} />
            <PreviewRow
              label="Gönderim"
              value={row.sent_at ? new Date(row.sent_at).toLocaleString('tr-TR') : 'Gönderilmedi'}
              last={!row.error_message}
            />
            {row.error_message && <PreviewRow label="Not" value={row.error_message} last />}
          </div>

          {incident && (
            <div>
              <div style={{ fontSize: '12px', fontWeight: 500, color: 'var(--text-primary)', marginBottom: '5px' }}>Maili Doguran Olay</div>
              <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                <PreviewRow label="Olay tarihi" value={formatIncidentDate(incident.timestamp)} />
                <PreviewRow label="Hedef" value={displayValue(incident.destination)} />
                <PreviewRow label="Aksiyon" value={displayValue(incident.action)} />
                <PreviewRow label="Kanal" value={displayValue(incident.channel)} />
                <PreviewRow label="Politika" value={displayValue(incident.policy)} />
                <PreviewRow label="Max Match" value={displayValue(incident.max_matches)} />
                <PreviewRow label="Veri tipi" value={displayValue(incident.data_type)} />
                <PreviewRow label="Siddet" value={displayValue(incident.severity)} />
                <PreviewRow label="Risk skoru" value={displayValue(incident.risk_score)} last />
              </div>
            </div>
          )}

          <div>
            <div style={{ fontSize: '12px', fontWeight: 500, color: 'var(--text-primary)', marginBottom: '5px' }}>İçerik</div>
            <div
              style={{
                border: '1px solid var(--border)',
                borderRadius: '8px',
                padding: '14px 16px',
                background: 'white',
                color: '#0f172a',
                fontSize: '13px',
                maxHeight: '340px',
                overflowY: 'auto',
                wordBreak: 'break-word',
              }}
              dangerouslySetInnerHTML={{ __html: row.body_html || '<em style="color:#94a3b8">İçerik boş</em>' }}
            />
          </div>
        </div>
      </div>
    </div>
  )
}

function EditableMailPreviewModal({ row, onClose, onSaved }: { row: PlaybookMailRow; onClose: () => void; onSaved: () => Promise<void> }) {
  const canEdit = row.status === 'pending'
  const [editing, setEditing] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [form, setForm] = useState({
    toEmail: row.to_email,
    ccEmail: row.cc_email ?? '',
    fullName: row.full_name ?? '',
    subject: row.subject,
    bodyHtml: row.body_html,
  })

  const saveChanges = async () => {
    setSaving(true)
    setError(null)
    try {
      await apiClient.put(`/api/playbooks/mail-log/${row.id}`, {
        to_email: form.toEmail,
        cc_email: form.ccEmail,
        full_name: form.fullName,
        subject: form.subject,
        body_html: form.bodyHtml,
      })
      await onSaved()
    } catch (e: any) {
      setError(e?.response?.data?.detail || 'Mail kaydi guncellenemedi')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div onClick={onClose} style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.55)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '20px' }}>
      <div onClick={event => event.stopPropagation()} style={{ background: 'var(--surface)', borderRadius: '14px', width: '100%', maxWidth: '700px', maxHeight: '88vh', overflowY: 'auto', boxShadow: '0 12px 40px rgba(15,23,42,0.25)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
          <h2 style={{ margin: 0, fontSize: '16px', fontWeight: 600, color: 'var(--text-primary)' }}>Mail Onizleme</h2>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            {canEdit && !editing && <button onClick={() => setEditing(true)} style={{ ...secondaryButtonStyle, padding: '6px 10px', fontSize: '12px' }}><Pencil size={13} /> Duzenle</button>}
            <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-muted)' }}><X size={19} /></button>
          </div>
        </div>

        <div style={{ padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
          {editing && <div style={{ padding: '10px 12px', border: '1px solid rgba(217,119,6,0.35)', borderRadius: '8px', background: 'rgba(217,119,6,0.08)', fontSize: '12px' }}>Bu mail henuz gonderilmedi. Onaydan once alici, konu ve icerigi duzenleyebilirsiniz.</div>}
          <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
            <PreviewRow label="Kullanici" value={`${row.user_email}${row.team ? ` - ${row.team}` : ''}`} />
            {editing ? (
              <>
                <EditablePreviewField label="Alici" value={form.toEmail} type="email" onChange={value => setForm(current => ({ ...current, toEmail: value }))} />
                <EditablePreviewField label="CC" value={form.ccEmail} type="email" onChange={value => setForm(current => ({ ...current, ccEmail: value }))} />
                <EditablePreviewField label="Ad Soyad" value={form.fullName} onChange={value => setForm(current => ({ ...current, fullName: value }))} />
                <EditablePreviewField label="Konu" value={form.subject} onChange={value => setForm(current => ({ ...current, subject: value }))} />
              </>
            ) : (
              <>
                <PreviewRow label="Alici" value={row.to_email} />
                {row.cc_email && <PreviewRow label="CC" value={row.cc_email} />}
                <PreviewRow label="Konu" value={row.subject} />
              </>
            )}
            <PreviewRow label="Sablon" value={row.template_name ?? 'Node icerigi'} />
            <PreviewRow label="Kriter" value={row.source_criterion_label ?? '-'} />
            {row.has_pdf_attachment && <PreviewRow label="PDF Eki" value={row.pdf_attachment_file_name ?? 'workflow_raporu.pdf'} last />}
            {!row.has_pdf_attachment && <PreviewRow label="PDF Eki" value="Yok" last />}
          </div>

          {parseIncidentSummary(row.incident_summary_json) && (() => {
            const incident = parseIncidentSummary(row.incident_summary_json)!
            return <div>
              <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-primary)', marginBottom: '5px' }}>Maili Doguran Olay</div>
              <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                <PreviewRow label="Olay tarihi" value={formatIncidentDate(incident.timestamp)} />
                <PreviewRow label="Hedef" value={displayValue(incident.destination)} />
                <PreviewRow label="Aksiyon" value={displayValue(incident.action)} />
                <PreviewRow label="Kanal" value={displayValue(incident.channel)} />
                <PreviewRow label="Politika" value={displayValue(incident.policy)} />
                <PreviewRow label="Max Match" value={displayValue(incident.max_matches)} last />
              </div>
            </div>
          })()}

          <div>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-primary)', marginBottom: '5px' }}>Icerik</div>
            {editing ? (
              <textarea value={form.bodyHtml} onChange={event => setForm(current => ({ ...current, bodyHtml: event.target.value }))} style={{ width: '100%', minHeight: '260px', resize: 'vertical', boxSizing: 'border-box', padding: '12px', border: '1px solid var(--border)', borderRadius: '8px', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px', lineHeight: 1.55 }} />
            ) : (
              <div style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '14px 16px', background: 'white', color: '#0f172a', fontSize: '13px', maxHeight: '340px', overflowY: 'auto', wordBreak: 'break-word' }} dangerouslySetInnerHTML={{ __html: row.body_html || '<em style="color:#94a3b8">Icerik bos</em>' }} />
            )}
          </div>

          {editing && <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '8px', flexWrap: 'wrap' }}>
            {error && <span style={{ marginRight: 'auto', color: '#b91c1c', fontSize: '12px' }}>{error}</span>}
            <button onClick={() => { setEditing(false); setError(null) }} disabled={saving} style={{ ...secondaryButtonStyle, padding: '7px 12px', fontSize: '12px' }}>Vazgec</button>
            <button onClick={saveChanges} disabled={saving} style={{ ...secondaryButtonStyle, padding: '7px 12px', fontSize: '12px', color: '#047857' }}><Save size={13} /> {saving ? 'Kaydediliyor...' : 'Degisiklikleri Kaydet'}</button>
          </div>}
          {!canEdit && <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Gonderilmis veya atlanmis mail kayitlari denetim izi icin salt okunurdur.</div>}
        </div>
      </div>
    </div>
  )
}

function EditablePreviewField({ label, value, type = 'text', onChange }: { label: string; value: string; type?: string; onChange: (value: string) => void }) {
  return <label style={{ display: 'flex', alignItems: 'center', gap: '12px', padding: '7px 10px', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
    <span style={{ width: '80px', flexShrink: 0, fontWeight: 600, color: 'var(--text-muted)' }}>{label}</span>
    <input type={type} value={value} onChange={event => onChange(event.target.value)} style={{ flex: 1, minWidth: 0, padding: '6px 8px', border: '1px solid var(--border)', borderRadius: '5px', background: 'var(--surface)', color: 'var(--text-primary)' }} />
  </label>
}

type IncidentSummary = Record<string, string | number | null | undefined>

function parseIncidentSummary(value?: string | null): IncidentSummary | null {
  if (!value) return null
  try {
    const parsed = JSON.parse(value)
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : null
  } catch {
    return null
  }
}

function displayValue(value: unknown): string {
  return value === null || value === undefined || value === '' ? '-' : String(value)
}

function formatIncidentDate(value: unknown): string {
  if (!value) return '-'
  const parsed = new Date(String(value))
  return Number.isNaN(parsed.getTime()) ? String(value) : parsed.toLocaleString('tr-TR')
}

function PreviewRow({ label, value, last }: { label: string; value: string; last?: boolean }) {
  return (
    <div style={{ display: 'flex', gap: '12px', padding: '9px 13px', borderBottom: last ? 'none' : '1px solid var(--border)', fontSize: '12px' }}>
      <span style={{ width: '80px', flexShrink: 0, fontWeight: 600, color: 'var(--text-muted)' }}>{label}</span>
      <span style={{ color: 'var(--text-primary)', wordBreak: 'break-word' }}>{value}</span>
    </div>
  )
}

function Th({ children, align = 'left' }: { children: ReactNode; align?: 'left' | 'right' }) {
  return (
    <th
      style={{
        padding: '9px 11px',
        textAlign: align,
        fontSize: '11px',
        fontWeight: 600,
        color: 'var(--text-secondary)',
        whiteSpace: 'nowrap',
      }}
    >
      {children}
    </th>
  )
}

function Td({
  children,
  align = 'left',
  nowrap,
}: {
  children: ReactNode
  align?: 'left' | 'right'
  nowrap?: boolean
}) {
  return (
    <td
      style={{
        padding: '9px 11px',
        textAlign: align,
        color: 'var(--text-secondary)',
        verticalAlign: 'top',
        whiteSpace: nowrap ? 'nowrap' : undefined,
      }}
    >
      {children}
    </td>
  )
}

function IconAction({
  title,
  onClick,
  children,
  disabled,
  accent,
}: {
  title: string
  onClick: () => void
  children: ReactNode
  disabled?: boolean
  accent?: string
}) {
  return (
    <button
      title={title}
      onClick={onClick}
      disabled={disabled}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '26px',
        height: '26px',
        background: 'var(--surface)',
        border: '1px solid var(--border)',
        borderRadius: '5px',
        color: accent ?? 'var(--text-muted)',
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.5 : 1,
      }}
    >
      {children}
    </button>
  )
}
