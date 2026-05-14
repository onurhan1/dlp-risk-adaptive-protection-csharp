'use client'

import React, { useState, useEffect, useRef } from 'react'
import apiClient from '@/lib/axios'
import { Search, Upload, Plus, Edit2, Trash2, X, AlertCircle } from 'lucide-react'
import { useAuth } from '@/components/AuthProvider'
import { useTranslation } from '@/components/LanguageProvider'

interface ExceptionRemoval {
  id: number
  team: string
  rule: string
  exception_name: string
  status: string
  usage_count: number
  removal_reason: string
  action_date: string
  change_no: string
}

export default function ExceptionRemovalsPage() {
  const { username } = useAuth()
  const { t } = useTranslation()
  const [exceptions, setExceptions] = useState<ExceptionRemoval[]>([])
  const [loading, setLoading] = useState(true)
  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [error, setError] = useState<string | null>(null)
  
  // Pagination
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [total, setTotal] = useState(0)

  // Upload
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)

  // Modal
  const [showModal, setShowModal] = useState(false)
  const [editingItem, setEditingItem] = useState<ExceptionRemoval | null>(null)
  const [formData, setFormData] = useState({
    team: '', rule: '', exception_name: '', status: 'Aktif',
    usage_count: 0, removal_reason: '', action_date: '', change_no: ''
  })

  useEffect(() => {
    fetchExceptions()
  }, [page, searchQuery, statusFilter])

  const fetchExceptions = async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await apiClient.get('/api/exception-entries/removal', {
        params: { page, pageSize: 50, search: searchQuery, status: statusFilter }
      })
      setExceptions(response.data.entries || [])
      setTotalPages(response.data.totalPages || 1)
      setTotal(response.data.total || 0)
    } catch (err: any) {
      setError(`${t('exceptionsList.errorLoad')} ` + (err.response?.data?.error || err.message))
    } finally {
      setLoading(false)
    }
  }

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault()
    setPage(1)
    fetchExceptions()
  }

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    setUploading(true)
    setError(null)
    
    const formData = new FormData()
    formData.append('file', file)

    try {
      await apiClient.post(`/api/exception-entries/removal/upload?uploadedBy=${username}`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      fetchExceptions()
      if (fileInputRef.current) fileInputRef.current.value = ''
    } catch (err: any) {
      setError(`${t('exceptionsList.errorUpload')} ` + (err.response?.data?.error || err.message))
    } finally {
      setUploading(false)
    }
  }

  const openModal = (item?: ExceptionRemoval) => {
    if (item) {
      setEditingItem(item)
      setFormData({
        team: item.team || '',
        rule: item.rule || '',
        exception_name: item.exception_name || '',
        status: item.status || 'Aktif',
        usage_count: item.usage_count || 0,
        removal_reason: item.removal_reason || '',
        action_date: item.action_date ? item.action_date.split('T')[0] : '',
        change_no: item.change_no || ''
      })
    } else {
      setEditingItem(null)
      setFormData({
        team: '', rule: '', exception_name: '', status: 'Aktif',
        usage_count: 0, removal_reason: '', action_date: '', change_no: ''
      })
    }
    setShowModal(true)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (!formData.exception_name) {
      setError(t('exceptionsList.exceptionNameRequired'))
      return
    }

    try {
      const payload = {
        team: formData.team,
        rule: formData.rule,
        exceptionName: formData.exception_name,
        status: formData.status,
        usageCount: formData.usage_count,
        removalReason: formData.removal_reason,
        actionDate: formData.action_date,
        changeNo: formData.change_no,
        createdBy: username
      }

      if (editingItem) {
        await apiClient.put(`/api/exception-entries/removal/${editingItem.id}`, payload)
      } else {
        await apiClient.post('/api/exception-entries/removal', payload)
      }
      
      setShowModal(false)
      fetchExceptions()
    } catch (err: any) {
      setError(`${t('exceptionsList.errorSave')} ` + (err.response?.data?.error || err.message))
    }
  }

  const handleDelete = async (id: number) => {
    if (!window.confirm(t('exceptionsList.deleteConfirm'))) return

    try {
      await apiClient.delete(`/api/exception-entries/removal/${id}`)
      fetchExceptions()
    } catch (err: any) {
      setError(`${t('exceptionsList.errorDelete')} ` + (err.response?.data?.error || err.message))
    }
  }

  const formatDate = (dateStr: string) => {
    if (!dateStr) return '-'
    const date = new Date(dateStr)
    return date.toLocaleDateString('tr-TR')
  }

  return (
    <div style={{ padding: '24px', background: 'var(--background)', minHeight: '100vh' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 'bold', margin: '0 0 8px 0', color: 'var(--text-primary)' }}>
            {t('exceptionsList.titleRemoval')}
          </h1>
          <p style={{ margin: 0, color: 'var(--text-secondary)', fontSize: '14px' }}>
            {t('exceptionsList.totalRecords').replace('{count}', total.toString())}
          </p>
        </div>

        <div style={{ display: 'flex', gap: '12px' }}>
          <select
            value={statusFilter}
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
            style={{ padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px' }}
          >
            <option value="">{t('exceptionsList.statusAll')}</option>
            <option value="Aktif">{t('exceptionsList.statusActive')}</option>
            <option value="Pasif">{t('exceptionsList.statusPassive')}</option>
          </select>

          <form onSubmit={handleSearch} style={{ display: 'flex', gap: '8px' }}>
            <input
              type="text"
              placeholder={`${t('common.search')}...`}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{ padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text-primary)', fontSize: '14px' }}
            />
            <button type="submit" style={{ padding: '8px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer' }}>
              <Search size={18} />
            </button>
          </form>

          <input type="file" ref={fileInputRef} onChange={handleFileUpload} accept=".xlsx" style={{ display: 'none' }} />
          <button
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            style={{ padding: '8px 16px', background: 'var(--surface)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: uploading ? 'not-allowed' : 'pointer', display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', fontWeight: 500 }}
          >
            <Upload size={18} />
            {uploading ? t('exceptionsList.uploading') : t('exceptionsList.uploadExcel')}
          </button>

          <button
            onClick={() => openModal()}
            style={{ padding: '8px 16px', background: '#10b981', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', fontWeight: 500 }}
          >
            <Plus size={18} />
            {t('exceptionsList.addNew')}
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '12px', background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', borderRadius: '6px', marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <AlertCircle size={18} />
          {error}
        </div>
      )}

      <div style={{ background: 'var(--surface)', borderRadius: '12px', overflow: 'hidden', border: '1px solid var(--border)' }}>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: 'var(--background)', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.team')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.rule')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.exceptionName')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.status')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.usageCount')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.removalReason')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.date')}</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.changeNo')}</th>
                <th style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: 'var(--text-secondary)' }}>{t('exceptionsList.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>{t('exceptionsList.loading')}</td></tr>
              ) : exceptions.length === 0 ? (
                <tr><td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>{t('exceptionsList.noRecords')}</td></tr>
              ) : (
                exceptions.map((item) => (
                  <tr key={item.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '12px' }}>{item.team || '-'}</td>
                    <td style={{ padding: '12px' }}>{item.rule || '-'}</td>
                    <td style={{ padding: '12px', fontWeight: 500 }}>{item.exception_name}</td>
                    <td style={{ padding: '12px' }}>
                      <span style={{ 
                        padding: '4px 8px', 
                        borderRadius: '4px', 
                        fontSize: '12px', 
                        fontWeight: 600,
                        background: item.status?.toLowerCase() === 'aktif' || item.status?.toLowerCase() === 'active' ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                        color: item.status?.toLowerCase() === 'aktif' || item.status?.toLowerCase() === 'active' ? '#10b981' : '#ef4444'
                      }}>
                        {item.status?.toLowerCase() === 'aktif' || item.status?.toLowerCase() === 'active' ? t('exceptionsList.statusActive') : item.status?.toLowerCase() === 'pasif' || item.status?.toLowerCase() === 'passive' ? t('exceptionsList.statusPassive') : '-'}
                      </span>
                    </td>
                    <td style={{ padding: '12px', textAlign: 'center' }}>
                      <span style={{ background: 'var(--background)', padding: '2px 8px', borderRadius: '12px' }}>
                        {item.usage_count}
                      </span>
                    </td>
                    <td style={{ padding: '12px', maxWidth: '250px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }} title={item.removal_reason}>
                      {item.removal_reason || '-'}
                    </td>
                    <td style={{ padding: '12px' }}>{formatDate(item.action_date)}</td>
                    <td style={{ padding: '12px' }}>{item.change_no || '-'}</td>
                    <td style={{ padding: '12px', textAlign: 'right' }}>
                      <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                        <button onClick={() => openModal(item)} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer', padding: '4px' }}>
                          <Edit2 size={16} />
                        </button>
                        <button onClick={() => handleDelete(item.id)} style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', padding: '4px' }}>
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        
        {/* Pagination */}
        {totalPages > 1 && (
          <div style={{ padding: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderTop: '1px solid var(--border)' }}>
            <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{t('pagination.page')} {page} {t('pagination.of')} {totalPages}</span>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button 
                onClick={() => setPage(p => Math.max(1, p - 1))} 
                disabled={page === 1}
                style={{ padding: '6px 12px', background: 'var(--background)', border: '1px solid var(--border)', borderRadius: '6px', cursor: page === 1 ? 'not-allowed' : 'pointer' }}
              >{t('pagination.previous')}</button>
              <button 
                onClick={() => setPage(p => Math.min(totalPages, p + 1))} 
                disabled={page >= totalPages}
                style={{ padding: '6px 12px', background: 'var(--background)', border: '1px solid var(--border)', borderRadius: '6px', cursor: page >= totalPages ? 'not-allowed' : 'pointer' }}
              >{t('pagination.next')}</button>
            </div>
          </div>
        )}
      </div>

      {/* Modal */}
      {showModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
          <div style={{ background: 'var(--surface)', width: '100%', maxWidth: '500px', borderRadius: '12px', border: '1px solid var(--border)', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <div style={{ padding: '20px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>
                {editingItem ? t('exceptionsList.editRecord') : t('exceptionsList.newRemovalRecord')}
              </h2>
              <button onClick={() => setShowModal(false)} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSubmit} style={{ padding: '24px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.exceptionName')} *</label>
                  <input required value={formData.exception_name} onChange={e => setFormData({...formData, exception_name: e.target.value})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                </div>
                
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.rule')}</label>
                  <input value={formData.rule} onChange={e => setFormData({...formData, rule: e.target.value})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.team')}</label>
                  <input value={formData.team} onChange={e => setFormData({...formData, team: e.target.value})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.status')}</label>
                    <select value={formData.status} onChange={e => setFormData({...formData, status: e.target.value})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}>
                      <option value="Aktif">{t('exceptionsList.statusActive')}</option>
                      <option value="Pasif">{t('exceptionsList.statusPassive')}</option>
                    </select>
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.usageCountLabel')}</label>
                    <input type="number" min="0" value={formData.usage_count} onChange={e => setFormData({...formData, usage_count: parseInt(e.target.value) || 0})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                  </div>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.removalReason')}</label>
                  <textarea value={formData.removal_reason} onChange={e => setFormData({...formData, removal_reason: e.target.value})} rows={2} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px', resize: 'vertical' }} />
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.date')}</label>
                    <input type="date" value={formData.action_date} onChange={e => setFormData({...formData, action_date: e.target.value})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>{t('exceptionsList.changeNo')}</label>
                    <input value={formData.change_no} onChange={e => setFormData({...formData, change_no: e.target.value})} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }} />
                  </div>
                </div>
              </div>

              <div style={{ marginTop: '24px', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" onClick={() => setShowModal(false)} style={{ padding: '8px 16px', background: 'transparent', border: '1px solid var(--border)', color: 'var(--text-primary)', borderRadius: '6px', cursor: 'pointer', fontWeight: 500 }}>{t('exceptionsList.cancel')}</button>
                <button type="submit" style={{ padding: '8px 16px', background: 'var(--primary)', border: 'none', color: 'white', borderRadius: '6px', cursor: 'pointer', fontWeight: 500 }}>{t('exceptionsList.save')}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
