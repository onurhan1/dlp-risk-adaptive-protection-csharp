'use client'

import React, { useState, useRef, useEffect, useCallback } from 'react'
import apiClient from '@/lib/axios'

interface Message {
    id: string
    role: 'user' | 'assistant'
    content: string
    timestamp: Date
}

// ─── Flow 2: Mercek Keyword Analysis ────────────────────────────────────────
// "X ifadesiyle analiz yap" → Mercek sayfasıyla aynı mantık:
// Tüm veriyi çek, client-side incidentDescription'da case-insensitive filtrele
async function searchMercekKeyword(keyword: string): Promise<string> {
    try {
        // Mercek sayfasıyla aynı davranış: searchTerm KULLANMADAN tüm veriyi çek
        // Böylece case-sensitivity sorunu olmaz, sayfa ile birebir aynı sonuç gelir
        const firstResponse = await apiClient.get('/api/mercek', {
            params: { page: 1, pageSize: 10000 }
        })
        const firstData = firstResponse.data
        const totalPages: number = firstData.totalPages ?? firstData.total_pages ?? 1
        let allItems: any[] = firstData.items || []

        // Kalan sayfaları çek (max 10 sayfa)
        const maxPages = Math.min(totalPages, 10)
        for (let page = 2; page <= maxPages; page++) {
            const pageResponse = await apiClient.get('/api/mercek', {
                params: { page, pageSize: 10000 }
            })
            allItems = allItems.concat(pageResponse.data?.items || [])
        }

        if (allItems.length === 0) {
            return `🔍 **Mercek Analizi: "${keyword}"**\n\nMercek veritabaninda hicbir kayit bulunamadi.`
        }

        const kw = keyword.toLowerCase()

        // API snake_case döndürüyor (Program.cs: SnakeCaseLower policy)
        // Mercek sayfasındaki "Olay Açıklaması" sütun filtresiyle birebir aynı mantık
        const descItems = allItems.filter((r: any) =>
            ((r.incident_description ?? r.incidentDescription) || '').toLowerCase().includes(kw)
        )
        const descMatches = descItems.length

        if (descMatches === 0) {
            return `🔍 **Mercek Analizi: "${keyword}"**\n\nOlay Aciklamasi alaninda **"${keyword}"** ifadesi bulunamadi.\n\n💡 Farkli bir ifade deneyin.`
        }

        const uniqueUsers = Array.from(new Set(
            descItems.map((r: any) => r.user_name ?? r.userName).filter(Boolean)
        )) as string[]

        let result = `🔍 **Mercek Analizi: "${keyword}"**\n\n`
        result += `📊 **Olay Aciklamasinda Eslesen: ${descMatches} kayit**\n`
        if (totalPages > maxPages) {
            result += `\n⚠️ _Toplam veriden ilk ${allItems.length} tanesi analiz edildi._\n`
        }
        if (uniqueUsers.length > 0) {
            result += `\n👤 **Benzersiz Kullanici Sayisi: ${uniqueUsers.length}**\n`
            uniqueUsers.slice(0, 8).forEach((u: string) => { result += `• ${u}\n` })
            if (uniqueUsers.length > 8) result += `• ... ve ${uniqueUsers.length - 8} kisi daha\n`
        }
        return result
    } catch (error: any) {
        if (error?.response?.status === 401) {
            return `⚠️ **Yetki Hatasi**\n\nMercek verilerine erismek icin oturum acmaniz gerekiyor.`
        }
        return `❌ **Mercek API Hatasi**\n\n"${keyword}" aramasi sirasinda bir hata olustu.\n_Hata: ${error?.message || 'Bilinmeyen hata'}_`
    }
}

// ─── Shared Helpers: Fetch incidents & exceptions (reusable across flows) ────

async function fetchIncidentsForChatbot(startDate?: string): Promise<any[]> {
    try {
        const params: any = { limit: 10000, orderBy: 'timestamp_desc' }
        if (startDate) params.startDate = startDate
        const response = await apiClient.get('/api/incidents', {
            params,
            timeout: 60000
        })
        const arr = Array.isArray(response.data) ? response.data : []
        return arr.map((item: any) => ({
            id: item.id,
            timestamp: item.timestamp,
            policy: item.policy,
            violationTriggers: item.violationTriggers || item.violation_triggers || item.ViolationTriggers || undefined,
            userEmail: item.userEmail || item.user_email || '',
            loginName: item.loginName || item.login_name || '',
            fullName: item.fullName || item.full_name || '',
            action: item.action || item.Action || 'Permit',
            channel: item.channel || item.Channel || '',
            destination: item.destination || item.Destination || '',
            severity: item.severity,
            riskScore: item.riskScore || item.risk_score || 0,
            riskLevel: item.riskLevel || item.risk_level || '',
            recommendedAction: item.recommendedAction || item.recommended_action || '',
            ruleName: item.ruleName || item.rule_name || '',
        }))
    } catch {
        return []
    }
}

async function fetchPolicyExceptionsForChatbot(): Promise<{
    policies: { policyName: string; rules: { ruleName: string; exceptions: string[] }[] }[];
    totalExceptions: number;
}> {
    try {
        const response = await apiClient.get('/api/policy-exceptions', { timeout: 30000 })
        let rawData: any[] = []
        let totalExceptions = 0

        if (response.data?.success) {
            rawData = response.data.data || []
            totalExceptions = response.data.totalExceptions || response.data.total_exceptions || 0
        } else if (Array.isArray(response.data)) {
            rawData = response.data
            totalExceptions = rawData.length
        }

        const policies = rawData.map((p: any) => ({
            policyName: p.policyName || p.policy_name || '',
            rules: (p.rules || []).map((r: any) => ({
                ruleName: r.ruleName || r.rule_name || '',
                exceptions: r.exceptions || []
            }))
        }))

        return { policies, totalExceptions }
    } catch {
        return { policies: [], totalExceptions: 0 }
    }
}

async function fetchRecommendation(riskScore: number, channel: string): Promise<any> {
    try {
        const response = await apiClient.post('/api/policies/recommendations', {
            risk_score: riskScore,
            channel: channel,
        })
        return response.data
    } catch {
        return null
    }
}

// ─── Flow 3: Destination / User Based DLP Analysis ─────────────────────────
async function analyzeDestinationOrUser(query: string): Promise<string> {
    try {
        const [allIncidents, excData] = await Promise.all([
            fetchIncidentsForChatbot(),
            fetchPolicyExceptionsForChatbot()
        ])

        const q = query.toLowerCase().trim()
        const now = new Date()
        const oneMonthAgo = new Date(now); oneMonthAgo.setMonth(oneMonthAgo.getMonth() - 1)
        const threeMonthsAgo = new Date(now); threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3)

        const matchIncident = (inc: any) => {
            const dest = (inc.destination || '').toLowerCase()
            const email = (inc.userEmail || '').toLowerCase()
            const login = (inc.loginName || '').toLowerCase()
            const fullName = (inc.fullName || '').toLowerCase()
            const triggers = (inc.violationTriggers || '').toLowerCase()
            return dest.includes(q) || email.includes(q) || login.includes(q) || fullName.includes(q) || triggers.includes(q)
        }

        const filtered = allIncidents.filter(matchIncident)

        // Cross-reference: find exceptions matching the query or related to filtered incidents
        const matchingExceptions: { policyName: string; ruleName: string; exceptionName: string; incidentCount: number }[] = []
        excData.policies.forEach(policy => {
            policy.rules.forEach(rule => {
                rule.exceptions.forEach(excName => {
                    const excLower = excName.toLowerCase()
                    const relatedIncidents = filtered.filter(inc => {
                        const triggers = (inc.violationTriggers || '').toLowerCase()
                        return triggers.includes(excLower)
                    }).length
                    if (excLower.includes(q) || relatedIncidents > 0) {
                        matchingExceptions.push({
                            policyName: policy.policyName,
                            ruleName: rule.ruleName,
                            exceptionName: excName,
                            incidentCount: relatedIncidents
                        })
                    }
                })
            })
        })

        if (filtered.length === 0 && matchingExceptions.length === 0) {
            return `🔍 **Analiz: "${query}"**\n\nBu ifadeyle eslesen hicbir incident veya exception bulunamadi.\n\n💡 Farkli bir destination, kullanici adi veya exception ismi deneyin.`
        }

        let result = `📊 **Analiz: "${query}"**\n\n`

        // ── Incident Analysis ──
        if (filtered.length > 0) {
            const getDate = (inc: any) => new Date(inc.timestamp)
            const last1Month = filtered.filter(inc => getDate(inc) >= oneMonthAgo)
            const last3Months = filtered.filter(inc => getDate(inc) >= threeMonthsAgo)

            const actionDist = (list: any[]) => {
                const counts: Record<string, number> = {}
                list.forEach(inc => {
                    const action = inc.action || 'Bilinmiyor'
                    counts[action] = (counts[action] || 0) + 1
                })
                return Object.entries(counts).sort((a, b) => b[1] - a[1])
            }

            result += `## Incident Analizi\n`
            result += `• Son 1 Ay: **${last1Month.length}** incident\n`
            const dist1 = actionDist(last1Month)
            if (dist1.length > 0) {
                dist1.forEach(([action, count]) => { result += `  - ${action}: ${count}\n` })
            }
            result += `• Son 3 Ay: **${last3Months.length}** incident\n`
            const dist3 = actionDist(last3Months)
            if (dist3.length > 0) {
                dist3.forEach(([action, count]) => { result += `  - ${action}: ${count}\n` })
            }
            result += `• Tum Zamanlar: **${filtered.length}** incident\n\n`

            // Recommendation
            const avgRisk = Math.round(filtered.reduce((sum: number, inc: any) => sum + (inc.riskScore || 0), 0) / filtered.length)
            const channelCounts: Record<string, number> = {}
            filtered.forEach((inc: any) => { const ch = inc.channel || 'Email'; channelCounts[ch] = (channelCounts[ch] || 0) + 1 })
            const topChannel = Object.entries(channelCounts).sort((a, b) => b[1] - a[1])[0]?.[0] || 'Email'

            const rec = await fetchRecommendation(avgRisk, topChannel)
            if (rec) {
                result += `## Politika Onerisi\n`
                result += `• Ort. Risk Skoru: **${avgRisk}** | Seviye: **${rec.risk_level || '-'}**\n`
                result += `• Onerilen Aksiyon: **${rec.recommended_action || '-'}**\n`
                result += `• Oncelik: **${rec.priority || '-'}**\n\n`
            }
        }

        // ── Exception Analysis ──
        if (matchingExceptions.length > 0) {
            result += `## Exception Analizi\n`
            result += `• Eslesen Exception: **${matchingExceptions.length}**\n\n`

            const byPolicy: Record<string, typeof matchingExceptions> = {}
            matchingExceptions.forEach(exc => {
                if (!byPolicy[exc.policyName]) byPolicy[exc.policyName] = []
                byPolicy[exc.policyName].push(exc)
            })

            Object.entries(byPolicy).forEach(([policyName, exceptions]) => {
                result += `**${policyName}**\n`
                exceptions.slice(0, 10).forEach(exc => {
                    result += `  • ${exc.ruleName} → ${exc.exceptionName} (${exc.incidentCount} incident)\n`
                })
                if (exceptions.length > 10) {
                    result += `  _... ve ${exceptions.length - 10} exception daha_\n`
                }
            })
        }

        if (filtered.length === 0 && matchingExceptions.length > 0) {
            result += `\n💡 Incident bulunamadi, ancak eslesen exception kayitlari mevcut.`
        }

        return result
    } catch (error: any) {
        if (error?.response?.status === 401) {
            return `⚠️ **Yetki Hatasi**\n\nVerilere erismek icin oturum acmaniz gerekiyor.`
        }
        const detail = error?.response?.status ? `Status: ${error.response.status}` : (error?.message || 'Bilinmeyen hata')
        return `❌ **Analiz Hatasi**\n\n"${query}" icin analiz yapilamadi.\n_Hata: ${detail}_`
    }
}

// ─── Flow 4: DLP + Mercek Combined Analysis ────────────────────────────────
// "X ifadesini dlp ve mercekle birlikte analiz et"
// 1. Mercek incidentDescription'da keyword'ü ara
// 2. Eşleşen kayıtlardan userName + tarih al
// 3. Her kullanıcı için aynı tarihte DLP incident eşleştirmesi yap
async function analyzeCombinedDlpMercek(keyword: string): Promise<string> {
    try {
        // Mercek sayfasıyla aynı mantık: searchTerm KULLANMADAN tüm veriyi çek
        // Böylece case-sensitivity sorunu olmaz
        const firstMercekRes = await apiClient.get('/api/mercek', {
            params: { page: 1, pageSize: 10000 }
        })
        const firstData = firstMercekRes.data
        const mercekTotalPages: number = firstData.totalPages ?? firstData.total_pages ?? 1
        let allMercekItems: any[] = firstData.items || []

        const maxPages = Math.min(mercekTotalPages, 10)
        if (maxPages > 1) {
            const pagePromises = []
            for (let p = 2; p <= maxPages; p++) {
                pagePromises.push(
                    apiClient.get('/api/mercek', { params: { page: p, pageSize: 10000 } })
                )
            }
            const pageResults = await Promise.all(pagePromises)
            pageResults.forEach(res => {
                allMercekItems = allMercekItems.concat(res.data?.items || [])
            })
        }

        const kw = keyword.toLowerCase()
        const toDateStr = (d: any): string => {
            if (!d) return ''
            try { return new Date(d).toISOString().split('T')[0] } catch { return '' }
        }

        // Client-side: sadece incidentDescription'da keyword geçen kayıtları filtrele
        // API snake_case döndürüyor (Program.cs: SnakeCaseLower policy)
        const matchedRecords = allMercekItems.filter((r: any) =>
            ((r.incident_description ?? r.incidentDescription) || '').toLowerCase().includes(kw)
        )

        if (matchedRecords.length === 0) {
            return `🔍 **DLP + Mercek Analizi: "${keyword}"**\n\nOlay Aciklamasi alaninda **"${keyword}"** ifadesi bulunamadi.\n\n💡 Farkli bir ifade deneyin.`
        }

        // Mercek eşleşmelerindeki en eski tarihi bul → DLP'yi bu tarihten itibaren çek
        const allMercekDates: string[] = matchedRecords
            .map((r: any) => toDateStr(r.open_date ?? r.openDate) || toDateStr(r.system_date ?? r.systemDate) || toDateStr(r.start_date ?? r.startDate))
            .filter(Boolean)
        const earliestDate = allMercekDates.length > 0
            ? allMercekDates.sort()[0]
            : undefined

        // DLP incidents ve exceptions'ı paralel çek
        // startDate ile Mercek tarih aralığını kapsayacak şekilde iste
        const [allIncidents, excData] = await Promise.all([
            fetchIncidentsForChatbot(earliestDate),
            fetchPolicyExceptionsForChatbot()
        ])

        // Kullanıcı bazlı gruplama: user → [mercek tarihleri]
        const userDateMap = new Map<string, { dates: Set<string>; mercekCount: number; records: any[] }>()
        matchedRecords.forEach((r: any) => {
            const user = ((r.user_name ?? r.userName) || '').trim()
            if (!user) return
            const date = toDateStr(r.open_date ?? r.openDate) || toDateStr(r.system_date ?? r.systemDate) || toDateStr(r.start_date ?? r.startDate)
            if (!userDateMap.has(user)) {
                userDateMap.set(user, { dates: new Set(), mercekCount: 0, records: [] })
            }
            const entry = userDateMap.get(user)!
            if (date) entry.dates.add(date)
            entry.mercekCount++
            entry.records.push(r)
        })

        if (userDateMap.size === 0) {
            return `🔍 **DLP + Mercek Analizi: "${keyword}"**\n\n"${keyword}" ifadesiyle eslesen kullanici bulunamadi.`
        }

        // Exception isimlerini topla
        const allExceptionNames: string[] = []
        excData.policies.forEach(policy => {
            policy.rules.forEach(rule => {
                rule.exceptions.forEach(exc => allExceptionNames.push(exc))
            })
        })

        // Her kullanıcı için: mercek tarihleriyle DLP incident tarih eşleştirmesi
        type UserRow = {
            user: string
            mercekCount: number
            mercekDates: string[]
            dlpDateMatchCount: number
            dlpDateMatchDetails: { date: string; action: string; destination: string }[]
            exceptionHits: string[]
        }
        const rows: UserRow[] = []

        userDateMap.forEach((data, userName) => {
            const userLower = userName.toLowerCase()

            // Bu kullanıcının tüm DLP incident'ları
            const userIncidents = allIncidents.filter(inc => {
                const email = (inc.userEmail || '').toLowerCase()
                const login = (inc.loginName || '').toLowerCase()
                const fullName = (inc.fullName || '').toLowerCase()
                return email.includes(userLower) || login.includes(userLower) || fullName.includes(userLower)
            })

            // Mercek tarihiyle eşleşen DLP incident'ları bul
            const dateMatchedIncidents = userIncidents.filter(inc => {
                const incDate = toDateStr(inc.timestamp)
                return incDate && data.dates.has(incDate)
            })

            const dlpDetails = dateMatchedIncidents.slice(0, 5).map(inc => ({
                date: toDateStr(inc.timestamp),
                action: inc.action || '-',
                destination: inc.destination || '-'
            }))

            // Mercek açıklamalarında geçen exception isimleri
            const matchedExc = new Set<string>()
            data.records.forEach((record: any) => {
                const desc = ((record.incident_description ?? record.incidentDescription) || '').toLowerCase()
                allExceptionNames.forEach(excName => {
                    if (desc.includes(excName.toLowerCase())) {
                        matchedExc.add(excName)
                    }
                })
            })

            rows.push({
                user: userName,
                mercekCount: data.mercekCount,
                mercekDates: Array.from(data.dates).sort(),
                dlpDateMatchCount: dateMatchedIncidents.length,
                dlpDateMatchDetails: dlpDetails,
                exceptionHits: Array.from(matchedExc)
            })
        })

        rows.sort((a, b) => b.dlpDateMatchCount - a.dlpDateMatchCount || b.mercekCount - a.mercekCount)

        const totalDateMatch = rows.reduce((s, r) => s + r.dlpDateMatchCount, 0)
        const usersWithMatch = rows.filter(r => r.dlpDateMatchCount > 0)
        const usersWithExc = rows.filter(r => r.exceptionHits.length > 0)

        // Recommendation hesapla
        let recData: any = null
        if (usersWithMatch.length > 0) {
            const matchedIncs = allIncidents.filter(inc => {
                const incDate = toDateStr(inc.timestamp)
                const incUser = (inc.userEmail || inc.loginName || inc.fullName || '').toLowerCase()
                return rows.some(r => {
                    const userLower = r.user.toLowerCase()
                    return (incUser.includes(userLower)) && r.mercekDates.includes(incDate)
                })
            })
            if (matchedIncs.length > 0) {
                const avgRisk = Math.round(matchedIncs.reduce((s: number, i: any) => s + (i.riskScore || 0), 0) / matchedIncs.length)
                const chCounts: Record<string, number> = {}
                matchedIncs.forEach((i: any) => { const c = i.channel || 'Email'; chCounts[c] = (chCounts[c] || 0) + 1 })
                const topCh = Object.entries(chCounts).sort((a, b) => b[1] - a[1])[0]?.[0] || 'Email'
                const rec = await fetchRecommendation(avgRisk, topCh)
                if (rec) recData = { avgRisk, ...rec }
            }
        }

        // ── Premium HTML çıktı oluştur ──
        let h = ''

        // Başlık kartı
        h += `<div style="background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:12px;padding:16px 18px;margin-bottom:12px;border:1px solid rgba(99,102,241,0.3)">`
        h += `<div style="font-size:14px;font-weight:700;color:#a5b4fc;margin-bottom:8px">📊 DLP + Mercek Birlesik Analiz</div>`
        h += `<div style="font-size:11px;color:#94a3b8;margin-bottom:10px">"${keyword}" ifadesi icin capraz analiz sonuclari</div>`
        h += `<div style="display:flex;gap:8px;flex-wrap:wrap">`
        h += `<span style="background:rgba(99,102,241,0.15);color:#818cf8;padding:4px 10px;border-radius:20px;font-size:11px;font-weight:600;border:1px solid rgba(99,102,241,0.25)">${matchedRecords.length} Mercek Kayit</span>`
        h += `<span style="background:rgba(16,185,129,0.15);color:#6ee7b7;padding:4px 10px;border-radius:20px;font-size:11px;font-weight:600;border:1px solid rgba(16,185,129,0.25)">${rows.length} Kullanici</span>`
        h += `<span style="background:rgba(245,158,11,0.15);color:#fbbf24;padding:4px 10px;border-radius:20px;font-size:11px;font-weight:600;border:1px solid rgba(245,158,11,0.25)">${totalDateMatch} DLP Eslesmesi</span>`
        if (usersWithExc.length > 0) {
            h += `<span style="background:rgba(239,68,68,0.15);color:#fca5a5;padding:4px 10px;border-radius:20px;font-size:11px;font-weight:600;border:1px solid rgba(239,68,68,0.25)">${usersWithExc.length} Exception</span>`
        }
        h += `</div></div>`

        // Ana tablo
        h += `<div style="border-radius:10px;overflow:hidden;border:1px solid rgba(255,255,255,0.08);margin-bottom:12px">`
        h += `<table style="width:100%;border-collapse:collapse;font-size:11px">`
        h += `<thead><tr style="background:linear-gradient(135deg,#1e293b,#334155)">`
        h += `<th style="padding:8px 10px;text-align:left;color:#94a3b8;font-weight:600;font-size:10px;text-transform:uppercase;letter-spacing:0.5px;border-bottom:2px solid rgba(99,102,241,0.3)">Kullanici</th>`
        h += `<th style="padding:8px 6px;text-align:center;color:#94a3b8;font-weight:600;font-size:10px;text-transform:uppercase;letter-spacing:0.5px;border-bottom:2px solid rgba(99,102,241,0.3)">Mercek</th>`
        h += `<th style="padding:8px 6px;text-align:center;color:#94a3b8;font-weight:600;font-size:10px;text-transform:uppercase;letter-spacing:0.5px;border-bottom:2px solid rgba(99,102,241,0.3)">DLP</th>`
        h += `<th style="padding:8px 6px;text-align:center;color:#94a3b8;font-weight:600;font-size:10px;text-transform:uppercase;letter-spacing:0.5px;border-bottom:2px solid rgba(99,102,241,0.3)">Exc.</th>`
        h += `<th style="padding:8px 10px;text-align:left;color:#94a3b8;font-weight:600;font-size:10px;text-transform:uppercase;letter-spacing:0.5px;border-bottom:2px solid rgba(99,102,241,0.3)">Tarihler</th>`
        h += `</tr></thead><tbody>`

        rows.slice(0, 15).forEach((row, idx) => {
            const bg = idx % 2 === 0 ? 'rgba(15,23,42,0.6)' : 'rgba(30,41,59,0.4)'
            const dlpColor = row.dlpDateMatchCount > 0 ? '#fbbf24' : '#475569'
            const excColor = row.exceptionHits.length > 0 ? '#f87171' : '#475569'
            const dlpBg = row.dlpDateMatchCount > 0 ? 'rgba(245,158,11,0.12)' : 'transparent'
            const excBg = row.exceptionHits.length > 0 ? 'rgba(239,68,68,0.12)' : 'transparent'
            h += `<tr style="background:${bg};border-bottom:1px solid rgba(255,255,255,0.04)">`
            h += `<td style="padding:7px 10px;color:#e2e8f0;font-weight:500">${row.user}</td>`
            h += `<td style="padding:7px 6px;text-align:center;color:#818cf8;font-weight:700">${row.mercekCount}</td>`
            h += `<td style="padding:7px 6px;text-align:center"><span style="background:${dlpBg};color:${dlpColor};font-weight:700;padding:2px 8px;border-radius:10px">${row.dlpDateMatchCount}</span></td>`
            h += `<td style="padding:7px 6px;text-align:center"><span style="background:${excBg};color:${excColor};font-weight:700;padding:2px 8px;border-radius:10px">${row.exceptionHits.length}</span></td>`
            h += `<td style="padding:7px 10px;color:#64748b;font-size:10px">${row.mercekDates.slice(0, 2).join(', ')}${row.mercekDates.length > 2 ? ' +' + (row.mercekDates.length - 2) : ''}</td>`
            h += `</tr>`
        })
        h += `</tbody></table></div>`
        if (rows.length > 15) {
            h += `<div style="text-align:center;color:#64748b;font-size:10px;margin-bottom:10px;font-style:italic">... ve ${rows.length - 15} kullanici daha</div>`
        }

        // Tarih eşleşme detayları
        if (usersWithMatch.length > 0) {
            h += `<div style="background:rgba(245,158,11,0.06);border:1px solid rgba(245,158,11,0.2);border-radius:10px;padding:12px 14px;margin-bottom:12px">`
            h += `<div style="font-size:12px;font-weight:700;color:#fbbf24;margin-bottom:8px">⚡ Tarih Eslesmesi Detaylari</div>`
            usersWithMatch.slice(0, 5).forEach(row => {
                h += `<div style="margin-bottom:8px">`
                h += `<div style="font-size:11px;font-weight:600;color:#e2e8f0;margin-bottom:4px">👤 ${row.user}</div>`
                if (row.dlpDateMatchDetails.length > 0) {
                    row.dlpDateMatchDetails.forEach(d => {
                        const actionColor = d.action === 'Block' ? '#f87171' : d.action === 'Permit' ? '#6ee7b7' : '#fbbf24'
                        h += `<div style="display:flex;gap:6px;align-items:center;padding:3px 0 3px 12px;font-size:10px">`
                        h += `<span style="color:#64748b">📅 ${d.date}</span>`
                        h += `<span style="color:${actionColor};font-weight:600;background:${actionColor}18;padding:1px 6px;border-radius:8px">${d.action}</span>`
                        h += `<span style="color:#94a3b8;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:120px" title="${d.destination}">→ ${d.destination}</span>`
                        h += `</div>`
                    })
                }
                h += `</div>`
            })
            if (usersWithMatch.length > 5) {
                h += `<div style="color:#92400e;font-size:10px;font-style:italic">... ve ${usersWithMatch.length - 5} kullanici daha</div>`
            }
            h += `</div>`
        }

        // Exception bilgisi
        if (usersWithExc.length > 0) {
            h += `<div style="background:rgba(239,68,68,0.06);border:1px solid rgba(239,68,68,0.2);border-radius:10px;padding:12px 14px;margin-bottom:12px">`
            h += `<div style="font-size:12px;font-weight:700;color:#f87171;margin-bottom:8px">🛡️ Mercek Aciklamalarindaki Exception'lar</div>`
            usersWithExc.slice(0, 8).forEach(row => {
                h += `<div style="display:flex;gap:6px;align-items:baseline;padding:3px 0;font-size:11px">`
                h += `<span style="color:#e2e8f0;font-weight:500;flex-shrink:0">${row.user}:</span>`
                h += `<span style="color:#fca5a5">${row.exceptionHits.join(', ')}</span>`
                h += `</div>`
            })
            h += `</div>`
        }

        // Politika önerisi
        if (recData) {
            const riskColor = recData.avgRisk >= 70 ? '#f87171' : recData.avgRisk >= 40 ? '#fbbf24' : '#6ee7b7'
            h += `<div style="background:linear-gradient(135deg,rgba(99,102,241,0.08),rgba(139,92,246,0.08));border:1px solid rgba(99,102,241,0.25);border-radius:10px;padding:12px 14px;margin-bottom:4px">`
            h += `<div style="font-size:12px;font-weight:700;color:#a5b4fc;margin-bottom:8px">💡 Politika Onerisi</div>`
            h += `<div style="display:flex;gap:12px;flex-wrap:wrap;font-size:11px">`
            h += `<div><span style="color:#64748b">Risk:</span> <strong style="color:${riskColor}">${recData.avgRisk}</strong></div>`
            h += `<div><span style="color:#64748b">Seviye:</span> <strong style="color:#c4b5fd">${recData.risk_level || '-'}</strong></div>`
            h += `<div><span style="color:#64748b">Aksiyon:</span> <strong style="color:#818cf8">${recData.recommended_action || '-'}</strong></div>`
            if (recData.priority) h += `<div><span style="color:#64748b">Oncelik:</span> <strong style="color:#fbbf24">${recData.priority}</strong></div>`
            h += `</div></div>`
        }

        if (usersWithMatch.length === 0) {
            h += `<div style="background:rgba(30,41,59,0.5);border:1px solid rgba(255,255,255,0.08);border-radius:8px;padding:10px 14px;color:#94a3b8;font-size:11px;text-align:center">💡 Mercek'te eslesen kullanicilar icin ayni tarihte DLP incident bulunamadi.</div>`
        }

        return h
    } catch (error: any) {
        if (error?.response?.status === 401) {
            return `⚠️ **Yetki Hatasi**\n\nVerilere erismek icin oturum acmaniz gerekiyor.`
        }
        const detail = error?.response?.status ? `Status: ${error.response.status}` : (error?.message || 'Bilinmeyen hata')
        return `❌ **Birlesik Analiz Hatasi**\n\n"${keyword}" icin DLP + Mercek analizi yapilamadi.\n_Hata: ${detail}_`
    }
}

// ─── Keyword Detection Helpers ──────────────────────────────────────────────

// Detects: "X ifadesiyle analiz yap" / "X kelimesiyle analiz yap" (single or multi-word before the trigger)
function detectMercekKeyword(msg: string): string | null {
    // Pattern: "ANYTHING ifadesiyle analiz yap" or "ANYTHING kelimesiyle analiz yap"
    const m1 = msg.match(/^(.+?)\s+(?:ifadesiyle|kelimesiyle|ifadesini|kelimesini)\s+analiz/i)
    if (m1) return m1[1].trim()

    // Pattern: quoted keyword
    const m2 = msg.match(/["'`](.+?)["'`]\s*(?:ifadesiyle|kelimesiyle|ile|analiz)/i)
    if (m2) return m2[1].trim()

    return null
}

// Detects: "X ifadesini dlp ve mercekle birlikte analiz et"
function detectCombinedQuery(msg: string): string | null {
    const m1 = msg.match(/^(.+?)\s+(?:ifadesini|kelimesini)\s+(?:dlp\s+ve\s+mercek|mercek\s+ve\s+dlp|dlp.*mercek|mercek.*dlp).*analiz/i)
    if (m1) return m1[1].trim()

    const m2 = msg.match(/["'`](.+?)["'`]\s*(?:ifadesini|kelimesini)?\s*(?:dlp.*mercek|mercek.*dlp).*analiz/i)
    if (m2) return m2[1].trim()

    return null
}

// Detects: "X analiz et" / "X icin analiz yap" (destination or user based)
function detectDestinationQuery(msg: string): string | null {
    // Pattern: "X icin analiz et/yap"
    const m1 = msg.match(/^(.+?)\s+(?:icin|için)\s+analiz/i)
    if (m1) {
        const q = m1[1].trim()
        if (q.length >= 2) return q
    }

    // Pattern: "X analiz et" / "X'i analiz et"
    const m2 = msg.match(/^(.+?)(?:'[iıİI])?\s+analiz\s+et/i)
    if (m2) {
        const q = m2[1].trim()
        if (q.length >= 2) return q
    }

    return null
}

// ─── Flow 1: Fallback ──────────────────────────────────────────────────────
function generateFallbackResponse(): string {
    return `🤔 **Uzgunum, anlayamadim.**\n\nAsagidaki komutlari deneyebilirsiniz:\n\n• **Mercek Analizi:** _"leasing ifadesiyle analiz yap"_\n• **Destination/Kullanici Analizi:** _"gmail.com icin analiz et"_\n  _(Incident + Exception + Politika Onerisi)_\n• **DLP + Mercek Birlesik Analiz:** _"leasing ifadesini dlp ve mercekle birlikte analiz et"_\n  _(Mercek + DLP + Exception + Politika Onerisi)_`
}

// ─── Static Knowledge Base (Azure fallback) ─────────────────────────────────
function generateResponse(userMessage: string): string {
    const msg = userMessage.toLowerCase()

    if (/^(merhaba|selam|hi|hello|hey|gunaydin|iyi gunler)/.test(msg)) {
        return '👋 **Merhaba! Ben Radarix.**\n\nDLP sistemimiz icin buradayim. Nasil yardimci olabilirim?'
    }
    if (/^(gule gule|hosca kal|bye|tamam tesekkur|tesekkurler|sagol|gorusuruz)/.test(msg)) {
        return '👋 **Gorusmek uzere!** 🛡️'
    }
    if (/ne yapabilir|neler yapabilir|yardim|help|nasil kullan/.test(msg)) {
        return '🤖 **Radarix Yetenekleri**\n\n1. **Mercek Analizi** — _"leasing ifadesiyle analiz yap"_\n2. **Destination/Kullanici Analizi** — _"gmail.com icin analiz et"_\n   _(Incident + Exception + Politika Onerisi)_\n3. **DLP + Mercek Birlesik** — _"leasing ifadesini dlp ve mercekle birlikte analiz et"_\n   _(Mercek + DLP + Exception + Politika Onerisi)_'
    }

    return generateFallbackResponse()
}

export default function ChatBot() {
    const [isOpen, setIsOpen] = useState(false)
    const [isMinimized, setIsMinimized] = useState(false)
    const [messages, setMessages] = useState<Message[]>([
        {
            id: '1',
            role: 'assistant',
            content: '👋 **Merhaba! Ben Radarix.**\n\nDLP sistemimiz icin buradayim.',
            timestamp: new Date(),
        },
    ])
    const [inputValue, setInputValue] = useState('')
    const [isTyping, setIsTyping] = useState(false)
    const [hasNewMessage, setHasNewMessage] = useState(false)
    const messagesEndRef = useRef<HTMLDivElement>(null)
    const inputRef = useRef<HTMLInputElement>(null)

    const scrollToBottom = useCallback(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }, [])

    useEffect(() => {
        scrollToBottom()
    }, [messages, scrollToBottom])

    useEffect(() => {
        if (isOpen && !isMinimized) {
            setHasNewMessage(false)
            setTimeout(() => inputRef.current?.focus(), 100)
        }
    }, [isOpen, isMinimized])

    const sendMessage = useCallback(async (text?: string) => {
        const messageText = text || inputValue.trim()
        if (!messageText) return

        const userMessage: Message = {
            id: Date.now().toString(),
            role: 'user',
            content: messageText,
            timestamp: new Date(),
        }

        setMessages((prev: Message[]) => [...prev, userMessage])
        setInputValue('')
        setIsTyping(true)

        try {
            let responseText: string | null = null

            // ── Flow 4: DLP + Mercek Combined (most specific — check FIRST) ──
            const combinedKeyword = detectCombinedQuery(messageText)
            if (combinedKeyword) {
                responseText = await analyzeCombinedDlpMercek(combinedKeyword)
            }

            // ── Flow 2: Mercek Keyword Analysis ─────────────────────────
            if (!responseText && /ifadesiyle\s+analiz|kelimesiyle\s+analiz/i.test(messageText)) {
                const keyword = detectMercekKeyword(messageText)
                if (keyword && keyword.length >= 2) {
                    responseText = await searchMercekKeyword(keyword)
                }
            }

            // ── Flow 3: Destination / User Based Analysis ───────────────
            if (!responseText && /analiz\s+et|icin\s+analiz|için\s+analiz/i.test(messageText)) {
                const destQuery = detectDestinationQuery(messageText)
                if (destQuery) {
                    responseText = await analyzeDestinationOrUser(destQuery)
                }
            }

            // ── Azure OpenAI Fallback (if configured) ───────────────────
            if (!responseText) {
                try {
                    const history = messages.map(m => ({ role: m.role, content: m.content }))
                    history.push({ role: 'user', content: messageText })
                    const res = await apiClient.post('/api/chatbot/chat', { messages: history }, {
                        _skipAuthRedirect: true
                    } as any)
                    responseText = res.data.reply || null
                } catch {
                    // Azure not available — use static fallback
                }
            }

            // ── Flow 1: Fallback — nothing matched ──────────────────────
            if (!responseText) {
                responseText = generateResponse(messageText)
            }

            const assistantMessage: Message = {
                id: (Date.now() + 1).toString(),
                role: 'assistant',
                content: responseText,
                timestamp: new Date(),
            }
            setMessages((prev: Message[]) => [...prev, assistantMessage])
        } catch (error: any) {
            console.error('ChatBot error:', error)
            const errorMsg: Message = {
                id: (Date.now() + 1).toString(),
                role: 'assistant',
                content: generateFallbackResponse(),
                timestamp: new Date(),
            }
            setMessages((prev: Message[]) => [...prev, errorMsg])
        } finally {
            setIsTyping(false)
            if (!isOpen || isMinimized) setHasNewMessage(true)
        }
    }, [inputValue, isOpen, isMinimized, messages])

    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault()
            sendMessage()
        }
    }

    const clearChat = () => {
        setMessages([
            {
                id: Date.now().toString(),
                role: 'assistant',
                content: '🔄 **Sohbet temizlendi.**\n\nMerhaba! Ben Radarix. DLP sistemimiz icin buradayim.',
                timestamp: new Date(),
            },
        ])
    }

    const formatContent = (content: string) => {
        // Flow 4 gibi saf HTML çıktılarını olduğu gibi döndür
        if (content.trimStart().startsWith('<div') || content.trimStart().startsWith('<table')) {
            return content
        }
        return content
            .split('\n')
            .map((line) => {
                line = line.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
                line = line.replace(/_(.*?)_/g, '<em>$1</em>')
                if (line.startsWith('## ')) return `<div class="chat-heading">${line.slice(3)}</div>`
                if (line.startsWith('• ')) return `<div class="chat-bullet">${line}</div>`
                if (/^\d+\. /.test(line)) return `<div class="chat-numbered">${line}</div>`
                if (line.startsWith('|')) return `<div class="chat-table-row">${line}</div>`
                if (line === '') return '<div class="chat-spacer"></div>'
                return `<div>${line}</div>`
            })
            .join('')
    }

    const timeLabel = (date: Date) =>
        date.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })

    return (
        <>
            {/* Chat Window */}
            {isOpen && (
                <div
                    style={{
                        position: 'fixed',
                        bottom: '90px',
                        right: '24px',
                        width: '390px',
                        height: isMinimized ? '60px' : '600px',
                        background: 'var(--surface)',
                        border: '1px solid rgba(0, 168, 232, 0.25)',
                        borderRadius: '16px',
                        boxShadow: '0 20px 60px rgba(0,0,0,0.18), 0 0 0 1px rgba(0,168,232,0.12)',
                        display: 'flex',
                        flexDirection: 'column',
                        zIndex: 9999,
                        overflow: 'hidden',
                        transition: 'height 0.3s cubic-bezier(0.4,0,0.2,1)',
                        animation: 'chatSlideUp 0.3s cubic-bezier(0.4,0,0.2,1)',
                    }}
                >
                    {/* Header */}
                    <div
                        style={{
                            padding: '14px 16px',
                            background: 'linear-gradient(135deg, #0066cc 0%, #00a8e8 100%)',
                            borderBottom: 'none',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '10px',
                            cursor: 'pointer',
                            flexShrink: 0,
                        }}
                        onClick={() => setIsMinimized(!isMinimized)}
                    >
                        <div style={{
                            width: '36px', height: '36px', borderRadius: '50%',
                            background: 'linear-gradient(135deg, #00a8e8, #0066cc)',
                            display: 'flex', alignItems: 'center', justifyContent: 'center',
                            flexShrink: 0, boxShadow: '0 0 12px rgba(0,168,232,0.5)',
                        }}>
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="white">
                                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" />
                            </svg>
                        </div>

                        <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontSize: '13px', fontWeight: 700, color: '#ffffff', letterSpacing: '0.3px' }}>
                                Radarix
                            </div>
                            <div style={{ fontSize: '11px', color: 'rgba(255,255,255,0.8)', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '1px' }}>
                                <span style={{
                                    width: '6px', height: '6px', borderRadius: '50%',
                                    background: '#7fffcc', display: 'inline-block',
                                    boxShadow: '0 0 6px #7fffcc', animation: 'pulse 2s infinite',
                                }} />
                                Cevrimici
                            </div>
                        </div>

                        <div style={{ display: 'flex', gap: '4px' }} onClick={e => e.stopPropagation()}>
                            {/* Clear button */}
                            <button onClick={clearChat} title="Sohbeti temizle"
                                style={{ background: 'rgba(255,255,255,0.15)', border: '1px solid rgba(255,255,255,0.25)', borderRadius: '6px', color: 'rgba(255,255,255,0.8)', cursor: 'pointer', padding: '4px 6px', transition: 'all 0.2s', display: 'flex', alignItems: 'center' }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.25)'; e.currentTarget.style.color = '#fff' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.15)'; e.currentTarget.style.color = 'rgba(255,255,255,0.8)' }}>
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z" /></svg>
                            </button>
                            {/* Minimize button */}
                            <button onClick={() => setIsMinimized(!isMinimized)}
                                style={{ background: 'rgba(255,255,255,0.15)', border: '1px solid rgba(255,255,255,0.25)', borderRadius: '6px', color: 'rgba(255,255,255,0.8)', cursor: 'pointer', padding: '4px 6px', transition: 'all 0.2s', display: 'flex', alignItems: 'center' }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.25)'; e.currentTarget.style.color = '#fff' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.15)'; e.currentTarget.style.color = 'rgba(255,255,255,0.8)' }}>
                                {isMinimized
                                    ? <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14l5-5 5 5h-10z" /></svg>
                                    : <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M7 10l5 5 5-5h-10z" /></svg>}
                            </button>
                            {/* Close button */}
                            <button onClick={() => setIsOpen(false)}
                                style={{ background: 'rgba(255,255,255,0.15)', border: '1px solid rgba(255,255,255,0.25)', borderRadius: '6px', color: 'rgba(255,255,255,0.8)', cursor: 'pointer', padding: '4px 6px', transition: 'all 0.2s', display: 'flex', alignItems: 'center' }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,100,100,0.4)'; e.currentTarget.style.color = '#fff' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.15)'; e.currentTarget.style.color = 'rgba(255,255,255,0.8)' }}>
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" /></svg>
                            </button>
                        </div>
                    </div>

                    {!isMinimized && (
                        <>
                            {/* Messages Area */}
                            <div
                                style={{ flex: 1, overflowY: 'auto', padding: '16px 12px', display: 'flex', flexDirection: 'column', gap: '10px', scrollBehavior: 'smooth', background: 'var(--background-secondary)' }}
                                className="chatbot-messages"
                            >
                                {messages.map((message) => (
                                    <div key={message.id} style={{
                                        display: 'flex',
                                        flexDirection: message.role === 'user' ? 'row-reverse' : 'row',
                                        gap: '8px', alignItems: 'flex-start',
                                        animation: 'msgFadeIn 0.25s ease',
                                    }}>
                                        {message.role === 'assistant' && (
                                            <div style={{ width: '28px', height: '28px', borderRadius: '50%', background: 'linear-gradient(135deg, #00a8e8, #0066cc)', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, marginTop: '2px', boxShadow: '0 0 8px rgba(0,168,232,0.4)' }}>
                                                <svg width="14" height="14" viewBox="0 0 24 24" fill="white"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" /></svg>
                                            </div>
                                        )}

                                        <div style={{ maxWidth: '82%', display: 'flex', flexDirection: 'column', gap: '3px', alignItems: message.role === 'user' ? 'flex-end' : 'flex-start' }}>
                                            <div style={{
                                                padding: '10px 13px',
                                                borderRadius: message.role === 'user' ? '14px 14px 4px 14px' : '4px 14px 14px 14px',
                                                background: message.role === 'user' ? 'linear-gradient(135deg, #0066cc, #00a8e8)' : 'var(--surface)',
                                                border: message.role === 'user' ? 'none' : '1px solid var(--border)',
                                                color: message.role === 'user' ? '#ffffff' : 'var(--text-primary)',
                                                fontSize: '12.5px', lineHeight: '1.5',
                                                boxShadow: message.role === 'user' ? '0 4px 12px rgba(0,102,204,0.25)' : '0 1px 4px rgba(0,0,0,0.07)',
                                            }}
                                                dangerouslySetInnerHTML={{ __html: formatContent(message.content) }}
                                            />
                                            <span style={{ fontSize: '10px', color: 'var(--text-muted)', paddingLeft: '2px', paddingRight: '2px' }}>
                                                {timeLabel(message.timestamp)}
                                            </span>
                                        </div>

                                        {message.role === 'user' && (
                                            <div style={{ width: '28px', height: '28px', borderRadius: '50%', background: 'linear-gradient(135deg, #6366f1, #8b5cf6)', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, marginTop: '2px' }}>
                                                <svg width="14" height="14" viewBox="0 0 24 24" fill="white"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" /></svg>
                                            </div>
                                        )}
                                    </div>
                                ))}

                                {isTyping && (
                                    <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-start', animation: 'msgFadeIn 0.25s ease' }}>
                                        <div style={{ width: '28px', height: '28px', borderRadius: '50%', background: 'linear-gradient(135deg, #00a8e8, #0066cc)', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, boxShadow: '0 0 8px rgba(0,168,232,0.4)' }}>
                                            <svg width="14" height="14" viewBox="0 0 24 24" fill="white"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" /></svg>
                                        </div>
                                        <div style={{ padding: '12px 16px', borderRadius: '4px 14px 14px 14px', background: 'var(--surface)', border: '1px solid var(--border)', boxShadow: '0 1px 4px rgba(0,0,0,0.07)', display: 'flex', gap: '4px', alignItems: 'center' }}>
                                            {[0, 1, 2].map(i => (
                                                <div key={i} style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#00a8e8', animation: `typingDot 1.2s ${i * 0.2}s infinite` }} />
                                            ))}
                                        </div>
                                    </div>
                                )}

                                <div ref={messagesEndRef} />
                            </div>

                            {/* Input Area */}
                            <div style={{ padding: '10px 12px 14px', borderTop: '1px solid var(--border)', background: 'var(--surface)', display: 'flex', gap: '8px', alignItems: 'center', flexShrink: 0 }}>
                                <input
                                    ref={inputRef}
                                    type="text"
                                    value={inputValue}
                                    onChange={e => setInputValue(e.target.value)}
                                    onKeyDown={handleKeyDown}
                                    placeholder="Ornek: leasing ifadesiyle analiz yap..."
                                    style={{ flex: 1, background: 'var(--background-secondary)', border: '1px solid var(--border)', borderRadius: '10px', padding: '9px 13px', color: 'var(--text-primary)', fontSize: '12.5px', outline: 'none', transition: 'all 0.2s' }}
                                    onFocus={(e: React.FocusEvent<HTMLInputElement>) => { e.target.style.borderColor = 'var(--primary)'; e.target.style.boxShadow = '0 0 0 3px rgba(59,130,246,0.12)' }}
                                    onBlur={(e: React.FocusEvent<HTMLInputElement>) => { e.target.style.borderColor = 'var(--border)'; e.target.style.boxShadow = 'none' }}
                                />
                                <button
                                    onClick={() => sendMessage()}
                                    disabled={!inputValue.trim() || isTyping}
                                    style={{
                                        width: '36px', height: '36px', borderRadius: '10px',
                                        background: inputValue.trim() && !isTyping ? 'linear-gradient(135deg, #00a8e8, #0066cc)' : 'rgba(255,255,255,0.05)',
                                        border: 'none', cursor: inputValue.trim() && !isTyping ? 'pointer' : 'default',
                                        display: 'flex', alignItems: 'center', justifyContent: 'center',
                                        flexShrink: 0, transition: 'all 0.2s',
                                        boxShadow: inputValue.trim() && !isTyping ? '0 4px 12px rgba(0,168,232,0.4)' : 'none',
                                    }}>
                                    <svg width="16" height="16" viewBox="0 0 24 24" fill={inputValue.trim() && !isTyping ? 'white' : 'var(--text-muted)'}>
                                        <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
                                    </svg>
                                </button>
                            </div>
                        </>
                    )}
                </div>
            )}

            {/* FAB Toggle Button */}
            <button
                onClick={() => { setIsOpen(!isOpen); setIsMinimized(false); setHasNewMessage(false) }}
                title="RADAR Guvenlik Asistani"
                style={{
                    position: 'fixed', bottom: '24px', right: '24px',
                    width: '60px', height: '60px', borderRadius: '50%',
                    background: isOpen ? 'linear-gradient(135deg, #004fa3, #0066cc)' : 'linear-gradient(135deg, #0066cc, #00a8e8)',
                    border: isOpen ? '2px solid rgba(0,168,232,0.5)' : '2px solid rgba(255,255,255,0.2)', cursor: 'pointer',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    boxShadow: isOpen ? '0 8px 25px rgba(0,102,204,0.5)' : '0 8px 30px rgba(0,102,204,0.55), 0 4px 10px rgba(0,0,0,0.2)',
                    zIndex: 9998, transition: 'all 0.3s cubic-bezier(0.4,0,0.2,1)', transform: 'scale(1)',
                }}
                onMouseEnter={e => { e.currentTarget.style.transform = 'scale(1.12)' }}
                onMouseLeave={e => { e.currentTarget.style.transform = 'scale(1)' }}
            >
                {isOpen
                    ? <svg width="22" height="22" viewBox="0 0 24 24" fill="white"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" /></svg>
                    : (
                        /* Robot icon */
                        <svg width="30" height="30" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg">
                            {/* Antenna */}
                            <line x1="32" y1="4" x2="32" y2="14" stroke="white" strokeWidth="3" strokeLinecap="round" />
                            <circle cx="32" cy="4" r="3" fill="#7fffcc" />
                            {/* Head */}
                            <rect x="14" y="14" width="36" height="26" rx="7" fill="white" fillOpacity="0.95" />
                            {/* Eyes */}
                            <circle cx="24" cy="25" r="4" fill="#0066cc" />
                            <circle cx="40" cy="25" r="4" fill="#0066cc" />
                            <circle cx="25.5" cy="23.5" r="1.5" fill="white" />
                            <circle cx="41.5" cy="23.5" r="1.5" fill="white" />
                            {/* Mouth */}
                            <rect x="22" y="32" width="20" height="4" rx="2" fill="#00a8e8" fillOpacity="0.8" />
                            {/* Ears */}
                            <rect x="8" y="20" width="6" height="10" rx="3" fill="white" fillOpacity="0.8" />
                            <rect x="50" y="20" width="6" height="10" rx="3" fill="white" fillOpacity="0.8" />
                            {/* Body */}
                            <rect x="18" y="42" width="28" height="16" rx="5" fill="white" fillOpacity="0.7" />
                            <circle cx="27" cy="50" r="3" fill="#00a8e8" fillOpacity="0.9" />
                            <circle cx="37" cy="50" r="3" fill="#7fffcc" fillOpacity="0.9" />
                        </svg>
                    )
                }
                {hasNewMessage && !isOpen && (
                    <div style={{ position: 'absolute', top: '-4px', right: '-4px', width: '16px', height: '16px', borderRadius: '50%', background: '#ef4444', border: '2px solid #0d1117', boxShadow: '0 0 8px rgba(239,68,68,0.6)', animation: 'pulse 1.5s infinite' }} />
                )}
                {!isOpen && (
                    <div style={{ position: 'absolute', inset: '-6px', borderRadius: '22px', border: '2px solid rgba(0,168,232,0.3)', animation: 'ripple 2s infinite', pointerEvents: 'none' }} />
                )}
            </button>

            <style>{`
        @keyframes chatSlideUp {
          from { opacity: 0; transform: translateY(20px) scale(0.95); }
          to   { opacity: 1; transform: translateY(0)    scale(1); }
        }
        @keyframes msgFadeIn {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes typingDot {
          0%, 60%, 100% { transform: translateY(0);   opacity: 0.4; }
          30%            { transform: translateY(-6px); opacity: 1; }
        }
        @keyframes ripple {
          0%   { transform: scale(1);   opacity: 0.6; }
          100% { transform: scale(1.4); opacity: 0; }
        }
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50%       { opacity: 0.5; }
        }
        .chatbot-messages::-webkit-scrollbar { width: 4px; }
        .chatbot-messages::-webkit-scrollbar-track { background: var(--background-secondary); }
        .chatbot-messages::-webkit-scrollbar-thumb { background: var(--border); border-radius: 2px; }
        .chatbot-messages::-webkit-scrollbar-thumb:hover { background: var(--primary); }
        .chat-heading { font-weight: 700; color: var(--primary); font-size: 13px; margin-bottom: 4px; }
        .chat-bullet  { padding-left: 4px; margin: 1px 0; color: var(--text-primary); }
        .chat-numbered { padding-left: 4px; margin: 1px 0; color: var(--text-primary); }
        .chat-table-row { font-family: monospace; font-size: 11px; color: var(--text-secondary); margin: 1px 0; }
        .chat-spacer  { height: 4px; }
      `}</style>
        </>
    )
}
