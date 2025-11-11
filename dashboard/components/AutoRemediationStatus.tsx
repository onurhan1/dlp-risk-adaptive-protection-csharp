'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

import { API_URL } from '@/lib/api-config'

interface RemediationHistory {
  id: number
  incident_id: number
  action: string
  status: string
  reason: string
  notes: string
  remediated_by: string
  remediated_at: string
  auto_remediated: boolean
  user_email: string
  risk_score: number
}

export default function AutoRemediationStatus() {
  const [history, setHistory] = useState<RemediationHistory[]>([])
  const [loading, setLoading] = useState(true)
  const [enabled, setEnabled] = useState(false)

  useEffect(() => {
    fetchStatus()
    const interval = setInterval(fetchStatus, 30000) // Refresh every 30 seconds
    return () => clearInterval(interval)
  }, [])

  const fetchStatus = async () => {
    try {
      const [settingsRes, historyRes] = await Promise.all([
        axios.get(`${API_URL}/api/settings`).catch(() => ({ data: { auto_remediation: false } })),
        axios.get(`${API_URL}/remediation/history?auto_remediated=true&limit=10`).catch(() => ({ data: { history: [] } }))
      ])
      
      setEnabled(settingsRes.data?.auto_remediation || false)
      setHistory(historyRes.data?.history || [])
    } catch (error) {
      console.error('Error fetching auto remediation status:', error)
    } finally {
      setLoading(false)
    }
  }

  // Always show the card, but with different content based on enabled status
  return (
    <div className="card" style={{ marginTop: '24px', borderLeft: '4px solid var(--primary)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <div>
          <h3 style={{ margin: 0, color: 'var(--text-primary)' }}>Auto Remediation</h3>
          <p style={{ margin: '4px 0 0 0', fontSize: '14px', color: 'var(--text-secondary)' }}>
            {enabled 
              ? 'High-risk incidents are being automatically remediated'
              : 'Auto remediation is disabled. Enable it in Settings to automatically remediate high-risk incidents.'}
          </p>
        </div>
        <span
          style={{
            padding: '6px 12px',
            borderRadius: '12px',
            background: enabled ? '#dcfce7' : '#f3f4f6',
            color: enabled ? '#166534' : '#6b7280',
            fontSize: '12px',
            fontWeight: '600'
          }}
        >
          {enabled ? '✓ Active' : '○ Inactive'}
        </span>
      </div>

      {loading ? (
        <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
          Loading remediation history...
        </div>
      ) : !enabled ? (
        <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
          <p style={{ marginBottom: '12px' }}>Auto remediation is currently disabled.</p>
          <a 
            href="/settings" 
            style={{
              padding: '8px 16px',
              background: 'var(--primary)',
              color: 'white',
              textDecoration: 'none',
              borderRadius: '6px',
              fontSize: '14px',
              fontWeight: '500',
              display: 'inline-block'
            }}
          >
            Go to Settings to Enable
          </a>
        </div>
      ) : history.length === 0 ? (
        <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
          No auto-remediated incidents yet. High-risk incidents will be automatically remediated.
        </div>
      ) : (
        <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
          <table className="data-table" style={{ fontSize: '13px' }}>
            <thead>
              <tr>
                <th>Time</th>
                <th>User</th>
                <th>Risk Score</th>
                <th>Action</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {history.map((item) => (
                <tr key={item.id}>
                  <td>{new Date(item.remediated_at).toLocaleString('en-GB', {
                    day: '2-digit',
                    month: 'short',
                    hour: '2-digit',
                    minute: '2-digit'
                  })}</td>
                  <td>{item.user_email}</td>
                  <td>
                    <span style={{
                      padding: '2px 8px',
                      borderRadius: '4px',
                      background: item.risk_score >= 80 ? '#fee2e2' : '#fef3c7',
                      color: item.risk_score >= 80 ? '#991b1b' : '#92400e',
                      fontSize: '11px',
                      fontWeight: '600'
                    }}>
                      {item.risk_score}
                    </span>
                  </td>
                  <td>{item.action}</td>
                  <td>
                    <span style={{
                      padding: '2px 8px',
                      borderRadius: '4px',
                      background: item.status === 'resolved' ? '#dcfce7' : '#dbeafe',
                      color: item.status === 'resolved' ? '#166534' : '#1e40af',
                      fontSize: '11px',
                      fontWeight: '500'
                    }}>
                      {item.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

