'use client'

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties, FormEvent, KeyboardEvent, ReactNode } from 'react'
import { Bot, Database, EyeOff, Loader2, MessageSquare, Plus, PlugZap, RefreshCw, Save, Send, ShieldCheck, Sparkles, Trash2 } from 'lucide-react'
import apiClient from '@/lib/axios'

type Settings = { enabled: boolean; generate_url: string; model: string; temperature: number; max_tokens: number }
type Count = { name: string; count: number }
type Snapshot = {
  start_utc: string; end_utc: string; total_incidents: number; unique_users: number
  maximum_matches: number; profile_count: number; actions: Count[]; channels: Count[]; policies: Count[]; users: unknown[]; samples: unknown[]
}
type ChatMessage = { role: 'user' | 'assistant'; content: string }
type ConversationSummary = { id: string; title: string; updated_at: string; message_count: number; preview?: string | null }
type ConversationDetail = { id: string; title: string; created_at: string; updated_at: string; messages: ChatMessage[] }
type MailProposal = { id: string; conversation_id: string; user_name: string; full_name?: string | null; department?: string | null; recipient_email?: string | null; subject: string; body: string; incident_summary_json: string; rationale?: string | null; status: 'pending' | 'sent' | 'rejected' | 'failed' | 'unresolved'; created_at: string; updated_at: string; sent_at?: string | null; error_message?: string | null }

const INITIAL_SETTINGS: Settings = {
  enabled: false,
  generate_url: 'http://127.0.0.1:11434/api/generate',
  model: 'qwen3:8b',
  temperature: 0.2,
  max_tokens: 1800,
}

const ALGORITHM_PROMPT = 'Seçili incident verisini incele. RADARın mevcut risk puanını kullanmadan, yalnızca ham olay özelliklerinden 0-100 aralığında denetlenebilir bir risk puanlama algoritması ve riskli kullanıcı sınıflandırması öner. Faktörleri, ağırlıkları, eşikleri, örnek normalizasyonları, doğrulama planını ve sınırlılıkları anlaşılır başlıklar ve maddeler halinde açıkla.'
const LOCAL_LLM_REQUEST_TIMEOUT_MS = 600_000

export default function LocalLlmLabPage() {
  const [settings, setSettings] = useState<Settings>(INITIAL_SETTINGS)
  const [lookbackDays, setLookbackDays] = useState(30)
  const [sampleSize, setSampleSize] = useState(40)
  const [maskIdentifiers, setMaskIdentifiers] = useState(true)
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [analysisMode, setAnalysisMode] = useState<'standard' | 'comprehensive'>('standard')
  const [detailedUserLimit, setDetailedUserLimit] = useState(20)
  const [evidenceRowsPerUser, setEvidenceRowsPerUser] = useState(160)
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [conversations, setConversations] = useState<ConversationSummary[]>([])
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null)
  const [mailProposals, setMailProposals] = useState<MailProposal[]>([])
  const [proposalBusyId, setProposalBusyId] = useState<string | null>(null)
  const chatScrollRef = useRef<HTMLDivElement | null>(null)
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState<'save' | 'test' | 'chat' | 'snapshot' | 'conversation' | null>(null)
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const snapshotParams = useMemo(() => ({
    lookbackDays,
    sampleSize,
    maskIdentifiers,
    startUtc: startDate ? `${startDate}T00:00:00.000Z` : undefined,
    endUtc: endDate ? `${endDate}T23:59:59.999Z` : undefined,
    comprehensive: analysisMode === 'comprehensive',
  }), [lookbackDays, sampleSize, maskIdentifiers, startDate, endDate, analysisMode])

  const refreshSnapshot = useCallback(async () => {
    setBusy('snapshot')
    try {
      const response = await apiClient.get('/api/local-llm-lab/snapshot', { params: snapshotParams })
      setSnapshot(response.data)
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Incident bağlamı alınamadı.' })
    } finally {
      setBusy(null)
    }
  }, [snapshotParams])

  useEffect(() => {
    const load = async () => {
      setLoading(true)
      try {
        const [settingsResponse, snapshotResponse, conversationsResponse] = await Promise.all([
          apiClient.get('/api/local-llm-lab/settings'),
          apiClient.get('/api/local-llm-lab/snapshot', { params: snapshotParams }),
          apiClient.get('/api/local-llm-lab/conversations'),
        ])
        setSettings({ ...INITIAL_SETTINGS, ...settingsResponse.data })
        setSnapshot(snapshotResponse.data)
        const savedConversations = Array.isArray(conversationsResponse.data) ? conversationsResponse.data : []
        setConversations(savedConversations)
        if (savedConversations.length > 0) {
          const conversationResponse = await apiClient.get(`/api/local-llm-lab/conversations/${savedConversations[0].id}`)
          const conversation = conversationResponse.data as ConversationDetail
          setActiveConversationId(conversation.id)
          setMessages(conversation.messages || [])
          const proposalsResponse = await apiClient.get(`/api/local-llm-lab/conversations/${conversation.id}/mail-proposals`)
          setMailProposals(Array.isArray(proposalsResponse.data) ? proposalsResponse.data : [])
        }
      } catch (error: any) {
        setNotice({ type: 'error', text: error?.response?.data?.detail || 'Yerel LLM Laboratuvarı yüklenemedi.' })
      } finally {
        setLoading(false)
      }
    }
    void load()
  // Only load once. Subsequent data changes are explicit through the refresh control.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const refreshConversations = useCallback(async () => {
    const response = await apiClient.get('/api/local-llm-lab/conversations')
    setConversations(Array.isArray(response.data) ? response.data : [])
  }, [])

  const refreshMailProposals = useCallback(async (conversationId: string) => {
    const response = await apiClient.get(`/api/local-llm-lab/conversations/${conversationId}/mail-proposals`)
    setMailProposals(Array.isArray(response.data) ? response.data : [])
  }, [])

  const selectConversation = async (conversationId: string) => {
    if (conversationId === activeConversationId || busy !== null) return
    setBusy('conversation'); setNotice(null)
    try {
      const response = await apiClient.get(`/api/local-llm-lab/conversations/${conversationId}`)
      const conversation = response.data as ConversationDetail
      setActiveConversationId(conversation.id)
      setMessages(conversation.messages || [])
      await refreshMailProposals(conversation.id)
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Sohbet açılamadı.' })
    } finally { setBusy(null) }
  }

  const createConversation = async () => {
    if (busy !== null) return
    setBusy('conversation'); setNotice(null)
    try {
      const response = await apiClient.post('/api/local-llm-lab/conversations', {})
      const conversation = response.data as ConversationDetail
      setActiveConversationId(conversation.id)
      setMessages([])
      setMailProposals([])
      await refreshConversations()
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Yeni sohbet oluşturulamadı.' })
    } finally { setBusy(null) }
  }

  const deleteConversation = async (conversationId: string) => {
    if (busy !== null || !window.confirm('Bu sohbet ve mesajları silinsin mi?')) return
    setBusy('conversation'); setNotice(null)
    try {
      await apiClient.delete(`/api/local-llm-lab/conversations/${conversationId}`)
      const remaining = conversations.filter(conversation => conversation.id !== conversationId)
      setConversations(remaining)
      if (activeConversationId === conversationId) {
        setActiveConversationId(null)
        setMessages([])
        setMailProposals([])
      }
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Sohbet silinemedi.' })
    } finally { setBusy(null) }
  }

  const saveSettings = async () => {
    setBusy('save'); setNotice(null)
    try {
      await apiClient.put('/api/local-llm-lab/settings', settings)
      setNotice({ type: 'success', text: 'Yerel LLM ayarları kaydedildi.' })
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Ayarlar kaydedilemedi.' })
    } finally { setBusy(null) }
  }

  const testConnection = async () => {
    setBusy('test'); setNotice(null)
    try {
      const response = await apiClient.post('/api/local-llm-lab/test', settings, { timeout: LOCAL_LLM_REQUEST_TIMEOUT_MS })
      setNotice({ type: 'success', text: `Bağlantı başarılı: ${response.data?.reply || 'Model yanıt verdi.'}` })
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Yerel LLM bağlantısı kurulamadı.' })
    } finally { setBusy(null) }
  }

  const send = async (message?: string) => {
    const content = (message ?? input).trim()
    if (!content || busy === 'chat') return
    const nextMessages = [...messages, { role: 'user' as const, content }]
    setMessages(nextMessages); setInput(''); setBusy('chat'); setNotice(null)
    try {
      let conversationId = activeConversationId
      if (!conversationId) {
        const created = await apiClient.post('/api/local-llm-lab/conversations', {})
        conversationId = created.data.id
        setActiveConversationId(conversationId)
      }
      const response = await apiClient.post('/api/local-llm-lab/chat', {
        message: content,
        history: messages,
        lookback_days: lookbackDays,
        sample_size: sampleSize,
        mask_identifiers: maskIdentifiers,
        start_utc: startDate ? `${startDate}T00:00:00.000Z` : null,
        end_utc: endDate ? `${endDate}T23:59:59.999Z` : null,
        comprehensive: analysisMode === 'comprehensive',
        detailed_user_limit: detailedUserLimit,
        evidence_rows_per_user: evidenceRowsPerUser,
        conversation_id: conversationId,
      }, { timeout: LOCAL_LLM_REQUEST_TIMEOUT_MS })
      setMessages(current => [...current, { role: 'assistant', content: response.data.reply || 'Model boş yanıt verdi.' }])
      setActiveConversationId(response.data.conversation_id || conversationId)
      setSnapshot(response.data.snapshot)
      void refreshConversations()
      if (response.data.conversation_id || conversationId) void refreshMailProposals(response.data.conversation_id || conversationId)
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Model yanıtı alınamadı.' })
    } finally { setBusy(null) }
  }

  const onSubmit = (event: FormEvent) => { event.preventDefault(); void send() }
  const onInputKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void send() }
  }

  useEffect(() => {
    const container = chatScrollRef.current
    if (container) container.scrollTop = container.scrollHeight
  }, [messages, busy])

  const updateProposal = (proposalId: string, patch: Partial<MailProposal>) => {
    setMailProposals(current => current.map(proposal => proposal.id === proposalId ? { ...proposal, ...patch } : proposal))
  }

  const saveProposal = async (proposal: MailProposal) => {
    setProposalBusyId(proposal.id); setNotice(null)
    try {
      const response = await apiClient.put(`/api/local-llm-lab/mail-proposals/${proposal.id}`, {
        recipient_email: proposal.recipient_email,
        subject: proposal.subject,
        body: proposal.body,
      })
      updateProposal(proposal.id, response.data)
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Mail taslağı kaydedilemedi.' })
    } finally { setProposalBusyId(null) }
  }

  const decideProposal = async (proposal: MailProposal, decision: 'approve' | 'reject') => {
    setProposalBusyId(proposal.id); setNotice(null)
    try {
      const response = await apiClient.post(`/api/local-llm-lab/mail-proposals/${proposal.id}/${decision}`)
      updateProposal(proposal.id, response.data)
      setNotice({ type: 'success', text: decision === 'approve' ? 'Mail gönderim sonucu taslağa işlendi.' : 'Mail taslağı reddedildi.' })
    } catch (error: any) {
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Mail taslağı işlenemedi.' })
    } finally { setProposalBusyId(null) }
  }

  if (loading) return <div className="dashboard-page"><p className="text-muted">Yerel LLM Laboratuvarı yükleniyor...</p></div>

  return <div className="dashboard-page" style={{ maxWidth: '1540px', margin: '0 auto' }}>
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '16px', flexWrap: 'wrap', marginBottom: '18px' }}>
      <div>
        <h1 style={{ margin: 0, fontSize: '24px', color: 'var(--text-primary)' }}>Yerel LLM Laboratuvarı</h1>
        <p style={{ margin: '5px 0 0', color: 'var(--text-muted)', fontSize: '13px' }}>Incident örnekleri üzerinden denetlenebilir risk puanlama önerileri üretin.</p>
      </div>
      <div style={{ display: 'inline-flex', gap: '8px', alignItems: 'center', color: settings.enabled ? '#047857' : '#a16207', fontSize: '12px', fontWeight: 600 }}>
        <span style={{ width: '8px', height: '8px', background: settings.enabled ? '#10b981' : '#f59e0b', borderRadius: '50%' }} />
        {settings.enabled ? 'Model kullanıma açık' : 'Model kapalı'}
      </div>
    </div>

    {notice && <div style={{ marginBottom: '14px', padding: '11px 13px', borderRadius: '7px', border: `1px solid ${notice.type === 'success' ? 'rgba(5,150,105,.35)' : 'rgba(220,38,38,.35)'}`, background: notice.type === 'success' ? 'rgba(5,150,105,.08)' : 'rgba(220,38,38,.08)', color: notice.type === 'success' ? '#047857' : '#b91c1c', fontSize: '13px' }}>{notice.text}</div>}

    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 360px) minmax(0, 1fr)', gap: '16px', alignItems: 'start' }}>
      <section style={panelStyle}>
        <PanelTitle icon={<PlugZap size={17} />} title="Model Bağlantısı" />
        <label style={labelStyle}>Generate URL<input value={settings.generate_url} onChange={event => setSettings(current => ({ ...current, generate_url: event.target.value }))} style={inputStyle} placeholder="http://sunucu:11434/api/generate" /></label>
        <label style={labelStyle}>Model<input value={settings.model} onChange={event => setSettings(current => ({ ...current, model: event.target.value }))} style={inputStyle} placeholder="qwen3:8b" /></label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
          <label style={labelStyle}>Sıcaklık<input type="number" min="0" max="2" step="0.1" value={settings.temperature} onChange={event => setSettings(current => ({ ...current, temperature: Number(event.target.value) }))} style={inputStyle} /></label>
          <label style={labelStyle}>Maks. çıktı<input type="number" min="64" max="4096" step="64" value={settings.max_tokens} onChange={event => setSettings(current => ({ ...current, max_tokens: Number(event.target.value) }))} style={inputStyle} /></label>
        </div>
        <label style={{ ...labelStyle, flexDirection: 'row', alignItems: 'center', cursor: 'pointer', marginTop: '8px' }}><input type="checkbox" checked={settings.enabled} onChange={event => setSettings(current => ({ ...current, enabled: event.target.checked }))} /> Laboratuvarı etkinleştir</label>
        <div style={{ display: 'flex', gap: '8px', marginTop: '14px' }}>
          <button onClick={saveSettings} disabled={busy !== null} style={primaryButtonStyle}><Save size={14} /> {busy === 'save' ? 'Kaydediliyor...' : 'Kaydet'}</button>
          <button onClick={testConnection} disabled={busy !== null} style={secondaryButtonStyle}><PlugZap size={14} /> {busy === 'test' ? 'Test ediliyor...' : 'Test Et'}</button>
        </div>

        <div style={{ borderTop: '1px solid var(--border)', marginTop: '18px', paddingTop: '14px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '8px', marginBottom: '9px' }}>
            <PanelTitle icon={<MessageSquare size={16} />} title="Sohbet Geçmişi" />
            <button onClick={() => void createConversation()} disabled={busy !== null} title="Yeni sohbet" style={{ ...secondaryButtonStyle, padding: '6px 7px' }}><Plus size={15} /></button>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '5px', maxHeight: '310px', overflowY: 'auto' }}>
            {conversations.length === 0 && <span style={{ color: 'var(--text-muted)', fontSize: '12px', padding: '5px 1px' }}>Henüz kaydedilmiş sohbet yok.</span>}
            {conversations.map(conversation => <div key={conversation.id} role="button" tabIndex={0} onClick={() => void selectConversation(conversation.id)} onKeyDown={event => { if (event.key === 'Enter') void selectConversation(conversation.id) }} style={{ display: 'flex', alignItems: 'center', gap: '7px', border: `1px solid ${conversation.id === activeConversationId ? '#2563eb' : 'var(--border)'}`, background: conversation.id === activeConversationId ? 'rgba(37,99,235,.08)' : 'var(--surface)', borderRadius: '6px', padding: '8px', cursor: busy === null ? 'pointer' : 'default' }}>
              <MessageSquare size={14} color={conversation.id === activeConversationId ? '#2563eb' : '#64748b'} style={{ flexShrink: 0 }} />
              <div style={{ minWidth: 0, flex: 1 }}>
                <div style={{ color: 'var(--text-primary)', fontSize: '12px', fontWeight: 650, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{conversation.title}</div>
                <div style={{ color: 'var(--text-muted)', fontSize: '10px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', marginTop: '2px' }}>{conversation.preview || `${conversation.message_count} mesaj`} · {formatConversationTime(conversation.updated_at)}</div>
              </div>
              <button onClick={event => { event.stopPropagation(); void deleteConversation(conversation.id) }} disabled={busy !== null} title="Sohbeti sil" style={{ border: 0, background: 'transparent', color: '#dc2626', cursor: 'pointer', display: 'inline-flex', padding: '3px' }}><Trash2 size={14} /></button>
            </div>)}
          </div>
        </div>
      </section>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', minWidth: 0 }}>
        <section style={panelStyle}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '12px', flexWrap: 'wrap' }}>
            <PanelTitle icon={<Database size={17} />} title="Salt Okunur Incident Bağlamı" />
            <button onClick={refreshSnapshot} disabled={busy !== null} style={secondaryButtonStyle}><RefreshCw size={14} className={busy === 'snapshot' ? 'spin' : ''} /> Yenile</button>
          </div>
          <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', margin: '12px 0' }}>
            <label style={compactLabel}>{analysisMode === 'comprehensive' ? 'Son' : 'Dönem'}<input type="number" min="1" max="366" value={lookbackDays} onChange={event => setLookbackDays(Number(event.target.value))} style={{ ...inputStyle, width: '72px' }} /> gün</label>
            <label style={compactLabel}>Başlangıç<input type="date" value={startDate} onChange={event => setStartDate(event.target.value)} style={{ ...inputStyle, width: '136px' }} /></label>
            <label style={compactLabel}>Bitiş<input type="date" value={endDate} onChange={event => setEndDate(event.target.value)} style={{ ...inputStyle, width: '136px' }} /></label>
            {analysisMode === 'standard' && <label style={compactLabel}>Örnek<input type="number" min="5" max="100" value={sampleSize} onChange={event => setSampleSize(Number(event.target.value))} style={{ ...inputStyle, width: '72px' }} /> kayıt</label>}
            {analysisMode === 'comprehensive' && <label style={compactLabel} title="Ayrıntılı olay geçmişi modele taşınacak öncelikli kullanıcı sayısı.">Ayrıntılı kullanıcı<input type="number" min="1" max="50" value={detailedUserLimit} onChange={event => setDetailedUserLimit(Number(event.target.value))} style={{ ...inputStyle, width: '66px' }} /></label>}
            {analysisMode === 'comprehensive' && <label style={compactLabel} title="Her kullanıcı için modele aktarılacak en fazla zaman çizelgesi satırı. Sunucudaki desen taraması tüm olaylarda çalışır.">Zaman çizelgesi<input type="number" min="25" max="500" step="25" value={evidenceRowsPerUser} onChange={event => setEvidenceRowsPerUser(Number(event.target.value))} style={{ ...inputStyle, width: '70px' }} /> satır</label>}
            <label style={{ ...compactLabel, cursor: 'pointer' }} title="Kullanıcı ve e-posta/hedef bilgilerini maskeleyerek modele gönderir."><input type="checkbox" checked={maskIdentifiers} onChange={event => setMaskIdentifiers(event.target.checked)} /> <EyeOff size={13} /> Kimlikleri maskele</label>
          </div>
          <div style={{ display: 'inline-flex', border: '1px solid var(--border)', borderRadius: '6px', overflow: 'hidden', marginBottom: '10px' }}>
            <button onClick={() => setAnalysisMode('standard')} style={{ border: 0, padding: '7px 10px', background: analysisMode === 'standard' ? '#e0e7ff' : 'var(--surface)', color: '#1e3a8a', cursor: 'pointer', fontSize: '12px', fontWeight: 650 }}>Hızlı Özet</button>
            <button onClick={() => setAnalysisMode('comprehensive')} style={{ border: 0, borderLeft: '1px solid var(--border)', padding: '7px 10px', background: analysisMode === 'comprehensive' ? '#d1fae5' : 'var(--surface)', color: '#065f46', cursor: 'pointer', fontSize: '12px', fontWeight: 650 }}>Kapsamlı Anomali Analizi</button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(120px, 1fr))', gap: '9px' }}>
            <Metric label="Olay" value={snapshot?.total_incidents ?? 0} /><Metric label="Kullanıcı" value={snapshot?.unique_users ?? 0} /><Metric label="Modele aktarılan profil" value={snapshot?.profile_count ?? 0} /><Metric label="En yüksek match" value={snapshot?.maximum_matches ?? 0} />
          </div>
          <p style={{ margin: '11px 0 0', color: 'var(--text-muted)', fontSize: '11px' }}><ShieldCheck size={12} style={{ verticalAlign: 'text-bottom' }} /> {analysisMode === 'comprehensive' ? `Tüm kullanıcılar dönem dağılımına dahil edilir. Öncelikli ${detailedUserLimit} kullanıcı için tüm olaylar sunucuda taranır; modele kullanıcı başına en fazla ${evidenceRowsPerUser} zaman çizelgesi satırı ve tam kanıt özeti aktarılır. Prompttaki saat bilgisiyle çapraz kanal desenleri ayrıca hesaplanır.` : `Model özet, dağılımlar ve en fazla ${sampleSize} olay örneğini görür.`} Veritabanı sorgulama veya güncelleme yetkisi yoktur.</p>
        </section>

        <section style={{ ...panelStyle, minHeight: '490px', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
            <PanelTitle icon={<Bot size={17} />} title="Yerel LLM Sohbeti" />
            <button onClick={() => void send(ALGORITHM_PROMPT)} disabled={!settings.enabled || busy !== null} style={secondaryButtonStyle}><Sparkles size={14} /> Risk Algoritması Oluştur</button>
          </div>
          <div style={{ marginTop: '8px', color: 'var(--text-muted)', fontSize: '11px' }}>{activeConversationId ? 'Seçili sohbet otomatik kaydedilir.' : 'İlk mesajınızla yeni bir sohbet oluşturulur.'}</div>
          <div ref={chatScrollRef} style={{ height: '380px', minHeight: '220px', margin: '12px 0', borderTop: '1px solid var(--border)', borderBottom: '1px solid var(--border)', padding: '12px 0', overflowY: 'auto', overscrollBehavior: 'contain' }}>
            {messages.length === 0 ? <div style={{ color: 'var(--text-muted)', fontSize: '13px', padding: '16px 0' }}>Model bağlantısını kaydedip etkinleştirdikten sonra risk faktörleri, eşikler veya doğrulama planı hakkında konuşabilirsiniz.</div> : messages.map((message, index) => <div key={`${message.role}-${index}`} style={{ margin: '0 0 12px', display: 'flex', justifyContent: message.role === 'user' ? 'flex-end' : 'flex-start' }}><div style={{ maxWidth: '88%', minWidth: 0, padding: '10px 12px', borderRadius: '7px', background: message.role === 'user' ? '#2563eb' : 'var(--surface-muted, #f1f5f9)', color: message.role === 'user' ? 'white' : 'var(--text-primary)', fontSize: '13px', lineHeight: 1.55 }}><MarkdownMessage content={message.content} /></div></div>)}
            {busy === 'chat' && <div style={{ color: 'var(--text-muted)', fontSize: '12px', display: 'flex', gap: '7px', alignItems: 'center' }}><Loader2 size={14} className="spin" /> Yerel model analiz ediyor...</div>}
          </div>
          {activeConversationId && mailProposals.length > 0 && <section style={{ borderTop: '1px solid var(--border)', paddingTop: '12px', marginBottom: '12px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '8px', marginBottom: '9px' }}>
              <PanelTitle icon={<Send size={16} />} title="Mail Taslakları" />
              <span style={{ color: '#a16207', fontSize: '12px', fontWeight: 650 }}>{mailProposals.filter(item => item.status === 'pending').length} onay bekliyor</span>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', maxHeight: '420px', overflowY: 'auto', paddingRight: '2px' }}>
              {mailProposals.map(proposal => {
                const editable = proposal.status === 'pending' || proposal.status === 'unresolved' || proposal.status === 'failed'
                const status = proposalStatusMeta(proposal.status)
                return <details key={proposal.id} style={{ border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface)' }}>
                  <summary style={{ cursor: 'pointer', padding: '10px 11px', display: 'flex', alignItems: 'center', gap: '8px', listStyle: 'none' }}>
                    <span style={{ width: '8px', height: '8px', flexShrink: 0, borderRadius: '50%', background: status.color }} />
                    <span style={{ flex: 1, minWidth: 0 }}><strong style={{ fontSize: '12px', color: 'var(--text-primary)' }}>{proposal.full_name || proposal.user_name}</strong><span style={{ color: 'var(--text-muted)', fontSize: '11px' }}> · {proposal.recipient_email || 'E-posta bulunamadı'}</span></span>
                    <span style={{ color: status.color, fontSize: '11px', fontWeight: 650 }}>{status.label}</span>
                  </summary>
                  <div style={{ borderTop: '1px solid var(--border)', padding: '11px' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: '8px', marginBottom: '8px' }}>
                      <label style={compactLabel}>Alıcı<input disabled={!editable || proposalBusyId === proposal.id} value={proposal.recipient_email || ''} onChange={event => updateProposal(proposal.id, { recipient_email: event.target.value })} style={inputStyle} /></label>
                      <label style={compactLabel}>Konu<input disabled={!editable || proposalBusyId === proposal.id} value={proposal.subject} onChange={event => updateProposal(proposal.id, { subject: event.target.value })} style={inputStyle} /></label>
                    </div>
                    <label style={{ ...labelStyle, marginTop: 0 }}>Mail metni<textarea disabled={!editable || proposalBusyId === proposal.id} value={proposal.body} onChange={event => updateProposal(proposal.id, { body: event.target.value })} style={{ ...inputStyle, minHeight: '150px', resize: 'vertical', lineHeight: 1.45 }} /></label>
                    <div style={{ color: 'var(--text-muted)', fontSize: '11px', margin: '8px 0' }}>{proposal.rationale || 'Incident bağlamına göre oluşturuldu.'}{proposal.error_message ? ` Hata: ${proposal.error_message}` : ''}</div>
                    {editable && <div style={{ display: 'flex', gap: '7px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                      <button onClick={() => void saveProposal(proposal)} disabled={proposalBusyId === proposal.id} style={secondaryButtonStyle}><Save size={13} /> Taslağı Kaydet</button>
                      {proposal.status === 'pending' && <><button onClick={() => void decideProposal(proposal, 'reject')} disabled={proposalBusyId === proposal.id} style={{ ...secondaryButtonStyle, color: '#b91c1c' }}>Reddet</button><button onClick={() => void decideProposal(proposal, 'approve')} disabled={proposalBusyId === proposal.id} style={primaryButtonStyle}><Send size={13} /> Onayla ve Gönder</button></>}
                    </div>}
                  </div>
                </details>
              })}
            </div>
          </section>}
          <form onSubmit={onSubmit} style={{ display: 'flex', gap: '9px', alignItems: 'flex-end' }}>
            <textarea value={input} onChange={event => setInput(event.target.value)} onKeyDown={onInputKeyDown} disabled={!settings.enabled || busy === 'chat'} placeholder="Örn. En yüksek riski açıklayan faktörleri ağırlıklarıyla öner." style={{ ...inputStyle, minHeight: '66px', resize: 'vertical', flex: 1 }} />
            <button type="submit" disabled={!settings.enabled || busy === 'chat' || !input.trim()} title="Gönder" style={{ ...primaryButtonStyle, height: '38px', width: '38px', justifyContent: 'center', padding: 0 }}><Send size={16} /></button>
          </form>
        </section>
      </div>
    </div>
  </div>
}

function PanelTitle({ icon, title }: { icon: ReactNode; title: string }) { return <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-primary)', fontWeight: 700, fontSize: '14px' }}>{icon}{title}</div> }
function Metric({ label, value }: { label: string; value: number }) { return <div style={{ background: 'var(--surface-muted, #f8fafc)', border: '1px solid var(--border)', padding: '9px 10px', borderRadius: '6px' }}><div style={{ color: 'var(--text-muted)', fontSize: '10px' }}>{label}</div><div style={{ color: 'var(--text-primary)', fontSize: '17px', fontWeight: 700, marginTop: '2px' }}>{Number(value).toLocaleString('tr-TR')}</div></div> }
function formatConversationTime(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}
function proposalStatusMeta(status: MailProposal['status']) {
  if (status === 'sent') return { label: 'Gönderildi', color: '#047857' }
  if (status === 'rejected') return { label: 'Reddedildi', color: '#64748b' }
  if (status === 'failed') return { label: 'Gönderim hatası', color: '#dc2626' }
  if (status === 'unresolved') return { label: 'E-posta bulunamadı', color: '#b45309' }
  return { label: 'Onay bekliyor', color: '#d97706' }
}

function MarkdownMessage({ content }: { content: string }) {
  const lines = content.replace(/\r\n/g, '\n').split('\n')
  const blocks: ReactNode[] = []
  let index = 0

  while (index < lines.length) {
    const line = lines[index]
    if (!line.trim()) {
      index += 1
      continue
    }

    if (/^\s*```/.test(line)) {
      const code: string[] = []
      index += 1
      while (index < lines.length && !/^\s*```/.test(lines[index])) code.push(lines[index++])
      if (index < lines.length) index += 1
      blocks.push(<pre key={`code-${index}`} style={{ margin: '8px 0', padding: '10px', overflowX: 'auto', borderRadius: '5px', background: 'rgba(15,23,42,.12)', fontSize: '12px', lineHeight: 1.45, whiteSpace: 'pre-wrap' }}>{code.join('\n')}</pre>)
      continue
    }

    const heading = line.match(/^(#{1,3})\s+(.+)$/)
    if (heading) {
      const size = heading[1].length === 1 ? '17px' : heading[1].length === 2 ? '15px' : '14px'
      blocks.push(<div key={`heading-${index}`} style={{ fontSize: size, fontWeight: 750, margin: '10px 0 6px' }}>{renderMarkdownInline(heading[2], `heading-${index}`)}</div>)
      index += 1
      continue
    }

    if (/^\s*((---+)|(\*\*\*+)|(___+))\s*$/.test(line)) {
      blocks.push(<hr key={`rule-${index}`} style={{ border: 0, borderTop: '1px solid currentColor', opacity: .24, margin: '12px 0' }} />)
      index += 1
      continue
    }

    if (isMarkdownTableLine(line) && index + 1 < lines.length && isMarkdownTableDivider(lines[index + 1])) {
      const header = splitMarkdownTableRow(line)
      index += 2
      const rows: string[][] = []
      while (index < lines.length && isMarkdownTableLine(lines[index])) rows.push(splitMarkdownTableRow(lines[index++]))
      blocks.push(<div key={`table-${index}`} style={{ margin: '9px 0', overflowX: 'auto', border: '1px solid currentColor', borderRadius: '5px', opacity: .96 }}><table style={{ width: '100%', minWidth: `${Math.max(header.length, 2) * 120}px`, borderCollapse: 'collapse', fontSize: '12px' }}><thead><tr>{header.map((cell, cellIndex) => <th key={`header-${cellIndex}`} style={{ textAlign: 'left', padding: '7px 8px', borderBottom: '1px solid currentColor', background: 'rgba(15,23,42,.09)', verticalAlign: 'top' }}>{renderMarkdownInline(cell, `table-header-${cellIndex}`)}</th>)}</tr></thead><tbody>{rows.map((row, rowIndex) => <tr key={`row-${rowIndex}`}>{header.map((_, cellIndex) => <td key={`cell-${rowIndex}-${cellIndex}`} style={{ padding: '7px 8px', borderTop: rowIndex ? '1px solid rgba(100,116,139,.2)' : 0, verticalAlign: 'top' }}>{renderMarkdownInline(row[cellIndex] ?? '', `table-${rowIndex}-${cellIndex}`)}</td>)}</tr>)}</tbody></table></div>)
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
      blocks.push(<List key={`list-${index}`} style={{ margin: '7px 0', paddingLeft: '21px' }}>{items.map((item, itemIndex) => <li key={`item-${itemIndex}`} style={{ margin: '3px 0' }}>{renderMarkdownInline(item, `list-${itemIndex}`)}</li>)}</List>)
      continue
    }

    const paragraph: string[] = []
    while (index < lines.length && lines[index].trim() && !/^\s*```/.test(lines[index]) && !/^(#{1,3})\s+/.test(lines[index]) && !/^\s*((---+)|(\*\*\*+)|(___+))\s*$/.test(lines[index]) && !(isMarkdownTableLine(lines[index]) && isMarkdownTableDivider(lines[index + 1] ?? '')) && !/^\s*[-*+]\s+/.test(lines[index]) && !/^\s*\d+[.)]\s+/.test(lines[index])) paragraph.push(lines[index++])
    blocks.push(<p key={`paragraph-${index}`} style={{ margin: '0 0 9px', whiteSpace: 'pre-wrap' }}>{paragraph.map((paragraphLine, lineIndex) => <span key={`line-${lineIndex}`}>{lineIndex > 0 && <br />}{renderMarkdownInline(paragraphLine, `paragraph-${lineIndex}`)}</span>)}</p>)
  }

  return <>{blocks}</>
}

function renderMarkdownInline(value: string, keyPrefix: string): ReactNode[] {
  return value.split(/(\*\*[^*]+\*\*|`[^`]+`)/g).filter(Boolean).map((part, index) => {
    if (part.startsWith('**') && part.endsWith('**')) return <strong key={`${keyPrefix}-bold-${index}`}>{part.slice(2, -2)}</strong>
    if (part.startsWith('`') && part.endsWith('`')) return <code key={`${keyPrefix}-code-${index}`} style={{ padding: '1px 4px', borderRadius: '3px', background: 'rgba(15,23,42,.11)', fontSize: '.92em' }}>{part.slice(1, -1)}</code>
    return <span key={`${keyPrefix}-text-${index}`}>{part}</span>
  })
}

function isMarkdownTableLine(line: string) { return /^\s*\|.*\|\s*$/.test(line) }
function isMarkdownTableDivider(line: string) { return /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(line) }
function splitMarkdownTableRow(line: string) { return line.trim().replace(/^\||\|$/g, '').split('|').map(cell => cell.trim()) }

const panelStyle: CSSProperties = { background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', boxShadow: '0 1px 2px rgba(15,23,42,.03)' }
const labelStyle: CSSProperties = { display: 'flex', flexDirection: 'column', gap: '5px', color: 'var(--text-secondary)', fontSize: '12px', fontWeight: 600, marginTop: '11px' }
const compactLabel: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', color: 'var(--text-secondary)', fontSize: '12px', fontWeight: 600 }
const inputStyle: CSSProperties = { width: '100%', boxSizing: 'border-box', padding: '8px 9px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px', fontFamily: 'inherit' }
const primaryButtonStyle: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', border: '1px solid #0f172a', background: '#0f172a', color: 'white', borderRadius: '6px', padding: '8px 10px', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }
const secondaryButtonStyle: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', borderRadius: '6px', padding: '8px 10px', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }
