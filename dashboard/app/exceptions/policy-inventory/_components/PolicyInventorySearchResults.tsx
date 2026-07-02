import React from 'react'
import { FileSearch, Layers, ShieldAlert } from 'lucide-react'
import { PolicyInventorySearchResult } from '../_lib/types'

interface Props {
  results: PolicyInventorySearchResult[]
  query: string
}

const AREA_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  policy: { bg: 'rgba(16,185,129,0.1)', text: '#10b981', border: 'rgba(16,185,129,0.25)' },
  rule: { bg: 'rgba(59,130,246,0.1)', text: '#60a5fa', border: 'rgba(59,130,246,0.25)' },
  exception: { bg: 'rgba(245,158,11,0.1)', text: '#f59e0b', border: 'rgba(245,158,11,0.25)' },
  source: { bg: 'rgba(14,165,233,0.1)', text: '#38bdf8', border: 'rgba(14,165,233,0.25)' },
  destination: { bg: 'rgba(139,92,246,0.1)', text: '#a78bfa', border: 'rgba(139,92,246,0.25)' },
  classifier: { bg: 'rgba(168,85,247,0.1)', text: '#c084fc', border: 'rgba(168,85,247,0.25)' },
  severity: { bg: 'rgba(239,68,68,0.1)', text: '#ef4444', border: 'rgba(239,68,68,0.25)' },
}

function Badge({ value }: { value?: string }) {
  if (!value) return null
  const colors = AREA_COLORS[value] || { bg: 'rgba(100,100,100,0.08)', text: 'var(--text-secondary)', border: 'var(--border-color)' }

  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      padding: '2px 8px',
      borderRadius: '999px',
      fontSize: '11px',
      fontWeight: 700,
      textTransform: 'uppercase',
      background: colors.bg,
      color: colors.text,
      border: `1px solid ${colors.border}`,
      whiteSpace: 'nowrap'
    }}>
      {value}
    </span>
  )
}

function TextCell({ children }: { children?: React.ReactNode }) {
  return (
    <td style={{
      padding: '11px 12px',
      borderBottom: '1px solid var(--border-color)',
      color: 'var(--text-primary)',
      fontSize: '12px',
      verticalAlign: 'top'
    }}>
      {children || <span style={{ color: 'var(--text-secondary)' }}>-</span>}
    </td>
  )
}

export default function PolicyInventorySearchResults({ results, query }: Props) {
  if (!results.length) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '72px 20px', gap: '14px' }}>
        <div style={{
          width: '58px',
          height: '58px',
          borderRadius: '14px',
          background: 'rgba(139,92,246,0.12)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center'
        }}>
          <FileSearch size={26} color="#a78bfa" />
        </div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>Sonuc bulunamadi</div>
          <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>
            "{query}" icin policy, rule, exception, source ve destination alanlari tarandi.
          </div>
        </div>
      </div>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '12px',
        padding: '13px 18px',
        borderBottom: '1px solid var(--border-color)',
        background: 'rgba(139,92,246,0.04)'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <FileSearch size={17} color="#a78bfa" />
          <div>
            <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-primary)' }}>Detayli arama sonucu</div>
            <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '1px' }}>
              {results.length} eslesme bulundu
            </div>
          </div>
        </div>
        <Badge value="filtered" />
      </div>

      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '980px' }}>
          <thead>
            <tr style={{ background: 'var(--bg-color)' }}>
              {['Policy', 'Rule', 'Scope', 'Exception', 'Alan', 'Eslesen Deger', 'Destination Type', 'Resource Type', 'Include'].map((header) => (
                <th key={header} style={{
                  padding: '10px 12px',
                  borderBottom: '1px solid var(--border-color)',
                  color: 'var(--text-secondary)',
                  fontSize: '11px',
                  fontWeight: 700,
                  textAlign: 'left',
                  textTransform: 'uppercase'
                }}>
                  {header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {results.map((result) => (
              <tr key={result.id}>
                <TextCell>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '7px' }}>
                    <ShieldAlert size={13} color="#10b981" />
                    <span style={{ fontWeight: 600 }}>{result.policy_name}</span>
                  </div>
                </TextCell>
                <TextCell>
                  {result.rule_name && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '7px' }}>
                      <Layers size={13} color="#60a5fa" />
                      {result.rule_name}
                    </div>
                  )}
                </TextCell>
                <TextCell><Badge value={result.scope} /></TextCell>
                <TextCell>{result.exception_rule_name}</TextCell>
                <TextCell>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <Badge value={result.match_area} />
                    <span style={{ color: 'var(--text-secondary)', fontSize: '11px' }}>{result.match_field}</span>
                  </div>
                </TextCell>
                <TextCell>
                  <span style={{ fontWeight: 700 }}>{result.matched_value}</span>
                </TextCell>
                <TextCell>{result.destination_type}</TextCell>
                <TextCell>{result.resource_type}</TextCell>
                <TextCell>{result.include}</TextCell>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
