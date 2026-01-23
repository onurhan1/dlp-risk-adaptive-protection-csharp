'use client'

import React, { useState, useEffect, useMemo, ChangeEvent, MouseEvent } from 'react'
import apiClient from '@/lib/axios'
import { format, isWithinInterval, parseISO, startOfDay, endOfDay } from 'date-fns'
import DomainFeaturesManager from '@/components/DomainFeaturesManager'

interface Incident {
  id: number
  timestamp: string
  userEmail?: string
  policy?: string
  action?: string
  severity: string
  destination?: string
  domain?: string
  team?: string
}

interface DomainFeature {
  domain: string
  gizlilikSozlesmesi: string
  egitim: string
  noterlik: string
  denetim: string
  banka: string
  hukukFirmasi: string
  istirak: string
}

export default function AnalyticsPage() {
  const [incidents, setIncidents] = useState<Incident[]>([])
  const [loading, setLoading] = useState(true)
  const [currentPage, setCurrentPage] = useState(1)
  const itemsPerPage = 10

  // Filter States
  const [dateRange, setDateRange] = useState({ start: '', end: '' })
  const [selectedTeam, setSelectedTeam] = useState('')
  const [selectedUser, setSelectedUser] = useState('')
  const [selectedPolicy, setSelectedPolicy] = useState('')
  const [selectedDomain, setSelectedDomain] = useState('')
  const [selectedAction, setSelectedAction] = useState('')

  // Domain Features States
  const [showDomainFeatures, setShowDomainFeatures] = useState(false)
  const [domainFeatures, setDomainFeatures] = useState<DomainFeature[]>([])
  const [domainFilter, setDomainFilter] = useState('')
  const [gizlilikFilter, setGizlilikFilter] = useState('')
  const [istirakFilter, setIstirakFilter] = useState('')
  const [egitimFilter, setEgitimFilter] = useState('')
  const [noterlikFilter, setNoterlikFilter] = useState('')
  const [hukukFirmasiFilter, setHukukFirmasiFilter] = useState('')
  const [denetimFilter, setDenetimFilter] = useState('')
  const [bankaFilter, setBankaFilter] = useState('')

  useEffect(() => {
    fetchIncidents()
  }, [])

  const fetchIncidents = async () => {
    setLoading(true)
    try {
      const response = await apiClient.get('/api/incidents', {
        params: {
          limit: 10000, // Fetch significantly more records to approximate "all" for analytics
          order_by: 'timestamp_desc'
        }
      })

      const data = Array.isArray(response.data) ? response.data : []

      const mappedIncidents: Incident[] = data.map((item: any) => {
        const dest = item.destination || ''

        // Birden fazla email adresi varsa (noktalı virgülle ayrılmış), her birinden domain çıkar
        let domain = 'Unknown'
        if (dest.includes(';')) {
          // Noktalı virgülle ayrılmış email adresleri
          const emails = dest.split(';').map((e: string) => e.trim()).filter((e: string) => e)
          const domains: string[] = []
          emails.forEach((email: string) => {
            if (email.includes('@')) {
              const parts = email.split('@')
              if (parts.length > 1) {
                const extractedDomain = parts[1].trim()
                if (extractedDomain && !domains.includes(extractedDomain)) {
                  domains.push(extractedDomain)
                }
              }
            }
          })
          domain = domains.length > 0 ? domains.join(', ') : 'Unknown'
        } else if (dest.includes('@')) {
          // Tek email adresi
          const parts = dest.split('@')
          domain = parts.length > 1 ? parts[1].trim() : dest
        } else {
          domain = dest
        }

        return {
          id: item.id,
          timestamp: item.timestamp,
          userEmail: item.userEmail,
          policy: item.policy,
          action: item.action || 'Permit',
          severity: item.severity >= 4 ? 'High' : item.severity >= 3 ? 'Medium' : 'Low',
          destination: dest,
          domain: domain || 'Unknown',
          team: item.department || item.user_department // Try to get department info
        }
      })

      setIncidents(mappedIncidents)
    } catch (error) {
      console.error('Error fetching incidents:', error)
    } finally {
      setLoading(false)
    }
  }

  const formatTeam = (team?: string) => {
    if (!team) return 'Unknown'
    if (team.includes('Şubesi')) return 'Şube'
    return team
  }

  // Filter Logic
  const filteredIncidents = useMemo(() => {
    return incidents.filter(incident => {
      // Date Filter
      if (dateRange.start && dateRange.end) {
        const incidentDate = parseISO(incident.timestamp)
        const start = startOfDay(parseISO(dateRange.start))
        const end = endOfDay(parseISO(dateRange.end))
        if (!isWithinInterval(incidentDate, { start, end })) return false
      }

      // Team Filter
      if (selectedTeam && formatTeam(incident.team) !== selectedTeam) return false

      // User Filter (Text search)
      if (selectedUser && !incident.userEmail?.toLowerCase().includes(selectedUser.toLowerCase())) return false

      // Policy Filter (Text search)
      if (selectedPolicy && !incident.policy?.toLowerCase().includes(selectedPolicy.toLowerCase())) return false

      // Domain Filter (Text search)
      if (selectedDomain && !incident.domain?.toLowerCase().includes(selectedDomain.toLowerCase())) return false

      // Action Filter
      if (selectedAction && incident.action !== selectedAction) return false

      return true
    })
  }, [incidents, dateRange, selectedTeam, selectedUser, selectedPolicy, selectedDomain, selectedAction])

  // Reset page when filters change
  useEffect(() => {
    setCurrentPage(1)
  }, [filteredIncidents])

  // Pagination Logic
  const paginatedIncidents = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage
    return filteredIncidents.slice(startIndex, startIndex + itemsPerPage)
  }, [filteredIncidents, currentPage])

  const totalPages = Math.ceil(filteredIncidents.length / itemsPerPage)

  // Get Unique Values for Selects
  const uniqueTeams = useMemo(() => Array.from(new Set(incidents.map(i => formatTeam(i.team)))).sort(), [incidents])
  const uniqueActions = useMemo(() => Array.from(new Set(incidents.map(i => i.action || 'Permit'))).sort(), [incidents])

  // Heatmap Data Calculation (using filtered incidents - ALL matching records, not just current page)
  const heatmapData = useMemo(() => {
    const teams = new Set<string>()
    const domains = new Set<string>()
    const counts: Record<string, Record<string, number>> = {}
    const domainTotalCounts: Record<string, number> = {}

    filteredIncidents.forEach(incident => {
      const team = formatTeam(incident.team)
      const domain = incident.domain || 'Unknown'

      teams.add(team)
      domains.add(domain)

      if (!counts[team]) counts[team] = {}
      counts[team][domain] = (counts[team][domain] || 0) + 1

      domainTotalCounts[domain] = (domainTotalCounts[domain] || 0) + 1
    })

    const sortedTeams = Array.from(teams).sort()
    // Sort domains by total count descending
    const sortedDomains = Array.from(domains).sort((a, b) => (domainTotalCounts[b] || 0) - (domainTotalCounts[a] || 0)).slice(0, 10)

    let maxCount = 0
    sortedTeams.forEach(t => {
      sortedDomains.forEach(d => {
        if (counts[t]?.[d] > maxCount) maxCount = counts[t][d]
      })
    })

    return { teams: sortedTeams, domains: sortedDomains, counts, maxCount }
  }, [filteredIncidents])

  const getHeatmapColor = (count: number, max: number) => {
    if (count === 0) return 'transparent'
    const intensity = max > 0 ? count / max : 0
    const lightness = 95 - (intensity * 55)
    return `hsl(200, 80%, ${lightness}%)`
  }

  const getTextColor = (count: number, max: number) => {
    if (count === 0) return 'var(--text-secondary)'
    const intensity = max > 0 ? count / max : 0
    return intensity > 0.5 ? 'white' : 'var(--text-primary)'
  }

  const getYesNoColor = (value: string) => {
    const normalized = value?.toLowerCase().trim() || ''
    const isYes = normalized === 'evet' || normalized === 'yes' || normalized === '1' || normalized === 'var'
    return {
      background: isYes ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
      color: isYes ? '#10b981' : '#ef4444'
    }
  }

  const handleCSVUpload = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = (e) => {
      const text = e.target?.result as string
      const lines = text.split('\n').filter(line => line.trim())

      if (lines.length < 2) {
        alert('CSV dosyası en az bir başlık satırı ve bir veri satırı içermelidir.')
        return
      }

      // İlk satır başlık satırı, onu atla veya kontrol et
      const headers = lines[0].split(',').map(h => h.trim().toLowerCase())
      const dataLines = lines.slice(1)

      const features: DomainFeature[] = dataLines.map((line, index) => {
        const values = line.split(',').map(v => v.trim())

        // CSV formatı: domain, gizlilik sözleşmesi, eğitim, noterlik, denetim, banka, hukuk firması
        return {
          domain: values[0] || '',
          gizlilikSozlesmesi: values[1] || '',
          egitim: values[2] || '',
          noterlik: values[3] || '',
          denetim: values[4] || '',
          banka: values[5] || '',
          hukukFirmasi: values[6] || '',
          istirak: values[7] || ''
        }
      }).filter(f => f.domain) // Boş domain'leri filtrele

      setDomainFeatures(features)
      setShowDomainFeatures(true)
    }

    reader.readAsText(file, 'UTF-8')
  }

  // Filtered Domain Features
  const filteredDomainFeatures = useMemo(() => {
    return domainFeatures.filter(feature => {
      if (domainFilter && !feature.domain.toLowerCase().includes(domainFilter.toLowerCase())) return false
      if (gizlilikFilter && !feature.gizlilikSozlesmesi.toLowerCase().includes(gizlilikFilter.toLowerCase())) return false
      if (istirakFilter && !feature.istirak.toLowerCase().includes(istirakFilter.toLowerCase())) return false
      if (egitimFilter && !feature.egitim.toLowerCase().includes(egitimFilter.toLowerCase())) return false
      if (noterlikFilter && !feature.noterlik.toLowerCase().includes(noterlikFilter.toLowerCase())) return false
      if (hukukFirmasiFilter && !feature.hukukFirmasi.toLowerCase().includes(hukukFirmasiFilter.toLowerCase())) return false
      if (denetimFilter && !feature.denetim.toLowerCase().includes(denetimFilter.toLowerCase())) return false
      if (bankaFilter && !feature.banka.toLowerCase().includes(bankaFilter.toLowerCase())) return false
      return true
    })
  }, [domainFeatures, domainFilter, gizlilikFilter, istirakFilter, egitimFilter, noterlikFilter, hukukFirmasiFilter, denetimFilter, bankaFilter])

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <h1 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Analytics Report</h1>
          <button
            onClick={() => setShowDomainFeatures(!showDomainFeatures)}
            style={{
              padding: '10px 20px',
              borderRadius: '6px',
              border: '1px solid var(--border)',
              background: showDomainFeatures ? 'var(--surface-hover)' : 'var(--surface)',
              color: 'var(--text-primary)',
              fontSize: '14px',
              fontWeight: '500',
              cursor: 'pointer',
              transition: 'all 0.2s'
            }}
          >
            Domain Features
          </button>
        </div>


        {/* Domain Features - New API-based Manager */}
        {showDomainFeatures && (
          <div style={{ marginBottom: '24px' }}>
            <DomainFeaturesManager onClose={() => setShowDomainFeatures(false)} />
          </div>
        )}


        {/* Filters Section */}
        {
          !showDomainFeatures && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px', marginBottom: '24px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
              <h2 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '16px' }}>Filters</h2>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>

                {/* Date Range */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Date Range</label>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <input
                      type="date"
                      value={dateRange.start}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => setDateRange(prev => ({ ...prev, start: e.target.value }))}
                      style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                    />
                    <input
                      type="date"
                      value={dateRange.end}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => setDateRange(prev => ({ ...prev, end: e.target.value }))}
                      style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                    />
                  </div>
                </div>

                {/* Team Select */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Team</label>
                  <select
                    value={selectedTeam}
                    onChange={(e: ChangeEvent<HTMLSelectElement>) => setSelectedTeam(e.target.value)}
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                  >
                    <option value="">All Teams</option>
                    {uniqueTeams.map(team => (
                      <option key={team} value={team}>{team}</option>
                    ))}
                  </select>
                </div>

                {/* User Search */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>User</label>
                  <input
                    type="text"
                    placeholder="Search user..."
                    value={selectedUser}
                    onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedUser(e.target.value)}
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                  />
                </div>

                {/* Policy Search */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Policy</label>
                  <input
                    type="text"
                    placeholder="Search policy..."
                    value={selectedPolicy}
                    onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedPolicy(e.target.value)}
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                  />
                </div>

                {/* Domain Search */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Domain</label>
                  <input
                    type="text"
                    placeholder="Search domain..."
                    value={selectedDomain}
                    onChange={(e: ChangeEvent<HTMLInputElement>) => setSelectedDomain(e.target.value)}
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                  />
                </div>

                {/* Action Select */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)' }}>Action</label>
                  <select
                    value={selectedAction}
                    onChange={(e: ChangeEvent<HTMLSelectElement>) => setSelectedAction(e.target.value)}
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '13px' }}
                  >
                    <option value="">All Actions</option>
                    {uniqueActions.map(action => (
                      <option key={action} value={action}>{action}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Reset Button */}
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
                <button
                  onClick={() => {
                    setDateRange({ start: '', end: '' })
                    setSelectedTeam('')
                    setSelectedUser('')
                    setSelectedPolicy('')
                    setSelectedDomain('')
                    setSelectedAction('')
                  }}
                  style={{
                    padding: '8px 16px',
                    borderRadius: '4px',
                    border: '1px solid var(--border)',
                    background: 'var(--surface-hover)',
                    color: 'var(--text-primary)',
                    fontSize: '13px',
                    cursor: 'pointer',
                    fontWeight: '500'
                  }}
                >
                  Reset Filters
                </button>
              </div>
            </div>
          )
        }

        {/* Incidents Table */}
        {
          !showDomainFeatures && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', marginBottom: '32px' }}>
              <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ fontSize: '16px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Incidents List</h2>
                <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                  Showing {filteredIncidents.length > 0 ? (currentPage - 1) * itemsPerPage + 1 : 0} - {Math.min(currentPage * itemsPerPage, filteredIncidents.length)} of {filteredIncidents.length} incidents
                </span>
              </div>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ background: 'var(--background-secondary)', borderBottom: '1px solid var(--border)' }}>
                    <th style={{ padding: '16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)', width: '180px' }}>Time</th>
                    <th style={{ padding: '16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>User</th>
                    <th style={{ padding: '16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Team</th>
                    <th style={{ padding: '16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Policy</th>
                    <th style={{ padding: '16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Domain</th>
                    <th style={{ padding: '16px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)', width: '120px' }}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={6} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>Loading incidents...</td>
                    </tr>
                  ) : paginatedIncidents.length === 0 ? (
                    <tr>
                      <td colSpan={6} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>No incidents found matching filters</td>
                    </tr>
                  ) : (
                    paginatedIncidents.map((incident) => (
                      <tr key={incident.id} style={{ borderBottom: '1px solid var(--border)', transition: 'background 0.2s' }} className="hover:bg-[var(--surface-hover)]">
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {format(new Date(incident.timestamp), 'dd MMM yyyy HH:mm:ss')}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.userEmail || 'Unknown'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {formatTeam(incident.team)}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.policy || '-'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          {incident.domain || '-'}
                        </td>
                        <td style={{ padding: '16px', fontSize: '14px', color: 'var(--text-primary)' }}>
                          <span style={{
                            padding: '4px 10px',
                            borderRadius: '12px',
                            background: incident.action === 'Block' ? 'rgba(239, 68, 68, 0.1)' : 'rgba(16, 185, 129, 0.1)',
                            color: incident.action === 'Block' ? '#ef4444' : '#10b981',
                            fontSize: '12px',
                            fontWeight: '600',
                            display: 'inline-block'
                          }}>
                            {incident.action}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>

              {/* Pagination Controls */}
              {!loading && filteredIncidents.length > 0 && (
                <div style={{ padding: '16px', borderTop: '1px solid var(--border)', display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: '12px' }}>
                  <button
                    onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                    disabled={currentPage === 1}
                    style={{
                      padding: '8px 12px',
                      borderRadius: '4px',
                      border: '1px solid var(--border)',
                      background: currentPage === 1 ? 'var(--background-secondary)' : 'var(--surface)',
                      color: currentPage === 1 ? 'var(--text-secondary)' : 'var(--text-primary)',
                      cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
                      fontSize: '13px'
                    }}
                  >
                    Previous
                  </button>
                  <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                    Page {currentPage} of {totalPages}
                  </span>
                  <button
                    onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                    disabled={currentPage === totalPages}
                    style={{
                      padding: '8px 12px',
                      borderRadius: '4px',
                      border: '1px solid var(--border)',
                      background: currentPage === totalPages ? 'var(--background-secondary)' : 'var(--surface)',
                      color: currentPage === totalPages ? 'var(--text-secondary)' : 'var(--text-primary)',
                      cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
                      fontSize: '13px'
                    }}
                  >
                    Next
                  </button>
                </div>
              )}
            </div>
          )
        }

        {/* Heatmap Section */}
        {
          !showDomainFeatures && !loading && filteredIncidents.length > 0 && (
            <div style={{ background: 'var(--surface)', borderRadius: '8px', border: '1px solid var(--border)', padding: '24px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '20px' }}>Domain vs Team Heatmap</h2>

              <div style={{ overflowX: 'auto' }}>
                <div style={{
                  display: 'grid',
                  gridTemplateColumns: `200px repeat(${heatmapData.teams.length}, 40px)`,
                  gap: '2px',
                  minWidth: 'max-content'
                }}>
                  {/* Header Row */}
                  <div style={{ padding: '8px', fontWeight: '600', color: 'var(--text-secondary)', fontSize: '12px' }}>Domain \ Team</div>
                  {heatmapData.teams.map(team => (
                    <div key={team} style={{
                      padding: '8px 2px',
                      fontWeight: '600',
                      color: 'var(--text-secondary)',
                      fontSize: '10px',
                      textAlign: 'center',
                      background: 'var(--background-secondary)',
                      borderRadius: '2px',
                      writingMode: 'vertical-rl',
                      transform: 'rotate(180deg)',
                      height: '100px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center'
                    }}>
                      {team}
                    </div>
                  ))}

                  {/* Data Rows */}
                  {heatmapData.domains.map(domain => (
                    <React.Fragment key={domain}>
                      {/* Row Header */}
                      <div key={`row-${domain}`} style={{
                        padding: '8px',
                        fontWeight: '600',
                        color: 'var(--text-primary)',
                        fontSize: '12px',
                        background: 'var(--background-secondary)',
                        borderRadius: '2px',
                        display: 'flex',
                        alignItems: 'center',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis'
                      }} title={domain}>
                        {domain}
                      </div>

                      {/* Cells */}
                      {heatmapData.teams.map(team => {
                        const count = heatmapData.counts[team]?.[domain] || 0
                        return (
                          <div key={`${team}-${domain}`} style={{
                            height: '30px',
                            textAlign: 'center',
                            background: getHeatmapColor(count, heatmapData.maxCount),
                            color: getTextColor(count, heatmapData.maxCount),
                            borderRadius: '2px',
                            fontSize: '10px',
                            fontWeight: count > 0 ? '600' : '400',
                            transition: 'transform 0.2s',
                            cursor: 'default',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center'
                          }}
                            title={`${domain} -> ${team}: ${count} incidents`}
                            onMouseEnter={(e: MouseEvent<HTMLDivElement>) => {
                              if (count > 0) e.currentTarget.style.transform = 'scale(1.2)'
                            }}
                            onMouseLeave={(e: MouseEvent<HTMLDivElement>) => {
                              if (count > 0) e.currentTarget.style.transform = 'scale(1)'
                            }}
                          >
                            {count > 0 ? count : ''}
                          </div>
                        )
                      })}
                    </React.Fragment>
                  ))}
                </div>
              </div>
            </div>
          )
        }
      </div >
    </div >
  )
}