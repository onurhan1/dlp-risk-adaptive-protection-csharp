'use client'
import React, { useState } from 'react'
import {
  ChevronDown, ChevronRight, Plus, Edit2, Trash2,
  ShieldAlert, Globe, FileWarning, Cpu, Mail,
  Network, HardDrive, Monitor, Wifi, Server,
  AlertTriangle, CheckCircle, XCircle, Info, ArrowRight
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

// Small pill used for include/exclude, active/inactive style flags
function FlagPill({ label, active }: { label: string; active: boolean }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: '3px',
      padding: '1px 6px', borderRadius: '999px', fontSize: '10px', fontWeight: 600,
      background: active ? 'rgba(16,185,129,0.1)' : 'rgba(239,68,68,0.08)',
      color: active ? '#10b981' : '#ef4444',
      border: `1px solid ${active ? 'rgba(16,185,129,0.25)' : 'rgba(239,68,68,0.2)'}`
    }}>
      {active ? <CheckCircle size={10} /> : <XCircle size={10} />}
      {label}
    </span>
  )
}

// Compact key:value chip used for meta fields like parts_count_type / condition_relation_type
function MetaChip({ label, value }: { label: string; value?: string | null }) {
  if (!value) return null
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: '4px',
      padding: '2px 8px', borderRadius: '6px', fontSize: '11px',
      background: 'rgba(0,0,0,0.04)', color: 'var(--text-secondary)'
    }}>
      <span style={{ fontWeight: 600, textTransform: 'uppercase', fontSize: '9px', letterSpacing: '0.03em' }}>{label}</span>
      {value}
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

// Resource chip: resource_name + resource_type + include/exclude — used by both
// rule-level and exception-level source/destination resource lists.
function ResourceChip({ name, type, include }: { name?: string; type?: string; include?: string }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap',
      padding: '3px 9px', borderRadius: '6px', fontSize: '11px',
      background: 'rgba(59,130,246,0.07)', border: '1px solid rgba(59,130,246,0.18)'
    }}>
      <span style={{ color: '#60a5fa', fontWeight: 500 }}>{name || '-'}</span>
      {type && (
        <span style={{ color: 'var(--text-secondary)', fontSize: '10px', background: 'rgba(0,0,0,0.05)', padding: '1px 6px', borderRadius: '999px' }}>
          {type}
        </span>
      )}
      {include !== undefined && include !== null && include !== '' && (
        <FlagPill label={include === 'true' ? 'Include' : 'Exclude'} active={include === 'true'} />
      )}
    </div>
  )
}

// Classifier chip — shared by rule-level and exception-level classifier lists.
function ClassifierChip({ c, position }: { c: any; position?: number }) {
  return (
    <div style={{
      background: 'rgba(139,92,246,0.08)', border: '1px solid rgba(139,92,246,0.2)',
      borderRadius: '8px', padding: '6px 12px', fontSize: '12px'
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
        {position !== undefined && position !== null && (
          <span style={{ fontSize: '10px', color: '#a78bfa', fontWeight: 700 }}>#{position}</span>
        )}
        <span style={{ fontWeight: 600, color: '#a78bfa' }}>{c.classifier_name}</span>
      </div>
      <div style={{ color: 'var(--text-secondary)', fontSize: '11px', marginTop: '2px' }}>
        {c.threshold_type} {c.threshold_value_from !== undefined && c.threshold_value_from !== null ? `≥ ${c.threshold_value_from}` : ''} {c.threshold_calculate_type}
      </div>
      {c.analyzed_specific_fields && (
        <div style={{ color: 'var(--text-secondary)', fontSize: '10px', marginTop: '2px', opacity: 0.8 }}>
          Alanlar: {c.analyzed_specific_fields}
        </div>
      )}
    </div>
  )
}

// Severity action row — shared by rule-level and exception-level lists.
// Rule-level entries carry `type` / `max_matches`, exception-level ones don't — both optional.
function SeverityActionRow({ sa, isLast }: { sa: any; isLast: boolean }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '6px',
      padding: '6px 0', borderBottom: isLast ? 'none' : '1px dashed var(--border-color)',
      fontSize: '12px'
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap' }}>
        <SeverityBadge severity={sa.severity_type} selected={sa.selected} />
        {sa.dup_severity_type && sa.dup_severity_type !== sa.severity_type && (
          <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>(dup: {sa.dup_severity_type})</span>
        )}
        {sa.number_of_matches !== undefined && sa.number_of_matches !== null && (
          <span style={{ color: 'var(--text-secondary)', fontSize: '11px' }}>#{sa.number_of_matches} eşleşme</span>
        )}
        {sa.type && <MetaChip label="Type" value={sa.type} />}
        {sa.max_matches && <MetaChip label="Max" value={sa.max_matches} />}
      </div>
      <span style={{ color: 'var(--text-secondary)', fontSize: '11px' }}>{sa.action_plan || '-'}</span>
    </div>
  )
}

// Source & Destination block — shared rendering for rule-level and exception-level data.
function SourceDestinationBlock({ sources, destinations }: { sources?: any[]; destinations?: any[] }) {
  const hasSources = (sources?.length ?? 0) > 0
  const hasDestinations = (destinations?.length ?? 0) > 0
  const emailDirs = destinations?.find(d => d.email_monitor_directions)?.email_monitor_directions

  if (!hasSources && !hasDestinations) {
    return <div style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>Kayıt yok</div>
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
      {hasSources && (
        <div>
          <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
            Sources ({sources!.length})
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '5px' }}>
            {sources!.map((s, i) => (
              <ResourceChip key={i} name={s.resource_name} type={s.resource_type} include={s.include} />
            ))}
          </div>
        </div>
      )}
      {hasDestinations && (
        <div>
          <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px', textTransform: 'uppercase', letterSpacing: '0.05em', display: 'flex', alignItems: 'center', gap: '6px' }}>
            Destinations ({destinations!.length})
            {emailDirs && emailDirs.length > 0 && (
              <span style={{ textTransform: 'none', fontWeight: 400, fontSize: '10px', color: 'var(--text-secondary)', opacity: 0.8 }}>
                · Mail yönü: {Array.isArray(emailDirs) ? emailDirs.join(', ') : emailDirs}
              </span>
            )}
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px' }}>
            {destinations!.map((d, i) => (
              <div key={i} style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <ChannelBadge channelType={d.channel_type} enabled={d.channel_enabled} />
                {d.channel_resources && d.channel_resources.length > 0 && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '3px', paddingLeft: '8px', borderLeft: '2px solid rgba(59,130,246,0.2)' }}>
                    {d.channel_resources.map((r: any, ri: number) => (
                      <div key={ri} style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <ArrowRight size={9} style={{ color: 'var(--text-secondary)', opacity: 0.6, flexShrink: 0 }} />
                        <ResourceChip name={r.resource_name} type={r.resource_type} include={r.include} />
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

function SourcePanel({ sources }: { sources?: any[] }) {
  if (!sources?.length) {
    return <div style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>Kayit yok</div>
  }

  const groupedSources = sources.reduce<Record<string, any[]>>((acc, source) => {
    const key = source.resource_type || 'UNKNOWN'
    acc[key] = acc[key] || []
    acc[key].push(source)
    return acc
  }, {})

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
      {Object.entries(groupedSources).map(([type, items]) => (
        <div key={type} style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ fontSize: '11px', fontWeight: 700, color: '#60a5fa', textTransform: 'uppercase' }}>{type}</span>
            <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>{items.length} kaynak</span>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '5px' }}>
            {items.map((s, i) => (
              <ResourceChip key={`${type}-${i}`} name={s.resource_name} type={s.resource_type} include={s.include} />
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}

function DestinationPanel({ destinations }: { destinations?: any[] }) {
  const [selectedType, setSelectedType] = useState('ALL')
  const [resourceQuery, setResourceQuery] = useState('')
  const [includeFilter, setIncludeFilter] = useState('all')

  if (!destinations?.length) {
    return <div style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>Kayit yok</div>
  }

  const emailDirs = destinations.find(d => d.email_monitor_directions)?.email_monitor_directions
  const channelTypes = Array.from(new Set(destinations.map(d => d.channel_type || 'UNKNOWN')))
  const visibleDestinations = selectedType === 'ALL'
    ? destinations
    : destinations.filter(d => (d.channel_type || 'UNKNOWN') === selectedType)

  const resourceRows = visibleDestinations.flatMap((destination) => {
    const resources = destination.channel_resources?.length
      ? destination.channel_resources
      : [{ resource_name: '', resource_type: '', include: '' }]

    return resources.map((resource: any, index: number) => ({
      key: `${destination.id || destination.channel_type}-${index}-${resource.id || resource.resource_name || 'empty'}`,
      channelType: destination.channel_type || 'UNKNOWN',
      channelEnabled: destination.channel_enabled,
      resourceName: resource.resource_name || '',
      resourceType: resource.resource_type || '',
      include: resource.include || '',
    }))
  }).filter((row) => {
    const q = resourceQuery.trim().toLowerCase()
    const matchesQuery = !q ||
      row.resourceName.toLowerCase().includes(q) ||
      row.resourceType.toLowerCase().includes(q) ||
      row.channelType.toLowerCase().includes(q)
    const matchesInclude = includeFilter === 'all' || row.include === includeFilter
    return matchesQuery && matchesInclude
  })

  const totalResources = destinations.reduce((sum, d) => sum + (d.channel_resources?.length || 0), 0)
  const activeChannels = destinations.filter(d => d.channel_enabled === 'true').length

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', alignItems: 'center' }}>
        <MetaChip label="Channel" value={`${destinations.length}`} />
        <MetaChip label="Resource" value={`${totalResources}`} />
        <MetaChip label="Active" value={`${activeChannels}`} />
        {emailDirs && emailDirs.length > 0 && (
          <MetaChip label="Mail" value={Array.isArray(emailDirs) ? emailDirs.join(', ') : emailDirs} />
        )}
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
        {['ALL', ...channelTypes].map((type) => (
          <button
            key={type}
            type="button"
            onClick={() => setSelectedType(type)}
            style={{
              border: `1px solid ${selectedType === type ? 'rgba(139,92,246,0.45)' : 'var(--border-color)'}`,
              background: selectedType === type ? 'rgba(139,92,246,0.14)' : 'var(--bg-color)',
              color: selectedType === type ? '#a78bfa' : 'var(--text-secondary)',
              borderRadius: '999px',
              padding: '4px 9px',
              fontSize: '11px',
              fontWeight: 700,
              cursor: 'pointer'
            }}
          >
            {type === 'ALL' ? 'All' : type}
          </button>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(180px, 1fr) 120px', gap: '8px' }}>
        <input
          value={resourceQuery}
          onChange={(e) => setResourceQuery(e.target.value)}
          placeholder="Resource ara..."
          style={{
            minWidth: 0,
            padding: '7px 9px',
            borderRadius: '7px',
            border: '1px solid var(--border-color)',
            background: 'var(--bg-color)',
            color: 'var(--text-primary)',
            fontSize: '12px',
            outline: 'none'
          }}
        />
        <select
          value={includeFilter}
          onChange={(e) => setIncludeFilter(e.target.value)}
          style={{
            padding: '7px 8px',
            borderRadius: '7px',
            border: '1px solid var(--border-color)',
            background: 'var(--bg-color)',
            color: 'var(--text-primary)',
            fontSize: '12px',
            outline: 'none'
          }}
        >
          <option value="all">All</option>
          <option value="true">Include</option>
          <option value="false">Exclude</option>
        </select>
      </div>

      <div style={{
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
        maxHeight: '260px',
        overflowY: 'auto',
        paddingRight: '4px'
      }}>
        {resourceRows.length ? resourceRows.map((row) => (
          <div key={row.key} style={{
            display: 'grid',
            gridTemplateColumns: 'minmax(110px, 0.8fr) minmax(160px, 1.3fr)',
            gap: '8px',
            alignItems: 'center',
            padding: '7px 9px',
            borderRadius: '8px',
            border: '1px solid rgba(139,92,246,0.16)',
            background: 'rgba(139,92,246,0.06)'
          }}>
            <ChannelBadge channelType={row.channelType} enabled={row.channelEnabled} />
            {row.resourceName || row.resourceType || row.include ? (
              <ResourceChip name={row.resourceName || '-'} type={row.resourceType} include={row.include} />
            ) : (
              <span style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>Resource yok</span>
            )}
          </div>
        )) : (
          <div style={{ color: 'var(--text-secondary)', fontSize: '12px', padding: '8px 0' }}>Sonuc yok</div>
        )}
      </div>

      {visibleDestinations.some(d => !d.channel_resources?.length) && (
        <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
          Resource olmayan channel kayitlari da listelenir.
        </div>
      )}
    </div>
  )
}

function RuleDetailPanel({
  rule, onAddException, onEditException, onDeleteException
}: {
  rule: PolicyRule
  onAddException: (ruleId: number) => void
  onEditException: (exc: PolicyException, ruleId: number) => void
  onDeleteException: (id: number, name: string) => void
}) {
  const [expandedExceptions, setExpandedExceptions] = useState<Record<number, boolean>>({})
  const toggleException = (id: number) => setExpandedExceptions(p => ({ ...p, [id]: !p[id] }))

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', padding: '16px 20px 20px 48px' }}>

      {/* Rule meta info */}
      {(rule.parts_count_type || rule.condition_relation_type) && (
        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
          <MetaChip label="Parts Count" value={rule.parts_count_type} />
          <MetaChip label="Condition İlişkisi" value={rule.condition_relation_type} />
        </div>
      )}

      {/* Classifiers */}
      {(rule.classifiers?.length ?? 0) > 0 && (
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<Cpu size={14} />} label="Classifiers" color="#8b5cf6" count={rule.classifiers!.length} />
          <div style={{ padding: '10px 14px', display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
            {rule.classifiers!.map((c, i) => <ClassifierChip key={i} c={c} />)}
          </div>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '12px' }}>
        {/* Severity Actions */}
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<ShieldAlert size={14} />} label="Severity Actions" color="#f59e0b" count={rule.severity_actions?.length} />
          <div style={{ padding: '10px 14px' }}>
            {rule.severity_actions?.length
              ? rule.severity_actions.map((sa, i) => (
                  <SeverityActionRow key={i} sa={sa} isLast={i === rule.severity_actions!.length - 1} />
                ))
              : <div style={{ fontSize: '12px', color: 'var(--text-secondary)', padding: '4px 0' }}>Kayıt yok</div>}
          </div>
        </div>

        {/* Source */}
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<Globe size={14} />} label="Source" color="#3b82f6" count={rule.sources?.length} />
          <div style={{ padding: '10px 14px' }}>
            <SourcePanel sources={rule.sources} />
          </div>
        </div>

        {/* Destination */}
        <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
          <SectionHeader icon={<Network size={14} />} label="Destination" color="#8b5cf6" count={rule.destinations?.length} />
          <div style={{ padding: '10px 14px' }}>
            <DestinationPanel destinations={rule.destinations} />
          </div>
        </div>
      </div>

      {/* Exceptions */}
      <div style={{ borderRadius: '10px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
        <SectionHeader icon={<FileWarning size={14} />} label="Exceptions" color="#10b981" count={rule.exceptions?.length} />
        {rule.exceptions?.length ? (
          <div>
            {rule.exceptions.map((exc, ei) => {
              const isOpen = !!expandedExceptions[exc.id]
              const srcCount = exc.sources?.length ?? 0
              const dstCount = exc.destinations?.length ?? 0
              const primarySeverity = exc.severity_actions?.find(s => s.selected === 'true') ?? exc.severity_actions?.[0]

              return (
                <div key={exc.id} style={{ borderBottom: ei < rule.exceptions!.length - 1 ? '1px solid var(--border-color)' : 'none' }}>
                  {/* Exception summary row */}
                  <div
                    onClick={() => toggleException(exc.id)}
                    style={{
                      display: 'flex', alignItems: 'center', gap: '10px',
                      padding: '9px 14px', cursor: 'pointer',
                      background: isOpen ? 'rgba(16,185,129,0.05)' : 'transparent',
                      transition: 'background 0.15s'
                    }}
                    onMouseEnter={e => { if (!isOpen) (e.currentTarget as HTMLDivElement).style.background = 'rgba(16,185,129,0.04)' }}
                    onMouseLeave={e => { if (!isOpen) (e.currentTarget as HTMLDivElement).style.background = 'transparent' }}
                  >
                    <div style={{ color: isOpen ? '#10b981' : 'var(--text-secondary)', transition: 'transform 0.15s, color 0.15s', transform: isOpen ? 'rotate(0deg)' : 'rotate(-90deg)', flexShrink: 0 }}>
                      <ChevronDown size={14} />
                    </div>

                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontWeight: 600, fontSize: '12px', color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {exc.exception_rule_name}
                      </div>
                      {exc.description && (
                        <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '1px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {exc.description}
                        </div>
                      )}
                    </div>

                    {/* Quick badges */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexShrink: 0 }}>
                      {exc.enabled === 'true'
                        ? <span title="Aktif" style={{ color: '#10b981', display: 'flex' }}><CheckCircle size={13} /></span>
                        : <span title="Pasif" style={{ color: '#ef4444', display: 'flex' }}><XCircle size={13} /></span>}
                      {srcCount > 0 && (
                        <span style={{ padding: '2px 7px', borderRadius: '999px', fontSize: '10px', fontWeight: 600, background: 'rgba(59,130,246,0.1)', color: '#60a5fa', border: '1px solid rgba(59,130,246,0.2)' }}>
                          {srcCount} kaynak
                        </span>
                      )}
                      {dstCount > 0 && (
                        <span style={{ padding: '2px 7px', borderRadius: '999px', fontSize: '10px', fontWeight: 600, background: 'rgba(139,92,246,0.1)', color: '#a78bfa', border: '1px solid rgba(139,92,246,0.2)' }}>
                          {dstCount} hedef
                        </span>
                      )}
                      {primarySeverity && <SeverityBadge severity={primarySeverity.severity_type} selected={primarySeverity.selected} />}
                    </div>

                    {/* Actions */}
                    <div style={{ display: 'flex', gap: '4px', flexShrink: 0 }} onClick={e => e.stopPropagation()}>
                      <button className="pi-icon-btn" onClick={() => onEditException(exc, rule.id)} title="Düzenle"><Edit2 size={13} /></button>
                      <button className="pi-icon-btn pi-delete" onClick={() => onDeleteException(exc.id, exc.exception_rule_name)} title="Sil"><Trash2 size={13} /></button>
                    </div>
                  </div>

                  {/* Exception detail panel */}
                  {isOpen && (
                    <div style={{ padding: '4px 14px 16px 34px', display: 'flex', flexDirection: 'column', gap: '10px', background: 'rgba(16,185,129,0.02)' }}>
                      {/* Flags */}
                      <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                        <FlagPill label="Condition" active={exc.condition_enabled === 'true'} />
                        <FlagPill label="Source" active={exc.source_enabled === 'true'} />
                        <FlagPill label="Destination" active={exc.destination_enabled === 'true'} />
                        <MetaChip label="Parts Count" value={exc.parts_count_type} />
                        <MetaChip label="Condition İlişkisi" value={exc.condition_relation_type} />
                      </div>

                      {/* Classifiers */}
                      {(exc.classifiers?.length ?? 0) > 0 && (
                        <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
                          <SectionHeader icon={<Cpu size={12} />} label="Classifiers" color="#8b5cf6" count={exc.classifiers!.length} />
                          <div style={{ padding: '8px 12px', display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
                            {exc.classifiers!.map((c: any, i: number) => (
                              <ClassifierChip key={i} c={c} position={c.position} />
                            ))}
                          </div>
                        </div>
                      )}

                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '10px' }}>
                        {/* Severity Actions — full list, not just the first */}
                        <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
                          <SectionHeader icon={<ShieldAlert size={12} />} label="Severity Actions" color="#f59e0b" count={exc.severity_actions?.length} />
                          <div style={{ padding: '8px 12px' }}>
                            {exc.severity_actions?.length
                              ? exc.severity_actions.map((sa: any, i: number) => (
                                  <SeverityActionRow key={i} sa={sa} isLast={i === exc.severity_actions!.length - 1} />
                                ))
                              : <div style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>Kayıt yok</div>}
                          </div>
                        </div>

                        {/* Source & Destination — this is the part that was previously missing */}
                        <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
                          <SectionHeader icon={<Globe size={12} />} label="Source" color="#3b82f6" count={exc.sources?.length} />
                          <div style={{ padding: '8px 12px' }}>
                            <SourcePanel sources={exc.sources} />
                          </div>
                        </div>

                        <div style={{ borderRadius: '8px', border: '1px solid var(--border-color)', overflow: 'hidden' }}>
                          <SectionHeader icon={<Network size={12} />} label="Destination" color="#8b5cf6" count={exc.destinations?.length} />
                          <div style={{ padding: '8px 12px' }}>
                            <DestinationPanel destinations={exc.destinations} />
                          </div>
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
            Exception bulunamadı.{' '}
            <button onClick={() => onAddException(rule.id)} style={{ background: 'none', border: 'none', color: '#10b981', cursor: 'pointer', fontWeight: 600 }}>
              Exception Ekle →
            </button>
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

                      {/* Rule Detail Panel */}
                      {isRuleOpen && (
                        <RuleDetailPanel
                          rule={rule}
                          onAddException={onAddException}
                          onEditException={onEditException}
                          onDeleteException={onDeleteException}
                        />
                      )}
                    </div>
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
