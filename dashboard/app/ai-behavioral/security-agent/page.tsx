'use client'

import { FormEvent, useCallback, useEffect, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { Bot, Loader2, RefreshCw, Send, ShieldCheck, Sparkles, Workflow } from 'lucide-react'
import apiClient from '@/lib/axios'

type ChatMessage = { role: 'user' | 'assistant'; content: string }
type Count = { name: string; count: number }
type AgentContext = {
  startUtc: string
  endUtc: string
  workflowCount: number
  enabledWorkflowCount: number
  incidentCount: number
  uniqueUsers: number
  nodeTypes: Count[]
  workflows: Array<{ id: number; name: string; enabled: boolean; autoSend: boolean; schedule?: string | null; nodes: string[]; validationErrors: string[]; lastRunStatus?: string | null; pendingMails: number; failedMails: number }>
}

function normalizeContext(data: any): AgentContext {
  const workflows = Array.isArray(data?.workflows) ? data.workflows.map((workflow: any) => ({
    id: workflow.id,
    name: workflow.name ?? 'Adsız workflow',
    enabled: Boolean(workflow.enabled),
    autoSend: Boolean(workflow.autoSend ?? workflow.auto_send),
    schedule: workflow.schedule ?? null,
    nodes: Array.isArray(workflow.nodes) ? workflow.nodes : [],
    validationErrors: Array.isArray(workflow.validationErrors) ? workflow.validationErrors : Array.isArray(workflow.validation_errors) ? workflow.validation_errors : [],
    lastRunStatus: workflow.lastRunStatus ?? workflow.last_run_status ?? null,
    pendingMails: Number(workflow.pendingMails ?? workflow.pending_mails ?? 0),
    failedMails: Number(workflow.failedMails ?? workflow.failed_mails ?? 0),
  })) : []

  return {
    startUtc: data?.startUtc ?? data?.start_utc ?? '',
    endUtc: data?.endUtc ?? data?.end_utc ?? '',
    workflowCount: Number(data?.workflowCount ?? data?.workflow_count ?? workflows.length),
    enabledWorkflowCount: Number(data?.enabledWorkflowCount ?? data?.enabled_workflow_count ?? 0),
    incidentCount: Number(data?.incidentCount ?? data?.incident_count ?? 0),
    uniqueUsers: Number(data?.uniqueUsers ?? data?.unique_users ?? 0),
    nodeTypes: Array.isArray(data?.nodeTypes) ? data.nodeTypes : Array.isArray(data?.node_types) ? data.node_types : [],
    workflows,
  }
}

const STARTER = 'Aktif workflowlarımı ve son 30 günlük olay dağılımını incele. Kapsama boşluğu olabilecek en önemli üç alanı; kanıtı, olası riski ve eklemem gereken workflow veya node önerisiyle sırala.'

export default function SecurityAgentPage() {
  const router = useRouter()
  const [context, setContext] = useState<AgentContext | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [startDate, setStartDate] = useState(() => new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 16))
  const [endDate, setEndDate] = useState(() => new Date().toISOString().slice(0, 16))
  const [loadingContext, setLoadingContext] = useState(true)
  const [sending, setSending] = useState(false)
  const [creatingDraft, setCreatingDraft] = useState(false)
  const [draftNotice, setDraftNotice] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const bottom = useRef<HTMLDivElement>(null)

  const loadContext = useCallback(async () => {
    setLoadingContext(true)
    setError(null)
    try {
      const { data } = await apiClient.get('/api/security-agent/context', { params: { startUtc: new Date(startDate).toISOString(), endUtc: new Date(endDate).toISOString() }, timeout: 60_000 })
      setContext(normalizeContext(data))
    } catch (requestError: any) {
      setError(requestError?.response?.data?.detail || 'Agent bağlamı alınamadı.')
    } finally {
      setLoadingContext(false)
    }
  }, [endDate, startDate])

  useEffect(() => {
    void loadContext()
  // Initial context uses the default last-30-day range. Subsequent date edits wait for explicit confirmation.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  useEffect(() => { bottom.current?.scrollIntoView({ behavior: 'smooth', block: 'end' }) }, [messages, sending])

  const send = async (value = input) => {
    const message = value.trim()
    if (!message || sending) return
    const history = messages
    setMessages(current => [...current, { role: 'user', content: message }])
    setInput('')
    setSending(true)
    setError(null)
    try {
      const { data } = await apiClient.post('/api/security-agent/chat', { message, history, startUtc: new Date(startDate).toISOString(), endUtc: new Date(endDate).toISOString() }, { timeout: 600_000 })
      setMessages(current => [...current, { role: 'assistant', content: data.reply }])
      setContext(normalizeContext(data.context))
    } catch (requestError: any) {
      setError(requestError?.response?.data?.detail || 'Agent yanıtı alınamadı.')
    } finally {
      setSending(false)
    }
  }

  const submit = (event: FormEvent) => { event.preventDefault(); void send() }
  const createDraft = async () => {
    const goal = [...messages].reverse().find(message => message.role === 'user')?.content || input.trim()
    if (!goal) {
      setError('Önce analiz etmek istediğiniz senaryoyu yazın veya Agent ile konuşun.')
      return
    }
    setCreatingDraft(true)
    setError(null)
    setDraftNotice(null)
    try {
      const { data } = await apiClient.post('/api/security-agent/workflow-drafts', { goal, startUtc: new Date(startDate).toISOString(), endUtc: new Date(endDate).toISOString() }, { timeout: 600_000 })
      setDraftNotice('Pasif workflow taslağı oluşturuldu. Editörde filtre ve eşikleri tamamlayın.')
      router.push(`/investigation/agentic-workflows/${data.playbookId ?? data.playbook_id}`)
    } catch (requestError: any) {
      setError(requestError?.response?.data?.detail || 'Workflow taslağı oluşturulamadı.')
    } finally {
      setCreatingDraft(false)
    }
  }
  const fmt = (value?: number) => (value ?? 0).toLocaleString('tr-TR')

  return <main className="container" style={{ maxWidth: 1480, paddingTop: 24, paddingBottom: 36 }}>
    <header style={{ display: 'flex', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap', alignItems: 'flex-start', marginBottom: 18 }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}><ShieldCheck size={24} color="#047857" /><h1 style={{ margin: 0, fontSize: 24, letterSpacing: 0 }}>Güvenlik Agentı</h1></div>
        <p style={{ margin: '7px 0 0', color: 'var(--text-secondary)', fontSize: 13 }}>Workflow kapsamasını ve seçtiğiniz dönemin olay dağılımını salt-okunur inceler; gözden kaçabilecek güvenlik senaryoları için kanıta dayalı öneri üretir.</p>
      </div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'end', flexWrap: 'wrap' }}>
        <label style={dateLabel}>Başlangıç<input type="datetime-local" value={startDate} onChange={event => setStartDate(event.target.value)} style={dateInput} /></label>
        <label style={dateLabel}>Bitiş<input type="datetime-local" value={endDate} onChange={event => setEndDate(event.target.value)} style={dateInput} /></label>
        <button onClick={() => void loadContext()} disabled={loadingContext} style={secondaryButton}><RefreshCw size={15} style={{ animation: loadingContext ? 'spin 1s linear infinite' : undefined }} /> Dönemi İncele</button>
      </div>
    </header>
    {error && <div style={{ marginBottom: 14, padding: 12, borderRadius: 6, background: '#fee2e2', color: '#b91c1c', fontSize: 13 }}>{error}</div>}
    {draftNotice && <div style={{ marginBottom: 14, padding: 12, borderRadius: 6, background: '#dcfce7', color: '#15803d', fontSize: 13 }}>{draftNotice}</div>}

    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(300px, 365px)', gap: 16, alignItems: 'start' }}>
      <section style={panelStyle}>
        <div style={{ padding: '16px 18px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}><strong style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Bot size={18} color="#2563eb" /> Agent Sohbeti</strong><span style={{ color: '#047857', fontSize: 12, fontWeight: 700 }}>Salt-okunur</span></div>
        <div style={{ height: 480, overflowY: 'auto', padding: 18, background: 'var(--background)' }}>
          {messages.length === 0 && <div style={{ height: '100%', display: 'grid', placeItems: 'center', textAlign: 'center', color: 'var(--text-secondary)' }}><div><Sparkles size={26} color="#2563eb" /><p style={{ color: 'var(--text-primary)', fontWeight: 800, margin: '12px 0 6px' }}>Kapsama boşluklarını birlikte inceleyelim.</p><p style={{ margin: 0, fontSize: 13 }}>Örnek: “Yüksek eşleşmeli verinin farklı kanallardan aktarılmasına karşı workflowlarım yeterli mi?”</p></div></div>}
          {messages.map((item, index) => <article key={`${item.role}-${index}`} style={{ marginLeft: item.role === 'user' ? 'auto' : 0, marginBottom: 14, maxWidth: '88%', padding: '11px 13px', borderRadius: 8, background: item.role === 'user' ? '#2563eb' : 'var(--surface)', color: item.role === 'user' ? 'white' : 'var(--text-primary)', border: item.role === 'user' ? 'none' : '1px solid var(--border)', whiteSpace: 'pre-wrap', fontSize: 13, lineHeight: 1.6 }}>{item.content}</article>)}
          {sending && <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--text-secondary)', fontSize: 13 }}><Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} /> Workflow ve olay kapsamı inceleniyor...</div>}
          <div ref={bottom} />
        </div>
        <form onSubmit={submit} style={{ padding: 14, borderTop: '1px solid var(--border)', display: 'flex', gap: 8 }}>
          <textarea value={input} onChange={event => setInput(event.target.value)} disabled={sending || loadingContext} placeholder="Eksik senaryo veya yeni workflow önerisi sor..." style={{ ...inputStyle, minHeight: 68, resize: 'vertical', flex: 1 }} />
          <button type="submit" disabled={!input.trim() || sending || loadingContext} title="Gönder" style={{ ...primaryButton, width: 40, padding: 0 }}><Send size={16} /></button>
        </form>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, padding: '0 14px 14px' }}>
          <button onClick={() => void send(STARTER)} disabled={sending || loadingContext} style={suggestionButton}>Kapsama boşluğu analizi</button>
          <button onClick={() => void send('Son 30 günlük kanal ve aksiyon dağılımına göre, mevcut workflowlarımın kaçırabileceği anomali senaryolarını listele. Her biri için hangi node veya filtreyi eklemem gerektiğini öner.')} disabled={sending || loadingContext} style={suggestionButton}>Yeni anomali senaryoları</button>
          <button onClick={() => void createDraft()} disabled={creatingDraft || sending || loadingContext} style={{ ...suggestionButton, background: '#ecfdf5', borderColor: '#a7f3d0', color: '#047857' }}>{creatingDraft ? 'Taslak hazırlanıyor...' : 'Workflow Taslağı Oluştur'}</button>
        </div>
      </section>

      <aside style={{ display: 'grid', gap: 16 }}>
        <section style={panelStyle}><div style={{ padding: 16, borderBottom: '1px solid var(--border)', fontWeight: 800 }}>İncelenen Bağlam</div><div style={{ padding: 16, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}><Metric label="Workflow" value={`${fmt(context?.enabledWorkflowCount)} / ${fmt(context?.workflowCount)}`} /><Metric label="Seçili dönem olay" value={fmt(context?.incidentCount)} /><Metric label="Farklı kullanıcı" value={fmt(context?.uniqueUsers)} /><Metric label="Node türü" value={fmt(context?.nodeTypes?.length)} /></div></section>
        <section style={panelStyle}><div style={{ padding: 16, borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 8, fontWeight: 800 }}><Workflow size={16} /> Workflow Durumu</div><div style={{ padding: 10, maxHeight: 330, overflowY: 'auto' }}>{loadingContext ? <div style={{ padding: 10, color: 'var(--text-secondary)', fontSize: 13 }}>Yükleniyor...</div> : (context?.workflows ?? []).map(workflow => <div key={workflow.id} style={{ padding: 10, borderBottom: '1px solid var(--border)' }}><div style={{ fontWeight: 700, fontSize: 13 }}>{workflow.name}</div><div style={{ marginTop: 4, fontSize: 11, color: 'var(--text-secondary)' }}>{workflow.enabled ? 'Etkin' : 'Pasif'} · {workflow.schedule || 'Zamanlama yok'} · {(workflow.nodes ?? []).length} node</div>{(workflow.validationErrors ?? []).length > 0 && <div style={{ marginTop: 5, fontSize: 11, color: '#b91c1c' }}>{workflow.validationErrors[0]}</div>}{workflow.failedMails > 0 && <div style={{ marginTop: 5, fontSize: 11, color: '#b91c1c' }}>{workflow.failedMails} başarısız mail</div>}</div>)}</div></section>
        <section style={{ ...panelStyle, padding: 15, background: '#eff6ff', borderColor: '#bfdbfe' }}><strong style={{ fontSize: 13, color: '#1d4ed8' }}>Agent sınırı</strong><p style={{ margin: '6px 0 0', fontSize: 12, lineHeight: 1.5, color: '#1e40af' }}>Workflow veya olay kaydı değiştiremez, mail gönderemez ve veritabanına SQL çalıştıramaz. Öneri verir; uygulama adımı sizde kalır.</p></section>
      </aside>
    </div>
  </main>
}

function Metric({ label, value }: { label: string; value: string }) { return <div style={{ padding: 10, border: '1px solid var(--border)', borderRadius: 6 }}><div style={{ color: 'var(--text-secondary)', fontSize: 11 }}>{label}</div><div style={{ marginTop: 4, fontSize: 17, fontWeight: 800 }}>{value}</div></div> }
const panelStyle = { background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' } as const
const inputStyle = { width: '100%', padding: '9px 11px', borderRadius: 6, border: '1px solid var(--border)', color: 'var(--text-primary)', background: 'var(--surface)', fontSize: 13, outline: 'none' } as const
const primaryButton = { display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 7, border: '1px solid #1d4ed8', borderRadius: 6, background: '#2563eb', color: 'white', cursor: 'pointer', fontWeight: 700 } as const
const secondaryButton = { display: 'inline-flex', alignItems: 'center', gap: 7, padding: '8px 11px', border: '1px solid var(--border)', borderRadius: 6, background: 'var(--surface)', color: 'var(--text-primary)', cursor: 'pointer', fontWeight: 700, fontSize: 13 } as const
const suggestionButton = { padding: '6px 9px', border: '1px solid #bfdbfe', borderRadius: 6, background: '#eff6ff', color: '#1d4ed8', cursor: 'pointer', fontSize: 12, fontWeight: 700 } as const
const dateLabel = { display: 'grid', gap: 4, color: 'var(--text-secondary)', fontWeight: 700, fontSize: 11 } as const
const dateInput = { height: 34, padding: '6px 8px', border: '1px solid var(--border)', borderRadius: 6, background: 'var(--surface)', color: 'var(--text-primary)', fontSize: 12 } as const
