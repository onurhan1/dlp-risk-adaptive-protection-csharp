'use client'
import React, { useState } from 'react'
import {
  ChevronDown, ChevronRight, Plus, Edit2, Trash2,
  ShieldAlert, Globe, FileWarning, Cpu, Mail,
  Network, HardDrive, Monitor, Wifi, Server,
  AlertTriangle, CheckCircle, XCircle, Info
} from 'lucide-react'
import { PolicyInventoryItem, PolicyRule, PolicyException } from '../_lib/types'

interface Props {
  policies: PolicyInventoryItem[]
  onRefresh: () => void
  onAddPolicy: () => void
  onEditPolicy: (p: PolicyInventoryItem) => void
  onDeletePolicy: (id: number, name: string) => void
  onAddRule: (policyId: number) => void
  onEditRule: (rule: PolicyRule, policyId: number) => void
  onDeleteRule: (id: number, name: string) => void
  onAddException: (ruleId: number) => void
  onEditException: (exc: PolicyException, ruleId: number) => void
  onDeleteException: (id: number, name: string) => void
}

const CHANNEL_ICONS: Record<string, React.ReactNode> = {
  EMAIL: <Mail size={12} />,
  HTTP: <Globe size={12} />,
  HTTPS: <Globe size={12} />,
  FTP: <HardDrive size={12} />,
  IM: <Network size={12} />,
  ENDPOINT_HTTP: <Monitor size={12} />,
  ENDPOINT_HTTPS: <Monitor size={12} />,
  ENDPOINT_APPLICATION: <Monitor size={12} />,
  ENDPOINT_LAN: <Wifi size={12} />,
  CASB_NEAR_REAL_TIME: <Server size={12} />,
}

const SEVERITY_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  LOW:      { bg: 'rgba(34, 197, 94, 0.12)',  text: '#22c55e', border: 'rgba(34, 197, 94, 0.3)' },
  MEDIUM:   { bg: 'rgba(245, 158, 11, 0.12)', text: '#f59e0b', border: 'rgba(245, 158, 11, 0.3)' },
  HIGH:     { bg: 'rgba(239, 68, 68, 0.12)',  text: '#ef4444', border: 'rgba(239, 68, 68, 0.3)' },
  CRITICAL: { bg: 'rgba(168, 85, 247, 0.12)', text: '#a855f7', border: 'rgba(168, 85, 247, 0.3)' },
}

function SeverityBadge({ severity, selected }: { severity?: string; selected?: string }) {
  const s = (severity || '').toUpperCase()
  const colors = SEVERITY_COLORS[s] || { bg: 'rgba(100,100,100,0.1)', text: 'var(--text-secondary)', border: 'rgba(100,100,100,0.2)' }
  const icon = selected === 'true'
    ? <CheckCircle size={11} />
    : selected === 'false'
    ? <XCircle size={11} />
    : null
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: '4px',
      padding: '2px 8px', borderRadius: '999px', fontSize: '11px', fontWeight: 600,
      background: colors.bg, color: colors.text, border: `1px solid ${colors.border}`
    }}>
      {icon}{s || '-'}
    </span>
  )
}

function ChannelBadge({ channelType, enabled }: { channelType: string; enabled?: string }) {
  const isActive = enabled === 'true'
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: '4px',
      padding: '2px 7px', borderRadius: '6px', fontSize: '11px', fontWeight: 500,
      background: isActive ? 'rgba(59,130,246,0.12)' : 'rgba(100,100,100,0.08)',
      color: isActive ? '#60a5fa' : 'var(--text-secondary)',
      border: `1px solid ${isActive ? 'rgba(59,130,246,0.25)' : 'rgba(100,100,100,0.15)'}`,
      textDecoration: isActive ? 'none' : 'line-through',
      opacity: isActive ? 1 : 0.65
    }}>
      {CHANNEL_ICONS[channelType] || <Network size={12} />}
      {channelType}
    </span>
  )
}

function SectionHeader({ icon, label, color, count }: { icon: React.ReactNode; label: string; color: string; count?: number }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: '8px',
      padding: '9px 14px',
      background: `${color}18`,
      borderBottom: `1px solid ${color}25`,
    }}>
      <span style={{ color, display: 'flex' }}>{icon}</span>
      <span style={{ fontSize: '12px', fontWeight: 700, color, textTransform: 'uppercase', letterSpacing: '0.05em' }}>{label}</span>
      {count !== undefined && (
        <span style={{
          marginLeft: 'auto', fontSize: '11px', padding: '1px 7px', borderRadius: '999px',
          background: `${color}22`, color, border: `1px solid ${color}30`
        }}>{count}</span>
      )}
    </div>
  )
}

function RuleDetailPanel({ 
  rule, onEditException, onDeleteException 
}: { 
  rule: PolicyRule, 
  onEditException: (exc: PolicyException) => void,
  onDeleteException: (id: number, name: string) => void
}) {
  const [expandedExceptions, setExpandedExceptions] = useState<Record<number, boolean>>({})
  const [expandedDestinations, setExpandedDestinations] = useState<Record<string, boolean>>({})

  const toggleException = (id: number) => setExpandedExceptions(p => ({ ...p, [id]: !p[id] }))
  const toggleDestination = (key: string) => setExpandedDestinations(p => ({ ...p, [key]: !p[key] }))

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', padding: '16px 20px 20px 48px' }}>

      {/* Classifiers */}
      {(rule.classifiers?.length ?? 0) > 0 && (
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<Cpu size={14} />} label="Classifiers" color="#8b5cf6" count={rule.classifiers!.length} />
          <div style={{ padding: '10px 14px', display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
            {rule.classifiers!.map((c, i) => (
              <div key={i} style={{
                background: 'rgba(139,92,246,0.08)', border: '1px solid rgba(139,92,246,0.2)',
                borderRadius: '8px', padding: '6px 12px', fontSize: '12px'
              }}>
                <div style={{ fontWeight: 600, color: '#a78bfa' }}>{c.classifier_name}</div>
                <div style={{ color: 'var(--text-secondary)', fontSize: '11px', marginTop: '2px' }}>
                  {c.threshold_type} {c.threshold_value_from !== undefined && c.threshold_value_from !== null ? `≥ ${c.threshold_value_from}` : ''} {c.threshold_calculate_type}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
        {/* Severity Actions */}
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<ShieldAlert size={14} />} label="Severity Actions" color="#f59e0b" count={rule.severity_actions?.length} />
          <div style={{ padding: '10px 14px' }}>
            {rule.severity_actions?.length ? rule.severity_actions.map((sa, i) => (
              <div key={i} style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                padding: '5px 0', borderBottom: i < rule.severity_actions!.length - 1 ? '1px dashed var(--border-color)' : 'none',
                fontSize: '12px'
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <SeverityBadge severity={sa.severity_type} selected={sa.selected} />
                  {sa.number_of_matches !== undefined && sa.number_of_matches !== null && (
                    <span style={{ color: 'var(--text-secondary)', fontSize: '11px' }}>#{sa.number_of_matches}</span>
                  )}
                </div>
                <span style={{ color: 'var(--text-secondary)', fontSize: '11px' }}>{sa.action_plan || '-'}</span>
              </div>
            )) : <div style={{ fontSize: '12px', color: 'var(--text-secondary)', padding: '4px 0' }}>Kayıt yok</div>}
          </div>
        </div>

        {/* Source & Destination */}
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<Globe size={14} />} label="Source / Destination" color="#3b82f6" />
          <div style={{ padding: '10px 14px', fontSize: '12px' }}>
            {/* Sources */}
            {(rule.sources?.length ?? 0) > 0 && (
              <div style={{ marginBottom: '8px' }}>
                <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Sources</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                  {rule.sources!.map((s, i) => (
                    <span key={i} style={{
                      padding: '2px 8px', borderRadius: '6px', fontSize: '11px',
                      background: 'rgba(59,130,246,0.1)', color: '#60a5fa',
                      border: '1px solid rgba(59,130,246,0.2)'
                    }}>{s.resource_name}</span>
                  ))}
                </div>
              </div>
            )}
            {/* Destinations */}
            {(rule.destinations?.length ?? 0) > 0 && (
              <div>
                <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Channels</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                  {rule.destinations!.map((d, i) => (
                    <div key={i} style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                      <ChannelBadge channelType={d.channel_type} enabled={d.channel_enabled} />
                      {d.resources && d.resources.length > 0 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px', paddingLeft: '8px', borderLeft: '2px solid rgba(59,130,246,0.2)' }}>
                          {d.resources.map((r, ri) => (
                            <span key={ri} style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>
                              ↳ {r.resource_name} ({r.include === 'true' ? 'Inc' : 'Exc'})
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
            {!(rule.sources?.length) && !(rule.destinations?.length) && (
              <div style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>Kayıt yok</div>
            )}
          </div>
        </div>
      </div>

      {/* Exceptions */}
      <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
        <SectionHeader icon={<FileWarning size={14} />} label="Exceptions" color="#10b981" count={rule.exceptions?.length} />
        {rule.exceptions?.length ? (
          <div style={{ display: 'flex', flexDirection: 'column' }}>
            {rule.exceptions.map(exc => {
              const isExpanded = !!expandedExceptions[exc.id]
              return (
                <div key={exc.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                  {/* Accordion Header */}
                  <div 
                    onClick={() => toggleException(exc.id)}
                    style={{ 
                      display: 'flex', alignItems: 'center', justifyContent: 'space-between', 
                      padding: '12px 16px', cursor: 'pointer', background: isExpanded ? 'rgba(16,185,129,0.05)' : 'transparent',
                      transition: 'background 0.2s'
                    }}
                    onMouseEnter={e => { if (!isExpanded) e.currentTarget.style.background = 'rgba(16,185,129,0.02)' }}
                    onMouseLeave={e => { if (!isExpanded) e.currentTarget.style.background = 'transparent' }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                      <div style={{ 
                        width: '24px', height: '24px', borderRadius: '6px', background: 'rgba(16,185,129,0.1)', 
                        display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#10b981'
                      }}>
                        {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                      </div>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text-primary)' }}>{exc.exception_rule_name}</div>
                        {exc.description && <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '2px' }}>{exc.description}</div>}
                      </div>
                    </div>
                    
                    <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                      <div style={{ fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        {exc.enabled === 'true' 
                          ? <span style={{ color: '#10b981', display: 'flex', alignItems: 'center', gap: '4px' }}><CheckCircle size={13} /> Aktif</span>
                          : <span style={{ color: '#ef4444', display: 'flex', alignItems: 'center', gap: '4px' }}><XCircle size={13} /> Pasif</span>}
                      </div>
                      
                      <div style={{ display: 'flex', gap: '4px' }} onClick={e => e.stopPropagation()}>
                        <button className="pi-icon-btn" onClick={() => onEditException(exc)} title="Düzenle"><Edit2 size={13} /></button>
                        <button className="pi-icon-btn pi-delete" onClick={() => onDeleteException(exc.id, exc.exception_rule_name)} title="Sil"><Trash2 size={13} /></button>
                      </div>
                    </div>
                  </div>

                  {/* Accordion Content */}
                  {isExpanded && (
                    <div style={{ padding: '16px 20px 20px 48px', borderTop: '1px solid var(--border-color)', background: 'var(--bg-color)', display: 'flex', flexDirection: 'column', gap: '12px' }}>
                      
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                        {/* Severity Actions (Exception) */}
                        <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', background: 'var(--card-bg)' }}>
                          <div style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase' }}>
                            Severity Actions
                          </div>
                          <div style={{ padding: '8px 12px' }}>
                            {exc.severity_actions?.length ? exc.severity_actions.map((sa, i) => (
                              <div key={i} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '4px 0', fontSize: '12px' }}>
                                <SeverityBadge severity={sa.severity_type} selected={sa.selected} />
                                <span style={{ color: 'var(--text-secondary)', fontSize: '11px' }}>{sa.action_plan || '-'}</span>
                              </div>
                            )) : <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Kayıt yok</div>}
                          </div>
                        </div>

                        {/* Sources (Exception) */}
                        <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', background: 'var(--card-bg)' }}>
                          <div style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', display: 'flex', justifyContent: 'space-between' }}>
                            <span>Sources</span>
                            {exc.source_enabled === 'true' ? <span style={{ color: '#60a5fa' }}><CheckCircle size={12} /></span> : <span style={{ color: 'var(--border-color)' }}><XCircle size={12} /></span>}
                          </div>
                          <div style={{ padding: '8px 12px', display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                            {exc.sources?.length ? exc.sources.map((s, i) => (
                              <span key={i} style={{ padding: '2px 8px', borderRadius: '6px', fontSize: '11px', background: 'rgba(59,130,246,0.1)', color: '#60a5fa', border: '1px solid rgba(59,130,246,0.2)' }}>
                                {s.resource_name} {s.include === 'false' && '(Exc)'}
                              </span>
                            )) : <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Kayıt yok</div>}
                          </div>
                        </div>
                      </div>

                      {/* Destinations Accordion (Exception) */}
                      <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', background: 'var(--card-bg)' }}>
                        <div style={{ padding: '8px 12px', borderBottom: '1px solid var(--border-color)', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', display: 'flex', justifyContent: 'space-between' }}>
                          <span>Destinations (Channels)</span>
                          {exc.destination_enabled === 'true' ? <span style={{ color: '#60a5fa' }}><CheckCircle size={12} /></span> : <span style={{ color: 'var(--border-color)' }}><XCircle size={12} /></span>}
                        </div>
                        <div>
                          {exc.destinations?.length ? exc.destinations.map((d, i) => {
                            const destKey = `${exc.id}-${d.channel_type}-${i}`
                            const isDestExpanded = !!expandedDestinations[destKey]
                            return (
                              <div key={i} style={{ borderBottom: i < exc.destinations!.length - 1 ? '1px solid var(--border-color)' : 'none' }}>
                                <div 
                                  onClick={() => toggleDestination(destKey)}
                                  style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 12px', cursor: 'pointer', background: isDestExpanded ? 'rgba(59,130,246,0.03)' : 'transparent' }}
                                >
                                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                    {isDestExpanded ? <ChevronDown size={14} color="var(--text-secondary)" /> : <ChevronRight size={14} color="var(--text-secondary)" />}
                                    <ChannelBadge channelType={d.channel_type} enabled={d.channel_enabled} />
                                  </div>
                                  <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
                                    {d.resources?.length || 0} kaynak
                                  </div>
                                </div>
                                
                                {isDestExpanded && (
                                  <div style={{ padding: '8px 12px 12px 34px', background: 'rgba(0,0,0,0.01)' }}>
                                    {d.resources?.length ? (
                                      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                        {d.resources.map((r, ri) => (
                                          <div key={ri} style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '11px', color: 'var(--text-secondary)' }}>
                                            <div style={{ width: '4px', height: '4px', borderRadius: '50%', background: r.include === 'true' ? '#10b981' : '#ef4444' }} />
                                            <span style={{ color: 'var(--text-primary)' }}>{r.resource_name}</span>
                                            <span style={{ fontSize: '10px', padding: '1px 4px', borderRadius: '4px', background: r.include === 'true' ? 'rgba(16,185,129,0.1)' : 'rgba(239,68,68,0.1)', color: r.include === 'true' ? '#10b981' : '#ef4444' }}>
                                              {r.include === 'true' ? 'Include' : 'Exclude'}
                                            </span>
                                          </div>
                                        ))}
                                      </div>
                                    ) : <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>Alt kaynak yok</div>}
                                  </div>
                                )}
                              </div>
                            )
                          }) : <div style={{ padding: '8px 12px', fontSize: '12px', color: 'var(--text-secondary)' }}>Kayıt yok</div>}
                        </div>
                      </div>

                    </div>
                  )}
                </div>
              )
            })}
          </div>
        ) : (
          <div style={{ padding: '16px', textAlign: 'center', color: 'var(--text-secondary)', fontSize: '13px' }}>
            Exception bulunamadı.
          </div>
        )}
      </div>

    </div>
  )
}

export default function PolicyInventoryTable({
  policies, onAddPolicy, onEditPolicy, onDeletePolicy,
  onAddRule, onEditRule, onDeleteRule,
  onAddException, onEditException, onDeleteException
}: Props) {
  const [expandedPolicies, setExpandedPolicies] = useState<Record<number, boolean>>({})
  const [expandedRules, setExpandedRules] = useState<Record<number, boolean>>({})

  const togglePolicy = (id: number) => setExpandedPolicies(p => ({ ...p, [id]: !p[id] }))
  const toggleRule   = (id: number) => setExpandedRules(p => ({ ...p, [id]: !p[id] }))

  if (!policies.length) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '80px 20px', gap: '16px' }}>
        <div style={{
          width: '64px', height: '64px', borderRadius: '16px',
          background: 'linear-gradient(135deg, rgba(16,185,129,0.15), rgba(59,130,246,0.15))',
          display: 'flex', alignItems: 'center', justifyContent: 'center'
        }}>
          <AlertTriangle size={28} color="#10b981" />
        </div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text-primary)', marginBottom: '6px' }}>Politika bulunamadı</div>
          <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>JSON veya Excel dosyası yükleyerek başlayın</div>
        </div>
        <button onClick={onAddPolicy} style={{
          display: 'flex', alignItems: 'center', gap: '8px',
          padding: '10px 20px', borderRadius: '10px', border: 'none', cursor: 'pointer',
          background: 'linear-gradient(135deg, #10b981, #059669)', color: '#fff',
          fontSize: '13px', fontWeight: 600
        }}>
          <Plus size={16} /> Yeni Politika Ekle
        </button>
      </div>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      {policies.map((policy, pi) => {
        const isPolicyOpen = !!expandedPolicies[policy.id]
        const totalExc = policy.rules?.reduce((a, r) => a + (r.exceptions?.length || 0), 0) ?? 0

        return (
          <div key={policy.id} style={{
            borderBottom: pi < policies.length - 1 ? '1px solid var(--border-color)' : 'none'
          }}>
            {/* Policy Header */}
            <div
              onClick={() => togglePolicy(policy.id)}
              style={{
                display: 'flex', alignItems: 'center', gap: '12px',
                padding: '14px 20px', cursor: 'pointer',
                background: isPolicyOpen
                  ? 'linear-gradient(135deg, rgba(16,185,129,0.07), rgba(59,130,246,0.04))'
                  : 'transparent',
                transition: 'background 0.2s',
              }}
              onMouseEnter={e => { if (!isPolicyOpen) (e.currentTarget as HTMLDivElement).style.background = 'rgba(16,185,129,0.04)' }}
              onMouseLeave={e => { if (!isPolicyOpen) (e.currentTarget as HTMLDivElement).style.background = 'transparent' }}
            >
              {/* Chevron */}
              <div style={{ color: isPolicyOpen ? '#10b981' : 'var(--text-secondary)', transition: 'transform 0.2s, color 0.2s', transform: isPolicyOpen ? 'rotate(0deg)' : 'rotate(-90deg)' }}>
                <ChevronDown size={18} />
              </div>

              {/* Policy Icon */}
              <div style={{
                width: '34px', height: '34px', borderRadius: '9px', flexShrink: 0,
                background: 'linear-gradient(135deg, #10b981, #059669)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                boxShadow: '0 3px 8px rgba(16,185,129,0.25)'
              }}>
                <ShieldAlert size={16} color="#fff" />
              </div>

              {/* Policy Name */}
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {policy.policy_name}
                </div>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>
                  {policy.rules?.length || 0} Kural · {totalExc} Exception
                </div>
              </div>

              {/* Stats Chips */}
              <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                <span style={{ padding: '3px 10px', borderRadius: '999px', fontSize: '11px', fontWeight: 600, background: 'rgba(16,185,129,0.12)', color: '#10b981', border: '1px solid rgba(16,185,129,0.2)' }}>
                  {policy.rules?.length || 0} Kural
                </span>
                <span style={{ padding: '3px 10px', borderRadius: '999px', fontSize: '11px', fontWeight: 600, background: 'rgba(245,158,11,0.12)', color: '#f59e0b', border: '1px solid rgba(245,158,11,0.2)' }}>
                  {totalExc} Exc
                </span>
              </div>

              {/* Actions */}
              <div style={{ display: 'flex', gap: '4px' }} onClick={e => e.stopPropagation()}>
                <button className="pi-icon-btn" onClick={() => onAddRule(policy.id)} title="Kural Ekle"><Plus size={15} /></button>
                <button className="pi-icon-btn" onClick={() => onEditPolicy(policy)} title="Düzenle"><Edit2 size={15} /></button>
                <button className="pi-icon-btn pi-delete" onClick={() => onDeletePolicy(policy.id, policy.policy_name)} title="Sil"><Trash2 size={15} /></button>
              </div>
            </div>

            {/* Rules */}
            {isPolicyOpen && (
              <div style={{ borderTop: '1px solid var(--border-color)' }}>
                {policy.rules?.map((rule, ri) => {
                  const isRuleOpen = !!expandedRules[rule.id]
                  const maxSev = rule.severity_actions?.find(s => s.selected === 'true')?.severity_type
                  const activeChan = rule.destinations?.filter(d => d.channel_enabled === 'true').length || 0

                  return (
                    <div key={rule.id} style={{
                      borderBottom: ri < (policy.rules?.length ?? 1) - 1 ? '1px solid var(--border-color)' : 'none'
                    }}>
                      {/* Rule Header */}
                      <div
                        onClick={() => toggleRule(rule.id)}
                        style={{
                          display: 'flex', alignItems: 'center', gap: '10px',
                          padding: '10px 20px 10px 44px', cursor: 'pointer',
                          background: isRuleOpen ? 'rgba(59,130,246,0.05)' : 'rgba(0,0,0,0.01)',
                          transition: 'background 0.15s'
                        }}
                        onMouseEnter={e => { if (!isRuleOpen) (e.currentTarget as HTMLDivElement).style.background = 'rgba(59,130,246,0.04)' }}
                        onMouseLeave={e => { if (!isRuleOpen) (e.currentTarget as HTMLDivElement).style.background = 'rgba(0,0,0,0.01)' }}
                      >
                        <div style={{ color: isRuleOpen ? '#3b82f6' : 'var(--text-secondary)', transition: 'transform 0.15s, color 0.15s', transform: isRuleOpen ? 'rotate(0deg)' : 'rotate(-90deg)' }}>
                          <ChevronDown size={15} />
                        </div>

                        <div style={{
                          width: '6px', height: '6px', borderRadius: '50%', flexShrink: 0,
                          background: isRuleOpen ? '#3b82f6' : 'var(--text-secondary)'
                        }} />

                        <span style={{ flex: 1, fontWeight: 600, fontSize: '13px', color: 'var(--text-primary)' }}>
                          {rule.rule_name}
                        </span>

                        {/* Rule Mini-Stats */}
                        <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                          {maxSev && <SeverityBadge severity={maxSev} />}
                          {activeChan > 0 && (
                            <span style={{ padding: '2px 8px', borderRadius: '999px', fontSize: '11px', fontWeight: 600, background: 'rgba(59,130,246,0.1)', color: '#60a5fa', border: '1px solid rgba(59,130,246,0.2)' }}>
                              {activeChan} kanal aktif
                            </span>
                          )}
                          <span style={{ padding: '2px 8px', borderRadius: '999px', fontSize: '11px', color: 'var(--text-secondary)', background: 'rgba(0,0,0,0.04)' }}>
                            {rule.exceptions?.length || 0} exc
                          </span>
                        </div>

                        {/* Rule Actions */}
                        <div style={{ display: 'flex', gap: '4px' }} onClick={e => e.stopPropagation()}>
                          <button className="pi-icon-btn" onClick={() => onAddException(rule.id)} title="Exception Ekle"><Plus size={13} /></button>
                          <button className="pi-icon-btn" onClick={() => onEditRule(rule, policy.id)} title="Kuralı Düzenle"><Edit2 size={13} /></button>
                          <button className="pi-icon-btn pi-delete" onClick={() => onDeleteRule(rule.id, rule.rule_name)} title="Kuralı Sil"><Trash2 size={13} /></button>
                        </div>
                      </div>

                      {expandedRules[rule.id] && (
                      <RuleDetailPanel 
                        rule={rule} 
                        onEditException={(e) => onEditException(e, rule.id)}
                        onDeleteException={onDeleteException}
                      />
                    )}</div>
                  )
                })}
                {!policy.rules?.length && (
                  <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)', fontSize: '13px' }}>
                    Kural bulunamadı. <button onClick={() => onAddRule(policy.id)} style={{ background: 'none', border: 'none', color: '#10b981', cursor: 'pointer', fontWeight: 600 }}>Kural Ekle →</button>
                  </div>
                )}
              </div>
            )}
          </div>
        )
      })}

      <style>{`
        .pi-icon-btn {
          background: none; border: none; color: var(--text-secondary); cursor: pointer;
          padding: 5px; border-radius: 6px; transition: all 0.15s;
          display: flex; align-items: center; justify-content: center;
        }
        .pi-icon-btn:hover { background: var(--border-color); color: var(--text-primary); }
        .pi-icon-btn.pi-delete:hover { background: rgba(239,68,68,0.12); color: #ef4444; }
      `}</style>
    </div>
  )
}
