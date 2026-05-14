'use client'

import { useState } from 'react'
import axios from 'axios'

import { getApiUrlDynamic } from '@/lib/api-config'
import { useTranslation } from '@/components/LanguageProvider'

interface RemediateButtonProps {
  incidentId: number
  currentStatus?: string
  onRemediated?: () => void
  isRemediated?: boolean
  currentAction?: string
  currentNotes?: string
}

export default function RemediateButton({ incidentId, currentStatus, onRemediated, isRemediated, currentAction, currentNotes }: RemediateButtonProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [showModal, setShowModal] = useState(false)
  const [action, setAction] = useState(currentAction || 'resolved')
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState(currentNotes || '')

  const handleRemediate = async () => {
    setLoading(true)
    try {
      const token = localStorage.getItem('authToken')
      const response = await axios.post(
        `${getApiUrlDynamic()}/api/incidents/${incidentId}/remediate`,
        {
          action,
          reason,
          notes
        },
        {
          headers: token ? { Authorization: `Bearer ${token}` } : {}
        }
      )

      setShowModal(false)
      if (onRemediated) {
        onRemediated()
      }

      // Show success message from API response if available
      const message = response.data?.message || 'Incident remediated successfully'
      alert(message)
    } catch (error: any) {
      console.error('Error remediating incident:', error)
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to remediate incident'
      alert(`Failed to remediate: ${errorMessage}`)
    } finally {
      setLoading(false)
    }
  }

  // Determine button text based on remediation status
  const buttonText = isRemediated ? t('remediate.updateStatus') : t('remediate.remediate')
  const modalTitle = isRemediated ? `${t('remediate.updateTitle')} #${incidentId}` : `${t('remediate.remediateTitle')} #${incidentId}`

  return (
    <>
      <button
        onClick={() => setShowModal(true)}
        className={isRemediated ? "update-status-btn" : "remediate-btn"}
        disabled={loading}
      >
        {loading ? t('remediate.processing') : buttonText}
      </button>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h3>{modalTitle}</h3>

            <div className="form-group">
              <label>{t('remediate.actionLabel')}</label>
              <select value={action} onChange={(e) => setAction(e.target.value)}>
                <option value="resolved">{t('remediate.resolved')}</option>
                <option value="false_positive">{t('remediate.falsePositive')}</option>
                <option value="investigating">{t('remediate.investigating')}</option>
              </select>
            </div>

            <div className="form-group">
              <label>{t('remediate.reason')}</label>
              <input
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder={t('remediate.reasonPlaceholder')}
              />
            </div>

            <div className="form-group">
              <label>{t('remediate.notes')}</label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder={t('remediate.notesPlaceholder')}
                rows={4}
              />
            </div>

            <div className="modal-actions">
              <button onClick={handleRemediate} disabled={loading} className="btn-primary">
                {loading ? t('remediate.processing') : t('remediate.confirm')}
              </button>
              <button onClick={() => setShowModal(false)} className="btn-secondary">
                {t('remediate.cancel')}
              </button>
            </div>
          </div>
        </div>
      )}

      <style jsx>{`
        .remediate-btn {
          padding: 6px 12px;
          background: #10b981;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-size: 12px;
          font-weight: 500;
        }

        .remediate-btn:hover {
          background: #45a049;
        }

        .remediate-btn:disabled {
          opacity: 0.6;
          cursor: not-allowed;
        }

        .update-status-btn {
          padding: 6px 12px;
          background: #f59e0b;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-size: 12px;
          font-weight: 500;
        }

        .update-status-btn:hover {
          background: #d97706;
        }

        .update-status-btn:disabled {
          opacity: 0.6;
          cursor: not-allowed;
        }

        .modal-overlay {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(0, 0, 0, 0.5);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10000;
        }

        .modal-content {
          background: var(--surface);
          padding: 24px;
          border-radius: 8px;
          max-width: 500px;
          width: 90%;
          max-height: 90vh;
          overflow-y: auto;
          border: 1px solid var(--border);
          box-shadow: var(--shadow-lg);
        }

        .modal-content h3 {
          margin: 0 0 20px 0;
          color: var(--text-primary);
        }

        .form-group {
          margin-bottom: 16px;
        }

        .form-group label {
          display: block;
          margin-bottom: 6px;
          font-weight: 500;
          color: var(--text-secondary);
          font-size: 14px;
        }

        .form-group select,
        .form-group input,
        .form-group textarea {
          width: 100%;
          padding: 8px;
          border: 1px solid var(--border);
          border-radius: 4px;
          font-size: 14px;
          box-sizing: border-box;
          background: var(--background);
          color: var(--text-primary);
        }

        .form-group textarea {
          resize: vertical;
        }

        .modal-actions {
          display: flex;
          gap: 12px;
          justify-content: flex-end;
          margin-top: 20px;
        }

        .btn-primary {
          padding: 10px 20px;
          background: var(--primary);
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: 500;
          transition: all 0.2s;
        }

        .btn-primary:hover {
          background: var(--primary-dark);
          transform: translateY(-1px);
          box-shadow: 0 2px 8px rgba(0, 168, 232, 0.3);
        }

        .btn-primary:disabled {
          opacity: 0.6;
          cursor: not-allowed;
          transform: none;
        }

        .btn-secondary {
          padding: 10px 20px;
          background: var(--surface-hover);
          color: var(--text-primary);
          border: 1px solid var(--border);
          border-radius: 4px;
          cursor: pointer;
          transition: all 0.2s;
        }

        .btn-secondary:hover {
          background: var(--surface-active);
          border-color: var(--border-hover);
        }
      `}</style>
    </>
  )
}

