import React, { useState, useEffect } from 'react'
import { X, Save } from 'lucide-react'
import { PolicyInventoryItem } from '../_lib/types'

interface PolicyFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<PolicyInventoryItem>) => void
  initialData?: PolicyInventoryItem | null
}

export default function PolicyFormModal({ isOpen, onClose, onSave, initialData }: PolicyFormModalProps) {
  const [policyName, setPolicyName] = useState('')

  useEffect(() => {
    if (initialData) {
      setPolicyName(initialData.policy_name)
    } else {
      setPolicyName('')
    }
  }, [initialData, isOpen])

  if (!isOpen) return null

  return (
    <div style={{
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      background: 'rgba(0,0,0,0.5)', zIndex: 9999,
      display: 'flex', alignItems: 'center', justifyContent: 'center'
    }}>
      <div style={{
        background: 'var(--card-bg)',
        border: '1px solid var(--border-color)',
        borderRadius: '12px',
        width: '500px',
        maxWidth: '90%',
        boxShadow: '0 10px 30px rgba(0,0,0,0.2)',
        display: 'flex', flexDirection: 'column'
      }}>
        {/* Header */}
        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h2 style={{ margin: 0, fontSize: '18px', color: 'var(--text-primary)' }}>
            {initialData ? 'Politikayı Düzenle' : 'Yeni Politika Ekle'}
          </h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
            <X size={20} />
          </button>
        </div>
        
        {/* Body */}
        <div style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>
              Politika Adı
            </label>
            <input
              type="text"
              value={policyName}
              onChange={(e) => setPolicyName(e.target.value)}
              placeholder="Örn: Muhasebe Veri Koruması"
              style={{
                width: '100%', padding: '10px 12px', borderRadius: '6px',
                border: '1px solid var(--border-color)', background: 'var(--bg-color)',
                color: 'var(--text-primary)', outline: 'none'
              }}
            />
          </div>
        </div>

        {/* Footer */}
        <div style={{ padding: '16px 20px', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
          <button onClick={onClose} style={{
            padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-color)',
            background: 'transparent', color: 'var(--text-primary)', cursor: 'pointer', fontSize: '14px'
          }}>
            İptal
          </button>
          <button 
            onClick={() => { onSave({ policy_name: policyName }); onClose() }}
            disabled={!policyName.trim()}
            style={{
              padding: '8px 16px', borderRadius: '6px', border: 'none',
              background: 'linear-gradient(135deg, #10b981, #059669)', color: '#fff', 
              cursor: policyName.trim() ? 'pointer' : 'not-allowed', 
              fontSize: '14px', display: 'flex', alignItems: 'center', gap: '8px',
              opacity: policyName.trim() ? 1 : 0.6
            }}
          >
            <Save size={16} /> Kaydet
          </button>
        </div>
      </div>
    </div>
  )
}
