'use client'

import { useState, useEffect, useCallback, ChangeEvent } from 'react'
import apiClient from '@/lib/axios'

interface DomainFeature {
    id: number
    domain: string
    hasNda: boolean
    isUnknown: boolean
    isPersonal: boolean
    istirakDomain: boolean
    egitim: boolean
    noter: boolean
    hukuk: boolean
    denetim: boolean
    banka: boolean
}

interface ColumnDef {
    name: string
    displayName: string
    key: keyof DomainFeature
}

interface DomainFeaturesManagerProps {
    onClose?: () => void
}

export default function DomainFeaturesManager({ onClose }: DomainFeaturesManagerProps) {
    const [domains, setDomains] = useState<DomainFeature[]>([])
    const [columns, setColumns] = useState<ColumnDef[]>([])
    const [loading, setLoading] = useState(true)
    const [saving, setSaving] = useState(false)
    const [page, setPage] = useState(1)
    const [pageSize] = useState(150)
    const [total, setTotal] = useState(0)
    const [totalPages, setTotalPages] = useState(0)
    const [search, setSearch] = useState('')
    const [modifiedIds, setModifiedIds] = useState<Set<number>>(new Set())
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

    const fetchDomains = useCallback(async () => {
        setLoading(true)
        try {
            const res = await apiClient.get('/api/domain-features', {
                params: { page, pageSize, search: search || undefined }
            })
            setDomains(res.data.domains)
            setTotal(res.data.pagination.total)
            setTotalPages(res.data.pagination.totalPages)
        } catch (err) {
            console.error('Failed to fetch domains', err)
            setMessage({ type: 'error', text: 'Domain verileri yüklenemedi' })
        } finally {
            setLoading(false)
        }
    }, [page, pageSize, search])

    const fetchColumns = async () => {
        try {
            const res = await apiClient.get('/api/domain-features/columns')
            setColumns(res.data)
        } catch (err) {
            console.error('Failed to fetch columns', err)
        }
    }

    useEffect(() => {
        fetchColumns()
    }, [])

    useEffect(() => {
        fetchDomains()
    }, [fetchDomains])

    const handleToggle = (domainId: number, columnKey: keyof DomainFeature) => {
        setDomains(prev =>
            prev.map(d => {
                if (d.id === domainId) {
                    return { ...d, [columnKey]: !d[columnKey] }
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
                    hasNda: d.hasNda,
                    isPersonal: d.isPersonal,
                    istirakDomain: d.istirakDomain,
                    egitim: d.egitim,
                    noter: d.noter,
                    hukuk: d.hukuk,
                    denetim: d.denetim,
                    banka: d.banka
                }))

            await apiClient.post('/api/domain-features/bulk-save', updates)
            setModifiedIds(new Set())
            setMessage({ type: 'success', text: `${updates.length} domain güncellendi` })
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

    const getToggleStyle = (value: boolean) => ({
        padding: '4px 12px',
        borderRadius: '12px',
        border: 'none',
        cursor: 'pointer',
        fontSize: '12px',
        fontWeight: '600' as const,
        background: value ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)',
        color: value ? '#10b981' : '#ef4444',
        transition: 'all 0.2s'
    })

    return (
        <div style={{
            background: 'var(--surface)',
            borderRadius: '12px',
            border: '1px solid var(--border)',
            padding: '24px',
            boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
        }}>
            {/* Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                <h2 style={{ fontSize: '20px', fontWeight: '700', color: 'var(--text-primary)', margin: 0 }}>
                    Domain Features Manager
                </h2>
                <div style={{ display: 'flex', gap: '12px' }}>
                    <button
                        onClick={handleExtractFromIncidents}
                        style={{
                            padding: '10px 16px',
                            borderRadius: '8px',
                            border: '1px solid var(--border)',
                            background: 'var(--background)',
                            color: 'var(--text-primary)',
                            fontSize: '13px',
                            fontWeight: '500',
                            cursor: 'pointer'
                        }}
                    >
                        🔍 Incident'lardan Çıkar
                    </button>
                    <button
                        onClick={handleBulkSave}
                        disabled={saving || modifiedIds.size === 0}
                        style={{
                            padding: '10px 20px',
                            borderRadius: '8px',
                            border: 'none',
                            background: modifiedIds.size > 0 ? '#10b981' : 'var(--surface-hover)',
                            color: modifiedIds.size > 0 ? 'white' : 'var(--text-muted)',
                            fontSize: '13px',
                            fontWeight: '600',
                            cursor: modifiedIds.size > 0 ? 'pointer' : 'default',
                            opacity: saving ? 0.5 : 1
                        }}
                    >
                        {saving ? 'Kaydediliyor...' : `💾 Kaydet (${modifiedIds.size})`}
                    </button>
                    {onClose && (
                        <button
                            onClick={onClose}
                            style={{
                                padding: '10px 16px',
                                borderRadius: '8px',
                                border: '1px solid var(--border)',
                                background: 'var(--background)',
                                color: 'var(--text-secondary)',
                                fontSize: '13px',
                                cursor: 'pointer'
                            }}
                        >
                            ✕ Kapat
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

            {/* Search */}
            <div style={{ marginBottom: '16px' }}>
                <input
                    type="text"
                    placeholder="Domain ara..."
                    value={search}
                    onChange={(e: ChangeEvent<HTMLInputElement>) => setSearch(e.target.value)}
                    style={{
                        width: '300px',
                        padding: '10px 14px',
                        borderRadius: '8px',
                        border: '1px solid var(--border)',
                        background: 'var(--background)',
                        color: 'var(--text-primary)',
                        fontSize: '14px'
                    }}
                />
                <span style={{ marginLeft: '16px', color: 'var(--text-muted)', fontSize: '13px' }}>
                    Toplam: {total} domain
                </span>
            </div>

            {/* Table */}
            <div style={{ overflowX: 'auto', maxHeight: '600px', overflowY: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                    <thead style={{ position: 'sticky', top: 0, background: 'var(--surface)', zIndex: 10 }}>
                        <tr style={{ borderBottom: '2px solid var(--border)' }}>
                            <th style={{ padding: '12px', textAlign: 'left', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)', minWidth: '200px' }}>
                                Domain
                            </th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>NDA</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Kişisel</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>İştirak</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Eğitim</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Noter</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Hukuk</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Denetim</th>
                            <th style={{ padding: '12px', textAlign: 'center', fontSize: '13px', fontWeight: '600', color: 'var(--text-secondary)' }}>Banka</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? (
                            <tr>
                                <td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                                    Yükleniyor...
                                </td>
                            </tr>
                        ) : domains.length === 0 ? (
                            <tr>
                                <td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
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
                                        {domain.isUnknown && (
                                            <span style={{ marginLeft: '8px', fontSize: '11px', color: '#f59e0b', fontWeight: '600' }}>YENİ</span>
                                        )}
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'hasNda')} style={getToggleStyle(domain.hasNda)}>
                                            {domain.hasNda ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'isPersonal')} style={getToggleStyle(domain.isPersonal)}>
                                            {domain.isPersonal ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'istirakDomain')} style={getToggleStyle(domain.istirakDomain)}>
                                            {domain.istirakDomain ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'egitim')} style={getToggleStyle(domain.egitim)}>
                                            {domain.egitim ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'noter')} style={getToggleStyle(domain.noter)}>
                                            {domain.noter ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'hukuk')} style={getToggleStyle(domain.hukuk)}>
                                            {domain.hukuk ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'denetim')} style={getToggleStyle(domain.denetim)}>
                                            {domain.denetim ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                                        <button onClick={() => handleToggle(domain.id, 'banka')} style={getToggleStyle(domain.banka)}>
                                            {domain.banka ? 'Evet' : 'Hayır'}
                                        </button>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {/* Pagination */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '20px', padding: '12px 0', borderTop: '1px solid var(--border)' }}>
                <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    Sayfa {page} / {totalPages} ({total} domain)
                </span>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <button
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        disabled={page === 1}
                        style={{
                            padding: '8px 16px',
                            borderRadius: '6px',
                            border: '1px solid var(--border)',
                            background: 'var(--background)',
                            color: page === 1 ? 'var(--text-muted)' : 'var(--text-primary)',
                            cursor: page === 1 ? 'default' : 'pointer',
                            fontSize: '13px'
                        }}
                    >
                        ← Önceki
                    </button>
                    <button
                        onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                        disabled={page === totalPages}
                        style={{
                            padding: '8px 16px',
                            borderRadius: '6px',
                            border: '1px solid var(--border)',
                            background: 'var(--background)',
                            color: page === totalPages ? 'var(--text-muted)' : 'var(--text-primary)',
                            cursor: page === totalPages ? 'default' : 'pointer',
                            fontSize: '13px'
                        }}
                    >
                        Sonraki →
                    </button>
                </div>
            </div>
        </div>
    )
}
