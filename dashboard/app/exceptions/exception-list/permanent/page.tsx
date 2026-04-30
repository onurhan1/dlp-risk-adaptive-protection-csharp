'use client'

import React, { useState, useEffect, useRef } from 'react'
import apiClient from '@/lib/axios'
import { Search, Upload, Download, Plus, Edit2, Trash2, X, AlertCircle } from 'lucide-react'
import { useAuth } from '@/components/AuthProvider'

interface PermanentException {
  id: number
  exceptionName: string
  exceptionDomain: string
  team: string
  policies: string
  rules: string
  channel: string
  duration: string
  actionDate: string
  changeNo: string
}

export default function PermanentExceptionsPage() {
  const { username } = useAuth()
  const [exceptions, setExceptions] = useState<PermanentException[]>([])
  const [loading, setLoading] = useState(true)
  const [searchQuery, setSearchQuery] = useState('')
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
  const [editingItem, setEditingItem] = useState<PermanentException | null>(null)
  const [formData, setFormData] = useState({
    exceptionName: '', exceptionDomain: '', team: '', policies: '',
    rules: '', channel: '', duration: '', actionDate: '', changeNo: ''
  })

  useEffect(() => {
    fetchExceptions()
  }, [page, searchQuery])

  const fetchExceptions = async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await apiClient.get('/api/exception-entries/permanent', {
        params: { page, pageSize: 50, search: searchQuery }
      })
      setExceptions(response.data.entries || [])
      setTotalPages(response.data.totalPages || 1)
      setTotal(response.data.total || 0)
    } catch (err: any) {
      setError('Veriler yüklenirken hata oluştu: ' + (err.response?.data?.error || err.message))
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
      await apiClient.post(`/api/exception-entries/permanent/upload?uploadedBy=${username}`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      fetchExceptions()
      if (fileInputRef.current) fileInputRef.current.value = ''
    } catch (err: any) {
      setError('Dosya yüklenirken hata oluştu: ' + (err.response?.data?.error || err.message))
    } finally {
      setUploading(false)
    }
  }

  const openModal = (item?: PermanentException) => {
    if (item) {
      setEditingItem(item)
      setFormData({
        exceptionName: item.exceptionName || '',
        exceptionDomain: item.exceptionDomain || '',
        team: item.team || '',
        policies: item.policies || '',
        rules: item.rules || '',
        channel: item.channel || '',
        duration: item.duration || '',
        actionDate: item.actionDate ? item.actionDate.split('T')[0] : '',
        changeNo: item.changeNo || ''
      })
    } else {
      setEditingItem(null)
      setFormData({
        exceptionName: '', exceptionDomain: '', team: '', policies: '',
        rules: '', channel: '', duration: '', actionDate: '', changeNo: ''
      })
    }
    setShowModal(true)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (!formData.exceptionName) {
      setError('İstisna Adı zorunludur')
      return
    }

    try {
      const payload = {
        ...formData,
        createdBy: username
      }

      if (editingItem) {
        await apiClient.put(`/api/exception-entries/permanent/${editingItem.id}`, payload)
      } else {
        await apiClient.post('/api/exception-entries/permanent', payload)
      }
      
      setShowModal(false)
      fetchExceptions()
    } catch (err: any) {
      setError('Kaydetme hatası: ' + (err.response?.data?.error || err.message))
    }
  }

  const handleDelete = async (id: number) => {
    if (!window.confirm('Bu kaydı silmek istediğinize emin misiniz?')) return

    try {
      await apiClient.delete(`/api/exception-entries/permanent/${id}`)
      fetchExceptions()
    } catch (err: any) {
      setError('Silme hatası: ' + (err.response?.data?.error || err.message))
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
            Kalıcı İstisna Listesi
          </h1>
          <p style={{ margin: 0, color: 'var(--text-secondary)', fontSize: '14px' }}>
            Toplam {total} kayıt bulundu
          </p>
        </div>

        <div style={{ display: 'flex', gap: '12px' }}>
          <form onSubmit={handleSearch} style={{ display: 'flex', gap: '8px' }}>
            <input
              type="text"
              placeholder="Ara..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                padding: '8px 12px',
                borderRadius: '6px',
                border: '1px solid var(--border)',
                background: 'var(--surface)',
                color: 'var(--text-primary)',
                fontSize: '14px'
              }}
            />
            <button type="submit" style={{ padding: '8px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer' }}>
              <Search size={18} />
            </button>
          </form>

          <input
            type="file"
            ref={fileInputRef}
            onChange={handleFileUpload}
            accept=".xlsx"
            style={{ display: 'none' }}
          />
          <button
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            style={{
              padding: '8px 16px',
              background: 'var(--surface)',
              color: 'var(--text-primary)',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              cursor: uploading ? 'not-allowed' : 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              fontSize: '14px',
              fontWeight: 500
            }}
          >
            <Upload size={18} />
            {uploading ? 'Yükleniyor...' : 'Excel Yükle'}
          </button>

          <button
            onClick={() => openModal()}
            style={{
              padding: '8px 16px',
              background: '#10b981',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              fontSize: '14px',
              fontWeight: 500
            }}
          >
            <Plus size={18} />
            Yeni Ekle
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
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>İstisna Adı</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Domain</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Ekip</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Politika / Kurallar</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Kanal</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Süre</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Tarih</th>
                <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)' }}>Change No</th>
                <th style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: 'var(--text-secondary)' }}>İşlemler</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Yükleniyor...</td>
                </tr>
              ) : exceptions.length === 0 ? (
                <tr>
                  <td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Kayıt bulunamadı.</td>
                </tr>
              ) : (
                exceptions.map((item) => (
                  <tr key={item.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '12px', fontWeight: 500 }}>{item.exceptionName}</td>
                    <td style={{ padding: '12px' }}>{item.exceptionDomain || '-'}</td>
                    <td style={{ padding: '12px' }}>{item.team || '-'}</td>
                    <td style={{ padding: '12px', maxWidth: '250px' }}>
                      <div style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }} title={item.policies}>
                        <strong style={{fontSize:'11px', color:'var(--text-secondary)'}}>POLİTİKA:</strong> {item.policies || '-'}
                      </div>
                      <div style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }} title={item.rules}>
                        <strong style={{fontSize:'11px', color:'var(--text-secondary)'}}>KURAL:</strong> {item.rules || '-'}
                      </div>
                    </td>
                    <td style={{ padding: '12px' }}>
                      <span style={{ padding: '4px 8px', background: 'var(--background)', borderRadius: '4px', fontSize: '12px' }}>
                        {item.channel || '-'}
                      </span>
                    </td>
                    <td style={{ padding: '12px' }}>{item.duration || '-'}</td>
                    <td style={{ padding: '12px' }}>{formatDate(item.actionDate)}</td>
                    <td style={{ padding: '12px' }}>{item.changeNo || '-'}</td>
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
            <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Sayfa {page} / {totalPages}</span>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button 
                onClick={() => setPage(p => Math.max(1, p - 1))} 
                disabled={page === 1}
                style={{ padding: '6px 12px', background: 'var(--background)', border: '1px solid var(--border)', borderRadius: '6px', cursor: page === 1 ? 'not-allowed' : 'pointer' }}
              >
                Önceki
              </button>
              <button 
                onClick={() => setPage(p => Math.min(totalPages, p + 1))} 
                disabled={page >= totalPages}
                style={{ padding: '6px 12px', background: 'var(--background)', border: '1px solid var(--border)', borderRadius: '6px', cursor: page >= totalPages ? 'not-allowed' : 'pointer' }}
              >
                Sonraki
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal */}
      {showModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
          <div style={{ background: 'var(--surface)', width: '100%', maxWidth: '600px', borderRadius: '12px', border: '1px solid var(--border)', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <div style={{ padding: '20px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>
                {editingItem ? 'İstisnayı Düzenle' : 'Yeni Kalıcı İstisna Ekle'}
              </h2>
              <button onClick={() => setShowModal(false)} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSubmit} style={{ padding: '24px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                <div style={{ gridColumn: '1 / -1' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>İstisna Adı *</label>
                  <input
                    required
                    value={formData.exceptionName}
                    onChange={e => setFormData({...formData, exceptionName: e.target.value})}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>
                
                <div style={{ gridColumn: '1 / -1' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Domain</label>
                  <input
                    value={formData.exceptionDomain}
                    onChange={e => setFormData({...formData, exceptionDomain: e.target.value})}
                    placeholder="örn: cisco.com, gmail.com"
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Ekip / Departman</label>
                  <input
                    value={formData.team}
                    onChange={e => setFormData({...formData, team: e.target.value})}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Kanal (Channel)</label>
                  <input
                    value={formData.channel}
                    onChange={e => setFormData({...formData, channel: e.target.value})}
                    placeholder="örn: Mail, Endpoint Printing"
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>

                <div style={{ gridColumn: '1 / -1' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Politikalar</label>
                  <textarea
                    value={formData.policies}
                    onChange={e => setFormData({...formData, policies: e.target.value})}
                    rows={2}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px', resize: 'vertical' }}
                  />
                </div>

                <div style={{ gridColumn: '1 / -1' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Kurallar</label>
                  <textarea
                    value={formData.rules}
                    onChange={e => setFormData({...formData, rules: e.target.value})}
                    rows={2}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px', resize: 'vertical' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Süre</label>
                  <input
                    value={formData.duration}
                    onChange={e => setFormData({...formData, duration: e.target.value})}
                    placeholder="örn: 1 yıl, Sınırsız"
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Tarih</label>
                  <input
                    type="date"
                    value={formData.actionDate}
                    onChange={e => setFormData({...formData, actionDate: e.target.value})}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--text-secondary)' }}>Change No</label>
                  <input
                    value={formData.changeNo}
                    onChange={e => setFormData({...formData, changeNo: e.target.value})}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--background)', color: 'var(--text-primary)', fontSize: '14px' }}
                  />
                </div>
              </div>

              <div style={{ marginTop: '24px', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" onClick={() => setShowModal(false)} style={{ padding: '8px 16px', background: 'transparent', border: '1px solid var(--border)', color: 'var(--text-primary)', borderRadius: '6px', cursor: 'pointer', fontWeight: 500 }}>
                  İptal
                </button>
                <button type="submit" style={{ padding: '8px 16px', background: 'var(--primary)', border: 'none', color: 'white', borderRadius: '6px', cursor: 'pointer', fontWeight: 500 }}>
                  Kaydet
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
