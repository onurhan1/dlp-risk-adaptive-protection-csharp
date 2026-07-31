'use client'

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties, PointerEvent as ReactPointerEvent, ReactNode } from 'react'
import { ZoomIn, ZoomOut, Maximize, Crosshair } from 'lucide-react'
import {
  NODE_WIDTH,
  NODE_HEIGHT,
  edgePath,
  inputPortPosition,
  outputPortPosition,
  newId,
  nodeDefinition,
  wouldCreateCycle,
  type PlaybookGraph,
  type PlaybookNode,
  type PlaybookNodeLog,
  type Point,
} from './types'
import PlaybookNodeCard from './PlaybookNodeCard'

interface Props {
  graph: PlaybookGraph
  onChange: (graph: PlaybookGraph) => void
  selectedNodeId: string | null
  onSelectNode: (nodeId: string | null) => void
  /** Per-node status from the most recent run, keyed by node id. */
  runStatuses?: Record<string, PlaybookNodeLog>
  invalidNodeIds?: Set<string>
  templateNames?: Record<number, string>
  /** Node type dropped from the palette, consumed on the next canvas click. */
  pendingDropType?: string | null
  onDropConsumed?: (x: number, y: number) => void
}

const MIN_ZOOM = 0.35
const MAX_ZOOM = 1.75
const GRID_SIZE = 24

type DragState =
  | { kind: 'pan'; startX: number; startY: number; originX: number; originY: number }
  | { kind: 'node'; nodeId: string; offsetX: number; offsetY: number }
  | { kind: 'connect'; source: string; handle: string | null; cursor: Point }
  | null

export default function PlaybookCanvas({
  graph,
  onChange,
  selectedNodeId,
  onSelectNode,
  runStatuses,
  invalidNodeIds,
  templateNames = {},
  pendingDropType,
  onDropConsumed,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [zoom, setZoom] = useState(1)
  const [pan, setPan] = useState<Point>({ x: 40, y: 20 })
  const [drag, setDrag] = useState<DragState>(null)
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null)

  const nodesById = useMemo(() => {
    const map: Record<string, PlaybookNode> = {}
    for (const node of graph.nodes) map[node.id] = node
    return map
  }, [graph.nodes])

  /** Screen (client) coordinates → graph coordinates. */
  const toGraphPoint = useCallback((clientX: number, clientY: number): Point => {
    const rect = containerRef.current?.getBoundingClientRect()
    if (!rect) return { x: 0, y: 0 }
    return {
      x: (clientX - rect.left - pan.x) / zoom,
      y: (clientY - rect.top - pan.y) / zoom,
    }
  }, [pan.x, pan.y, zoom])

  // ── Background: pan, deselect, and palette drops ─────────────────────────

  const handleBackgroundPointerDown = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (e.button !== 0) return

    if (pendingDropType && onDropConsumed) {
      const point = toGraphPoint(e.clientX, e.clientY)
      onDropConsumed(point.x - NODE_WIDTH / 2, point.y - NODE_HEIGHT / 2)
      return
    }

    onSelectNode(null)
    setSelectedEdgeId(null)
    e.currentTarget.setPointerCapture(e.pointerId)
    setDrag({ kind: 'pan', startX: e.clientX, startY: e.clientY, originX: pan.x, originY: pan.y })
  }

  const handlePointerMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (!drag) return

    if (drag.kind === 'pan') {
      setPan({ x: drag.originX + (e.clientX - drag.startX), y: drag.originY + (e.clientY - drag.startY) })
      return
    }

    if (drag.kind === 'node') {
      const point = toGraphPoint(e.clientX, e.clientY)
      const x = Math.round(point.x - drag.offsetX)
      const y = Math.round(point.y - drag.offsetY)
      onChange({
        ...graph,
        nodes: graph.nodes.map(n => (n.id === drag.nodeId ? { ...n, x, y } : n)),
      })
      return
    }

    if (drag.kind === 'connect') {
      setDrag({ ...drag, cursor: toGraphPoint(e.clientX, e.clientY) })
    }
  }

  /**
   * Pointer capture is held by the container for the duration of a drag, so pointerup is
   * retargeted here rather than to the node under the cursor. Hit-test the drop point instead
   * of relying on the target node receiving its own pointerup.
   */
  const handlePointerUp = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (drag?.kind === 'connect') {
      const element = document.elementFromPoint(e.clientX, e.clientY)
      const targetId = element?.closest('[data-node-id]')?.getAttribute('data-node-id')
      if (targetId) completeConnection(drag, targetId)
    }
    // A connection that did not land on a node is simply abandoned.
    setDrag(null)
  }

  const completeConnection = (pending: Extract<DragState, { kind: 'connect' }>, targetId: string) => {
    const target = nodesById[targetId]
    const source = nodesById[pending.source]

    if (!target || !source) return
    if ((nodeDefinition(target.type)?.inputs ?? 1) === 0) return
    if (target.id === source.id) return
    if (wouldCreateCycle(graph, source.id, target.id)) return

    const duplicate = graph.edges.some(
      edge =>
        edge.source === source.id &&
        edge.target === target.id &&
        (edge.source_handle ?? null) === (pending.handle ?? null)
    )
    if (duplicate) return

    onChange({
      ...graph,
      edges: [
        ...graph.edges,
        { id: newId('e'), source: source.id, target: target.id, source_handle: pending.handle },
      ],
    })
  }

  /**
   * React registers wheel listeners passively, which makes preventDefault a no-op and lets the
   * page scroll while zooming — attach a non-passive listener to the element directly.
   */
  useEffect(() => {
    const element = containerRef.current
    if (!element) return

    const onWheel = (event: WheelEvent) => {
      event.preventDefault()

      const factor = event.deltaY < 0 ? 1.1 : 1 / 1.1
      const next = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom * factor))
      if (next === zoom) return

      // Keep the point under the cursor fixed while zooming.
      const rect = element.getBoundingClientRect()
      const cursorX = event.clientX - rect.left
      const cursorY = event.clientY - rect.top
      setPan({
        x: cursorX - ((cursorX - pan.x) / zoom) * next,
        y: cursorY - ((cursorY - pan.y) / zoom) * next,
      })
      setZoom(next)
    }

    element.addEventListener('wheel', onWheel, { passive: false })
    return () => element.removeEventListener('wheel', onWheel)
  }, [zoom, pan.x, pan.y])

  // ── Nodes ────────────────────────────────────────────────────────────────

  const handleNodePointerDown = (e: ReactPointerEvent<HTMLDivElement>, nodeId: string) => {
    if (e.button !== 0) return
    e.stopPropagation()

    onSelectNode(nodeId)
    setSelectedEdgeId(null)

    const node = nodesById[nodeId]
    if (!node) return

    const point = toGraphPoint(e.clientX, e.clientY)
    containerRef.current?.setPointerCapture(e.pointerId)
    setDrag({ kind: 'node', nodeId, offsetX: point.x - node.x, offsetY: point.y - node.y })
  }

  const handleOutputPointerDown = (
    e: ReactPointerEvent<HTMLDivElement>,
    nodeId: string,
    handle: string | null
  ) => {
    if (e.button !== 0) return
    containerRef.current?.setPointerCapture(e.pointerId)
    setDrag({ kind: 'connect', source: nodeId, handle, cursor: toGraphPoint(e.clientX, e.clientY) })
  }

  const deleteNode = useCallback((nodeId: string) => {
    onChange({
      nodes: graph.nodes.filter(n => n.id !== nodeId),
      edges: graph.edges.filter(e => e.source !== nodeId && e.target !== nodeId),
    })
    onSelectNode(null)
  }, [graph, onChange, onSelectNode])

  const deleteEdge = useCallback((edgeId: string) => {
    onChange({ ...graph, edges: graph.edges.filter(e => e.id !== edgeId) })
    setSelectedEdgeId(null)
  }, [graph, onChange])

  // Delete/Backspace removes whichever element is selected, unless a field has focus.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Delete' && event.key !== 'Backspace') return
      const target = event.target as HTMLElement | null
      if (target && ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)) return

      if (selectedEdgeId) {
        event.preventDefault()
        deleteEdge(selectedEdgeId)
      } else if (selectedNodeId) {
        event.preventDefault()
        deleteNode(selectedNodeId)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [selectedEdgeId, selectedNodeId, deleteEdge, deleteNode])

  // ── View helpers ─────────────────────────────────────────────────────────

  const fitToView = useCallback(() => {
    const rect = containerRef.current?.getBoundingClientRect()
    if (!rect || graph.nodes.length === 0) {
      setZoom(1)
      setPan({ x: 40, y: 20 })
      return
    }

    const minX = Math.min(...graph.nodes.map(n => n.x))
    const minY = Math.min(...graph.nodes.map(n => n.y))
    const maxX = Math.max(...graph.nodes.map(n => n.x + NODE_WIDTH))
    const maxY = Math.max(...graph.nodes.map(n => n.y + NODE_HEIGHT))

    const padding = 60
    const scale = Math.min(
      MAX_ZOOM,
      Math.max(MIN_ZOOM, Math.min(
        (rect.width - padding * 2) / Math.max(1, maxX - minX),
        (rect.height - padding * 2) / Math.max(1, maxY - minY)
      ))
    )

    setZoom(scale)
    setPan({
      x: (rect.width - (maxX - minX) * scale) / 2 - minX * scale,
      y: (rect.height - (maxY - minY) * scale) / 2 - minY * scale,
    })
  }, [graph.nodes])

  // ── Edge geometry ────────────────────────────────────────────────────────

  const edgeShapes = useMemo(() => {
    return graph.edges.flatMap(edge => {
      const source = nodesById[edge.source]
      const target = nodesById[edge.target]
      if (!source || !target) return []

      const from = outputPortPosition(source, edge.source_handle)
      const to = inputPortPosition(target)
      const isFalseBranch = source.type === 'logic.condition' && edge.source_handle === 'false'
      return [{ edge, path: edgePath(from, to), color: isFalseBranch ? '#94a3b8' : 'var(--primary)' }]
    })
  }, [graph.edges, nodesById])

  const pendingPath = useMemo(() => {
    if (!drag || drag.kind !== 'connect') return null
    const source = nodesById[drag.source]
    if (!source) return null
    return edgePath(outputPortPosition(source, drag.handle), drag.cursor)
  }, [drag, nodesById])

  const cursor = drag?.kind === 'pan' ? 'grabbing' : pendingDropType ? 'copy' : 'default'

  return (
    <div
      ref={containerRef}
      onPointerDown={handleBackgroundPointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        overflow: 'hidden',
        background: 'var(--background)',
        backgroundImage:
          'radial-gradient(circle, var(--border) 1px, transparent 1px)',
        backgroundSize: `${GRID_SIZE * zoom}px ${GRID_SIZE * zoom}px`,
        backgroundPosition: `${pan.x}px ${pan.y}px`,
        cursor,
        touchAction: 'none',
      }}
    >
      {/* Transformed graph layer */}
      <div
        style={{
          position: 'absolute',
          left: 0,
          top: 0,
          transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
          transformOrigin: '0 0',
        }}
      >
        {/* Edges — sized generously so the SVG never clips a connection */}
        <svg
          width={12000}
          height={8000}
          style={{ position: 'absolute', left: 0, top: 0, overflow: 'visible', pointerEvents: 'none' }}
        >
          {edgeShapes.map(({ edge, path, color }) => (
            <g key={edge.id} style={{ pointerEvents: 'stroke' }}>
              {/* Wide transparent twin makes the thin edge easy to click */}
              <path
                d={path}
                stroke="transparent"
                strokeWidth={16}
                fill="none"
                style={{ cursor: 'pointer' }}
                onPointerDown={e => {
                  e.stopPropagation()
                  setSelectedEdgeId(edge.id)
                  onSelectNode(null)
                }}
              />
              <path
                d={path}
                stroke={selectedEdgeId === edge.id ? '#ef4444' : color}
                strokeWidth={selectedEdgeId === edge.id ? 3 : 2}
                fill="none"
                strokeLinecap="round"
                pointerEvents="none"
              />
            </g>
          ))}

          {pendingPath && (
            <path
              d={pendingPath}
              stroke="var(--primary)"
              strokeWidth={2}
              strokeDasharray="6 4"
              fill="none"
              pointerEvents="none"
            />
          )}
        </svg>

        {/* Nodes */}
        {graph.nodes.map(node => (
          <PlaybookNodeCard
            key={node.id}
            node={node}
            selected={selectedNodeId === node.id}
            runStatus={runStatuses?.[node.id]?.status}
            invalid={invalidNodeIds?.has(node.id)}
            templateNames={templateNames}
            onPointerDownBody={handleNodePointerDown}
            onPointerDownOutput={handleOutputPointerDown}
            onDelete={deleteNode}
          />
        ))}
      </div>

      {/* Controls */}
      <div
        style={{
          position: 'absolute',
          bottom: '16px',
          left: '16px',
          display: 'flex',
          gap: '6px',
          background: 'var(--surface)',
          border: '1px solid var(--border)',
          borderRadius: '8px',
          padding: '4px',
          boxShadow: '0 2px 8px rgba(15,23,42,0.1)',
        }}
        onPointerDown={e => e.stopPropagation()}
      >
        <CanvasButton title="Uzaklaştır" onClick={() => setZoom(z => Math.max(MIN_ZOOM, z / 1.2))}>
          <ZoomOut size={15} />
        </CanvasButton>
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            padding: '0 6px',
            fontSize: '12px',
            color: 'var(--text-muted)',
            minWidth: '44px',
            justifyContent: 'center',
          }}
        >
          {Math.round(zoom * 100)}%
        </span>
        <CanvasButton title="Yakınlaştır" onClick={() => setZoom(z => Math.min(MAX_ZOOM, z * 1.2))}>
          <ZoomIn size={15} />
        </CanvasButton>
        <CanvasButton title="Ekrana sığdır" onClick={fitToView}>
          <Maximize size={15} />
        </CanvasButton>
        <CanvasButton
          title="Görünümü sıfırla"
          onClick={() => {
            setZoom(1)
            setPan({ x: 40, y: 20 })
          }}
        >
          <Crosshair size={15} />
        </CanvasButton>
      </div>

      {/* Hints */}
      {pendingDropType && (
        <div style={hintStyle}>Node'u yerleştirmek için tuvale tıklayın</div>
      )}
      {!pendingDropType && selectedEdgeId && (
        <div style={hintStyle}>Bağlantıyı kaldırmak için Delete tuşuna basın</div>
      )}
      {!pendingDropType && !selectedEdgeId && graph.nodes.length === 0 && (
        <div style={{ ...hintStyle, left: '50%', transform: 'translateX(-50%)' }}>
          Soldaki paletten bir node ekleyerek başlayın
        </div>
      )}
    </div>
  )
}

function CanvasButton({
  title,
  onClick,
  children,
}: {
  title: string
  onClick: () => void
  children: ReactNode
}) {
  return (
    <button
      title={title}
      onClick={onClick}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '28px',
        height: '28px',
        background: 'transparent',
        border: 'none',
        borderRadius: '6px',
        color: 'var(--text-secondary)',
        cursor: 'pointer',
      }}
    >
      {children}
    </button>
  )
}

const hintStyle: CSSProperties = {
  position: 'absolute',
  top: '16px',
  left: '16px',
  padding: '6px 12px',
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: '8px',
  fontSize: '12px',
  color: 'var(--text-muted)',
  pointerEvents: 'none',
}
