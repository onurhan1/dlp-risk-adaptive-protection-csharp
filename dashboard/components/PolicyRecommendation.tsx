'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

interface PolicyRecommendationProps {
  riskScore: number
  riskLevel: string
  channel: string
  userEmail?: string
}

export default function PolicyRecommendation({ riskScore, riskLevel, channel, userEmail }: PolicyRecommendationProps) {
  const [recommendation, setRecommendation] = useState<any>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    fetchRecommendation()
  }, [riskScore, riskLevel, channel])

  const fetchRecommendation = async () => {
    setLoading(true)
    try {
      const response = await axios.post(`${API_URL}/api/policies/recommendations`, {
        risk_score: riskScore,
        channel: channel,
        user_email: userEmail
      })
      setRecommendation(response.data)
    } catch (error) {
      console.error('Error fetching recommendation:', error)
    } finally {
      setLoading(false)
    }
  }

  if (loading || !recommendation) {
    return null
  }

  const getActionColor = (action: string) => {
    switch (action.toLowerCase()) {
      case 'block': return '#f44336'
      case 'encrypt': return '#ff9800'
      case 'confirm prompt': return '#2196f3'
      default: return '#4caf50'
    }
  }

  return (
    <div className="policy-recommendation">
      <div className="recommendation-header">
        <span className="label">Recommended Action:</span>
        <span 
          className="action-badge"
          style={{ backgroundColor: getActionColor(recommendation.recommended_action) }}
        >
          {recommendation.recommended_action}
        </span>
      </div>
      <div className="recommendation-desc">
        {recommendation.description}
      </div>
      <style jsx>{`
        .policy-recommendation {
          padding: 12px;
          background: #f5f5f5;
          border-radius: 4px;
          margin-top: 8px;
        }
        .recommendation-header {
          display: flex;
          align-items: center;
          gap: 8px;
          margin-bottom: 6px;
        }
        .label {
          font-size: 12px;
          color: #666;
          font-weight: 500;
        }
        .action-badge {
          padding: 4px 8px;
          border-radius: 4px;
          color: white;
          font-size: 11px;
          font-weight: 600;
        }
        .recommendation-desc {
          font-size: 11px;
          color: #666;
          font-style: italic;
        }
      `}</style>
    </div>
  )
}

