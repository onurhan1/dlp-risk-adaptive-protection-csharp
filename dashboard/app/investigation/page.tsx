'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format } from 'date-fns'
import InvestigationUsersList from '@/components/InvestigationUsersList'
import InvestigationTimeline from '@/components/InvestigationTimeline'
import InvestigationAlertDetails from '@/components/InvestigationAlertDetails'

import { API_URL } from '@/lib/api-config'

interface TimelineEvent {
  id: number
  timestamp: string
  alert_type: string
  severity: string
  description: string
  tags: string[]
  channel?: string
  action?: string
  destination?: string
  classification?: string[]
  matched_rules?: string[]
  files?: Array<{
    name: string
    size: string
    protected: boolean
    classification: string[]
  }>
  source_application?: string
  email_subject?: string
  recipients?: string
  iob_number?: string
}

export default function InvestigationPage() {
  const [selectedUser, setSelectedUser] = useState<string>()
  const [selectedUserRiskScore, setSelectedUserRiskScore] = useState<number | null>(null)
  const [selectedEvent, setSelectedEvent] = useState<TimelineEvent>()
  const [activeTab, setActiveTab] = useState<'users' | 'alerts'>('users')
  const [searchQuery, setSearchQuery] = useState('')
  const [filterRisk, setFilterRisk] = useState<string>('all')
  const [filterClassification, setFilterClassification] = useState<string>('all')

  const handleEventSelect = (event: TimelineEvent) => {
    // Enrich event with additional details from API or defaults
    const enrichedEvent: TimelineEvent = {
      ...event,
      destination: event.channel === 'Email' ? 'gmail.com' : event.destination,
      classification: event.tags.includes('Data exfiltration')
        ? ['PCI', 'CCN', 'PII']
        : event.classification || [],
      matched_rules: event.matched_rules || [`NEO IoB-502 ${event.description}`],
      source_application: event.channel === 'Email' ? 'outlook.exe' : event.source_application,
      email_subject: event.channel === 'Email' ? 'backup customer list' : event.email_subject,
      recipients: event.channel === 'Email' ? 'fabianoCese@gmail.com' : event.recipients,
      iob_number: event.iob_number || '904',
      files: event.files || [
        {
          name: 'Top 100.pdf',
          size: '12MB',
          protected: true,
          classification: []
        },
        {
          name: 'Customer list.csv',
          size: '1MB',
          protected: true,
          classification: ['CCN', 'PII']
        },
        {
          name: 'QBR 0122.pptx',
          size: '5MB',
          protected: true,
          classification: ['PCI', 'CCN']
        }
      ]
    }
    setSelectedEvent(enrichedEvent)
  }

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)' }}>
      {/* Header */}
      <div style={{ background: 'var(--surface)', borderBottom: '1px solid var(--border)', padding: '16px 24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <h1 style={{ fontSize: '24px', fontWeight: '600', color: 'var(--text-primary)' }}>Investigation</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', border: '1px solid var(--border)', borderRadius: '8px', padding: '6px 12px', background: 'var(--surface)' }}>
              <button
                onClick={() => setActiveTab('users')}
                style={{
                  padding: '6px 16px',
                  borderRadius: '6px',
                  fontSize: '14px',
                  fontWeight: '500',
                  transition: 'all 0.2s',
                  background: activeTab === 'users' ? 'var(--primary)' : 'transparent',
                  color: activeTab === 'users' ? 'white' : 'var(--text-secondary)',
                  border: 'none',
                  cursor: 'pointer'
                }}
                onMouseEnter={(e) => {
                  if (activeTab !== 'users') {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }
                }}
                onMouseLeave={(e) => {
                  if (activeTab !== 'users') {
                    e.currentTarget.style.color = 'var(--text-secondary)'
                  }
                }}
              >
                Users
              </button>
              <button
                onClick={() => setActiveTab('alerts')}
                style={{
                  padding: '6px 16px',
                  borderRadius: '6px',
                  fontSize: '14px',
                  fontWeight: '500',
                  transition: 'all 0.2s',
                  background: activeTab === 'alerts' ? 'var(--primary)' : 'transparent',
                  color: activeTab === 'alerts' ? 'white' : 'var(--text-secondary)',
                  border: 'none',
                  cursor: 'pointer'
                }}
                onMouseEnter={(e) => {
                  if (activeTab !== 'alerts') {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }
                }}
                onMouseLeave={(e) => {
                  if (activeTab !== 'alerts') {
                    e.currentTarget.style.color = 'var(--text-secondary)'
                  }
                }}
              >
                Alerts
              </button>
            </div>
            <div style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
              {format(new Date(), 'HH:mm dd-MMM-yyyy')}
            </div>
          </div>
        </div>
      </div>

      {/* Main Content - Three Column Layout */}
      <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr 400px', height: 'calc(100vh - 73px)' }}>
        {/* Left Panel - Users List */}
        <div style={{ background: 'var(--surface)', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)' }}>
            <h2 style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-secondary)', marginBottom: '12px' }}>Investigation</h2>
            <input
              type="text"
              placeholder="Search users..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 12px',
                border: '1px solid var(--border)',
                borderRadius: '6px',
                fontSize: '14px',
                background: 'var(--background)',
                color: 'var(--text-primary)',
                outline: 'none'
              }}
              onFocus={(e) => {
                e.target.style.borderColor = 'var(--primary)'
                e.target.style.boxShadow = '0 0 0 2px rgba(0, 168, 232, 0.2)'
              }}
              onBlur={(e) => {
                e.target.style.borderColor = 'var(--border)'
                e.target.style.boxShadow = 'none'
              }}
            />
            <div style={{ marginTop: '12px' }}>
              <select
                value={filterRisk}
                onChange={(e) => setFilterRisk(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  border: '1px solid var(--border)',
                  borderRadius: '6px',
                  fontSize: '14px',
                  background: 'var(--background)',
                  color: 'var(--text-primary)',
                  outline: 'none'
                }}
                onFocus={(e) => {
                  e.currentTarget.style.borderColor = 'var(--primary)'
                  e.currentTarget.style.boxShadow = '0 0 0 2px rgba(0, 168, 232, 0.2)'
                }}
                onBlur={(e) => {
                  e.currentTarget.style.borderColor = 'var(--border)'
                  e.currentTarget.style.boxShadow = 'none'
                }}
              >
                <option value="all">All Risk Levels</option>
                <option value="critical">Critical (80+)</option>
                <option value="high">High (50-79)</option>
                <option value="medium">Medium (30-49)</option>
                <option value="low">Low (0-29)</option>
              </select>
            </div>
          </div>
          
          <InvestigationUsersList
            onUserSelect={(email, riskScore) => {
              setSelectedUser(email)
              setSelectedUserRiskScore(riskScore)
            }}
            selectedUser={selectedUser}
            searchQuery={searchQuery}
            filterRisk={filterRisk}
          />
        </div>

        {/* Center Panel - Timeline */}
        <div style={{ background: 'var(--surface)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)' }}>Timeline</h2>
              {selectedUser && (
                <button
                  onClick={() => setSelectedUser(undefined)}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: 'var(--text-muted)',
                    fontSize: '20px',
                    lineHeight: '1',
                    cursor: 'pointer',
                    padding: '4px'
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.color = 'var(--text-primary)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.color = 'var(--text-muted)'
                  }}
                >
                  ×
                </button>
              )}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <button
                style={{
                  padding: '8px',
                  borderRadius: '6px',
                  background: 'transparent',
                  border: 'none',
                  color: 'var(--text-secondary)',
                  cursor: 'pointer',
                  transition: 'all 0.2s'
                }}
                title="Filter"
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'var(--surface-hover)'
                  e.currentTarget.style.color = 'var(--text-primary)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'transparent'
                  e.currentTarget.style.color = 'var(--text-secondary)'
                }}
              >
                ⚡
              </button>
              <button
                style={{
                  padding: '8px',
                  borderRadius: '6px',
                  background: 'transparent',
                  border: 'none',
                  color: 'var(--text-secondary)',
                  cursor: 'pointer',
                  transition: 'all 0.2s'
                }}
                title="Sort"
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'var(--surface-hover)'
                  e.currentTarget.style.color = 'var(--text-primary)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'transparent'
                  e.currentTarget.style.color = 'var(--text-secondary)'
                }}
              >
                ⇅
              </button>
            </div>
          </div>

          <InvestigationTimeline
            userEmail={selectedUser}
            userRiskScore={selectedUserRiskScore}
            onEventSelect={handleEventSelect}
            selectedEventId={selectedEvent?.id}
          />
        </div>

        {/* Right Panel - Alert Details */}
        <div style={{ background: 'var(--surface)', borderLeft: '1px solid var(--border)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: '600', color: 'var(--text-primary)' }}>Alert details</h2>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                {selectedEvent && (
                  <>
                    <span style={{
                      padding: '4px 8px',
                      borderRadius: '4px',
                      fontSize: '12px',
                      fontWeight: '600',
                      background: selectedEvent.severity === 'High' 
                        ? 'rgba(217, 83, 79, 0.2)' 
                        : selectedEvent.severity === 'Medium'
                        ? 'rgba(240, 173, 78, 0.2)'
                        : 'rgba(92, 184, 92, 0.2)',
                      color: selectedEvent.severity === 'High'
                        ? '#d9534f'
                        : selectedEvent.severity === 'Medium'
                        ? '#f0ad4e'
                        : '#5cb85c'
                    }}>
                      ⚡ {selectedEvent.severity}
                    </span>
                    <button 
                      style={{
                        padding: '4px',
                        borderRadius: '6px',
                        background: 'transparent',
                        border: 'none',
                        color: 'var(--text-secondary)',
                        cursor: 'pointer'
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.background = 'var(--surface-hover)'
                        e.currentTarget.style.color = 'var(--text-primary)'
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.background = 'transparent'
                        e.currentTarget.style.color = 'var(--text-secondary)'
                      }}
                    >
                      ▶
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>

          <InvestigationAlertDetails event={selectedEvent} />
        </div>
      </div>
    </div>
  )
}
