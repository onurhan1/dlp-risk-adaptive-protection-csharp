export interface Incident {
  id: number
  timestamp: string
  userEmail?: string
  policy?: string
  action?: string
  domain?: string
  department?: string
  team?: string
  fullName?: string
  maxMatches?: number
  loginName?: string
  emailAddress?: string
  violationTriggers?: string
  channel?: string
}

export interface DateRange {
  start: string
  end: string
}

export interface HeatmapBreakdown {
  block: number
  permit: number
  authorized: number
  quarantine: number
  maxMatchTotal: number
  incidentCount: number
}

export interface HeatmapData {
  teams: string[]
  domains: string[]
  counts: Record<string, Record<string, number>>
  breakdown: Record<string, Record<string, HeatmapBreakdown>>
  hasMoreDomains: boolean
}

export interface AppliedFilters {
  dateRange: DateRange
  selectedDepartments: string[]
  selectedTeams: string[]
  selectedUser: string
  selectedFullName: string
  selectedPolicy: string
  selectedDomain: string
  selectedActions: string[]
}

export interface ClassifierStats {
  name: string
  incidentCount: number
  avgMatches: number
  p25: number
  p50: number
  p70: number
  p75: number
  p90: number
  recommendations: {
    medium: { threshold: number; label: string; action: string }
    high: { threshold: number; label: string; action: string }
  }
}

export interface ExceptionStats {
  name: string
  incidentCount: number
  avgMatches: number
  p25: number
  p75: number
  p90: number
  classifiers: ClassifierStats[]
}

export interface RuleStats {
  name: string
  incidentCount: number
  avgMatches: number
  p25: number
  p75: number
  p90: number
  classifiers: ClassifierStats[]
  exceptions: ExceptionStats[]
}

export interface PolicyReport {
  name: string
  incidentCount: number
  avgMatches: number
  rules: RuleStats[]
}
