'use client'

import React, { useState, useEffect } from 'react'
import { useTranslation } from '@/components/LanguageProvider'

interface ReleaseEntry {
    title: string
    titleEn: string
    description: string
    descriptionEn: string
    category: 'feature' | 'improvement' | 'bugfix'
    commitHash?: string
    createdBy: string
}

interface ReleaseVersion {
    version: string
    date: string
    isPublished: boolean
    entries: ReleaseEntry[]
}

const categoryIcons: Record<string, { icon: string; color: string; label: string; labelEn: string }> = {
    feature: { icon: '🚀', color: '#10b981', label: 'Yeni Özellik', labelEn: 'New Feature' },
    improvement: { icon: '⚡', color: '#3b82f6', label: 'İyileştirme', labelEn: 'Improvement' },
    bugfix: { icon: '🐛', color: '#f59e0b', label: 'Düzeltme', labelEn: 'Bug Fix' },
}

export default function ReleaseNotesPage() {
    const { locale, t } = useTranslation()
    const [releases, setReleases] = useState<ReleaseVersion[]>([])
    const [loading, setLoading] = useState(true)
    const [expandedVersions, setExpandedVersions] = useState<Set<string>>(new Set())

    useEffect(() => {
        fetchReleaseNotes()
    }, [])

    const fetchReleaseNotes = async () => {
        try {
            const response = await fetch('/release-notes.json')
            const data = await response.json()
            setReleases(data)
            // Expand the first version by default
            if (data.length > 0) {
                setExpandedVersions(new Set([data[0].version]))
            }
        } catch (error) {
            console.error('Error loading release notes:', error)
        } finally {
            setLoading(false)
        }
    }

    const toggleVersion = (version: string) => {
        setExpandedVersions(prev => {
            const newSet = new Set(prev)
            if (newSet.has(version)) {
                newSet.delete(version)
            } else {
                newSet.add(version)
            }
            return newSet
        })
    }

    const getTitle = (entry: ReleaseEntry) => locale === 'en' ? entry.titleEn : entry.title
    const getDescription = (entry: ReleaseEntry) => locale === 'en' ? entry.descriptionEn : entry.description
    const getCategoryLabel = (cat: string) => {
        const info = categoryIcons[cat]
        return locale === 'en' ? info?.labelEn : info?.label
    }

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh', color: 'var(--text-muted)' }}>
                {t('common.loading')}
            </div>
        )
    }

    return (
        <div style={{ maxWidth: '900px', margin: '0 auto', padding: '24px' }}>
            {/* Page Header */}
            <div style={{ marginBottom: '32px' }}>
                <h1 style={{
                    fontSize: '28px',
                    fontWeight: '700',
                    color: 'var(--text-primary)',
                    margin: '0 0 8px 0',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '12px',
                }}>
                    📋 {t('releaseNotes.title')}
                </h1>
                <p style={{
                    fontSize: '14px',
                    color: 'var(--text-muted)',
                    margin: 0,
                }}>
                    {locale === 'tr'
                        ? 'RADAR uygulamasındaki tüm değişiklikler ve yenilikler'
                        : 'All changes and updates to the RADAR application'}
                </p>
            </div>

            {/* Version Timeline */}
            <div style={{ position: 'relative' }}>
                {/* Timeline line */}
                <div style={{
                    position: 'absolute',
                    left: '15px',
                    top: '0',
                    bottom: '0',
                    width: '2px',
                    background: 'linear-gradient(to bottom, #3b82f6, var(--border))',
                }} />

                {releases.filter(r => r.isPublished).map((release, rIdx) => {
                    const isExpanded = expandedVersions.has(release.version)
                    const features = release.entries.filter(e => e.category === 'feature')
                    const improvements = release.entries.filter(e => e.category === 'improvement')
                    const bugfixes = release.entries.filter(e => e.category === 'bugfix')

                    return (
                        <div key={release.version} style={{ position: 'relative', paddingLeft: '44px', marginBottom: '24px' }}>
                            {/* Timeline dot */}
                            <div style={{
                                position: 'absolute',
                                left: '8px',
                                top: '14px',
                                width: '16px',
                                height: '16px',
                                borderRadius: '50%',
                                background: rIdx === 0 ? '#3b82f6' : 'var(--surface)',
                                border: rIdx === 0 ? '3px solid rgba(59, 130, 246, 0.3)' : '3px solid var(--border)',
                                zIndex: 1,
                            }} />

                            {/* Version card */}
                            <div style={{
                                background: 'var(--surface)',
                                borderRadius: '12px',
                                border: '1px solid var(--border)',
                                overflow: 'hidden',
                                transition: 'box-shadow 0.2s ease',
                                boxShadow: rIdx === 0 ? '0 2px 12px rgba(59, 130, 246, 0.1)' : 'none',
                            }}>
                                {/* Version header */}
                                <div
                                    onClick={() => toggleVersion(release.version)}
                                    style={{
                                        padding: '16px 20px',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        justifyContent: 'space-between',
                                        alignItems: 'center',
                                        background: isExpanded ? 'rgba(59, 130, 246, 0.03)' : 'transparent',
                                        transition: 'background 0.15s',
                                    }}
                                >
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                        <span style={{
                                            fontSize: '18px',
                                            fontWeight: '700',
                                            color: '#3b82f6',
                                        }}>
                                            {release.version}
                                        </span>
                                        <span style={{
                                            fontSize: '12px',
                                            color: 'var(--text-muted)',
                                            background: 'var(--background)',
                                            padding: '3px 10px',
                                            borderRadius: '12px',
                                        }}>
                                            {new Date(release.date).toLocaleDateString(locale === 'tr' ? 'tr-TR' : 'en-US', {
                                                day: 'numeric',
                                                month: 'long',
                                                year: 'numeric',
                                            })}
                                        </span>
                                        {rIdx === 0 && (
                                            <span style={{
                                                fontSize: '10px',
                                                fontWeight: '700',
                                                color: 'white',
                                                background: '#10b981',
                                                padding: '2px 8px',
                                                borderRadius: '10px',
                                                textTransform: 'none',
                                            }}>
                                                {locale === 'tr' ? 'Güncel' : 'Latest'}
                                            </span>
                                        )}
                                    </div>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                        {/* Category counts */}
                                        {features.length > 0 && (
                                            <span style={{ fontSize: '11px', color: '#10b981' }}>🚀 {features.length}</span>
                                        )}
                                        {improvements.length > 0 && (
                                            <span style={{ fontSize: '11px', color: '#3b82f6' }}>⚡ {improvements.length}</span>
                                        )}
                                        {bugfixes.length > 0 && (
                                            <span style={{ fontSize: '11px', color: '#f59e0b' }}>🐛 {bugfixes.length}</span>
                                        )}
                                        <span style={{
                                            transform: isExpanded ? 'rotate(180deg)' : 'rotate(0deg)',
                                            transition: 'transform 0.2s',
                                            fontSize: '12px',
                                            color: 'var(--text-muted)',
                                        }}>▼</span>
                                    </div>
                                </div>

                                {/* Expanded content */}
                                {isExpanded && (
                                    <div style={{ padding: '0 20px 20px 20px' }}>
                                        {[
                                            { entries: features, key: 'feature' },
                                            { entries: improvements, key: 'improvement' },
                                            { entries: bugfixes, key: 'bugfix' },
                                        ].filter(group => group.entries.length > 0).map(group => (
                                            <div key={group.key} style={{ marginTop: '16px' }}>
                                                {/* Category header */}
                                                <div style={{
                                                    fontSize: '13px',
                                                    fontWeight: '700',
                                                    color: categoryIcons[group.key].color,
                                                    marginBottom: '10px',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '6px',
                                                }}>
                                                    {categoryIcons[group.key].icon} {getCategoryLabel(group.key)}
                                                </div>

                                                {/* Entries */}
                                                {group.entries.map((entry, eIdx) => (
                                                    <div
                                                        key={eIdx}
                                                        style={{
                                                            padding: '10px 14px',
                                                            marginBottom: '6px',
                                                            borderRadius: '8px',
                                                            background: 'var(--background)',
                                                            border: '1px solid var(--border)',
                                                        }}
                                                    >
                                                        <div style={{
                                                            fontWeight: '600',
                                                            fontSize: '13px',
                                                            color: 'var(--text-primary)',
                                                            marginBottom: '4px',
                                                        }}>
                                                            {getTitle(entry)}
                                                        </div>
                                                        <div style={{
                                                            fontSize: '12px',
                                                            color: 'var(--text-muted)',
                                                            lineHeight: '1.5',
                                                        }}>
                                                            {getDescription(entry)}
                                                        </div>
                                                        {entry.commitHash && (
                                                            <div style={{
                                                                fontSize: '10px',
                                                                color: 'var(--text-muted)',
                                                                marginTop: '6px',
                                                                fontFamily: 'monospace',
                                                                opacity: 0.7,
                                                            }}>
                                                                #{entry.commitHash.substring(0, 7)}
                                                            </div>
                                                        )}
                                                    </div>
                                                ))}
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    )
                })}
            </div>
        </div>
    )
}
