'use client'

import React, { useState, useRef, useEffect, useCallback } from 'react'
import apiClient from '@/lib/axios'

interface Message {
    id: string
    role: 'user' | 'assistant'
    content: string
    timestamp: Date
}

interface QuickSuggestion {
    label: string
    query: string
}

const QUICK_SUGGESTIONS: QuickSuggestion[] = [
    { label: '🔴 Yüksek Riskli Kullanicilar', query: 'Yuksek riskli kullanicilari nasil analiz edebilirim?' },
    { label: '📊 DLP Politikalari', query: 'DLP politika ihlalleri hakkinda bilgi ver' },
    { label: '🔍 Olay Sorusturma', query: 'Bir guvenlik olayini nasil sorusturuyorum?' },
    { label: '⚡ Otomatik Duzeltme', query: 'Otomatik duzeltme onerileri neler?' },
    { label: '📈 Risk Skoru', query: 'Risk skoru nasil hesaplanir?' },
    { label: '🛡️ Veri Koruma', query: 'Veri sizintisini nasil onleyebilirim?' },
    { label: '🔎 Mercek Kelime Analizi', query: 'leasing kelimesiyle analiz yap' },
]

// ─── Mercek Keyword Analysis via real API ──────────────────────────────────────
async function searchMercekKeyword(keyword: string): Promise<string> {
    try {
        const response = await apiClient.get('/api/mercek', {
            params: { page: 1, pageSize: 10000, searchTerm: keyword }
        })
        const data = response.data
        const totalCount: number = data.totalCount ?? data.items?.length ?? 0
        const items: any[] = data.items || []

        if (totalCount === 0) {
            return `🔍 **Mercek Kelime Analizi: "${keyword}"**\n\nMercek veritabaninda **"${keyword}"** kelimesi hicbir kayitta bulunamadi.\n\n💡 Farkli bir kelime veya yazim deneyin.`
        }

        // Breakdown by field
        const kw = keyword.toLowerCase()
        const descMatches = items.filter(r =>
            (r.incidentDescription || '').toLowerCase().includes(kw)
        ).length
        const solutionMatches = items.filter(r =>
            (r.solutionMethod || '').toLowerCase().includes(kw)
        ).length
        const uniqueUsers = [
            ...new Set(
                items
                    .filter(r =>
                        (r.incidentDescription || '').toLowerCase().includes(kw) ||
                        (r.solutionMethod || '').toLowerCase().includes(kw)
                    )
                    .map((r: any) => r.userName)
                    .filter(Boolean)
            )
        ] as string[]

        let result = `🔍 **Mercek Kelime Analizi: "${keyword}"**\n\n`
        result += `📊 **Toplam Eslesen Kayit: ${totalCount}**\n\n`
        if (descMatches > 0) result += `• **Olay Aciklamasinda:** ${descMatches} kayit\n`
        if (solutionMatches > 0) result += `• **Cozum Yonteminde:** ${solutionMatches} kayit\n`
        if (descMatches === 0 && solutionMatches === 0) {
            result += `• Diger alanlarda: ${totalCount} kayit\n`
        }
        if (uniqueUsers.length > 0) {
            result += `\n👤 **Ilgili Kullanicilar (${uniqueUsers.length}):**\n`
            uniqueUsers.slice(0, 5).forEach((u: string) => { result += `• ${u}\n` })
            if (uniqueUsers.length > 5) result += `• ... ve ${uniqueUsers.length - 5} kisi daha\n`
        }
        result += `\n💡 Detayli inceleme icin **Mercek Analysis** sayfasina gidin ve "${keyword}" ile arama yapin.`
        return result
    } catch (error: any) {
        if (error?.response?.status === 401) {
            return `⚠️ **Yetki Hatasi**\n\nMercek verilerine erismek icin oturum acmaniz gerekiyor.`
        }
        const msg = error?.message || 'Bilinmeyen hata'
        return `❌ **Mercek API Hatasi**\n\n"${keyword}" aramasi sirasinda bir hata olustu. Sunucu baglantisini kontrol edin.\n\n_Hata: ${msg}_`
    }
}

// ─── DLP Knowledge Base ─────────────────────────────────────────────────────
const DLP_KNOWLEDGE: Record<string, string[]> = {
    risk: [
        '📊 **Risk Skoru Analizi**\n\nRADAR sistemi risk skorlarini su faktorlere gore hesaplar:\n\n• **Politika Ihlal Sayisi** — Son 30 gundeki ihlal miktari\n• **Ihlal Siddeti** — Kritik, Yuksek, Orta, Dusuk kategorileri\n• **Hedef Hassasiyeti** — Disariya gonderilen verinin turu\n• **Kullanici Davranis Gecmisi** — Anomali tespiti\n\n💡 **Ipucu:** Risk skoru >80 olan kullanicilar icin acil inceleme baslatin.',
        '🎯 **Risk Seviyesi Detaylari**\n\n| Seviye | Puan | Aksiyon |\n|--------|------|---------|\n| Kritik | 90-100 | Aninda Engelle |\n| Yuksek | 70-89 | Acil Incele |\n| Orta | 40-69 | Gozlemle |\n| Dusuk | 1-39 | Raporla |\n\nKullanici risk puanlari her 15 dakikada bir guncellenir.',
    ],
    dlp: [
        '🛡️ **DLP Politika Ihlalleri**\n\nSik karsilasilan ihlal turleri:\n\n• **Veri Sizdirma** — E-posta veya USB ile hassas veri transferi\n• **Yetkisiz Erisim** — Izinsiz dosya veya sistemlere erisim\n• **Politika Bypass** — Guvenlik kontrollerini atlatma girisimleri\n• **Supeheli Indirme** — Toplu veri indirme aktivitesi\n\n⚠️ Tum ihlaller otomatik olarak DLP motoru tarafindan loglanir.',
        '📋 **Politika Yonetimi**\n\nRADAR uzerinden politikalar su sekilde yonetilir:\n\n1. **Kural Olusturma** — Settings > DLP Rules bolumunden\n2. **Esik Ayarlama** — Her kural icin tetikleme esigi\n3. **Aksiyon Tanimlama** — Bildirim, engelleme veya kayit\n4. **Inceleme Periyodu** — Otomatik gozden gecirme zamanlari\n\n💡 Politikalari duzenli araliklar ile guncellemeniz onerilir.',
    ],
    investigation: [
        '🔍 **Olay Sorusturma Adimlari**\n\n1. **Ilk Degerlendirme** — Olayin siddetini belirle\n2. **Log Analizi** — Ilgili loglari topla ve incele\n3. **Timeline Olusturma** — Olay zaman cizelgesini hazirla\n4. **Etki Analizi** — Etkilenen sistemleri ve verileri tespit et\n5. **Duzeltici Aksiyon** — Gerekli onlemleri al\n6. **Raporlama** — Yonetim raporunu olustur\n\n⏱️ Kritik olaylar icin yanis suresi <1 saat olmalidir.',
        '📁 **Investigation Modulu**\n\nSolda "Investigation" menuunden erisebileceginiz modul sunlari sunar:\n\n• **Alert Details** — Detayli uyari incelemesi\n• **Timeline View** — Kronolojik olay gorunumu\n• **User Activity** — Kullanici aktivite gecmisi\n• **Network Map** — Ag baglanti haritasi\n\n🎯 Her olayi kapatmadan once tam belgeleme yapin.',
    ],
    remediation: [
        '⚡ **Otomatik Duzeltme Secenekleri**\n\nRADAR\'in otomatik duzeltme motoru su aksiyonlari alabilir:\n\n• **Hesap Kilitleme** — Supeheli aktivitede otomatik kilit\n• **Oturum Sonlandirma** — Aktif oturumlari kapatma\n• **Erisim Iptali** — Belirli kaynaklara erisimi engelleme\n• **E-posta Karantina** — Supeheli e-postalari tutma\n• **Dosya Geri Alma** — Yetkisiz transferleri geri alma\n\n⚠️ Otomatik duzeltmeyi etkinlestirmeden once politikalarinizi test edin.',
    ],
    user: [
        '👤 **Kullanici Risk Analizi**\n\nYuksek riskli kullanicilari belirlemek icin:\n\n1. Ana dashboard\'daki **High Risk Users** panelini inceleyin\n2. Kullaniciya tiklayarak **Entity Detail Modal**\'i acin\n3. **Behavioral Analytics** sekmesinde anomalileri gozden gecirin\n4. **Risk Timeline** uzerinde trend analizi yapin\n\n🔴 Risk skoru >85 olan kullanicilar icin HR ile koordinasyon oneririz.',
        '📊 **Kullanici Davranis Analizi**\n\nDAVRANIS PUANLAMA SISTEMI:\n\n• Mesai disi erisim: +15 puan\n• Buyuk dosya transferi: +20 puan\n• Yeni cihazdan erisim: +10 puan\n• Coklu basarisiz giris: +25 puan\n• Normal disi saat aktivitesi: +15 puan\n\n📈 Bu puanlar gercek zamanli olarak hesaplanir ve dashboardda gosterilir.',
    ],
    data: [
        '🔒 **Veri Koruma Stratejileri**\n\nEtkili veri koruma icin oneriler:\n\n1. **Veri Siniflandirma** — Hassasiyet seviyelerine gore etiketleme\n2. **Erisim Kontrolu** — En az ayricalik ilkesi\n3. **Sifreleme** — Hem dinlenme hem de transfer sirasinda\n4. **DLP Politikalari** — Icerik tabanli filtreleme kurallari\n5. **Kullanici Egitimi** — Guvenlik farkindalik programlari\n\n💡 RADAR, veri sizintisini gercek zamanli olarak tespit eder ve engeller.',
    ],
    report: [
        '📈 **Raporlama ve Analytics**\n\nRADAR raporlama ozellikleri:\n\n• **Anlik Raporlar** — Gercek zamanli dashboard gorunumu\n• **Scheduled Reports** — Otomatik periyodik raporlar\n• **Ozel Raporlar** — Ihtiyaca gore ozellestirilebilir\n• **Export Secenekleri** — PDF, Excel, CSV formatlari\n\nRapor almak icin ust menudeki **Reports** bolumune gidin.\n\n📊 Aylik trend raporlari icin "Analytics" menusunu kullanin.',
    ],
}

function generateResponse(userMessage: string): string {
    const msg = userMessage.toLowerCase()

    if (/^(merhaba|selam|hi|hello|hey|gunaydin|iyi gunler)/.test(msg)) {
        return '👋 **Merhaba! RADAR Guvenlik Asistani\'na hos geldiniz.**\n\nSize su konularda yardimci olabilirim:\n\n• 🔴 Risk analizi ve yuksek riskli kullanicilar\n• 🛡️ DLP politikalari ve ihlal yonetimi\n• 🔍 Guvenlik olayi sorusturma\n• ⚡ Otomatik duzeltme aksiyonlari\n• 📊 Raporlama ve analytics\n• 🔎 Mercek kelime analizi (ornek: "leasing kelimesiyle analiz yap")\n\nNasil yardimci olabilirim?'
    }

    if (/^(gule gule|hosca kal|bye|tamam tesekkur|tesekkurler|sagol|gorusuruz)/.test(msg)) {
        return '👋 **Gorusmek uzere!**\n\nHerhangi bir guvenlik sorunuz oldugunda buradayim. RADAR sistemini guvende tutun! 🛡️'
    }

    if (/ne yapabilir|neler yapabilir|yardim|help|nasil kullan/.test(msg)) {
        return '🤖 **RADAR Guvenlik Asistani Yetenekleri**\n\nSize su konularda rehberlik edebilirim:\n\n1. **Risk Yonetimi** — Kullanici ve sistem risk skorlari\n2. **DLP Politikalari** — Ihlal analizi ve politika yonetimi\n3. **Olay Sorusturma** — Adim adim sorusturma rehberi\n4. **Otomatik Duzeltme** — Guvenlik aksiyonlari\n5. **Raporlama** — Dashboard ve raporlar\n6. **Veri Koruma** — En iyi uygulamalar\n7. **Mercek Analizi** — "X kelimesiyle analiz yap" yazarak gercek veritabani aramasi\n\n💡 Asagidaki hizli onerileri veya kendi sorunuzu kullanabilirsiniz.'
    }

    if (/risk|puan|skor|score/.test(msg)) {
        const responses = DLP_KNOWLEDGE.risk
        return responses[Math.floor(Math.random() * responses.length)]
    }

    if (/dlp|politika|ihlal|kural|rule|policy/.test(msg)) {
        const responses = DLP_KNOWLEDGE.dlp
        return responses[Math.floor(Math.random() * responses.length)]
    }

    if (/sorustur|incele|analiz|investigate|olay|alert|uyari/.test(msg)) {
        const responses = DLP_KNOWLEDGE.investigation
        return responses[Math.floor(Math.random() * responses.length)]
    }

    if (/duzeltme|remediat|engel|kapat|kilit|otomatik/.test(msg)) {
        const responses = DLP_KNOWLEDGE.remediation
        return responses[Math.floor(Math.random() * responses.length)]
    }

    if (/kullanici|user|kisi|calisan|employee|davranis|behavior/.test(msg)) {
        const responses = DLP_KNOWLEDGE.user
        return responses[Math.floor(Math.random() * responses.length)]
    }

    if (/veri|data|sizinti|koruma|leak|protect|sifrele|encrypt/.test(msg)) {
        const responses = DLP_KNOWLEDGE.data
        return responses[Math.floor(Math.random() * responses.length)]
    }

    if (/rapor|report|analitik|analytic|istatistik|statistic/.test(msg)) {
        const responses = DLP_KNOWLEDGE.report
        return responses[Math.floor(Math.random() * responses.length)]
    }

    return `🤔 Sorunuzu anlamaya calisiyorum...\n\n"**${userMessage}**" hakkinda dogrudan bilgim olmayabilir. Deneyebileceginiz sorgular:\n\n• Mercek analizi: **"leasing kelimesiyle analiz yap"**\n• Risk skoru icin **"risk"** yazin\n• DLP ihlalleri icin **"dlp politika"** yazin\n• Olay sorusturmasi icin **"sorusturma"** yazin\n\n💡 Veya asagidaki hizli onerilerden birini secebilirsiniz.`
}

// ─── Mercek Keyword Detection ──────────────────────────────────────────────
function detectMercekKeyword(msg: string): string | null {
    // Pattern 1: "X kelimesiyle" / "X kelimesini" / "X kelimesinde"
    const m1 = msg.match(/(\w[\w\s-]{0,30}?)\s+kelimesi(?:yle|ni|nde|nden)?/i)
    if (m1) {
        const kw = m1[1].trim().split(/\s+/).pop() || m1[1].trim()
        return kw
    }

    // Pattern 2: quoted keyword + analiz/mercek
    const m2 = msg.match(/["'`](\w[\w\s-]{0,30}?)["'`]\s*(?:kelime|analiz|ara|bul)/i)
    if (m2 && /mercek|analiz|kelime|bul|ara|kac/i.test(msg)) {
        return m2[1].trim()
    }

    // Pattern 3: "mercek ... X analiz" / "X ... mercek"
    const m3 = msg.match(/(?:mercek\w*\s+(?:\w+\s+){0,5}?)(\w{3,})\s*(?:kelime|analiz|arama|bul|kac)/i)
    if (m3) return m3[1].trim()

    return null
}

export default function ChatBot() {
    const [isOpen, setIsOpen] = useState(false)
    const [isMinimized, setIsMinimized] = useState(false)
    const [messages, setMessages] = useState<Message[]>([
        {
            id: '1',
            role: 'assistant',
            content: '👋 **RADAR Guvenlik Asistani**\n\nMerhaba! DLP guvenlik sisteminiz hakkinda sorularinizi yanitlamak icin buradayim.\n\nRisk analizi, politika ihlalleri, olay sorusturma, veri koruma veya **Mercek kelime analizi** konularinda yardimci olabilirim.\n\n💡 Ornek: _"leasing kelimesiyle analiz yap"_',
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

        // ── Mercek keyword analysis detection ──────────────────────
        const isMercekQuery = /mercek|kelimesiyle|kelimesini|kelimesinde|kelimesinden|kelime.*analiz|analiz.*kelime/i.test(messageText)
        if (isMercekQuery) {
            const keyword = detectMercekKeyword(messageText)
            if (keyword && keyword.length >= 2) {
                // Real API call — no setTimeout, await the result
                const apiResult = await searchMercekKeyword(keyword)
                const assistantMessage: Message = {
                    id: (Date.now() + 1).toString(),
                    role: 'assistant',
                    content: apiResult,
                    timestamp: new Date(),
                }
                setMessages((prev: Message[]) => [...prev, assistantMessage])
                setIsTyping(false)
                if (!isOpen || isMinimized) setHasNewMessage(true)
                return
            }
            // keyword detected but too short — ask user to clarify
            const clarifyMsg: Message = {
                id: (Date.now() + 1).toString(),
                role: 'assistant',
                content: `🔎 **Mercek Analizi**\n\nHangi kelimeyi aramami istiyorsunuz? Lutfen soyle yazin:\n\n_"leasing kelimesiyle analiz yap"_\n_"nda kelimesiyle analiz yap"_`,
                timestamp: new Date(),
            }
            setMessages((prev: Message[]) => [...prev, clarifyMsg])
            setIsTyping(false)
            return
        }
        // ── End Mercek detection ─────────────────────────────────

        // Static knowledge-base response with simulated delay
        const thinkTime = 700 + Math.random() * 1000
        setTimeout(() => {
            const response = generateResponse(messageText)
            const assistantMessage: Message = {
                id: (Date.now() + 1).toString(),
                role: 'assistant',
                content: response,
                timestamp: new Date(),
            }
            setMessages((prev: Message[]) => [...prev, assistantMessage])
            setIsTyping(false)
            if (!isOpen || isMinimized) setHasNewMessage(true)
        }, thinkTime)
    }, [inputValue, isOpen, isMinimized])

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
                content: '🔄 **Sohbet temizlendi.**\n\nYeni bir konusma baslatin. Ornek: _"leasing kelimesiyle analiz yap"_',
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
                        background: 'linear-gradient(135deg, #0d1117 0%, #161b27 100%)',
                        border: '1px solid rgba(0, 168, 232, 0.3)',
                        borderRadius: '16px',
                        boxShadow: '0 25px 60px rgba(0,0,0,0.6), 0 0 0 1px rgba(0,168,232,0.1), inset 0 1px 0 rgba(255,255,255,0.05)',
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
                            background: 'linear-gradient(135deg, rgba(0,168,232,0.15) 0%, rgba(0,100,180,0.1) 100%)',
                            borderBottom: isMinimized ? 'none' : '1px solid rgba(0,168,232,0.2)',
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
                            <div style={{ fontSize: '13px', fontWeight: 700, color: '#e8eaed', letterSpacing: '0.3px' }}>
                                RADAR Guvenlik Asistani
                            </div>
                            <div style={{ fontSize: '11px', color: '#00a8e8', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '1px' }}>
                                <span style={{
                                    width: '6px', height: '6px', borderRadius: '50%',
                                    background: '#00d4aa', display: 'inline-block',
                                    boxShadow: '0 0 6px #00d4aa', animation: 'pulse 2s infinite',
                                }} />
                                Cevrimici
                            </div>
                        </div>

                        <div style={{ display: 'flex', gap: '4px' }} onClick={e => e.stopPropagation()}>
                            {/* Clear button */}
                            <button onClick={clearChat} title="Sohbeti temizle"
                                style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '6px', color: '#8a9199', cursor: 'pointer', padding: '4px 6px', transition: 'all 0.2s', display: 'flex', alignItems: 'center' }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.1)'; e.currentTarget.style.color = '#e8eaed' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; e.currentTarget.style.color = '#8a9199' }}>
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z" /></svg>
                            </button>
                            {/* Minimize button */}
                            <button onClick={() => setIsMinimized(!isMinimized)}
                                style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '6px', color: '#8a9199', cursor: 'pointer', padding: '4px 6px', transition: 'all 0.2s', display: 'flex', alignItems: 'center' }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.1)'; e.currentTarget.style.color = '#e8eaed' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; e.currentTarget.style.color = '#8a9199' }}>
                                {isMinimized
                                    ? <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14l5-5 5 5h-10z" /></svg>
                                    : <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M7 10l5 5 5-5h-10z" /></svg>}
                            </button>
                            {/* Close button */}
                            <button onClick={() => setIsOpen(false)}
                                style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '6px', color: '#8a9199', cursor: 'pointer', padding: '4px 6px', transition: 'all 0.2s', display: 'flex', alignItems: 'center' }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(220,50,50,0.2)'; e.currentTarget.style.color = '#ff6b6b' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; e.currentTarget.style.color = '#8a9199' }}>
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" /></svg>
                            </button>
                        </div>
                    </div>

                    {!isMinimized && (
                        <>
                            {/* Messages Area */}
                            <div
                                style={{ flex: 1, overflowY: 'auto', padding: '16px 12px', display: 'flex', flexDirection: 'column', gap: '10px', scrollBehavior: 'smooth' }}
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
                                                background: message.role === 'user' ? 'linear-gradient(135deg, #00a8e8, #0066cc)' : 'rgba(255,255,255,0.06)',
                                                border: message.role === 'user' ? 'none' : '1px solid rgba(255,255,255,0.08)',
                                                color: '#e8eaed', fontSize: '12.5px', lineHeight: '1.5',
                                                boxShadow: message.role === 'user' ? '0 4px 12px rgba(0,168,232,0.3)' : 'none',
                                            }}
                                                dangerouslySetInnerHTML={{ __html: formatContent(message.content) }}
                                            />
                                            <span style={{ fontSize: '10px', color: '#5a6170', paddingLeft: '2px', paddingRight: '2px' }}>
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
                                        <div style={{ padding: '12px 16px', borderRadius: '4px 14px 14px 14px', background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.08)', display: 'flex', gap: '4px', alignItems: 'center' }}>
                                            {[0, 1, 2].map(i => (
                                                <div key={i} style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#00a8e8', animation: `typingDot 1.2s ${i * 0.2}s infinite` }} />
                                            ))}
                                        </div>
                                    </div>
                                )}

                                <div ref={messagesEndRef} />
                            </div>

                            {/* Quick Suggestions */}
                            <div style={{ padding: '8px 12px 4px', borderTop: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexWrap: 'wrap', gap: '5px', flexShrink: 0 }}>
                                {QUICK_SUGGESTIONS.map((s, i) => (
                                    <button key={i} onClick={() => sendMessage(s.query)}
                                        style={{ background: 'rgba(0,168,232,0.08)', border: '1px solid rgba(0,168,232,0.2)', borderRadius: '20px', color: '#7dd3f9', fontSize: '10.5px', padding: '4px 10px', cursor: 'pointer', transition: 'all 0.2s', whiteSpace: 'nowrap' }}
                                        onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { const el = e.currentTarget; el.style.background = 'rgba(0,168,232,0.2)'; el.style.borderColor = 'rgba(0,168,232,0.5)'; el.style.color = '#00a8e8' }}
                                        onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { const el = e.currentTarget; el.style.background = 'rgba(0,168,232,0.08)'; el.style.borderColor = 'rgba(0,168,232,0.2)'; el.style.color = '#7dd3f9' }}>
                                        {s.label}
                                    </button>
                                ))}
                            </div>

                            {/* Input Area */}
                            <div style={{ padding: '10px 12px 14px', borderTop: '1px solid rgba(255,255,255,0.06)', display: 'flex', gap: '8px', alignItems: 'center', flexShrink: 0 }}>
                                <input
                                    ref={inputRef}
                                    type="text"
                                    value={inputValue}
                                    onChange={e => setInputValue(e.target.value)}
                                    onKeyDown={handleKeyDown}
                                    placeholder="Ornek: leasing kelimesiyle analiz yap..."
                                    style={{ flex: 1, background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '10px', padding: '9px 13px', color: '#e8eaed', fontSize: '12.5px', outline: 'none', transition: 'all 0.2s' }}
                                    onFocus={(e: React.FocusEvent<HTMLInputElement>) => { e.target.style.borderColor = 'rgba(0,168,232,0.5)'; e.target.style.boxShadow = '0 0 0 2px rgba(0,168,232,0.1)' }}
                                    onBlur={(e: React.FocusEvent<HTMLInputElement>) => { e.target.style.borderColor = 'rgba(255,255,255,0.1)'; e.target.style.boxShadow = 'none' }}
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
                                    <svg width="16" height="16" viewBox="0 0 24 24" fill={inputValue.trim() && !isTyping ? 'white' : '#4a5170'}>
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
                    width: '56px', height: '56px', borderRadius: '16px',
                    background: isOpen ? 'linear-gradient(135deg, #1a1f2e, #222839)' : 'linear-gradient(135deg, #00a8e8, #0066cc)',
                    border: isOpen ? '1px solid rgba(0,168,232,0.4)' : 'none', cursor: 'pointer',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    boxShadow: isOpen ? '0 8px 25px rgba(0,0,0,0.5), 0 0 0 1px rgba(0,168,232,0.3)' : '0 8px 25px rgba(0,168,232,0.5), 0 4px 10px rgba(0,0,0,0.3)',
                    zIndex: 9998, transition: 'all 0.3s cubic-bezier(0.4,0,0.2,1)', transform: 'scale(1)',
                }}
                onMouseEnter={e => { e.currentTarget.style.transform = 'scale(1.1)' }}
                onMouseLeave={e => { e.currentTarget.style.transform = 'scale(1)' }}
            >
                {isOpen
                    ? <svg width="22" height="22" viewBox="0 0 24 24" fill="#00a8e8"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" /></svg>
                    : <svg width="24" height="24" viewBox="0 0 24 24" fill="white"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z" /></svg>
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
        .chatbot-messages::-webkit-scrollbar-track { background: transparent; }
        .chatbot-messages::-webkit-scrollbar-thumb { background: rgba(0,168,232,0.3); border-radius: 2px; }
        .chatbot-messages::-webkit-scrollbar-thumb:hover { background: rgba(0,168,232,0.5); }
        .chat-heading { font-weight: 700; color: #00a8e8; font-size: 13px; margin-bottom: 4px; }
        .chat-bullet  { padding-left: 4px; margin: 1px 0; }
        .chat-numbered { padding-left: 4px; margin: 1px 0; }
        .chat-table-row { font-family: monospace; font-size: 11px; color: #b0b8c4; margin: 1px 0; }
        .chat-spacer  { height: 4px; }
      `}</style>
        </>
    )
}
