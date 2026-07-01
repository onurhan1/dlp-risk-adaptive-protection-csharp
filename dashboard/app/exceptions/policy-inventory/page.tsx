'use client'

import React, { useState, useEffect } from 'react'
import { ClipboardList } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'
import LoadingOverlay from '@/components/ui/LoadingOverlay'
import apiClient from '@/lib/axios'
import { PolicyInventoryItem, PolicyRule, PolicyException, PolicyInventoryStats } from './_lib/types'

// Placeholders for sub-components
import Stats from './_components/PolicyInventoryStats'
import Toolbar from './_components/PolicyInventoryToolbar'
import Table from './_components/PolicyInventoryTable'
import ImportExport from './_components/PolicyInventoryImportExport'
import DeleteConfirmModal from './_components/DeleteConfirmModal'
import PolicyFormModal from './_components/PolicyFormModal'
import RuleFormModal from './_components/RuleFormModal'
import ExceptionFormModal from './_components/ExceptionFormModal'

export default function PolicyInventoryPage() {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [policies, setPolicies] = useState<PolicyInventoryItem[]>([])
  const [stats, setStats] = useState<PolicyInventoryStats>({
    totalPolicies: 0, totalRules: 0, totalExceptions: 0, activeExceptionsPercentage: 0
  })

  // Filters and Search
  const [searchQuery, setSearchQuery] = useState('')
  const [searchFilter, setSearchFilter] = useState('all')

  // Modals state
  const [isPolicyModalOpen, setIsPolicyModalOpen] = useState(false)
  const [isRuleModalOpen, setIsRuleModalOpen] = useState(false)
  const [isExceptionModalOpen, setIsExceptionModalOpen] = useState(false)
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false)

  // Current items for editing/deleting
  const [currentPolicy, setCurrentPolicy] = useState<PolicyInventoryItem | null>(null)
  const [currentRule, setCurrentRule] = useState<PolicyRule | null>(null)
  const [currentException, setCurrentException] = useState<PolicyException | null>(null)
  
  // Parent IDs for adding Rule/Exception
  const [parentPolicyId, setParentPolicyId] = useState<number | null>(null)
  const [parentRuleId, setParentRuleId] = useState<number | null>(null)

  const [deleteContext, setDeleteContext] = useState<{type: 'policy' | 'rule' | 'exception', id: number, name: string} | null>(null)

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
        setStats({
          totalPolicies: statsRes.data.totalPolicies ?? statsRes.data.total_policies ?? 0,
          totalRules: statsRes.data.totalRules ?? statsRes.data.total_rules ?? 0,
          totalExceptions: statsRes.data.totalExceptions ?? statsRes.data.total_exceptions ?? 0,
          activeExceptionsPercentage: statsRes.data.activeExceptionsPercentage ?? statsRes.data.active_exceptions_percentage ?? 0
        })
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

  const filteredPolicies = policies.filter(p => {
    const q = searchQuery.toLowerCase()
    if (!q) return true

    if (searchFilter === 'policy') {
      return p.policy_name.toLowerCase().includes(q)
    }
    if (searchFilter === 'rule') {
      return p.rules.some(r => r.rule_name.toLowerCase().includes(q))
    }
    if (searchFilter === 'exception') {
      return p.rules.some(r => r.exceptions?.some(e => e.exception_rule_name.toLowerCase().includes(q)))
    }
    if (searchFilter === 'source') {
      return p.rules.some(r => 
        r.sources?.some(s => s.resource_name.toLowerCase().includes(q)) ||
        r.exceptions?.some(e => e.sources?.some(s => s.resource_name.toLowerCase().includes(q)))
      )
    }
    if (searchFilter === 'destination') {
      return p.rules.some(r => 
        r.destinations?.some(d => d.channel_type.toLowerCase().includes(q) || d.resources?.some(res => res.resource_name.toLowerCase().includes(q))) ||
        r.exceptions?.some(e => e.destinations?.some(d => d.channel_type.toLowerCase().includes(q) || d.resources?.some(res => res.resource_name.toLowerCase().includes(q))))
      )
    }

    // Default 'all'
    return p.policy_name.toLowerCase().includes(q) ||
           p.rules.some(r => 
             r.rule_name.toLowerCase().includes(q) || 
             (r.exceptions && r.exceptions.some(e => e.exception_rule_name.toLowerCase().includes(q))) ||
             r.sources?.some(s => s.resource_name.toLowerCase().includes(q)) ||
             r.destinations?.some(d => d.resources?.some(res => res.resource_name.toLowerCase().includes(q))) ||
             r.exceptions?.some(e => e.sources?.some(s => s.resource_name.toLowerCase().includes(q))) ||
             r.exceptions?.some(e => e.destinations?.some(d => d.resources?.some(res => res.resource_name.toLowerCase().includes(q))))
           )
  })

  const handleSavePolicy = async (data: Partial<PolicyInventoryItem>) => {
    try {
      if (currentPolicy) {
        await apiClient.put(`/api/policy-inventory/policies/${currentPolicy.id}`, data)
      } else {
        await apiClient.post('/api/policy-inventory/policies', data)
      }
      setIsPolicyModalOpen(false)
      loadData()
    } catch (e) { console.error(e) }
  }

  const handleSaveRule = async (data: Partial<PolicyRule>) => {
    try {
      if (currentRule) {
        await apiClient.put(`/api/policy-inventory/rules/${currentRule.id}`, data)
      } else {
        await apiClient.post('/api/policy-inventory/rules', { ...data, policy_id: parentPolicyId })
      }
      setIsRuleModalOpen(false)
      loadData()
    } catch (e) { console.error(e) }
  }

  const handleSaveException = async (data: Partial<PolicyException>) => {
    try {
      if (currentException) {
        await apiClient.put(`/api/policy-inventory/exceptions/${currentException.id}`, data)
      } else {
        await apiClient.post('/api/policy-inventory/exceptions', { ...data, rule_id: parentRuleId })
      }
      setIsExceptionModalOpen(false)
      loadData()
    } catch (e) { console.error(e) }
  }

  const handleDeleteConfirm = async () => {
    if (!deleteContext) return
    try {
      let url = ''
      if (deleteContext.type === 'policy') url = `/api/policy-inventory/policies/${deleteContext.id}`
      if (deleteContext.type === 'rule') url = `/api/policy-inventory/rules/${deleteContext.id}`
      if (deleteContext.type === 'exception') url = `/api/policy-inventory/exceptions/${deleteContext.id}`
      await apiClient.delete(url)
      setIsDeleteModalOpen(false)
      loadData()
    } catch (e) { console.error(e) }
  }

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
            searchFilter={searchFilter}
            setSearchFilter={setSearchFilter}
            onNewPolicy={() => { setCurrentPolicy(null); setIsPolicyModalOpen(true); }}
          />

          <div style={{ position: 'relative', flex: 1 }}>
            {loading ? (
              <LoadingOverlay isLoading={true} message="Yükleniyor..." />
            ) : (
              <Table 
                policies={filteredPolicies} 
                onRefresh={loadData}
                onAddPolicy={() => { setCurrentPolicy(null); setIsPolicyModalOpen(true); }}
                onEditPolicy={(p) => { setCurrentPolicy(p); setIsPolicyModalOpen(true); }}
                onDeletePolicy={(id, name) => { setDeleteContext({type:'policy', id, name}); setIsDeleteModalOpen(true); }}
                
                onAddRule={(pId) => { setParentPolicyId(pId); setCurrentRule(null); setIsRuleModalOpen(true); }}
                onEditRule={(r, pId) => { setParentPolicyId(pId); setCurrentRule(r); setIsRuleModalOpen(true); }}
                onDeleteRule={(id, name) => { setDeleteContext({type:'rule', id, name}); setIsDeleteModalOpen(true); }}

                onAddException={(rId) => { setParentRuleId(rId); setCurrentException(null); setIsExceptionModalOpen(true); }}
                onEditException={(e, rId) => { setParentRuleId(rId); setCurrentException(e); setIsExceptionModalOpen(true); }}
                onDeleteException={(id, name) => { setDeleteContext({type:'exception', id, name}); setIsDeleteModalOpen(true); }}
              />
            )}
          </div>
        </div>
      </div>

      <PolicyFormModal 
        isOpen={isPolicyModalOpen}
        onClose={() => setIsPolicyModalOpen(false)}
        onSave={handleSavePolicy}
        initialData={currentPolicy}
      />

      <RuleFormModal 
        isOpen={isRuleModalOpen}
        onClose={() => setIsRuleModalOpen(false)}
        onSave={handleSaveRule}
        initialData={currentRule}
        policyName={policies.find(p => p.id === parentPolicyId)?.policy_name}
      />

      <ExceptionFormModal 
        isOpen={isExceptionModalOpen}
        onClose={() => setIsExceptionModalOpen(false)}
        onSave={handleSaveException}
        initialData={currentException}
        ruleName={policies.flatMap(p=>p.rules).find(r=>r.id===parentRuleId)?.rule_name}
      />

      <DeleteConfirmModal 
        isOpen={isDeleteModalOpen}
        onClose={() => setIsDeleteModalOpen(false)}
        onConfirm={handleDeleteConfirm}
        title="Silme İşlemi"
        message={`${deleteContext?.name} adlı kaydı silmek istediğinize emin misiniz? Altındaki tüm kayıtlar da silinecektir.`}
      />
    </div>
  )
}
