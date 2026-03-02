'use client'

import { useState, useEffect, useCallback, ChangeEvent } from 'react'
import apiClient from '@/lib/axios'
import {
    Search,
    Globe,
    Settings,
    Plus,
    Save,
    X,
    Flame,
    Download,
    ChevronLeft,
    ChevronRight,
    Filter,
    CheckCircle,
    Pencil,
    Trash2
} from 'lucide-react'

interface DomainFeature {
    id: number
    domain: string
    has_nda: boolean
    is_unknown: boolean
    is_personal: boolean
    istirak_domain: boolean
    egitim: boolean
    noter: boolean
    hukuk: boolean
    denetim: boolean
    banka: boolean
    custom_features: Record<string, boolean>
    incident_count?: number
    incident_stats?: {
        actions: Record<string, number>
        teams: Record<string, number>
    }
}

// Helper component for Incident Count Tooltip
const IncidentCountCell = ({ count, stats }: { count: number, stats?: DomainFeature['incident_stats'] }) => {
    const [showTooltip, setShowTooltip] = useState(false)

    if (!stats || count === 0) return <span style={{ fontWeight: 'bold' }}>{count || 0}</span>

    return (
        <div
            style={{ position: 'relative', display: 'inline-block' }}
            onMouseEnter={() => setShowTooltip(true)}
            onMouseLeave={() => setShowTooltip(false)}
        >
            <span style={{ fontWeight: 'bold', cursor: 'help', textDecoration: 'underline dotted', color: '#2563eb' }}>
                {count}
            </span>

            {showTooltip && (
                <div style={{
                    position: 'absolute',
                    top: '100%', // Show below
                    left: '50%',
                    transform: 'translateX(-50%)',
                    marginTop: '8px', // Margin from top
                    background: 'white',
                    border: '1px solid #e5e7eb',
                    borderRadius: '8px',
                    boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
                    padding: '12px',
                    zIndex: 50,
                    minWidth: '220px',
                    maxWidth: '300px',
                    textAlign: 'left'
                }}>
                    {/* Arrow (Pointing Up) */}
                    <div style={{
                        position: 'absolute',
                        bottom: '100%', // Arrow at top of tooltip
                        left: '50%',
                        marginLeft: '-6px',
                        borderWidth: '6px',
                        borderStyle: 'solid',
                        borderColor: 'transparent transparent white transparent' // Point Up
                    }}></div>

                    <div style={{ display: 'flex', gap: '16px' }}>
                        {/* Actions Section */}
                        <div style={{ flex: 1 }}>
                            <h4 style={{ margin: '0 0 8px 0', fontSize: '11px', textTransform: 'none', color: '#374151', fontWeight: '800' }}>Aksiyonlar</h4>
                            {Object.entries(stats.actions).sort((a, b) => b[1] - a[1]).map(([action, val]) => (
                                <div key={action} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', marginBottom: '4px' }}>
                                    <span style={{
                                        fontWeight: '600',
                                        color: action === 'BLOCK' ? '#dc2626' : action === 'PERMIT' || action === 'AUTHORIZED' ? '#059669' : '#111827'
                                    }}>
                                        {action}
                                    </span>
                                    <span style={{ fontWeight: '700', color: '#000000' }}>{val}</span>
                                </div>
                            ))}
                        </div>

                        {/* Divider */}
                        <div style={{ width: '1px', background: '#d1d5db' }}></div>

                        {/* Teams Section */}
                        <div style={{ flex: 1 }}>
                            <h4 style={{ margin: '0 0 8px 0', fontSize: '11px', textTransform: 'none', color: '#374151', fontWeight: '800' }}>Ekipler</h4>
                            {Object.entries(stats.teams).sort((a, b) => b[1] - a[1]).slice(0, 5).map(([team, val]) => (
                                <div key={team} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', marginBottom: '4px' }}>
                                    <span style={{ color: '#111827', fontWeight: '600', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', maxWidth: '90px' }} title={team}>
                                        {team || 'Diğer'}
                                    </span>
                                    <span style={{ fontWeight: '700', color: '#000000' }}>{val}</span>
                                </div>
                            ))}
                            {Object.keys(stats.teams).length > 5 && (
                                <div style={{ fontSize: '10px', color: '#4b5563', fontWeight: '600', marginTop: '2px' }}>
                                    + {Object.keys(stats.teams).length - 5} diğer
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    )
}

interface ColumnDef {
    name: string
    display_name: string
    key: string
    is_static: boolean
    id?: number
}

interface DomainFeaturesManagerProps {
    onClose?: () => void
}

export default function DomainFeaturesManager({ onClose }: DomainFeaturesManagerProps) {
    const [domains, setDomains] = useState<DomainFeature[]>([])
    const [columns, setColumns] = useState<ColumnDef[]>([])
    const [loading, setLoading] = useState(true)
    const [saving, setSaving] = useState(false)

    // Pagination
    const [page, setPage] = useState(1)
    const [pageSize] = useState(150)
    const [total, setTotal] = useState(0)
    const [totalPages, setTotalPages] = useState(0)

    // State
    const [search, setSearch] = useState('')
    const [onlyFlagged, setOnlyFlagged] = useState(true)
    const [viewMode, setViewMode] = useState<'standard' | 'top'>('standard')
    const [selectedFilters, setSelectedFilters] = useState<string[]>([])

    const [modifiedIds, setModifiedIds] = useState<Set<number>>(new Set())
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

    // Add/Edit Column State
    const [showAddModal, setShowAddModal] = useState(false)
    const [showEditModal, setShowEditModal] = useState(false)
    const [editingColumn, setEditingColumn] = useState<ColumnDef | null>(null)
    const [columnNameInput, setColumnNameInput] = useState('')
    const [submittingColumn, setSubmittingColumn] = useState(false)
    const [deletingColumn, setDeletingColumn] = useState(false)

    const fetchColumns = async () => {
        try {
            const res = await apiClient.get('/api/domain-features/columns')
            setColumns(res.data)
        } catch (err) {
            console.error('Failed to fetch columns', err)
        }
    }

    const fetchDomains = useCallback(async () => {
        setLoading(true)
        try {
            let endpoint = '/api/domain-features'
            const params: any = {}

            if (viewMode === 'top') {
                endpoint = '/api/domain-features/top'
                params.limit = pageSize
            } else {
                params.page = page
                params.pageSize = pageSize
                params.search = search || undefined

                // Column-based filtering takes priority
                if (selectedFilters.length > 0) {
                    params.filterColumns = selectedFilters.join(',')
                } else if (!search) {
                    params.onlyFlagged = onlyFlagged
                }
            }

            const res = await apiClient.get(endpoint, { params })

            if (viewMode === 'top') {
                setDomains(res.data.domains)
                setTotal(res.data.domains.length)
                setTotalPages(1)
            } else {
                setDomains(res.data.domains)
                setTotal(res.data.pagination.total)
                setTotalPages(res.data.pagination.totalPages)
            }

        } catch (err) {
            console.error('Failed to fetch domains', err)
            setMessage({ type: 'error', text: 'Domain verileri yüklenemedi' })
        } finally {
            setLoading(false)
        }
    }, [page, pageSize, search, onlyFlagged, viewMode, selectedFilters])

    useEffect(() => {
        fetchColumns()
    }, [])

    useEffect(() => {
        fetchDomains()
    }, [fetchDomains])

    // Arama yapıldığında otomatik olarak flagged filtresini kaldır ve standard moda geç
    const handleSearchChange = (e: ChangeEvent<HTMLInputElement>) => {
        const val = e.target.value
        setSearch(val)
        if (val) {
            setViewMode('standard')
            setSelectedFilters([]) // Clear column filters when searching
        }
    }

    // Toggle column filter
    const toggleColumnFilter = (colKey: string) => {
        setSelectedFilters(prev => {
            if (prev.includes(colKey)) {
                return prev.filter(k => k !== colKey)
            } else {
                return [...prev, colKey]
            }
        })
        setPage(1) // Reset to first page
        setViewMode('standard')
    }

    // Clear all column filters
    const clearColumnFilters = () => {
        setSelectedFilters([])
        setPage(1)
    }

    const handleToggle = (domainId: number, col: ColumnDef) => {
        setDomains(prev =>
            prev.map(d => {
                if (d.id === domainId) {
                    if (col.is_static) {
                        return { ...d, [col.key]: !d[col.key as keyof DomainFeature] }
                    } else {
                        const custom_features = d.custom_features ? { ...d.custom_features } : {}
                        custom_features[col.key] = !custom_features[col.key]
                        return { ...d, custom_features }
                    }
                }
                return d
            })
        )
        setModifiedIds(prev => new Set(prev).add(domainId))
    }

    const handleBulkSave = async () => {
        if (modifiedIds.size === 0) {
            setMessage({ type: 'error', text: 'Kaydedilecek değişiklik yok' })
            return
        }

        setSaving(true)
        try {
            const updates = domains
                .filter(d => modifiedIds.has(d.id))
                .map(d => ({
                    id: d.id,
                    has_nda: d.has_nda,
                    is_personal: d.is_personal,
                    istirak_domain: d.istirak_domain,
                    egitim: d.egitim,
                    noter: d.noter,
                    hukuk: d.hukuk,
                    denetim: d.denetim,
                    banka: d.banka,
                    custom_features: d.custom_features
                }))

            await apiClient.post('/api/domain-features/bulk-save', updates)
            setModifiedIds(new Set())
            setMessage({ type: 'success', text: `${updates.length} domain güncellendi` })
            
            // Refresh data from server to ensure consistency with DB
            fetchDomains()
        } catch (err) {
            console.error('Failed to save', err)
            setMessage({ type: 'error', text: 'Kaydetme başarısız' })
        } finally {
            setSaving(false)
        }
    }

    const handleExtractFromIncidents = async () => {
        try {
            const res = await apiClient.post('/api/domain-features/extract-from-incidents')
            setMessage({ type: 'success', text: `${res.data.added} yeni domain eklendi` })
            fetchDomains()
        } catch (err) {
            console.error('Failed to extract', err)
            setMessage({ type: 'error', text: 'Domain çıkarma başarısız' })
        }
    }

    const handleAddColumn = async () => {
        if (!columnNameInput.trim()) return

        setSubmittingColumn(true)
        try {
            const res = await apiClient.post('/api/domain-features/columns', { display_name: columnNameInput })
            const newCol = res.data
            setColumns(prev => [...prev, newCol])
            setColumnNameInput('')
            setShowAddModal(false)
            setMessage({ type: 'success', text: 'Yeni özellik eklendi' })
        } catch (err) {
            console.error('Failed to add column', err)
            setMessage({ type: 'error', text: 'Özellik eklenemedi' })
        } finally {
            setSubmittingColumn(false)
        }
    }

    const handleUpdateColumn = async () => {
        if (!columnNameInput.trim() || !editingColumn || !editingColumn.id) return

        setSubmittingColumn(true)
        try {
            await apiClient.put(`/api/domain-features/columns/${editingColumn.id}`, { display_name: columnNameInput })

            setColumns(prev => prev.map(c => c.id === editingColumn.id ? { ...c, display_name: columnNameInput } : c))
            setColumnNameInput('')
            setEditingColumn(null)
            setShowEditModal(false)
            setMessage({ type: 'success', text: 'Özellik ismi güncellendi' })
        } catch (err) {
            console.error('Failed to update column', err)
            setMessage({ type: 'error', text: 'Güncelleme başarısız' })
        } finally {
            setSubmittingColumn(false)
        }
    }

    const handleDeleteColumn = async () => {
        if (!editingColumn || !editingColumn.id) return

        if (!window.confirm(`"${editingColumn.display_name}" özelliğini silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`)) {
            return
        }

        setDeletingColumn(true)
        try {
            await apiClient.delete(`/api/domain-features/columns/${editingColumn.id}`)

            setColumns(prev => prev.filter(c => c.id !== editingColumn.id))
            setSelectedFilters(prev => prev.filter(k => k !== editingColumn.key))
            setColumnNameInput('')
            setEditingColumn(null)
            setShowEditModal(false)
            setMessage({ type: 'success', text: 'Özellik silindi' })

            fetchDomains()
        } catch (err) {
            console.error('Failed to delete column', err)
            setMessage({ type: 'error', text: 'Silme işlemi başarısız' })
        } finally {
            setDeletingColumn(false)
        }
    }


    const openEditModal = (col: ColumnDef, e: React.MouseEvent) => {
        e.stopPropagation() // Prevent triggering column filter
        if (col.is_static) return
        setEditingColumn(col)
        setColumnNameInput(col.display_name)
        setShowEditModal(true)
    }

    const getToggleStyle = (value: boolean) => ({
        padding: '4px 12px',
        borderRadius: '12px',
        border: 'none',
        cursor: 'pointer',
        fontSize: '12px',
        fontWeight: '600' as const,
        background: value ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)',
        color: value ? '#10b981' : '#ef4444',
        transition: 'all 0.2s',
        minWidth: '60px'
    })

    // Determine value for a cell
    const getValue = (domain: DomainFeature, col: ColumnDef) => {
        if (col.is_static) {
            return !!domain[col.key as keyof DomainFeature]
        }
        return !!domain.custom_features?.[col.key]
    }

    return (
        <div style={{
            background: 'var(--surface)',
            borderRadius: '12px',
            border: '1px solid var(--border)',
            padding: '24px',
            boxShadow: '0 2px 12px rgba(0,0,0,0.08)',
            borderTop: '3px solid #3b82f6'
        }}>
            {/* Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '0' }}>
                        <div style={{
                            width: '32px',
                            height: '32px',
                            borderRadius: '8px',
                            background: 'linear-gradient(135deg, #3b82f6, #06b6d4)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            boxShadow: '0 3px 10px rgba(59, 130, 246, 0.25)'
                        }}>
                            <Settings size={17} color="#fff" />
                        </div>
                        <h2 style={{ fontSize: '20px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #3b82f6, #06b6d4)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                            Domain Features Manager
                        </h2>
                    </div>
                    <div style={{ display: 'flex', gap: '12px', marginTop: '8px', alignItems: 'center' }}>
                        <button
                            onClick={() => {
                                setViewMode('standard')
                                setOnlyFlagged(true)
                                setSearch('')
                                setSelectedFilters([])
                                setPage(1)
                            }}
                            style={{
                                fontSize: '13px',
                                color: viewMode === 'standard' && onlyFlagged && selectedFilters.length === 0 ? '#2563eb' : 'var(--text-muted)',
                                fontWeight: viewMode === 'standard' && onlyFlagged && selectedFilters.length === 0 ? '600' : '400',
                                background: 'none', border: 'none', cursor: 'pointer', padding: 0,
                                textDecoration: viewMode === 'standard' && onlyFlagged && selectedFilters.length === 0 ? 'underline' : 'none'
                            }}
                        >
                            İşaretliler
                        </button>
                        <button
                            onClick={() => {
                                setViewMode('standard')
                                setOnlyFlagged(false)
                                setSearch('')
                                setSelectedFilters([])
                                setPage(1)
                            }}
                            style={{
                                fontSize: '13px',
                                color: viewMode === 'standard' && !onlyFlagged && selectedFilters.length === 0 ? '#2563eb' : 'var(--text-muted)',
                                fontWeight: viewMode === 'standard' && !onlyFlagged && selectedFilters.length === 0 ? '600' : '400',
                                background: 'none', border: 'none', cursor: 'pointer', padding: 0,
                                textDecoration: viewMode === 'standard' && !onlyFlagged && selectedFilters.length === 0 ? 'underline' : 'none'
                            }}
                        >
                            Tüm Liste
                        </button>
                        {selectedFilters.length > 0 && (
                            <span style={{
                                fontSize: '12px',
                                color: '#2563eb',
                                background: 'rgba(37, 99, 235, 0.08)',
                                padding: '4px 10px',
                                borderRadius: '16px',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '6px',
                                border: '1px solid rgba(37, 99, 235, 0.15)',
                                fontWeight: '600'
                            }}>
                                <Filter size={11} /> {selectedFilters.length} filtre aktif
                                <button
                                    onClick={clearColumnFilters}
                                    style={{
                                        background: 'none', border: 'none', cursor: 'pointer',
                                        color: '#2563eb', fontSize: '14px', padding: 0, display: 'flex', alignItems: 'center'
                                    }}
                                    title="Filtreleri Temizle"
                                >
                                    <X size={12} />
                                </button>
                            </span>
                        )}
                    </div>
                </div>

                <div style={{ display: 'flex', gap: '10px' }}>
                    <button
                        onClick={() => {
                            if (viewMode === 'top') {
                                setViewMode('standard')
                                setOnlyFlagged(true)
                                setSearch('')
                                setSelectedFilters([])
                                setPage(1)
                            } else {
                                setViewMode('top')
                                setSearch('')
                                setOnlyFlagged(false)
                                setSelectedFilters([])
                            }
                        }}
                        style={{
                            padding: '9px 16px',
                            borderRadius: '8px',
                            border: viewMode === 'top' ? '2px solid #f59e0b' : '1px solid var(--border)',
                            background: viewMode === 'top' ? 'rgba(245, 158, 11, 0.08)' : 'var(--background)',
                            color: viewMode === 'top' ? '#b45309' : 'var(--text-primary)',
                            fontSize: '13px',
                            fontWeight: '600',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px'
                        }}
                    >
                        <Flame size={14} /> Sık Kullanılanlar
                    </button>

                    <button
                        onClick={() => {
                            setColumnNameInput('')
                            setShowAddModal(true)
                        }}
                        style={{
                            padding: '9px 16px',
                            borderRadius: '8px',
                            border: '1px dashed rgba(139, 92, 246, 0.4)',
                            background: 'rgba(139, 92, 246, 0.04)',
                            color: '#8b5cf6',
                            fontSize: '13px',
                            fontWeight: '600',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px'
                        }}
                    >
                        <Plus size={14} /> Özellik Ekle
                    </button>

                    <button
                        onClick={handleExtractFromIncidents}
                        style={{
                            padding: '9px 16px',
                            borderRadius: '8px',
                            border: '1px solid rgba(6, 182, 212, 0.3)',
                            background: 'rgba(6, 182, 212, 0.04)',
                            color: '#0891b2',
                            fontSize: '13px',
                            fontWeight: '600',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px'
                        }}
                    >
                        <Download size={14} /> Incident'lardan Çıkar
                    </button>

                    <button
                        onClick={handleBulkSave}
                        disabled={saving || modifiedIds.size === 0}
                        style={{
                            padding: '9px 20px',
                            borderRadius: '8px',
                            border: 'none',
                            background: modifiedIds.size > 0 ? 'linear-gradient(135deg, #10b981, #059669)' : 'var(--surface-hover)',
                            color: modifiedIds.size > 0 ? 'white' : 'var(--text-muted)',
                            fontSize: '13px',
                            fontWeight: '600',
                            cursor: modifiedIds.size > 0 ? 'pointer' : 'default',
                            opacity: saving ? 0.5 : 1,
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            boxShadow: modifiedIds.size > 0 ? '0 3px 10px rgba(16, 185, 129, 0.3)' : 'none'
                        }}
                    >
                        <Save size={14} />
                        {saving ? 'Kaydediliyor...' : `Kaydet (${modifiedIds.size})`}
                    </button>

                    {onClose && (
                        <button
                            onClick={onClose}
                            style={{
                                padding: '9px 16px',
                                borderRadius: '8px',
                                border: '1px solid var(--border)',
                                background: 'var(--background)',
                                color: 'var(--text-secondary)',
                                fontSize: '13px',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '6px'
                            }}
                        >
                            <X size={14} /> Kapat
                        </button>
                    )}
                </div>
            </div>

            {/* Message */}
            {message && (
                <div style={{
                    padding: '12px 16px',
                    borderRadius: '8px',
                    marginBottom: '16px',
                    background: message.type === 'success' ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                    color: message.type === 'success' ? '#10b981' : '#ef4444',
                    fontSize: '14px'
                }}>
                    {message.text}
                </div>
            )}

            {/* Add/Edit Modal */}
            {(showAddModal || showEditModal) && (
                <div style={{
                    position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
                    background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100
                }}>
                    <div style={{
                        background: 'var(--surface)', padding: '24px', borderRadius: '12px', width: '400px',
                        boxShadow: '0 4px 20px rgba(0,0,0,0.2)'
                    }}>
                        <h3 style={{ marginTop: 0, color: 'var(--text-primary)' }}>
                            {showEditModal ? 'Özellik Düzenle' : 'Yeni Özellik Ekle'}
                        </h3>
                        <p style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                            {showEditModal
                                ? 'Sınıflandırma ismini güncelleyebilir veya silebilirsiniz.'
                                : 'Yeni özellik ekleyin. Varsayılan olarak "Hayır" olacaktır.'}
                        </p>
                        <input
                            type="text"
                            placeholder="Özellik Adı"
                            value={columnNameInput}
                            onChange={(e) => setColumnNameInput(e.target.value)}
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border)',
                                background: 'var(--background)', color: 'var(--text-primary)', marginBottom: '16px'
                            }}
                        />
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div>
                                {showEditModal && (
                                    <button
                                        onClick={handleDeleteColumn}
                                        disabled={deletingColumn}
                                        style={{
                                            padding: '8px 16px', borderRadius: '6px', border: '1px solid #ef4444',
                                            background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', cursor: 'pointer',
                                            fontSize: '13px', fontWeight: '500'
                                        }}
                                    >
                                        <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                            <Trash2 size={13} /> {deletingColumn ? 'Siliniyor...' : 'Sil'}
                                        </span>
                                    </button>
                                )}
                            </div>
                            <div style={{ display: 'flex', gap: '8px' }}>
                                <button
                                    onClick={() => {
                                        setShowAddModal(false)
                                        setShowEditModal(false)
                                        setEditingColumn(null)
                                    }}
                                    style={{
                                        padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)',
                                        background: 'transparent', color: 'var(--text-primary)', cursor: 'pointer'
                                    }}
                                >
                                    İptal
                                </button>
                                <button
                                    onClick={showEditModal ? handleUpdateColumn : handleAddColumn}
                                    disabled={submittingColumn || !columnNameInput}
                                    style={{
                                        padding: '8px 16px', borderRadius: '6px', border: 'none',
                                        background: '#3b82f6', color: 'white', cursor: 'pointer', opacity: submittingColumn ? 0.7 : 1
                                    }}
                                >
                                    {submittingColumn ? 'Kaydediliyor...' : 'Kaydet'}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Search & Info */}
            <div style={{
                marginBottom: '16px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '14px 16px',
                background: 'rgba(59, 130, 246, 0.03)',
                borderRadius: '10px',
                border: '1px solid rgba(59, 130, 246, 0.08)'
            }}>
                <div style={{ position: 'relative', width: '380px' }}>
                    <Search size={15} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
                    <input
                        type="text"
                        placeholder="Domain ara (otomatik tüm listede arar)..."
                        value={search}
                        onChange={handleSearchChange}
                        style={{
                            width: '100%',
                            padding: '10px 14px 10px 36px',
                            borderRadius: '8px',
                            border: '1px solid var(--border)',
                            background: 'var(--background)',
                            color: 'var(--text-primary)',
                            fontSize: '14px'
                        }}
                    />
                </div>
                <div style={{ textAlign: 'right' }}>
                    <span style={{ color: 'var(--text-muted)', fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px', justifyContent: 'flex-end' }}>
                        {viewMode === 'top' && <Flame size={13} style={{ color: '#f59e0b' }} />}
                        {selectedFilters.length > 0 && <Filter size={13} style={{ color: '#3b82f6' }} />}
                        {viewMode === 'top'
                            ? `En çok kullanılan ${total} domain listeleniyor`
                            : selectedFilters.length > 0
                                ? `${total} domain (${selectedFilters.length} filtre aktif)`
                                : onlyFlagged && !search
                                    ? `Filtrelenmiş ${total} domain (Sadece işaretliler)`
                                    : `Toplam ${total} domain`}
                    </span>
                    {selectedFilters.length === 0 && onlyFlagged && !search && viewMode === 'standard' && (
                        <span style={{ fontSize: '11px', color: '#f59e0b', marginTop: '4px', display: 'block' }}>
                            * Sütun başlığına tıklayarak filtreleyebilirsiniz
                        </span>
                    )}
                </div>
            </div>

            {/* Table */}
            <div style={{ overflowX: 'auto', maxHeight: '600px', overflowY: 'auto', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                    <thead style={{ position: 'sticky', top: 0, zIndex: 10 }}>
                        <tr style={{ background: 'rgba(59, 130, 246, 0.06)', borderBottom: '2px solid rgba(59, 130, 246, 0.15)' }}>
                            <th style={{ padding: '12px', textAlign: 'left', fontSize: '13px', fontWeight: '700', color: 'var(--text-primary)', minWidth: '200px' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                    <Globe size={14} style={{ color: '#3b82f6' }} />
                                    Domain
                                </div>
                            </th>
                            {viewMode === 'top' && (
                                <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '700', color: 'var(--text-primary)', minWidth: '100px' }}>
                                    Incident #
                                </th>
                            )}
                            {columns.map(col => {
                                const isFiltered = selectedFilters.includes(col.key)
                                return (
                                    <th
                                        key={col.key}
                                        onClick={() => toggleColumnFilter(col.key)}
                                        style={{
                                            padding: '12px',
                                            textAlign: 'center',
                                            fontSize: '13px',
                                            fontWeight: '700',
                                            color: isFiltered ? '#2563eb' : 'var(--text-primary)',
                                            minWidth: '100px',
                                            cursor: 'pointer',
                                            background: isFiltered ? 'rgba(37, 99, 235, 0.12)' : 'transparent',
                                            transition: 'all 0.2s'
                                        }}
                                        title={`Tıklayarak "${col.display_name}" sütununda Evet olanları filtreleyin`}
                                    >
                                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '5px' }}>
                                            {isFiltered && <CheckCircle size={12} style={{ color: '#2563eb' }} />}
                                            {col.display_name}
                                            {!col.is_static && (
                                                <button
                                                    onClick={(e) => openEditModal(col, e)}
                                                    style={{
                                                        background: 'none', border: 'none', cursor: 'pointer',
                                                        color: 'var(--text-muted)', padding: '2px', display: 'flex', alignItems: 'center'
                                                    }}
                                                    title="Düzenle"
                                                >
                                                    <Pencil size={11} />
                                                </button>
                                            )}
                                        </div>
                                    </th>
                                )
                            })}
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? (
                            <tr>
                                <td colSpan={columns.length + 2} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                                    Yükleniyor...
                                </td>
                            </tr>
                        ) : domains.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length + 2} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                                    Domain bulunamadı
                                </td>
                            </tr>
                        ) : (
                            domains.map(domain => (
                                <tr
                                    key={domain.id}
                                    style={{
                                        borderBottom: '1px solid var(--border)',
                                        background: modifiedIds.has(domain.id) ? 'rgba(59, 130, 246, 0.05)' : 'transparent'
                                    }}
                                >
                                    <td style={{ padding: '10px 12px', fontSize: '14px', color: 'var(--text-primary)' }}>
                                        {domain.domain}
                                        {domain.is_unknown && (
                                            <span style={{ marginLeft: '8px', fontSize: '11px', color: '#f59e0b', fontWeight: '600' }}>YENİ</span>
                                        )}
                                    </td>
                                    {viewMode === 'top' && (
                                        <td style={{ padding: '10px 12px', textAlign: 'center', fontSize: '13px' }}>
                                            <IncidentCountCell count={domain.incident_count || 0} stats={domain.incident_stats} />
                                        </td>
                                    )}
                                    {columns.map(col => {
                                        const val = getValue(domain, col)
                                        return (
                                            <td key={col.key} style={{ padding: '10px 12px', textAlign: 'center' }}>
                                                <button onClick={() => handleToggle(domain.id, col)} style={getToggleStyle(val)}>
                                                    {val ? 'Evet' : 'Hayır'}
                                                </button>
                                            </td>
                                        )
                                    })}
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {/* Pagination */}
            {viewMode === 'standard' && (
                <div style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginTop: '20px',
                    padding: '14px 16px',
                    borderRadius: '10px',
                    background: 'rgba(59, 130, 246, 0.03)',
                    border: '1px solid rgba(59, 130, 246, 0.08)'
                }}>
                    <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>
                        Sayfa <strong style={{ color: '#3b82f6' }}>{page}</strong> / {totalPages} ({total} domain)
                    </span>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button
                            onClick={() => setPage(p => Math.max(1, p - 1))}
                            disabled={page === 1}
                            style={{
                                padding: '8px 16px',
                                borderRadius: '6px',
                                border: '1px solid var(--border)',
                                background: page === 1 ? 'var(--background)' : 'var(--surface)',
                                color: page === 1 ? 'var(--text-muted)' : 'var(--text-primary)',
                                cursor: page === 1 ? 'default' : 'pointer',
                                fontSize: '13px',
                                fontWeight: '500',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px'
                            }}
                        >
                            <ChevronLeft size={14} /> Önceki
                        </button>
                        <button
                            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                            disabled={page === totalPages}
                            style={{
                                padding: '8px 16px',
                                borderRadius: '6px',
                                border: '1px solid var(--border)',
                                background: page === totalPages ? 'var(--background)' : 'var(--surface)',
                                color: page === totalPages ? 'var(--text-muted)' : 'var(--text-primary)',
                                cursor: page === totalPages ? 'default' : 'pointer',
                                fontSize: '13px',
                                fontWeight: '500',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px'
                            }}
                        >
                            Sonraki <ChevronRight size={14} />
                        </button>
                    </div>
                </div>
            )}
        </div>
    )
}
