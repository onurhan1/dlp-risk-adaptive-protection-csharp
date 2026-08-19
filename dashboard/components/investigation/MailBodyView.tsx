'use client'

import type { CSSProperties } from 'react'

interface MailBodyViewProps {
  bodyText?: string | null
  emptyText?: string
}

interface MailThreadSections {
  current: string
  quoted: string
}

const preStyle: CSSProperties = {
  margin: 0,
  whiteSpace: 'pre-wrap',
  wordBreak: 'break-word',
  fontFamily: 'ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  fontSize: 13,
  lineHeight: 1.55,
  color: 'var(--text-primary)',
}

const panelStyle: CSSProperties = {
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: 14,
}

const quotedHeaderPattern = /^\s*(From|Sent|To|Cc|Subject|G.nderen|Kimden|G.nderildi|Kime|Bilgi|Konu)\s*:/i
const quotedContextPattern = /^\s*(Sent|To|Cc|Subject|G.nderildi|Kime|Bilgi|Konu)\s*:/im
const originalMessagePattern = /^\s*-{2,}\s*Original Message\s*-{2,}\s*$/i
const wrotePattern = /^\s*On\s+.+\s+wrote:\s*$/i

function tidy(value: string) {
  return value
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim()
}

function splitMailThread(bodyText?: string | null): MailThreadSections {
  const normalized = tidy(bodyText || '')
  if (!normalized) return { current: '', quoted: '' }

  const lines = normalized.split('\n')
  for (let index = 1; index < lines.length; index++) {
    const line = lines[index]
    const windowText = lines.slice(index, index + 10).join('\n')

    if (originalMessagePattern.test(line) || wrotePattern.test(line)) {
      return {
        current: tidy(lines.slice(0, index).join('\n')),
        quoted: tidy(lines.slice(index).join('\n')),
      }
    }

    if (quotedHeaderPattern.test(line) && quotedContextPattern.test(windowText)) {
      return {
        current: tidy(lines.slice(0, index).join('\n')),
        quoted: tidy(lines.slice(index).join('\n')),
      }
    }
  }

  return { current: normalized, quoted: '' }
}

export default function MailBodyView({ bodyText, emptyText = 'Gosterilecek metin icerigi bulunamadi.' }: MailBodyViewProps) {
  const sections = splitMailThread(bodyText)

  if (!sections.current && !sections.quoted) {
    return (
      <div style={{ ...panelStyle, color: 'var(--text-secondary)', fontSize: 13 }}>
        {emptyText}
      </div>
    )
  }

  return (
    <div style={{ display: 'grid', gap: 12 }}>
      <div style={panelStyle}>
        {sections.quoted && (
          <div style={{ marginBottom: 10, color: 'var(--text-secondary)', fontSize: 12, fontWeight: 800 }}>
            Cevap
          </div>
        )}
        <pre style={preStyle}>{sections.current || emptyText}</pre>
      </div>

      {sections.quoted && (
        <details style={{
          ...panelStyle,
          padding: 0,
          overflow: 'hidden',
        }}>
          <summary style={{
            padding: '12px 14px',
            cursor: 'pointer',
            color: 'var(--text-primary)',
            fontSize: 13,
            fontWeight: 800,
            borderBottom: '1px solid var(--border)',
          }}>
            Onceki yazisma
          </summary>
          <div style={{ padding: 14, background: 'var(--background-secondary)' }}>
            <pre style={{ ...preStyle, color: 'var(--text-secondary)' }}>{sections.quoted}</pre>
          </div>
        </details>
      )}
    </div>
  )
}
