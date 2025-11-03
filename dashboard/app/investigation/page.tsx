'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { format } from 'date-fns'
import InvestigationUsersList from '@/components/InvestigationUsersList'
import InvestigationTimeline from '@/components/InvestigationTimeline'
import InvestigationAlertDetails from '@/components/InvestigationAlertDetails'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000'

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
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-white border-b border-gray-200 px-6 py-4">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-semibold text-gray-900">Investigation</h1>
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2 border border-gray-300 rounded-lg px-3 py-1.5">
              <button
                onClick={() => setActiveTab('users')}
                className={`px-4 py-1.5 rounded text-sm font-medium transition-colors ${
                  activeTab === 'users'
                    ? 'bg-teal-50 text-teal-700'
                    : 'text-gray-600 hover:text-gray-900'
                }`}
              >
                Users
              </button>
              <button
                onClick={() => setActiveTab('alerts')}
                className={`px-4 py-1.5 rounded text-sm font-medium transition-colors ${
                  activeTab === 'alerts'
                    ? 'bg-teal-50 text-teal-700'
                    : 'text-gray-600 hover:text-gray-900'
                }`}
              >
                Alerts
              </button>
            </div>
            <div className="text-sm text-gray-600">
              {format(new Date(), 'HH:mm dd-MMM-yyyy')}
            </div>
          </div>
        </div>
      </div>

      {/* Main Content - Three Column Layout */}
      <div className="grid grid-cols-[300px_1fr_400px] h-[calc(100vh-73px)]">
        {/* Left Panel - Users List */}
        <div className="bg-white border-r border-gray-200 flex flex-col overflow-hidden">
          <div className="p-4 border-b border-gray-200">
            <h2 className="text-sm font-semibold text-gray-700 mb-3">Investigation</h2>
            <input
              type="text"
              placeholder="Search users..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
            />
            <div className="mt-3">
              <select
                value={filterRisk}
                onChange={(e) => setFilterRisk(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-teal-500"
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
        <div className="bg-white flex flex-col overflow-hidden">
          <div className="p-4 border-b border-gray-200 flex items-center justify-between">
            <div className="flex items-center gap-4">
              <h2 className="text-lg font-semibold text-gray-900">Timeline</h2>
              {selectedUser && (
                <button
                  onClick={() => setSelectedUser(undefined)}
                  className="text-gray-400 hover:text-gray-600 text-xl leading-none"
                >
                  ×
                </button>
              )}
            </div>
            <div className="flex items-center gap-2">
              <button
                className="p-2 rounded hover:bg-gray-100 transition-colors"
                title="Filter"
              >
                ⚡
              </button>
              <button
                className="p-2 rounded hover:bg-gray-100 transition-colors"
                title="Sort"
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
        <div className="bg-white border-l border-gray-200 flex flex-col overflow-hidden">
          <div className="p-4 border-b border-gray-200">
            <div className="flex items-center justify-between mb-2">
              <h2 className="text-lg font-semibold text-gray-900">Alert details</h2>
              <div className="flex items-center gap-2">
                {selectedEvent && (
                  <>
                    <span className={`px-2 py-1 rounded text-xs font-semibold ${
                      selectedEvent.severity === 'High'
                        ? 'bg-red-100 text-red-700'
                        : selectedEvent.severity === 'Medium'
                        ? 'bg-yellow-100 text-yellow-700'
                        : 'bg-green-100 text-green-700'
                    }`}>
                      ⚡ {selectedEvent.severity}
                    </span>
                    <button className="p-1 rounded hover:bg-gray-100">▶</button>
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
