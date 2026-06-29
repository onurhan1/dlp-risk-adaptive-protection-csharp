'use client'

import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import { X, Send, Eye, ArrowLeft } from 'lucide-react'
import apiClient from '@/lib/axios'
import { MailTemplate, WeeklyFlagUser, applyPlaceholders } from './types'

interface Props {
  user: WeeklyFlagUser
  onClose: () => void
}

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
const labelStyle: CSSProperties = { display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px', color: 'var(--text-primary)' }

function PreviewRow({ label, value, last }: { label: string; value: string; last?: boolean }) {
  return (
    <div style={{ display: 'flex', gap: '12px', padding: '10px 14px', borderBottom: last ? 'none' : '1px solid var(--border)', fontSize: '13px' }}>
      <span style={{ width: '60px', flexShrink: 0, fontWeight: 600, color: 'var(--text-muted)' }}>{label}</span>
      <span style={{ color: 'var(--text-primary)', wordBreak: 'break-word' }}>{value}</span>
    </div>
  )
}

export default function SendMailModal({ user, onClose }: Props) {
  const [templates, setTemplates] = useState<MailTemplate[]>([])
  const [selectedId, setSelectedId] = useState<string>('')
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [ccAdmin, setCcAdmin] = useState(true)
  const [sending, setSending] = useState(false)
  const [preview, setPreview] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  useEffect(() => {
    apiClient.get('/api/mail-templates')
      .then(res => setTemplates(Array.isArray(res.data) ? res.data : []))
      .catch(() => setTemplates([]))
  }, [])

  const recipient = user.contact_email || user.user_email

  const applyTemplate = (id: string) => {
    setSelectedId(id)
    const tpl = templates.find(t => String(t.id) === id)
    if (tpl) {
      setSubject(applyPlaceholders(tpl.subject, user))
      setBody(applyPlaceholders(tpl.body, user))
    }
  }

  const openPreview = () => {
    if (!subject.trim()) {
      setMessage({ type: 'error', text: 'Konu zorunludur' })
      return
    }
    setMessage(null)
    setPreview(true)
  }

  const send = async () => {
    setSending(true)
    setMessage(null)
    try {
      const res = await apiClient.post('/api/investigation/send-mail', {
        to_email: recipient,
        to_name: user.user_email,
        cc_admin: ccAdmin,
        subject,
        body_html: body,
      })
      setMessage({ type: 'success', text: res.data?.message || 'Mail gönderildi' })
      setTimeout(onClose, 1200)
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Mail gönderilemedi' })
    } finally {
      setSending(false)
    }
  }

  const templateOptions = useMemo(() => templates.map(t => ({ value: String(t.id), label: t.name })), [templates])

  return (
    <div
      onClick={onClose}
      style={{
        position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.55)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '20px',
      }}
    >
      <div
        onClick={e => e.stopPropagation()}
        style={{
          background: 'var(--surface)', borderRadius: '14px', width: '100%', maxWidth: '640px',
          maxHeight: '90vh', overflowY: 'auto', boxShadow: '0 12px 40px rgba(15,23,42,0.25)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '18px 22px', borderBottom: '1px solid var(--border)' }}>
          <h2 style={{ margin: 0, fontSize: '17px', fontWeight: 600, color: 'var(--text-primary)' }}>{preview ? 'Gönderim Önizlemesi' : 'Mail Gönder'}</h2>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-muted)' }}><X size={20} /></button>
        </div>

        <div style={{ padding: '20px 22px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
          {message && (
            <div style={{
              padding: '10px 14px', borderRadius: '6px',
              background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
              color: message.type === 'success' ? '#166534' : '#991b1b',
              border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`,
            }}>{message.text}</div>
          )}

          {!preview ? (
            <>
              <div>
                <label style={labelStyle}>Alıcı</label>
                <input style={{ ...inputStyle, opacity: 0.8 }} value={recipient} readOnly />
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{user.user_email}{user.team ? ` · ${user.team}` : ''}</div>
              </div>

              <div>
                <label style={labelStyle}>Şablon Seç</label>
                <select style={inputStyle} value={selectedId} onChange={e => applyTemplate(e.target.value)}>
                  <option value="">— Şablon seçin (opsiyonel) —</option>
                  {templateOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>

              <div>
                <label style={labelStyle}>Konu</label>
                <input style={inputStyle} value={subject} onChange={e => setSubject(e.target.value)} placeholder="Mail konusu" />
              </div>

              <div>
                <label style={labelStyle}>İçerik</label>
                <textarea style={{ ...inputStyle, minHeight: '200px', resize: 'vertical' }} value={body} onChange={e => setBody(e.target.value)} placeholder="Mail içeriği (HTML)" />
              </div>

              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--text-primary)' }}>
                <input type="checkbox" checked={ccAdmin} onChange={e => setCcAdmin(e.target.checked)} />
                Admin / güvenlik adresini CC'ye ekle
              </label>
            </>
          ) : (
            <>
              <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                <PreviewRow label="Alıcı" value={`${recipient}${user.user_email !== recipient ? ` (${user.user_email}${user.team ? ` · ${user.team}` : ''})` : (user.team ? ` (${user.team})` : '')}`} />
                <PreviewRow label="CC" value={ccAdmin ? 'Admin / güvenlik adresi eklenecek' : '—'} />
                <PreviewRow label="Konu" value={subject} last />
              </div>

              <div>
                <label style={labelStyle}>İçerik Önizlemesi</label>
                <div
                  style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '14px 16px', background: 'white', color: '#0f172a', fontSize: '14px', minHeight: '120px', maxHeight: '320px', overflowY: 'auto', wordBreak: 'break-word' }}
                  dangerouslySetInnerHTML={{ __html: body || '<em style="color:#94a3b8">İçerik boş</em>' }}
                />
              </div>

              <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                Mail yukarıdaki haliyle gönderilecektir. Düzenlemek için "Geri Dön"e basın.
              </div>
            </>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', padding: '16px 22px', borderTop: '1px solid var(--border)' }}>
          {!preview ? (
            <>
              <button onClick={onClose} style={{ padding: '9px 18px', background: 'transparent', color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer' }}>İptal</button>
              <button onClick={openPreview} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '9px 20px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer' }}>
                <Eye size={15} /> Önizle
              </button>
            </>
          ) : (
            <>
              <button onClick={() => setPreview(false)} disabled={sending} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '9px 18px', background: 'transparent', color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: '6px', cursor: sending ? 'not-allowed' : 'pointer' }}>
                <ArrowLeft size={15} /> Geri Dön
              </button>
              <button onClick={send} disabled={sending} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '9px 20px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: sending ? 'not-allowed' : 'pointer', opacity: sending ? 0.6 : 1 }}>
                <Send size={15} /> {sending ? 'Gönderiliyor...' : 'Onayla ve Gönder'}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
