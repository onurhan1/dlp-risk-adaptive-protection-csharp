import { format, subDays } from 'date-fns'
import type { AppliedFilters, HeatmapBreakdown } from './types'
import type { CSSProperties } from 'react'

// Magic strings
export const UNKNOWN_TEAM = 'Takım Belirtilmemiş'
export const UNKNOWN_DOMAIN = 'Unknown'
export const DEFAULT_TEAM_FALLBACK = 'Hesap Araştırmaları'
export const SUBE_SUFFIX = 'Şubesi'
export const SUBE_NORMALIZED = 'Şube'
export const OTHER_LABEL = 'Diğer'

// Pagination
export const ITEMS_PER_PAGE = 10
export const TEAMS_PER_PAGE = 8
export const INITIAL_DOMAIN_COUNT = 10

// Date defaults - computed once at module level (not per render)
export const DEFAULT_START = format(subDays(new Date(), 7), 'yyyy-MM-dd')
export const DEFAULT_END = format(new Date(), 'yyyy-MM-dd')

export const DEFAULT_APPLIED_FILTERS: AppliedFilters = {
  dateRange: { start: DEFAULT_START, end: DEFAULT_END },
  selectedDepartments: [],
  selectedTeams: [],
  selectedUser: '',
  selectedFullName: '',
  selectedPolicy: '',
  selectedDomain: '',
  selectedActions: []
}

export const EMPTY_BREAKDOWN: HeatmapBreakdown = {
  block: 0, permit: 0, authorized: 0, quarantine: 0, maxMatchTotal: 0, incidentCount: 0
}

// Column definitions
export const TABLE_COLUMNS = ['time', 'user', 'fullName', 'department', 'team', 'policy', 'domain', 'max', 'action'] as const
export type TableColumn = typeof TABLE_COLUMNS[number]

export const COLUMN_LABELS: Record<string, string> = {
  time: 'Time',
  user: 'User',
  fullName: 'Manager Name',
  department: 'Department',
  team: 'Team',
  policy: 'Policy',
  domain: 'Domain',
  max: 'Max',
  action: 'Action'
}

export const MULTISELECT_COLUMNS = new Set(['department', 'team', 'action'])

// Reusable styles (extracted from inline to avoid re-creation per render)
export const STYLES = {
  filterLabel: {
    fontSize: '10px',
    fontWeight: '600',
    color: 'var(--text-secondary)',
    textTransform: 'none' as const,
    letterSpacing: '0.5px'
  } as CSSProperties,
  filterInput: {
    width: '100%',
    padding: '6px 8px',
    borderRadius: '6px',
    border: '1px solid var(--border)',
    background: 'var(--surface)',
    color: 'var(--text-primary)',
    fontSize: '12px',
    boxSizing: 'border-box' as const
  } as CSSProperties,
  tableCell: {
    padding: '16px',
    fontSize: '14px',
    color: 'var(--text-primary)'
  } as CSSProperties,
  sectionCard: (borderColor: string): CSSProperties => ({
    background: 'var(--surface)',
    borderRadius: '10px',
    border: '1px solid var(--border)',
    padding: '24px',
    boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
    borderTop: `3px solid ${borderColor}`
  }),
  iconBox: (gradient: string, shadow: string): CSSProperties => ({
    width: '32px',
    height: '32px',
    borderRadius: '8px',
    background: gradient,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    boxShadow: shadow
  }),
  gradientText: (gradient: string): CSSProperties => ({
    fontSize: '18px',
    fontWeight: '700',
    margin: 0,
    background: gradient,
    WebkitBackgroundClip: 'text',
    WebkitTextFillColor: 'transparent'
  }),
  dateInput: {
    flex: 1,
    padding: '5px 4px',
    borderRadius: '4px',
    background: 'var(--surface)',
    color: 'var(--text-primary)',
    fontSize: '11px',
    boxSizing: 'border-box' as const,
    minWidth: 0
  } as CSSProperties,
  accordionHover: {
    cursor: 'pointer',
    transition: 'all 0.2s'
  } as CSSProperties,
  expandButton: (size: number): CSSProperties => ({
    width: `${size}px`,
    height: `${size}px`,
    borderRadius: '50%',
    background: '#3b82f6',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    color: 'white',
    fontWeight: '600',
    flexShrink: 0
  }),
  statLabel: (fontSize = '10px'): CSSProperties => ({
    color: 'var(--text-secondary)',
    fontSize,
    marginBottom: '2px'
  }),
  statValue: (color = 'var(--text-primary)'): CSSProperties => ({
    color,
    fontWeight: '600'
  }),
} as const
