'use client'

import RemediateButton from './RemediateButton'

interface AlertDetailsProps {
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
  }
}

export default function AlertDetails({ event }: AlertDetailsProps) {
  if (!event) {
    return (
      <div className="alert-details-empty">
        <p>Select an alert from the timeline to view details</p>
        <style jsx>{`
          .alert-details-empty {
            background: white;
            border-radius: 8px;
            padding: 40px;
            text-align: center;
            color: #666;
          }
        `}</style>
      </div>
    )
  }

  return (
    <div className="alert-details">
      <div className="alert-header">
        <h3>Alert details</h3>
        <div className="header-actions">
          <RemediateButton 
            incidentId={event.id}
            onRemediated={() => {
              // Refresh or update UI
              window.location.reload()
            }}
          />
          <button className="play-btn">▶</button>
          <span className={`severity-badge ${event.severity.toLowerCase()}`}>
            ⚡ {event.severity}
          </span>
          <button className="play-btn">▶</button>
        </div>
      </div>

      <div className="alert-summary">
        <div className="summary-row">
          <span className="label">Channel:</span>
          <span className="value">{event.channel || 'Unknown'}</span>
        </div>
        <div className="summary-row">
          <span className="label">Action:</span>
          <span className="value">{event.action || 'Permit'}</span>
        </div>
        {event.destination && (
          <div className="summary-row">
            <span className="label">Destination:</span>
            <span className="value">{event.destination}</span>
          </div>
        )}
        {event.classification && event.classification.length > 0 && (
          <div className="summary-row">
            <span className="label">Classification:</span>
            <div className="classification-tags">
              {event.classification.map((cls, idx) => (
                <span key={idx} className="classification-tag">{cls}</span>
              ))}
            </div>
          </div>
        )}
      </div>

      {event.matched_rules && event.matched_rules.length > 0 && (
        <div className="matched-rules">
          <h4>Matched Rule(s)</h4>
          <ul>
            {event.matched_rules.map((rule, idx) => (
              <li key={idx}>{rule}</li>
            ))}
          </ul>
        </div>
      )}

      <div className="details-section">
        <h4>Details</h4>
        <div className="details-content">
          <div className="detail-item">
            <span className="detail-label">Timestamp:</span>
            <span className="detail-value">{new Date(event.timestamp).toLocaleString()}</span>
          </div>
          <div className="detail-item">
            <span className="detail-label">Description:</span>
            <span className="detail-value">{event.description}</span>
          </div>
          {event.tags && event.tags.length > 0 && (
            <div className="detail-item">
              <span className="detail-label">Tags:</span>
              <div className="tags">
                {event.tags.map((tag, idx) => (
                  <span key={idx} className="tag">{tag}</span>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {event.files && event.files.length > 0 && (
        <div className="forensics-section">
          <h4>Forensics</h4>
          <p className="instruction">
            Filter the table below by classification type or search for a specific file.
          </p>
          
          <div className="forensics-filters">
            <input
              type="text"
              placeholder="Search / select a file"
              className="search-input"
            />
            <button className="classifiers-btn">
              Classifiers ▼
            </button>
          </div>

          <div className="classification-summary">
            <div className="classifier-item">
              <span className="classifier-name">DLP: US credit cards: all credit cards</span>
              <div className="classifier-stats">
                <span>Matches: 10</span>
                <span>Unique: 4</span>
              </div>
            </div>
            <div className="classifier-item">
              <span className="classifier-name">US SSN With First And Last Names</span>
              <div className="classifier-stats">
                <span>Matches: 3</span>
                <span>Unique: 3</span>
              </div>
            </div>
          </div>

          <table className="files-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Size</th>
                <th>Protected</th>
                <th>Classification</th>
              </tr>
            </thead>
            <tbody>
              {event.files.map((file, idx) => (
                <tr key={idx}>
                  <td>{file.name}</td>
                  <td>{file.size}</td>
                  <td>
                    {file.protected ? (
                      <span className="protected-icon">🔒</span>
                    ) : (
                      <span>-</span>
                    )}
                  </td>
                  <td>
                    <div className="file-classification">
                      {file.classification.map((cls, cIdx) => (
                        <span key={cIdx} className="file-class-tag">{cls}</span>
                      ))}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <style jsx>{`
        .alert-details {
          background: white;
          border-radius: 8px;
          padding: 16px;
          height: 100%;
          overflow-y: auto;
        }

        .alert-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 20px;
          padding-bottom: 12px;
          border-bottom: 1px solid #e0e0e0;
        }

        .alert-header h3 {
          margin: 0;
          color: #333;
        }

        .header-actions {
          display: flex;
          align-items: center;
          gap: 8px;
        }

        .play-btn {
          background: none;
          border: none;
          font-size: 18px;
          cursor: pointer;
          color: #666;
        }

        .severity-badge {
          padding: 4px 12px;
          border-radius: 12px;
          font-size: 12px;
          font-weight: 600;
        }

        .severity-badge.high {
          background: #ffebee;
          color: #c62828;
        }

        .severity-badge.medium {
          background: #fff3e0;
          color: #e65100;
        }

        .severity-badge.low {
          background: #e8f5e9;
          color: #2e7d32;
        }

        .alert-summary {
          background: #f5f5f5;
          padding: 16px;
          border-radius: 4px;
          margin-bottom: 20px;
        }

        .summary-row {
          display: flex;
          margin-bottom: 8px;
        }

        .summary-row:last-child {
          margin-bottom: 0;
        }

        .label {
          font-weight: 600;
          min-width: 100px;
          color: #666;
        }

        .value {
          color: #333;
        }

        .classification-tags {
          display: flex;
          gap: 6px;
          flex-wrap: wrap;
        }

        .classification-tag {
          padding: 2px 8px;
          background: #2196f3;
          color: white;
          border-radius: 12px;
          font-size: 11px;
          font-weight: 500;
        }

        .matched-rules,
        .details-section,
        .forensics-section {
          margin-bottom: 24px;
        }

        .matched-rules h4,
        .details-section h4,
        .forensics-section h4 {
          margin: 0 0 12px 0;
          color: #333;
          font-size: 14px;
        }

        .matched-rules ul {
          margin: 0;
          padding-left: 20px;
        }

        .matched-rules li {
          margin-bottom: 4px;
          color: #666;
        }

        .details-content {
          background: #f9f9f9;
          padding: 12px;
          border-radius: 4px;
        }

        .detail-item {
          margin-bottom: 12px;
        }

        .detail-item:last-child {
          margin-bottom: 0;
        }

        .detail-label {
          font-weight: 600;
          color: #666;
          display: block;
          margin-bottom: 4px;
        }

        .detail-value {
          color: #333;
        }

        .tags {
          display: flex;
          gap: 6px;
          flex-wrap: wrap;
          margin-top: 4px;
        }

        .tag {
          padding: 4px 10px;
          background: #e3f2fd;
          color: #1976d2;
          border-radius: 12px;
          font-size: 11px;
          font-weight: 500;
        }

        .instruction {
          font-size: 12px;
          color: #666;
          margin-bottom: 12px;
        }

        .forensics-filters {
          display: flex;
          gap: 8px;
          margin-bottom: 16px;
        }

        .search-input {
          flex: 1;
          padding: 8px 12px;
          border: 1px solid #ddd;
          border-radius: 4px;
        }

        .classifiers-btn {
          padding: 8px 16px;
          background: #00acc1;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: 500;
        }

        .classification-summary {
          background: #f5f5f5;
          padding: 12px;
          border-radius: 4px;
          margin-bottom: 16px;
        }

        .classifier-item {
          margin-bottom: 8px;
        }

        .classifier-item:last-child {
          margin-bottom: 0;
        }

        .classifier-name {
          display: block;
          font-weight: 500;
          margin-bottom: 4px;
          color: #333;
        }

        .classifier-stats {
          display: flex;
          gap: 16px;
          font-size: 12px;
          color: #666;
        }

        .files-table {
          width: 100%;
          border-collapse: collapse;
        }

        .files-table th {
          background: #f5f5f5;
          padding: 8px 12px;
          text-align: left;
          font-weight: 600;
          font-size: 12px;
          color: #666;
        }

        .files-table td {
          padding: 8px 12px;
          border-bottom: 1px solid #e0e0e0;
          font-size: 12px;
        }

        .files-table tr:hover {
          background: #f9f9f9;
        }

        .protected-icon {
          font-size: 16px;
        }

        .file-classification {
          display: flex;
          gap: 4px;
          flex-wrap: wrap;
        }

        .file-class-tag {
          padding: 2px 6px;
          background: #e3f2fd;
          color: #1976d2;
          border-radius: 8px;
          font-size: 10px;
        }
      `}</style>
    </div>
  )
}

