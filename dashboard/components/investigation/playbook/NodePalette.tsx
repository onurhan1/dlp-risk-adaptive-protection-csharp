'use client'

import { Plus } from 'lucide-react'
import { NODE_CATALOG, type NodeDefinition, type PlaybookNodeType } from './types'

interface Props {
  /** Click-to-add: appends the node to the right of the selection and auto-connects it. */
  onAdd: (type: PlaybookNodeType) => void
  /** Arms a drop so the next canvas click places the node at the cursor. */
  onArmDrop: (type: PlaybookNodeType) => void
  armedType: PlaybookNodeType | null
  /** A graph may hold only one trigger, so the palette greys out the rest. */
  hasTrigger: boolean
}

const CATEGORY_ORDER: NodeDefinition['category'][] = ['Tetikleyici', 'Kaynak', 'İşlem', 'Çıktı']

export default function NodePalette({ onAdd, onArmDrop, armedType, hasTrigger }: Props) {
  return (
    <div
      style={{
        width: '240px',
        flexShrink: 0,
        borderRight: '1px solid var(--border)',
        background: 'var(--surface)',
        overflowY: 'auto',
        padding: '14px',
      }}
    >
      <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
        Node Paleti
      </div>
      <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '14px', lineHeight: 1.4 }}>
        Tıklayarak ekleyin, ya da <strong>Yerleştir</strong>'e basıp tuvalde istediğiniz noktaya tıklayın.
      </div>

      {CATEGORY_ORDER.map(category => {
        const definitions = NODE_CATALOG.filter(d => d.category === category)
        if (definitions.length === 0) return null

        return (
          <div key={category} style={{ marginBottom: '16px' }}>
            <div
              style={{
                fontSize: '10px',
                fontWeight: 700,
                letterSpacing: '0.06em',
                textTransform: 'uppercase',
                color: 'var(--text-muted)',
                marginBottom: '8px',
              }}
            >
              {category}
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {definitions.map(definition => {
                const Icon = definition.icon
                const blocked = definition.inputs === 0 && hasTrigger
                const armed = armedType === definition.type

                return (
                  <div
                    key={definition.type}
                    title={blocked ? 'Akışta yalnızca bir tetikleyici olabilir' : definition.description}
                    style={{
                      border: `1px solid ${armed ? 'var(--primary)' : 'var(--border)'}`,
                      borderRadius: '8px',
                      padding: '9px 10px',
                      background: armed ? 'var(--surface-hover)' : 'var(--surface)',
                      opacity: blocked ? 0.45 : 1,
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <div
                        style={{
                          width: '26px',
                          height: '26px',
                          borderRadius: '7px',
                          background: definition.color,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          color: 'white',
                          flexShrink: 0,
                        }}
                      >
                        <Icon size={14} />
                      </div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-primary)' }}>
                          {definition.label}
                        </div>
                      </div>
                    </div>

                    <div style={{ fontSize: '10px', color: 'var(--text-muted)', margin: '6px 0 8px', lineHeight: 1.4 }}>
                      {definition.description}
                    </div>

                    <div style={{ display: 'flex', gap: '6px' }}>
                      <button
                        disabled={blocked}
                        onClick={() => onAdd(definition.type)}
                        style={{
                          flex: 1,
                          display: 'inline-flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          gap: '4px',
                          padding: '5px 8px',
                          fontSize: '11px',
                          fontWeight: 500,
                          background: 'var(--primary)',
                          color: 'white',
                          border: 'none',
                          borderRadius: '5px',
                          cursor: blocked ? 'not-allowed' : 'pointer',
                        }}
                      >
                        <Plus size={12} /> Ekle
                      </button>
                      <button
                        disabled={blocked}
                        onClick={() => onArmDrop(definition.type)}
                        style={{
                          padding: '5px 8px',
                          fontSize: '11px',
                          background: 'transparent',
                          color: armed ? 'var(--primary)' : 'var(--text-muted)',
                          border: `1px solid ${armed ? 'var(--primary)' : 'var(--border)'}`,
                          borderRadius: '5px',
                          cursor: blocked ? 'not-allowed' : 'pointer',
                        }}
                      >
                        {armed ? 'İptal' : 'Yerleştir'}
                      </button>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>
        )
      })}
    </div>
  )
}
