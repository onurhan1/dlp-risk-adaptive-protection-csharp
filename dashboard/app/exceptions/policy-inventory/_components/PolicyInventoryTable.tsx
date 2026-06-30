import React, { useState } from 'react'
import { PolicyInventoryItem, PolicyRule, PolicyException } from '../_lib/types'
import { ChevronDown, ChevronRight, Edit2, Trash2, Plus, ShieldAlert, Globe, FileWarning } from 'lucide-react'

interface TableProps {
  policies: PolicyInventoryItem[]
  onRefresh: () => void
  onAddPolicy: () => void
  onEditPolicy: (policy: PolicyInventoryItem) => void
  onDeletePolicy: (id: number, name: string) => void
  onAddRule: (policyId: number) => void
  onEditRule: (rule: PolicyRule, policyId: number) => void
  onDeleteRule: (id: number, name: string) => void
  onAddException: (ruleId: number) => void
  onEditException: (exc: PolicyException, ruleId: number) => void
  onDeleteException: (id: number, name: string) => void
}

export default function Table({ 
  policies, onRefresh,
  onAddPolicy, onEditPolicy, onDeletePolicy,
  onAddRule, onEditRule, onDeleteRule,
  onAddException, onEditException, onDeleteException
}: TableProps) {
  const [expandedPolicies, setExpandedPolicies] = useState<Record<number, boolean>>({})
  const [expandedRules, setExpandedRules] = useState<Record<number, boolean>>({})

  const togglePolicy = (id: number) => {
    setExpandedPolicies(prev => ({ ...prev, [id]: !prev[id] }))
  }

  const toggleRule = (id: number) => {
    setExpandedRules(prev => ({ ...prev, [id]: !prev[id] }))
  }

  return (
    <div style={{ padding: '20px' }}>
      {policies.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
          Kayıt bulunamadı.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {policies.map(policy => (
            <div key={policy.id} style={{ 
              border: '1px solid var(--border-color)', 
              borderRadius: '8px', 
              background: 'var(--bg-color)',
              overflow: 'hidden'
            }}>
              {/* Policy Header */}
              <div 
                style={{ 
                  padding: '16px', 
                  display: 'flex', 
                  alignItems: 'center', 
                  justifyContent: 'space-between',
                  cursor: 'pointer',
                  background: expandedPolicies[policy.id] ? 'rgba(0,0,0,0.02)' : 'transparent'
                }}
                onClick={() => togglePolicy(policy.id)}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  {expandedPolicies[policy.id] ? <ChevronDown size={20} /> : <ChevronRight size={20} />}
                  <span style={{ fontWeight: '600', fontSize: '15px', color: 'var(--text-primary)' }}>{policy.policy_name}</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '16px', color: 'var(--text-secondary)', fontSize: '13px' }}>
                  <span>{policy.rules.length} Kural</span>
                  <span>{policy.rules.reduce((acc, r) => acc + (r.exceptions?.length || 0), 0)} Exception</span>
                  <div style={{ display: 'flex', gap: '8px' }} onClick={e => e.stopPropagation()}>
                    <button className="icon-button" onClick={() => onAddRule(policy.id)} title="Kural Ekle"><Plus size={16} /></button>
                    <button className="icon-button" onClick={() => onEditPolicy(policy)} title="Politikayı Düzenle"><Edit2 size={16} /></button>
                    <button className="icon-button delete" onClick={() => onDeletePolicy(policy.id, policy.policy_name)} title="Politikayı Sil"><Trash2 size={16} /></button>
                  </div>
                </div>
              </div>

              {/* Rules List */}
              {expandedPolicies[policy.id] && (
                <div style={{ padding: '0 0 0 32px', borderTop: '1px solid var(--border-color)' }}>
                  {policy.rules.map(rule => (
                    <div key={rule.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <div 
                        style={{ 
                          padding: '12px 16px', 
                          display: 'flex', 
                          alignItems: 'center', 
                          justifyContent: 'space-between',
                          cursor: 'pointer',
                          background: expandedRules[rule.id] ? 'rgba(0,0,0,0.01)' : 'transparent'
                        }}
                        onClick={() => toggleRule(rule.id)}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                          {expandedRules[rule.id] ? <ChevronDown size={18} /> : <ChevronRight size={18} />}
                          <span style={{ fontWeight: '500', fontSize: '14px', color: 'var(--text-primary)' }}>{rule.rule_name}</span>
                          <span style={{ fontSize: '12px', padding: '2px 8px', background: 'var(--card-bg)', border: '1px solid var(--border-color)', borderRadius: '12px' }}>
                            {rule.parts_count_type} | {rule.condition_relation_type}
                          </span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '16px', color: 'var(--text-secondary)', fontSize: '13px' }}>
                          <span>{rule.exceptions?.length || 0} Exc</span>
                          <div style={{ display: 'flex', gap: '8px' }} onClick={e => e.stopPropagation()}>
                            <button className="icon-button" onClick={() => onAddException(rule.id)} title="Exception Ekle"><Plus size={14} /></button>
                            <button className="icon-button" onClick={() => onEditRule(rule, policy.id)} title="Kuralı Düzenle"><Edit2 size={14} /></button>
                            <button className="icon-button delete" onClick={() => onDeleteRule(rule.id, rule.rule_name)} title="Kuralı Sil"><Trash2 size={14} /></button>
                          </div>
                        </div>
                      </div>

                      {/* Rule Details */}
                      {expandedRules[rule.id] && (
                        <div style={{ padding: '16px 20px 24px 44px', display: 'flex', flexDirection: 'column', gap: '20px' }}>
                          
                          {/* Severity & Sources */}
                          <div style={{ display: 'flex', gap: '24px', flexWrap: 'wrap' }}>
                            
                            {/* Severity Actions Box */}
                            <div style={{ flex: 1, minWidth: '300px', background: 'var(--card-bg)', border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
                              <div style={{ padding: '10px 12px', background: 'rgba(245, 158, 11, 0.1)', borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '8px', color: '#f59e0b', fontWeight: '600', fontSize: '13px' }}>
                                <ShieldAlert size={16} />
                                SEVERITY ACTIONS
                              </div>
                              <div style={{ padding: '12px' }}>
                                {rule.severity_actions?.map(sa => (
                                  <div key={sa.id} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', padding: '4px 0', borderBottom: '1px dashed var(--border-color)' }}>
                                    <span>#{sa.number_of_matches} Match ({sa.selected === 'true' ? 'Active' : 'Inactive'})</span>
                                    <span style={{ fontWeight: 500 }}>{sa.severity_type} → {sa.action_plan}</span>
                                  </div>
                                ))}
                                {!rule.severity_actions?.length && <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Kayıt yok.</div>}
                              </div>
                            </div>

                            {/* Source/Dest Box */}
                            <div style={{ flex: 1, minWidth: '300px', background: 'var(--card-bg)', border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
                              <div style={{ padding: '10px 12px', background: 'rgba(59, 130, 246, 0.1)', borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '8px', color: '#3b82f6', fontWeight: '600', fontSize: '13px' }}>
                                <Globe size={16} />
                                SOURCE / DESTINATION
                              </div>
                              <div style={{ padding: '12px', fontSize: '13px' }}>
                                <div style={{ marginBottom: '8px' }}>
                                  <strong>Source:</strong> {rule.sources?.map(s => s.resource_name).join(', ') || '-'}
                                </div>
                                <div>
                                  <strong>Dest:</strong> {rule.destinations?.map(d => d.channel_type + (d.channel_enabled==='true'?'(Active)':'(Inactive)')).join(', ') || '-'}
                                </div>
                              </div>
                            </div>

                          </div>

                          {/* Exceptions List */}
                          <div style={{ background: 'var(--card-bg)', border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
                            <div style={{ padding: '10px 12px', background: 'rgba(16, 185, 129, 0.1)', borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', color: '#10b981', fontWeight: '600', fontSize: '13px' }}>
                              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}><FileWarning size={16} /> EXCEPTIONS</div>
                              <button style={{ background: 'none', border: 'none', color: '#10b981', cursor: 'pointer' }}><Plus size={16} /></button>
                            </div>
                            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                              <thead>
                                <tr style={{ background: 'rgba(0,0,0,0.02)', textAlign: 'left' }}>
                                  <th style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)' }}>Name</th>
                                  <th style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)' }}>Enabled</th>
                                  <th style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)' }}>Severity</th>
                                  <th style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)' }}>Action Plan</th>
                                  <th style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)' }}>Actions</th>
                                </tr>
                              </thead>
                              <tbody>
                                {rule.exceptions?.map(exc => (
                                  <tr key={exc.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                                    <td style={{ padding: '8px 12px' }}>{exc.exception_rule_name}</td>
                                    <td style={{ padding: '8px 12px' }}>{exc.enabled === 'true' ? '✅' : '❌'}</td>
                                    <td style={{ padding: '8px 12px' }}>{exc.severity_actions?.[0]?.severity_type || '-'}</td>
                                    <td style={{ padding: '8px 12px' }}>{exc.severity_actions?.[0]?.action_plan || '-'}</td>
                                    <td style={{ padding: '8px 12px' }}>
                                      <div style={{ display: 'flex', gap: '8px' }}>
                                        <button className="icon-button" style={{ padding: '4px' }} onClick={() => onEditException(exc, rule.id)} title="Exception Düzenle"><Edit2 size={14} /></button>
                                        <button className="icon-button delete" style={{ padding: '4px' }} onClick={() => onDeleteException(exc.id, exc.exception_rule_name)} title="Exception Sil"><Trash2 size={14} /></button>
                                      </div>
                                    </td>
                                  </tr>
                                ))}
                                {!rule.exceptions?.length && (
                                  <tr><td colSpan={5} style={{ padding: '12px', textAlign: 'center', color: 'var(--text-secondary)' }}>Exception bulunamadı.</td></tr>
                                )}
                              </tbody>
                            </table>
                          </div>

                        </div>
                      )}
                    </div>
                  ))}
                  {!policy.rules.length && <div style={{ padding: '16px', color: 'var(--text-secondary)' }}>Kural bulunamadı.</div>}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
      <style dangerouslySetInnerHTML={{__html: `
        .icon-button { background: none; border: none; color: var(--text-secondary); cursor: pointer; padding: 6px; border-radius: 4px; transition: all 0.2s; display: flex; align-items: center; justify-content: center; }
        .icon-button:hover { background: var(--border-color); color: var(--text-primary); }
        .icon-button.delete:hover { background: rgba(239, 68, 68, 0.1); color: #ef4444; }
      `}} />
    </div>
  )
}
