'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import type { CSSProperties, FormEvent, KeyboardEvent, ReactNode } from 'react'
import { Bot, Database, EyeOff, Loader2, PlugZap, RefreshCw, Save, Send, ShieldCheck, Sparkles } from 'lucide-react'
import apiClient, { LONG_REQUEST_TIMEOUT_MS } from '@/lib/axios'

type Settings = { enabled: boolean; generate_url: string; model: string; temperature: number; max_tokens: number }
type Count = { name: string; count: number }
type Snapshot = {
  start_utc: string; end_utc: string; total_incidents: number; unique_users: number
  maximum_matches: number; actions: Count[]; channels: Count[]; policies: Count[]; users: unknown[]; samples: unknown[]
}
type ChatMessage = { role: 'user' | 'assistant'; content: string }

const INITIAL_SETTINGS: Settings = {
  enabled: false,
  generate_url: 'http://127.0.0.1:11434/api/generate',
  model: 'qwen3:8b',
  temperature: 0.2,
  max_tokens: 1800,
}

const ALGORITHM_PROMPT = 'Seçili incident verisini incele. RADARın mevcut risk puanını kullanmadan, yalnızca ham olay özelliklerinden 0-100 aralığında denetlenebilir bir risk puanlama algoritması ve riskli kullanıcı sınıflandırması öner. Faktörleri, ağırlıkları, eşikleri, örnek normalizasyonları, doğrulama planını ve sınırlılıkları ver. Sonunda öneriyi JSON olarak özetle.'

export default function LocalLlmLabPage() {
  const [settings, setSettings] = useState<Settings>(INITIAL_SETTINGS)
  const [lookbackDays, setLookbackDays] = useState(30)
  const [sampleSize, setSampleSize] = useState(40)
  const [maskIdentifiers, setMaskIdentifiers] = useState(true)
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState<'save' | 'test' | 'chat' | 'snapshot' | null>(null)
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const snapshotParams = useMemo(() => ({ lookbackDays, sampleSize, maskIdentifiers }), [lookbackDays, sampleSize, maskIdentifiers])

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
        const [settingsResponse, snapshotResponse] = await Promise.all([
          apiClient.get('/api/local-llm-lab/settings'),
          apiClient.get('/api/local-llm-lab/snapshot', { params: snapshotParams }),
        ])
        setSettings({ ...INITIAL_SETTINGS, ...settingsResponse.data })
        setSnapshot(snapshotResponse.data)
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
      const response = await apiClient.post('/api/local-llm-lab/test', settings, { timeout: LONG_REQUEST_TIMEOUT_MS })
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
      const response = await apiClient.post('/api/local-llm-lab/chat', {
        message: content,
        history: messages,
        lookbackDays,
        sampleSize,
        maskIdentifiers,
      }, { timeout: LONG_REQUEST_TIMEOUT_MS })
      setMessages(current => [...current, { role: 'assistant', content: response.data.reply || 'Model boş yanıt verdi.' }])
      setSnapshot(response.data.snapshot)
    } catch (error: any) {
      setMessages(current => current.filter((item, index) => !(index === current.length - 1 && item.role === 'user' && item.content === content)))
      setNotice({ type: 'error', text: error?.response?.data?.detail || 'Model yanıtı alınamadı.' })
    } finally { setBusy(null) }
  }

  const onSubmit = (event: FormEvent) => { event.preventDefault(); void send() }
  const onInputKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void send() }
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
      </section>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', minWidth: 0 }}>
        <section style={panelStyle}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '12px', flexWrap: 'wrap' }}>
            <PanelTitle icon={<Database size={17} />} title="Salt Okunur Incident Bağlamı" />
            <button onClick={refreshSnapshot} disabled={busy !== null} style={secondaryButtonStyle}><RefreshCw size={14} className={busy === 'snapshot' ? 'spin' : ''} /> Yenile</button>
          </div>
          <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', margin: '12px 0' }}>
            <label style={compactLabel}>Dönem<input type="number" min="1" max="180" value={lookbackDays} onChange={event => setLookbackDays(Number(event.target.value))} style={{ ...inputStyle, width: '72px' }} /> gün</label>
            <label style={compactLabel}>Örnek<input type="number" min="5" max="100" value={sampleSize} onChange={event => setSampleSize(Number(event.target.value))} style={{ ...inputStyle, width: '72px' }} /> kayıt</label>
            <label style={{ ...compactLabel, cursor: 'pointer' }} title="Kullanıcı ve e-posta/hedef bilgilerini maskeleyerek modele gönderir."><input type="checkbox" checked={maskIdentifiers} onChange={event => setMaskIdentifiers(event.target.checked)} /> <EyeOff size={13} /> Kimlikleri maskele</label>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(120px, 1fr))', gap: '9px' }}>
            <Metric label="Olay" value={snapshot?.total_incidents ?? 0} /><Metric label="Kullanıcı" value={snapshot?.unique_users ?? 0} /><Metric label="Kullanıcı profili" value={snapshot?.users?.length ?? 0} /><Metric label="En yüksek match" value={snapshot?.maximum_matches ?? 0} />
          </div>
          <p style={{ margin: '11px 0 0', color: 'var(--text-muted)', fontSize: '11px' }}><ShieldCheck size={12} style={{ verticalAlign: 'text-bottom' }} /> Model yalnızca bu özet, dağılımlar ve en fazla {sampleSize} olay örneğini görür. Veritabanı sorgulama veya güncelleme yetkisi yoktur.</p>
        </section>

        <section style={{ ...panelStyle, minHeight: '490px', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
            <PanelTitle icon={<Bot size={17} />} title="Risk Modeli Sohbeti" />
            <button onClick={() => void send(ALGORITHM_PROMPT)} disabled={!settings.enabled || busy !== null} style={secondaryButtonStyle}><Sparkles size={14} /> Risk Algoritması Oluştur</button>
          </div>
          <div style={{ flex: 1, minHeight: '290px', margin: '12px 0', borderTop: '1px solid var(--border)', borderBottom: '1px solid var(--border)', padding: '12px 0', overflowY: 'auto' }}>
            {messages.length === 0 ? <div style={{ color: 'var(--text-muted)', fontSize: '13px', padding: '16px 0' }}>Model bağlantısını kaydedip etkinleştirdikten sonra risk faktörleri, eşikler veya doğrulama planı hakkında konuşabilirsiniz.</div> : messages.map((message, index) => <div key={`${message.role}-${index}`} style={{ margin: '0 0 12px', display: 'flex', justifyContent: message.role === 'user' ? 'flex-end' : 'flex-start' }}><div style={{ maxWidth: '88%', whiteSpace: 'pre-wrap', padding: '10px 12px', borderRadius: '7px', background: message.role === 'user' ? '#2563eb' : 'var(--surface-muted, #f1f5f9)', color: message.role === 'user' ? 'white' : 'var(--text-primary)', fontSize: '13px', lineHeight: 1.55 }}>{message.content}</div></div>)}
            {busy === 'chat' && <div style={{ color: 'var(--text-muted)', fontSize: '12px', display: 'flex', gap: '7px', alignItems: 'center' }}><Loader2 size={14} className="spin" /> Yerel model analiz ediyor...</div>}
          </div>
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

const panelStyle: CSSProperties = { background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', boxShadow: '0 1px 2px rgba(15,23,42,.03)' }
const labelStyle: CSSProperties = { display: 'flex', flexDirection: 'column', gap: '5px', color: 'var(--text-secondary)', fontSize: '12px', fontWeight: 600, marginTop: '11px' }
const compactLabel: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', color: 'var(--text-secondary)', fontSize: '12px', fontWeight: 600 }
const inputStyle: CSSProperties = { width: '100%', boxSizing: 'border-box', padding: '8px 9px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px', fontFamily: 'inherit' }
const primaryButtonStyle: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', border: '1px solid #0f172a', background: '#0f172a', color: 'white', borderRadius: '6px', padding: '8px 10px', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }
const secondaryButtonStyle: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', borderRadius: '6px', padding: '8px 10px', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }
