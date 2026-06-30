import React, { useState, useEffect } from 'react'
import { X, Save, Plus, Trash2 } from 'lucide-react'
import { PolicyRule } from '../_lib/types'

interface RuleFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<PolicyRule>) => void
  initialData?: PolicyRule | null
  policyName?: string
}

const TABS = ['Genel', 'Classifiers', 'Severity', 'Source', 'Destination']

export default function RuleFormModal({ isOpen, onClose, onSave, initialData, policyName }: RuleFormModalProps) {
  const [activeTab, setActiveTab] = useState(0)
  const [ruleName, setRuleName] = useState('')
  const [partsCountType, setPartsCountType] = useState('CROSS_COUNT')
  const [conditionRelType, setConditionRelType] = useState('AND')
  
  // Minimal state management just for the mockup
  useEffect(() => {
    if (initialData) {
      setRuleName(initialData.rule_name || '')
      setPartsCountType(initialData.parts_count_type || 'CROSS_COUNT')
      setConditionRelType(initialData.condition_relation_type || 'AND')
    } else {
      setRuleName('')
      setPartsCountType('CROSS_COUNT')
      setConditionRelType('AND')
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
              {initialData ? 'Kuralı Düzenle' : 'Yeni Kural Ekle'}
            </h2>
            {policyName && <p style={{ margin: '4px 0 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>Politika: {policyName}</p>}
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
                color: activeTab === idx ? '#3b82f6' : 'var(--text-secondary)',
                borderBottom: activeTab === idx ? '2px solid #3b82f6' : '2px solid transparent',
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
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Kural Adı</label>
                <input type="text" value={ruleName} onChange={(e) => setRuleName(e.target.value)}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }} />
              </div>
              <div style={{ display: 'flex', gap: '16px' }}>
                <div style={{ flex: 1 }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Parts Count Type</label>
                  <select value={partsCountType} onChange={(e) => setPartsCountType(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }}>
                    <option value="CROSS_COUNT">CROSS_COUNT</option>
                    <option value="INDIVIDUAL_COUNT">INDIVIDUAL_COUNT</option>
                  </select>
                </div>
                <div style={{ flex: 1 }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Condition Relation Type</label>
                  <select value={conditionRelType} onChange={(e) => setConditionRelType(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }}>
                    <option value="AND">AND</option>
                    <option value="OR">OR</option>
                  </select>
                </div>
              </div>
            </div>
          )}
          
          {activeTab === 1 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               Classifier listesi eklenecek. (Örn: CHECK_GREATER_THAN, vb.)
             </div>
          )}

          {activeTab === 2 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               Severity Actions tablosu eklenecek. (Örn: # Matche'a göre MEDIUM, HIGH, Audit/Block)
             </div>
          )}

          {activeTab === 3 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               Source listesi eklenecek. (Örn: AD Groups, Users)
             </div>
          )}

          {activeTab === 4 && (
             <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
               Destination listesi eklenecek. (Örn: EMAIL, IM, HTTPS kanalları)
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
            onClick={() => { onSave({ rule_name: ruleName, parts_count_type: partsCountType, condition_relation_type: conditionRelType }); onClose() }}
            disabled={!ruleName.trim()}
            style={{
              padding: '8px 16px', borderRadius: '6px', border: 'none',
              background: 'linear-gradient(135deg, #3b82f6, #2563eb)', color: '#fff', 
              cursor: ruleName.trim() ? 'pointer' : 'not-allowed', 
              fontSize: '14px', display: 'flex', alignItems: 'center', gap: '8px',
              opacity: ruleName.trim() ? 1 : 0.6
            }}
          >
            <Save size={16} /> Kaydet
          </button>
        </div>
      </div>
    </div>
  )
}
