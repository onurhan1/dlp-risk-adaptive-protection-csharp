'use client'

import { FormEvent, ReactNode, useCallback, useEffect, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { Bot, Loader2, Plus, RefreshCw, Send, ShieldCheck, Sparkles, Workflow } from 'lucide-react'
import apiClient from '@/lib/axios'

type ChatMessage = { role: 'user' | 'assistant'; content: string }
type Count = { name: string; count: number }
type ShadowCandidate = { userEmail: string; team?: string | null; shadowScore: number; dailyRiskScore: number; isolationForestScore: number; baselineDelta: number; incidentCount: number; confidence: string; evidence: string[] }
type ReviewSummary = { confirmed: number; falsePositive: number; needsReview: number; reviewed: number; precision: number }
type SimulationResult = { playbookId: number; runId: number; status: string; dryRun: boolean; pendingMails: number; failedMails: number; skippedMails: number; errorMessage?: string | null; nodeSummary: string[]; workflowName: string }
type HighRiskList = { startDate: string; endDate: string; minimumScore: number; matchingCandidateCount: number; candidates: ShadowCandidate[] }
type AgentContext = {
  startUtc: string
  endUtc: string
  workflowCount: number
  enabledWorkflowCount: number
  incidentCount: number
  uniqueUsers: number
  nodeTypes: Count[]
  shadowRiskCandidates: ShadowCandidate[]
  reviews: ReviewSummary
  workflows: Array<{ id: number; name: string; enabled: boolean; autoSend: boolean; schedule?: string | null; nodes: string[]; validationErrors: string[]; lastRunStatus?: string | null; pendingMails: number; failedMails: number }>
}

function normalizeReviews(data: any, fallback: ReviewSummary = { confirmed: 0, falsePositive: 0, needsReview: 0, reviewed: 0, precision: 0 }): ReviewSummary {
  return {
    confirmed: Number(data?.confirmed ?? fallback.confirmed),
    falsePositive: Number(data?.falsePositive ?? data?.false_positive ?? fallback.falsePositive),
    needsReview: Number(data?.needsReview ?? data?.needs_review ?? fallback.needsReview),
    reviewed: Number(data?.reviewed ?? fallback.reviewed),
    precision: Number(data?.precision ?? fallback.precision),
  }
}

function normalizeHighRiskList(data: any): HighRiskList | null {
  if (!data) return null
  return {
    startDate: data.startDate ?? data.start_date ?? '',
    endDate: data.endDate ?? data.end_date ?? '',
    minimumScore: Number(data.minimumScore ?? data.minimum_score ?? 70),
    matchingCandidateCount: Number(data.matchingCandidateCount ?? data.matching_candidate_count ?? 0),
    candidates: Array.isArray(data.candidates) ? data.candidates : [],
  }
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
    shadowRiskCandidates: Array.isArray(data?.shadowRiskCandidates) ? data.shadowRiskCandidates : Array.isArray(data?.shadow_risk_candidates) ? data.shadow_risk_candidates : [],
    reviews: normalizeReviews(data?.reviews),
    workflows,
  }
}

function toDateTimeLocalValue(value?: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
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
  const [simulatingWorkflowId, setSimulatingWorkflowId] = useState<number | null>(null)
  const [reviewingUser, setReviewingUser] = useState<string | null>(null)
  const [reviewedShadow, setReviewedShadow] = useState<Record<string, string>>({})
  const [shadowFilter, setShadowFilter] = useState<'all' | 'high' | 'medium'>('all')
  const [simulation, setSimulation] = useState<SimulationResult | null>(null)
  const [highRiskList, setHighRiskList] = useState<HighRiskList | null>(null)
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
      setHighRiskList(normalizeHighRiskList(data.highRiskList ?? data.high_risk_list))
      const refreshedContext = normalizeContext(data.context)
      setContext(refreshedContext)
      setStartDate(toDateTimeLocalValue(refreshedContext.startUtc))
      setEndDate(toDateTimeLocalValue(refreshedContext.endUtc))
    } catch (requestError: any) {
      setError(requestError?.response?.data?.detail || 'Agent yanıtı alınamadı.')
    } finally {
      setSending(false)
    }
  }

  const submit = (event: FormEvent) => { event.preventDefault(); void send() }
  const startNewConversation = () => {
    if (sending || creatingDraft) return
    setMessages([])
    setInput('')
    setError(null)
    setDraftNotice(null)
    setHighRiskList(null)
  }
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
  const simulateWorkflow = async (playbookId: number, name: string) => {
    setSimulatingWorkflowId(playbookId)
    setError(null)
    try {
      const { data } = await apiClient.post(`/api/security-agent/workflows/${playbookId}/simulate`, undefined, { timeout: 600_000 })
      setSimulation({
        playbookId: Number(data.playbookId ?? data.playbook_id ?? playbookId),
        runId: Number(data.runId ?? data.run_id ?? 0),
        status: data.status ?? 'unknown',
        dryRun: Boolean(data.dryRun ?? data.dry_run),
        pendingMails: Number(data.pendingMails ?? data.pending_mails ?? 0),
        failedMails: Number(data.failedMails ?? data.failed_mails ?? 0),
        skippedMails: Number(data.skippedMails ?? data.skipped_mails ?? 0),
        errorMessage: data.errorMessage ?? data.error_message ?? null,
        nodeSummary: Array.isArray(data.nodeSummary) ? data.nodeSummary : Array.isArray(data.node_summary) ? data.node_summary : [],
        workflowName: name,
      })
      setDraftNotice(`${name}: dry-run tamamlandı. ${Number(data.pendingMails ?? data.pending_mails ?? 0)} onay bekleyen e-posta, ${Number(data.failedMails ?? data.failed_mails ?? 0)} hata. E-posta gönderilmedi.`)
      await loadContext()
    } catch (requestError: any) {
      setError(requestError?.response?.data?.detail || 'Workflow simülasyonu tamamlanamadı.')
    } finally {
      setSimulatingWorkflowId(null)
    }
  }
  const reviewShadow = async (userEmail: string, verdict: string) => {
    setReviewingUser(userEmail)
    try {
      const { data } = await apiClient.post('/api/risk-shadow/reviews', { userEmail, verdict })
      setReviewedShadow(current => ({ ...current, [userEmail]: verdict }))
      setContext(current => current ? { ...current, reviews: normalizeReviews(data, current.reviews) } : current)
      setDraftNotice(`${userEmail} için shadow değerlendirmesi kaydedildi.`)
    }
    catch (requestError: any) { setError(requestError?.response?.data?.detail || 'Değerlendirme kaydedilemedi.') }
    finally { setReviewingUser(null) }
  }
  const applyPeriodPreset = (days: number) => {
    const end = new Date()
    const start = new Date(end.getTime() - days * 86400000)
    setStartDate(start.toISOString().slice(0, 16))
    setEndDate(end.toISOString().slice(0, 16))
    setDraftNotice(`Son ${days} gün seçildi. Bağlamı yenilemek için “Dönemi İncele”ye basın.`)
  }
  const visibleShadowCandidates = (context?.shadowRiskCandidates ?? []).filter(candidate =>
    shadowFilter === 'all' || (shadowFilter === 'high' ? candidate.shadowScore >= 70 : candidate.shadowScore >= 50 && candidate.shadowScore < 70))
  const fmt = (value?: number) => (value ?? 0).toLocaleString('tr-TR')

  return <main className="container" style={{ maxWidth: 1480, paddingTop: 24, paddingBottom: 36 }}>
    <header style={{ display: 'flex', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap', alignItems: 'flex-start', marginBottom: 18 }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}><ShieldCheck size={24} color="#047857" /><h1 style={{ margin: 0, fontSize: 24, letterSpacing: 0 }}>Güvenlik Agentı</h1></div>
        <p style={{ margin: '7px 0 0', color: 'var(--text-secondary)', fontSize: 13 }}>Workflow kapsamasını ve seçtiğiniz dönemin olay dağılımını salt-okunur inceler; gözden kaçabilecek güvenlik senaryoları için kanıta dayalı öneri üretir.</p>
      </div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'end', flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', gap: 4, paddingBottom: 1 }} aria-label="Hızlı dönem seçimi">
          {[7, 30, 90].map(days => <button key={days} onClick={() => applyPeriodPreset(days)} disabled={loadingContext} style={{ ...suggestionButton, padding: '6px 7px' }}>Son {days} gün</button>)}
        </div>
        <label style={dateLabel}>Başlangıç<input type="datetime-local" value={startDate} onChange={event => setStartDate(event.target.value)} style={dateInput} /></label>
        <label style={dateLabel}>Bitiş<input type="datetime-local" value={endDate} onChange={event => setEndDate(event.target.value)} style={dateInput} /></label>
        <button onClick={() => void loadContext()} disabled={loadingContext} style={secondaryButton}><RefreshCw size={15} style={{ animation: loadingContext ? 'spin 1s linear infinite' : undefined }} /> Dönemi İncele</button>
      </div>
    </header>
    {error && <div style={{ marginBottom: 14, padding: 12, borderRadius: 6, background: '#fee2e2', color: '#b91c1c', fontSize: 13 }}>{error}</div>}
    {draftNotice && <div style={{ marginBottom: 14, padding: 12, borderRadius: 6, background: '#dcfce7', color: '#15803d', fontSize: 13 }}>{draftNotice}</div>}
    {highRiskList && <section style={{ ...panelStyle, marginBottom: 14, borderColor: '#bfdbfe', background: '#eff6ff' }}><div style={{ padding: '12px 14px', fontWeight: 800, color: '#1e3a8a' }}>Deterministik yüksek risk sorgusu</div><div style={{ padding: '0 14px 14px', color: '#1e40af', fontSize: 12 }}>Dönem: {highRiskList.startDate} – {highRiskList.endDate} UTC · Eşik: {highRiskList.minimumScore.toFixed(0)}+ · Eşleşen: {fmt(highRiskList.matchingCandidateCount)} · Gösterilen: {fmt(highRiskList.candidates.length)}. Tablo, modelden bağımsız olarak sunucu tarafından üretildi.</div></section>}
    {simulation && <section style={{ ...panelStyle, marginBottom: 14, borderColor: simulation.failedMails > 0 ? '#fecaca' : '#a7f3d0' }}>
      <div style={{ padding: '12px 14px', borderBottom: '1px solid var(--border)', fontWeight: 800 }}>Son dry-run sonucu · {simulation.workflowName}</div>
      <div style={{ padding: 14, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 10 }}>
        <Metric label="Durum" value={simulation.status} /><Metric label="Onay bekleyen mail" value={fmt(simulation.pendingMails)} /><Metric label="Atlanan" value={fmt(simulation.skippedMails)} /><Metric label="Hata" value={fmt(simulation.failedMails)} />
      </div>
      <div style={{ padding: '0 14px 14px', color: 'var(--text-secondary)', fontSize: 12 }}>
        <strong>{simulation.dryRun ? 'E-posta gönderilmedi; bu bir simülasyondur.' : 'Çalıştırma sonucu.'}</strong>{simulation.errorMessage ? ` Hata detayı: ${simulation.errorMessage}` : ''}
        {simulation.nodeSummary.length > 0 && <ul style={{ margin: '8px 0 0', paddingLeft: 18 }}>{simulation.nodeSummary.slice(0, 8).map(node => <li key={node}>{node}</li>)}</ul>}
      </div>
    </section>}

    <div className="security-agent-layout" style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(300px, 365px)', gap: 16, alignItems: 'start' }}>
      <section style={panelStyle}>
        <div style={{ padding: '16px 18px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}><strong style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Bot size={18} color="#2563eb" /> Agent Sohbeti</strong><div style={{ display: 'flex', alignItems: 'center', gap: 10 }}><span style={{ color: '#047857', fontSize: 12, fontWeight: 700 }}>Salt-okunur</span><button onClick={startNewConversation} disabled={sending || creatingDraft || messages.length === 0} title="Sohbeti temizle ve yeni bir analiz başlat" style={{ ...secondaryButton, padding: '6px 8px', fontSize: 12 }}><Plus size={14} /> Yeni sohbet</button></div></div>
        <div style={{ height: 480, overflowY: 'auto', padding: 18, background: 'var(--background)' }}>
          {messages.length === 0 && <div style={{ height: '100%', display: 'grid', placeItems: 'center', textAlign: 'center', color: 'var(--text-secondary)' }}><div><Sparkles size={26} color="#2563eb" /><p style={{ color: 'var(--text-primary)', fontWeight: 800, margin: '12px 0 6px' }}>Kapsama boşluklarını birlikte inceleyelim.</p><p style={{ margin: 0, fontSize: 13 }}>Örnek: “Yüksek eşleşmeli verinin farklı kanallardan aktarılmasına karşı workflowlarım yeterli mi?”</p></div></div>}
          {messages.map((item, index) => <article key={`${item.role}-${index}`} style={{ marginLeft: item.role === 'user' ? 'auto' : 0, marginBottom: 14, maxWidth: '88%', padding: '11px 13px', borderRadius: 8, background: item.role === 'user' ? '#2563eb' : 'var(--surface)', color: item.role === 'user' ? 'white' : 'var(--text-primary)', border: item.role === 'user' ? 'none' : '1px solid var(--border)', fontSize: 13, lineHeight: 1.6 }}><MarkdownMessage content={item.content} /></article>)}
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
        <section style={panelStyle}>
          <div style={{ padding: 16, borderBottom: '1px solid var(--border)', fontWeight: 800 }}>Shadow risk adayları</div>
          <div style={{ padding: '10px 10px 0', color: 'var(--text-secondary)', fontSize: 11 }}>İncelenen: {fmt(context?.reviews.reviewed)} · Doğrulanan: {fmt(context?.reviews.confirmed)} · Yanlış pozitif: {fmt(context?.reviews.falsePositive)} · Precision: %{context?.reviews.precision.toFixed(1) ?? '0.0'}</div>
          <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap', padding: 10 }}>
            {([['all', 'Tümü'], ['high', '70+ yüksek'], ['medium', '50–69 orta']] as const).map(([value, label]) => <button key={value} onClick={() => setShadowFilter(value)} style={{ ...suggestionButton, background: shadowFilter === value ? '#dbeafe' : '#eff6ff' }}>{label}</button>)}
          </div>
          <div style={{ padding: '0 10px 10px', maxHeight: 260, overflowY: 'auto' }}>
            {(context?.shadowRiskCandidates ?? []).length === 0 ? <div style={{ padding: 8, color: 'var(--text-secondary)', fontSize: 12 }}>Bu dönem için yeterli risk/baz çizgisi verisi yok.</div> : visibleShadowCandidates.length === 0 ? <div style={{ padding: 8, color: 'var(--text-secondary)', fontSize: 12 }}>Bu skor bandında aday yok.</div> : visibleShadowCandidates.map(item => {
              const verdict = reviewedShadow[item.userEmail]
              return <div key={item.userEmail} style={{ padding: 9, borderBottom: '1px solid var(--border)', fontSize: 12 }}><strong>{item.userEmail}</strong><span style={{ float: 'right', color: item.shadowScore >= 70 ? '#b91c1c' : '#a16207' }}>{item.shadowScore.toFixed(1)}</span><div style={{ marginTop: 4, color: 'var(--text-secondary)' }}>Güven: {item.confidence} · Baz farkı: {item.baselineDelta.toFixed(1)} · Olay: {item.incidentCount}</div><div style={{ marginTop: 4, color: 'var(--text-secondary)' }}>{(item.evidence ?? []).slice(0, 2).join(' · ')}</div>{verdict ? <div style={{ marginTop: 7, color: verdict === 'confirmed' ? '#047857' : '#a16207', fontWeight: 700 }}>Bu oturumda {verdict === 'confirmed' ? 'doğrulandı' : 'yanlış pozitif olarak işaretlendi'}.</div> : <div style={{ display: 'flex', gap: 5, marginTop: 7 }}><button disabled={reviewingUser !== null} onClick={() => void reviewShadow(item.userEmail, 'confirmed')} style={suggestionButton}>Doğrula</button><button disabled={reviewingUser !== null} onClick={() => void reviewShadow(item.userEmail, 'false_positive')} style={suggestionButton}>Yanlış pozitif</button></div>}</div>
            })}
          </div>
          <details style={{ margin: '0 10px 10px', padding: '9px 10px', border: '1px solid #bfdbfe', borderRadius: 6, background: '#f8fbff', color: '#1e3a8a', fontSize: 11 }}>
            <summary style={{ cursor: 'pointer', fontWeight: 800 }}>Shadow score nasıl hesaplanır?</summary>
            <div style={{ marginTop: 8, lineHeight: 1.55 }}>
              <div><code>Günlük risk × 0,55 + Isolation Forest × 0,30 + min(pozitif baz farkı, 30) × 0,50</code></div>
              <ul style={{ margin: '7px 0', paddingLeft: 17 }}>
                <li>Günlük risk: seçili dönemdeki kullanıcının ortalama günlük risk puanı.</li>
                <li>Baz farkı: seçili dönem ile önceki 60 günlük kişisel ortalama arasındaki değişimdir; yalnız artış puana eklenir.</li>
                <li>Isolation Forest: en güncel davranış anomalisi sinyalidir.</li>
              </ul>
              <div><strong>70+</strong> yüksek, <strong>50–69</strong> orta önceliktir. Skor bir ihlal kararı veya otomatik aksiyon değildir; analist doğrulaması gerekir. Güven etiketi, kullanıcının anomali modeli için yeterli baz verisi olup olmadığını gösterir.</div>
            </div>
          </details>
        </section>
        <section style={panelStyle}><div style={{ padding: 16, borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 8, fontWeight: 800 }}><Workflow size={16} /> Workflow Durumu</div><div style={{ padding: 10, maxHeight: 330, overflowY: 'auto' }}>{loadingContext ? <div style={{ padding: 10, color: 'var(--text-secondary)', fontSize: 13 }}>Yükleniyor...</div> : (context?.workflows ?? []).map(workflow => <div key={workflow.id} style={{ padding: 10, borderBottom: '1px solid var(--border)' }}><div style={{ fontWeight: 700, fontSize: 13 }}>{workflow.name}</div><div style={{ marginTop: 4, fontSize: 11, color: 'var(--text-secondary)' }}>{workflow.enabled ? 'Etkin' : 'Pasif'} · {workflow.schedule || 'Zamanlama yok'} · {(workflow.nodes ?? []).length} node</div>{(workflow.validationErrors ?? []).length > 0 && <div style={{ marginTop: 5, fontSize: 11, color: '#b91c1c' }}>{workflow.validationErrors[0]}</div>}{workflow.failedMails > 0 && <div style={{ marginTop: 5, fontSize: 11, color: '#b91c1c' }}>{workflow.failedMails} başarısız mail</div>}<button onClick={() => void simulateWorkflow(workflow.id, workflow.name)} disabled={simulatingWorkflowId !== null || loadingContext} style={{ ...suggestionButton, marginTop: 8 }}>{simulatingWorkflowId === workflow.id ? 'Simüle ediliyor...' : 'Dry-run simüle et'}</button></div>)}</div></section>
        <section style={{ ...panelStyle, padding: 15, background: '#eff6ff', borderColor: '#bfdbfe' }}><strong style={{ fontSize: 13, color: '#1d4ed8' }}>Agent sınırı</strong><p style={{ margin: '6px 0 0', fontSize: 12, lineHeight: 1.5, color: '#1e40af' }}>Workflow veya olay kaydı değiştiremez, mail gönderemez ve veritabanına SQL çalıştıramaz. Öneri verir; uygulama adımı sizde kalır.</p></section>
        <section style={{ ...panelStyle, padding: 15, background: '#f8fafc' }}><strong style={{ fontSize: 13 }}>Nasıl kullanılır?</strong><ol style={{ margin: '8px 0 0', paddingLeft: 18, fontSize: 12, lineHeight: 1.55, color: 'var(--text-secondary)' }}><li>Dönemi seçip bağlamı yenileyin.</li><li>Sohbette kapsama boşluğunu veya araştırma sorusunu yazın.</li><li>Öneriyi inceleyin; gerekirse pasif workflow taslağı oluşturun.</li><li>Editörde kuralları tamamlayıp dry-run ile sonucu doğrulayın.</li></ol></section>
      </aside>
    </div>
    <style jsx>{`
      @media (max-width: 900px) {
        .security-agent-layout { grid-template-columns: minmax(0, 1fr) !important; }
      }
    `}</style>
  </main>
}

function Metric({ label, value }: { label: string; value: string }) { return <div style={{ padding: 10, border: '1px solid var(--border)', borderRadius: 6 }}><div style={{ color: 'var(--text-secondary)', fontSize: 11 }}>{label}</div><div style={{ marginTop: 4, fontSize: 17, fontWeight: 800 }}>{value}</div></div> }

function MarkdownMessage({ content }: { content: string }) {
  const lines = content.replace(/\r\n/g, '\n').split('\n')
  const blocks: ReactNode[] = []
  let index = 0

  while (index < lines.length) {
    const line = lines[index]
    if (!line.trim()) { index += 1; continue }

    const heading = line.match(/^(#{1,3})\s+(.+)$/)
    if (heading) {
      const size = heading[1].length === 1 ? 17 : heading[1].length === 2 ? 15 : 14
      blocks.push(<div key={`heading-${index}`} style={{ fontSize: size, fontWeight: 800, margin: '10px 0 6px' }}>{renderMarkdownInline(heading[2], `heading-${index}`)}</div>)
      index += 1
      continue
    }

    if (/^\s*((---+)|(\*\*\*+)|(___+))\s*$/.test(line)) {
      blocks.push(<hr key={`rule-${index}`} style={{ border: 0, borderTop: '1px solid currentColor', opacity: .22, margin: '12px 0' }} />)
      index += 1
      continue
    }

    if (isMarkdownTableLine(line) && isMarkdownTableDivider(lines[index + 1] ?? '')) {
      const header = splitMarkdownTableRow(line)
      index += 2
      const rows: string[][] = []
      while (index < lines.length && isMarkdownTableLine(lines[index])) rows.push(splitMarkdownTableRow(lines[index++]))
      blocks.push(<div key={`table-${index}`} style={{ margin: '9px 0', overflowX: 'auto', border: '1px solid currentColor', borderRadius: 5, opacity: .96 }}><table style={{ width: '100%', minWidth: `${Math.max(header.length, 2) * 120}px`, borderCollapse: 'collapse', fontSize: 12 }}><thead><tr>{header.map((cell, cellIndex) => <th key={`header-${cellIndex}`} style={{ textAlign: 'left', padding: '7px 8px', borderBottom: '1px solid currentColor', background: 'rgba(15,23,42,.09)', verticalAlign: 'top' }}>{renderMarkdownInline(cell, `header-${cellIndex}`)}</th>)}</tr></thead><tbody>{rows.map((row, rowIndex) => <tr key={`row-${rowIndex}`}>{header.map((_, cellIndex) => <td key={`cell-${rowIndex}-${cellIndex}`} style={{ padding: '7px 8px', borderTop: rowIndex ? '1px solid rgba(100,116,139,.2)' : 0, verticalAlign: 'top' }}>{renderMarkdownInline(row[cellIndex] ?? '', `cell-${rowIndex}-${cellIndex}`)}</td>)}</tr>)}</tbody></table></div>)
      continue
    }

    const unordered = line.match(/^\s*[-*+]\s+(.+)$/)
    const ordered = line.match(/^\s*\d+[.)]\s+(.+)$/)
    if (unordered || ordered) {
      const isOrdered = Boolean(ordered)
      const items: string[] = []
      while (index < lines.length) {
        const item = isOrdered ? lines[index].match(/^\s*\d+[.)]\s+(.+)$/) : lines[index].match(/^\s*[-*+]\s+(.+)$/)
        if (!item) break
        items.push(item[1])
        index += 1
      }
      const List = isOrdered ? 'ol' : 'ul'
      blocks.push(<List key={`list-${index}`} style={{ margin: '7px 0', paddingLeft: 21 }}>{items.map((item, itemIndex) => <li key={`item-${itemIndex}`} style={{ margin: '3px 0' }}>{renderMarkdownInline(item, `list-${itemIndex}`)}</li>)}</List>)
      continue
    }

    const paragraph: string[] = []
    while (index < lines.length && lines[index].trim() && !/^(#{1,3})\s+/.test(lines[index]) && !(isMarkdownTableLine(lines[index]) && isMarkdownTableDivider(lines[index + 1] ?? '')) && !/^\s*[-*+]\s+/.test(lines[index]) && !/^\s*\d+[.)]\s+/.test(lines[index])) paragraph.push(lines[index++])
    blocks.push(<p key={`paragraph-${index}`} style={{ margin: '0 0 9px', whiteSpace: 'pre-wrap' }}>{paragraph.map((paragraphLine, lineIndex) => <span key={`line-${lineIndex}`}>{lineIndex > 0 && <br />}{renderMarkdownInline(paragraphLine, `paragraph-${lineIndex}`)}</span>)}</p>)
  }

  return <>{blocks}</>
}

function renderMarkdownInline(value: string, keyPrefix: string): ReactNode[] {
  return value.split(/(\*\*[^*]+\*\*|`[^`]+`)/g).filter(Boolean).map((part, index) => {
    if (part.startsWith('**') && part.endsWith('**')) return <strong key={`${keyPrefix}-bold-${index}`}>{part.slice(2, -2)}</strong>
    if (part.startsWith('`') && part.endsWith('`')) return <code key={`${keyPrefix}-code-${index}`} style={{ padding: '1px 4px', borderRadius: 3, background: 'rgba(15,23,42,.11)', fontSize: '.92em' }}>{part.slice(1, -1)}</code>
    return <span key={`${keyPrefix}-text-${index}`}>{part}</span>
  })
}

function isMarkdownTableLine(line: string) { return /^\s*\|.*\|\s*$/.test(line) }
function isMarkdownTableDivider(line: string) { return /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(line) }
function splitMarkdownTableRow(line: string) { return line.trim().replace(/^\||\|$/g, '').split('|').map(cell => cell.trim()) }

const panelStyle = { background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' } as const
const inputStyle = { width: '100%', padding: '9px 11px', borderRadius: 6, border: '1px solid var(--border)', color: 'var(--text-primary)', background: 'var(--surface)', fontSize: 13, outline: 'none' } as const
const primaryButton = { display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 7, border: '1px solid #1d4ed8', borderRadius: 6, background: '#2563eb', color: 'white', cursor: 'pointer', fontWeight: 700 } as const
const secondaryButton = { display: 'inline-flex', alignItems: 'center', gap: 7, padding: '8px 11px', border: '1px solid var(--border)', borderRadius: 6, background: 'var(--surface)', color: 'var(--text-primary)', cursor: 'pointer', fontWeight: 700, fontSize: 13 } as const
const suggestionButton = { padding: '6px 9px', border: '1px solid #bfdbfe', borderRadius: 6, background: '#eff6ff', color: '#1d4ed8', cursor: 'pointer', fontSize: 12, fontWeight: 700 } as const
const dateLabel = { display: 'grid', gap: 4, color: 'var(--text-secondary)', fontWeight: 700, fontSize: 11 } as const
const dateInput = { height: 34, padding: '6px 8px', border: '1px solid var(--border)', borderRadius: 6, background: 'var(--surface)', color: 'var(--text-primary)', fontSize: 12 } as const
