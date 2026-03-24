'use client'

import React, { useState, useMemo, memo, ChangeEvent } from 'react'
import { RotateCcw, ChevronUp, ChevronDown, BarChart3 } from 'lucide-react'
import Pagination from '@/components/ui/Pagination'
import SearchableMultiSelect from './SearchableMultiSelect'
import type { Incident, AppliedFilters, HeatmapData } from '../_lib/types'
import { normalizeTeamName, isInDateRange, matchesUserSearch, getHeatmapColor, getTextColor } from '../_lib/utils'
import {
  DEFAULT_START, DEFAULT_END, TEAMS_PER_PAGE, INITIAL_DOMAIN_COUNT,
  DEFAULT_APPLIED_FILTERS, EMPTY_BREAKDOWN, OTHER_LABEL, STYLES
} from '../_lib/constants'

interface HeatmapSectionProps {
  incidents: Incident[]
  uniqueDepartments: string[]
  uniqueTeams: string[]
  uniqueActions: string[]
}

export default memo(function HeatmapSection({ incidents, uniqueDepartments, uniqueTeams, uniqueActions }: HeatmapSectionProps) {
  // Heatmap pagination & visibility
  const [heatmapTeamPage, setHeatmapTeamPage] = useState(1)
  const [heatmapDomainCount, setHeatmapDomainCount] = useState(INITIAL_DOMAIN_COUNT)
  const [hiddenDomains, setHiddenDomains] = useState<Set<string>>(new Set())
  const [hiddenTeams, setHiddenTeams] = useState<Set<string>>(new Set())

  // Filters (pending)
  const [dateRange, setDateRange] = useState({ start: DEFAULT_START, end: DEFAULT_END })
  const [dateError, setDateError] = useState('')
  const [selectedDepartments, setSelectedDepartments] = useState<string[]>([])
  const [selectedTeams, setSelectedTeams] = useState<string[]>([])
  const [selectedUser, setSelectedUser] = useState('')
  const [selectedFullName, setSelectedFullName] = useState('')
  const [selectedPolicy, setSelectedPolicy] = useState('')
  const [selectedDomain, setSelectedDomain] = useState('')
  const [selectedActions, setSelectedActions] = useState<string[]>([])

  // Applied (snapshot after clicking "Filtrele")
  const [appliedFilters, setAppliedFilters] = useState<AppliedFilters>(DEFAULT_APPLIED_FILTERS)

  const applyFilters = () => {
    setAppliedFilters({
      dateRange: { ...dateRange },
      selectedDepartments, selectedTeams, selectedUser,
      selectedFullName, selectedPolicy, selectedDomain, selectedActions
    })
  }

  const resetFilters = () => {
    setDateRange({ start: DEFAULT_START, end: DEFAULT_END })
    setDateError('')
    setSelectedDepartments([])
    setSelectedTeams([])
    setSelectedUser('')
    setSelectedFullName('')
    setSelectedPolicy('')
    setSelectedDomain('')
    setSelectedActions([])
    setAppliedFilters(DEFAULT_APPLIED_FILTERS)
  }

  // Filtered incidents for heatmap
  const heatmapFilteredIncidents = useMemo(() => {
    return incidents.filter(incident => {
      if (!isInDateRange(incident.timestamp, appliedFilters.dateRange)) return false
      if (appliedFilters.selectedDepartments.length > 0 && !appliedFilters.selectedDepartments.includes(incident.department || '')) return false
      if (appliedFilters.selectedTeams.length > 0) {
        const norm = normalizeTeamName(incident.team)
        if (!appliedFilters.selectedTeams.some(t => normalizeTeamName(t) === norm)) return false
      }
      if (!matchesUserSearch(incident, appliedFilters.selectedUser)) return false
      if (appliedFilters.selectedFullName && !incident.fullName?.toLowerCase().includes(appliedFilters.selectedFullName.toLowerCase())) return false
      if (appliedFilters.selectedPolicy && !incident.policy?.toLowerCase().includes(appliedFilters.selectedPolicy.toLowerCase())) return false
      if (appliedFilters.selectedDomain && !incident.domain?.toLowerCase().includes(appliedFilters.selectedDomain.toLowerCase())) return false
      if (appliedFilters.selectedActions.length > 0 && !appliedFilters.selectedActions.includes(incident.action || '')) return false
      return true
    })
  }, [incidents, appliedFilters])

  // Split heatmapData: Step 1 - compute raw counts (only depends on filtered incidents)
  const heatmapRawData = useMemo(() => {
    const teams = new Set<string>()
    const domains = new Set<string>()
    const counts: Record<string, Record<string, number>> = {}
    const breakdown: Record<string, Record<string, typeof EMPTY_BREAKDOWN>> = {}
    const domainTotalCounts: Record<string, number> = {}
    const teamTotalCounts: Record<string, number> = {}

    heatmapFilteredIncidents.forEach(incident => {
      const rawTeam = incident.team || incident.department || 'Unknown'
      const team = normalizeTeamName(rawTeam) || 'Hesap Araştırmaları'
      const domain = incident.domain || 'Unknown'
      const action = incident.action?.toLowerCase() || 'permit'

      teams.add(team)
      domains.add(domain)

      if (!counts[team]) counts[team] = {}
      counts[team][domain] = (counts[team][domain] || 0) + 1

      if (!breakdown[team]) breakdown[team] = {}
      if (!breakdown[team][domain]) breakdown[team][domain] = { block: 0, permit: 0, authorized: 0, quarantine: 0, maxMatchTotal: 0, incidentCount: 0 }

      breakdown[team][domain].maxMatchTotal += (incident.maxMatches || 0)
      breakdown[team][domain].incidentCount++

      if (action.includes('block')) breakdown[team][domain].block++
      else if (action.includes('permit') || action.includes('released')) breakdown[team][domain].permit++
      else if (action.includes('authorized') || action.includes('allow')) breakdown[team][domain].authorized++
      else if (action.includes('quarantine')) breakdown[team][domain].quarantine++

      domainTotalCounts[domain] = (domainTotalCounts[domain] || 0) + 1
      teamTotalCounts[team] = (teamTotalCounts[team] || 0) + 1
    })

    const sortedTeams = Array.from(teams).sort((a, b) => (teamTotalCounts[b] || 0) - (teamTotalCounts[a] || 0))
    const allSortedDomains = Array.from(domains).sort((a, b) => (domainTotalCounts[b] || 0) - (domainTotalCounts[a] || 0))

    return { sortedTeams, allSortedDomains, counts, breakdown }
  }, [heatmapFilteredIncidents])

  // Split heatmapData: Step 2 - domain slicing (only re-runs when domainCount changes)
  const heatmapData: HeatmapData = useMemo(() => {
    const { sortedTeams, allSortedDomains, counts, breakdown } = heatmapRawData
    const topDomains = allSortedDomains.slice(0, heatmapDomainCount)
    const otherDomains = allSortedDomains.slice(heatmapDomainCount)
    const domains = otherDomains.length > 0 ? [...topDomains, OTHER_LABEL] : topDomains

    if (otherDomains.length > 0) {
      sortedTeams.forEach(t => {
        let otherCount = 0
        const otherBd = { ...EMPTY_BREAKDOWN }
        otherDomains.forEach(d => {
          otherCount += counts[t]?.[d] || 0
          const bd = breakdown[t]?.[d]
          if (bd) {
            otherBd.block += bd.block
            otherBd.permit += bd.permit
            otherBd.authorized += bd.authorized
            otherBd.quarantine += bd.quarantine
            otherBd.maxMatchTotal += bd.maxMatchTotal
            otherBd.incidentCount += bd.incidentCount
          }
        })
        if (!counts[t]) counts[t] = {}
        counts[t][OTHER_LABEL] = otherCount
        if (!breakdown[t]) breakdown[t] = {}
        breakdown[t][OTHER_LABEL] = otherBd
      })
    }

    return { teams: sortedTeams, domains, counts, breakdown, hasMoreDomains: otherDomains.length > 0 }
  }, [heatmapRawData, heatmapDomainCount])

  const visibleTeams = heatmapData.teams.filter(t => !hiddenTeams.has(t))
  const totalTeamPages = Math.ceil(visibleTeams.length / TEAMS_PER_PAGE)
  const paginatedTeams = visibleTeams.slice((heatmapTeamPage - 1) * TEAMS_PER_PAGE, heatmapTeamPage * TEAMS_PER_PAGE)
  const visibleDomains = heatmapData.domains.filter(d => !hiddenDomains.has(d))

  const handleDateChange = (field: 'start' | 'end') => (e: ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value
    setDateRange(prev => {
      const newer = { ...prev, [field]: val }
      setDateError(newer.start && newer.end && newer.start > newer.end ? 'Start > End' : '')
      return newer
    })
  }

  return (
    <div style={{ ...STYLES.sectionCard('#3b82f6'), marginBottom: '24px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
        <div style={STYLES.iconBox('linear-gradient(135deg, #3b82f6, #06b6d4)', '0 3px 10px rgba(59, 130, 246, 0.25)')}>
          <BarChart3 size={17} color="#fff" />
        </div>
        <h2 style={STYLES.gradientText('linear-gradient(135deg, #3b82f6, #06b6d4)')}>Team Based Analysis Heatmap</h2>
      </div>

      {/* Inline Filters Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '12px', alignItems: 'end', marginBottom: '16px', padding: '14px', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <label style={STYLES.filterLabel}>Start</label>
          <input type="date" value={dateRange.start} onChange={handleDateChange('start')}
            style={{ ...STYLES.filterInput, border: dateError ? '1px solid #ef4444' : '1px solid var(--border)' }} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <label style={STYLES.filterLabel}>End</label>
          <input type="date" value={dateRange.end} onChange={handleDateChange('end')}
            style={{ ...STYLES.filterInput, border: dateError ? '1px solid #ef4444' : '1px solid var(--border)' }} />
          {dateError && <span style={{ color: '#ef4444', fontSize: '9px', marginTop: '1px' }}>{dateError}</span>}
        </div>
        <SearchableMultiSelect label="Department" options={uniqueDepartments} selectedValues={selectedDepartments} onChange={setSelectedDepartments} placeholder="All" compact />
        <SearchableMultiSelect label="Team" options={uniqueTeams} selectedValues={selectedTeams} onChange={setSelectedTeams} placeholder="All" compact />
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <label style={STYLES.filterLabel}>User</label>
          <input type="text" placeholder="User..." value={selectedUser} onChange={(e) => setSelectedUser(e.target.value)} style={STYLES.filterInput} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <label style={STYLES.filterLabel}>Manager Name</label>
          <input type="text" placeholder="Name..." value={selectedFullName} onChange={(e) => setSelectedFullName(e.target.value)} style={STYLES.filterInput} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <label style={STYLES.filterLabel}>Policy</label>
          <input type="text" placeholder="Policy..." value={selectedPolicy} onChange={(e) => setSelectedPolicy(e.target.value)} style={STYLES.filterInput} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <label style={STYLES.filterLabel}>Domain</label>
          <input type="text" placeholder="Domain..." value={selectedDomain} onChange={(e) => setSelectedDomain(e.target.value)} style={STYLES.filterInput} />
        </div>
        <SearchableMultiSelect label="Action" options={uniqueActions} selectedValues={selectedActions} onChange={setSelectedActions} placeholder="All" compact />
        <div style={{ display: 'flex', gap: '6px', alignItems: 'flex-end', alignSelf: 'end' }}>
          <button onClick={applyFilters} style={{ flex: 1, padding: '6px 0', borderRadius: '6px', border: 'none', background: '#3b82f6', color: '#fff', fontSize: '12px', fontWeight: '600', cursor: 'pointer', whiteSpace: 'nowrap', transition: 'background 0.2s' }}>Filtrele</button>
          <button onClick={resetFilters} style={{ flex: 1, padding: '6px 0', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', fontWeight: '500', cursor: 'pointer', whiteSpace: 'nowrap', transition: 'background 0.2s' }}>Temizle</button>
        </div>
      </div>

      {/* Hidden items controls */}
      {(hiddenDomains.size > 0 || hiddenTeams.size > 0 || heatmapDomainCount > INITIAL_DOMAIN_COUNT) && (
        <div style={{ display: 'flex', gap: '8px', marginBottom: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
          {hiddenDomains.size > 0 && (
            <button onClick={() => setHiddenDomains(new Set())} style={{ padding: '4px 10px', borderRadius: '4px', border: '1px solid #f59e0b', background: 'rgba(245, 158, 11, 0.1)', color: '#f59e0b', fontSize: '11px', fontWeight: '500', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px' }}>
              <RotateCcw size={14} /> {hiddenDomains.size} gizli domain göster
            </button>
          )}
          {hiddenTeams.size > 0 && (
            <button onClick={() => setHiddenTeams(new Set())} style={{ padding: '4px 10px', borderRadius: '4px', border: '1px solid #8b5cf6', background: 'rgba(139, 92, 246, 0.1)', color: '#8b5cf6', fontSize: '11px', fontWeight: '500', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px' }}>
              <RotateCcw size={14} /> {hiddenTeams.size} gizli team göster
            </button>
          )}
          {heatmapDomainCount > INITIAL_DOMAIN_COUNT && (
            <button onClick={() => setHeatmapDomainCount(INITIAL_DOMAIN_COUNT)} style={{ padding: '4px 10px', borderRadius: '4px', border: '1px solid #3b82f6', background: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6', fontSize: '11px', fontWeight: '500', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px' }}>
              <ChevronUp size={14} /> Domain listesini daralt (ilk 10)
            </button>
          )}
        </div>
      )}

      {/* Heatmap Grid */}
      {heatmapFilteredIncidents.length > 0 && (
        <div style={{ position: 'relative', overflowX: 'auto', maxWidth: '100%' }}>
          <div style={{ display: 'grid', gridTemplateColumns: `200px repeat(${paginatedTeams.length}, 110px)`, gap: '3px', width: 'fit-content', margin: '0 auto' }}>
            {/* Corner Cell */}
            <div style={{ padding: '10px 12px', fontWeight: '700', color: '#3b82f6', fontSize: '11px', textTransform: 'none', letterSpacing: '0.5px', position: 'sticky', left: 0, zIndex: 10, background: 'linear-gradient(135deg, rgba(59, 130, 246, 0.08), rgba(6, 182, 212, 0.06))', borderRadius: '8px 0 0 0', display: 'flex', alignItems: 'center', gap: '6px', borderBottom: '2px solid rgba(59, 130, 246, 0.15)', borderRight: '2px solid rgba(59, 130, 246, 0.15)' }}>
              <span style={{ display: 'inline-flex', width: '6px', height: '6px', borderRadius: '50%', background: 'linear-gradient(135deg, #3b82f6, #06b6d4)' }} />
              Domain / Team
            </div>
            {/* Team Headers */}
            {paginatedTeams.map((team, idx) => (
              <div key={team} style={{ padding: '10px 6px', fontWeight: '700', color: 'var(--text-primary)', fontSize: '11px', textAlign: 'center', background: 'linear-gradient(180deg, rgba(59, 130, 246, 0.07) 0%, rgba(59, 130, 246, 0.03) 100%)', borderRadius: idx === paginatedTeams.length - 1 ? '0 8px 0 0' : '0', minHeight: '64px', maxHeight: '64px', display: 'flex', alignItems: 'center', justifyContent: 'center', width: '110px', cursor: 'pointer', transition: 'all 0.2s', borderBottom: '2px solid rgba(59, 130, 246, 0.15)', letterSpacing: '0.2px' }}
                title={`${team} - Gizlemek için tıklayın`}
                onClick={() => setHiddenTeams(prev => new Set([...Array.from(prev), team]))}
                onMouseEnter={(e) => { e.currentTarget.style.background = 'rgba(59, 130, 246, 0.12)' }}
                onMouseLeave={(e) => { e.currentTarget.style.background = 'linear-gradient(180deg, rgba(59, 130, 246, 0.07) 0%, rgba(59, 130, 246, 0.03) 100%)' }}
              >
                <span style={{ wordWrap: 'break-word', wordBreak: 'break-word', overflowWrap: 'break-word', lineHeight: '1.35', overflow: 'hidden', display: '-webkit-box', WebkitLineClamp: 3, WebkitBoxOrient: 'vertical', textAlign: 'center', width: '100%' }}>{team}</span>
              </div>
            ))}

            {/* Data Rows */}
            {visibleDomains.map((domain, rowIdx) => (
              <React.Fragment key={domain}>
                <div style={{ padding: '8px 12px', fontWeight: '600', color: domain === OTHER_LABEL ? '#3b82f6' : 'var(--text-primary)', fontSize: '12px', background: domain === OTHER_LABEL ? 'linear-gradient(90deg, rgba(59, 130, 246, 0.1), rgba(59, 130, 246, 0.04))' : rowIdx % 2 === 0 ? 'rgba(148, 163, 184, 0.04)' : 'rgba(148, 163, 184, 0.08)', borderRadius: rowIdx === visibleDomains.length - 1 ? '0 0 0 8px' : '0', display: 'flex', alignItems: 'center', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', position: 'sticky', left: 0, zIndex: 5, cursor: 'pointer', gap: '6px', transition: 'all 0.2s', borderRight: '2px solid rgba(59, 130, 246, 0.1)', letterSpacing: '0.1px' }}
                  title={domain === OTHER_LABEL ? 'Tıklayarak 10 domain daha göster' : `${domain} - Gizlemek için tıklayın`}
                  onClick={() => domain === OTHER_LABEL ? setHeatmapDomainCount(prev => prev + 10) : setHiddenDomains(prev => new Set([...Array.from(prev), domain]))}
                  onMouseEnter={(e) => { e.currentTarget.style.background = 'rgba(59, 130, 246, 0.08)' }}
                  onMouseLeave={(e) => { e.currentTarget.style.background = domain === OTHER_LABEL ? 'linear-gradient(90deg, rgba(59, 130, 246, 0.1), rgba(59, 130, 246, 0.04))' : rowIdx % 2 === 0 ? 'rgba(148, 163, 184, 0.04)' : 'rgba(148, 163, 184, 0.08)' }}
                >
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{domain}</span>
                  {domain === OTHER_LABEL && <ChevronDown size={12} style={{ flexShrink: 0 }} />}
                </div>

                {paginatedTeams.map(team => {
                  const count = heatmapData.counts[team]?.[domain] || 0
                  const bd = heatmapData.breakdown[team]?.[domain] || EMPTY_BREAKDOWN
                  const showTooltipBelow = rowIdx < 2
                  return (
                    <div key={`${team}-${domain}`} style={{ height: '34px', width: '110px', textAlign: 'center', background: getHeatmapColor(count), color: getTextColor(count), borderRadius: '4px', fontSize: '11px', fontWeight: count > 0 ? '700' : '400', transition: 'all 0.2s', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative', boxShadow: count > 0 ? '0 1px 3px rgba(0,0,0,0.08)' : 'none' }}
                      className="group"
                      onMouseEnter={(e) => { if (count > 0) e.currentTarget.style.transform = 'scale(1.08)'; e.currentTarget.style.zIndex = '2' }}
                      onMouseLeave={(e) => { e.currentTarget.style.transform = 'scale(1)'; e.currentTarget.style.zIndex = '0' }}
                    >
                      {count > 0 ? count : ''}
                      {count > 0 && (
                        <div className="hidden group-hover:block" style={{ position: 'absolute', ...(showTooltipBelow ? { top: '100%', marginTop: '6px' } : { bottom: '100%', marginBottom: '6px' }), left: '50%', transform: 'translateX(-50%)', background: 'var(--surface)', border: '1px solid rgba(99, 102, 241, 0.15)', padding: '10px 12px', borderRadius: '8px', boxShadow: '0 8px 24px rgba(0, 0, 0, 0.12)', zIndex: 20, minWidth: '170px', pointerEvents: 'none' }}>
                          <div style={{ fontSize: '11px', fontWeight: '700', marginBottom: '6px', borderBottom: '1px solid var(--border)', paddingBottom: '6px', color: 'var(--text-primary)', letterSpacing: '0.2px' }}>{domain} / {team}</div>
                          {[
                            { label: 'Block', color: '#ef4444', value: bd.block },
                            { label: 'Quarantine', color: '#f59e0b', value: bd.quarantine },
                            { label: 'Authorized', color: '#3b82f6', value: bd.authorized },
                            { label: 'Released', color: '#10b981', value: bd.permit },
                          ].map(item => (
                            <div key={item.label} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '4px', alignItems: 'center' }}>
                              <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}><span style={{ width: '6px', height: '6px', borderRadius: '50%', background: item.color, display: 'inline-block' }} />{item.label}</span>
                              <span style={{ color: item.color, fontWeight: '700' }}>{item.value}</span>
                            </div>
                          ))}
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: 'var(--text-secondary)', marginTop: '6px', paddingTop: '6px', borderTop: '1px solid var(--border)', alignItems: 'center' }}>
                            <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}><span style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#8b5cf6', display: 'inline-block' }} />Avg Max Match</span>
                            <span style={{ color: '#8b5cf6', fontWeight: '700' }}>{bd.incidentCount > 0 ? (bd.maxMatchTotal / bd.incidentCount).toFixed(1) : 0}</span>
                          </div>
                        </div>
                      )}
                    </div>
                  )
                })}
              </React.Fragment>
            ))}
          </div>

          {totalTeamPages > 1 && (
            <div style={{ marginTop: '16px', paddingTop: '16px', borderTop: '1px solid var(--border)' }}>
              <Pagination currentPage={heatmapTeamPage} totalPages={totalTeamPages} totalItems={heatmapData.teams.length} pageSize={TEAMS_PER_PAGE} onPageChange={setHeatmapTeamPage} showPageInput showFirstLast showTotalItems />
            </div>
          )}
        </div>
      )}
    </div>
  )
})
