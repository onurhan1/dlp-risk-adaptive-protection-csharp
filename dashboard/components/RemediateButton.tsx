'use client'

import { useState } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

interface RemediateButtonProps {
  incidentId: number
  currentStatus?: string
  onRemediated?: () => void
}

export default function RemediateButton({ incidentId, currentStatus, onRemediated }: RemediateButtonProps) {
  const [loading, setLoading] = useState(false)
  const [showModal, setShowModal] = useState(false)
  const [action, setAction] = useState('resolved')
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')

  const handleRemediate = async () => {
    setLoading(true)
    try {
      await axios.post(`${API_URL}/api/incidents/${incidentId}/remediate`, {
        action,
        reason,
        notes
      })
      
      setShowModal(false)
      if (onRemediated) {
        onRemediated()
      }
      alert('Incident remediated successfully')
    } catch (error: any) {
      console.error('Error remediating incident:', error)
      alert(`Failed to remediate: ${error.response?.data?.detail || error.message}`)
    } finally {
      setLoading(false)
    }
  }

  if (currentStatus === 'resolved' || currentStatus === 'false_positive') {
    return (
      <span style={{ color: '#4caf50', fontSize: '12px' }}>
        ✓ {currentStatus === 'resolved' ? 'Resolved' : 'False Positive'}
      </span>
    )
  }

  return (
    <>
      <button
        onClick={() => setShowModal(true)}
        className="remediate-btn"
        disabled={loading}
      >
        {loading ? 'Processing...' : 'Remediate'}
      </button>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h3>Remediate Incident #{incidentId}</h3>
            
            <div className="form-group">
              <label>Action:</label>
              <select value={action} onChange={(e) => setAction(e.target.value)}>
                <option value="resolved">Resolved</option>
                <option value="false_positive">False Positive</option>
                <option value="investigating">Investigating</option>
              </select>
            </div>

            <div className="form-group">
              <label>Reason:</label>
              <input
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Reason for remediation"
              />
            </div>

            <div className="form-group">
              <label>Notes:</label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Additional notes..."
                rows={4}
              />
            </div>

            <div className="modal-actions">
              <button onClick={handleRemediate} disabled={loading} className="btn-primary">
                {loading ? 'Processing...' : 'Confirm'}
              </button>
              <button onClick={() => setShowModal(false)} className="btn-secondary">
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      <style jsx>{`
        .remediate-btn {
          padding: 6px 12px;
          background: #4caf50;
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
          background: white;
          padding: 24px;
          border-radius: 8px;
          max-width: 500px;
          width: 90%;
          max-height: 90vh;
          overflow-y: auto;
        }

        .modal-content h3 {
          margin: 0 0 20px 0;
          color: #333;
        }

        .form-group {
          margin-bottom: 16px;
        }

        .form-group label {
          display: block;
          margin-bottom: 6px;
          font-weight: 500;
          color: #666;
          font-size: 14px;
        }

        .form-group select,
        .form-group input,
        .form-group textarea {
          width: 100%;
          padding: 8px;
          border: 1px solid #ddd;
          border-radius: 4px;
          font-size: 14px;
          box-sizing: border-box;
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
          background: #2196f3;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: 500;
        }

        .btn-primary:hover {
          background: #1976d2;
        }

        .btn-primary:disabled {
          opacity: 0.6;
          cursor: not-allowed;
        }

        .btn-secondary {
          padding: 10px 20px;
          background: #757575;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
        }

        .btn-secondary:hover {
          background: #616161;
        }
      `}</style>
    </>
  )
}

