'use client'

import { useCallback, useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
import { MailPlus, RefreshCw, Send } from 'lucide-react'
import apiClient from '@/lib/axios'
import { MailTemplate, toEmailHtml } from './types'

const inputStyle: CSSProperties = {
  width: '100%',
  padding: '9px 12px',
  border: '1px solid var(--border)',
  borderRadius: '6px',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontFamily: 'inherit',
  fontSize: '14px',
}

const labelStyle: CSSProperties = {
  display: 'block',
  marginBottom: '6px',
  fontWeight: 500,
  fontSize: '13px',
  color: 'var(--text-primary)',
}

function applyManualPlaceholders(text: string, recipient: string): string {
  return (text || '')
    .replaceAll('{{kullanici}}', recipient)
    .replaceAll('{{tam_ad}}', recipient)
    .replaceAll('{{takim}}', '-')
    .replaceAll('{{tarih}}', new Date().toLocaleDateString('tr-TR'))
    .replaceAll('{{destination}}', '-')
    .replaceAll('{{hedef}}', '-')
    .replaceAll('{{kanal}}', '-')
    .replaceAll('{{channel}}', '-')
    .replaceAll('{{policy}}', '-')
    .replaceAll('{{kural}}', '-')
    .replaceAll('{{max_match}}', '-')
    .replaceAll('{{max_matches}}', '-')
    .replaceAll('{{olaylar}}', '-')
}

export default function ManualMailSender() {
  const [templates, setTemplates] = useState<MailTemplate[]>([])
  const [templatesLoading, setTemplatesLoading] = useState(false)
  const [selectedId, setSelectedId] = useState('')
  const [recipient, setRecipient] = useState('')
  const [ccEmail, setCcEmail] = useState('')
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [sending, setSending] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const fetchTemplates = useCallback(async () => {
    setTemplatesLoading(true)
    try {
      const res = await apiClient.get('/api/mail-templates')
      setTemplates(Array.isArray(res.data) ? res.data : [])
    } catch {
      setTemplates([])
      setMessage({ type: 'error', text: 'Mail şablonları yüklenemedi' })
    } finally {
      setTemplatesLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchTemplates()
  }, [fetchTemplates])

  const isValidEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())

  const selectTemplate = (id: string) => {
    setSelectedId(id)
    const template = templates.find(t => String(t.id) === id)
    if (!template) {
      setSubject('')
      setBody('')
      return
    }

    setSubject(template.subject)
    setBody(template.body)
    setMessage(null)
  }

  const send = async () => {
    if (!isValidEmail(recipient)) {
      setMessage({ type: 'error', text: 'Geçerli bir alıcı e-posta adresi girin' })
      return
    }
    if (!selectedId) {
      setMessage({ type: 'error', text: 'Bir mail şablonu seçin' })
      return
    }
    if (!subject.trim()) {
      setMessage({ type: 'error', text: 'Konu zorunludur' })
      return
    }
    if (ccEmail.trim() && !isValidEmail(ccEmail)) {
      setMessage({ type: 'error', text: 'Geçerli bir CC e-posta adresi girin' })
      return
    }

    setSending(true)
    setMessage(null)
    try {
      const normalizedRecipient = recipient.trim()
      const res = await apiClient.post('/api/investigation/send-mail', {
        to_email: normalizedRecipient,
        to_name: null,
        cc_admin: false,
        cc_email: ccEmail.trim() || null,
        subject: applyManualPlaceholders(subject, normalizedRecipient),
        body_html: toEmailHtml(applyManualPlaceholders(body, normalizedRecipient)),
      })
      setMessage({ type: 'success', text: res.data?.message || 'Mail gönderildi' })
      setRecipient('')
      setCcEmail('')
      setSelectedId('')
      setSubject('')
      setBody('')
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Mail gönderilemedi' })
    } finally {
      setSending(false)
    }
  }

  return (
    <div className="card" style={{ marginTop: '24px', marginBottom: '24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: '12px', alignItems: 'flex-start', marginBottom: '18px' }}>
        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          <div style={{ width: '40px', height: '40px', borderRadius: '10px', background: 'linear-gradient(135deg, #0ea5e9, #2563eb)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white' }}>
            <MailPlus size={20} />
          </div>
          <div>
            <h2 style={{ margin: 0, color: 'var(--text-primary)', fontSize: '16px', fontWeight: 600 }}>Manuel Mail Gönderimi</h2>
            <p style={{ margin: '3px 0 0', color: 'var(--text-muted)', fontSize: '13px' }}>Listede olmayan bir adrese kayıtlı şablonlardan biriyle mail gönderin.</p>
          </div>
        </div>
        <button
          onClick={fetchTemplates}
          disabled={templatesLoading}
          title="Şablonları yenile"
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '7px 11px', background: 'var(--surface)', color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: '6px', cursor: templatesLoading ? 'not-allowed' : 'pointer' }}
        >
          <RefreshCw size={14} style={{ animation: templatesLoading ? 'spin 1s linear infinite' : undefined }} /> Şablonları Yenile
        </button>
      </div>

      {message && (
        <div style={{ padding: '10px 14px', borderRadius: '6px', marginBottom: '14px', background: message.type === 'success' ? '#dcfce7' : '#fee2e2', color: message.type === 'success' ? '#166534' : '#991b1b', border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}` }}>
          {message.text}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '14px' }}>
        <div>
          <label style={labelStyle}>Alıcı</label>
          <input type="email" style={inputStyle} value={recipient} onChange={e => setRecipient(e.target.value)} placeholder="alici@firma.com" />
        </div>
        <div>
          <label style={labelStyle}>Şablon</label>
          <select style={inputStyle} value={selectedId} onChange={e => selectTemplate(e.target.value)} disabled={templatesLoading}>
            <option value="">— Şablon seçin —</option>
            {templates.map(template => <option key={template.id} value={String(template.id)}>{template.name}</option>)}
          </select>
        </div>
        <div>
          <label style={labelStyle}>CC <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>(opsiyonel)</span></label>
          <input type="email" style={inputStyle} value={ccEmail} onChange={e => setCcEmail(e.target.value)} placeholder="cc@firma.com" />
        </div>
      </div>

      {selectedId && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', marginTop: '14px' }}>
          <div>
            <label style={labelStyle}>Konu</label>
            <input style={inputStyle} value={subject} onChange={e => setSubject(e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>İçerik</label>
            <textarea style={{ ...inputStyle, minHeight: '150px', resize: 'vertical' }} value={body} onChange={e => setBody(e.target.value)} />
          </div>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
        <button
          onClick={send}
          disabled={sending || templatesLoading}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '7px', padding: '9px 20px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: sending || templatesLoading ? 'not-allowed' : 'pointer', opacity: sending || templatesLoading ? 0.6 : 1 }}
        >
          <Send size={15} /> {sending ? 'Gönderiliyor...' : 'Gönder'}
        </button>
      </div>
    </div>
  )
}
