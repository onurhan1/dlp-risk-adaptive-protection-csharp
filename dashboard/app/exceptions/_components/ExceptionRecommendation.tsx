'use client'

import React, { useState, useMemo, memo, useCallback } from 'react'
import { Plus, Minus, ClipboardList, Search, Sparkles, SlidersHorizontal } from 'lucide-react'
import { parseISO, startOfDay, endOfDay, isWithinInterval } from 'date-fns'
import SearchableMultiSelect from './SearchableMultiSelect'
import useUserReportData from '../_hooks/useUserReportData'
import type { Incident } from '../_lib/types'
import { normalizeTeamName, parseViolationTriggers, extractPoliciesFromIncidents, toggleSetItem } from '../_lib/utils'
import { DEFAULT_START, DEFAULT_END, STYLES } from '../_lib/constants'

interface ExceptionRecommendationProps {
  incidents: Incident[]
  uniqueDepartments: string[]
  uniqueTeams: string[]
  uniqueActions: string[]
  uniqueChannels: string[]
  uniquePolicies: string[]
}

export default memo(function ExceptionRecommendation({ incidents, uniqueDepartments, uniqueTeams, uniqueActions, uniqueChannels, uniquePolicies }: ExceptionRecommendationProps) {
  const [exceptionDeptFilter, setExceptionDeptFilter] = useState<string[]>([])
  const [exceptionTeamFilter, setExceptionTeamFilter] = useState<string[]>([])
  const [userSearchQuery, setUserSearchQuery] = useState('')
  const [exceptionDomainFilter, setExceptionDomainFilter] = useState('')
  const [exceptionActionFilter, setExceptionActionFilter] = useState<string[]>([])
  const [exceptionChannelFilter, setExceptionChannelFilter] = useState<string[]>([])
  const [exceptionPolicyFilter, setExceptionPolicyFilter] = useState<string[]>([])
  const [exceptionDateRange, setExceptionDateRange] = useState({ start: DEFAULT_START, end: DEFAULT_END })
  const [userIncidents, setUserIncidents] = useState<Incident[]>([])
  const [loadingUserIncidents, setLoadingUserIncidents] = useState(false)

  const [expandedPolicies, setExpandedPolicies] = useState<Set<number>>(new Set())
  const [expandedRules, setExpandedRules] = useState<Set<string>>(new Set())
  const [expandedClassifiers, setExpandedClassifiers] = useState<Set<string>>(new Set())
  const [expandedExceptions, setExpandedExceptions] = useState<Set<string>>(new Set())
  const [ruleThresholds, setRuleThresholds] = useState<Record<string, string>>({})

  const userReportData = useUserReportData(userIncidents)

  const uniqueUserChannels = useMemo(() => Array.from(new Set(userIncidents.map(i => i.channel).filter(Boolean))).sort(), [userIncidents])
  const uniqueUserPolicies = useMemo(() => extractPoliciesFromIncidents(userIncidents), [userIncidents])

  const hasUserQuery = userSearchQuery.trim()
  const hasFilters = exceptionActionFilter.length > 0 || exceptionChannelFilter.length > 0 || exceptionPolicyFilter.length > 0 || exceptionTeamFilter.length > 0 || exceptionDeptFilter.length > 0 || exceptionDomainFilter.trim()
  const canRecommend = !!(hasUserQuery || hasFilters)

  const handleRecommend = useCallback(() => {
    if (!canRecommend) { setUserIncidents([]); return }
    setLoadingUserIncidents(true)
    const startedAt = Date.now()
    const query = userSearchQuery.toLowerCase().trim()
    const domainQuery = exceptionDomainFilter.toLowerCase().trim()

    setTimeout(() => {
      const filtered = incidents.filter(incident => {
        if (exceptionDateRange.start && exceptionDateRange.end) {
          try {
            const d = parseISO(incident.timestamp)
            const s = startOfDay(parseISO(exceptionDateRange.start))
            const e = endOfDay(parseISO(exceptionDateRange.end))
            if (!isWithinInterval(d, { start: s, end: e })) return false
          } catch { /* skip */ }
        }
        if (query && !(incident.userEmail?.toLowerCase().includes(query) || incident.loginName?.toLowerCase().includes(query) || incident.fullName?.toLowerCase().includes(query))) return false
        if (domainQuery && incident.domain && !incident.domain.toLowerCase().includes(domainQuery)) return false
        if (exceptionDeptFilter.length > 0 && !exceptionDeptFilter.includes(incident.department || '')) return false
        if (exceptionTeamFilter.length > 0) {
          const norm = normalizeTeamName(incident.team)
          if (!exceptionTeamFilter.some(t => normalizeTeamName(t) === norm)) return false
        }
        if (exceptionActionFilter.length > 0 && incident.action && !exceptionActionFilter.includes(incident.action)) return false
        if (exceptionChannelFilter.length > 0 && incident.channel && !exceptionChannelFilter.includes(incident.channel)) return false
        if (exceptionPolicyFilter.length > 0) {
          const triggers = parseViolationTriggers(incident.violationTriggers)
          let match = false
          if (triggers.length > 0) {
            triggers.forEach((t: any) => { if ((t.PolicyName || t.policy_name || incident.policy) && exceptionPolicyFilter.includes(t.PolicyName || t.policy_name || incident.policy)) match = true })
          } else if (incident.policy && exceptionPolicyFilter.includes(incident.policy)) match = true
          if (!match) return false
        }
        return true
      })
      setUserIncidents(filtered)
      const elapsed = Date.now() - startedAt
      if (elapsed < 320) setTimeout(() => setLoadingUserIncidents(false), 320 - elapsed)
      else setLoadingUserIncidents(false)
    }, 0)
  }, [incidents, userSearchQuery, exceptionDomainFilter, exceptionDeptFilter, exceptionTeamFilter, exceptionActionFilter, exceptionChannelFilter, exceptionPolicyFilter, exceptionDateRange, canRecommend])

  const handleClearFilters = useCallback(() => {
    setUserSearchQuery(''); setExceptionDomainFilter(''); setExceptionActionFilter([]); setExceptionChannelFilter([]); setExceptionPolicyFilter([]); setExceptionTeamFilter([]); setExceptionDeptFilter([])
    setExceptionDateRange({ start: DEFAULT_START, end: DEFAULT_END }); setUserIncidents([])
    setExpandedPolicies(new Set()); setExpandedRules(new Set()); setExpandedClassifiers(new Set()); setExpandedExceptions(new Set())
    setRuleThresholds({})
  }, [])

  const togglePolicy = (pIdx: number) => setExpandedPolicies(prev => toggleSetItem(prev, pIdx))
  const toggleRule = (pIdx: number, rIdx: number) => setExpandedRules(prev => toggleSetItem(prev, `${pIdx}-${rIdx}`))
  const toggleClassifier = (pIdx: number, rIdx: number, cIdx: number) => setExpandedClassifiers(prev => toggleSetItem(prev, `${pIdx}-${rIdx}-${cIdx}`))
  const toggleException = (pIdx: number, rIdx: number, eIdx: number) => setExpandedExceptions(prev => toggleSetItem(prev, `${pIdx}-${rIdx}-${eIdx}`))

  const hoverHandlers = {
    onMouseEnter: (e: React.MouseEvent) => { (e.currentTarget as HTMLElement).style.opacity = '0.8' },
    onMouseLeave: (e: React.MouseEvent) => { (e.currentTarget as HTMLElement).style.opacity = '1' }
  }

  return (
    <div style={{ ...STYLES.sectionCard('#10b981'), marginTop: '24px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '20px' }}>
        <div style={STYLES.iconBox('linear-gradient(135deg, #10b981, #059669)', '0 3px 10px rgba(16, 185, 129, 0.25)')}>
          <Sparkles size={17} color="#fff" />
        </div>
        <h2 style={STYLES.gradientText('linear-gradient(135deg, #10b981, #059669)')}>Exception Recommendation</h2>
      </div>

      {/* Filters Grid */}
      <div style={{ marginBottom: '20px', display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', padding: '16px', background: 'rgba(16, 185, 129, 0.03)', borderRadius: '10px', border: '1px solid rgba(16, 185, 129, 0.12)' }}>
        <SearchableMultiSelect label="Filter by Department" options={uniqueDepartments} selectedValues={exceptionDeptFilter} onChange={setExceptionDeptFilter} placeholder="All Departments" />
        <SearchableMultiSelect label="Filter by Team" options={uniqueTeams} selectedValues={exceptionTeamFilter} onChange={setExceptionTeamFilter} placeholder="All Teams" />
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', display: 'block', letterSpacing: '0.3px' }}>Date Range</label>
          <div style={{ display: 'flex', gap: '4px' }}>
            <input type="date" value={exceptionDateRange.start} onChange={(e) => setExceptionDateRange(prev => ({ ...prev, start: e.target.value }))} style={{ flex: 1, padding: '10px 8px', borderRadius: '8px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', transition: 'border-color 0.2s' }} />
            <input type="date" value={exceptionDateRange.end} onChange={(e) => setExceptionDateRange(prev => ({ ...prev, end: e.target.value }))} style={{ flex: 1, padding: '10px 8px', borderRadius: '8px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', transition: 'border-color 0.2s' }} />
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', display: 'block', letterSpacing: '0.3px' }}>Search User</label>
          <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
            <Search size={14} style={{ position: 'absolute', left: '10px', color: 'var(--text-secondary)', pointerEvents: 'none' }} />
            <input type="text" placeholder="Email, login name or full name..." value={userSearchQuery} onChange={(e) => setUserSearchQuery(e.target.value)} style={{ width: '100%', padding: '10px 12px 10px 32px', borderRadius: '8px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px', transition: 'border-color 0.2s', boxSizing: 'border-box' }} />
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', display: 'block', letterSpacing: '0.3px' }}>Filter by Domain</label>
          <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
            <Search size={14} style={{ position: 'absolute', left: '10px', color: 'var(--text-secondary)', pointerEvents: 'none' }} />
            <input type="text" placeholder="Enter domain to filter..." value={exceptionDomainFilter} onChange={(e) => setExceptionDomainFilter(e.target.value)} style={{ width: '100%', padding: '10px 12px 10px 32px', borderRadius: '8px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '13px', transition: 'border-color 0.2s', boxSizing: 'border-box' }} />
          </div>
        </div>
        <SearchableMultiSelect label="Filter by Action" options={uniqueActions} selectedValues={exceptionActionFilter} onChange={setExceptionActionFilter} placeholder="All Actions" />
        <SearchableMultiSelect label="Filter by Channel" options={uniqueChannels} selectedValues={exceptionChannelFilter} onChange={setExceptionChannelFilter} placeholder="All Channels" />
        <SearchableMultiSelect label="Filter by Policy" options={uniquePolicies} selectedValues={exceptionPolicyFilter} onChange={setExceptionPolicyFilter} placeholder="All Policies" />
      </div>

      {/* Buttons */}
      <div style={{ marginBottom: '20px', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button onClick={handleClearFilters} style={{ padding: '12px 24px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px', fontWeight: '600', cursor: 'pointer', transition: 'all 0.2s' }}
          onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--surface-hover)'; e.currentTarget.style.transform = 'translateY(-1px)' }}
          onMouseLeave={(e) => { e.currentTarget.style.background = 'var(--surface)'; e.currentTarget.style.transform = 'translateY(0)' }}>
          Filtreleri Temizle
        </button>
        <button onClick={handleRecommend} disabled={!canRecommend} style={{ padding: '12px 24px', borderRadius: '6px', border: 'none', background: canRecommend ? '#3b82f6' : '#93c5fd', color: '#ffffff', fontSize: '14px', fontWeight: '600', cursor: canRecommend ? 'pointer' : 'not-allowed', transition: 'all 0.2s', boxShadow: canRecommend ? '0 2px 4px rgba(59, 130, 246, 0.3)' : 'none', opacity: canRecommend ? 1 : 0.7 }}
          onMouseEnter={(e) => { if (canRecommend) { e.currentTarget.style.background = '#2563eb'; e.currentTarget.style.transform = 'translateY(-1px)' } }}
          onMouseLeave={(e) => { if (canRecommend) { e.currentTarget.style.background = '#3b82f6'; e.currentTarget.style.transform = 'translateY(0)' } }}>
          Recommend
        </button>
      </div>

      {/* Summary Statistics */}
      {userIncidents.length > 0 && userReportData.length > 0 && (
        <div style={{ display: 'flex', gap: '24px', marginBottom: '24px', padding: '16px', background: 'var(--background-secondary)', borderRadius: '8px', border: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', position: 'relative', cursor: 'pointer' }} className="group">
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: '500' }}>Total Policies</div>
            <div style={{ fontSize: '20px', fontWeight: '600', color: 'var(--text-primary)' }}>{userReportData.length}</div>
            {uniqueUserPolicies.length > 0 && (
              <div className="hidden group-hover:block" style={{ position: 'absolute', bottom: '100%', left: 0, marginBottom: '8px', background: 'var(--surface)', border: '1px solid var(--border)', padding: '12px', borderRadius: '6px', boxShadow: '0 4px 6px rgba(0,0,0,0.1)', zIndex: 1000, minWidth: '200px', maxWidth: '400px', pointerEvents: 'none' }}>
                <div style={{ fontSize: '11px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)', paddingBottom: '4px' }}>Policies ({uniqueUserPolicies.length}):</div>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.6' }}>{uniqueUserPolicies.map((p, i) => <div key={i} style={{ marginBottom: '4px' }}>• {p}</div>)}</div>
              </div>
            )}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: '500' }}>Total Incidents</div>
            <div style={{ fontSize: '20px', fontWeight: '600', color: '#3b82f6' }}>{userIncidents.length}</div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', position: 'relative', cursor: 'pointer' }} className="group">
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: '500' }}>Total Channels</div>
            <div style={{ fontSize: '20px', fontWeight: '600', color: '#10b981' }}>{uniqueUserChannels.length}</div>
            {uniqueUserChannels.length > 0 && (
              <div className="hidden group-hover:block" style={{ position: 'absolute', bottom: '100%', left: 0, marginBottom: '8px', background: 'var(--surface)', border: '1px solid var(--border)', padding: '12px', borderRadius: '6px', boxShadow: '0 4px 6px rgba(0,0,0,0.1)', zIndex: 1000, minWidth: '200px', maxWidth: '400px', pointerEvents: 'none' }}>
                <div style={{ fontSize: '11px', fontWeight: '600', marginBottom: '8px', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)', paddingBottom: '4px' }}>Channels ({uniqueUserChannels.length}):</div>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.6' }}>{uniqueUserChannels.map((c, i) => <div key={i} style={{ marginBottom: '4px' }}>• {c}</div>)}</div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Report Data */}
      {loadingUserIncidents ? (
        <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>Öneri hesaplanıyor…</div>
      ) : userIncidents.length === 0 ? (
        <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>Seçilen filtrelere uyan incident bulunamadı</div>
      ) : userReportData.length === 0 ? (
        <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>Bu kayıtlar için politika / tetikleyici verisi bulunamadı</div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          {userReportData.map((policy, pIdx) => {
            const isPolicyExpanded = expandedPolicies.has(pIdx)
            return (
              <div key={pIdx} style={{ background: 'var(--background-secondary)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
                <div onClick={() => togglePolicy(pIdx)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: isPolicyExpanded ? '16px' : '0', paddingBottom: isPolicyExpanded ? '12px' : '0', borderBottom: isPolicyExpanded ? '2px solid var(--border)' : 'none', ...STYLES.accordionHover }} {...hoverHandlers}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={STYLES.expandButton(24)}>{isPolicyExpanded ? <Minus size={16} /> : <Plus size={16} />}</div>
                    <span style={{ width: '12px', height: '12px', borderRadius: '50%', background: '#3b82f6' }} />
                    <h3 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>{policy.name}</h3>
                  </div>
                  <div style={{ display: 'flex', gap: '24px', fontSize: '13px' }}>
                    <div style={{ textAlign: 'right' }}><div style={STYLES.statLabel('11px')}>Total Incidents</div><div style={{ ...STYLES.statValue(), fontSize: '16px' }}>{policy.incidentCount}</div></div>
                    <div style={{ textAlign: 'right' }}><div style={STYLES.statLabel('11px')}>Avg Matches</div><div style={{ ...STYLES.statValue('#3b82f6'), fontSize: '16px' }}>{policy.avgMatches.toFixed(1)}</div></div>
                  </div>
                </div>

                {isPolicyExpanded && policy.rules.length > 0 && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', paddingLeft: '12px' }}>
                    {policy.rules.map((rule, rIdx) => {
                      const isRuleExpanded = expandedRules.has(`${pIdx}-${rIdx}`)
                      return (
                        <div key={rIdx} style={{ borderLeft: '3px solid #ef4444', paddingLeft: '16px', paddingTop: '12px', paddingBottom: '12px', background: 'var(--surface)', borderRadius: '6px' }}>
                          <div onClick={() => toggleRule(pIdx, rIdx)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: isRuleExpanded ? '12px' : '0', ...STYLES.accordionHover }} {...hoverHandlers}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                              <div style={STYLES.expandButton(20)}>{isRuleExpanded ? <Minus size={14} /> : <Plus size={14} />}</div>
                              <span style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)' }}><ClipboardList size={16} style={{ marginRight: '4px' }} /> {rule.name}</span>
                            </div>
                            <div style={{ display: 'flex', gap: '16px', fontSize: '12px' }}>
                              {[{ label: 'Incidents', value: rule.incidentCount, color: undefined }, { label: 'Avg Matches', value: rule.avgMatches.toFixed(1), color: '#3b82f6' }, { label: 'P25', value: rule.p25.toFixed(1), color: '#10b981' }, { label: 'P75', value: rule.p75.toFixed(1), color: '#f59e0b' }, { label: 'P90', value: rule.p90.toFixed(1), color: '#ef4444' }].map(s => (
                                <div key={s.label} style={{ textAlign: 'right' }}><div style={STYLES.statLabel()}>{s.label}</div><div style={STYLES.statValue(s.color)}>{s.value}</div></div>
                              ))}
                            </div>
                          </div>

                          {isRuleExpanded && rule.allMatches && rule.allMatches.length > 0 && (() => {
                            const ruleKey = `${pIdx}-${rIdx}`
                            const thresholdStr = ruleThresholds[ruleKey] || ''
                            const thresholdVal = thresholdStr ? parseFloat(thresholdStr) : null
                            const belowCount = thresholdVal !== null ? rule.allMatches.filter(m => m <= thresholdVal).length : null
                            const aboveCount = thresholdVal !== null ? rule.allMatches.filter(m => m > thresholdVal).length : null
                            const totalCount = rule.allMatches.length
                            const belowPct = belowCount !== null && totalCount > 0 ? ((belowCount / totalCount) * 100).toFixed(1) : null
                            const abovePct = aboveCount !== null && totalCount > 0 ? ((aboveCount / totalCount) * 100).toFixed(1) : null
                            return (
                              <div style={{ margin: '12px 0 0 8px', padding: '14px 16px', background: 'linear-gradient(135deg, rgba(99, 102, 241, 0.06), rgba(139, 92, 246, 0.04))', borderRadius: '8px', border: '1px solid rgba(99, 102, 241, 0.18)' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '10px' }}>
                                  <SlidersHorizontal size={14} color="#6366f1" />
                                  <span style={{ fontSize: '12px', fontWeight: '600', color: '#6366f1' }}>Threshold Analizi</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
                                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                    <label style={{ fontSize: '11px', fontWeight: '500', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>Threshold Değeri:</label>
                                    <input
                                      type="number"
                                      min="0"
                                      step="1"
                                      placeholder="Örn: 5"
                                      value={thresholdStr}
                                      onClick={(e) => e.stopPropagation()}
                                      onChange={(e) => {
                                        e.stopPropagation()
                                        setRuleThresholds(prev => ({ ...prev, [ruleKey]: e.target.value }))
                                      }}
                                      style={{ width: '90px', padding: '6px 10px', borderRadius: '6px', border: '1px solid rgba(99, 102, 241, 0.3)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', fontWeight: '600', textAlign: 'center', outline: 'none', transition: 'border-color 0.2s' }}
                                      onFocus={(e) => { e.currentTarget.style.borderColor = '#6366f1' }}
                                      onBlur={(e) => { e.currentTarget.style.borderColor = 'rgba(99, 102, 241, 0.3)' }}
                                    />
                                  </div>
                                  {thresholdVal !== null && belowCount !== null && aboveCount !== null && (
                                    <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
                                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', padding: '5px 12px', borderRadius: '6px', background: 'rgba(16, 185, 129, 0.1)', border: '1px solid rgba(16, 185, 129, 0.25)' }}>
                                        <span style={{ width: '7px', height: '7px', borderRadius: '50%', background: '#10b981', display: 'inline-block' }} />
                                        <span style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>≤ {thresholdVal}:</span>
                                        <span style={{ fontSize: '13px', fontWeight: '700', color: '#10b981' }}>{belowCount}</span>
                                        <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>({belowPct}%)</span>
                                      </div>
                                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', padding: '5px 12px', borderRadius: '6px', background: 'rgba(239, 68, 68, 0.1)', border: '1px solid rgba(239, 68, 68, 0.25)' }}>
                                        <span style={{ width: '7px', height: '7px', borderRadius: '50%', background: '#ef4444', display: 'inline-block' }} />
                                        <span style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>&gt; {thresholdVal}:</span>
                                        <span style={{ fontSize: '13px', fontWeight: '700', color: '#ef4444' }}>{aboveCount}</span>
                                        <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>({abovePct}%)</span>
                                      </div>
                                      {/* Progress bar */}
                                      <div style={{ flex: 1, minWidth: '80px', maxWidth: '200px', height: '8px', borderRadius: '4px', background: 'rgba(239, 68, 68, 0.2)', overflow: 'hidden' }}>
                                        <div style={{ height: '100%', width: `${belowPct}%`, background: 'linear-gradient(90deg, #10b981, #34d399)', borderRadius: '4px', transition: 'width 0.3s ease' }} />
                                      </div>
                                    </div>
                                  )}
                                </div>
                              </div>
                            )
                          })()}

                          {isRuleExpanded && (
                            <>
                              {rule.classifiers.length > 0 && (
                                <div style={{ paddingLeft: '8px', marginTop: '12px', marginBottom: '12px' }}>
                                  <div style={{ padding: '16px', background: 'var(--background-secondary)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                                    <h5 style={{ fontSize: '13px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '12px', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>Classifier Statistics</h5>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                      {rule.classifiers.map((classifier, cIdx) => {
                                        const isExpanded = expandedClassifiers.has(`${pIdx}-${rIdx}-${cIdx}`)
                                        return (
                                          <div key={cIdx} style={{ padding: '12px', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '6px' }}>
                                            <div onClick={() => toggleClassifier(pIdx, rIdx, cIdx)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: isExpanded ? '8px' : '0', ...STYLES.accordionHover }} {...hoverHandlers}>
                                              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                <div style={STYLES.expandButton(18)}>{isExpanded ? <Minus size={12} /> : <Plus size={12} />}</div>
                                                <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-primary)' }}>{classifier.name}</div>
                                              </div>
                                            </div>
                                            {isExpanded && (
                                              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '12px', fontSize: '11px', marginTop: '8px' }}>
                                                {[{ label: 'Incidents', value: classifier.incidentCount, color: undefined }, { label: 'Avg Matches', value: classifier.avgMatches.toFixed(1), color: '#3b82f6' }, { label: 'P25', value: classifier.p25.toFixed(1), color: '#10b981' }, { label: 'P75', value: classifier.p75.toFixed(1), color: '#f59e0b' }, { label: 'P90', value: classifier.p90.toFixed(1), color: '#ef4444' }].map(s => (
                                                  <div key={s.label} style={{ textAlign: 'center' }}><div style={STYLES.statLabel()}>{s.label}</div><div style={STYLES.statValue(s.color)}>{s.value}</div></div>
                                                ))}
                                              </div>
                                            )}
                                          </div>
                                        )
                                      })}
                                    </div>
                                  </div>
                                </div>
                              )}

                              {rule.exceptions && rule.exceptions.length > 0 && (
                                <div style={{ paddingLeft: '8px', marginTop: '12px', marginBottom: '12px' }}>
                                  <div style={{ padding: '16px', background: 'var(--background-secondary)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                                    <h5 style={{ fontSize: '13px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '12px', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>Exception Statistics</h5>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                      {rule.exceptions.map((exception, eIdx) => {
                                        const isExpanded = expandedExceptions.has(`${pIdx}-${rIdx}-${eIdx}`)
                                        return (
                                          <div key={eIdx} style={{ padding: '12px', background: 'var(--surface)', border: '2px solid #f59e0b', borderRadius: '6px' }}>
                                            <div onClick={() => toggleException(pIdx, rIdx, eIdx)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: isExpanded ? '8px' : '0', ...STYLES.accordionHover }} {...hoverHandlers}>
                                              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                <div style={STYLES.expandButton(18)}>{isExpanded ? '−' : '+'}</div>
                                                <span style={{ fontSize: '12px', fontWeight: '600', color: '#f59e0b' }}>⚠️ {exception.name}</span>
                                                <span style={{ fontSize: '10px', fontWeight: '600', padding: '2px 6px', borderRadius: '4px', background: 'rgba(245, 158, 11, 0.15)', color: '#f59e0b', textTransform: 'none' as const }}>Exception</span>
                                              </div>
                                              <div style={{ display: 'flex', gap: '12px', fontSize: '11px' }}>
                                                <div style={{ textAlign: 'right' }}><div style={STYLES.statLabel('9px')}>Incidents</div><div style={STYLES.statValue()}>{exception.incidentCount}</div></div>
                                                <div style={{ textAlign: 'right' }}><div style={STYLES.statLabel('9px')}>Avg Matches</div><div style={STYLES.statValue('#3b82f6')}>{exception.avgMatches.toFixed(1)}</div></div>
                                              </div>
                                            </div>
                                            {isExpanded && exception.classifiers && exception.classifiers.length > 0 && (
                                              <div style={{ marginTop: '8px', paddingLeft: '8px' }}>
                                                <div style={{ padding: '12px', background: 'var(--background-secondary)', borderRadius: '6px', border: '1px solid var(--border)' }}>
                                                  <h6 style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px', paddingBottom: '6px', borderBottom: '1px solid var(--border)' }}>Exception Classifiers</h6>
                                                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                                    {exception.classifiers.map((cl, cIdx) => (
                                                      <div key={cIdx} style={{ padding: '8px', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '4px' }}>
                                                        <div style={{ fontSize: '11px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '6px' }}>{cl.name}</div>
                                                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '8px', fontSize: '10px' }}>
                                                          {[{ label: 'Incidents', value: cl.incidentCount, color: undefined }, { label: 'Avg Matches', value: cl.avgMatches.toFixed(1), color: '#3b82f6' }, { label: 'P25', value: cl.p25.toFixed(1), color: '#10b981' }, { label: 'P75', value: cl.p75.toFixed(1), color: '#f59e0b' }, { label: 'P90', value: cl.p90.toFixed(1), color: '#ef4444' }].map(s => (
                                                            <div key={s.label} style={{ textAlign: 'center' }}><div style={STYLES.statLabel('9px')}>{s.label}</div><div style={STYLES.statValue(s.color)}>{s.value}</div></div>
                                                          ))}
                                                        </div>
                                                      </div>
                                                    ))}
                                                  </div>
                                                </div>
                                              </div>
                                            )}
                                          </div>
                                        )
                                      })}
                                    </div>
                                  </div>
                                </div>
                              )}

                              {rule.classifiers.length > 0 && (
                                <div style={{ paddingLeft: '8px', marginTop: '12px' }}>
                                  <div style={{ padding: '16px', background: 'var(--background-secondary)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                                    <h5 style={{ fontSize: '13px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '12px', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>Exception Recommendations</h5>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                      <div style={{ padding: '12px', background: 'rgba(245, 158, 11, 0.1)', border: '1px solid rgba(245, 158, 11, 0.3)', borderRadius: '6px' }}>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                                          <span style={{ width: '10px', height: '10px', borderRadius: '50%', background: '#f59e0b' }} />
                                          <strong style={{ color: '#f59e0b', fontSize: '13px' }}>Medium (Audit):</strong>
                                        </div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-primary)', lineHeight: '1.6', paddingLeft: '18px' }}>
                                          {rule.classifiers.map((c, i) => <span key={i}>{c.name}: &gt;{c.recommendations.medium.threshold.toFixed(0)}{i < rule.classifiers.length - 1 ? ' ' : ''}</span>)}
                                        </div>
                                      </div>
                                      <div style={{ padding: '12px', background: 'rgba(239, 68, 68, 0.1)', border: '1px solid rgba(239, 68, 68, 0.3)', borderRadius: '6px' }}>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                                          <span style={{ width: '10px', height: '10px', borderRadius: '50%', background: '#ef4444' }} />
                                          <strong style={{ color: '#ef4444', fontSize: '13px' }}>High (Block):</strong>
                                        </div>
                                        <div style={{ fontSize: '12px', color: 'var(--text-primary)', lineHeight: '1.6', paddingLeft: '18px' }}>
                                          {rule.classifiers.map((c, i) => <span key={i}>{c.name}: &gt;{c.recommendations.high.threshold.toFixed(0)}{i < rule.classifiers.length - 1 ? ' ' : ''}</span>)}
                                        </div>
                                      </div>
                                    </div>
                                  </div>
                                </div>
                              )}
                            </>
                          )}
                        </div>
                      )
                    })}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
})
