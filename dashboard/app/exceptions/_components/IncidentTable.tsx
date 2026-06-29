'use client'

import React, { useState, useMemo, useEffect, memo, useCallback } from 'react'
import { ChevronUp, ChevronDown, X, ListChecks } from 'lucide-react'
import { format, parseISO, startOfDay, endOfDay } from 'date-fns'
import Pagination from '@/components/ui/Pagination'
import GridExport from '@/components/ui/GridExport'
import { useTranslation } from '@/components/LanguageProvider'
import type { Incident } from '../_lib/types'
import { normalizeTeamName, getActionStyle } from '../_lib/utils'
import { DEFAULT_START, DEFAULT_END, ITEMS_PER_PAGE, TABLE_COLUMNS, COLUMN_LABELS, MULTISELECT_COLUMNS, STYLES, UNKNOWN_TEAM, DEFAULT_TEAM_FALLBACK } from '../_lib/constants'

interface IncidentTableProps {
  incidents: Incident[]
}

export default memo(function IncidentTable({ incidents }: IncidentTableProps) {
  const { t } = useTranslation()
  const [currentPage, setCurrentPage] = useState(1)
  const [sortColumn, setSortColumn] = useState<string | null>(null)
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')
  const [openColumnFilter, setOpenColumnFilter] = useState<string | null>(null)
  const [columnFilterSearch, setColumnFilterSearch] = useState<Record<string, string>>({})
  const [columnFilters, setColumnFilters] = useState<Record<string, string[]>>({ time: [DEFAULT_START, DEFAULT_END] })

  // Pre-compute unique column values
  const columnUniqueValues = useMemo(() => {
    const sets: Record<string, Set<string>> = {
      user: new Set(), fullName: new Set(), department: new Set(),
      team: new Set(), policy: new Set(), domain: new Set(), action: new Set()
    }
    incidents.forEach(incident => {
      const user = incident.userEmail || incident.loginName || incident.emailAddress
      if (user) sets.user.add(user)
      if (incident.fullName) sets.fullName.add(incident.fullName)
      if (incident.department) sets.department.add(incident.department)
      const team = normalizeTeamName(incident.team)
      if (team) sets.team.add(team)
      if (incident.policy) sets.policy.add(incident.policy)
      if (incident.domain) sets.domain.add(incident.domain)
      if (incident.action) sets.action.add(incident.action)
    })
    const result: Record<string, string[]> = {}
    for (const key of Object.keys(sets)) {
      result[key] = Array.from(sets[key]).sort()
    }
    return result
  }, [incidents])

  const getUniqueColumnValues = (column: string): string[] => columnUniqueValues[column] || []

  const handleSort = useCallback((column: string) => {
    setSortColumn(prev => {
      if (prev === column) {
        setSortDirection(d => d === 'asc' ? 'desc' : 'asc')
        return column
      }
      setSortDirection('asc')
      return column
    })
  }, [])

  const toggleColumnFilter = useCallback((column: string) => {
    setOpenColumnFilter(prev => {
      if (prev === column) {
        setColumnFilterSearch(s => ({ ...s, [column]: '' }))
        return null
      }
      return column
    })
  }, [])

  const toggleColumnFilterValue = useCallback((column: string, value: string) => {
    setColumnFilters(prev => {
      const current = prev[column] || []
      return { ...prev, [column]: current.includes(value) ? current.filter(v => v !== value) : [...current, value] }
    })
  }, [])

  // Close filter dropdowns on outside click
  useEffect(() => {
    const handler = (event: Event) => {
      if (!(event.target as HTMLElement).closest('[data-column-filter]')) {
        setOpenColumnFilter(null)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  // Filtered & sorted incidents
  const filteredIncidents = useMemo(() => {
    let filtered = incidents.filter(incident => {
      if (columnFilters.time && (columnFilters.time[0] || columnFilters.time[1])) {
        try {
          const incidentDate = parseISO(incident.timestamp)
          if (columnFilters.time[0] && incidentDate < startOfDay(parseISO(columnFilters.time[0]))) return false
          if (columnFilters.time[1] && incidentDate > endOfDay(parseISO(columnFilters.time[1]))) return false
        } catch { /* skip */ }
      }
      if (columnFilters.user?.length > 0) {
        if (!columnFilters.user.some(f => incident.userEmail?.toLowerCase().includes(f.toLowerCase()) || incident.loginName?.toLowerCase().includes(f.toLowerCase()) || incident.emailAddress?.toLowerCase().includes(f.toLowerCase()))) return false
      }
      if (columnFilters.fullName?.length > 0) {
        if (!columnFilters.fullName.some(f => incident.fullName?.toLowerCase().includes(f.toLowerCase()))) return false
      }
      if (columnFilters.department?.length > 0 && !columnFilters.department.includes(incident.department || '')) return false
      if (columnFilters.team?.length > 0) {
        const norm = normalizeTeamName(incident.team)
        if (!columnFilters.team.some(f => normalizeTeamName(f) === norm)) return false
      }
      if (columnFilters.policy?.length > 0) {
        if (!columnFilters.policy.some(f => incident.policy?.toLowerCase().includes(f.toLowerCase()))) return false
      }
      if (columnFilters.domain?.length > 0) {
        if (!columnFilters.domain.some(f => incident.domain?.toLowerCase().includes(f.toLowerCase()))) return false
      }
      if (columnFilters.action?.length > 0 && !columnFilters.action.includes(incident.action || '')) return false
      return true
    })

    if (sortColumn) {
      filtered = [...filtered].sort((a, b) => {
        let aVal: any, bVal: any
        switch (sortColumn) {
          case 'time': aVal = new Date(a.timestamp).getTime(); bVal = new Date(b.timestamp).getTime(); break
          case 'user': aVal = (a.userEmail || '').toLowerCase(); bVal = (b.userEmail || '').toLowerCase(); break
          case 'fullName': aVal = (a.fullName || '').toLowerCase(); bVal = (b.fullName || '').toLowerCase(); break
          case 'department': aVal = (a.department || '').toLowerCase(); bVal = (b.department || '').toLowerCase(); break
          case 'team': aVal = normalizeTeamName(a.team).toLowerCase(); bVal = normalizeTeamName(b.team).toLowerCase(); break
          case 'policy': aVal = (a.policy || '').toLowerCase(); bVal = (b.policy || '').toLowerCase(); break
          case 'domain': aVal = (a.domain || '').toLowerCase(); bVal = (b.domain || '').toLowerCase(); break
          case 'max': aVal = a.maxMatches || 0; bVal = b.maxMatches || 0; break
          case 'action': aVal = (a.action || '').toLowerCase(); bVal = (b.action || '').toLowerCase(); break
          default: return 0
        }
        if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1
        if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1
        return 0
      })
    }
    return filtered
  }, [incidents, columnFilters, sortColumn, sortDirection])

  useEffect(() => { setCurrentPage(1) }, [filteredIncidents])

  const paginatedIncidents = useMemo(() => {
    const start = (currentPage - 1) * ITEMS_PER_PAGE
    return filteredIncidents.slice(start, start + ITEMS_PER_PAGE)
  }, [filteredIncidents, currentPage])

  const totalPages = Math.ceil(filteredIncidents.length / ITEMS_PER_PAGE)

  const hasNonDefaultFilters = (Object.entries(columnFilters) as [string, string[]][]).some(([k, v]) => {
    if (k === 'time') return v[0] !== DEFAULT_START || v[1] !== DEFAULT_END
    return v && v.length > 0
  })

  const clearAllFilters = useCallback(() => {
    setColumnFilters({ time: [DEFAULT_START, DEFAULT_END] })
    setColumnFilterSearch({})
    setOpenColumnFilter(null)
  }, [])

  return (
    <div style={{ background: 'var(--surface)', borderRadius: '10px', border: '1px solid var(--border)', overflow: 'hidden', boxShadow: '0 2px 8px rgba(0,0,0,0.06)', marginBottom: '32px', borderTop: '3px solid #f59e0b' }}>
      <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div style={STYLES.iconBox('linear-gradient(135deg, #f59e0b, #ef4444)', '0 3px 10px rgba(245, 158, 11, 0.25)')}>
              <ListChecks size={16} color="#fff" />
            </div>
            <h2 style={{ fontSize: '16px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #f59e0b, #ef4444)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>{t('incidentTable.incidentsList')}</h2>
          </div>
          {hasNonDefaultFilters && (
            <button onClick={clearAllFilters} style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-secondary)', fontSize: '12px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px' }}>
              <X size={14} /> {t('exc.clearFilters')}
            </button>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
            {t('incidentTable.showing')} {filteredIncidents.length > 0 ? (currentPage - 1) * ITEMS_PER_PAGE + 1 : 0} - {Math.min(currentPage * ITEMS_PER_PAGE, filteredIncidents.length)} {t('incidentTable.of')} {filteredIncidents.length} {t('exc.incidents')}
          </span>
          <GridExport data={filteredIncidents} fileName="team-based-analysis" columns={[
            { key: 'timestamp', header: 'Time', formatter: (val) => new Date(val).toLocaleString('tr-TR') },
            { key: 'userEmail', header: 'User' }, { key: 'fullName', header: 'Manager Name' },
            { key: 'department', header: 'Department' }, { key: 'team', header: 'Team' },
            { key: 'policy', header: 'Policy' }, { key: 'domain', header: 'Domain' },
            { key: 'maxMatches', header: 'Max Matches' }, { key: 'action', header: 'Action' }
          ]} />
        </div>
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ background: 'rgba(245, 158, 11, 0.06)', borderBottom: '1px solid rgba(245, 158, 11, 0.15)' }}>
            {TABLE_COLUMNS.map(column => {
              const isSorted = sortColumn === column
              return (
                <th key={column} style={{ padding: '12px 16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: isSorted ? '#f59e0b' : 'var(--text-primary)', width: column === 'time' ? '150px' : column === 'action' ? '100px' : 'auto', borderBottom: 'none' }}>
                  <span onClick={() => handleSort(column)} style={{ cursor: 'pointer', userSelect: 'none', display: 'flex', alignItems: 'center', gap: '4px', width: '100%' }}>
                    {column === 'time' ? t('incidentTable.time') : column === 'max' ? t('incidentTable.max') : t(`heatmap.${column}`) || COLUMN_LABELS[column]}
                    {isSorted && (sortDirection === 'asc' ? <ChevronUp size={12} /> : <ChevronDown size={12} />)}
                  </span>
                </th>
              )
            })}
          </tr>
          <tr style={{ background: 'rgba(245, 158, 11, 0.04)', borderBottom: '2px solid rgba(245, 158, 11, 0.18)' }}>
            {TABLE_COLUMNS.map(column => {
              const uniqueValues = getUniqueColumnValues(column)
              const searchQuery = columnFilterSearch[column] || ''
              const filteredValues = uniqueValues.filter(v => v.toLowerCase().includes(searchQuery.toLowerCase()))
              const selectedValues = columnFilters[column] || []
              const hasActiveFilter = selectedValues.length > 0
              const isMultiSelect = MULTISELECT_COLUMNS.has(column)

              return (
                <th key={`filter-${column}`} data-column-filter style={{ padding: '0 12px 12px 12px', textAlign: 'left', position: 'relative', verticalAlign: 'top' }}>
                  {column === 'time' ? (
                    <div style={{ display: 'flex', gap: '4px' }}>
                      <input type="date" value={(columnFilters.time && columnFilters.time[0]) || ''} onChange={(e) => { const val = e.target.value; setColumnFilters(prev => ({ ...prev, time: [val, (prev.time || ['', ''])[1] || ''] })) }}
                        style={{ ...STYLES.dateInput, border: (columnFilters.time && columnFilters.time[0]) ? '1px solid #f59e0b' : '1px solid var(--border)' }} />
                      <input type="date" value={(columnFilters.time && columnFilters.time[1]) || ''} onChange={(e) => { const val = e.target.value; setColumnFilters(prev => ({ ...prev, time: [(prev.time || ['', ''])[0] || '', val] })) }}
                        style={{ ...STYLES.dateInput, border: (columnFilters.time && columnFilters.time[1]) ? '1px solid #f59e0b' : '1px solid var(--border)' }} />
                    </div>
                  ) : column !== 'max' ? (
                    isMultiSelect ? (
                      <>
                        <button onClick={(e) => { e.stopPropagation(); toggleColumnFilter(column) }} style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: hasActiveFilter ? 'rgba(59, 130, 246, 0.1)' : 'var(--surface)', color: hasActiveFilter ? '#3b82f6' : 'var(--text-secondary)', fontSize: '12px', textAlign: 'left', cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{hasActiveFilter ? `${selectedValues.length} ${t('incidentTable.selected')}` : t('incidentTable.all')}</span>
                          <ChevronDown size={12} />
                        </button>
                        {openColumnFilter === column && (
                          <div style={{ position: 'absolute', top: 'calc(100% - 8px)', left: '12px', width: 'max(100%, 180px)', background: 'var(--background)', border: '1px solid var(--border)', borderRadius: '6px', boxShadow: '0 4px 6px rgba(0,0,0,0.1)', zIndex: 1000, maxHeight: '300px', overflowY: 'auto', padding: '8px' }} onClick={(e) => e.stopPropagation()}>
                            <input type="text" placeholder={t('incidentTable.search')} value={searchQuery} onChange={(e) => setColumnFilterSearch(prev => ({ ...prev, [column]: e.target.value }))} style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', marginBottom: '8px', boxSizing: 'border-box' }} />
                            <div onClick={() => setColumnFilters(prev => ({ ...prev, [column]: selectedValues.length === filteredValues.length ? [] : [...filteredValues] }))} style={{ padding: '6px 8px', borderBottom: '1px solid var(--border)', cursor: 'pointer', fontSize: '11px', fontWeight: '600', color: 'var(--text-primary)', background: selectedValues.length === filteredValues.length ? 'var(--surface-hover)' : 'transparent', marginBottom: '4px' }}>
                              {selectedValues.length === filteredValues.length ? t('incidentTable.clearSelection') : t('incidentTable.selectAll')}
                            </div>
                            {filteredValues.map(value => (
                              <label key={value} style={{ display: 'flex', alignItems: 'center', padding: '6px 8px', cursor: 'pointer', fontSize: '12px', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)', background: selectedValues.includes(value) ? 'var(--surface-hover)' : 'transparent' }}>
                                <input type="checkbox" checked={selectedValues.includes(value)} onChange={() => toggleColumnFilterValue(column, value)} style={{ marginRight: '8px' }} />
                                <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{value}</span>
                              </label>
                            ))}
                            {filteredValues.length === 0 && <div style={{ padding: '8px', fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center' }}>{t('exc.notFound')}</div>}
                          </div>
                        )}
                      </>
                    ) : (
                      <input type="text" placeholder={t('incidentTable.search')} value={hasActiveFilter ? selectedValues[0] : ''} onChange={(e) => { const val = e.target.value; setColumnFilters(prev => ({ ...prev, [column]: val ? [val] : [] })) }}
                        style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '12px', boxSizing: 'border-box' }} />
                    )
                  ) : null}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {paginatedIncidents.length === 0 ? (
            <tr><td colSpan={9} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>{t('exc.noIncidentsForFilter')}</td></tr>
          ) : (
            paginatedIncidents.map(incident => {
              const actionStyle = getActionStyle(incident.action)
              return (
                <tr key={incident.id} style={{ borderBottom: '1px solid var(--border)', transition: 'background 0.2s' }} className="hover:bg-[var(--surface-hover)]">
                  <td style={STYLES.tableCell}>{format(new Date(incident.timestamp), 'dd MMM yyyy HH:mm:ss')}</td>
                  <td style={STYLES.tableCell}>{incident.userEmail || t('incidentTable.unknown')}</td>
                  <td style={STYLES.tableCell}>{incident.fullName || '-'}</td>
                  <td style={STYLES.tableCell}>{incident.department || '-'}</td>
                  <td style={STYLES.tableCell}>{
                    normalizeTeamName(incident.team) === UNKNOWN_TEAM ? t('incidentTable.unknown') :
                    normalizeTeamName(incident.team) === DEFAULT_TEAM_FALLBACK ? t('incidentTable.defaultTeam') :
                    normalizeTeamName(incident.team)
                  }</td>
                  <td style={STYLES.tableCell}>{incident.policy || '-'}</td>
                  <td style={STYLES.tableCell}>{incident.domain || '-'}</td>
                  <td style={STYLES.tableCell}>{incident.maxMatches || 0}</td>
                  <td style={STYLES.tableCell}>
                    <span style={{ padding: '4px 10px', borderRadius: '12px', ...actionStyle, fontSize: '12px', fontWeight: '600', display: 'inline-block' }}>{incident.action}</span>
                  </td>
                </tr>
              )
            })
          )}
        </tbody>
      </table>

      {filteredIncidents.length > 0 && (
        <div style={{ padding: '16px', borderTop: '1px solid var(--border)' }}>
          <Pagination currentPage={currentPage} totalPages={totalPages} totalItems={filteredIncidents.length} pageSize={ITEMS_PER_PAGE} onPageChange={setCurrentPage} showPageInput showFirstLast showTotalItems />
        </div>
      )}
    </div>
  )
})
