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
// "X ifadesiyle analiz yap" → searches incidentDescription in mercek, returns count
async function searchMercekKeyword(keyword: string): Promise<string> {
    try {
        const response = await apiClient.get('/api/mercek', {
            params: { page: 1, pageSize: 10000, searchTerm: keyword }
        })
        const data = response.data
        const totalCount: number = data.totalCount ?? data.items?.length ?? 0
        const items: any[] = data.items || []

        if (totalCount === 0) {
            return `🔍 **Mercek Analizi: "${keyword}"**\n\nMercek veritabaninda **"${keyword}"** ifadesi hicbir kayitta bulunamadi.\n\n💡 Farkli bir ifade deneyin.`
        }

        const kw = keyword.toLowerCase()
        const descMatches = items.filter((r: any) =>
            (r.incidentDescription || '').toLowerCase().includes(kw)
        ).length
        const solutionMatches = items.filter((r: any) =>
            (r.solutionMethod || '').toLowerCase().includes(kw)
        ).length
        const uniqueUsers = Array.from(new Set(
            items
                .filter((r: any) =>
                    (r.incidentDescription || '').toLowerCase().includes(kw) ||
                    (r.solutionMethod || '').toLowerCase().includes(kw)
                )
                .map((r: any) => r.userName)
                .filter(Boolean)
        )) as string[]

        let result = `🔍 **Mercek Analizi: "${keyword}"**\n\n`
        result += `📊 **Toplam Eslesen Kayit: ${totalCount}**\n\n`
        if (descMatches > 0) result += `• **Olay Aciklamasinda:** ${descMatches} kayit\n`
        if (solutionMatches > 0) result += `• **Cozum Yonteminde:** ${solutionMatches} kayit\n`
        if (descMatches === 0 && solutionMatches === 0) {
            result += `• Diger alanlarda: ${totalCount} kayit\n`
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

async function fetchIncidentsForChatbot(): Promise<any[]> {
    try {
        const response = await apiClient.get('/api/incidents', {
            params: { limit: 10000, orderBy: 'timestamp_desc' },
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
// Returns: unique users for keyword in mercek + their DLP incident count + exception info
async function analyzeCombinedDlpMercek(keyword: string): Promise<string> {
    try {
        // Fetch mercek, incidents, and exceptions in parallel
        const [mercekResponse, allIncidents, excData] = await Promise.all([
            apiClient.get('/api/mercek', { params: { page: 1, pageSize: 10000, searchTerm: keyword } }),
            fetchIncidentsForChatbot(),
            fetchPolicyExceptionsForChatbot()
        ])

        const mercekData = mercekResponse.data
        const mercekItems: any[] = mercekData.items || []

        if (mercekItems.length === 0) {
            return `🔍 **DLP + Mercek Analizi: "${keyword}"**\n\nMercek veritabaninda **"${keyword}"** ifadesi bulunamadi.`
        }

        const kw = keyword.toLowerCase()
        const relevantItems = mercekItems.filter((r: any) =>
            (r.incidentDescription || '').toLowerCase().includes(kw) ||
            (r.solutionMethod || '').toLowerCase().includes(kw) ||
            (r.summaryDescription || '').toLowerCase().includes(kw)
        )

        const uniqueUsers = Array.from(new Set(
            relevantItems.map((r: any) => r.userName).filter(Boolean)
        )) as string[]

        if (uniqueUsers.length === 0) {
            return `🔍 **DLP + Mercek Analizi: "${keyword}"**\n\n"${keyword}" ifadesiyle eslesen kullanici bulunamadi.`
        }

        // Collect all exception names for keyword matching
        const allExceptionNames: string[] = []
        excData.policies.forEach(policy => {
            policy.rules.forEach(rule => {
                rule.exceptions.forEach(exc => allExceptionNames.push(exc))
            })
        })

        const today = new Date()
        const todayStr = today.toISOString().split('T')[0]
        const thirtyDaysAgo = new Date(today); thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30)

        const tableRows: { user: string; mercekCount: number; dlpTodayCount: number; dlp30dCount: number; exceptionHits: number }[] = []

        uniqueUsers.forEach(userName => {
            const userLower = userName.toLowerCase()

            const userMercekCount = relevantItems.filter((r: any) =>
                (r.userName || '').toLowerCase() === userLower
            ).length

            const userIncidents = allIncidents.filter(inc => {
                const email = (inc.userEmail || '').toLowerCase()
                const login = (inc.loginName || '').toLowerCase()
                const fullName = (inc.fullName || '').toLowerCase()
                return email.includes(userLower) || login.includes(userLower) || fullName.includes(userLower)
            })

            const userDlpToday = userIncidents.filter(inc => {
                const incDate = inc.timestamp ? new Date(inc.timestamp).toISOString().split('T')[0] : ''
                return incDate === todayStr
            }).length

            const userDlp30d = userIncidents.filter(inc => {
                return inc.timestamp && new Date(inc.timestamp) >= thirtyDaysAgo
            }).length

            // Count how many of this user's incidents have exception matches in violationTriggers
            const exceptionHits = userIncidents.filter(inc => {
                const triggers = (inc.violationTriggers || '').toLowerCase()
                if (!triggers) return false
                return allExceptionNames.some(excName => triggers.includes(excName.toLowerCase()))
            }).length

            tableRows.push({ user: userName, mercekCount: userMercekCount, dlpTodayCount: userDlpToday, dlp30dCount: userDlp30d, exceptionHits })
        })

        tableRows.sort((a, b) => b.mercekCount - a.mercekCount)

        let result = `📊 **DLP + Mercek Birlesik Analiz: "${keyword}"**\n\n`
        result += `👤 **Benzersiz Kullanici Sayisi: ${uniqueUsers.length}**\n\n`
        result += `| Kullanici | Mercek | DLP Bugun | DLP 30g | Exception |\n`
        result += `|-----------|:------:|:---------:|:-------:|:---------:|\n`
        tableRows.slice(0, 20).forEach(row => {
            result += `| ${row.user} | ${row.mercekCount} | ${row.dlpTodayCount} | ${row.dlp30dCount} | ${row.exceptionHits} |\n`
        })
        if (tableRows.length > 20) {
            result += `\n_... ve ${tableRows.length - 20} kullanici daha_\n`
        }

        // Summary: keyword matching exceptions
        const kwExceptions = allExceptionNames.filter(exc => exc.toLowerCase().includes(kw))
        if (kwExceptions.length > 0) {
            result += `\n## Iliskili Exception'lar\n`
            result += `• **"${keyword}"** ifadesiyle eslesen **${kwExceptions.length}** exception:\n`
            kwExceptions.slice(0, 8).forEach(exc => { result += `  - ${exc}\n` })
            if (kwExceptions.length > 8) result += `  _... ve ${kwExceptions.length - 8} tane daha_\n`
        }

        // Recommendation based on aggregated risk
        if (allIncidents.length > 0) {
            const relatedIncidents = allIncidents.filter(inc => {
                const userLower = (inc.userEmail || inc.loginName || inc.fullName || '').toLowerCase()
                return uniqueUsers.some(u => userLower.includes(u.toLowerCase()))
            })
            if (relatedIncidents.length > 0) {
                const avgRisk = Math.round(relatedIncidents.reduce((sum: number, inc: any) => sum + (inc.riskScore || 0), 0) / relatedIncidents.length)
                const channelCounts: Record<string, number> = {}
                relatedIncidents.forEach((inc: any) => { const ch = inc.channel || 'Email'; channelCounts[ch] = (channelCounts[ch] || 0) + 1 })
                const topChannel = Object.entries(channelCounts).sort((a, b) => b[1] - a[1])[0]?.[0] || 'Email'

                const rec = await fetchRecommendation(avgRisk, topChannel)
                if (rec) {
                    result += `\n## Politika Onerisi\n`
                    result += `• Ort. Risk: **${avgRisk}** | Seviye: **${rec.risk_level || '-'}**\n`
                    result += `• Onerilen Aksiyon: **${rec.recommended_action || '-'}**\n`
                }
            }
        }

        result += `\n📅 DLP verileri bugunun tarihine (**${todayStr}**) ve son 30 gune gore hesaplanmistir.`
        return result
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
                    const res = await apiClient.post('/api/chatbot/chat', { messages: history })
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
