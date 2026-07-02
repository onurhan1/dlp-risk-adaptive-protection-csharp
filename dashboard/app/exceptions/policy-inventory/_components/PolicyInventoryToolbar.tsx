import React from 'react'
import { Search, Plus } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'

interface ToolbarProps {
  searchQuery: string
  setSearchQuery: (val: string) => void
  searchFilter: string
  setSearchFilter: (val: string) => void
  onNewPolicy: () => void
}

export default function Toolbar({ searchQuery, setSearchQuery, searchFilter, setSearchFilter, onNewPolicy }: ToolbarProps) {
  const { t } = useTranslation()

  return (
    <div style={{
      padding: '16px 20px',
      borderBottom: '1px solid var(--border-color)',
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      gap: '16px',
      flexWrap: 'wrap'
    }}>
      <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: '8px', flex: 1, maxWidth: '600px' }}>
        <select
          value={searchFilter}
          onChange={(e) => setSearchFilter(e.target.value)}
          style={{
            padding: '10px 12px',
            borderRadius: '8px',
            border: '1px solid var(--border-color)',
            background: 'var(--bg-color)',
            color: 'var(--text-primary)',
            fontSize: '13px',
            outline: 'none',
            cursor: 'pointer',
            minWidth: '140px'
          }}
        >
          <option value="all">{t('policyInventory.searchAll') || 'Tümü'}</option>
          <option value="policy">{t('policyInventory.searchPolicy') || 'Politika Adı'}</option>
          <option value="rule">{t('policyInventory.searchRule') || 'Kural Adı'}</option>
          <option value="exception">{t('policyInventory.searchException') || 'Exception Adı'}</option>
          <option value="source">{t('policyInventory.searchSource') || 'Kaynak (Source)'}</option>
          <option value="destination">{t('policyInventory.searchDestination') || 'Hedef (Destination)'}</option>
          <option value="classifier">Classifier</option>
          <option value="severity">Severity</option>
        </select>
        <div style={{ position: 'relative', flex: 1 }}>
          <Search size={16} color="var(--text-secondary)" style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }} />
          <input
            type="text"
            placeholder={t('policyInventory.search')}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              width: '100%',
              padding: '10px 12px 10px 36px',
              borderRadius: '8px',
              border: '1px solid var(--border-color)',
              background: 'var(--bg-color)',
              color: 'var(--text-primary)',
              fontSize: '14px',
              outline: 'none',
              transition: 'border-color 0.2s'
            }}
          />
        </div>
      </div>

      <button
        onClick={onNewPolicy}
        className="glass-button"
        style={{
          background: 'linear-gradient(135deg, #10b981, #059669)',
          color: '#fff',
          border: 'none',
          padding: '10px 20px',
          borderRadius: '8px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          cursor: 'pointer',
          fontWeight: '500',
          fontSize: '14px',
          boxShadow: '0 4px 12px rgba(16, 185, 129, 0.2)'
        }}
      >
        <Plus size={16} />
        {t('policyInventory.newPolicy')}
      </button>
    </div>
  )
}
