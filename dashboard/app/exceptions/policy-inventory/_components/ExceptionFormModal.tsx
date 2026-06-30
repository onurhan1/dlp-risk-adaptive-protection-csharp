import React, { useState, useEffect } from 'react'
import { X, Save } from 'lucide-react'
import { PolicyException } from '../_lib/types'

interface ExceptionFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<PolicyException>) => void
  initialData?: PolicyException | null
  ruleName?: string
}

const TABS = ['Genel', 'Classifiers', 'Severity', 'Source', 'Destination']

export default function ExceptionFormModal({ isOpen, onClose, onSave, initialData, ruleName }: ExceptionFormModalProps) {
  const [activeTab, setActiveTab] = useState(0)
  const [excName, setExcName] = useState('')
  const [enabled, setEnabled] = useState('true')
  const [description, setDescription] = useState('')
  
  // Toggle states
  const [conditionEnabled, setConditionEnabled] = useState('false')
  const [sourceEnabled, setSourceEnabled] = useState('false')
  const [destinationEnabled, setDestinationEnabled] = useState('false')

  useEffect(() => {
    if (initialData) {
      setExcName(initialData.exception_rule_name || '')
      setEnabled(initialData.enabled || 'true')
      setDescription(initialData.description || '')
      setConditionEnabled(initialData.condition_enabled || 'false')
      setSourceEnabled(initialData.source_enabled || 'false')
      setDestinationEnabled(initialData.destination_enabled || 'false')
    } else {
      setExcName('')
      setEnabled('true')
      setDescription('')
      setConditionEnabled('false')
      setSourceEnabled('false')
      setDestinationEnabled('false')
    }
    setActiveTab(0)
  }, [initialData, isOpen])

  if (!isOpen) return null

  return (
    <div style={{
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      background: 'rgba(0,0,0,0.5)', zIndex: 9999,
      display: 'flex', alignItems: 'center', justifyContent: 'center'
    }}>
      <div style={{
        background: 'var(--card-bg)', border: '1px solid var(--border-color)', borderRadius: '12px',
        width: '700px', maxWidth: '95%', height: '80vh', maxHeight: '700px',
        boxShadow: '0 10px 30px rgba(0,0,0,0.2)', display: 'flex', flexDirection: 'column'
      }}>
        {/* Header */}
        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div>
            <h2 style={{ margin: 0, fontSize: '18px', color: 'var(--text-primary)' }}>
              {initialData ? 'Exception Düzenle' : 'Yeni Exception Ekle'}
            </h2>
            {ruleName && <p style={{ margin: '4px 0 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>Kural: {ruleName}</p>}
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
            <X size={20} />
          </button>
        </div>

        {/* Tabs Header */}
        <div style={{ display: 'flex', borderBottom: '1px solid var(--border-color)', padding: '0 20px', gap: '24px' }}>
          {TABS.map((tab, idx) => (
            <div 
              key={tab} 
              onClick={() => setActiveTab(idx)}
              style={{
                padding: '12px 0', fontSize: '14px', fontWeight: '500', cursor: 'pointer',
                color: activeTab === idx ? '#10b981' : 'var(--text-secondary)',
                borderBottom: activeTab === idx ? '2px solid #10b981' : '2px solid transparent',
                transition: 'all 0.2s'
              }}
            >
              {tab}
            </div>
          ))}
        </div>
        
        {/* Body Content */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '20px' }}>
          {activeTab === 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div style={{ display: 'flex', gap: '16px' }}>
                <div style={{ flex: 1 }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Exception Adı</label>
                  <input type="text" value={excName} onChange={(e) => setExcName(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }} />
                </div>
                <div style={{ width: '120px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Durum</label>
                  <select value={enabled} onChange={(e) => setEnabled(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }}>
                    <option value="true">Aktif</option>
                    <option value="false">Pasif</option>
                  </select>
                </div>
              </div>
              
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Açıklama</label>
                <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)', resize: 'none' }} />
              </div>

              <div style={{ display: 'flex', gap: '24px', marginTop: '8px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <input type="checkbox" checked={conditionEnabled === 'true'} onChange={(e) => setConditionEnabled(e.target.checked ? 'true' : 'false')} />
                  Condition Enabled
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <input type="checkbox" checked={sourceEnabled === 'true'} onChange={(e) => setSourceEnabled(e.target.checked ? 'true' : 'false')} />
                  Source Enabled
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <input type="checkbox" checked={destinationEnabled === 'true'} onChange={(e) => setDestinationEnabled(e.target.checked ? 'true' : 'false')} />
                  Destination Enabled
                </label>
              </div>
            </div>
          )}
          
          {activeTab === 1 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               {conditionEnabled === 'true' ? 'Exception Classifier listesi eklenecek.' : 'Condition pasif. Etkinleştirmek için Genel sekmesine gidin.'}
             </div>
          )}

          {activeTab === 2 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               Exception Severity tablosu eklenecek. (Örn: # Matche'a göre MEDIUM, HIGH, Permit/Block)
             </div>
          )}

          {activeTab === 3 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               {sourceEnabled === 'true' ? 'Exception Source listesi eklenecek.' : 'Source pasif. Etkinleştirmek için Genel sekmesine gidin.'}
             </div>
          )}

          {activeTab === 4 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               {destinationEnabled === 'true' ? 'Exception Destination listesi eklenecek.' : 'Destination pasif. Etkinleştirmek için Genel sekmesine gidin.'}
             </div>
          )}
        </div>

        {/* Footer */}
        <div style={{ padding: '16px 20px', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
          <button onClick={onClose} style={{
            padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-color)',
            background: 'transparent', color: 'var(--text-primary)', cursor: 'pointer', fontSize: '14px'
          }}>İptal</button>
          <button 
            onClick={() => { onSave({ exception_rule_name: excName, enabled, description, condition_enabled: conditionEnabled, source_enabled: sourceEnabled, destination_enabled: destinationEnabled }); onClose() }}
            disabled={!excName.trim()}
            style={{
              padding: '8px 16px', borderRadius: '6px', border: 'none',
              background: 'linear-gradient(135deg, #10b981, #059669)', color: '#fff', 
              cursor: excName.trim() ? 'pointer' : 'not-allowed', 
              fontSize: '14px', display: 'flex', alignItems: 'center', gap: '8px',
              opacity: excName.trim() ? 1 : 0.6
            }}
          >
            <Save size={16} /> Kaydet
          </button>
        </div>
      </div>
    </div>
  )
}
