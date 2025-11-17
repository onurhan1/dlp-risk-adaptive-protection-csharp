'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'
import { getApiUrlDynamic } from '@/lib/api-config'
import { OPENAI_MODELS } from '@/lib/openai-models'

interface AISettings {
  openai_api_key: string
  openai_api_key_set: boolean
  copilot_api_key: string
  copilot_api_key_set: boolean
  azure_openai_endpoint: string
  azure_openai_key: string
  azure_openai_key_set: boolean
  model_provider: string // 'openai', 'azure', 'copilot', 'local'
  model_name: string
  temperature: number
  max_tokens: number
  enabled: boolean
}

export default function AISettingsPage() {
  const [settings, setSettings] = useState<AISettings>({
    openai_api_key: '',
    openai_api_key_set: false,
    copilot_api_key: '',
    copilot_api_key_set: false,
    azure_openai_endpoint: '',
    azure_openai_key: '',
    azure_openai_key_set: false,
    model_provider: 'local',
    model_name: 'gpt-4o-mini',
    temperature: 0.7,
    max_tokens: 1000,
    enabled: true
  })
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [showOpenAIKey, setShowOpenAIKey] = useState(false)
  const [showCopilotKey, setShowCopilotKey] = useState(false)
  const [showAzureKey, setShowAzureKey] = useState(false)

  useEffect(() => {
    fetchSettings()
  }, [])

  const fetchSettings = async () => {
    setLoading(true)
    try {
      console.log('Fetching AI settings from:', `${getApiUrlDynamic()}/api/settings/ai`)
      const response = await apiClient.get('/api/settings/ai')
      console.log('Fetched AI settings response:', response.data)
      
      if (response.data) {
        const modelProvider = response.data.model_provider ?? 'local'
        console.log('Setting model_provider to:', modelProvider)
        
        setSettings({
          openai_api_key: '',
          openai_api_key_set: response.data.openai_api_key_set ?? false,
          copilot_api_key: '',
          copilot_api_key_set: response.data.copilot_api_key_set ?? false,
          azure_openai_endpoint: response.data.azure_openai_endpoint ?? '',
          azure_openai_key: '',
          azure_openai_key_set: response.data.azure_openai_key_set ?? false,
          model_provider: modelProvider,
          model_name: response.data.model_name ?? 'gpt-4o-mini',
          temperature: response.data.temperature ?? 0.7,
          max_tokens: response.data.max_tokens ?? 1000,
          enabled: response.data.enabled ?? true
        })
        
        console.log('Updated settings state. model_provider:', modelProvider)
      }
    } catch (error: any) {
      console.error('Error fetching AI settings:', error)
      if (error.code === 'ECONNREFUSED' || error.message?.includes('Network Error') || error.message?.includes('Failed to fetch')) {
        setMessage({ type: 'error', text: 'Network Error: Could not connect to the API server. Please ensure the backend is running on http://localhost:5001' })
      } else if (error.response?.status === 404) {
        setMessage({ type: 'error', text: 'AI Settings endpoint not found. Please ensure the API is running and updated.' })
      } else if (error.response?.status) {
        setMessage({ type: 'error', text: `Failed to fetch AI settings: ${error.response.status} ${error.response.statusText}` })
      } else {
        setMessage({ type: 'error', text: error.message || 'Failed to fetch AI settings. Please check your connection.' })
      }
    } finally {
      setLoading(false)
    }
  }

  const saveSettings = async () => {
    setSaving(true)
    setMessage(null)
    try {
      // Ensure model_provider always has a value
      const modelProvider = settings.model_provider || 'local'
      
      console.log('Saving AI settings:', {
        model_provider: modelProvider,
        model_name: settings.model_name,
        temperature: settings.temperature,
        max_tokens: settings.max_tokens,
        enabled: settings.enabled
      })
      
      const payload = {
        openai_api_key: settings.openai_api_key || undefined,
        copilot_api_key: settings.copilot_api_key || undefined,
        azure_openai_endpoint: settings.azure_openai_endpoint || undefined,
        azure_openai_key: settings.azure_openai_key || undefined,
        model_provider: modelProvider, // Always send model_provider
        model_name: settings.model_name || 'gpt-4o-mini',
        temperature: settings.temperature,
        max_tokens: settings.max_tokens,
        enabled: settings.enabled
      }
      
      const response = await apiClient.post('/api/settings/ai', payload, {
        timeout: 10000
      })
      
      if (response.data.success || response.status === 200) {
        console.log('Save successful, response:', response.data)
        setMessage({ type: 'success', text: 'AI settings saved successfully!' })
        // Small delay to ensure backend has saved the data
        await new Promise(resolve => setTimeout(resolve, 500))
        // Refresh settings to get the latest values from backend
        console.log('Refreshing settings after save...')
        await fetchSettings()
        console.log('Settings refreshed. Current model_provider:', settings.model_provider)
      } else {
        throw new Error(response.data.message || 'Failed to save settings')
      }
    } catch (error: any) {
      console.error('Error saving AI settings:', error)
      const errorMessage = error.response?.data?.detail || error.response?.data?.message || error.message || 'Failed to save AI settings'
      setMessage({ type: 'error', text: errorMessage })
    } finally {
      setSaving(false)
      setTimeout(() => setMessage(null), 5000)
    }
  }

  const testConnection = async (provider: string) => {
    try {
      const payload: any = { provider }
      
      // Include API key from input if provided
      if (provider === 'openai') {
        if (settings.openai_api_key) {
          payload.apiKey = settings.openai_api_key
        }
        if (settings.model_name) {
          payload.model = settings.model_name
        }
      } else if (provider === 'copilot') {
        if (settings.copilot_api_key) {
          payload.apiKey = settings.copilot_api_key
        }
      } else if (provider === 'azure') {
        if (settings.azure_openai_key) {
          payload.apiKey = settings.azure_openai_key
        }
        if (settings.azure_openai_endpoint) {
          payload.endpoint = settings.azure_openai_endpoint
        }
      }
      
      const response = await apiClient.post('/api/settings/ai/test', payload, {
        timeout: 30000 // 30 seconds for OpenAI API test
      })
      if (response.data.success) {
        setMessage({ type: 'success', text: response.data.message || `${provider} connection test successful!` })
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Connection test failed' })
      }
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 
                          error.response?.data?.detail || 
                          error.message || 
                          'Connection test failed'
      setMessage({ type: 'error', text: errorMessage })
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', background: 'var(--background)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <div style={{ fontSize: '18px', color: 'var(--text-secondary)' }}>Loading AI settings...</div>
      </div>
    )
  }

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '1000px', margin: '0 auto' }}>
        <div style={{ marginBottom: '32px' }}>
          <h1 style={{ fontSize: '32px', fontWeight: '700', color: 'var(--text-primary)', marginBottom: '8px' }}>
            AI Settings
          </h1>
          <p style={{ fontSize: '16px', color: 'var(--text-secondary)' }}>
            Configure AI model providers and API keys for advanced behavioral analysis
          </p>
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

        {/* Model Provider Selection */}
        <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
          <h2 style={{ fontSize: '20px', fontWeight: '600', marginBottom: '16px', color: 'var(--text-primary)' }}>
            Model Provider
          </h2>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="radio"
                name="model_provider"
                value="local"
                checked={settings.model_provider === 'local'}
                onChange={(e) => setSettings({ ...settings, model_provider: e.target.value })}
              />
              <span>Local (Z-score Baseline) - No API key required</span>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="radio"
                name="model_provider"
                value="openai"
                checked={settings.model_provider === 'openai'}
                onChange={(e) => setSettings({ ...settings, model_provider: e.target.value })}
              />
              <span>OpenAI (ChatGPT API)</span>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="radio"
                name="model_provider"
                value="azure"
                checked={settings.model_provider === 'azure'}
                onChange={(e) => setSettings({ ...settings, model_provider: e.target.value })}
              />
              <span>Azure OpenAI</span>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="radio"
                name="model_provider"
                value="copilot"
                checked={settings.model_provider === 'copilot'}
                onChange={(e) => setSettings({ ...settings, model_provider: e.target.value })}
              />
              <span>GitHub Copilot API</span>
            </label>
          </div>
        </div>

        {/* OpenAI Settings */}
        {settings.model_provider === 'openai' && (
          <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
            <h2 style={{ fontSize: '20px', fontWeight: '600', marginBottom: '16px', color: 'var(--text-primary)' }}>
              OpenAI API Configuration
            </h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                  API Key
                </label>
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <input
                    type={showOpenAIKey ? 'text' : 'password'}
                    value={settings.openai_api_key}
                    onChange={(e) => setSettings({ ...settings, openai_api_key: e.target.value })}
                    placeholder={settings.openai_api_key_set ? '********' : 'sk-...'}
                    style={{ flex: 1, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
                  />
                  <button
                    type="button"
                    onClick={() => setShowOpenAIKey(!showOpenAIKey)}
                    style={{ padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px', background: 'white', cursor: 'pointer' }}
                  >
                    {showOpenAIKey ? 'Hide' : 'Show'}
                  </button>
                  <button
                    type="button"
                    onClick={() => testConnection('openai')}
                    style={{ padding: '8px 16px', background: '#0ea5e9', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer' }}
                  >
                    Test
                  </button>
                </div>
                {settings.openai_api_key_set && (
                  <div style={{ fontSize: '12px', color: '#10b981', marginTop: '4px' }}>✓ API key is configured</div>
                )}
              </div>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                  Model Name
                </label>
                <select
                  value={settings.model_name}
                  onChange={(e) => setSettings({ ...settings, model_name: e.target.value })}
                  style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
                >
                  {OPENAI_MODELS.map((model) => (
                    <option key={model.value} value={model.value}>
                      {model.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>
        )}

        {/* Azure OpenAI Settings */}
        {settings.model_provider === 'azure' && (
          <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
            <h2 style={{ fontSize: '20px', fontWeight: '600', marginBottom: '16px', color: 'var(--text-primary)' }}>
              Azure OpenAI Configuration
            </h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                  Endpoint URL
                </label>
                <input
                  type="text"
                  value={settings.azure_openai_endpoint}
                  onChange={(e) => setSettings({ ...settings, azure_openai_endpoint: e.target.value })}
                  placeholder="https://your-resource.openai.azure.com"
                  style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                  API Key
                </label>
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <input
                    type={showAzureKey ? 'text' : 'password'}
                    value={settings.azure_openai_key}
                    onChange={(e) => setSettings({ ...settings, azure_openai_key: e.target.value })}
                    placeholder={settings.azure_openai_key_set ? '********' : 'Enter API key'}
                    style={{ flex: 1, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
                  />
                  <button
                    type="button"
                    onClick={() => setShowAzureKey(!showAzureKey)}
                    style={{ padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px', background: 'white', cursor: 'pointer' }}
                  >
                    {showAzureKey ? 'Hide' : 'Show'}
                  </button>
                  <button
                    type="button"
                    onClick={() => testConnection('azure')}
                    style={{ padding: '8px 16px', background: '#0ea5e9', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer' }}
                  >
                    Test
                  </button>
                </div>
                {settings.azure_openai_key_set && (
                  <div style={{ fontSize: '12px', color: '#10b981', marginTop: '4px' }}>✓ API key is configured</div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Copilot Settings */}
        {settings.model_provider === 'copilot' && (
          <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
            <h2 style={{ fontSize: '20px', fontWeight: '600', marginBottom: '16px', color: 'var(--text-primary)' }}>
              GitHub Copilot API Configuration
            </h2>
            <div>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                API Key
              </label>
              <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                <input
                  type={showCopilotKey ? 'text' : 'password'}
                  value={settings.copilot_api_key}
                  onChange={(e) => setSettings({ ...settings, copilot_api_key: e.target.value })}
                  placeholder={settings.copilot_api_key_set ? '********' : 'Enter Copilot API key'}
                  style={{ flex: 1, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
                />
                <button
                  type="button"
                  onClick={() => setShowCopilotKey(!showCopilotKey)}
                  style={{ padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px', background: 'white', cursor: 'pointer' }}
                >
                  {showCopilotKey ? 'Hide' : 'Show'}
                </button>
                <button
                  type="button"
                  onClick={() => testConnection('copilot')}
                  style={{ padding: '8px 16px', background: '#0ea5e9', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer' }}
                >
                  Test
                </button>
              </div>
              {settings.copilot_api_key_set && (
                <div style={{ fontSize: '12px', color: '#10b981', marginTop: '4px' }}>✓ API key is configured</div>
              )}
            </div>
          </div>
        )}

        {/* Advanced Settings */}
        <div style={{ background: 'var(--surface)', padding: '24px', borderRadius: '12px', marginBottom: '24px', border: '1px solid var(--border)' }}>
          <h2 style={{ fontSize: '20px', fontWeight: '600', marginBottom: '16px', color: 'var(--text-primary)' }}>
            Advanced Settings
          </h2>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
            <div>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                Temperature (0-1)
              </label>
              <input
                type="number"
                min="0"
                max="1"
                step="0.1"
                value={settings.temperature}
                onChange={(e) => setSettings({ ...settings, temperature: Number(e.target.value) })}
                style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
                Max Tokens
              </label>
              <input
                type="number"
                min="100"
                max="4000"
                value={settings.max_tokens}
                onChange={(e) => setSettings({ ...settings, max_tokens: Number(e.target.value) })}
                style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px' }}
              />
            </div>
          </div>
          <div style={{ marginTop: '16px' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: '500', color: 'var(--text-primary)' }}>
              <input
                type="checkbox"
                checked={settings.enabled}
                onChange={(e) => setSettings({ ...settings, enabled: e.target.checked })}
              />
              Enable AI Behavioral Analysis
            </label>
          </div>
        </div>

        {/* Save Button */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '24px' }}>
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
            {saving ? 'Saving...' : 'Save AI Settings'}
          </button>
        </div>
      </div>
    </div>
  )
}

