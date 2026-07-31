'use client'

import type { PointerEvent as ReactPointerEvent } from 'react'
import { Trash2, AlertTriangle } from 'lucide-react'
import {
  NODE_WIDTH,
  NODE_HEIGHT,
  describeNode,
  nodeDefinition,
  type PlaybookNode,
} from './types'

interface Props {
  node: PlaybookNode
  selected: boolean
  /** Node ran in the last run — tints the border so the canvas doubles as a run view. */
  runStatus?: 'success' | 'failed' | 'skipped'
  invalid?: boolean
  templateNames: Record<number, string>
  onPointerDownBody: (e: ReactPointerEvent<HTMLDivElement>, nodeId: string) => void
  onPointerDownOutput: (e: ReactPointerEvent<HTMLDivElement>, nodeId: string, handle: string | null) => void
  onDelete: (nodeId: string) => void
}

const PORT_SIZE = 14

const RUN_BORDER: Record<string, string> = {
  success: '#10b981',
  failed: '#ef4444',
  skipped: '#94a3b8',
}

export default function PlaybookNodeCard({
  node,
  selected,
  runStatus,
  invalid,
  templateNames,
  onPointerDownBody,
  onPointerDownOutput,
  onDelete,
}: Props) {
  const definition = nodeDefinition(node.type)
  const Icon = definition?.icon
  const outputs = definition?.outputs ?? [{ handle: null }]
  const hasInput = (definition?.inputs ?? 1) > 0

  const borderColor = invalid
    ? '#ef4444'
    : selected
      ? 'var(--primary)'
      : runStatus
        ? RUN_BORDER[runStatus]
        : 'var(--border)'

  return (
    <div
      data-node-id={node.id}
      onPointerDown={e => onPointerDownBody(e, node.id)}
      style={{
        position: 'absolute',
        left: node.x,
        top: node.y,
        width: NODE_WIDTH,
        height: NODE_HEIGHT,
        background: 'var(--surface)',
        border: `2px solid ${borderColor}`,
        borderRadius: '12px',
        boxShadow: selected ? '0 8px 24px rgba(15,23,42,0.18)' : '0 2px 8px rgba(15,23,42,0.08)',
        cursor: 'grab',
        userSelect: 'none',
        touchAction: 'none',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '10px 12px 6px' }}>
        <div
          style={{
            width: '28px',
            height: '28px',
            borderRadius: '8px',
            background: definition?.color ?? 'var(--surface-hover)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: 'white',
            flexShrink: 0,
          }}
        >
          {Icon ? <Icon size={16} /> : null}
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div
            style={{
              fontSize: '13px',
              fontWeight: 600,
              color: 'var(--text-primary)',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {node.label}
          </div>
        </div>
        {invalid && (
          <span title="Bu adımın ayarları eksik" style={{ color: '#ef4444', display: 'flex', flexShrink: 0 }}>
            <AlertTriangle size={14} />
          </span>
        )}
        {selected && (
          <button
            onPointerDown={e => e.stopPropagation()}
            onClick={e => {
              e.stopPropagation()
              onDelete(node.id)
            }}
            title="Node'u sil"
            style={{
              background: 'transparent',
              border: 'none',
              cursor: 'pointer',
              color: 'var(--text-muted)',
              display: 'flex',
              padding: 0,
              flexShrink: 0,
            }}
          >
            <Trash2 size={14} />
          </button>
        )}
      </div>

      {/* Config summary */}
      <div
        style={{
          padding: '0 12px 10px',
          fontSize: '11px',
          lineHeight: 1.4,
          color: 'var(--text-muted)',
          overflow: 'hidden',
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
        }}
      >
        {describeNode(node, templateNames)}
      </div>

      {/* Input port — purely visual; the canvas hit-tests data-node-id to land a connection */}
      {hasInput && (
        <div
          data-port="input"
          style={{
            position: 'absolute',
            left: -PORT_SIZE / 2,
            top: NODE_HEIGHT / 2 - PORT_SIZE / 2,
            width: PORT_SIZE,
            height: PORT_SIZE,
            borderRadius: '50%',
            background: 'var(--surface)',
            border: '2px solid var(--text-muted)',
          }}
        />
      )}

      {/* Output ports — drag from here to draw a connection */}
      {outputs.map((port, index) => {
        const offset = outputs.length === 2 ? (index === 0 ? -16 : 16) : 0
        return (
          <div key={port.handle ?? 'main'} style={{ position: 'absolute', right: -PORT_SIZE / 2, top: NODE_HEIGHT / 2 + offset - PORT_SIZE / 2 }}>
            <div
              data-port="output"
              onPointerDown={e => {
                e.stopPropagation()
                onPointerDownOutput(e, node.id, port.handle)
              }}
              title="Bağlantı çizmek için sürükleyin"
              style={{
                width: PORT_SIZE,
                height: PORT_SIZE,
                borderRadius: '50%',
                background: 'var(--primary)',
                border: '2px solid var(--surface)',
                cursor: 'crosshair',
                touchAction: 'none',
              }}
            />
            {port.label && (
              <span
                style={{
                  position: 'absolute',
                  left: PORT_SIZE + 4,
                  top: -3,
                  fontSize: '10px',
                  fontWeight: 600,
                  color: 'var(--text-muted)',
                  whiteSpace: 'nowrap',
                  pointerEvents: 'none',
                }}
              >
                {port.label}
              </span>
            )}
          </div>
        )
      })}
    </div>
  )
}
