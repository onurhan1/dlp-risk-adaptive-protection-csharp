'use client'

import React, { useState, useEffect } from 'react'
import { ClipboardList } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import apiClient from '@/lib/axios'
import { PolicyInventoryItem, PolicyInventoryStats } from './_lib/types'

// Placeholders for sub-components
import Stats from './_components/PolicyInventoryStats'
import Toolbar from './_components/PolicyInventoryToolbar'
import Table from './_components/PolicyInventoryTable'
import ImportExport from './_components/PolicyInventoryImportExport'

export default function PolicyInventoryPage() {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [policies, setPolicies] = useState<PolicyInventoryItem[]>([])
  const [stats, setStats] = useState<PolicyInventoryStats>({
    totalPolicies: 0,
    totalRules: 0,
    totalExceptions: 0,
    activeExceptionsPercentage: 0
  })

  // Filters and Search
  const [searchQuery, setSearchQuery] = useState('')

  const loadData = async () => {
    setLoading(true)
    try {
      const [policiesRes, statsRes] = await Promise.all([
        apiClient.get('/api/policy-inventory'),
        apiClient.get('/api/policy-inventory/stats')
      ])
      
      if (policiesRes.data && policiesRes.data.data) {
        setPolicies(policiesRes.data.data)
      }
      if (statsRes.data) {
        setStats(statsRes.data)
      }
    } catch (error) {
      console.error('Failed to load policy inventory data', error)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  const filteredPolicies = policies.filter(p => 
    p.policy_name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    p.rules.some(r => r.rule_name.toLowerCase().includes(searchQuery.toLowerCase()) || 
      (r.exceptions && r.exceptions.some(e => e.exception_rule_name.toLowerCase().includes(searchQuery.toLowerCase())))
    )
  )

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px', position: 'relative' }}>
      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div style={{
              width: '38px', height: '38px', borderRadius: '10px',
              background: 'linear-gradient(135deg, #10b981, #059669)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)'
            }}>
              <ClipboardList size={20} color="#fff" />
            </div>
            <div>
              <h1 style={{ fontSize: '24px', fontWeight: '700', margin: 0, color: 'var(--text-primary)' }}>
                {t('policyInventory.title')}
              </h1>
              <p style={{ margin: 0, fontSize: '14px', color: 'var(--text-secondary)' }}>
                {t('policyInventory.subtitle')}
              </p>
            </div>
          </div>
          
          <ImportExport onImportSuccess={loadData} />
        </div>

        {/* Stats */}
        <Stats stats={stats} />

        {/* Main Content Area */}
        <div style={{
          background: 'var(--card-bg)',
          borderRadius: '16px',
          border: '1px solid var(--border-color)',
          boxShadow: '0 4px 20px rgba(0,0,0,0.05)',
          display: 'flex',
          flexDirection: 'column',
          minHeight: '500px'
        }}>
          <Toolbar 
            searchQuery={searchQuery}
            setSearchQuery={setSearchQuery}
            onNewPolicy={() => {/* TODO */}}
          />

          <div style={{ position: 'relative', flex: 1 }}>
            {loading ? (
              <LoadingOverlay isLoading={true} message="Yükleniyor..." />
            ) : (
              <Table 
                policies={filteredPolicies} 
                onRefresh={loadData}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
