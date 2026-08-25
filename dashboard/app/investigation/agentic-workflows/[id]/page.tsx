'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import type { CSSProperties, ReactNode } from 'react'
import { useParams, useRouter } from 'next/navigation'
import {
  ArrowLeft,
  Save,
  Play,
  Send,
  AlertTriangle,
  CheckCircle2,
  History,
  ListChecks,
  Workflow,
  Loader2,
} from 'lucide-react'
import apiClient from '@/lib/axios'
import type { MailTemplate } from '@/components/investigation/types'
import PlaybookCanvas from '@/components/investigation/playbook/PlaybookCanvas'
import NodePalette from '@/components/investigation/playbook/NodePalette'
import NodeInspector from '@/components/investigation/playbook/NodeInspector'
import RunHistoryPanel from '@/components/investigation/playbook/RunHistoryPanel'
import PlaybookReport from '@/components/investigation/playbook/PlaybookReport'
import {
  NODE_WIDTH,
  createNode,
  describeSchedule,
  isTriggerType,
  newId,
  nodeDefinition,
  reachableFrom,
  validateGraph,
  wouldCreateCycle,
  type PlaybookDetail,
  type PlaybookGraph,
  type PlaybookNode,
  type PlaybookNodeLog,
  type PlaybookNodeType,
  type PlaybookRun,
} from '@/components/investigation/playbook/types'
import {
  primaryButtonStyle,
  secondaryButtonStyle,
  disabled as withDisabled,
} from '@/components/investigation/playbook/formStyles'

type Tab = 'canvas' | 'runs' | 'report'

const EMPTY_GRAPH: PlaybookGraph = { nodes: [], edges: [] }

export default function PlaybookEditorPage() {
  const params = useParams()
  const router = useRouter()
  const playbookId = Number(params?.id)

  const [playbook, setPlaybook] = useState<PlaybookDetail | null>(null)
  const [graph, setGraph] = useState<PlaybookGraph>(EMPTY_GRAPH)
  const [name, setName] = useState('')
  const [autoSend, setAutoSend] = useState(false)
  const [enabled, setEnabled] = useState(false)
  const [dirty, setDirty] = useState(false)

  const [templates, setTemplates] = useState<MailTemplate[]>([])
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null)
  const [armedType, setArmedType] = useState<PlaybookNodeType | null>(null)

  const [tab, setTab] = useState<Tab>('canvas')
  const [runs, setRuns] = useState<PlaybookRun[]>([])
  const [runsLoading, setRunsLoading] = useState(false)
  const [expandedRun, setExpandedRun] = useState<PlaybookRun | null>(null)
  const [lastRunStatuses, setLastRunStatuses] = useState<Record<string, PlaybookNodeLog>>({})

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [running, setRunning] = useState(false)
  const [reportKey, setReportKey] = useState(0)
  const [pendingMails, setPendingMails] = useState(0)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  // ── Loading ──────────────────────────────────────────────────────────────

  const applyDetail = (detail: PlaybookDetail) => {
    setPlaybook(detail)
    setGraph(detail.graph ?? EMPTY_GRAPH)
    setName(detail.name)
    setAutoSend(detail.auto_send)
    setEnabled(detail.enabled)
    setDirty(false)
  }

  const fetchPlaybook = useCallback(async () => {
    setLoading(true)
    try {
      const res = await apiClient.get(`/api/playbooks/${playbookId}`)
      applyDetail(res.data)
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Agentic Workflow yüklenemedi' })
    } finally {
      setLoading(false)
    }
  }, [playbookId])

  const fetchRuns = useCallback(async () => {
    setRunsLoading(true)
    try {
      const res = await apiClient.get(`/api/playbooks/${playbookId}/runs`, { params: { limit: 50 } })
      const list: PlaybookRun[] = Array.isArray(res.data) ? res.data : []
      setRuns(list)

      // Tint the canvas with the newest run's per-node outcome.
      if (list.length > 0) {
        const detail = await apiClient.get(`/api/playbooks/runs/${list[0].id}`)
        const log: PlaybookNodeLog[] = detail.data?.run?.node_log ?? []
        setLastRunStatuses(Object.fromEntries(log.map(entry => [entry.node_id, entry])))
      } else {
        setLastRunStatuses({})
      }
    } catch {
      setRuns([])
    } finally {
      setRunsLoading(false)
    }
  }, [playbookId])

  useEffect(() => {
    if (!Number.isFinite(playbookId)) return
    fetchPlaybook()
    fetchRuns()
    apiClient.get('/api/mail-templates')
      .then(res => setTemplates(Array.isArray(res.data) ? res.data : []))
      .catch(() => setTemplates([]))
  }, [playbookId, fetchPlaybook, fetchRuns])

  // Warn before losing unsaved graph edits.
  useEffect(() => {
    if (!dirty) return
    const handler = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  // ── Graph editing ────────────────────────────────────────────────────────

  const updateGraph = (next: PlaybookGraph) => {
    setGraph(next)
    setDirty(true)
  }

  const templateNames = useMemo(
    () => Object.fromEntries(templates.map(t => [t.id, t.name])) as Record<number, string>,
    [templates]
  )

  const validation = useMemo(() => validateGraph(graph), [graph])

  /** Nodes whose own settings are incomplete, so the canvas can flag them. */
  const invalidNodeIds = useMemo(() => {
    const ids = new Set<string>()
    for (const node of graph.nodes) {
      const single = validateGraph({ nodes: [node], edges: [] })
      // Ignore the "needs a trigger" style errors that only apply to the whole graph.
      if (single.errors.some(error => error.includes(`'${node.label}'`))) ids.add(node.id)
    }
    return ids
  }, [graph])

  const hasTrigger = graph.nodes.some(n => isTriggerType(n.type))

  /**
   * Click-to-add: drop the node to the right of the selected one (or the right-most node) and
   * wire it up automatically, so building the common left-to-right chain takes one click.
   */
  const addNode = (type: PlaybookNodeType) => {
    const anchor = selectedNodeId
      ? graph.nodes.find(n => n.id === selectedNodeId)
      : graph.nodes.slice().sort((a, b) => b.x - a.x)[0]

    const x = anchor ? anchor.x + NODE_WIDTH + 60 : 80
    const y = anchor ? anchor.y : 200
    const node = createNode(type, x, y)

    const edges = [...graph.edges]
    const canConnect =
      anchor &&
      (nodeDefinition(type)?.inputs ?? 1) > 0 &&
      (nodeDefinition(anchor.type)?.outputs.length ?? 0) > 0 &&
      !wouldCreateCycle(graph, anchor.id, node.id)

    if (canConnect) {
      const handle = anchor.type === 'logic.condition' ? 'true' : null
      const taken = edges.some(e => e.source === anchor.id && (e.source_handle ?? null) === handle)
      if (!taken) edges.push({ id: newId('e'), source: anchor.id, target: node.id, source_handle: handle })
    }

    updateGraph({ nodes: [...graph.nodes, node], edges })
    setSelectedNodeId(node.id)
    setArmedType(null)
  }

  const placeArmedNode = (x: number, y: number) => {
    if (!armedType) return
    const node = createNode(armedType, x, y)
    updateGraph({ nodes: [...graph.nodes, node], edges: graph.edges })
    setSelectedNodeId(node.id)
    setArmedType(null)
  }

  const updateNode = (updated: PlaybookNode) => {
    updateGraph({ ...graph, nodes: graph.nodes.map(n => (n.id === updated.id ? updated : n)) })
  }

  const selectedNode = graph.nodes.find(n => n.id === selectedNodeId) ?? null

  /**
   * Nodes downstream of an incident metric source carry a single organisation-wide number rather
   * than a user list, so the inspector shows metric tokens and demands a fixed mail recipient.
   */
  const metricFlowNodeIds = useMemo(() => {
    const ids = new Set<string>()
    for (const source of graph.nodes.filter(n => n.type === 'source.incidentMetric')) {
      reachableFrom(source.id, graph).forEach(id => ids.add(id))
    }
    return ids
  }, [graph])

  // ── Actions ──────────────────────────────────────────────────────────────

  const save = async (nextEnabled = enabled) => {
    if (!name.trim()) {
      setMessage({ type: 'error', text: 'Workflow adı zorunludur' })
      return false
    }
    if (nextEnabled && validation.errors.length > 0) {
      setMessage({ type: 'error', text: `Zamanlamayı açmak için akış geçerli olmalı: ${validation.errors.join(' · ')}` })
      return false
    }

    setSaving(true)
    setMessage(null)
    try {
      const res = await apiClient.put(`/api/playbooks/${playbookId}`, {
        name: name.trim(),
        description: playbook?.description ?? null,
        graph,
        enabled: nextEnabled,
        auto_send: autoSend,
      })
      applyDetail(res.data)
      setMessage({ type: 'success', text: 'Agentic Workflow kaydedildi' })
      return true
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Agentic Workflow kaydedilemedi' })
      return false
    } finally {
      setSaving(false)
    }
  }

  const run = async (dryRun: boolean) => {
    if (validation.errors.length > 0) {
      setMessage({ type: 'error', text: `Akış geçerli değil: ${validation.errors.join(' · ')}` })
      return
    }
    if (!dryRun && !window.confirm(
      'Mailler gerçekten gönderilecek. Devam edilsin mi?\n\n' +
      'Önce "Test Et" ile kimlere hangi konuyla mail gideceğini görmeniz önerilir.'
    )) return

    // The backend runs the saved graph, so unsaved edits must land first.
    if (dirty && !(await save())) return

    setRunning(true)
    setMessage(null)
    try {
      const res = await apiClient.post(`/api/playbooks/${playbookId}/run`, null, {
        params: { dry_run: dryRun },
      })
      const result: PlaybookRun = res.data
      setMessage({
        type: result.status === 'failed' ? 'error' : 'success',
        text: result.status === 'failed'
          ? (result.error_message || 'Çalıştırma başarısız')
          : dryRun
            ? `Test tamamlandı: ${result.mails_pending} mail gönderilmeden önizlemeye hazır` +
              (result.mails_skipped ? `, ${result.mails_skipped} kayıt atlandı` : '')
            : `Çalıştırma tamamlandı: ${result.mails_sent} mail gönderildi` +
              (result.mails_failed ? `, ${result.mails_failed} hata` : ''),
      })
      await fetchRuns()
      setReportKey(k => k + 1)
      if (result.mails_pending > 0 || result.mails_sent > 0) setTab('report')
    } catch (e: any) {
      setMessage({ type: 'error', text: e?.response?.data?.detail || 'Çalıştırma başarısız' })
    } finally {
      setRunning(false)
    }
  }

  const loadRunDetail = async (runId: number | null) => {
    if (runId == null) {
      setExpandedRun(null)
      return
    }
    try {
      const res = await apiClient.get(`/api/playbooks/runs/${runId}`)
      setExpandedRun(res.data?.run ?? null)
    } catch {
      setExpandedRun(null)
    }
  }

  const scheduleNode = graph.nodes.find(n => n.type === 'trigger.schedule')

  if (loading) {
    return (
      <div className="dashboard-page">
        <p className="text-muted">Yükleniyor...</p>
      </div>
    )
  }

  if (!playbook) {
    return (
      <div className="dashboard-page">
        <div className="card">
          <p style={{ margin: '0 0 14px', color: 'var(--text-primary)' }}>Agentic Workflow bulunamadı.</p>
          <button onClick={() => router.push('/investigation/agentic-workflows')} style={secondaryButtonStyle}>
            <ArrowLeft size={15} /> Workflow Listesi
          </button>
        </div>
      </div>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh', overflow: 'hidden', background: 'var(--background)' }}>
      {/* Header */}
      <div style={{ background: 'var(--surface)', borderBottom: '1px solid var(--border)', padding: '12px 20px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
          <button
            onClick={() => router.push('/investigation/agentic-workflows')}
            title="Workflow listesi"
            style={{ ...secondaryButtonStyle, padding: '7px 10px' }}
          >
            <ArrowLeft size={15} />
          </button>

          <div
            style={{
              width: '32px',
              height: '32px',
              borderRadius: '9px',
              background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              flexShrink: 0,
            }}
          >
            <Workflow size={17} />
          </div>

          <input
            value={name}
            onChange={e => {
              setName(e.target.value)
              setDirty(true)
            }}
            style={{
              fontSize: '16px',
              fontWeight: 600,
              color: 'var(--text-primary)',
              background: 'transparent',
              border: '1px solid transparent',
              borderRadius: '6px',
              padding: '6px 8px',
              minWidth: '200px',
              maxWidth: '340px',
              flex: '1 1 200px',
            }}
            onFocus={e => { e.currentTarget.style.borderColor = 'var(--border)' }}
            onBlur={e => { e.currentTarget.style.borderColor = 'transparent' }}
          />

          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginLeft: 'auto', flexWrap: 'wrap' }}>
            {dirty && (
              <span style={{ fontSize: '12px', color: '#d97706', fontWeight: 500 }}>Kaydedilmemiş değişiklik</span>
            )}

            <button
              onClick={() => run(true)}
              disabled={running || saving}
              title="Mail göndermeden kaynak, alıcı ve seçilen şablon sonucunu test eder"
              style={withDisabled(secondaryButtonStyle, running || saving)}
            >
              {running ? <Loader2 size={15} style={{ animation: 'spin 1s linear infinite' }} /> : <Play size={15} />}
              Test Et
            </button>

            <button
              onClick={() => run(false)}
              disabled={running || saving}
              title="Mailleri onay beklemeden gönderir"
              style={{
                ...withDisabled(secondaryButtonStyle, running || saving),
                color: '#dc2626',
                borderColor: '#fca5a5',
              }}
            >
              <Send size={15} /> Gönderimli Çalıştır
            </button>

            <button onClick={() => save()} disabled={saving} style={withDisabled(primaryButtonStyle, saving)}>
              <Save size={15} /> {saving ? 'Kaydediliyor...' : 'Kaydet'}
            </button>
          </div>
        </div>

        {/* Settings row */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '18px', marginTop: '10px', flexWrap: 'wrap', fontSize: '12px' }}>
          <label style={toggleLabelStyle}>
            <input
              type="checkbox"
              checked={enabled}
              onChange={e => {
                const next = e.target.checked
                setEnabled(next)
                save(next)
              }}
              disabled={!scheduleNode}
            />
            <span style={{ color: scheduleNode ? 'var(--text-primary)' : 'var(--text-muted)' }}>
              Zamanlama aktif
              {scheduleNode ? ` · ${describeSchedule(scheduleNode.config)}` : ' (Zamanlama node\'u ekleyin)'}
            </span>
          </label>

          <label style={toggleLabelStyle}>
            <input
              type="checkbox"
              checked={autoSend}
              onChange={e => {
                setAutoSend(e.target.checked)
                setDirty(true)
              }}
            />
            <span style={{ color: 'var(--text-primary)' }}>
              Otomatik gönder <span style={{ color: 'var(--text-muted)' }}>(kapalıyken mailler onay bekler)</span>
            </span>
          </label>

          {playbook.next_run_at && enabled && (
            <span style={{ color: 'var(--text-muted)' }}>
              Sıradaki çalıştırma: {new Date(playbook.next_run_at).toLocaleString('tr-TR')}
            </span>
          )}
        </div>

        {/* Tabs */}
        <div style={{ display: 'flex', gap: '4px', marginTop: '12px' }}>
          <TabButton active={tab === 'canvas'} onClick={() => setTab('canvas')} icon={<Workflow size={14} />}>
            Akış
          </TabButton>
          <TabButton active={tab === 'runs'} onClick={() => setTab('runs')} icon={<History size={14} />}>
            Çalıştırma Geçmişi{runs.length > 0 ? ` (${runs.length})` : ''}
          </TabButton>
          <TabButton active={tab === 'report'} onClick={() => setTab('report')} icon={<ListChecks size={14} />}>
            Rapor{pendingMails > 0 ? ` · ${pendingMails} bekliyor` : ''}
          </TabButton>
        </div>
      </div>

      {/* Messages + validation */}
      {(message || validation.errors.length > 0 || validation.warnings.length > 0) && (
        <div style={{ padding: '10px 20px 0', display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {message && (
            <Banner
              tone={message.type === 'success' ? 'success' : 'error'}
              icon={message.type === 'success' ? <CheckCircle2 size={14} /> : <AlertTriangle size={14} />}
              onDismiss={() => setMessage(null)}
            >
              {message.text}
            </Banner>
          )}
          {validation.errors.length > 0 && (
            <Banner tone="error" icon={<AlertTriangle size={14} />}>
              {validation.errors.join(' · ')}
            </Banner>
          )}
          {validation.errors.length === 0 && validation.warnings.length > 0 && (
            <Banner tone="warning" icon={<AlertTriangle size={14} />}>
              {validation.warnings.join(' · ')}
            </Banner>
          )}
        </div>
      )}

      {/* Body */}
      <div style={{ flex: 1, minHeight: 0, display: 'flex', overflow: 'hidden' }}>
        {tab === 'canvas' ? (
          <>
            <NodePalette
              onAdd={addNode}
              onArmDrop={type => setArmedType(current => (current === type ? null : type))}
              armedType={armedType}
              hasTrigger={hasTrigger}
            />
            <div style={{ flex: 1, minWidth: 0 }}>
              <PlaybookCanvas
                graph={graph}
                onChange={updateGraph}
                selectedNodeId={selectedNodeId}
                onSelectNode={setSelectedNodeId}
                runStatuses={lastRunStatuses}
                invalidNodeIds={invalidNodeIds}
                templateNames={templateNames}
                pendingDropType={armedType}
                onDropConsumed={placeArmedNode}
              />
            </div>
            <NodeInspector
              node={selectedNode}
              templates={templates}
              inMetricFlow={selectedNode ? metricFlowNodeIds.has(selectedNode.id) : false}
              onChange={updateNode}
            />
          </>
        ) : (
          <div style={{ flex: 1, overflowY: 'auto', padding: '20px' }}>
            <div className="card">
              {tab === 'runs' ? (
                <RunHistoryPanel
                  runs={runs}
                  loading={runsLoading}
                  expandedRun={expandedRun}
                  onExpand={loadRunDetail}
                  onRefresh={fetchRuns}
                />
              ) : (
                <PlaybookReport
                  playbookId={playbookId}
                  playbookName={playbook.name}
                  refreshKey={reportKey}
                  onPendingCountChange={setPendingMails}
                />
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

// ── Small presentational pieces ────────────────────────────────────────────

function TabButton({
  active,
  onClick,
  icon,
  children,
}: {
  active: boolean
  onClick: () => void
  icon: ReactNode
  children: ReactNode
}) {
  return (
    <button
      onClick={onClick}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '6px',
        padding: '7px 14px',
        fontSize: '13px',
        fontWeight: 500,
        border: 'none',
        borderRadius: '6px',
        cursor: 'pointer',
        background: active ? 'var(--primary)' : 'transparent',
        color: active ? 'white' : 'var(--text-secondary)',
      }}
    >
      {icon} {children}
    </button>
  )
}

function Banner({
  tone,
  icon,
  children,
  onDismiss,
}: {
  tone: 'success' | 'error' | 'warning'
  icon: ReactNode
  children: ReactNode
  onDismiss?: () => void
}) {
  const palette = {
    success: { bg: '#dcfce7', border: '#86efac', color: '#166534' },
    error: { bg: '#fee2e2', border: '#fca5a5', color: '#991b1b' },
    warning: { bg: 'rgba(217,119,6,0.12)', border: 'rgba(217,119,6,0.35)', color: '#92400e' },
  }[tone]

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'flex-start',
        gap: '8px',
        padding: '9px 13px',
        borderRadius: '7px',
        background: palette.bg,
        border: `1px solid ${palette.border}`,
        color: palette.color,
        fontSize: '12px',
        lineHeight: 1.45,
      }}
    >
      <span style={{ display: 'flex', flexShrink: 0, marginTop: '1px' }}>{icon}</span>
      <span style={{ flex: 1 }}>{children}</span>
      {onDismiss && (
        <button
          onClick={onDismiss}
          style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: palette.color, fontSize: '15px', lineHeight: 1, padding: 0 }}
        >
          ×
        </button>
      )}
    </div>
  )
}

const toggleLabelStyle: CSSProperties = {
  display: 'inline-flex',
  alignItems: 'center',
  gap: '7px',
  cursor: 'pointer',
}
