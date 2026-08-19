'use client'

import { useState, useEffect } from 'react'
import axios from 'axios'
import { getApiUrlDynamic } from '@/lib/api-config'
import { useTranslation } from '@/components/LanguageProvider'

interface User {
    id: number
    username: string
    email: string
    fullName?: string
    full_name?: string
    role: string
    createdAt?: string
    created_at?: string
    isActive?: boolean
    is_active?: boolean
}

export default function UsersTab() {
    const { t } = useTranslation()
    const [users, setUsers] = useState<User[]>([])
    const [loading, setLoading] = useState(true)
    const [showModal, setShowModal] = useState(false)
    const [editingUser, setEditingUser] = useState<User | null>(null)
    const [ldapPreview, setLdapPreview] = useState<{ username: string; email?: string; fullName?: string; full_name?: string } | null>(null)
    const [ldapLookupLoading, setLdapLookupLoading] = useState(false)
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

    const [formData, setFormData] = useState({
        username: '',
        email: '',
        fullName: '',
        role: 'standard'
    })

    useEffect(() => {
        fetchUsers()
    }, [])

    const fetchUsers = async () => {
        setLoading(true)
        try {
            const token = localStorage.getItem('authToken')
            const apiUrl = getApiUrlDynamic()
            const response = await axios.get(`${apiUrl}/api/users`, {
                headers: token ? { Authorization: `Bearer ${token}` } : {}
            })
            setUsers(Array.isArray(response.data) ? response.data : response.data.users || [])
        } catch (error: any) {
            console.error('Error fetching users:', error)
            setMessage({
                type: 'error',
                text: error.response?.data?.detail || 'Failed to load users'
            })
            setTimeout(() => setMessage(null), 5000)
        } finally {
            setLoading(false)
        }
    }

    const handleCreate = () => {
        setEditingUser(null)
        setFormData({ username: '', email: '', fullName: '', role: 'standard' })
        setLdapPreview(null)
        setShowModal(true)
    }

    const handleEdit = (user: User) => {
        setEditingUser(user)
        setFormData({
            username: user.username,
            email: user.email,
            fullName: user.fullName || user.full_name || '',
            role: user.role
        })
        setLdapPreview(null)
        setShowModal(true)
    }

    const handleLdapLookup = async () => {
        const username = formData.username.trim()
        if (!username) {
            setMessage({ type: 'error', text: 'LDAP kullanici adi zorunludur' })
            setTimeout(() => setMessage(null), 4000)
            return
        }

        setLdapLookupLoading(true)
        setLdapPreview(null)
        try {
            const token = localStorage.getItem('authToken')
            const apiUrl = getApiUrlDynamic()
            const response = await axios.post(
                `${apiUrl}/api/users/ldap/lookup`,
                { username },
                { headers: token ? { Authorization: `Bearer ${token}` } : {} }
            )
            const profile = {
                username: response.data.username || username,
                email: response.data.email || '',
                fullName: response.data.fullName || response.data.full_name || '',
            }
            setLdapPreview(profile)
            setFormData({
                ...formData,
                username: profile.username,
                email: profile.email,
                fullName: profile.fullName,
            })
        } catch (error: any) {
            const errorMessage = error.response?.data?.detail || error.message || 'LDAP kullanicisi bulunamadi'
            setMessage({ type: 'error', text: errorMessage })
            setTimeout(() => setMessage(null), 5000)
        } finally {
            setLdapLookupLoading(false)
        }
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()
        setMessage(null)

        try {
            const token = localStorage.getItem('authToken')
            const apiUrl = getApiUrlDynamic()

            if (editingUser) {
                await axios.put(
                    `${apiUrl}/api/users/${editingUser.id}`,
                    { username: formData.username, email: formData.email, fullName: formData.fullName, role: formData.role },
                    { headers: token ? { Authorization: `Bearer ${token}` } : {} }
                )
                setMessage({ type: 'success', text: 'User updated successfully' })
            } else {
                await axios.post(
                    `${apiUrl}/api/users/ldap`,
                    { username: formData.username, role: formData.role },
                    { headers: token ? { Authorization: `Bearer ${token}` } : {} }
                )
                setMessage({ type: 'success', text: 'LDAP user created successfully' })
            }

            setShowModal(false)
            await fetchUsers()
            setTimeout(() => setMessage(null), 3000)
        } catch (error: any) {
            const errorMessage = error.response?.data?.detail || error.message || 'Failed to save user'
            setMessage({ type: 'error', text: errorMessage })
            setTimeout(() => setMessage(null), 5000)
        }
    }

    const handleDelete = async (id: number) => {
        if (!confirm('Are you sure you want to delete this user?')) return

        try {
            const token = localStorage.getItem('authToken')
            const apiUrl = getApiUrlDynamic()
            await axios.delete(`${apiUrl}/api/users/${id}`, {
                headers: token ? { Authorization: `Bearer ${token}` } : {}
            })
            setMessage({ type: 'success', text: 'User deleted successfully' })
            await fetchUsers()
            setTimeout(() => setMessage(null), 3000)
        } catch (error: any) {
            const errorMessage = error.response?.data?.detail || error.message || 'Failed to delete user'
            setMessage({ type: 'error', text: errorMessage })
            setTimeout(() => setMessage(null), 5000)
        }
    }

    if (loading) {
        return <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>Loading users...</div>
    }

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
                <div>
                    <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>{t('users.title')}</h3>
                    <p style={{ margin: '4px 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>{t('users.subtitle')}</p>
                </div>
                <button
                    onClick={handleCreate}
                    style={{
                        padding: '10px 20px',
                        background: 'var(--primary)',
                        color: 'white',
                        border: 'none',
                        borderRadius: '6px',
                        cursor: 'pointer',
                        fontWeight: '600',
                        fontSize: '13px'
                    }}
                >
                    + LDAP User Ekle
                </button>
            </div>

            {message && (
                <div style={{
                    padding: '12px 16px',
                    borderRadius: '6px',
                    marginBottom: '16px',
                    background: message.type === 'success' ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
                    color: message.type === 'success' ? '#5cb85c' : '#d9534f',
                    border: `1px solid ${message.type === 'success' ? 'rgba(92, 184, 92, 0.3)' : 'rgba(217, 83, 79, 0.3)'}`
                }}>
                    {message.text}
                </div>
            )}

            <div style={{ background: 'var(--background)', borderRadius: '8px', overflow: 'hidden' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                    <thead>
                        <tr style={{ borderBottom: '2px solid var(--border)' }}>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>ID</th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>{t('users.username')}</th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>Ad Soyad</th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>{t('users.email')}</th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>{t('users.role')}</th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>{t('users.status')}</th>
                            <th style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', textTransform: 'none' }}>{t('users.actions')}</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.length === 0 ? (
                            <tr><td colSpan={7} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>{t('users.noUsers')}</td></tr>
                        ) : (
                            users.map((user) => (
                                <tr key={user.id} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '12px' }}>{user.id}</td>
                                    <td style={{ padding: '12px', fontWeight: 500 }}>{user.username}</td>
                                    <td style={{ padding: '12px' }}>{user.fullName || user.full_name || '-'}</td>
                                    <td style={{ padding: '12px' }}>{user.email}</td>
                                    <td style={{ padding: '12px' }}>
                                        <span style={{
                                            padding: '4px 10px',
                                            borderRadius: '12px',
                                            fontSize: '11px',
                                            fontWeight: 600,
                                            background: user.role === 'admin' ? 'rgba(0, 168, 232, 0.2)' : 'rgba(100, 116, 139, 0.2)',
                                            color: user.role === 'admin' ? 'var(--primary)' : 'var(--text-secondary)'
                                        }}>
                                            {user.role === 'admin' ? t('users.admin') : t('users.standard')}
                                        </span>
                                    </td>
                                    <td style={{ padding: '12px' }}>
                                        <span style={{
                                            padding: '4px 10px',
                                            borderRadius: '12px',
                                            fontSize: '11px',
                                            fontWeight: 600,
                                            background: (user.isActive ?? user.is_active ?? true) ? 'rgba(92, 184, 92, 0.2)' : 'rgba(217, 83, 79, 0.2)',
                                            color: (user.isActive ?? user.is_active ?? true) ? '#5cb85c' : '#d9534f'
                                        }}>
                                            {(user.isActive ?? user.is_active ?? true) ? 'Active' : 'Inactive'}
                                        </span>
                                    </td>
                                    <td style={{ padding: '12px', textAlign: 'right' }}>
                                        <button onClick={() => handleEdit(user)} style={{ padding: '6px 12px', marginRight: '8px', background: 'var(--surface-hover)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', fontSize: '12px' }}>{t('users.edit')}</button>
                                        <button onClick={() => handleDelete(user.id)} style={{ padding: '6px 12px', background: 'rgba(217, 83, 79, 0.1)', color: '#d9534f', border: '1px solid rgba(217, 83, 79, 0.3)', borderRadius: '6px', cursor: 'pointer', fontSize: '12px' }}>{t('users.delete')}</button>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {/* Modal */}
            {showModal && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0, 0, 0, 0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 3000 }} onClick={() => setShowModal(false)}>
                    <div style={{ background: 'var(--surface)', borderRadius: '8px', padding: '24px', width: '90%', maxWidth: '450px' }} onClick={(e) => e.stopPropagation()}>
                        <h3 style={{ margin: '0 0 20px 0', fontSize: '18px' }}>{editingUser ? t('users.editUser') : 'LDAP User Ekle'}</h3>
                        <form onSubmit={handleSubmit}>
                            <div style={{ marginBottom: '16px' }}>
                                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '12px', textTransform: 'none' }}>{editingUser ? t('users.username') : 'LDAP Kullanici Adi'}</label>
                                <div style={{ display: 'flex', gap: '8px' }}>
                                    <input type="text" value={formData.username} onChange={(e) => { setFormData({ ...formData, username: e.target.value }); setLdapPreview(null) }} required style={{ width: '100%', padding: '10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '14px', background: 'var(--background)', color: 'var(--text-primary)' }} />
                                    {!editingUser && (
                                        <button type="button" onClick={handleLdapLookup} disabled={ldapLookupLoading} style={{ padding: '0 14px', background: 'var(--surface-hover)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', fontWeight: 600, whiteSpace: 'nowrap' }}>
                                            {ldapLookupLoading ? 'Araniyor...' : 'LDAP Getir'}
                                        </button>
                                    )}
                                </div>
                            </div>
                            {(editingUser || ldapPreview) && (
                                <div style={{ marginBottom: '16px', padding: '12px', border: '1px solid var(--border)', borderRadius: '8px', background: 'var(--background)' }}>
                                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '8px' }}>LDAP Bilgileri</div>
                                    <div style={{ display: 'grid', gap: '6px', fontSize: '13px', color: 'var(--text-primary)' }}>
                                        <div><strong>Kullanici:</strong> {formData.username || '-'}</div>
                                        <div><strong>Ad Soyad:</strong> {formData.fullName || '-'}</div>
                                        <div><strong>E-posta:</strong> {formData.email || '-'}</div>
                                    </div>
                                </div>
                            )}
                            {editingUser && (
                                <div style={{ marginBottom: '16px' }}>
                                    <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '12px', textTransform: 'none' }}>Ad Soyad</label>
                                    <input type="text" value={formData.fullName} onChange={(e) => setFormData({ ...formData, fullName: e.target.value })} style={{ width: '100%', padding: '10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '14px', background: 'var(--background)', color: 'var(--text-primary)' }} />
                                </div>
                            )}
                            {editingUser && (
                            <div style={{ marginBottom: '16px' }}>
                                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '12px', textTransform: 'none' }}>{t('users.email')}</label>
                                <input type="email" value={formData.email} onChange={(e) => setFormData({ ...formData, email: e.target.value })} required style={{ width: '100%', padding: '10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '14px', background: 'var(--background)', color: 'var(--text-primary)' }} />
                            </div>
                            )}
                            <div style={{ marginBottom: '20px' }}>
                                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '12px', textTransform: 'none' }}>{t('users.role')}</label>
                                <select value={formData.role} onChange={(e) => setFormData({ ...formData, role: e.target.value })} style={{ width: '100%', padding: '10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '14px', background: 'var(--background)', color: 'var(--text-primary)' }}>
                                    <option value="standard">{t('users.standard')}</option>
                                    <option value="admin">{t('users.admin')}</option>
                                </select>
                            </div>
                            <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setShowModal(false)} style={{ padding: '10px 20px', background: 'var(--surface-hover)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer' }}>{t('users.cancel')}</button>
                                <button type="submit" style={{ padding: '10px 20px', background: 'var(--primary)', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontWeight: 600 }}>{editingUser ? 'Update' : 'LDAP User Ekle'}</button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    )
}
