'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001'

interface AnomalyIndicatorProps {
  userEmail: string
}

export default function AnomalyIndicator({ userEmail }: AnomalyIndicatorProps) {
  const [anomalies, setAnomalies] = useState<any[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (userEmail) {
      fetchAnomalies()
    }
  }, [userEmail])

  const fetchAnomalies = async () => {
    setLoading(true)
    try {
      const response = await axios.get(`${API_URL}/api/risk/anomaly/detections`, {
        params: {
          user_email: userEmail,
          severity: 'High'
        }
      })
      setAnomalies(response.data.detections || [])
    } catch (error) {
      console.error('Error fetching anomalies:', error)
    } finally {
      setLoading(false)
    }
  }

  if (loading || anomalies.length === 0) {
    return null
  }

  return (
    <div className="anomaly-indicator">
      <div className="anomaly-badge">
        ⚠️ Anomaly Detected
      </div>
      <style jsx>{`
        .anomaly-indicator {
          margin: 8px 0;
        }
        .anomaly-badge {
          display: inline-flex;
          align-items: center;
          gap: 6px;
          padding: 6px 12px;
          background: #fff3cd;
          border: 1px solid #ffc107;
          border-radius: 4px;
          font-size: 12px;
          font-weight: 500;
          color: #856404;
        }
      `}</style>
    </div>
  )
}

