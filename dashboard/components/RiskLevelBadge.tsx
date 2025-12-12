'use client'

interface RiskLevelBadgeProps {
  riskScore: number
  showScore?: boolean
  size?: 'small' | 'medium' | 'large'
}

export default function RiskLevelBadge({ 
  riskScore, 
  showScore = true,
  size = 'medium'
}: RiskLevelBadgeProps) {
  const getRiskColor = (score: number) => {
    if (score >= 70) return '#ef4444' // Critical - Red
    if (score >= 50) return '#f59e0b' // High - Orange
    if (score >= 30) return '#fbbf24' // Medium - Yellow
    if (score >= 10) return '#10b981' // Low - Green
    return '#6b7280' // Safe - Gray
  }

  const getRiskLabel = (score: number) => {
    if (score >= 70) return 'Critical'
    if (score >= 50) return 'High'
    if (score >= 30) return 'Medium'
    if (score >= 10) return 'Low'
    return 'Safe'
  }

  const sizeClass = size === 'small' ? '24px' : size === 'large' ? '40px' : '32px'
  const fontSize = size === 'small' ? '12px' : size === 'large' ? '16px' : '14px'

  return (
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: '8px' }}>
      <div
        className="badge-circle"
        style={{
          width: sizeClass,
          height: sizeClass,
          borderRadius: '50%',
          backgroundColor: getRiskColor(riskScore),
          color: 'white',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontWeight: 700,
          fontSize: fontSize,
          flexShrink: 0
        }}
      >
        {riskScore}
      </div>
      {showScore && (
        <span style={{ 
          fontSize: '12px', 
          color: '#64748b',
          fontWeight: 500
        }}>
          {getRiskLabel(riskScore)}
        </span>
      )}
    </div>
  )
}
