import React from 'react'
import { PolicyInventoryStats } from '../_lib/types'
import { Shield, FileWarning, Layers, Activity } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'

export default function Stats({ stats }: { stats: PolicyInventoryStats }) {
  const { t } = useTranslation()
  
  const statCards = [
    {
      title: t('policyInventory.totalPolicies'),
      value: stats.totalPolicies,
      icon: <Shield size={20} color="#10b981" />,
      bg: 'rgba(16, 185, 129, 0.1)'
    },
    {
      title: t('policyInventory.totalRules'),
      value: stats.totalRules,
      icon: <Layers size={20} color="#3b82f6" />,
      bg: 'rgba(59, 130, 246, 0.1)'
    },
    {
      title: t('policyInventory.totalExceptions'),
      value: stats.totalExceptions,
      icon: <FileWarning size={20} color="#f59e0b" />,
      bg: 'rgba(245, 158, 11, 0.1)'
    },
    {
      title: t('policyInventory.activeExceptions'),
      value: `%${stats.activeExceptionsPercentage}`,
      icon: <Activity size={20} color="#8b5cf6" />,
      bg: 'rgba(139, 92, 246, 0.1)'
    }
  ]

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
      {statCards.map((card, idx) => (
        <div key={idx} style={{
          background: 'var(--card-bg)',
          borderRadius: '12px',
          padding: '20px',
          display: 'flex',
          alignItems: 'center',
          gap: '16px',
          border: '1px solid var(--border-color)',
          boxShadow: '0 2px 8px rgba(0,0,0,0.02)'
        }}>
          <div style={{
            width: '48px', height: '48px', borderRadius: '50%',
            background: card.bg, display: 'flex', alignItems: 'center', justifyContent: 'center'
          }}>
            {card.icon}
          </div>
          <div>
            <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '4px' }}>
              {card.title}
            </div>
            <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--text-primary)' }}>
              {card.value}
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}
