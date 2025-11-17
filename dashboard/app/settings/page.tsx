'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

import { getApiUrlDynamic } from '@/lib/api-config'

interface Settings {
  email_notifications: boolean
  daily_report_time: string
  risk_threshold_low: number
  risk_threshold_medium: number
  risk_threshold_high: number
  admin_email: string
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

interface SplunkSettings {
  enabled: boolean
  hec_url: string
  hec_token: string
  hec_token_set: boolean
  index: string
  source: string
  sourcetype: string
}

export default function SettingsPage() {
  const [settings, setSettings] = useState<Settings>({
    email_notifications: true,
    daily_report_time: '06:00',
    risk_threshold_low: 10,
    risk_threshold_medium: 30,
    risk_threshold_high: 50,
    admin_email: ''
  })
  const [dlpSettings, setDlpSettings] = useState<DlpSettings>({
    manager_ip: '',
    manager_port: 8443,
    use_https: true,
    timeout_seconds: 30,
    username: '',
    password: '',
    password_set: false,
    last_updated: null
  })
  const [loading, setLoading] = useState(true)
  const [dlpLoading, setDlpLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [dlpSaving, setDlpSaving] = useState(false)
  const [dlpTesting, setDlpTesting] = useState(false)
  const [sendingEmail, setSendingEmail] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [dlpMessage, setDlpMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [emailConfigured, setEmailConfigured] = useState<boolean | null>(null)
  const [showPassword, setShowPassword] = useState(false)
  const [emailSettings, setEmailSettings] = useState<EmailSettings>({
    smtp_host: '',
    smtp_port: 587,
    enable_ssl: true,
    username: '',
    password: '',
    password_set: false,
    from_email: '',
    from_name: 'DLP Risk Analyzer',
    is_configured: false,
    last_updated: null
  })
  const [emailLoading, setEmailLoading] = useState(true)
  const [emailSaving, setEmailSaving] = useState(false)
  const [emailTesting, setEmailTesting] = useState(false)
  const [emailMessage, setEmailMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [showEmailPassword, setShowEmailPassword] = useState(false)
  const [splunkSettings, setSplunkSettings] = useState<SplunkSettings>({
    enabled: false,
    hec_url: '',
    hec_token: '',
    hec_token_set: false,
    index: 'dlp_risk_analyzer',
    source: 'dlp-risk-analyzer',
    sourcetype: 'dlp:audit'
  })
  const [splunkLoading, setSplunkLoading] = useState(true)
  const [splunkSaving, setSplunkSaving] = useState(false)
  const [splunkTesting, setSplunkTesting] = useState(false)
  const [splunkMessage, setSplunkMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [showSplunkToken, setShowSplunkToken] = useState(false)

  useEffect(() => {
    fetchSettings()
    fetchDlpSettings()
    fetchEmailSettings()
    fetchSplunkSettings()
  }, [])

  const fetchSettings = async () => {
    setLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/settings`).catch(() => ({ data: null }))
      if (response.data) {
        // Ensure all values are properly typed
        setSettings({
          email_notifications: response.data.email_notifications ?? true,
          daily_report_time: response.data.daily_report_time ?? '06:00',
          risk_threshold_low: Number(response.data.risk_threshold_low) || 10,
          risk_threshold_medium: Number(response.data.risk_threshold_medium) || 30,
          risk_threshold_high: Number(response.data.risk_threshold_high) || 50,
          admin_email: response.data.admin_email ?? ''
        })
      }
    } catch (error) {
      console.error('Error fetching settings:', error)
    } finally {
      setLoading(false)
    }
  }

  const fetchDlpSettings = async () => {
    setDlpLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/settings/dlp`)
      const data = response.data
      setDlpSettings({
        manager_ip: data.managerIp ?? '',
        manager_port: Number(data.managerPort) || 8443,
        use_https: data.useHttps ?? true,
        timeout_seconds: Number(data.timeoutSeconds) || 30,
        username: data.username ?? '',
        password: '',
        password_set: data.passwordSet ?? false,
        last_updated: data.updatedAt ?? null
      })
    } catch (error) {
      console.error('Error fetching DLP settings:', error)
    } finally {
      setDlpLoading(false)
    }
  }

const fetchEmailSettings = async () => {
  setEmailLoading(true)
  try {
    const apiUrl = getApiUrlDynamic()
    const response = await axios.get(`${apiUrl}/api/settings/email`)
    const data = response.data
    setEmailSettings({
      smtp_host: data.smtpHost ?? '',
      smtp_port: Number(data.smtpPort) || 587,
      enable_ssl: data.enableSsl ?? true,
      username: data.username ?? '',
      password: '',
      password_set: data.passwordSet ?? false,
      from_email: data.fromEmail ?? '',
      from_name: data.fromName ?? 'DLP Risk Analyzer',
      is_configured: data.isConfigured ?? false,
      last_updated: data.updatedAt ?? null
    })
    setEmailConfigured(data.isConfigured ?? false)
  } catch (error) {
    console.error('Error fetching SMTP settings:', error)
  } finally {
    setEmailLoading(false)
  }
}

  const saveSettings = async () => {
    setSaving(true)
    setMessage(null)
    try {
      console.log('Saving settings:', settings)
      const apiUrl = getApiUrlDynamic()
      const response = await axios.post(`${apiUrl}/api/settings`, settings, {
        headers: {
          'Content-Type': 'application/json'
        },
        timeout: 10000 // 10 second timeout
      })
      console.log('Save response:', response.data)
      // Check if response has success field or if status is 200
      if (response.data.success || response.status === 200) {
        // If response includes settings, use them directly
        if (response.data.settings) {
          // Ensure all values are properly typed
          setSettings({
            email_notifications: response.data.settings.email_notifications ?? true,
            daily_report_time: response.data.settings.daily_report_time ?? '06:00',
            risk_threshold_low: Number(response.data.settings.risk_threshold_low) || 10,
            risk_threshold_medium: Number(response.data.settings.risk_threshold_medium) || 30,
            risk_threshold_high: Number(response.data.settings.risk_threshold_high) || 50,
            admin_email: response.data.settings.admin_email ?? ''
          })
          console.log('Settings updated from response:', response.data.settings)
        } else {
          // Otherwise refresh from server
          await fetchSettings()
        }
        setMessage({ type: 'success', text: 'Settings saved successfully!' })
        setTimeout(() => setMessage(null), 3000)
      } else {
        throw new Error(response.data.message || 'Failed to save settings')
      }
    } catch (error: any) {
      console.error('Error saving settings:', error)
      let errorMessage = 'Failed to save settings'
      
      if (error.code === 'ECONNREFUSED' || error.message?.includes('Network Error') || error.message?.includes('ERR_NETWORK')) {
        const apiUrl = getApiUrlDynamic()
        errorMessage = `Network Error: Cannot connect to API. Please ensure the API is running on ${apiUrl}`
      } else if (error.response?.data?.detail) {
        errorMessage = error.response.data.detail
      } else if (error.response?.data?.message) {
        errorMessage = error.response.data.message
      } else if (error.message) {
        errorMessage = error.message
      }
      
      setMessage({ type: 'error', text: errorMessage })
      setTimeout(() => setMessage(null), 5000)
    } finally {
      setSaving(false)
    }
  }

  const updateSetting = (key: keyof Settings, value: any) => {
    setSettings({ ...settings, [key]: value })
  }

  const updateDlpSetting = (key: keyof DlpSettings, value: any) => {
    setDlpSettings({ ...dlpSettings, [key]: value })
  }

  const updateEmailSetting = (key: keyof EmailSettings, value: any) => {
    setEmailSettings({ ...emailSettings, [key]: value })
  }

  const updateSplunkSetting = (key: keyof SplunkSettings, value: any) => {
    setSplunkSettings({ ...splunkSettings, [key]: value })
  }

  const fetchSplunkSettings = async () => {
    setSplunkLoading(true)
    try {
      const apiUrl = getApiUrlDynamic()
      const response = await axios.get(`${apiUrl}/api/settings/splunk`)
      const data = response.data
      setSplunkSettings({
        enabled: data.enabled ?? false,
        hec_url: data.hec_url ?? '',
        hec_token: '',
        hec_token_set: data.hec_token_set ?? false,
        index: data.index ?? 'dlp_risk_analyzer',
        source: data.source ?? 'dlp-risk-analyzer',
        sourcetype: data.sourcetype ?? 'dlp:audit'
      })
    } catch (error) {
      console.error('Error fetching Splunk settings:', error)
    } finally {
      setSplunkLoading(false)
    }
  }

  const saveSplunkSettings = async () => {
    setSplunkSaving(true)
    setSplunkMessage(null)
    try {
      const apiUrl = getApiUrlDynamic()
      const payload = {
        enabled: splunkSettings.enabled,
        hec_url: splunkSettings.hec_url.trim(),
        hec_token: splunkSettings.hec_token.trim() || undefined,
        index: splunkSettings.index.trim(),
        source: splunkSettings.source.trim(),
        sourcetype: splunkSettings.sourcetype.trim()
      }
      const response = await axios.post(`${apiUrl}/api/settings/splunk`, payload, {
        headers: { 'Content-Type': 'application/json' },
        timeout: 15000
      })

      if (response.data?.success) {
        setSplunkSettings((prev) => ({
          ...prev,
          hec_token: '',
          hec_token_set: true
        }))
        setSplunkMessage({ type: 'success', text: 'Splunk settings saved successfully' })
        setTimeout(() => setSplunkMessage(null), 3000)
      } else {
        throw new Error(response.data?.detail || 'Failed to save Splunk settings')
      }
    } catch (error: any) {
      console.error('Error saving Splunk settings:', error)
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to save Splunk settings'
      setSplunkMessage({ type: 'error', text: errorMessage })
      setTimeout(() => setSplunkMessage(null), 5000)
    } finally {
      setSplunkSaving(false)
    }
  }

  const testSplunkConnection = async () => {
    setSplunkTesting(true)
    setSplunkMessage(null)
    try {
      const apiUrl = getApiUrlDynamic()
      const payload = {
        hec_url: splunkSettings.hec_url.trim() || undefined,
        hec_token: splunkSettings.hec_token.trim() || undefined,
        index: splunkSettings.index.trim() || undefined,
        source: splunkSettings.source.trim() || undefined,
        sourcetype: splunkSettings.sourcetype.trim() || undefined
      }
      const response = await axios.post(`${apiUrl}/api/settings/splunk/test`, payload, {
        headers: { 'Content-Type': 'application/json' },
        timeout: 15000
      })

      if (response.data?.success) {
        setSplunkMessage({ type: 'success', text: response.data.message || 'Splunk connection test successful' })
      } else {
        throw new Error(response.data?.message || 'Connection test failed')
      }
    } catch (error: any) {
      console.error('Error testing Splunk connection:', error)
      const errorMessage = error.response?.data?.message || error.response?.data?.detail || error.message || 'Connection test failed'
      setSplunkMessage({ type: 'error', text: errorMessage })
    } finally {
      setSplunkTesting(false)
      setTimeout(() => setSplunkMessage(null), 5000)
    }
  }

  const buildDlpPayload = () => {
    const trimmedPassword = dlpSettings.password?.trim()
    return {
      managerIp: dlpSettings.manager_ip,
      managerPort: dlpSettings.manager_port,
      useHttps: dlpSettings.use_https,
      timeoutSeconds: dlpSettings.timeout_seconds,
      username: dlpSettings.username,
      password: trimmedPassword ? trimmedPassword : undefined
    }
  }

  const saveDlpApiSettings = async () => {
    setDlpSaving(true)
    setDlpMessage(null)
    try {
      const apiUrl = getApiUrlDynamic()
      const payload = buildDlpPayload()
      const response = await axios.post(`${apiUrl}/api/settings/dlp`, payload, {
        headers: { 'Content-Type': 'application/json' },
        timeout: 15000
      })

      if (response.data?.success) {
        setDlpSettings((prev) => ({
          ...prev,
          manager_ip: response.data.settings.managerIp ?? prev.manager_ip,
          manager_port: Number(response.data.settings.managerPort) || prev.manager_port,
          use_https: response.data.settings.useHttps ?? prev.use_https,
          timeout_seconds: Number(response.data.settings.timeoutSeconds) || prev.timeout_seconds,
          username: response.data.settings.username ?? prev.username,
          password: '',
          password_set: true,
          last_updated: response.data.settings.updatedAt ?? new Date().toISOString()
        }))
        setDlpMessage({ type: 'success', text: 'DLP API ayarları kaydedildi' })
      } else {
        throw new Error(response.data?.detail || 'Ayarlar kaydedilemedi')
      }
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || error.message || 'DLP ayarları kaydedilemedi'
      setDlpMessage({ type: 'error', text: errorMessage })
    } finally {
      setDlpSaving(false)
      setTimeout(() => setDlpMessage(null), 5000)
    }
  }

  const testDlpApiSettings = async () => {
    setDlpTesting(true)
    setDlpMessage(null)
    try {
      const apiUrl = getApiUrlDynamic()
      const payload = buildDlpPayload()
      const response = await axios.post(`${apiUrl}/api/settings/dlp/test`, payload, {
        headers: { 'Content-Type': 'application/json' },
        timeout: 20000
      })
      setDlpMessage({ type: 'success', text: response.data?.message || 'Bağlantı başarılı' })
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || error.response?.data?.detail || error.message || 'Bağlantı testi başarısız'
      setDlpMessage({ type: 'error', text: errorMessage })
    } finally {
      setDlpTesting(false)
      setTimeout(() => setDlpMessage(null), 7000)
    }
  }

  const buildEmailPayload = () => {
    const trimmedPassword = emailSettings.password?.trim()
    return {
      smtpHost: emailSettings.smtp_host,
      smtpPort: emailSettings.smtp_port,
      enableSsl: emailSettings.enable_ssl,
      username: emailSettings.username,
      password: trimmedPassword ? trimmedPassword : undefined,
      fromEmail: emailSettings.from_email,
      fromName: emailSettings.from_name
    }
  }

  const saveEmailSettings = async () => {
    setEmailSaving(true)
    setEmailMessage(null)
    try {
      const apiUrl = getApiUrlDynamic()
      const payload = buildEmailPayload()
      const response = await axios.post(`${apiUrl}/api/settings/email`, payload, {
        headers: { 'Content-Type': 'application/json' },
        timeout: 15000
      })

      if (response.data?.success) {
        const saved = response.data.settings
        setEmailSettings({
          smtp_host: saved.smtpHost ?? '',
          smtp_port: Number(saved.smtpPort) || 587,
          enable_ssl: saved.enableSsl ?? true,
          username: saved.username ?? '',
          password: '',
          password_set: saved.passwordSet ?? false,
          from_email: saved.fromEmail ?? '',
          from_name: saved.fromName ?? 'DLP Risk Analyzer',
          is_configured: saved.isConfigured ?? false,
          last_updated: saved.updatedAt ?? new Date().toISOString()
        })
        setEmailConfigured(saved.isConfigured ?? false)
        setEmailMessage({ type: 'success', text: 'SMTP ayarları kaydedildi' })
      } else {
        throw new Error(response.data?.detail || 'SMTP ayarları kaydedilemedi')
      }
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || error.message || 'SMTP ayarları kaydedilemedi'
      setEmailMessage({ type: 'error', text: errorMessage })
    } finally {
      setEmailSaving(false)
      setTimeout(() => setEmailMessage(null), 5000)
    }
  }

  const testEmailSettings = async () => {
    setEmailTesting(true)
    setEmailMessage(null)
    try {
      const apiUrl = getApiUrlDynamic()
      const payload = {
        ...buildEmailPayload(),
        password: emailSettings.password || undefined
      }
      const response = await axios.post(`${apiUrl}/api/settings/email/test`, payload, {
        headers: { 'Content-Type': 'application/json' },
        timeout: 20000
      })
      setEmailMessage({ type: 'success', text: response.data?.message || 'SMTP bağlantısı başarılı' })
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || error.response?.data?.detail || error.message || 'SMTP testi başarısız'
      setEmailMessage({ type: 'error', text: errorMessage })
    } finally {
      setEmailTesting(false)
      setTimeout(() => setEmailMessage(null), 7000)
    }
  }

  const sendTestEmail = async () => {
    if (!settings.admin_email) {
      setMessage({ type: 'error', text: 'Please enter an email address first' })
      setTimeout(() => setMessage(null), 3000)
      return
    }

    setSendingEmail(true)
    setMessage(null)
    
    try {
      const token = localStorage.getItem('authToken')
      const apiUrl = getApiUrlDynamic()
      const response = await axios.post(
        `${apiUrl}/api/settings/send-test-email`,
        { email: settings.admin_email },
        {
          headers: token ? { Authorization: `Bearer ${token}` } : {}
        }
      )

      if (response.data.success) {
        setMessage({ type: 'success', text: response.data.message || 'Test email sent successfully!' })
        setEmailConfigured(response.data.configured ?? true)
        setTimeout(() => setMessage(null), 5000)
      } else {
        throw new Error(response.data.detail || 'Failed to send test email')
      }
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || error.response?.data?.message || error.message || 'Failed to send test email'
      setMessage({ type: 'error', text: errorMessage })
      setEmailConfigured(error.response?.data?.configured ?? null)
      setTimeout(() => setMessage(null), 5000)
    } finally {
      setSendingEmail(false)
    }
  }

  const isLoading = loading || dlpLoading

  if (isLoading) {
    return (
      <div className="dashboard-page">
        <div className="loading">Loading settings...</div>
      </div>
    )
  }

  return (
    <div className="dashboard-page">
      <div className="dashboard-header">
        <div>
          <h1>Settings</h1>
          <p className="text-muted">Configure system preferences and notifications</p>
        </div>
      </div>

      <div className="card">
        <h2>SMTP Configuration</h2>
        {emailMessage && (
          <div
            style={{
              padding: '12px 16px',
              borderRadius: '6px',
              marginBottom: '16px',
              background: emailMessage.type === 'success' ? '#dcfce7' : '#fee2e2',
              color: emailMessage.type === 'success' ? '#166534' : '#991b1b',
              border: `1px solid ${emailMessage.type === 'success' ? '#86efac' : '#fca5a5'}`
            }}
          >
            {emailMessage.text}
          </div>
        )}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '20px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>SMTP Host</label>
            <input
              type="text"
              value={emailSettings.smtp_host}
              onChange={(e) => updateEmailSetting('smtp_host', e.target.value)}
              placeholder="smtp.company.com"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Port</label>
            <input
              type="number"
              min={1}
              max={65535}
              value={emailSettings.smtp_port}
              onChange={(e) => updateEmailSetting('smtp_port', parseInt(e.target.value) || 587)}
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Username</label>
            <input
              type="text"
              value={emailSettings.username}
              onChange={(e) => updateEmailSetting('username', e.target.value)}
              placeholder="smtp_user"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Password</label>
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <input
                type={showEmailPassword ? 'text' : 'password'}
                value={emailSettings.password}
                onChange={(e) => updateEmailSetting('password', e.target.value)}
                placeholder={emailSettings.password_set ? '********' : 'Enter password'}
                style={{ flex: 1, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
              />
              {emailSettings.password_set && (
                <button
                  type="button"
                  onClick={() => updateEmailSetting('password', '')}
                  style={{
                    padding: '8px 12px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    background: 'white',
                    cursor: 'pointer'
                  }}
                >
                  Reset
                </button>
              )}
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', marginTop: '6px' }}>
              <input type="checkbox" checked={showEmailPassword} onChange={(e) => setShowEmailPassword(e.target.checked)} />
              Şifreyi göster
            </label>
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>From Email</label>
            <input
              type="email"
              value={emailSettings.from_email}
              onChange={(e) => updateEmailSetting('from_email', e.target.value)}
              placeholder="dlp@company.com"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>From Name</label>
            <input
              type="text"
              value={emailSettings.from_name}
              onChange={(e) => updateEmailSetting('from_name', e.target.value)}
              placeholder="DLP Risk Analyzer"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Use SSL/TLS</label>
            <select
              value={emailSettings.enable_ssl ? 'true' : 'false'}
              onChange={(e) => updateEmailSetting('enable_ssl', e.target.value === 'true')}
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            >
              <option value="true">Enabled</option>
              <option value="false">Disabled</option>
            </select>
          </div>
        </div>
        <div style={{ marginTop: '16px', fontSize: '13px', color: '#6b7280' }}>
          {emailSettings.last_updated ? `Son güncelleme: ${new Date(emailSettings.last_updated).toLocaleString()}` : 'Henüz yapılandırılmadı'}
        </div>
        <div style={{ marginTop: '20px', display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
          <button
            onClick={testEmailSettings}
            disabled={emailTesting}
            style={{
              padding: '10px 24px',
              background: '#0ea5e9',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: emailTesting ? 'not-allowed' : 'pointer',
              opacity: emailTesting ? 0.6 : 1
            }}
          >
            {emailTesting ? 'Testing...' : 'Test SMTP'}
          </button>
          <button
            onClick={saveEmailSettings}
            disabled={emailSaving}
            style={{
              padding: '10px 24px',
              background: 'var(--primary)',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: emailSaving ? 'not-allowed' : 'pointer',
              opacity: emailSaving ? 0.6 : 1
            }}
          >
            {emailSaving ? 'Saving...' : 'Save SMTP Settings'}
          </button>
        </div>
      </div>

      {message && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '6px',
            marginBottom: '24px',
            background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
            color: message.type === 'success' ? '#166534' : '#991b1b',
            border: `1px solid ${message.type === 'success' ? '#86efac' : '#fca5a5'}`
          }}
        >
          {message.text}
        </div>
      )}

      <div className="card">
        <h2>Notification Settings</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div>
              <label style={{ fontWeight: '500', color: 'var(--text-primary)', display: 'block', marginBottom: '4px' }}>
                Email Notifications
              </label>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)', margin: 0 }}>
                Receive email alerts for high-risk incidents
              </p>
            </div>
            <label style={{ position: 'relative', display: 'inline-block', width: '52px', height: '28px' }}>
              <input
                type="checkbox"
                checked={settings.email_notifications}
                onChange={(e) => updateSetting('email_notifications', e.target.checked)}
                style={{ opacity: 0, width: 0, height: 0 }}
              />
              <span
                style={{
                  position: 'absolute',
                  cursor: 'pointer',
                  top: 0,
                  left: 0,
                  right: 0,
                  bottom: 0,
                  backgroundColor: settings.email_notifications ? 'var(--primary)' : '#ccc',
                  borderRadius: '28px',
                  transition: '0.3s'
                }}
              >
                <span
                  style={{
                    position: 'absolute',
                    content: '""',
                    height: '22px',
                    width: '22px',
                    left: '3px',
                    bottom: '3px',
                    backgroundColor: 'white',
                    borderRadius: '50%',
                    transition: '0.3s',
                    transform: settings.email_notifications ? 'translateX(24px)' : 'translateX(0)'
                  }}
                />
              </span>
            </label>
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
              Daily Report Time
            </label>
            <input
              type="time"
              value={settings.daily_report_time}
              onChange={(e) => updateSetting('daily_report_time', e.target.value)}
              style={{
                width: '200px',
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px'
              }}
            />
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
              Administrator Email
            </label>
            <div style={{ display: 'flex', gap: '12px', alignItems: 'flex-start' }}>
              <input
                type="email"
                value={settings.admin_email}
                onChange={(e) => updateSetting('admin_email', e.target.value)}
                placeholder="admin@company.com"
                style={{
                  flex: 1,
                  maxWidth: '400px',
                  padding: '8px 12px',
                  border: '1px solid var(--border)',
                  borderRadius: '6px',
                  fontSize: '14px',
                  background: 'var(--background)',
                  color: 'var(--text-primary)'
                }}
              />
              <button
                onClick={sendTestEmail}
                disabled={sendingEmail || !settings.admin_email}
                style={{
                  padding: '8px 20px',
                  background: sendingEmail || !settings.admin_email ? '#ccc' : 'var(--primary)',
                  color: 'white',
                  border: 'none',
                  borderRadius: '6px',
                  cursor: sendingEmail || !settings.admin_email ? 'not-allowed' : 'pointer',
                  fontWeight: '500',
                  fontSize: '14px',
                  whiteSpace: 'nowrap',
                  opacity: sendingEmail || !settings.admin_email ? 0.6 : 1
                }}
              >
                {sendingEmail ? 'Sending...' : 'Send Test Email'}
              </button>
            </div>
            {emailConfigured === false && (
              <p style={{ fontSize: '12px', color: '#f59e0b', marginTop: '8px', marginBottom: 0 }}>
                ⚠️ Email service is not configured. Please configure SMTP settings below.
              </p>
            )}
            {emailConfigured === true && (
              <p style={{ fontSize: '12px', color: '#10b981', marginTop: '8px', marginBottom: 0 }}>
                ✓ Email service is configured and ready
              </p>
            )}
          </div>
        </div>
      </div>

      <div className="card">
        <h2>Risk Thresholds</h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '20px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
              Low Risk Threshold
            </label>
            <input
              type="number"
              min="0"
              max="100"
              value={settings.risk_threshold_low || 10}
              onChange={(e) => {
                const val = parseInt(e.target.value) || 10
                updateSetting('risk_threshold_low', val)
              }}
              style={{
                width: '100%',
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px'
              }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
              Medium Risk Threshold
            </label>
            <input
              type="number"
              min="0"
              max="100"
              value={settings.risk_threshold_medium || 30}
              onChange={(e) => {
                const val = parseInt(e.target.value) || 30
                updateSetting('risk_threshold_medium', val)
              }}
              style={{
                width: '100%',
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px'
              }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
              High Risk Threshold
            </label>
            <input
              type="number"
              min="0"
              max="100"
              value={settings.risk_threshold_high || 50}
              onChange={(e) => {
                const val = parseInt(e.target.value) || 50
                updateSetting('risk_threshold_high', val)
              }}
              style={{
                width: '100%',
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px'
              }}
            />
          </div>
        </div>
      </div>

      {/* Splunk Settings Card */}
      <div className="card" style={{ marginTop: '24px' }}>
        <h2>Splunk SIEM Configuration</h2>
        {splunkMessage && (
          <div
            style={{
              padding: '12px 16px',
              borderRadius: '8px',
              marginBottom: '16px',
              background: splunkMessage.type === 'success' ? '#dcfce7' : '#fee2e2',
              color: splunkMessage.type === 'success' ? '#166534' : '#991b1b',
              border: `1px solid ${splunkMessage.type === 'success' ? '#86efac' : '#fca5a5'}`
            }}
          >
            {splunkMessage.text}
          </div>
        )}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '20px' }}>
          <div>
            <label style={{ fontWeight: '500', color: 'var(--text-primary)', display: 'block', marginBottom: '4px' }}>
              Enable Splunk Integration
            </label>
            <p style={{ fontSize: '14px', color: 'var(--text-secondary)', margin: 0 }}>
              Send audit and application logs to Splunk SIEM
            </p>
          </div>
          <label style={{ position: 'relative', display: 'inline-block', width: '52px', height: '28px' }}>
            <input
              type="checkbox"
              checked={splunkSettings.enabled}
              onChange={(e) => updateSplunkSetting('enabled', e.target.checked)}
              style={{ opacity: 0, width: 0, height: 0 }}
            />
            <span
              style={{
                position: 'absolute',
                cursor: 'pointer',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                backgroundColor: splunkSettings.enabled ? 'var(--primary)' : '#ccc',
                borderRadius: '28px',
                transition: '0.3s'
              }}
            >
              <span
                style={{
                  position: 'absolute',
                  content: '""',
                  height: '22px',
                  width: '22px',
                  left: '3px',
                  bottom: '3px',
                  backgroundColor: 'white',
                  borderRadius: '50%',
                  transition: '0.3s',
                  transform: splunkSettings.enabled ? 'translateX(24px)' : 'translateX(0)'
                }}
              />
            </span>
          </label>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: '20px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>HEC URL</label>
            <input
              type="text"
              value={splunkSettings.hec_url}
              onChange={(e) => updateSplunkSetting('hec_url', e.target.value)}
              placeholder="https://splunk-server:8088/services/collector/event"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>HEC Token</label>
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <input
                type={showSplunkToken ? 'text' : 'password'}
                value={splunkSettings.hec_token}
                onChange={(e) => updateSplunkSetting('hec_token', e.target.value)}
                placeholder={splunkSettings.hec_token_set ? '********' : 'Enter HEC token'}
                style={{ flex: 1, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
              />
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', marginTop: '6px' }}>
              <input type="checkbox" checked={showSplunkToken} onChange={(e) => setShowSplunkToken(e.target.checked)} />
              Show token
            </label>
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Index</label>
            <input
              type="text"
              value={splunkSettings.index}
              onChange={(e) => updateSplunkSetting('index', e.target.value)}
              placeholder="dlp_risk_analyzer"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Source</label>
            <input
              type="text"
              value={splunkSettings.source}
              onChange={(e) => updateSplunkSetting('source', e.target.value)}
              placeholder="dlp-risk-analyzer"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Sourcetype</label>
            <input
              type="text"
              value={splunkSettings.sourcetype}
              onChange={(e) => updateSplunkSetting('sourcetype', e.target.value)}
              placeholder="dlp:audit"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
        </div>
        <div style={{ marginTop: '20px', display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
          <button
            onClick={testSplunkConnection}
            disabled={splunkTesting}
            style={{
              padding: '10px 24px',
              background: '#0ea5e9',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: splunkTesting ? 'not-allowed' : 'pointer',
              opacity: splunkTesting ? 0.6 : 1
            }}
          >
            {splunkTesting ? 'Testing...' : 'Test Connection'}
          </button>
          <button
            onClick={saveSplunkSettings}
            disabled={splunkSaving}
            style={{
              padding: '10px 24px',
              background: 'var(--primary)',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: splunkSaving ? 'not-allowed' : 'pointer',
              opacity: splunkSaving ? 0.6 : 1
            }}
          >
            {splunkSaving ? 'Saving...' : 'Save Splunk Settings'}
          </button>
        </div>
      </div>

      <div className="card">
        <h2>DLP API Configuration</h2>
        {dlpMessage && (
          <div
            style={{
              padding: '12px 16px',
              borderRadius: '6px',
              marginBottom: '16px',
              background: dlpMessage.type === 'success' ? '#dcfce7' : '#fee2e2',
              color: dlpMessage.type === 'success' ? '#166534' : '#991b1b',
              border: `1px solid ${dlpMessage.type === 'success' ? '#86efac' : '#fca5a5'}`
            }}
          >
            {dlpMessage.text}
          </div>
        )}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '20px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Manager IP / Host</label>
            <input
              type="text"
              value={dlpSettings.manager_ip}
              onChange={(e) => updateDlpSetting('manager_ip', e.target.value)}
              placeholder="172.16.245.126"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Port</label>
            <input
              type="number"
              min={1}
              max={65535}
              value={dlpSettings.manager_port}
              onChange={(e) => updateDlpSetting('manager_port', parseInt(e.target.value) || 8443)}
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Timeout (sn)</label>
            <input
              type="number"
              min={5}
              max={300}
              value={dlpSettings.timeout_seconds}
              onChange={(e) => updateDlpSetting('timeout_seconds', parseInt(e.target.value) || 30)}
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Username</label>
            <input
              type="text"
              value={dlpSettings.username}
              onChange={(e) => updateDlpSetting('username', e.target.value)}
              placeholder="dlp_api_user"
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Password</label>
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <input
                type={showPassword ? 'text' : 'password'}
                value={dlpSettings.password}
                onChange={(e) => updateDlpSetting('password', e.target.value)}
                placeholder={dlpSettings.password_set ? '********' : 'Enter password'}
                style={{ flex: 1, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
              />
              {dlpSettings.password_set && (
                <button
                  type="button"
                  onClick={() => updateDlpSetting('password', '')}
                  style={{
                    padding: '8px 12px',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    background: 'white',
                    cursor: 'pointer'
                  }}
                >
                  Reset
                </button>
              )}
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', marginTop: '6px' }}>
              <input type="checkbox" checked={showPassword} onChange={(e) => setShowPassword(e.target.checked)} />
              Şifreyi göster
            </label>
          </div>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: 'var(--text-primary)' }}>Use HTTPS</label>
            <select
              value={dlpSettings.use_https ? 'true' : 'false'}
              onChange={(e) => updateDlpSetting('use_https', e.target.value === 'true')}
              style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
            >
              <option value="true">HTTPS</option>
              <option value="false">HTTP</option>
            </select>
          </div>
        </div>
        <div style={{ marginTop: '16px', fontSize: '13px', color: '#6b7280' }}>
          {dlpSettings.last_updated ? `Son güncelleme: ${new Date(dlpSettings.last_updated).toLocaleString()}` : 'Henüz yapılandırılmadı'}
        </div>
        <div style={{ marginTop: '20px', display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
          <button
            onClick={testDlpApiSettings}
            disabled={dlpTesting}
            style={{
              padding: '10px 24px',
              background: '#0ea5e9',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: dlpTesting ? 'not-allowed' : 'pointer',
              opacity: dlpTesting ? 0.6 : 1
            }}
          >
            {dlpTesting ? 'Testing...' : 'Test Connection'}
          </button>
          <button
            onClick={saveDlpApiSettings}
            disabled={dlpSaving}
            style={{
              padding: '10px 24px',
              background: 'var(--primary)',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: dlpSaving ? 'not-allowed' : 'pointer',
              opacity: dlpSaving ? 0.6 : 1
            }}
          >
            {dlpSaving ? 'Saving...' : 'Save DLP Settings'}
          </button>
        </div>
      </div>

      <div style={{ marginTop: '24px', display: 'flex', justifyContent: 'flex-end' }}>
        <button
          onClick={saveSettings}
          disabled={saving}
          style={{
            padding: '12px 32px',
            background: 'var(--primary)',
            color: 'white',
            border: 'none',
            borderRadius: '6px',
            cursor: saving ? 'not-allowed' : 'pointer',
            fontWeight: '600',
            fontSize: '16px',
            opacity: saving ? 0.6 : 1
          }}
        >
          {saving ? 'Saving...' : 'Save Settings'}
        </button>
      </div>
    </div>
  )
}

