import React from 'react'
import { AlertTriangle, X } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'

interface DeleteConfirmModalProps {
  isOpen: boolean
  onClose: () => void
  onConfirm: () => void
  title: string
  message?: string
}

export default function DeleteConfirmModal({ isOpen, onClose, onConfirm, title, message }: DeleteConfirmModalProps) {
  const { t } = useTranslation()

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
        padding: '24px',
        width: '400px',
        maxWidth: '90%',
        boxShadow: '0 10px 30px rgba(0,0,0,0.2)'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '16px' }}>
          <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
            <div style={{ width: '40px', height: '40px', borderRadius: '50%', background: 'rgba(239, 68, 68, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <AlertTriangle color="#ef4444" size={20} />
            </div>
            <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text-primary)' }}>{title}</h3>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
            <X size={20} />
          </button>
        </div>
        
        <p style={{ margin: '0 0 24px 0', fontSize: '14px', color: 'var(--text-secondary)' }}>
          {message || t('policyInventory.deleteCascadeWarning')}
        </p>

        <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
          <button onClick={onClose} style={{
            padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-color)',
            background: 'transparent', color: 'var(--text-primary)', cursor: 'pointer', fontSize: '14px'
          }}>
            İptal
          </button>
          <button onClick={() => { onConfirm(); onClose(); }} style={{
            padding: '8px 16px', borderRadius: '6px', border: 'none',
            background: '#ef4444', color: '#fff', cursor: 'pointer', fontSize: '14px'
          }}>
            Sil
          </button>
        </div>
      </div>
    </div>
  )
}
