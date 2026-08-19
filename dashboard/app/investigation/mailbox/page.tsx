'use client'

import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { AlertCircle, CheckCircle2, Inbox, Mail, RefreshCw, Search, Settings, XCircle } from 'lucide-react'
import apiClient from '@/lib/axios'
import MailBodyView from '@/components/investigation/MailBodyView'

interface ImapSettings {
  enabled: boolean
  host: string
  port: number
  enable_ssl: boolean
  username: string
  password_set: boolean
  folder: string
  unread_only: boolean
  lookback_days: number
  max_messages: number
  is_configured: boolean
}

interface ImapInboxMessage {
  id: string
  from: string
  subject: string
  date: string
  unread: boolean
  size: number
}

interface ImapMessageContent extends ImapInboxMessage {
  content_type: string
  body_text: string
  truncated: boolean
  message?: string
}

const inputStyle = {
  height: 36,
  padding: '8px 11px',
  border: '1px solid var(--border)',
  borderRadius: 6,
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontSize: 13,
  outline: 'none',
} as const

const buttonStyle = {
  height: 36,
  padding: '0 12px',
  borderRadius: 6,
  border: '1px solid var(--border)',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  cursor: 'pointer',
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 7,
  fontSize: 13,
  fontWeight: 700,
} as const

export default function MailboxPage() {
  const [settings, setSettings] = useState<ImapSettings | null>(null)
  const [messages, setMessages] = useState<ImapInboxMessage[]>([])
  const [selected, setSelected] = useState<ImapMessageContent | null>(null)
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [messageLoadingId, setMessageLoadingId] = useState<string | null>(null)
  const [folder, setFolder] = useState('INBOX')
  const [lookbackDays, setLookbackDays] = useState(7)
  const [unreadOnly, setUnreadOnly] = useState(true)
  const [search, setSearch] = useState('')
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const filteredMessages = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return messages
    return messages.filter((mail) =>
      [mail.from, mail.subject, mail.date].some((value) => (value || '').toLowerCase().includes(q))
    )
  }, [messages, search])

  useEffect(() => {
    void loadSettingsAndInbox()
  }, [])

  const loadSettingsAndInbox = async () => {
    setLoading(true)
    try {
      const { data } = await apiClient.get('/api/settings/imap')
      const loaded: ImapSettings = {
        enabled: Boolean(data.enabled),
        host: data.host || '',
        port: Number(data.port) || 993,
        enable_ssl: data.enable_ssl ?? true,
        username: data.username || '',
        password_set: Boolean(data.password_set),
        folder: data.folder || 'INBOX',
        unread_only: data.unread_only ?? true,
        lookback_days: Number(data.lookback_days) || 7,
        max_messages: Number(data.max_messages) || 500,
        is_configured: Boolean(data.is_configured),
      }
      setSettings(loaded)
      setFolder(loaded.folder || 'INBOX')
      setLookbackDays(loaded.lookback_days || 7)
      setUnreadOnly(loaded.unread_only)
      if (loaded.is_configured) await fetchInbox(loaded, loaded.folder || 'INBOX', loaded.lookback_days || 7, loaded.unread_only)
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'IMAP ayarlari okunamadi')
    } finally {
      setLoading(false)
    }
  }

  const fetchInbox = async (
    currentSettings = settings,
    currentFolder = folder,
    currentLookbackDays = lookbackDays,
    currentUnreadOnly = unreadOnly,
  ) => {
    if (!currentSettings?.is_configured) return
    setRefreshing(true)
    try {
      const { data } = await apiClient.post('/api/settings/imap/inbox', {
        ...currentSettings,
        folder: currentFolder.trim() || 'INBOX',
        lookback_days: currentLookbackDays,
        unread_only: currentUnreadOnly,
        preview_count: 50,
      }, { timeout: 30000 })
      setMessages(Array.isArray(data.messages) ? data.messages : [])
      flash(data.success ? 'success' : 'error', data.message || 'Mail kutusu yenilendi')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'Mail kutusu okunamadi')
    } finally {
      setRefreshing(false)
    }
  }

  const viewMessage = async (mail: ImapInboxMessage) => {
    if (!settings) return
    setMessageLoadingId(mail.id)
    try {
      const payload = {
        ...settings,
        folder: folder.trim() || 'INBOX',
        message_id: mail.id,
      }
      let data
      try {
        const response = await apiClient.post('/api/settings/imap/message', payload, { timeout: 30000 })
        data = response.data
      } catch (error: any) {
        if (error.response?.status !== 404) throw error
        const response = await apiClient.post(`/api/settings/imap/messages/${encodeURIComponent(mail.id)}`, payload, { timeout: 30000 })
        data = response.data
      }
      if (!data.success) {
        flash('error', data.message || 'Mail icerigi goruntulenemedi')
        return
      }
      setSelected({
        ...mail,
        from: data.from || mail.from,
        subject: data.subject || mail.subject,
        date: data.date || mail.date,
        content_type: data.content_type || '',
        body_text: data.body_text || '',
        truncated: Boolean(data.truncated),
        message: data.message,
      })
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'Mail icerigi goruntulenemedi')
    } finally {
      setMessageLoadingId(null)
    }
  }

  const flash = (type: 'success' | 'error', text: string) => {
    setNotice({ type, text })
    window.setTimeout(() => setNotice(null), 4500)
  }

  if (loading) {
    return <div className="container"><div className="loading">Mail kutusu yukleniyor...</div></div>
  }

  return (
    <div className="container page-enter" style={{ maxWidth: '100%', padding: '16px 18px 32px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 16, marginBottom: 14, flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 22, color: 'var(--text-primary)' }}>Mail Kutusu</h1>
          <p style={{ margin: '4px 0 0', color: 'var(--text-secondary)', fontSize: 13 }}>IMAP uzerinden gelen sorgu cevaplarini goruntuleyin.</p>
        </div>
        <Link href="/settings" style={{ ...buttonStyle, textDecoration: 'none' }}>
          <Settings size={15} /> IMAP Ayarlari
        </Link>
      </div>

      {notice && (
        <div style={{
          marginBottom: 12,
          padding: '10px 12px',
          borderRadius: 8,
          border: `1px solid ${notice.type === 'success' ? '#86efac' : '#fca5a5'}`,
          background: notice.type === 'success' ? '#dcfce7' : '#fee2e2',
          color: notice.type === 'success' ? '#166534' : '#991b1b',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          fontSize: 13,
          fontWeight: 700,
        }}>
          {notice.type === 'success' ? <CheckCircle2 size={15} /> : <AlertCircle size={15} />}
          {notice.text}
        </div>
      )}

      {!settings?.is_configured ? (
        <div style={{ border: '1px solid var(--border)', borderRadius: 8, background: 'var(--surface)', padding: 18 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--text-primary)', fontWeight: 800 }}>
            <AlertCircle size={18} /> IMAP yapilandirmasi tamamlanmamis
          </div>
          <p style={{ margin: '8px 0 14px', color: 'var(--text-secondary)', fontSize: 13 }}>Mail kutusunu goruntulemek icin once IMAP sunucu, kullanici ve sifre bilgilerini kaydedin.</p>
          <Link href="/settings" style={{ ...buttonStyle, width: 'fit-content', textDecoration: 'none', background: 'var(--primary)', color: '#fff', borderColor: 'var(--primary)' }}>
            <Settings size={15} /> Ayarlara Git
          </Link>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: selected ? 'minmax(360px, 520px) minmax(0, 1fr)' : '1fr', gap: 14, alignItems: 'start' }}>
          <section style={{ border: '1px solid var(--border)', borderRadius: 8, background: 'var(--surface)', overflow: 'hidden' }}>
            <div style={{ padding: 14, borderBottom: '1px solid var(--border)', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: 10, alignItems: 'end' }}>
              <label style={{ display: 'grid', gap: 6, fontSize: 12, fontWeight: 700, color: 'var(--text-primary)' }}>
                Klasor
                <input style={inputStyle} value={folder} onChange={(event) => setFolder(event.target.value)} />
              </label>
              <label style={{ display: 'grid', gap: 6, fontSize: 12, fontWeight: 700, color: 'var(--text-primary)' }}>
                Gun
                <input type="number" min={1} style={inputStyle} value={lookbackDays} onChange={(event) => setLookbackDays(Number(event.target.value) || 7)} />
              </label>
              <label style={{ display: 'inline-flex', alignItems: 'center', gap: 8, height: 36, color: 'var(--text-primary)', fontSize: 13, fontWeight: 700 }}>
                <input type="checkbox" checked={unreadOnly} onChange={(event) => setUnreadOnly(event.target.checked)} />
                Sadece okunmamis
              </label>
              <button style={{ ...buttonStyle, background: 'var(--primary)', borderColor: 'var(--primary)', color: '#fff' }} onClick={() => fetchInbox()} disabled={refreshing}>
                <RefreshCw size={15} /> {refreshing ? 'Yenileniyor...' : 'Yenile'}
              </button>
            </div>

            <div style={{ padding: 14, borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{ position: 'relative', flex: 1 }}>
                <Search size={15} style={{ position: 'absolute', left: 11, top: 10, color: 'var(--text-secondary)' }} />
                <input
                  style={{ ...inputStyle, width: '100%', paddingLeft: 34 }}
                  placeholder="Gonderen, konu veya tarih ara"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </div>
              <span style={{ color: 'var(--text-secondary)', fontSize: 12, whiteSpace: 'nowrap' }}>{filteredMessages.length} mail</span>
            </div>

            {filteredMessages.length === 0 ? (
              <div style={{ minHeight: 260, display: 'grid', placeItems: 'center', color: 'var(--text-secondary)', fontSize: 13 }}>
                <div style={{ textAlign: 'center' }}>
                  <Inbox size={32} style={{ marginBottom: 10, opacity: .65 }} />
                  <div>Mail bulunamadi</div>
                </div>
              </div>
            ) : (
              <div style={{ maxHeight: 'calc(100vh - 275px)', overflow: 'auto' }}>
                {filteredMessages.map((mail) => (
                  <button
                    key={mail.id}
                    type="button"
                    onClick={() => viewMessage(mail)}
                    style={{
                      width: '100%',
                      border: 'none',
                      borderBottom: '1px solid var(--border)',
                      background: selected?.id === mail.id ? 'var(--surface-hover)' : 'var(--surface)',
                      textAlign: 'left',
                      cursor: 'pointer',
                      padding: '12px 14px',
                      display: 'grid',
                      gap: 6,
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                      <span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', color: 'var(--text-primary)', fontWeight: mail.unread ? 800 : 650, fontSize: 13 }}>
                        {mail.subject || 'Konu yok'}
                      </span>
                      <span style={{ color: mail.unread ? '#059669' : 'var(--text-secondary)', fontSize: 11, fontWeight: 800 }}>{mail.unread ? 'Okunmamis' : 'Okundu'}</span>
                    </div>
                    <div style={{ color: 'var(--text-secondary)', fontSize: 12, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{mail.from || '-'}</div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10, color: 'var(--text-muted)', fontSize: 11 }}>
                      <span>{mail.date || '-'}</span>
                      <span>{messageLoadingId === mail.id ? 'Aciliyor...' : mail.size ? `${Math.round(mail.size / 1024)} KB` : '-'}</span>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </section>

          <section style={{ border: '1px solid var(--border)', borderRadius: 8, background: 'var(--surface)', minHeight: 420, overflow: 'hidden' }}>
            {selected ? (
              <>
                <div style={{ padding: 14, borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', gap: 14 }}>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ color: 'var(--text-primary)', fontSize: 16, fontWeight: 850, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{selected.subject || 'Konu yok'}</div>
                    <div style={{ marginTop: 7, display: 'flex', flexWrap: 'wrap', gap: 14, color: 'var(--text-secondary)', fontSize: 12 }}>
                      <span>{selected.from || '-'}</span>
                      <span>{selected.date || '-'}</span>
                      {selected.truncated && <span style={{ color: '#b45309', fontWeight: 800 }}>Onizleme kisaltilmis</span>}
                    </div>
                  </div>
                  <button style={{ ...buttonStyle, width: 36, padding: 0 }} title="Kapat" onClick={() => setSelected(null)}>
                    <XCircle size={16} />
                  </button>
                </div>
                <div style={{ padding: 14, maxHeight: 'calc(100vh - 230px)', overflow: 'auto', background: 'var(--background-secondary)' }}>
                  <MailBodyView bodyText={selected.body_text} />
                </div>
              </>
            ) : (
              <div style={{ minHeight: 420, display: 'grid', placeItems: 'center', color: 'var(--text-secondary)', fontSize: 13 }}>
                <div style={{ textAlign: 'center' }}>
                  <Mail size={36} style={{ marginBottom: 10, opacity: .65 }} />
                  <div>Goruntulemek icin soldan bir mail secin</div>
                </div>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  )
}
