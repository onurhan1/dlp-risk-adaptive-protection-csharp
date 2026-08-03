'use client'

import { Suspense, useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import {
  Workflow,
  Plus,
  RefreshCw,
  Clock,
  Trash2,
  Settings2,
  CalendarClock,
  MousePointerClick,
  BarChart3,
} from 'lucide-react'
import apiClient from '@/lib/axios'
import { RunStatusBadge } from '@/components/investigation/playbook/RunHistoryPanel'
import {
  createStarterGraph,
  createIncidentMetricGraph,
  type PlaybookGraph,
  type PlaybookSummary,
} from '@/components/investigation/playbook/types'
import { primaryButtonStyle, secondaryButtonStyle, disabled as withDisabled } from '@/components/investigation/playbook/formStyles'

export default function PlaybooksPage() {
  return (
    <Suspense fallback={<div className="dashboard-page"><p className="text-muted">Yükleniyor...</p></div>}>
      <PlaybooksPageContent />
    </Suspense>
  )
}

function PlaybooksPageContent() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const [playbooks, setPlaybooks] = useState<PlaybookSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [creating, setCreating] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const fetchPlaybooks = useCallback(async () => {
    setLoading(true)
    try {
      const res = await apiClient.get('/api/playbooks')
      setPlaybooks(Array.isArray(res.data) ? res.data : [])
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Agentic Workflow listesi alınamadı' })
      setPlaybooks([])
    } finally {
      setLoading(false)
    }
  }, [])

  const create = useCallback(async (name: string, description: string, graph: PlaybookGraph) => {
    setCreating(true)
    setMessage(null)
    try {
      const res = await apiClient.post('/api/playbooks', {
        name,
        description,
        graph,
        enabled: false,
        auto_send: false,
      })
      router.push(`/investigation/agentic-workflows/${res.data.id}`)
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Agentic Workflow oluşturulamadı' })
      setCreating(false)
    }
  }, [router])

  const createPlaybook = useCallback((name: string, criteria?: string[]) =>
    create(
      name,
      'Haftalık Sorgu kullanıcılarını listeler, mail hazırlar ve raporlar.',
      createStarterGraph(criteria)
    ), [create])

  const createMetricPlaybook = useCallback(() =>
    create(
      'Incident Sayısı Eşik Uyarısı',
      'Haftalık incident sayısını ölçer, eşik aşılırsa özet mail gönderir ve raporlar.',
      createIncidentMetricGraph()
    ), [create])

  useEffect(() => {
    fetchPlaybooks()
  }, [fetchPlaybooks])

  // Deep link from the Weekly Review page: ?from_criterion=high_volume creates a
  // pre-filled playbook for that criterion and jumps straight into the editor.
  useEffect(() => {
    const criterion = searchParams.get('from_criterion')
    if (!criterion) return
    router.replace('/investigation/agentic-workflows')
    createPlaybook('Haftalık Sorgu Akışı', [criterion])
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams])

  const toggleEnabled = async (playbook: PlaybookSummary) => {
    setMessage(null)
    try {
      await apiClient.post(`/api/playbooks/${playbook.id}/toggle`)
      await fetchPlaybooks()
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Durum değiştirilemedi' })
    }
  }

  const remove = async (playbook: PlaybookSummary) => {
    if (!window.confirm(`"${playbook.name}" agentic workflow silinsin mi? Gönderim kayıtları raporda kalmaya devam eder.`)) return
    setMessage(null)
    try {
      await apiClient.delete(`/api/playbooks/${playbook.id}`)
      await fetchPlaybooks()
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Agentic Workflow silinemedi' })
    }
  }

  return (
    <div className="dashboard-page">
      <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '16px', flexWrap: 'wrap' }}>
        <div>
          <h1>Agentic Workflow</h1>
          <p className="text-muted">
            Soruşturma akışlarını node'larla kurun, zamanlayın ve gönderim raporunu alın
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
          <button onClick={fetchPlaybooks} disabled={loading} style={withDisabled(secondaryButtonStyle, loading)}>
            <RefreshCw size={15} style={{ animation: loading ? 'spin 1s linear infinite' : undefined }} /> Yenile
          </button>
          <button
            onClick={createMetricPlaybook}
            disabled={creating}
            title="Haftalık incident sayısını ölçen, eşik aşılırsa mail atan hazır akış"
            style={withDisabled(secondaryButtonStyle, creating)}
          >
            <BarChart3 size={15} /> Incident Eşik Akışı
          </button>
          <button
            onClick={() => createPlaybook('Haftalık Sorgu Akışı')}
            disabled={creating}
            style={withDisabled(primaryButtonStyle, creating)}
          >
            <Plus size={15} /> {creating ? 'Oluşturuluyor...' : 'Yeni Workflow'}
          </button>
        </div>
      </div>

      {message && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '8px',
            marginBottom: '20px',
            background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
            color: message.type === 'success' ? '#166534' : '#991b1b',
            border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`,
          }}
        >
          {message.text}
        </div>
      )}

      {loading ? (
        <div className="card"><p className="text-muted" style={{ margin: 0 }}>Yükleniyor...</p></div>
      ) : playbooks.length === 0 ? (
        <div className="card" style={{ textAlign: 'center', padding: '48px 24px' }}>
          <div
            style={{
              width: '52px',
              height: '52px',
              borderRadius: '14px',
              background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              marginBottom: '14px',
            }}
          >
            <Workflow size={26} />
          </div>
          <h2 style={{ margin: '0 0 6px', fontSize: '17px', fontWeight: 600, color: 'var(--text-primary)' }}>
            Henüz agentic workflow yok
          </h2>
          <p className="text-muted" style={{ margin: '0 auto 18px', maxWidth: '460px', fontSize: '13px' }}>
            Yeni bir workflow, Haftalık Sorgu zincirini hazır node'larla kurar:
            Zamanlama → Haftalık Sorgu Kaynağı → Filtre → Mail Gönder → Rapor Çıktısı.
          </p>
          <button
            onClick={() => createPlaybook('Haftalık Sorgu Akışı')}
            disabled={creating}
            style={withDisabled(primaryButtonStyle, creating)}
          >
            <Plus size={15} /> {creating ? 'Oluşturuluyor...' : 'İlk Workflow\'u Oluştur'}
          </button>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))', gap: '16px' }}>
          {playbooks.map(playbook => (
            <div key={playbook.id} className="card" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: '12px' }}>
                <div
                  style={{
                    width: '38px',
                    height: '38px',
                    borderRadius: '10px',
                    background: playbook.enabled
                      ? 'linear-gradient(135deg, #6366f1, #8b5cf6)'
                      : 'linear-gradient(135deg, #94a3b8, #64748b)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: 'white',
                    flexShrink: 0,
                  }}
                >
                  <Workflow size={19} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <h2 style={{ margin: 0, fontSize: '15px', fontWeight: 600, color: 'var(--text-primary)', wordBreak: 'break-word' }}>
                    {playbook.name}
                  </h2>
                  {playbook.description && (
                    <p style={{ margin: '3px 0 0', fontSize: '12px', color: 'var(--text-muted)', lineHeight: 1.4 }}>
                      {playbook.description}
                    </p>
                  )}
                </div>
                {playbook.last_run && <RunStatusBadge status={playbook.last_run.status} />}
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '5px', fontSize: '12px', color: 'var(--text-secondary)' }}>
                <InfoLine
                  icon={playbook.schedule_cron ? <CalendarClock size={13} /> : <MousePointerClick size={13} />}
                  text={playbook.schedule_cron ? playbook.schedule_summary : 'Zamanlama yok — elle çalıştırılır'}
                />
                {playbook.next_run_at && (
                  <InfoLine icon={<Clock size={13} />} text={`Sıradaki: ${new Date(playbook.next_run_at).toLocaleString('tr-TR')}`} />
                )}
                {playbook.last_run_at && (
                  <InfoLine icon={<Clock size={13} />} text={`Son çalıştırma: ${new Date(playbook.last_run_at).toLocaleString('tr-TR')}`} />
                )}
              </div>

              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'center' }}>
                <span
                  style={{
                    fontSize: '11px',
                    fontWeight: 600,
                    padding: '3px 9px',
                    borderRadius: '20px',
                    background: playbook.auto_send ? 'rgba(220,38,38,0.12)' : 'rgba(5,150,105,0.12)',
                    color: playbook.auto_send ? '#dc2626' : '#059669',
                  }}
                >
                  {playbook.auto_send ? 'Otomatik gönderim açık' : 'Prova + onay'}
                </span>
                {playbook.pending_mails > 0 && (
                  <span
                    style={{
                      fontSize: '11px',
                      fontWeight: 600,
                      padding: '3px 9px',
                      borderRadius: '20px',
                      background: 'rgba(217,119,6,0.14)',
                      color: '#d97706',
                    }}
                  >
                    {playbook.pending_mails} mail onay bekliyor
                  </span>
                )}
              </div>

              <div style={{ display: 'flex', gap: '8px', marginTop: 'auto', flexWrap: 'wrap' }}>
                <button
                  onClick={() => router.push(`/investigation/agentic-workflows/${playbook.id}`)}
                  style={{ ...primaryButtonStyle, flex: 1, justifyContent: 'center' }}
                >
                  <Settings2 size={14} /> Akışı Düzenle
                </button>
                <button
                  onClick={() => toggleEnabled(playbook)}
                  title={playbook.enabled ? 'Zamanlamayı durdur' : 'Zamanlamayı başlat'}
                  style={{
                    ...secondaryButtonStyle,
                    color: playbook.enabled ? '#dc2626' : '#059669',
                    borderColor: playbook.enabled ? '#fca5a5' : '#86efac',
                  }}
                >
                  {playbook.enabled ? 'Durdur' : 'Başlat'}
                </button>
                <button onClick={() => remove(playbook)} title="Sil" style={{ ...secondaryButtonStyle, color: '#dc2626' }}>
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function InfoLine({ icon, text }: { icon: ReactNode; text: string }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
      <span style={{ color: 'var(--text-muted)', display: 'flex', flexShrink: 0 }}>{icon}</span>
      <span style={{ wordBreak: 'break-word' }}>{text}</span>
    </div>
  )
}
