'use client'

import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { Mail, ChevronDown, ChevronRight, ChevronLeft, AlertTriangle, Activity, Layers, RefreshCw, Workflow, FileSpreadsheet } from 'lucide-react'
import apiClient from '@/lib/axios'
import MailTemplateManager from '@/components/investigation/MailTemplateManager'
import SendMailModal from '@/components/investigation/SendMailModal'
import ManualMailSender from '@/components/investigation/ManualMailSender'
import { WeeklyFlagsResult, WeeklyFlagUser } from '@/components/investigation/types'

const EMPTY: WeeklyFlagsResult = { personal_email_senders: [], high_volume: [], massive_matches: [] }

function normalizeWeeklyFlags(payload: any): WeeklyFlagsResult {
  const source = payload?.data ?? payload ?? {}
  return {
    personal_email_senders: normalizeUsers(source.personal_email_senders ?? source.personalEmailSenders),
    high_volume: normalizeUsers(source.high_volume ?? source.highVolume),
    massive_matches: normalizeUsers(source.massive_matches ?? source.massiveMatches),
  }
}

function normalizeUsers(value: any): WeeklyFlagUser[] {
  if (!Array.isArray(value)) return []
  return value.map(normalizeUser)
}

function normalizeUser(user: any): WeeklyFlagUser {
  return {
    user_email: user?.user_email ?? user?.userEmail ?? '',
    full_name: user?.full_name ?? user?.fullName ?? null,
    team: user?.team ?? null,
    contact_email: user?.contact_email ?? user?.contactEmail ?? user?.user_email ?? user?.userEmail ?? '',
    gender: user?.gender ?? null,
    trigger_count: Number(user?.trigger_count ?? user?.triggerCount ?? 0),
    first_seen: user?.first_seen ?? user?.firstSeen ?? '',
    last_seen: user?.last_seen ?? user?.lastSeen ?? '',
    sample_incidents: normalizeIncidents(user?.sample_incidents ?? user?.sampleIncidents),
  }
}

function normalizeIncidents(value: any) {
  if (!Array.isArray(value)) return []
  return value.map((incident: any) => ({
    timestamp: incident?.timestamp ?? '',
    policy: incident?.policy ?? null,
    max_matches: Number(incident?.max_matches ?? incident?.maxMatches ?? 0),
    destination: incident?.destination ?? null,
    channel: incident?.channel ?? null,
  }))
}

export default function WeeklyReviewPage() {
  const [data, setData] = useState<WeeklyFlagsResult>(EMPTY)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [mailUser, setMailUser] = useState<WeeklyFlagUser | null>(null)

  const fetchData = async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await apiClient.get('/api/investigation/weekly-flags', { params: { days: 7 } })
      setData(normalizeWeeklyFlags(res.data))
    } catch (e: any) {
      setError(e?.response?.data?.detail || 'Veriler alınamadı')
      setData(EMPTY)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchData()
  }, [])

  return (
    <div className="dashboard-page">
      <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h1>Haftalık Sorgu</h1>
          <p className="text-muted">Bu hafta sorgulanıp mail atılması gereken kullanıcılar (son 7 gün)</p>
        </div>
        <button
          onClick={fetchData}
          disabled={loading}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '8px 16px', background: 'var(--surface)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '8px', cursor: loading ? 'not-allowed' : 'pointer' }}
        >
          <RefreshCw size={15} style={{ animation: loading ? 'spin 1s linear infinite' : undefined }} /> Yenile
        </button>
      </div>

      <div style={{ display: 'flex', gap: 0, borderBottom: '1px solid var(--border)', marginBottom: 20 }}>
        <Link
          href="/investigation/weekly-review"
          style={{ padding: '10px 16px', color: 'var(--primary)', borderBottom: '2px solid var(--primary)', textDecoration: 'none', fontWeight: 600, fontSize: 14 }}
        >
          Haftalık Sorgu
        </Link>
        <Link
          href="/investigation/queries"
          style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '10px 16px', color: 'var(--text-secondary)', borderBottom: '2px solid transparent', textDecoration: 'none', fontWeight: 600, fontSize: 14 }}
        >
          <FileSpreadsheet size={15} /> Sorgulamalar
        </Link>
      </div>

      {error && (
        <div style={{ padding: '12px 16px', borderRadius: '8px', marginBottom: '20px', background: '#fee2e2', color: '#991b1b', border: '1px solid #fca5a5' }}>
          {error}
        </div>
      )}

      <FlagSection
        title="Şahsi Maile Gönderim Yapanlar"
        subtitle='"KT Şüpheli Kullanıcı Aktivitesi" — 10 dakika içinde 2+ olay'
        icon={<AlertTriangle size={22} />}
        color="linear-gradient(135deg, #ef4444, #f97316)"
        users={data.personal_email_senders}
        loading={loading}
        triggerLabel="olay"
        onSend={setMailUser}
        criterion="personal_email_senders"
      />

      <FlagSection
        title="30 Dakikada 10+ Olay Üretenler"
        subtitle="Herhangi 30 dakikalık pencerede 10 veya daha fazla olay kaydı"
        icon={<Activity size={22} />}
        color="linear-gradient(135deg, #f59e0b, #eab308)"
        users={data.high_volume}
        loading={loading}
        triggerLabel="olay/pencere"
        onSend={setMailUser}
        criterion="high_volume"
      />

      <FlagSection
        title="Ard Arda 500+ Eşleşmeli Olay Üretenler"
        subtitle="Zaman sırasına göre ard arda 2 adet 500+ maximum matches olayı"
        icon={<Layers size={22} />}
        color="linear-gradient(135deg, #6366f1, #8b5cf6)"
        users={data.massive_matches}
        loading={loading}
        triggerLabel="olay"
        onSend={setMailUser}
        criterion="massive_matches"
      />

      <MailTemplateManager />

      <ManualMailSender />

      {mailUser && <SendMailModal user={mailUser} onClose={() => setMailUser(null)} />}
    </div>
  )
}

interface SectionProps {
  title: string
  subtitle: string
  icon: ReactNode
  color: string
  users: WeeklyFlagUser[]
  loading: boolean
  triggerLabel: string
  onSend: (u: WeeklyFlagUser) => void
  /** Weekly-flag criterion key, used to pre-fill an agentic workflow built from this section. */
  criterion: string
}

const PAGE_SIZE = 10

function FlagSection({ title, subtitle, icon, color, users, loading, triggerLabel, onSend, criterion }: SectionProps) {
  const router = useRouter()
  const [page, setPage] = useState(1)
  const totalPages = Math.max(1, Math.ceil(users.length / PAGE_SIZE))
  const safePage = Math.min(page, totalPages)
  const pagedUsers = users.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE)

  // Reset to first page when the data set changes (e.g. refresh)
  useEffect(() => {
    setPage(1)
  }, [users])

  return (
    <div className="card" style={{ marginBottom: '24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div style={{ width: '40px', height: '40px', borderRadius: '10px', background: color, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white' }}>
            {icon}
          </div>
          <div>
            <h2 style={{ margin: 0, color: 'var(--text-primary)', fontSize: '16px', fontWeight: 600 }}>{title}</h2>
            <p style={{ margin: '2px 0 0 0', fontSize: '13px', color: 'var(--text-muted)' }}>{subtitle}</p>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexShrink: 0 }}>
          <button
            onClick={() => router.push(`/investigation/agentic-workflows?from_criterion=${criterion}`)}
            title="Bu kriteri zamanlanmış bir agentic workflow'a dönüştür"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '6px 12px', background: 'var(--surface)', color: 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', whiteSpace: 'nowrap' }}
          >
            <Workflow size={14} /> Agentic Workflow'a Dönüştür
          </button>
          <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-muted)', background: 'var(--surface-hover)', padding: '4px 12px', borderRadius: '20px', whiteSpace: 'nowrap' }}>
            {users.length} kullanıcı
          </span>
        </div>
      </div>

      {loading ? (
        <div style={{ color: 'var(--text-muted)', fontSize: '14px', padding: '12px 0' }}>Yükleniyor...</div>
      ) : users.length === 0 ? (
        <div style={{ color: 'var(--text-muted)', fontSize: '14px', padding: '12px 0' }}>Bu kritere takılan kullanıcı yok.</div>
      ) : (
        <>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {pagedUsers.map(u => <UserRow key={u.user_email} user={u} triggerLabel={triggerLabel} onSend={onSend} />)}
          </div>
          {totalPages > 1 && (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '8px', marginTop: '14px' }}>
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={safePage === 1}
                style={{ display: 'inline-flex', alignItems: 'center', padding: '6px 10px', background: 'var(--surface)', color: safePage === 1 ? 'var(--text-muted)' : 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: safePage === 1 ? 'not-allowed' : 'pointer' }}
              >
                <ChevronLeft size={16} />
              </button>
              <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Sayfa {safePage} / {totalPages}
              </span>
              <button
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={safePage === totalPages}
                style={{ display: 'inline-flex', alignItems: 'center', padding: '6px 10px', background: 'var(--surface)', color: safePage === totalPages ? 'var(--text-muted)' : 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: safePage === totalPages ? 'not-allowed' : 'pointer' }}
              >
                <ChevronRight size={16} />
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}

function UserRow({ user, triggerLabel, onSend }: { user: WeeklyFlagUser; triggerLabel: string; onSend: (u: WeeklyFlagUser) => void }) {
  const [open, setOpen] = useState(false)
  return (
    <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', padding: '12px 14px' }}>
        <button onClick={() => setOpen(!open)} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-muted)', display: 'flex' }}>
          {open ? <ChevronDown size={18} /> : <ChevronRight size={18} />}
        </button>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 600, fontSize: '14px', color: 'var(--text-primary)' }}>
            {user.user_email}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            {user.contact_email !== user.user_email ? user.contact_email : ''}{user.team ? `${user.contact_email !== user.user_email ? ' · ' : ''}${user.team}` : ''}
          </div>
        </div>
        <div style={{ textAlign: 'right', marginRight: '4px' }}>
          <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>{user.trigger_count} <span style={{ fontSize: '11px', fontWeight: 400, color: 'var(--text-muted)' }}>{triggerLabel}</span></div>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>son: {new Date(user.last_seen).toLocaleString('tr-TR')}</div>
        </div>
        <button
          onClick={() => onSend(user)}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', padding: '8px 14px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '13px', flexShrink: 0 }}
        >
          <Mail size={14} /> Mail Gönder
        </button>
      </div>

      {open && (
        <div style={{ borderTop: '1px solid var(--border)', padding: '10px 14px', background: 'var(--surface-hover)' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px' }}>Örnek Olaylar</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            {user.sample_incidents.map((inc, idx) => (
              <div key={idx} style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                <span style={{ color: 'var(--text-muted)' }}>{new Date(inc.timestamp).toLocaleString('tr-TR')}</span>
                <span>{inc.policy ?? '-'}</span>
                <span style={{ fontWeight: 600 }}>{inc.max_matches} eşleşme</span>
                <span>{inc.channel ?? '-'}</span>
                <span style={{ color: 'var(--text-muted)' }}>{inc.destination ?? '-'}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
