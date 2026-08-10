import React, { useEffect, useMemo, useState } from 'react'
import { ChevronLeft, ChevronRight, FileSearch, Layers, PowerOff, ShieldAlert } from 'lucide-react'
import { PolicyInventorySearchResult } from '../_lib/types'

interface SearchResultFilters {
  area: string
  scope: string
  destinationType: string
  resourceType: string
  include: string
  exceptionStatus: string
}

interface SearchResultFilterOptions {
  areas: string[]
  scopes: string[]
  destinationTypes: string[]
  resourceTypes: string[]
  includes: string[]
  exceptionStatuses: string[]
}

interface SearchResultFilterSetters {
  setArea: (value: string) => void
  setScope: (value: string) => void
  setDestinationType: (value: string) => void
  setResourceType: (value: string) => void
  setInclude: (value: string) => void
  setExceptionStatus: (value: string) => void
}

interface Props {
  results: PolicyInventorySearchResult[]
  query: string
  isPending?: boolean
  isTooShort?: boolean
  totalBeforeFilters?: number
  filters: SearchResultFilters
  filterOptions: SearchResultFilterOptions
  onFiltersChange: SearchResultFilterSetters
  onBulkDisableExceptions?: (exceptionIds: number[]) => void
  bulkDisableInProgress?: boolean
}

const PAGE_SIZE = 100

const AREA_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  policy: { bg: 'rgba(16,185,129,0.1)', text: '#10b981', border: 'rgba(16,185,129,0.25)' },
  rule: { bg: 'rgba(59,130,246,0.1)', text: '#60a5fa', border: 'rgba(59,130,246,0.25)' },
  exception: { bg: 'rgba(245,158,11,0.1)', text: '#f59e0b', border: 'rgba(245,158,11,0.25)' },
  source: { bg: 'rgba(14,165,233,0.1)', text: '#38bdf8', border: 'rgba(14,165,233,0.25)' },
  destination: { bg: 'rgba(139,92,246,0.1)', text: '#a78bfa', border: 'rgba(139,92,246,0.25)' },
  classifier: { bg: 'rgba(168,85,247,0.1)', text: '#c084fc', border: 'rgba(168,85,247,0.25)' },
  severity: { bg: 'rgba(239,68,68,0.1)', text: '#ef4444', border: 'rgba(239,68,68,0.25)' },
  filtered: { bg: 'rgba(100,116,139,0.1)', text: 'var(--text-secondary)', border: 'var(--border-color)' },
}

function Badge({ value }: { value?: string }) {
  if (!value) return null
  const colors = AREA_COLORS[value] || AREA_COLORS.filtered

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

function FilterSelect({
  label,
  value,
  options,
  onChange,
  formatOption,
}: {
  label: string
  value: string
  options: string[]
  onChange: (value: string) => void
  formatOption?: (value: string) => string
}) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: '5px', minWidth: '150px' }}>
      <span style={{ fontSize: '10px', fontWeight: 700, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        style={{
          padding: '8px 9px',
          borderRadius: '8px',
          border: '1px solid var(--border-color)',
          background: 'var(--bg-color)',
          color: 'var(--text-primary)',
          fontSize: '12px',
          outline: 'none'
        }}
      >
        <option value="all">All</option>
        {options.map((option) => (
          <option key={option} value={option}>{formatOption ? formatOption(option) : option}</option>
        ))}
      </select>
    </label>
  )
}

function StatusPill({ value }: { value?: string }) {
  if (!value) return <span style={{ color: 'var(--text-secondary)' }}>-</span>
  const active = value === 'true'
  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      padding: '2px 8px',
      borderRadius: '999px',
      fontSize: '11px',
      fontWeight: 700,
      background: active ? 'rgba(16,185,129,0.1)' : 'rgba(239,68,68,0.08)',
      color: active ? '#10b981' : '#ef4444',
      border: `1px solid ${active ? 'rgba(16,185,129,0.25)' : 'rgba(239,68,68,0.2)'}`
    }}>
      {active ? 'Aktif' : 'Pasif'}
    </span>
  )
}

function EmptyState({ color, title, description }: { color: string; title: string; description: string }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '72px 20px', gap: '14px' }}>
      <div style={{
        width: '58px',
        height: '58px',
        borderRadius: '14px',
        background: `${color}1f`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center'
      }}>
        <FileSearch size={26} color={color} />
      </div>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>{title}</div>
        <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{description}</div>
      </div>
    </div>
  )
}

export default function PolicyInventorySearchResults({
  results,
  query,
  isPending = false,
  isTooShort = false,
  totalBeforeFilters = results.length,
  filters,
  filterOptions,
  onFiltersChange,
  onBulkDisableExceptions,
  bulkDisableInProgress = false,
}: Props) {
  const [page, setPage] = useState(1)
  const pageCount = Math.max(1, Math.ceil(results.length / PAGE_SIZE))
  const activeExceptionIds = useMemo(
    () => Array.from(new Set(
      results
        .filter(result => result.exception_id && result.exception_enabled === 'true')
        .map(result => result.exception_id!)
    )),
    [results]
  )

  useEffect(() => {
    setPage(1)
  }, [results, filters.area, filters.scope, filters.destinationType, filters.resourceType, filters.include, filters.exceptionStatus])

  const visibleResults = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE
    return results.slice(start, start + PAGE_SIZE)
  }, [results, page])

  if (isTooShort) {
    return (
      <EmptyState
        color="#60a5fa"
        title="Arama icin en az 2 karakter girin"
        description="Buyuk envanterlerde sistemin donmamasi icin detayli arama 2 karakterden sonra baslar."
      />
    )
  }

  if (isPending) {
    return <div style={{ padding: '32px 20px', color: 'var(--text-secondary)', fontSize: '13px' }}>Arama hazirlaniyor...</div>
  }

  if (!totalBeforeFilters) {
    return (
      <EmptyState
        color="#a78bfa"
        title="Sonuc bulunamadi"
        description={`"${query}" icin policy, rule, exception, source ve destination alanlari tarandi.`}
      />
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
              {results.length} / {totalBeforeFilters} eslesme gosteriliyor - sayfa {page}/{pageCount}
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          {onBulkDisableExceptions && activeExceptionIds.length > 0 && (
            <button
              type="button"
              disabled={bulkDisableInProgress}
              onClick={() => onBulkDisableExceptions(activeExceptionIds)}
              className="pi-bulk-disable-btn"
              title="Filtrelenen aktif exceptionlari Forcepoint uzerinde pasiflestir"
            >
              <PowerOff size={14} />
              {bulkDisableInProgress ? 'Kapatiliyor...' : `Filtrelenenleri Kapat (${activeExceptionIds.length})`}
            </button>
          )}
          <Badge value="filtered" />
        </div>
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px', padding: '12px 18px', borderBottom: '1px solid var(--border-color)' }}>
        <FilterSelect label="Alan" value={filters.area} options={filterOptions.areas} onChange={onFiltersChange.setArea} />
        <FilterSelect label="Entity Group" value={filters.scope} options={filterOptions.scopes} onChange={onFiltersChange.setScope} />
        <FilterSelect label="Destination Type" value={filters.destinationType} options={filterOptions.destinationTypes} onChange={onFiltersChange.setDestinationType} />
        <FilterSelect label="Resource Type" value={filters.resourceType} options={filterOptions.resourceTypes} onChange={onFiltersChange.setResourceType} />
        <FilterSelect label="Include" value={filters.include} options={filterOptions.includes} onChange={onFiltersChange.setInclude} />
        <FilterSelect
          label="Durum"
          value={filters.exceptionStatus}
          options={filterOptions.exceptionStatuses}
          onChange={onFiltersChange.setExceptionStatus}
          formatOption={(option) => option === 'true' ? 'Aktif' : 'Pasif'}
        />
      </div>

      {!results.length ? (
        <EmptyState
          color="#f59e0b"
          title="Filtrelere uygun sonuc yok"
          description="Arama sonucu var ancak secili source, destination veya entity group filtreleri sonucu bosaltti."
        />
      ) : (
        <>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '980px' }}>
              <thead>
                <tr style={{ background: 'var(--bg-color)' }}>
                  {['Policy', 'Rule', 'Scope', 'Exception', 'Durum', 'Alan', 'Eslesen Deger', 'Destination Type', 'Resource Type', 'Include'].map((header) => (
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
                {visibleResults.map((result) => (
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
                    <TextCell><StatusPill value={result.exception_enabled} /></TextCell>
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

          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '12px 18px',
            borderTop: '1px solid var(--border-color)',
            color: 'var(--text-secondary)',
            fontSize: '12px'
          }}>
            <span>
              {((page - 1) * PAGE_SIZE) + 1}-{Math.min(page * PAGE_SIZE, results.length)} / {results.length}
            </span>
            <div style={{ display: 'flex', gap: '6px' }}>
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage(p => Math.max(1, p - 1))}
                className="pi-search-page-btn"
              >
                <ChevronLeft size={14} /> Onceki
              </button>
              <button
                type="button"
                disabled={page >= pageCount}
                onClick={() => setPage(p => Math.min(pageCount, p + 1))}
                className="pi-search-page-btn"
              >
                Sonraki <ChevronRight size={14} />
              </button>
            </div>
          </div>

          <style>{`
            .pi-search-page-btn {
              display: inline-flex;
              align-items: center;
              gap: 5px;
              padding: 7px 10px;
              border-radius: 8px;
              border: 1px solid var(--border-color);
              background: var(--card-bg);
              color: var(--text-primary);
              cursor: pointer;
              font-size: 12px;
            }
            .pi-search-page-btn:disabled {
              cursor: not-allowed;
              opacity: 0.45;
            }
            .pi-bulk-disable-btn {
              display: inline-flex;
              align-items: center;
              gap: 6px;
              padding: 8px 11px;
              border-radius: 8px;
              border: 1px solid rgba(239,68,68,0.28);
              background: rgba(239,68,68,0.1);
              color: #ef4444;
              cursor: pointer;
              font-size: 12px;
              font-weight: 700;
            }
            .pi-bulk-disable-btn:disabled {
              cursor: wait;
              opacity: 0.6;
            }
          `}</style>
        </>
      )}
    </div>
  )
}
