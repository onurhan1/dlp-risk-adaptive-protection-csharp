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
      background: 'rgba(15, 23, 42, 0.18)', zIndex: 9999,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      padding: '24px'
    }}>
      <div style={{
        background: 'var(--surface)',
        border: '1px solid var(--border)',
        borderRadius: '10px',
        width: '500px',
        maxWidth: '100%',
        boxShadow: '0 24px 70px rgba(15, 23, 42, 0.22)',
        display: 'flex', flexDirection: 'column',
        overflow: 'hidden'
      }}>
        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h2 style={{ margin: 0, fontSize: '18px', color: 'var(--text-primary)' }}>
            {initialData ? 'Politikayi Duzenle' : 'Yeni Politika Ekle'}
          </h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
            <X size={20} />
          </button>
        </div>

        <div style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>
              Politika Adi
            </label>
            <input
              type="text"
              value={policyName}
              onChange={(e) => setPolicyName(e.target.value)}
              placeholder="Orn: Muhasebe Veri Korumasi"
              style={{
                width: '100%', padding: '10px 12px', borderRadius: '6px',
                border: '1px solid var(--border)', background: 'var(--background)',
                color: 'var(--text-primary)', outline: 'none'
              }}
            />
          </div>
        </div>

        <div style={{ padding: '16px 20px', borderTop: '1px solid var(--border)', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
          <button onClick={onClose} style={{
            padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)',
            background: 'transparent', color: 'var(--text-primary)', cursor: 'pointer', fontSize: '14px'
          }}>
            Iptal
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
