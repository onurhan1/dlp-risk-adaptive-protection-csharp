'use client'

import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import { X, Send, Eye, ArrowLeft } from 'lucide-react'
import apiClient from '@/lib/axios'
import { MailTemplate, WeeklyFlagUser, applyPlaceholders, toEmailHtml } from './types'

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
  const defaultRecipient = user.contact_email || user.user_email
  const [templates, setTemplates] = useState<MailTemplate[]>([])
  const [selectedId, setSelectedId] = useState<string>('')
  const [recipient, setRecipient] = useState(defaultRecipient)
  const [ccEmail, setCcEmail] = useState('')
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [sending, setSending] = useState(false)
  const [preview, setPreview] = useState(false)
  const [selectedIncidentIndex, setSelectedIncidentIndex] = useState(0)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  useEffect(() => {
    apiClient.get('/api/mail-templates')
      .then(res => setTemplates(Array.isArray(res.data) ? res.data : []))
      .catch(() => setTemplates([]))

    apiClient.get('/api/settings')
      .then(res => setCcEmail(res.data?.admin_email || ''))
      .catch(() => setCcEmail(''))
  }, [])

  const isValidEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())

  const incidentOptions = useMemo(() => user.sample_incidents || [], [user.sample_incidents])
  const templateUser = useMemo<WeeklyFlagUser>(() => {
    if (incidentOptions.length === 0) return user
    const safeIndex = Math.min(Math.max(selectedIncidentIndex, 0), incidentOptions.length - 1)
    const selectedIncident = incidentOptions[safeIndex]
    return {
      ...user,
      sample_incidents: [
        selectedIncident,
        ...incidentOptions.filter((_, index) => index !== safeIndex),
      ],
      first_seen: selectedIncident.timestamp || user.first_seen,
      last_seen: selectedIncident.timestamp || user.last_seen,
    }
  }, [incidentOptions, selectedIncidentIndex, user])

  const applyTemplate = (id: string, currentUser = templateUser) => {
    setSelectedId(id)
    const tpl = templates.find(t => String(t.id) === id)
    if (tpl) {
      setSubject(applyPlaceholders(tpl.subject, currentUser))
      setBody(applyPlaceholders(tpl.body, currentUser))
    }
  }

  const changeIncident = (value: string) => {
    const nextIndex = Number(value) || 0
    setSelectedIncidentIndex(nextIndex)
    if (!selectedId) return

    const selectedIncident = incidentOptions[nextIndex]
    const nextUser = selectedIncident
      ? {
          ...user,
          sample_incidents: [
            selectedIncident,
            ...incidentOptions.filter((_, index) => index !== nextIndex),
          ],
          first_seen: selectedIncident.timestamp || user.first_seen,
          last_seen: selectedIncident.timestamp || user.last_seen,
        }
      : user
    applyTemplate(selectedId, nextUser)
  }

  const openPreview = () => {
    if (!isValidEmail(recipient)) {
      setMessage({ type: 'error', text: 'Geçerli bir alıcı e-posta adresi girin' })
      return
    }
    if (!subject.trim()) {
      setMessage({ type: 'error', text: 'Konu zorunludur' })
      return
    }
    setMessage(null)
    setPreview(true)
  }

  const send = async () => {
    if (!isValidEmail(recipient)) {
      setMessage({ type: 'error', text: 'Geçerli bir alıcı e-posta adresi girin' })
      return
    }
    if (ccEmail.trim() && !isValidEmail(ccEmail)) {
      setMessage({ type: 'error', text: 'Geçerli bir CC e-posta adresi girin' })
      return
    }

    setSending(true)
    setMessage(null)
    try {
      const res = await apiClient.post('/api/investigation/send-mail', {
        to_email: recipient.trim(),
        to_name: recipient.trim().toLowerCase() === defaultRecipient.toLowerCase() ? (user.full_name || user.user_email) : null,
        user_code: user.user_email?.includes('@') ? null : user.user_email,
        cc_admin: false,
        cc_email: ccEmail.trim() || null,
        subject,
        body_html: toEmailHtml(body),
        incident_timestamp: templateUser.sample_incidents?.[0]?.timestamp || null,
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
                <input type="email" style={inputStyle} value={recipient} onChange={e => setRecipient(e.target.value)} placeholder="alici@firma.com" />
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{user.user_email}{user.team ? ` · ${user.team}` : ''}</div>
              </div>

              {incidentOptions.length > 0 && (
                <div>
                  <label style={labelStyle}>Olay Kaydi</label>
                  <select style={inputStyle} value={selectedIncidentIndex} onChange={e => changeIncident(e.target.value)}>
                    {incidentOptions.map((incident, index) => (
                      <option key={`${incident.timestamp}-${index}`} value={index}>
                        {new Date(incident.timestamp).toLocaleString('tr-TR')} | {incident.policy || '-'} | Max Match: {incident.max_matches ?? '-'}
                      </option>
                    ))}
                  </select>
                </div>
              )}

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

            </>
          ) : (
            <>
              <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                <PreviewRow label="Alıcı" value={`${recipient}${user.user_email !== recipient ? ` (${user.user_email}${user.team ? ` · ${user.team}` : ''})` : (user.team ? ` (${user.team})` : '')}`} />
                <PreviewRow label="Konu" value={subject} last />
              </div>

              <div>
                <label style={labelStyle}>CC <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>(opsiyonel)</span></label>
                <input type="email" style={inputStyle} value={ccEmail} onChange={e => setCcEmail(e.target.value)} placeholder="cc@firma.com" />
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>Bu adres yalnızca bu gönderim için kullanılacaktır.</div>
              </div>

              <div>
                <label style={labelStyle}>İçerik Önizlemesi</label>
                <div
                  style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '14px 16px', background: 'white', color: '#0f172a', fontSize: '14px', minHeight: '120px', maxHeight: '320px', overflowY: 'auto', wordBreak: 'break-word' }}
                  dangerouslySetInnerHTML={{ __html: toEmailHtml(body) || '<em style="color:#94a3b8">İçerik boş</em>' }}
                />
              </div>

              <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                Alıcı, konu veya içeriği düzenlemek için "Geri Dön"e basın. CC adresini bu ekranda değiştirebilirsiniz.
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
