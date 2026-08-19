'use client'

import { CSSProperties, ReactNode, useEffect, useMemo, useState } from 'react'
import dynamic from 'next/dynamic'
import axios from 'axios'
import {
  Bell,
  Bot,
  BriefcaseBusiness,
  ChevronDown,
  Cloud,
  Database,
  Eye,
  EyeOff,
  Info,
  KeyRound,
  Mail,
  Save,
  Send,
  ShieldCheck,
  TestTube2,
  X,
} from 'lucide-react'
import { getApiUrlDynamic } from '@/lib/api-config'

const AISettingsTab = dynamic(() => import('@/components/settings/AISettingsTab'), { ssr: false })

interface GeneralSettings {
  email_notifications: boolean
  daily_report_time: string
  risk_threshold_low: number
  risk_threshold_medium: number
  risk_threshold_high: number
  admin_email: string
  job_history_page_size: number
  hangfire_max_history: number
  job_history_latest_count: number
}

interface DlpSettings {
  manager_ip: string
  manager_port: number
  use_https: boolean
  timeout_seconds: number
  username: string
  password: string
  password_set: boolean
  last_updated?: string | null
}

interface EmailSettings {
  smtp_host: string
  smtp_port: number
  enable_ssl: boolean
  username: string
  password: string
  password_set: boolean
  from_email: string
  from_name: string
  is_configured: boolean
  last_updated?: string | null
}

interface ImapSettings {
  enabled: boolean
  host: string
  port: number
  enable_ssl: boolean
  username: string
  password: string
  password_set: boolean
  folder: string
  unread_only: boolean
  lookback_days: number
  max_messages: number
  is_configured: boolean
  updated_at?: string | null
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

interface LdapSettings {
  enabled: boolean
  use_ldaps: boolean
  host: string
  port: number
  domain: string
  search_base: string
  service_account: string
  service_password: string
  service_password_set: boolean
  is_configured: boolean
  updated_at?: string | null
}

interface ExternalUserDbSettings {
  enabled: boolean
  provider: 'postgresql' | 'mssql'
  host: string
  port: number
  database: string
  username: string
  password: string
  password_set: boolean
  encrypt: boolean
  trust_server_certificate: boolean
  table_name: string
  match_column: string
  first_name_column: string
  last_name_column: string
  full_name_column: string
  email_column: string
  department_column: string
  where_clause: string
  is_configured: boolean
  updated_at?: string | null
}

type Message = { type: 'success' | 'error'; text: string } | null

const inputStyle = {
  width: '100%',
  height: 34,
  padding: '7px 11px',
  border: '1px solid var(--border)',
  borderRadius: 6,
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontSize: 13,
  outline: 'none',
} as const

const buttonBase = {
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 6,
  minHeight: 34,
  padding: '7px 12px',
  borderRadius: 6,
  border: '1px solid var(--border)',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  cursor: 'pointer',
  fontSize: 13,
  fontWeight: 600,
} as const

const thStyle: CSSProperties = {
  padding: '8px 10px',
  borderBottom: '1px solid var(--border)',
  whiteSpace: 'nowrap',
}

const tdStyle: CSSProperties = {
  padding: '9px 10px',
  color: 'var(--text-primary)',
  verticalAlign: 'top',
  whiteSpace: 'nowrap',
}

export default function SettingsPage() {
  const [open, setOpen] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [generalSaving, setGeneralSaving] = useState(false)
  const [dlpSaving, setDlpSaving] = useState(false)
  const [dlpTesting, setDlpTesting] = useState(false)
  const [emailSaving, setEmailSaving] = useState(false)
  const [emailTesting, setEmailTesting] = useState(false)
  const [imapSaving, setImapSaving] = useState(false)
  const [imapTesting, setImapTesting] = useState(false)
  const [imapInboxLoading, setImapInboxLoading] = useState(false)
  const [imapMessageLoadingId, setImapMessageLoadingId] = useState<string | null>(null)
  const [ldapSaving, setLdapSaving] = useState(false)
  const [ldapTesting, setLdapTesting] = useState(false)
  const [externalDbSaving, setExternalDbSaving] = useState(false)
  const [externalDbTesting, setExternalDbTesting] = useState(false)
  const [externalDbLookupTesting, setExternalDbLookupTesting] = useState(false)
  const [sendingEmail, setSendingEmail] = useState(false)
  const [showDlpPassword, setShowDlpPassword] = useState(false)
  const [showEmailPassword, setShowEmailPassword] = useState(false)
  const [showImapPassword, setShowImapPassword] = useState(false)
  const [showLdapPassword, setShowLdapPassword] = useState(false)
  const [showExternalDbPassword, setShowExternalDbPassword] = useState(false)
  const [smtpTestRecipient, setSmtpTestRecipient] = useState('')
  const [externalDbTestUsername, setExternalDbTestUsername] = useState('')
  const [imapInboxMessages, setImapInboxMessages] = useState<ImapInboxMessage[]>([])
  const [imapSelectedMessage, setImapSelectedMessage] = useState<ImapMessageContent | null>(null)
  const [message, setMessage] = useState<Message>(null)

  const [general, setGeneral] = useState<GeneralSettings>({
    email_notifications: true,
    daily_report_time: '06:00',
    risk_threshold_low: 10,
    risk_threshold_medium: 30,
    risk_threshold_high: 50,
    admin_email: '',
    job_history_page_size: 30,
    hangfire_max_history: 100,
    job_history_latest_count: 20,
  })

  const [email, setEmail] = useState<EmailSettings>({
    smtp_host: '',
    smtp_port: 587,
    enable_ssl: true,
    username: '',
    password: '',
    password_set: false,
    from_email: '',
    from_name: 'DLP Risk Analyzer',
    is_configured: false,
    last_updated: null,
  })

  const [imap, setImap] = useState<ImapSettings>({
    enabled: false,
    host: '',
    port: 993,
    enable_ssl: true,
    username: '',
    password: '',
    password_set: false,
    folder: 'INBOX',
    unread_only: true,
    lookback_days: 7,
    max_messages: 500,
    is_configured: false,
    updated_at: null,
  })

  const [ldap, setLdap] = useState<LdapSettings>({
    enabled: false,
    use_ldaps: true,
    host: '',
    port: 636,
    domain: '',
    search_base: '',
    service_account: '',
    service_password: '',
    service_password_set: false,
    is_configured: false,
    updated_at: null,
  })

  const [externalDb, setExternalDb] = useState<ExternalUserDbSettings>({
    enabled: false,
    provider: 'postgresql',
    host: '',
    port: 5432,
    database: '',
    username: '',
    password: '',
    password_set: false,
    encrypt: false,
    trust_server_certificate: true,
    table_name: '',
    match_column: 'username',
    first_name_column: '',
    last_name_column: '',
    full_name_column: '',
    email_column: 'email',
    department_column: '',
    where_clause: '',
    is_configured: false,
    updated_at: null,
  })

  const [dlp, setDlp] = useState<DlpSettings>({
    manager_ip: '',
    manager_port: 8443,
    use_https: true,
    timeout_seconds: 30,
    username: '',
    password: '',
    password_set: false,
    last_updated: null,
  })

  const apiUrl = useMemo(() => getApiUrlDynamic(), [])

  useEffect(() => {
    void loadAll()
  }, [])

  const loadAll = async () => {
    setLoading(true)
    try {
      await Promise.all([
        fetchGeneral(),
        fetchEmail(),
        fetchImap(),
        fetchLdap(),
        fetchExternalDb(),
        fetchDlp(),
      ])
    } finally {
      setLoading(false)
    }
  }

  const fetchGeneral = async () => {
    const response = await axios.get(`${apiUrl}/api/settings`).catch(() => ({ data: null }))
    const data = response.data
    if (!data) return
    setGeneral({
      email_notifications: data.email_notifications ?? true,
      daily_report_time: data.daily_report_time ?? '06:00',
      risk_threshold_low: Number(data.risk_threshold_low) || 10,
      risk_threshold_medium: Number(data.risk_threshold_medium) || 30,
      risk_threshold_high: Number(data.risk_threshold_high) || 50,
      admin_email: data.admin_email ?? '',
      job_history_page_size: Number(data.job_history_page_size) || 30,
      hangfire_max_history: Number(data.hangfire_max_history) || 100,
      job_history_latest_count: Number(data.job_history_latest_count) || 20,
    })
  }

  const fetchEmail = async () => {
    const { data } = await axios.get(`${apiUrl}/api/settings/email`).catch(() => ({ data: null }))
    if (!data) return
    setEmail({
      smtp_host: data.smtp_host ?? '',
      smtp_port: Number(data.smtp_port) || 587,
      enable_ssl: data.enable_ssl ?? true,
      username: data.username ?? '',
      password: '',
      password_set: data.password_set ?? false,
      from_email: data.from_email ?? '',
      from_name: data.from_name ?? 'DLP Risk Analyzer',
      is_configured: data.is_configured ?? false,
      last_updated: data.updated_at ?? null,
    })
  }

  const fetchImap = async () => {
    const { data } = await axios.get(`${apiUrl}/api/settings/imap`).catch(() => ({ data: null }))
    if (!data) return
    setImap({
      enabled: data.enabled ?? false,
      host: data.host ?? '',
      port: Number(data.port) || 993,
      enable_ssl: data.enable_ssl ?? true,
      username: data.username ?? '',
      password: '',
      password_set: data.password_set ?? false,
      folder: data.folder ?? 'INBOX',
      unread_only: data.unread_only ?? true,
      lookback_days: Number(data.lookback_days) || 7,
      max_messages: Number(data.max_messages) || 500,
      is_configured: data.is_configured ?? false,
      updated_at: data.updated_at ?? null,
    })
  }

  const fetchLdap = async () => {
    const { data } = await axios.get(`${apiUrl}/api/settings/ldap`).catch(() => ({ data: null }))
    if (!data) return
    setLdap({
      enabled: data.enabled ?? false,
      use_ldaps: data.use_ldaps ?? true,
      host: data.host ?? '',
      port: Number(data.port) || (data.use_ldaps ? 636 : 389),
      domain: data.domain ?? '',
      search_base: data.search_base ?? '',
      service_account: data.service_account ?? '',
      service_password: '',
      service_password_set: data.service_password_set ?? false,
      is_configured: data.is_configured ?? false,
      updated_at: data.updated_at ?? null,
    })
  }

  const fetchExternalDb = async () => {
    const { data } = await axios.get(`${apiUrl}/api/settings/external-user-db`).catch(() => ({ data: null }))
    if (!data) return
    setExternalDb({
      enabled: data.enabled ?? false,
      provider: data.provider === 'mssql' ? 'mssql' : 'postgresql',
      host: data.host ?? '',
      port: Number(data.port) || (data.provider === 'mssql' ? 1433 : 5432),
      database: data.database ?? '',
      username: data.username ?? '',
      password: '',
      password_set: data.password_set ?? false,
      encrypt: data.encrypt ?? (data.provider === 'mssql'),
      trust_server_certificate: data.trust_server_certificate ?? true,
      table_name: data.table_name ?? '',
      match_column: data.match_column ?? 'username',
      first_name_column: data.first_name_column ?? '',
      last_name_column: data.last_name_column ?? '',
      full_name_column: data.full_name_column ?? '',
      email_column: data.email_column ?? 'email',
      department_column: data.department_column ?? '',
      where_clause: data.where_clause ?? '',
      is_configured: data.is_configured ?? false,
      updated_at: data.updated_at ?? null,
    })
  }

  const fetchDlp = async () => {
    const { data } = await axios.get(`${apiUrl}/api/settings/dlp`).catch(() => ({ data: null }))
    if (!data) return
    setDlp({
      manager_ip: data.manager_ip ?? '',
      manager_port: Number(data.manager_port) || 8443,
      use_https: data.use_https ?? true,
      timeout_seconds: Number(data.timeout_seconds) || 30,
      username: data.username ?? '',
      password: '',
      password_set: data.password_set ?? false,
      last_updated: data.updated_at ?? null,
    })
  }

  const saveGeneral = async () => {
    setGeneralSaving(true)
    setMessage(null)
    try {
      const response = await axios.post(`${apiUrl}/api/settings`, general, { timeout: 10000 })
      if (response.data?.settings) {
        const saved = response.data.settings
        setGeneral((prev) => ({ ...prev, ...saved }))
      }
      flash('success', 'Genel ayarlar kaydedildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Genel ayarlar kaydedilemedi')
    } finally {
      setGeneralSaving(false)
    }
  }

  const saveEmail = async () => {
    setEmailSaving(true)
    setMessage(null)
    try {
      const payload = {
        smtp_host: email.smtp_host.trim(),
        smtp_port: email.smtp_port,
        enable_ssl: email.enable_ssl,
        username: email.username.trim(),
        password: email.password.trim() || undefined,
        from_email: email.from_email.trim(),
        from_name: email.from_name.trim(),
      }
      const response = await axios.post(`${apiUrl}/api/settings/email`, payload, { timeout: 15000 })
      const saved = response.data?.settings
      if (saved) {
        setEmail({
          smtp_host: saved.smtp_host ?? '',
          smtp_port: Number(saved.smtp_port) || 587,
          enable_ssl: saved.enable_ssl ?? true,
          username: saved.username ?? '',
          password: '',
          password_set: saved.password_set ?? false,
          from_email: saved.from_email ?? '',
          from_name: saved.from_name ?? 'DLP Risk Analyzer',
          is_configured: saved.is_configured ?? false,
          last_updated: saved.updated_at ?? new Date().toISOString(),
        })
      }
      flash('success', 'SMTP ayarlari kaydedildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'SMTP ayarlari kaydedilemedi')
    } finally {
      setEmailSaving(false)
    }
  }

  const testEmail = async () => {
    setEmailTesting(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/email/test`, {
        smtp_host: email.smtp_host.trim(),
        smtp_port: email.smtp_port,
        enable_ssl: email.enable_ssl,
        username: email.username.trim(),
        password: email.password.trim() || undefined,
        from_email: email.from_email.trim(),
        from_name: email.from_name.trim(),
      }, { timeout: 20000 })
      flash('success', response.data?.message || 'SMTP baglantisi basarili')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'SMTP testi basarisiz')
    } finally {
      setEmailTesting(false)
    }
  }

  const sendSmtpTestEmail = async () => {
    const recipient = smtpTestRecipient.trim()
    if (!recipient) {
      flash('error', 'Test alicisi girin')
      return
    }
    setSendingEmail(true)
    try {
      const token = localStorage.getItem('authToken')
      const response = await axios.post(`${apiUrl}/api/settings/send-test-email`, { email: recipient }, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        timeout: 20000,
      })
      flash('success', response.data?.message || 'Test e-postasi gonderildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Test e-postasi gonderilemedi')
    } finally {
      setSendingEmail(false)
    }
  }

  const saveImap = async () => {
    setImapSaving(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/imap`, {
        ...imap,
        password: imap.password.trim() || undefined,
      }, { timeout: 15000 })
      const saved = response.data?.settings
      if (saved) {
        setImap((prev) => ({ ...prev, ...saved, password: '' }))
      }
      flash('success', 'IMAP ayarlari kaydedildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'IMAP ayarlari kaydedilemedi')
    } finally {
      setImapSaving(false)
    }
  }

  const testImap = async () => {
    setImapTesting(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/imap/test`, {
        ...imap,
        password: imap.password.trim() || undefined,
      }, { timeout: 20000 })
      flash(response.data?.success ? 'success' : 'error', response.data?.message || 'IMAP testi tamamlandi')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'IMAP testi basarisiz')
    } finally {
      setImapTesting(false)
    }
  }

  const previewImapInbox = async () => {
    setImapInboxLoading(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/imap/inbox`, {
        ...imap,
        password: imap.password.trim() || undefined,
        preview_count: 20,
      }, { timeout: 30000 })
      const messages = Array.isArray(response.data?.messages) ? response.data.messages : []
      setImapInboxMessages(messages)
      flash(response.data?.success ? 'success' : 'error', response.data?.message || 'INBOX goruntuleme tamamlandi')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'INBOX goruntulenemedi')
    } finally {
      setImapInboxLoading(false)
    }
  }

  const viewImapMessage = async (mail: ImapInboxMessage) => {
    setImapMessageLoadingId(mail.id)
    try {
      const payload = {
        ...imap,
        password: imap.password.trim() || undefined,
        message_id: mail.id,
      }
      let response
      try {
        response = await axios.post(`${apiUrl}/api/settings/imap/message`, payload, { timeout: 30000 })
      } catch (error: any) {
        if (error.response?.status !== 404) throw error
        response = await axios.post(`${apiUrl}/api/settings/imap/messages/${encodeURIComponent(mail.id)}`, payload, { timeout: 30000 })
      }
      if (!response.data?.success) {
        flash('error', response.data?.message || 'Mail icerigi goruntulenemedi')
        return
      }
      setImapSelectedMessage({
        ...mail,
        from: response.data.from || mail.from,
        subject: response.data.subject || mail.subject,
        date: response.data.date || mail.date,
        content_type: response.data.content_type || '',
        body_text: response.data.body_text || '',
        truncated: Boolean(response.data.truncated),
        message: response.data.message,
      })
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'Mail icerigi goruntulenemedi')
    } finally {
      setImapMessageLoadingId(null)
    }
  }

  const saveLdap = async () => {
    setLdapSaving(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/ldap`, {
        ...ldap,
        service_password: ldap.service_password.trim() || undefined,
      }, { timeout: 15000 })
      const saved = response.data?.settings
      if (saved) {
        setLdap((prev) => ({ ...prev, ...saved, service_password: '' }))
      }
      flash('success', 'LDAP ayarlari kaydedildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'LDAP ayarlari kaydedilemedi')
    } finally {
      setLdapSaving(false)
    }
  }

  const testLdap = async () => {
    setLdapTesting(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/ldap/test`, {
        ...ldap,
        service_password: ldap.service_password.trim() || undefined,
      }, { timeout: 20000 })
      flash(response.data?.success ? 'success' : 'error', response.data?.message || 'LDAP testi tamamlandi')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'LDAP testi basarisiz')
    } finally {
      setLdapTesting(false)
    }
  }

  const saveExternalDb = async () => {
    setExternalDbSaving(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/external-user-db`, {
        ...externalDb,
        password: externalDb.password.trim() || undefined,
      }, { timeout: 15000 })
      const saved = response.data?.settings
      if (saved) {
        setExternalDb((prev) => ({ ...prev, ...saved, password: '' }))
      }
      flash('success', 'Harici kullanici veritabani ayarlari kaydedildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'Harici veritabani ayarlari kaydedilemedi')
    } finally {
      setExternalDbSaving(false)
    }
  }

  const testExternalDb = async () => {
    setExternalDbTesting(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/external-user-db/test`, {
        ...externalDb,
        password: externalDb.password.trim() || undefined,
      }, { timeout: 30000 })
      flash(response.data?.success ? 'success' : 'error', response.data?.message || 'Veritabani testi tamamlandi')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'Veritabani testi basarisiz')
    } finally {
      setExternalDbTesting(false)
    }
  }

  const testExternalDbLookup = async () => {
    if (!externalDbTestUsername.trim()) {
      flash('error', 'Test kullanici adi girin')
      return
    }
    setExternalDbLookupTesting(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/external-user-db/lookup`, {
        ...externalDb,
        password: externalDb.password.trim() || undefined,
        test_username: externalDbTestUsername.trim(),
      }, { timeout: 30000 })
      const user = response.data?.user
      const detail = user ? `: ${user.full_name || '-'} / ${user.email || '-'} / ${user.department || '-'}` : ''
      flash(response.data?.success ? 'success' : 'error', `${response.data?.message || 'Lookup tamamlandi'}${detail}`)
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'Lookup testi basarisiz')
    } finally {
      setExternalDbLookupTesting(false)
    }
  }

  const saveDlp = async () => {
    setDlpSaving(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/dlp`, {
        manager_ip: dlp.manager_ip.trim(),
        manager_port: dlp.manager_port,
        use_https: dlp.use_https,
        timeout_seconds: dlp.timeout_seconds,
        username: dlp.username.trim(),
        password: dlp.password.trim() || undefined,
      }, { timeout: 15000 })
      const saved = response.data?.settings
      if (saved) {
        setDlp((prev) => ({
          ...prev,
          manager_ip: saved.manager_ip ?? prev.manager_ip,
          manager_port: Number(saved.manager_port) || prev.manager_port,
          use_https: saved.use_https ?? prev.use_https,
          timeout_seconds: Number(saved.timeout_seconds) || prev.timeout_seconds,
          username: saved.username ?? prev.username,
          password: '',
          password_set: saved.password_set ?? prev.password_set,
          last_updated: saved.updated_at ?? new Date().toISOString(),
        }))
      }
      flash('success', 'DLP API ayarlari kaydedildi')
    } catch (error: any) {
      flash('error', error.response?.data?.detail || error.message || 'DLP API ayarlari kaydedilemedi')
    } finally {
      setDlpSaving(false)
    }
  }

  const testDlp = async () => {
    setDlpTesting(true)
    try {
      const response = await axios.post(`${apiUrl}/api/settings/dlp/test`, {
        manager_ip: dlp.manager_ip.trim(),
        manager_port: dlp.manager_port,
        use_https: dlp.use_https,
        timeout_seconds: dlp.timeout_seconds,
        username: dlp.username.trim(),
        password: dlp.password.trim() || undefined,
      }, { timeout: 20000 })
      flash(response.data?.success ? 'success' : 'error', response.data?.message || 'DLP testi tamamlandi')
    } catch (error: any) {
      flash('error', error.response?.data?.message || error.response?.data?.detail || error.message || 'DLP testi basarisiz')
    } finally {
      setDlpTesting(false)
    }
  }

  const toggleSection = (id: string) => {
    setOpen((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id])
  }

  const flash = (type: 'success' | 'error', text: string) => {
    setMessage({ type, text })
    window.setTimeout(() => setMessage(null), 4500)
  }

  const updateGeneral = <K extends keyof GeneralSettings>(key: K, value: GeneralSettings[K]) =>
    setGeneral((prev) => ({ ...prev, [key]: value }))
  const updateEmail = <K extends keyof EmailSettings>(key: K, value: EmailSettings[K]) =>
    setEmail((prev) => ({ ...prev, [key]: value }))
  const updateImap = <K extends keyof ImapSettings>(key: K, value: ImapSettings[K]) =>
    setImap((prev) => ({ ...prev, [key]: value }))
  const updateLdap = <K extends keyof LdapSettings>(key: K, value: LdapSettings[K]) =>
    setLdap((prev) => ({ ...prev, [key]: value }))
  const updateExternalDb = <K extends keyof ExternalUserDbSettings>(key: K, value: ExternalUserDbSettings[K]) =>
    setExternalDb((prev) => ({ ...prev, [key]: value }))

  const updateExternalDbProvider = (provider: ExternalUserDbSettings['provider']) =>
    setExternalDb((prev) => ({
      ...prev,
      provider,
      port: provider === 'mssql' ? 1433 : 5432,
      encrypt: provider === 'mssql' ? prev.encrypt : false,
      trust_server_certificate: provider === 'mssql' ? prev.trust_server_certificate : true,
    }))
  const updateDlp = <K extends keyof DlpSettings>(key: K, value: DlpSettings[K]) =>
    setDlp((prev) => ({ ...prev, [key]: value }))

  if (loading) {
    return <div className="container"><div className="loading">Ayarlar yukleniyor...</div></div>
  }

  return (
    <div className="container page-enter" style={{ maxWidth: '100%', padding: '16px 18px 32px' }}>
      {message && (
        <div style={{
          position: 'sticky',
          top: 8,
          zIndex: 10,
          marginBottom: 12,
          padding: '10px 14px',
          borderRadius: 8,
          border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`,
          background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
          color: message.type === 'success' ? '#166534' : '#991b1b',
          fontWeight: 600,
          fontSize: 13,
        }}>
          {message.text}
        </div>
      )}

      <AccordionSection id="ai" title="Yapay Zeka" icon={<Bot size={17} />} open={open.includes('ai')} onToggle={toggleSection}>
        <InfoBar text="Yapay zeka destekli analiz ve oneri ayarlari." />
        <div style={{ marginTop: 14 }}>
          <AISettingsTab />
        </div>
      </AccordionSection>

      <AccordionSection id="smtp" title="SMTP" icon={<Mail size={17} />} open={open.includes('smtp')} onToggle={toggleSection}>
        <InfoBar text="E-posta gonderimi icin SMTP sunucu yapilandirmasi." />
        <Panel>
          <Toggle checked={email.enable_ssl} label="SSL Aktif" onChange={(value) => updateEmail('enable_ssl', value)} />
        </Panel>
        <Panel title="Baglanti Bilgileri" icon={<Cloud size={15} />}>
          <Grid>
            <Field label="SMTP Sunucu Adresi"><input style={inputStyle} value={email.smtp_host} onChange={(e) => updateEmail('smtp_host', e.target.value)} /></Field>
            <Field label="SMTP Port"><input type="number" style={inputStyle} value={email.smtp_port} onChange={(e) => updateEmail('smtp_port', Number(e.target.value) || 587)} /></Field>
            <Field label="Gonderen E-posta"><input type="email" style={inputStyle} value={email.from_email} onChange={(e) => updateEmail('from_email', e.target.value)} /></Field>
            <Field label="Gonderen Adi"><input style={inputStyle} value={email.from_name} onChange={(e) => updateEmail('from_name', e.target.value)} /></Field>
          </Grid>
        </Panel>
        <Panel title="Kimlik Dogrulama" icon={<KeyRound size={15} />}>
          <Grid>
            <Field label="Kullanici Adi"><input style={inputStyle} value={email.username} onChange={(e) => updateEmail('username', e.target.value)} /></Field>
            <Field label="Sifre">
              <SecretInput
                value={email.password}
                placeholder={email.password_set ? 'Degistirmek icin yeni sifre girin' : 'Sifre girin'}
                visible={showEmailPassword}
                onToggle={() => setShowEmailPassword((v) => !v)}
                onChange={(value) => updateEmail('password', value)}
                saved={email.password_set}
              />
            </Field>
          </Grid>
        </Panel>
        <Panel>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <Field label="Test Maili Alicisi" style={{ minWidth: 260, flex: 1 }}>
              <input type="email" style={inputStyle} value={smtpTestRecipient} onChange={(e) => setSmtpTestRecipient(e.target.value)} />
            </Field>
            <ActionButton icon={<Send size={15} />} onClick={sendSmtpTestEmail} disabled={sendingEmail}>{sendingEmail ? 'Gonderiliyor...' : 'Test Maili Gonder'}</ActionButton>
            <ActionButton icon={<TestTube2 size={15} />} onClick={testEmail} disabled={emailTesting}>{emailTesting ? 'Test ediliyor...' : 'Baglanti Testi'}</ActionButton>
            <PrimaryButton icon={<Save size={15} />} onClick={saveEmail} disabled={emailSaving}>{emailSaving ? 'Kaydediliyor...' : 'Kaydet'}</PrimaryButton>
          </div>
        </Panel>
      </AccordionSection>

      <AccordionSection id="imap" title="IMAP" icon={<Database size={17} />} open={open.includes('imap')} onToggle={toggleSection}>
        <InfoBar text="Sorgu mailleri ve cevap takibi icin IMAP yapilandirmasi." />
        <Panel>
          <div style={{ display: 'flex', gap: 28, flexWrap: 'wrap' }}>
            <Toggle checked={imap.enabled} label="IMAP Aktif" onChange={(value) => updateImap('enabled', value)} />
            <Toggle checked={imap.enable_ssl} label="SSL Aktif" onChange={(value) => updateImap('enable_ssl', value)} />
            <Toggle checked={imap.unread_only} label="Sadece okunmamis mailler" onChange={(value) => updateImap('unread_only', value)} />
          </div>
        </Panel>
        <Panel title="Baglanti Bilgileri" icon={<Cloud size={15} />}>
          <Grid>
            <Field label="IMAP Sunucu Adresi"><input style={inputStyle} value={imap.host} onChange={(e) => updateImap('host', e.target.value)} /></Field>
            <Field label="IMAP Port"><input type="number" style={inputStyle} value={imap.port} onChange={(e) => updateImap('port', Number(e.target.value) || 993)} /></Field>
            <Field label="Klasor"><input style={inputStyle} value={imap.folder} onChange={(e) => updateImap('folder', e.target.value)} /></Field>
            <Field label="Son kac gun"><input type="number" min={1} style={inputStyle} value={imap.lookback_days} onChange={(e) => updateImap('lookback_days', Number(e.target.value) || 7)} /></Field>
            <Field label="Maksimum mail"><input type="number" min={1} style={inputStyle} value={imap.max_messages} onChange={(e) => updateImap('max_messages', Number(e.target.value) || 500)} /></Field>
          </Grid>
        </Panel>
        <Panel title="Kimlik Dogrulama" icon={<KeyRound size={15} />}>
          <Grid>
            <Field label="Kullanici Adi"><input style={inputStyle} value={imap.username} onChange={(e) => updateImap('username', e.target.value)} /></Field>
            <Field label="Sifre">
              <SecretInput
                value={imap.password}
                placeholder={imap.password_set ? 'Degistirmek icin yeni sifre girin' : 'Sifre girin'}
                visible={showImapPassword}
                onToggle={() => setShowImapPassword((v) => !v)}
                onChange={(value) => updateImap('password', value)}
                saved={imap.password_set}
              />
            </Field>
          </Grid>
        </Panel>
        <Actions>
          <ActionButton icon={<TestTube2 size={15} />} onClick={testImap} disabled={imapTesting}>{imapTesting ? 'Test ediliyor...' : 'Baglanti Testi'}</ActionButton>
          <ActionButton icon={<Eye size={15} />} onClick={previewImapInbox} disabled={imapInboxLoading}>{imapInboxLoading ? 'Yukleniyor...' : 'INBOX Goruntule'}</ActionButton>
          <PrimaryButton icon={<Save size={15} />} onClick={saveImap} disabled={imapSaving}>{imapSaving ? 'Kaydediliyor...' : 'Kaydet'}</PrimaryButton>
        </Actions>
        {imapInboxMessages.length > 0 && (
          <Panel title="INBOX Onizleme" icon={<Mail size={15} />}>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                <thead>
                  <tr style={{ color: 'var(--text-muted)', textAlign: 'left' }}>
                    <th style={thStyle}>Durum</th>
                    <th style={thStyle}>Gonderen</th>
                    <th style={thStyle}>Konu</th>
                    <th style={thStyle}>Tarih</th>
                    <th style={thStyle}>Boyut</th>
                    <th style={thStyle}>Icerik</th>
                  </tr>
                </thead>
                <tbody>
                  {imapInboxMessages.map((mail) => (
                    <tr key={mail.id} style={{ borderTop: '1px solid var(--border)' }}>
                      <td style={tdStyle}>{mail.unread ? 'Okunmamis' : 'Okundu'}</td>
                      <td style={tdStyle}>{mail.from || '-'}</td>
                      <td style={{ ...tdStyle, minWidth: 260 }}>{mail.subject || '-'}</td>
                      <td style={tdStyle}>{mail.date || '-'}</td>
                      <td style={tdStyle}>{mail.size ? `${Math.round(mail.size / 1024)} KB` : '-'}</td>
                      <td style={tdStyle}>
                        <ActionButton icon={<Eye size={14} />} onClick={() => viewImapMessage(mail)} disabled={imapMessageLoadingId === mail.id}>
                          {imapMessageLoadingId === mail.id ? 'Aciliyor...' : 'Goruntule'}
                        </ActionButton>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>
        )}
      </AccordionSection>

      <AccordionSection id="ldap" title="LDAP / AD" icon={<ShieldCheck size={17} />} open={open.includes('ldap')} onToggle={toggleSection}>
        <InfoBar text="Active Directory / LDAP ile kimlik dogrulama ayarlari." />
        <Panel>
          <div style={{ display: 'flex', gap: 28, flexWrap: 'wrap' }}>
            <Toggle checked={ldap.enabled} label="AD/LDAP ile girisi ac" onChange={(value) => updateLdap('enabled', value)} />
            <Toggle
              checked={ldap.use_ldaps}
              label="LDAPS (SSL) kullan"
              onChange={(value) => setLdap((prev) => ({ ...prev, use_ldaps: value, port: value ? 636 : 389 }))}
            />
          </div>
        </Panel>
        <Panel title="Baglanti Bilgileri" icon={<Cloud size={15} />}>
          <Grid>
            <Field label="LDAP/AD sunucu adresi"><input style={inputStyle} value={ldap.host} onChange={(e) => updateLdap('host', e.target.value)} /></Field>
            <Field label="Port (389 LDAP, 636 LDAPS)"><input type="number" style={inputStyle} value={ldap.port} onChange={(e) => updateLdap('port', Number(e.target.value) || 636)} /></Field>
          </Grid>
        </Panel>
        <Panel title="Domain Yapilandirmasi" icon={<BriefcaseBusiness size={15} />}>
          <Grid>
            <Field label="AD domain adi"><input style={inputStyle} value={ldap.domain} onChange={(e) => updateLdap('domain', e.target.value)} placeholder="COMPANY" /></Field>
            <Field label="LDAP arama tabani"><input style={inputStyle} value={ldap.search_base} onChange={(e) => updateLdap('search_base', e.target.value)} placeholder="DC=sirket,DC=com" /></Field>
          </Grid>
        </Panel>
        <Panel title="Kimlik Dogrulama" icon={<KeyRound size={15} />}>
          <Grid>
            <Field label="LDAP sorgu servis hesabi"><input style={inputStyle} value={ldap.service_account} onChange={(e) => updateLdap('service_account', e.target.value)} placeholder="DOMAIN\\ldap_user" /></Field>
            <Field label="LDAP servis hesabi sifresi">
              <SecretInput
                value={ldap.service_password}
                placeholder={ldap.service_password_set ? 'Degistirmek icin yeni sifre girin' : 'Sifre girin'}
                visible={showLdapPassword}
                onToggle={() => setShowLdapPassword((v) => !v)}
                onChange={(value) => updateLdap('service_password', value)}
                saved={ldap.service_password_set}
              />
            </Field>
          </Grid>
        </Panel>
        <Actions>
          <ActionButton icon={<TestTube2 size={15} />} onClick={testLdap} disabled={ldapTesting}>{ldapTesting ? 'Test ediliyor...' : 'Baglanti Testi'}</ActionButton>
          <PrimaryButton icon={<Save size={15} />} onClick={saveLdap} disabled={ldapSaving}>{ldapSaving ? 'Kaydediliyor...' : 'Kaydet'}</PrimaryButton>
        </Actions>
      </AccordionSection>

      <AccordionSection id="external-user-db" title="Harici Kullanici Veritabani" icon={<Database size={17} />} open={open.includes('external-user-db')} onToggle={toggleSection}>
        <InfoBar text="Incident kullanici adini baska sunucudaki kullanici veritabaniyla eslestirerek ad, soyad, e-posta ve departman bilgilerini zenginlestirir." />
        <Panel>
          <div style={{ display: 'flex', gap: 28, flexWrap: 'wrap' }}>
            <Toggle checked={externalDb.enabled} label="Veritabani zenginlestirme aktif" onChange={(value) => updateExternalDb('enabled', value)} />
            <Toggle checked={externalDb.encrypt} label="SSL / Encrypt" onChange={(value) => updateExternalDb('encrypt', value)} />
            <Toggle checked={externalDb.trust_server_certificate} label="Sertifikaya guven" onChange={(value) => updateExternalDb('trust_server_certificate', value)} />
          </div>
        </Panel>
        <Panel title="Baglanti Bilgileri" icon={<Cloud size={15} />}>
          <Grid>
            <Field label="Veritabani Tipi">
              <select style={inputStyle} value={externalDb.provider} onChange={(e) => updateExternalDbProvider(e.target.value as ExternalUserDbSettings['provider'])}>
                <option value="postgresql">PostgreSQL</option>
                <option value="mssql">MSSQL</option>
              </select>
            </Field>
            <Field label="Sunucu Adresi"><input style={inputStyle} value={externalDb.host} onChange={(e) => updateExternalDb('host', e.target.value)} placeholder="db.company.local" /></Field>
            <Field label="Port"><input type="number" style={inputStyle} value={externalDb.port} onChange={(e) => updateExternalDb('port', Number(e.target.value) || (externalDb.provider === 'mssql' ? 1433 : 5432))} /></Field>
            <Field label="Database"><input style={inputStyle} value={externalDb.database} onChange={(e) => updateExternalDb('database', e.target.value)} /></Field>
            <Field label="Kullanici Adi"><input style={inputStyle} value={externalDb.username} onChange={(e) => updateExternalDb('username', e.target.value)} /></Field>
            <Field label="Sifre">
              <SecretInput
                value={externalDb.password}
                placeholder={externalDb.password_set ? 'Degistirmek icin yeni sifre girin' : 'Sifre girin'}
                visible={showExternalDbPassword}
                onToggle={() => setShowExternalDbPassword((v) => !v)}
                onChange={(value) => updateExternalDb('password', value)}
                saved={externalDb.password_set}
              />
            </Field>
          </Grid>
        </Panel>
        {externalDb.provider === 'postgresql' && (
          <Panel title="Eslesme ve Kolonlar" icon={<BriefcaseBusiness size={15} />}>
            <Grid>
              <Field label="Tablo / View"><input style={inputStyle} value={externalDb.table_name} onChange={(e) => updateExternalDb('table_name', e.target.value)} placeholder="public.users" /></Field>
              <Field label="Kullanici Adi Kolonu"><input style={inputStyle} value={externalDb.match_column} onChange={(e) => updateExternalDb('match_column', e.target.value)} placeholder="username" /></Field>
              <Field label="Ad Kolonu"><input style={inputStyle} value={externalDb.first_name_column} onChange={(e) => updateExternalDb('first_name_column', e.target.value)} placeholder="first_name" /></Field>
              <Field label="Soyad Kolonu"><input style={inputStyle} value={externalDb.last_name_column} onChange={(e) => updateExternalDb('last_name_column', e.target.value)} placeholder="last_name" /></Field>
              <Field label="Tam Ad Kolonu"><input style={inputStyle} value={externalDb.full_name_column} onChange={(e) => updateExternalDb('full_name_column', e.target.value)} placeholder="display_name" /></Field>
              <Field label="E-posta Kolonu"><input style={inputStyle} value={externalDb.email_column} onChange={(e) => updateExternalDb('email_column', e.target.value)} placeholder="email" /></Field>
              <Field label="Departman / Ekip Kolonu"><input style={inputStyle} value={externalDb.department_column} onChange={(e) => updateExternalDb('department_column', e.target.value)} placeholder="department" /></Field>
              <Field label="Opsiyonel WHERE Filtresi"><input style={inputStyle} value={externalDb.where_clause} onChange={(e) => updateExternalDb('where_clause', e.target.value)} placeholder="is_active = true" /></Field>
            </Grid>
          </Panel>
        )}
        <Panel>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <Field label="Test Kullanici Adi" style={{ minWidth: 240, flex: 1 }}>
              <input style={inputStyle} value={externalDbTestUsername} onChange={(e) => setExternalDbTestUsername(e.target.value)} placeholder="DOMAIN\\kullanici veya kullanici" />
            </Field>
            <ActionButton icon={<TestTube2 size={15} />} onClick={testExternalDb} disabled={externalDbTesting}>{externalDbTesting ? 'Test ediliyor...' : 'Baglanti Testi'}</ActionButton>
            <ActionButton icon={<Eye size={15} />} onClick={testExternalDbLookup} disabled={externalDbLookupTesting}>{externalDbLookupTesting ? 'Araniyor...' : 'Kullanici Testi'}</ActionButton>
            <PrimaryButton icon={<Save size={15} />} onClick={saveExternalDb} disabled={externalDbSaving}>{externalDbSaving ? 'Kaydediliyor...' : 'Kaydet'}</PrimaryButton>
          </div>
        </Panel>
      </AccordionSection>

      <AccordionSection id="notifications" title="Bildirim Ayarlari" icon={<Bell size={17} />} open={open.includes('notifications')} onToggle={toggleSection}>
        <Panel>
          <div style={{ display: 'flex', gap: 24, alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <Toggle checked={general.email_notifications} label="E-posta Bildirimleri" onChange={(value) => updateGeneral('email_notifications', value)} />
            <Field label="Gunluk Rapor Saati"><input type="time" style={inputStyle} value={general.daily_report_time} onChange={(e) => updateGeneral('daily_report_time', e.target.value)} /></Field>
            <Field label="Yonetici E-postasi" style={{ flex: 1, minWidth: 260 }}><input type="email" style={inputStyle} value={general.admin_email} onChange={(e) => updateGeneral('admin_email', e.target.value)} /></Field>
            <PrimaryButton icon={<Save size={15} />} onClick={saveGeneral} disabled={generalSaving}>{generalSaving ? 'Kaydediliyor...' : 'Kaydet'}</PrimaryButton>
          </div>
        </Panel>
      </AccordionSection>

      <AccordionSection id="dlp" title="DLP API" icon={<Cloud size={17} />} open={open.includes('dlp')} onToggle={toggleSection}>
        <Panel title="Baglanti Bilgileri" icon={<Cloud size={15} />}>
          <Grid>
            <Field label="Manager IP"><input style={inputStyle} value={dlp.manager_ip} onChange={(e) => updateDlp('manager_ip', e.target.value)} /></Field>
            <Field label="Manager Port"><input type="number" style={inputStyle} value={dlp.manager_port} onChange={(e) => updateDlp('manager_port', Number(e.target.value) || 8443)} /></Field>
            <Field label="Zaman Asimi (sn)"><input type="number" style={inputStyle} value={dlp.timeout_seconds} onChange={(e) => updateDlp('timeout_seconds', Number(e.target.value) || 30)} /></Field>
            <Field label="Protokol">
              <select style={inputStyle} value={dlp.use_https ? 'https' : 'http'} onChange={(e) => updateDlp('use_https', e.target.value === 'https')}>
                <option value="https">HTTPS</option>
                <option value="http">HTTP</option>
              </select>
            </Field>
          </Grid>
        </Panel>
        <Panel title="Kimlik Dogrulama" icon={<KeyRound size={15} />}>
          <Grid>
            <Field label="Kullanici Adi"><input style={inputStyle} value={dlp.username} onChange={(e) => updateDlp('username', e.target.value)} /></Field>
            <Field label="Sifre">
              <SecretInput value={dlp.password} placeholder={dlp.password_set ? 'Degistirmek icin yeni sifre girin' : 'Sifre girin'} visible={showDlpPassword} onToggle={() => setShowDlpPassword((v) => !v)} onChange={(value) => updateDlp('password', value)} saved={dlp.password_set} />
            </Field>
          </Grid>
        </Panel>
        <Actions>
          <ActionButton icon={<TestTube2 size={15} />} onClick={testDlp} disabled={dlpTesting}>{dlpTesting ? 'Test ediliyor...' : 'Baglanti Testi'}</ActionButton>
          <PrimaryButton icon={<Save size={15} />} onClick={saveDlp} disabled={dlpSaving}>{dlpSaving ? 'Kaydediliyor...' : 'Kaydet'}</PrimaryButton>
        </Actions>
      </AccordionSection>

      {imapSelectedMessage && (
        <MailContentModal message={imapSelectedMessage} onClose={() => setImapSelectedMessage(null)} />
      )}
    </div>
  )
}

function MailContentModal({ message, onClose }: { message: ImapMessageContent; onClose: () => void }) {
  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      zIndex: 50,
      background: 'rgba(15,23,42,.42)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      padding: 18,
    }}>
      <div style={{
        width: 'min(920px, 100%)',
        maxHeight: '88vh',
        overflow: 'hidden',
        borderRadius: 8,
        background: 'var(--surface)',
        border: '1px solid var(--border)',
        boxShadow: 'var(--shadow-lg)',
        display: 'flex',
        flexDirection: 'column',
      }}>
        <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', gap: 12 }}>
          <div style={{ minWidth: 0 }}>
            <div style={{ fontSize: 15, fontWeight: 800, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {message.subject || 'Konu yok'}
            </div>
            <div style={{ marginTop: 6, display: 'flex', gap: 14, flexWrap: 'wrap', color: 'var(--text-secondary)', fontSize: 12 }}>
              <span>{message.from || '-'}</span>
              <span>{message.date || '-'}</span>
              {message.truncated && <span style={{ color: '#b45309', fontWeight: 700 }}>Onizleme kisaltilmis</span>}
            </div>
          </div>
          <button type="button" title="Kapat" onClick={onClose} style={{ ...buttonBase, width: 34, padding: 0, flex: '0 0 auto' }}>
            <X size={16} />
          </button>
        </div>
        <div style={{ padding: 16, overflow: 'auto', background: 'var(--background-secondary)' }}>
          <pre style={{
            margin: 0,
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Consolas, monospace',
            fontSize: 13,
            lineHeight: 1.55,
            color: 'var(--text-primary)',
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            borderRadius: 8,
            padding: 14,
          }}>
            {message.body_text || 'Gosterilecek metin icerigi bulunamadi.'}
          </pre>
        </div>
      </div>
    </div>
  )
}

function AccordionSection({ id, title, icon, open, onToggle, children }: { id: string; title: string; icon: ReactNode; open: boolean; onToggle: (id: string) => void; children: ReactNode }) {
  return (
    <section style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, marginBottom: 12, overflow: 'hidden', boxShadow: 'var(--shadow-sm)' }}>
      <button
        type="button"
        onClick={() => onToggle(id)}
        style={{
          width: '100%',
          minHeight: 46,
          padding: '0 16px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          border: 'none',
          borderBottom: open ? '3px solid rgba(59,130,246,0.28)' : '1px solid var(--border)',
          background: 'var(--surface)',
          color: 'var(--accent)',
          cursor: 'pointer',
          fontWeight: 700,
          fontSize: 16,
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>{icon}{title}</span>
        <ChevronDown size={18} style={{ transform: open ? 'rotate(180deg)' : 'rotate(0deg)', transition: 'transform .2s' }} />
      </button>
      {open && <div style={{ padding: '16px 18px 18px', background: 'var(--surface)' }}>{children}</div>}
    </section>
  )
}

function InfoBar({ text }: { text: string }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, minHeight: 38, padding: '8px 12px', borderRadius: 8, background: 'var(--surface-hover)', color: 'var(--text-secondary)', fontSize: 13 }}>
      <Info size={14} /> {text}
    </div>
  )
}

function Panel({ title, icon, children }: { title?: string; icon?: ReactNode; children: ReactNode }) {
  return (
    <div style={{ marginTop: 14, padding: 18, borderRadius: 8, background: 'var(--background-secondary)' }}>
      {title && <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 18, color: 'var(--text-secondary)', fontSize: 13, fontWeight: 800, textTransform: 'uppercase' }}>{icon}{title}</div>}
      {children}
    </div>
  )
}

function Grid({ children, columns = 'repeat(auto-fit, minmax(280px, 1fr))' }: { children: ReactNode; columns?: string }) {
  return <div style={{ display: 'grid', gridTemplateColumns: columns, gap: 16 }}>{children}</div>
}

function Field({ label, children, style }: { label: string; children: ReactNode; style?: CSSProperties }) {
  return (
    <label style={{ display: 'block', ...style }}>
      <span style={{ display: 'block', marginBottom: 7, color: 'var(--text-primary)', fontSize: 12, fontWeight: 700 }}>{label}</span>
      {children}
    </label>
  )
}

function Toggle({ checked, label, onChange }: { checked: boolean; label: string; onChange: (value: boolean) => void }) {
  return (
    <label style={{ display: 'inline-flex', alignItems: 'center', gap: 9, cursor: 'pointer', fontWeight: 700, color: 'var(--text-primary)', fontSize: 13 }}>
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} style={{ display: 'none' }} />
      <span style={{ width: 32, height: 18, borderRadius: 999, background: checked ? '#10b981' : '#94a3b8', position: 'relative', transition: 'background .2s' }}>
        <span style={{ position: 'absolute', top: 3, left: checked ? 17 : 3, width: 12, height: 12, borderRadius: 999, background: '#fff', transition: 'left .2s' }} />
      </span>
      {label}
    </label>
  )
}

function SecretInput({ value, placeholder, visible, saved, onToggle, onChange }: { value: string; placeholder: string; visible: boolean; saved: boolean; onToggle: () => void; onChange: (value: string) => void }) {
  return (
    <div>
      <div style={{ display: 'flex' }}>
        <input type={visible ? 'text' : 'password'} style={{ ...inputStyle, borderTopRightRadius: 0, borderBottomRightRadius: 0 }} value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />
        <button type="button" title={visible ? 'Gizle' : 'Goster'} onClick={onToggle} style={{ ...buttonBase, width: 42, borderTopLeftRadius: 0, borderBottomLeftRadius: 0, borderLeft: 'none', padding: 0 }}>
          {visible ? <EyeOff size={15} /> : <Eye size={15} />}
        </button>
      </div>
      {saved && <span style={{ display: 'inline-flex', marginTop: 6, padding: '2px 6px', borderRadius: 5, background: 'rgba(16,185,129,.12)', color: '#059669', fontSize: 11, fontWeight: 700 }}>Kayitli</span>}
    </div>
  )
}

function Actions({ children }: { children: ReactNode }) {
  return <div style={{ marginTop: 14, display: 'flex', gap: 10, justifyContent: 'flex-end', flexWrap: 'wrap' }}>{children}</div>
}

function ActionButton({ icon, children, onClick, disabled }: { icon: ReactNode; children: ReactNode; onClick: () => void; disabled?: boolean }) {
  return <button type="button" onClick={onClick} disabled={disabled} style={{ ...buttonBase, opacity: disabled ? .6 : 1, cursor: disabled ? 'not-allowed' : 'pointer' }}>{icon}{children}</button>
}

function PrimaryButton({ icon, children, onClick, disabled }: { icon: ReactNode; children: ReactNode; onClick: () => void; disabled?: boolean }) {
  return (
    <button type="button" onClick={onClick} disabled={disabled} style={{ ...buttonBase, borderColor: 'var(--primary)', background: 'var(--primary)', color: '#fff', opacity: disabled ? .6 : 1, cursor: disabled ? 'not-allowed' : 'pointer' }}>
      {icon}{children}
    </button>
  )
}
