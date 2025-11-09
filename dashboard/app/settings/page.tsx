'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

interface Settings {
  email_notifications: boolean
  daily_report_time: string
  risk_threshold_low: number
  risk_threshold_medium: number
  risk_threshold_high: number
  admin_email: string
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
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [sendingEmail, setSendingEmail] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [emailConfigured, setEmailConfigured] = useState<boolean | null>(null)

  useEffect(() => {
    fetchSettings()
  }, [])

  const fetchSettings = async () => {
    setLoading(true)
    try {
      const response = await axios.get(`${API_URL}/api/settings`).catch(() => ({ data: null }))
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

  const saveSettings = async () => {
    setSaving(true)
    setMessage(null)
    try {
      console.log('Saving settings:', settings)
      const response = await axios.post(`${API_URL}/api/settings`, settings, {
        headers: {
          'Content-Type': 'application/json'
        }
      })
      console.log('Save response:', response.data)
      if (response.data.success) {
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
      const errorMessage = error.response?.data?.detail || error.response?.data?.message || error.message || 'Failed to save settings'
      setMessage({ type: 'error', text: errorMessage })
      setTimeout(() => setMessage(null), 5000)
    } finally {
      setSaving(false)
    }
  }

  const updateSetting = (key: keyof Settings, value: any) => {
    setSettings({ ...settings, [key]: value })
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
      const response = await axios.post(
        `${API_URL}/api/settings/send-test-email`,
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

  if (loading) {
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
                ⚠️ Email service is not configured. Please configure SMTP settings in appsettings.json
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

