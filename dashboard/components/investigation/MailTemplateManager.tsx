'use client'

import { useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
import { Mail, Plus, Pencil, Trash2, Save, X } from 'lucide-react'
import apiClient from '@/lib/axios'
import { MailTemplate, TEMPLATE_PLACEHOLDERS } from './types'

const inputStyle: CSSProperties = {
  width: '100%',
  padding: '8px 12px',
  border: '1px solid var(--border)',
  borderRadius: '6px',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontFamily: 'inherit',
  fontSize: '14px',
}

interface Props {
  /** Compact mode reduces vertical spacing when embedded inside another page. */
  compact?: boolean
}

export default function MailTemplateManager({ compact = false }: Props) {
  const [templates, setTemplates] = useState<MailTemplate[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<MailTemplate | null>(null)
  const [form, setForm] = useState({ name: '', subject: '', body: '' })
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const fetchTemplates = async () => {
    setLoading(true)
    try {
      const res = await apiClient.get('/api/mail-templates')
      setTemplates(Array.isArray(res.data) ? res.data : [])
    } catch (e) {
      setTemplates([])
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchTemplates()
  }, [])

  const showMessage = (type: 'success' | 'error', text: string) => {
    setMessage({ type, text })
    setTimeout(() => setMessage(null), 4000)
  }

  const startNew = () => {
    setEditing(null)
    setForm({ name: '', subject: '', body: '' })
  }

  const startEdit = (t: MailTemplate) => {
    setEditing(t)
    setForm({ name: t.name, subject: t.subject, body: t.body })
  }

  const save = async () => {
    if (!form.name.trim() || !form.subject.trim()) {
      showMessage('error', 'İsim ve konu zorunludur')
      return
    }
    setSaving(true)
    try {
      if (editing) {
        await apiClient.put(`/api/mail-templates/${editing.id}`, form)
        showMessage('success', 'Şablon güncellendi')
      } else {
        await apiClient.post('/api/mail-templates', form)
        showMessage('success', 'Şablon kaydedildi')
      }
      startNew()
      await fetchTemplates()
    } catch (e: any) {
      showMessage('error', e?.response?.data?.detail || 'Şablon kaydedilemedi')
    } finally {
      setSaving(false)
    }
  }

  const remove = async (t: MailTemplate) => {
    if (!confirm(`"${t.name}" şablonu silinsin mi?`)) return
    try {
      await apiClient.delete(`/api/mail-templates/${t.id}`)
      if (editing?.id === t.id) startNew()
      await fetchTemplates()
      showMessage('success', 'Şablon silindi')
    } catch (e: any) {
      showMessage('error', e?.response?.data?.detail || 'Şablon silinemedi')
    }
  }

  return (
    <div className="card" style={{ marginBottom: compact ? '0' : '24px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '16px' }}>
        <Mail size={20} style={{ color: 'var(--primary)' }} />
        <h2 style={{ margin: 0, fontSize: '16px', fontWeight: 600, color: 'var(--text-primary)' }}>Mail Şablonları</h2>
      </div>

      {message && (
        <div style={{
          padding: '10px 14px',
          borderRadius: '6px',
          marginBottom: '14px',
          background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
          color: message.type === 'success' ? '#166534' : '#991b1b',
          border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`,
        }}>
          {message.text}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.4fr', gap: '24px', alignItems: 'start' }}>
        {/* Template list */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-muted)' }}>Kayıtlı Şablonlar</span>
            <button onClick={startNew} style={btnGhost}>
              <Plus size={14} /> Yeni
            </button>
          </div>
          {loading ? (
            <div style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Yükleniyor...</div>
          ) : templates.length === 0 ? (
            <div style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Henüz şablon yok.</div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {templates.map(t => (
                <div
                  key={t.id}
                  style={{
                    border: `1px solid ${editing?.id === t.id ? 'var(--primary)' : 'var(--border)'}`,
                    borderRadius: '8px',
                    padding: '10px 12px',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: '8px',
                  }}
                >
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontWeight: 600, fontSize: '14px', color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{t.name}</div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{t.subject}</div>
                  </div>
                  <div style={{ display: 'flex', gap: '6px', flexShrink: 0 }}>
                    <button onClick={() => startEdit(t)} title="Düzenle" style={iconBtn}><Pencil size={15} /></button>
                    <button onClick={() => remove(t)} title="Sil" style={{ ...iconBtn, color: '#dc2626' }}><Trash2 size={15} /></button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Editor */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-muted)' }}>
              {editing ? `Düzenle: ${editing.name}` : 'Yeni Şablon'}
            </span>
            {editing && (
              <button onClick={startNew} style={btnGhost}><X size={14} /> İptal</button>
            )}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <div>
              <label style={labelStyle}>Şablon Adı</label>
              <input style={inputStyle} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="Örn. Şahsi Mail Uyarısı" />
            </div>
            <div>
              <label style={labelStyle}>Konu</label>
              <input style={inputStyle} value={form.subject} onChange={e => setForm({ ...form, subject: e.target.value })} placeholder="DLP - Aktivite Sorgusu" />
            </div>
            <div>
              <label style={labelStyle}>İçerik (HTML destekler)</label>
              <textarea
                style={{ ...inputStyle, minHeight: '180px', resize: 'vertical', fontFamily: 'inherit' }}
                value={form.body}
                onChange={e => setForm({ ...form, body: e.target.value })}
                placeholder="Sayın {{tam_ad}}, ..."
              />
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
              Kullanılabilir alanlar:{' '}
              {TEMPLATE_PLACEHOLDERS.map(p => (
                <code key={p.token} title={p.desc} style={{ marginRight: '6px', background: 'var(--surface-hover)', padding: '1px 5px', borderRadius: '4px' }}>{p.token}</code>
              ))}
            </div>
            <div>
              <button onClick={save} disabled={saving} style={{ ...btnPrimary, opacity: saving ? 0.6 : 1 }}>
                <Save size={15} /> {saving ? 'Kaydediliyor...' : (editing ? 'Güncelle' : 'Kaydet')}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

const labelStyle: CSSProperties = { display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px', color: 'var(--text-primary)' }
const btnPrimary: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '9px 18px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '14px' }
const btnGhost: CSSProperties = { display: 'inline-flex', alignItems: 'center', gap: '4px', padding: '5px 10px', background: 'transparent', color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', fontSize: '12px' }
const iconBtn: CSSProperties = { display: 'inline-flex', alignItems: 'center', justifyContent: 'center', padding: '6px', background: 'transparent', color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer' }
