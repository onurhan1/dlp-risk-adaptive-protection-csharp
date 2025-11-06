'use client'

import RemediateButton from './RemediateButton'

interface InvestigationAlertDetailsProps {
  event?: {
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
}

export default function InvestigationAlertDetails({ event }: InvestigationAlertDetailsProps) {
  if (!event) {
    return (
      <div className="flex-1 flex items-center justify-center text-gray-500 p-8">
        <p className="text-center">Select an alert from the timeline to view details</p>
      </div>
    )
  }

  const getClassificationColor = (cls: string): string => {
    if (cls === 'PCI') return '#ef4444'
    if (cls === 'CCN') return '#14b8a6'
    if (cls === 'PII') return '#8b5cf6'
    return '#6b7280'
  }

  return (
    <div className="flex-1 overflow-y-auto p-4">
      {/* Summary Section */}
      <div className="space-y-3 mb-6">
        <div className="flex items-center justify-between">
          <span className="text-sm text-gray-600">Channel:</span>
          <span className="text-sm font-medium text-gray-900">{event.channel || 'Unknown'}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-sm text-gray-600">Action:</span>
          <span className="text-sm font-medium text-gray-900">{event.action || 'Permit'}</span>
        </div>
        {event.destination && (
          <div className="flex items-center justify-between">
            <span className="text-sm text-gray-600">Destination:</span>
            <span className="text-sm font-medium text-gray-900">{event.destination}</span>
          </div>
        )}
        {event.classification && event.classification.length > 0 && (
          <div>
            <span className="text-sm text-gray-600 block mb-2">Classification:</span>
            <div className="flex flex-wrap gap-2">
              {event.classification.map((cls, idx) => (
                <span
                  key={idx}
                  className="px-2 py-1 rounded text-xs font-semibold text-white"
                  style={{ backgroundColor: getClassificationColor(cls) }}
                >
                  {cls}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Matched Rules */}
      {event.matched_rules && event.matched_rules.length > 0 && (
        <div className="mb-6">
          <h4 className="text-sm font-semibold text-gray-900 mb-2">Matched Rule(s)</h4>
          <div className="space-y-2">
            {event.matched_rules.map((rule, idx) => (
              <div key={idx} className="flex items-center gap-2 text-sm">
                <span className="w-2 h-2 rounded-full bg-red-500" />
                <span className="text-red-600 font-medium">NEO</span>
                <span className="text-gray-700">IoB-{event.iob_number || '502'}</span>
                <span className="text-gray-600">{event.description}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Details Section */}
      <div className="mb-6">
        <h4 className="text-sm font-semibold text-gray-900 mb-3">Details</h4>
        <div className="space-y-2 text-sm">
          {event.source_application && (
            <div>
              <span className="text-gray-600">Source application: </span>
              <span className="text-gray-900 font-medium">{event.source_application}</span>
            </div>
          )}
          {event.email_subject && (
            <div>
              <span className="text-gray-600">Email Subject: </span>
              <span className="text-gray-900 font-medium">{event.email_subject}</span>
            </div>
          )}
          {event.recipients && (
            <div>
              <span className="text-gray-600">Recipients To: </span>
              <span className="text-gray-900 font-medium">{event.recipients}</span>
            </div>
          )}
        </div>
      </div>

      {/* Forensics Section */}
      <div className="mb-6">
        <h4 className="text-sm font-semibold text-gray-900 mb-2">Forensics</h4>
        <p className="text-xs text-gray-600 mb-3">
          Filter the table below by classification type or search for a specific file.
        </p>

        {/* Search and Filter */}
        <div className="mb-4 space-y-2">
          <div className="relative">
            <input
              type="text"
              placeholder="Search / select a file"
              className="w-full px-3 py-2 pr-8 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-teal-500"
            />
            <svg
              className="absolute right-2 top-2.5 w-4 h-4 text-gray-400"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>
          <button className="w-full px-3 py-2 bg-teal-50 text-teal-700 rounded-md text-sm font-medium hover:bg-teal-100 transition-colors flex items-center justify-between">
            <span>Classifiers</span>
            <span>▼</span>
          </button>
        </div>

        {/* DLP Classifiers */}
        <div className="mb-4 space-y-2">
          <div className="p-3 bg-gray-50 rounded-lg">
            <div className="text-sm font-medium text-gray-900 mb-1">
              DLP: US credit cards: all credit cards
            </div>
            <div className="flex gap-4 text-xs text-gray-600">
              <span>Matches: 10</span>
              <span>Unique: 4</span>
            </div>
          </div>
          <div className="p-3 bg-gray-50 rounded-lg">
            <div className="text-sm font-medium text-gray-900 mb-1">
              US SSN With First And Last Names
            </div>
            <div className="flex gap-4 text-xs text-gray-600">
              <span>Matches: 3</span>
              <span>Unique: 3</span>
            </div>
          </div>
        </div>

        {/* Files Table */}
        {event.files && event.files.length > 0 && (
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-600 uppercase">Name</th>
                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-600 uppercase">Size</th>
                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-600 uppercase">Protected</th>
                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-600 uppercase">Classification</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {event.files.map((file, idx) => (
                  <tr key={idx} className="hover:bg-gray-50">
                    <td className="px-3 py-2 text-gray-900">{file.name}</td>
                    <td className="px-3 py-2 text-gray-600">{file.size}</td>
                    <td className="px-3 py-2">
                      {file.protected ? (
                        <svg className="w-4 h-4 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z" clipRule="evenodd" />
                        </svg>
                      ) : (
                        <span className="text-gray-400">—</span>
                      )}
                    </td>
                    <td className="px-3 py-2">
                      <div className="flex flex-wrap gap-1">
                        {file.classification.map((cls, clsIdx) => (
                          <span
                            key={clsIdx}
                            className="px-1.5 py-0.5 rounded text-xs font-medium text-white"
                            style={{ backgroundColor: getClassificationColor(cls) }}
                          >
                            {cls}
                          </span>
                        ))}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Remediate Button */}
      <div className="mt-6 pt-4 border-t border-gray-200">
        <RemediateButton
          incidentId={event.id}
          onRemediated={() => {
            window.location.reload()
          }}
        />
      </div>
    </div>
  )
}

