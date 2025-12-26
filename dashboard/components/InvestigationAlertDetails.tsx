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
    // New extended fields
    fileName?: string
    loginName?: string
    emailAddress?: string
    violationTriggers?: string
    policy?: string
    // Remediation fields
    isRemediated?: boolean
    remediatedAt?: string
    remediatedBy?: string
    remediationAction?: string
    remediationNotes?: string
  }
}

export default function InvestigationAlertDetails({ event }: InvestigationAlertDetailsProps) {
  if (!event) {
    return (
      <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-muted)', padding: '32px' }}>
        <p style={{ textAlign: 'center' }}>Select an alert from the timeline to view details</p>
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
    <div style={{ flex: 1, overflowY: 'auto', padding: '16px' }}>
      {/* Summary Section */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Channel:</span>
          <span style={{ fontSize: '14px', fontWeight: '500', color: 'var(--text-primary)' }}>{event.channel || 'Unknown'}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Action:</span>
          <span style={{ fontSize: '14px', fontWeight: '500', color: 'var(--text-primary)' }}>{event.action || 'Permit'}</span>
        </div>
        {event.destination && (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Destination:</span>
            <span style={{ fontSize: '14px', fontWeight: '500', color: 'var(--text-primary)' }}>{event.destination}</span>
          </div>
        )}
        {event.classification && event.classification.length > 0 && (
          <div>
            <span style={{ fontSize: '14px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px' }}>Classification:</span>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
              {event.classification.map((cls, idx) => (
                <span
                  key={idx}
                  style={{ padding: '4px 8px', borderRadius: '4px', fontSize: '12px', fontWeight: '600', color: 'white', backgroundColor: getClassificationColor(cls) }}
                >
                  {cls}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Matched Policy */}
      {(event.policy || event.violationTriggers) && (
        <div style={{ marginBottom: '24px' }}>
          <h4 style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Matched Policy</h4>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {(() => {
              let triggers: any[] = []
              if (event.violationTriggers) {
                try {
                  triggers = JSON.parse(event.violationTriggers)
                } catch {
                  triggers = []
                }
              }
              const policyName = triggers.length > 0 ? triggers[0]?.PolicyName : event.policy
              return policyName ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px' }}>
                  <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#3b82f6' }} />
                  <span style={{ color: 'var(--text-primary)' }}>{policyName}</span>
                </div>
              ) : null
            })()}
          </div>

          {/* Matched Rules Sub-section */}
          {(() => {
            let triggers: any[] = []
            if (event.violationTriggers) {
              try {
                triggers = JSON.parse(event.violationTriggers)
              } catch {
                triggers = []
              }
            }
            const ruleNames = triggers.map((t: any) => t.RuleName).filter(Boolean)
            const rules = ruleNames.length > 0 ? ruleNames : (event.matched_rules || [])

            // Extract classifiers with NumberMatches
            const classifiersWithMatches = triggers.flatMap((t: any) =>
              (t.Classifiers || []).map((c: any) => ({
                name: c.ClassifierName,
                matches: c.NumberMatches,
                rule: t.RuleName
              }))
            ).filter((c: any) => c.name && c.matches > 0)

            return (
              <>
                {rules.length > 0 && (
                  <div style={{ marginTop: '12px' }}>
                    <h5 style={{ fontSize: '13px', fontWeight: '500', color: 'var(--text-secondary)', marginBottom: '6px' }}>Matched Rules</h5>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', paddingLeft: '12px' }}>
                      {rules.map((rule: string, idx: number) => (
                        <div key={idx} style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px' }}>
                          <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#ef4444' }} />
                          <span style={{ color: 'var(--text-primary)' }}>{rule}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* MaxMatches - Classifier Matches Section */}
                {classifiersWithMatches.length > 0 && (
                  <div style={{ marginTop: '12px' }}>
                    <h5 style={{ fontSize: '13px', fontWeight: '500', color: 'var(--text-secondary)', marginBottom: '6px' }}>
                      Classifier Matches
                    </h5>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', paddingLeft: '12px' }}>
                      {classifiersWithMatches.map((c: any, idx: number) => (
                        <div key={idx} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '13px', padding: '6px 8px', background: 'var(--background-secondary)', borderRadius: '4px' }}>
                          <span style={{ color: 'var(--text-primary)' }}>{c.name}</span>
                          <span style={{
                            padding: '2px 8px',
                            borderRadius: '12px',
                            fontSize: '12px',
                            fontWeight: '600',
                            background: c.matches >= 10 ? '#dc2626' : c.matches >= 5 ? '#f59e0b' : '#10b981',
                            color: 'white'
                          }}>
                            {c.matches} matches
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </>
            )
          })()}
        </div>
      )}

      {/* Legacy Matched Rules (if no policy/violationTriggers but matched_rules exist) */}
      {!event.policy && !event.violationTriggers && event.matched_rules && event.matched_rules.length > 0 && (
        <div style={{ marginBottom: '24px' }}>
          <h4 style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Matched Rule(s)</h4>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {event.matched_rules.map((rule, idx) => (
              <div key={idx} style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px' }}>
                <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#ef4444' }} />
                <span style={{ color: 'var(--text-primary)' }}>{rule}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Details Section */}
      <div style={{ marginBottom: '24px' }}>
        <h4 style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '12px' }}>Details</h4>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '14px' }}>
          {event.loginName && (
            <div>
              <span style={{ color: 'var(--text-secondary)' }}>Login Name: </span>
              <span style={{ color: 'var(--text-primary)', fontWeight: '500' }}>{event.loginName}</span>
            </div>
          )}
          {event.emailAddress && (
            <div>
              <span style={{ color: 'var(--text-secondary)' }}>Email Address: </span>
              <span style={{ color: 'var(--text-primary)', fontWeight: '500' }}>{event.emailAddress}</span>
            </div>
          )}
          {event.fileName && (
            <div>
              <span style={{ color: 'var(--text-secondary)' }}>File Name: </span>
              <span style={{ color: 'var(--text-primary)', fontWeight: '500' }}>{event.fileName}</span>
            </div>
          )}
          {event.source_application && (
            <div>
              <span style={{ color: 'var(--text-secondary)' }}>Source application: </span>
              <span style={{ color: 'var(--text-primary)', fontWeight: '500' }}>{event.source_application}</span>
            </div>
          )}
          {event.email_subject && (
            <div>
              <span style={{ color: 'var(--text-secondary)' }}>Email Subject: </span>
              <span style={{ color: 'var(--text-primary)', fontWeight: '500' }}>{event.email_subject}</span>
            </div>
          )}
          {event.recipients && (
            <div>
              <span style={{ color: 'var(--text-secondary)' }}>Recipients To: </span>
              <span style={{ color: 'var(--text-primary)', fontWeight: '500' }}>{event.recipients}</span>
            </div>
          )}
        </div>
      </div>

      {/* Forensics Section - Only show if files are available */}
      {event.files && event.files.length > 0 && (
        <div style={{ marginBottom: '24px' }}>
          <h4 style={{ fontSize: '14px', fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>Forensics</h4>
          <p style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '12px' }}>
            Filter the table below by classification type or search for a specific file.
          </p>

          {/* Search and Filter */}
          <div style={{ marginBottom: '16px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                placeholder="Search / select a file"
                style={{
                  width: '100%',
                  padding: '8px 32px 8px 12px',
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
              <svg
                style={{ position: 'absolute', right: '8px', top: '10px', width: '16px', height: '16px', color: 'var(--text-muted)' }}
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </div>
            <button style={{
              width: '100%',
              padding: '8px 12px',
              background: 'rgba(0, 168, 232, 0.1)',
              color: 'var(--primary)',
              borderRadius: '6px',
              fontSize: '14px',
              fontWeight: '500',
              border: 'none',
              cursor: 'pointer',
              transition: 'all 0.2s',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between'
            }}
              onMouseEnter={(e) => {
                e.currentTarget.style.background = 'rgba(0, 168, 232, 0.2)'
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.background = 'rgba(0, 168, 232, 0.1)'
              }}
            >
              <span>Classifiers</span>
              <span>▼</span>
            </button>
          </div>

          {/* DLP Classifiers - Only show if classification data is available */}
          {event.classification && event.classification.length > 0 && (
            <div style={{ marginBottom: '16px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {event.classification.map((cls, idx) => (
                <div key={idx} style={{ padding: '12px', background: 'var(--background-secondary)', borderRadius: '8px' }}>
                  <div style={{ fontSize: '14px', fontWeight: '500', color: 'var(--text-primary)', marginBottom: '4px' }}>
                    {cls}
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Files Table */}
          <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
            <table style={{ width: '100%', fontSize: '14px' }}>
              <thead style={{ background: 'var(--background-secondary)' }}>
                <tr>
                  <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Name</th>
                  <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Size</th>
                  <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Protected</th>
                  <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '12px', fontWeight: '600', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Classification</th>
                </tr>
              </thead>
              <tbody style={{ borderTop: '1px solid var(--border)' }}>
                {event.files.map((file, idx) => (
                  <tr key={idx} style={{ borderBottom: '1px solid var(--border)', transition: 'all 0.2s' }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.background = 'var(--surface-hover)'
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.background = 'transparent'
                    }}
                  >
                    <td style={{ padding: '8px 12px', color: 'var(--text-primary)' }}>{file.name}</td>
                    <td style={{ padding: '8px 12px', color: 'var(--text-secondary)' }}>{file.size}</td>
                    <td style={{ padding: '8px 12px' }}>
                      {file.protected ? (
                        <svg style={{ width: '16px', height: '16px', color: 'var(--text-secondary)' }} fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z" clipRule="evenodd" />
                        </svg>
                      ) : (
                        <span style={{ color: 'var(--text-muted)' }}>—</span>
                      )}
                    </td>
                    <td style={{ padding: '8px 12px' }}>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                        {file.classification.map((cls, clsIdx) => (
                          <span
                            key={clsIdx}
                            style={{ padding: '2px 6px', borderRadius: '4px', fontSize: '12px', fontWeight: '500', color: 'white', backgroundColor: getClassificationColor(cls) }}
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
        </div>
      )}

      {/* Remediation Section */}
      <div style={{ marginTop: '24px', paddingTop: '16px', borderTop: '1px solid var(--border)' }}>
        {event.isRemediated ? (
          <div style={{
            background: 'rgba(16, 185, 129, 0.1)',
            border: '1px solid rgba(16, 185, 129, 0.3)',
            borderRadius: '8px',
            padding: '12px 16px'
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <span style={{ fontSize: '18px' }}>✅</span>
              <span style={{ fontWeight: '600', color: '#10b981' }}>Remediated #{event.id}</span>
            </div>
            <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
              <div style={{ marginBottom: '4px' }}>
                <strong>Date:</strong> {event.remediatedAt ? new Date(event.remediatedAt).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : 'N/A'}
              </div>
              <div style={{ marginBottom: '4px' }}>
                <strong>By:</strong> {event.remediatedBy || 'System'}
              </div>
              {event.remediationAction && (
                <div style={{ marginBottom: '4px' }}>
                  <strong>Action:</strong> <span style={{ color: '#f59e0b', fontWeight: '500' }}>{event.remediationAction}</span>
                </div>
              )}
              {event.remediationNotes && (
                <div>
                  <strong>Notes:</strong> {event.remediationNotes}
                </div>
              )}
            </div>
          </div>
        ) : (
          <RemediateButton
            incidentId={event.id}
            onRemediated={() => {
              window.location.reload()
            }}
          />
        )}
      </div>
    </div>
  )
}