'use client'

import { useState, useEffect } from 'react'
import apiClient from '@/lib/axios'
import { getApiUrlDynamic } from '@/lib/api-config'
import { OPENAI_MODELS } from '@/lib/openai-models'
import { useTranslation } from '@/components/LanguageProvider'

interface AISettings {
    openai_api_key: string
    openai_api_key_set: boolean
    copilot_api_key: string
    copilot_api_key_set: boolean
    azure_openai_endpoint: string
    azure_openai_key: string
    azure_openai_key_set: boolean
    model_provider: string
    model_name: string
    temperature: number
    max_tokens: number
    enabled: boolean
}

export default function AISettingsTab() {
    const { t } = useTranslation()
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
            const response = await apiClient.get('/api/settings/ai')
            if (response.data) {
                setSettings({
                    openai_api_key: '',
                    openai_api_key_set: response.data.openai_api_key_set ?? false,
                    copilot_api_key: '',
                    copilot_api_key_set: response.data.copilot_api_key_set ?? false,
                    azure_openai_endpoint: response.data.azure_openai_endpoint ?? '',
                    azure_openai_key: '',
                    azure_openai_key_set: response.data.azure_openai_key_set ?? false,
                    model_provider: response.data.model_provider ?? 'local',
                    model_name: response.data.model_name ?? 'gpt-4o-mini',
                    temperature: response.data.temperature ?? 0.7,
                    max_tokens: response.data.max_tokens ?? 1000,
                    enabled: response.data.enabled ?? true
                })
            }
        } catch (error: any) {
            console.error('Error fetching AI settings:', error)
            setMessage({ type: 'error', text: 'Failed to fetch AI settings' })
        } finally {
            setLoading(false)
        }
    }

    const saveSettings = async () => {
        setSaving(true)
        setMessage(null)
        try {
            const payload = {
                openai_api_key: settings.openai_api_key || undefined,
                copilot_api_key: settings.copilot_api_key || undefined,
                azure_openai_endpoint: settings.azure_openai_endpoint || undefined,
                azure_openai_key: settings.azure_openai_key || undefined,
                model_provider: settings.model_provider || 'local',
                model_name: settings.model_name || 'gpt-4o-mini',
                temperature: settings.temperature,
                max_tokens: settings.max_tokens,
                enabled: settings.enabled
            }

            const response = await apiClient.post('/api/settings/ai', payload, { timeout: 10000 })
            if (response.data.success || response.status === 200) {
                setMessage({ type: 'success', text: 'AI settings saved successfully!' })
                await new Promise(resolve => setTimeout(resolve, 500))
                await fetchSettings()
            } else {
                throw new Error(response.data.message || 'Failed to save settings')
            }
        } catch (error: any) {
            const errorMessage = error.response?.data?.detail || error.message || 'Failed to save AI settings'
            setMessage({ type: 'error', text: errorMessage })
        } finally {
            setSaving(false)
            setTimeout(() => setMessage(null), 5000)
        }
    }

    const testConnection = async (provider: string) => {
        try {
            const payload: any = { provider }
            if (provider === 'openai') {
                if (settings.openai_api_key) payload.apiKey = settings.openai_api_key
                if (settings.model_name) payload.model = settings.model_name
            } else if (provider === 'copilot' && settings.copilot_api_key) {
                payload.apiKey = settings.copilot_api_key
            } else if (provider === 'azure') {
                if (settings.azure_openai_key) payload.apiKey = settings.azure_openai_key
                if (settings.azure_openai_endpoint) payload.endpoint = settings.azure_openai_endpoint
            }

            const response = await apiClient.post('/api/settings/ai/test', payload, { timeout: 30000 })
            if (response.data.success) {
                setMessage({ type: 'success', text: response.data.message || `${provider} connection test successful!` })
            } else {
                setMessage({ type: 'error', text: response.data.message || 'Connection test failed' })
            }
        } catch (error: any) {
            setMessage({ type: 'error', text: error.response?.data?.message || 'Connection test failed' })
        }
    }

    if (loading) {
        return <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>Loading AI settings...</div>
    }

    const inputStyle = { width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }
    const btnStyle = { padding: '8px 12px', border: '1px solid var(--border)', borderRadius: '6px', background: 'var(--surface-hover)', cursor: 'pointer', fontSize: '13px' }

    return (
        <div>
            <div style={{ marginBottom: '24px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>AI Model Configuration</h3>
                <p style={{ margin: '4px 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>Configure AI providers for behavioral analysis</p>
            </div>

            {message && (
                <div style={{
                    padding: '12px 16px', borderRadius: '6px', marginBottom: '16px',
                    background: message.type === 'success' ? '#dcfce7' : '#fee2e2',
                    color: message.type === 'success' ? '#166534' : '#991b1b'
                }}>{message.text}</div>
            )}

            {/* Model Provider Selection */}
            <div style={{ background: 'var(--background)', padding: '20px', borderRadius: '8px', marginBottom: '20px' }}>
                <h4 style={{ margin: '0 0 12px', fontSize: '15px', fontWeight: 600 }}>{t('aiSettings.modelProvider')}</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {[
                        { value: 'local', label: 'Local (Z-score Baseline) - No API key required' },
                        { value: 'openai', label: 'OpenAI (ChatGPT API)' },
                        { value: 'azure', label: 'Azure OpenAI' },
                        { value: 'copilot', label: 'GitHub Copilot API' }
                    ].map(opt => (
                        <label key={opt.value} style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                            <input type="radio" name="model_provider" value={opt.value} checked={settings.model_provider === opt.value} onChange={(e) => setSettings({ ...settings, model_provider: e.target.value })} />
                            <span style={{ fontSize: '14px' }}>{opt.label}</span>
                        </label>
                    ))}
                </div>
            </div>

            {/* OpenAI Settings */}
            {settings.model_provider === 'openai' && (
                <div style={{ background: 'var(--background)', padding: '20px', borderRadius: '8px', marginBottom: '20px' }}>
                    <h4 style={{ margin: '0 0 16px', fontSize: '15px', fontWeight: 600 }}>OpenAI Configuration</h4>
                    <div style={{ marginBottom: '16px' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>API Key</label>
                        <div style={{ display: 'flex', gap: '8px' }}>
                            <input type={showOpenAIKey ? 'text' : 'password'} value={settings.openai_api_key} onChange={(e) => setSettings({ ...settings, openai_api_key: e.target.value })} placeholder={settings.openai_api_key_set ? '********' : 'sk-...'} style={{ ...inputStyle, flex: 1 }} />
                            <button type="button" onClick={() => setShowOpenAIKey(!showOpenAIKey)} style={btnStyle}>{showOpenAIKey ? 'Hide' : 'Show'}</button>
                            <button type="button" onClick={() => testConnection('openai')} style={{ ...btnStyle, background: '#0ea5e9', color: 'white', border: 'none' }}>Test</button>
                        </div>
                        {settings.openai_api_key_set && <div style={{ fontSize: '12px', color: '#10b981', marginTop: '4px' }}>✓ API key is configured</div>}
                    </div>
                    <div>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>{t('aiSettings.modelName')}</label>
                        <select value={settings.model_name} onChange={(e) => setSettings({ ...settings, model_name: e.target.value })} style={inputStyle}>
                            {OPENAI_MODELS.map((model) => <option key={model.value} value={model.value}>{model.label}</option>)}
                        </select>
                    </div>
                </div>
            )}

            {/* Azure Settings */}
            {settings.model_provider === 'azure' && (
                <div style={{ background: 'var(--background)', padding: '20px', borderRadius: '8px', marginBottom: '20px' }}>
                    <h4 style={{ margin: '0 0 16px', fontSize: '15px', fontWeight: 600 }}>Azure OpenAI Configuration</h4>
                    <div style={{ marginBottom: '16px' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>Endpoint URL</label>
                        <input type="text" value={settings.azure_openai_endpoint} onChange={(e) => setSettings({ ...settings, azure_openai_endpoint: e.target.value })} placeholder="https://your-resource.openai.azure.com" style={inputStyle} />
                    </div>
                    <div>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>API Key</label>
                        <div style={{ display: 'flex', gap: '8px' }}>
                            <input type={showAzureKey ? 'text' : 'password'} value={settings.azure_openai_key} onChange={(e) => setSettings({ ...settings, azure_openai_key: e.target.value })} placeholder={settings.azure_openai_key_set ? '********' : 'Enter API key'} style={{ ...inputStyle, flex: 1 }} />
                            <button type="button" onClick={() => setShowAzureKey(!showAzureKey)} style={btnStyle}>{showAzureKey ? 'Hide' : 'Show'}</button>
                            <button type="button" onClick={() => testConnection('azure')} style={{ ...btnStyle, background: '#0ea5e9', color: 'white', border: 'none' }}>Test</button>
                        </div>
                        {settings.azure_openai_key_set && <div style={{ fontSize: '12px', color: '#10b981', marginTop: '4px' }}>✓ API key is configured</div>}
                    </div>
                </div>
            )}

            {/* Copilot Settings */}
            {settings.model_provider === 'copilot' && (
                <div style={{ background: 'var(--background)', padding: '20px', borderRadius: '8px', marginBottom: '20px' }}>
                    <h4 style={{ margin: '0 0 16px', fontSize: '15px', fontWeight: 600 }}>GitHub Copilot Configuration</h4>
                    <div>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>API Key</label>
                        <div style={{ display: 'flex', gap: '8px' }}>
                            <input type={showCopilotKey ? 'text' : 'password'} value={settings.copilot_api_key} onChange={(e) => setSettings({ ...settings, copilot_api_key: e.target.value })} placeholder={settings.copilot_api_key_set ? '********' : 'Enter API key'} style={{ ...inputStyle, flex: 1 }} />
                            <button type="button" onClick={() => setShowCopilotKey(!showCopilotKey)} style={btnStyle}>{showCopilotKey ? 'Hide' : 'Show'}</button>
                            <button type="button" onClick={() => testConnection('copilot')} style={{ ...btnStyle, background: '#0ea5e9', color: 'white', border: 'none' }}>Test</button>
                        </div>
                        {settings.copilot_api_key_set && <div style={{ fontSize: '12px', color: '#10b981', marginTop: '4px' }}>✓ API key is configured</div>}
                    </div>
                </div>
            )}

            {/* Advanced Settings */}
            <div style={{ background: 'var(--background)', padding: '20px', borderRadius: '8px', marginBottom: '20px' }}>
                <h4 style={{ margin: '0 0 16px', fontSize: '15px', fontWeight: 600 }}>{t('aiSettings.advancedSettings')}</h4>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px' }}>
                    <div>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>Temperature (0-1)</label>
                        <input type="number" min="0" max="1" step="0.1" value={settings.temperature} onChange={(e) => setSettings({ ...settings, temperature: Number(e.target.value) })} style={inputStyle} />
                    </div>
                    <div>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '13px' }}>{t('aiSettings.maxTokens')}</label>
                        <input type="number" min="100" max="4000" value={settings.max_tokens} onChange={(e) => setSettings({ ...settings, max_tokens: Number(e.target.value) })} style={inputStyle} />
                    </div>
                </div>
                <div style={{ marginTop: '16px' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                        <input type="checkbox" checked={settings.enabled} onChange={(e) => setSettings({ ...settings, enabled: e.target.checked })} />
                        <span style={{ fontSize: '14px', fontWeight: 500 }}>Enable AI Behavioral Analysis</span>
                    </label>
                </div>
            </div>

            {/* Save Button */}
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <button onClick={saveSettings} disabled={saving} style={{
                    padding: '12px 28px',
                    background: 'var(--primary)',
                    color: 'white',
                    border: 'none',
                    borderRadius: '6px',
                    cursor: saving ? 'not-allowed' : 'pointer',
                    fontWeight: 600,
                    fontSize: '14px',
                    opacity: saving ? 0.6 : 1
                }}>
                    {saving ? `${t('settings.saving')}` : `${t('common.save')} AI`}
                </button>
            </div>
        </div>
    )
}
