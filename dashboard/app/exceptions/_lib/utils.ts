import { parseISO, startOfDay, endOfDay, isWithinInterval } from 'date-fns'
import type { Incident, DateRange } from './types'
import { UNKNOWN_TEAM, UNKNOWN_DOMAIN, SUBE_SUFFIX, SUBE_SUFFIX_ALT, SUBE_TICARI, SUBE_KURUMSAL, SUBE_NORMAL } from './constants'

// Memoize normalizeTeamName results for performance
const teamNameCache = new Map<string, string>()

export function normalizeTeamName(team: string | undefined | null): string {
  if (!team) return UNKNOWN_TEAM
  const cached = teamNameCache.get(team)
  if (cached) return cached
  const trimmed = team.trim()
  let result: string
  if (trimmed === 'Unknown' || trimmed === '') {
    result = UNKNOWN_TEAM
  } else if (trimmed.endsWith(SUBE_SUFFIX) || trimmed.endsWith(SUBE_SUFFIX_ALT)) {
    // Classify branch type based on keywords in the name
    const lower = trimmed.toLowerCase()
    if (lower.includes('ticari')) {
      result = SUBE_TICARI
    } else if (lower.includes('kurumsal')) {
      result = SUBE_KURUMSAL
    } else {
      result = SUBE_NORMAL
    }
  } else {
    result = trimmed
  }
  teamNameCache.set(team, result)
  return result
}

export function extractDomain(dest: string): string {
  if (!dest) return UNKNOWN_DOMAIN
  if (dest.includes(';')) {
    const domains: string[] = []
    dest.split(';').forEach(part => {
      const email = part.trim()
      if (email.includes('@')) {
        const d = email.split('@')[1]?.trim()
        if (d && !domains.includes(d)) domains.push(d)
      }
    })
    return domains.length > 0 ? domains.join(', ') : UNKNOWN_DOMAIN
  }
  if (dest.includes('@')) {
    return dest.split('@')[1]?.trim() || dest
  }
  return dest || UNKNOWN_DOMAIN
}

export function mapIncidentData(item: Record<string, any>): Incident {
  const dest = item.destination || ''
  const dept = item.department || item.user_department
  // Apply same branch classification to department
  let normalizedDept = dept
  if (dept) {
    const deptTrimmed = dept.trim()
    if (deptTrimmed.endsWith(SUBE_SUFFIX) || deptTrimmed.endsWith(SUBE_SUFFIX_ALT)) {
      const deptLower = deptTrimmed.toLowerCase()
      if (deptLower.includes('ticari')) normalizedDept = SUBE_TICARI
      else if (deptLower.includes('kurumsal')) normalizedDept = SUBE_KURUMSAL
      else normalizedDept = SUBE_NORMAL
    }
  }
  return {
    id: item.id,
    timestamp: item.timestamp,
    userEmail: item.user_email || item.userEmail,
    policy: item.policy,
    action: item.action || 'Permit',
    domain: extractDomain(dest) || UNKNOWN_DOMAIN,
    department: normalizedDept,
    team: item.team,
    fullName: item.full_name || item.fullName,
    maxMatches: item.max_matches || item.maxMatches || 0,
    loginName: item.login_name || item.loginName,
    emailAddress: item.email_address || item.emailAddress,
    violationTriggers: item.violationTriggers || item.violation_triggers || item.ViolationTriggers || undefined,
    channel: item.channel
  }
}

export function calculatePercentile(arr: number[], percentile: number): number {
  if (arr.length === 0) return 0
  const sorted = [...arr].sort((a, b) => a - b)
  const index = Math.ceil((percentile / 100) * sorted.length) - 1
  return sorted[Math.max(0, index)]
}

export function parseViolationTriggers(violationTriggers: string | undefined): any[] {
  if (!violationTriggers) return []
  try {
    return typeof violationTriggers === 'string'
      ? JSON.parse(violationTriggers)
      : violationTriggers
  } catch {
    return []
  }
}

export function getHeatmapColor(count: number): string {
  if (count === 0) return 'rgba(148, 163, 184, 0.06)'
  if (count > 100) return 'linear-gradient(135deg, hsl(220, 85%, 22%), hsl(220, 90%, 18%))'
  if (count > 75) return 'linear-gradient(135deg, hsl(220, 85%, 30%), hsl(220, 90%, 25%))'
  if (count > 50) return 'linear-gradient(135deg, hsl(220, 85%, 37%), hsl(220, 90%, 32%))'
  if (count > 30) return 'linear-gradient(135deg, hsl(220, 85%, 44%), hsl(220, 90%, 38%))'
  if (count > 20) return 'linear-gradient(135deg, hsl(220, 80%, 52%), hsl(220, 85%, 46%))'
  if (count > 15) return 'linear-gradient(135deg, hsl(215, 75%, 60%), hsl(220, 80%, 54%))'
  if (count > 10) return 'linear-gradient(135deg, hsl(215, 70%, 68%), hsl(220, 75%, 62%))'
  if (count > 5) return 'linear-gradient(135deg, hsl(215, 65%, 76%), hsl(220, 70%, 72%))'
  return 'linear-gradient(135deg, hsl(215, 60%, 84%), hsl(220, 65%, 80%))'
}

export function getTextColor(count: number): string {
  if (count === 0) return 'var(--text-muted)'
  return count > 20 ? '#ffffff' : '#1e293b'
}

export function toggleSetItem<T>(prev: Set<T>, item: T): Set<T> {
  const next = new Set(prev)
  if (next.has(item)) next.delete(item)
  else next.add(item)
  return next
}

export function extractPoliciesFromIncidents(incidents: Incident[]): string[] {
  const policySet = new Set<string>()
  incidents.forEach(incident => {
    const triggers = parseViolationTriggers(incident.violationTriggers)
    if (triggers.length > 0) {
      triggers.forEach((t: any) => {
        const name = t.PolicyName || t.policy_name || incident.policy
        if (name) policySet.add(name)
      })
    } else if (incident.policy) {
      policySet.add(incident.policy)
    }
  })
  return Array.from(policySet).sort()
}

export function getActionStyle(action: string | undefined): { background: string; color: string } {
  const a = action?.toLowerCase() || ''
  if (a.includes('block')) return { background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444' }
  if (a.includes('quarantine')) return { background: 'rgba(245, 158, 11, 0.1)', color: '#f59e0b' }
  if (a.includes('authorized') || a.includes('allow')) return { background: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6' }
  return { background: 'rgba(16, 185, 129, 0.1)', color: '#10b981' }
}

export function isInDateRange(timestamp: string, range: DateRange): boolean {
  if (!range.start || !range.end) return true
  try {
    const date = parseISO(timestamp)
    const start = startOfDay(parseISO(range.start))
    const end = endOfDay(parseISO(range.end))
    if (isNaN(start.getTime()) || isNaN(end.getTime())) return true
    return isWithinInterval(date, { start, end })
  } catch {
    return true
  }
}

export function matchesUserSearch(incident: Incident, search: string): boolean {
  if (!search) return true
  const s = search.toLowerCase()
  return !!(
    incident.userEmail?.toLowerCase().includes(s) ||
    incident.loginName?.toLowerCase().includes(s) ||
    incident.emailAddress?.toLowerCase().includes(s)
  )
}
