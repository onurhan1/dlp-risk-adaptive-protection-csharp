'use client'

import React, { useState, useRef, useEffect, useCallback } from 'react'

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
    { label: '🔴 Yüksek Riskli Kullanıcılar', query: 'Yüksek riskli kullanıcıları nasıl analiz edebilirim?' },
    { label: '📊 DLP Politikaları', query: 'DLP politika ihlalleri hakkında bilgi ver' },
    { label: '🔍 Olay Soruşturma', query: 'Bir güvenlik olayını nasıl soruşturuyorum?' },
    { label: '⚡ Otomatik Düzeltme', query: 'Otomatik düzeltme önerileri neler?' },
    { label: '📈 Risk Skoru', query: 'Risk skoru nasıl hesaplanır?' },
    { label: '🛡️ Veri Koruma', query: 'Veri sızıntısını nasıl önleyebilirim?' },
]

// DLP-specific knowledge base for dynamic responses
const DLP_KNOWLEDGE: Record<string, string[]> = {
    risk: [
        '📊 **Risk Skoru Analizi**\n\nRADAR sistemi risk skorlarını şu faktörlere göre hesaplar:\n\n• **Politika İhlal Sayısı** — Son 30 gündeki ihlal miktarı\n• **İhlal Şiddeti** — Kritik, Yüksek, Orta, Düşük kategorileri\n• **Hedef Hassasiyeti** — Dışarıya gönderilen verinin türü\n• **Kullanıcı Davranış Geçmişi** — Anomali tespiti\n\n💡 **İpucu:** Risk skoru >80 olan kullanıcılar için acil inceleme başlatın.',
        '🎯 **Risk Seviyesi Detayları**\n\n| Seviye | Puan | Aksiyon |\n|--------|------|---------|\n| Kritik | 90-100 | Anında Engelle |\n| Yüksek | 70-89 | Acil İncele |\n| Orta | 40-69 | Gözlemle |\n| Düşük | 1-39 | Raporla |\n\nKullanıcı risk puanları her 15 dakikada bir güncellenir.',
    ],
    dlp: [
        '🛡️ **DLP Politika İhlalleri**\n\nSık karşılaşılan ihlal türleri:\n\n• **Veri Sızdırma** — E-posta veya USB ile hassas veri transferi\n• **Yetkisiz Erişim** — İzinsiz dosya veya sistemlere erişim\n• **Politika Bypass** — Güvenlik kontrollerini atlatma girişimleri\n• **Şüpheli İndirme** — Toplu veri indirme aktivitesi\n\n⚠️ Tüm ihlaller otomatik olarak DLP motoru tarafından loglanır.',
        '📋 **Politika Yönetimi**\n\nRADAR üzerinden politikalar şu şekilde yönetilir:\n\n1. **Kural Oluşturma** — Settings > DLP Rules bölümünden\n2. **Eşik Ayarlama** — Her kural için tetikleme eşiği\n3. **Aksiyon Tanımlama** — Bildirim, engelleme veya kayıt\n4. **İnceleme Periyodu** — Otomatik gözden geçirme zamanları\n\n💡 Politikaları düzenli aralıklarla güncellemeniz önerilir.',
    ],
    investigation: [
        '🔍 **Olay Soruşturma Adımları**\n\n1. **İlk Değerlendirme** — Olayın şiddetini belirle\n2. **Log Analizi** — İlgili logları topla ve incele\n3. **Timeline Oluşturma** — Olay zaman çizelgesini hazırla\n4. **Etki Analizi** — Etkilenen sistemleri ve verileri tespit et\n5. **Düzeltici Aksiyon** — Gerekli önlemleri al\n6. **Raporlama** — Yönetim raporunu oluştur\n\n⏱️ Kritik olaylar için yanıt süresi <1 saat olmalıdır.',
        '📁 **Investigation Modülü**\n\nSolda "Investigation" menüsünden erişebileceğiniz modül şunları sunar:\n\n• **Alert Details** — Detaylı uyarı incelemesi\n• **Timeline View** — Kronolojik olay görünümü\n• **User Activity** — Kullanıcı aktivite geçmişi\n• **Network Map** — Ağ bağlantı haritası\n\n🎯 Her olayı kapatmadan önce tam belgeleme yapın.',
    ],
    remediation: [
        '⚡ **Otomatik Düzeltme Seçenekleri**\n\nRADAR\'ın otomatik düzeltme motoru şu aksiyonları alabilir:\n\n• **Hesap Kilitleme** — Şüpheli aktivitede otomatik kilit\n• **Oturum Sonlandırma** — Aktif oturumları kapatma\n• **Erişim İptali** — Belirli kaynaklara erişimi engelleme\n• **E-posta Karantina** — Şüpheli e-postaları tutma\n• **Dosya Geri Alma** — Yetkisiz transferleri geri alma\n\n⚠️ Otomatik düzeltmeyi etkinleştirmeden önce politikalarınızı test edin.',
    ],
    user: [
        '👤 **Kullanıcı Risk Analizi**\n\nYüksek riskli kullanıcıları belirlemek için:\n\n1. Ana dashboard\'daki **High Risk Users** panelini inceleyin\n2. Kullanıcıya tıklayarak **Entity Detail Modal**\'ı açın\n3. **Behavioral Analytics** sekmesinde anomalileri gözden geçirin\n4. **Risk Timeline** üzerinde trend analizi yapın\n\n🔴 Risk skoru >85 olan kullanıcılar için HR ile koordinasyon öneririz.',
        '📊 **Kullanıcı Davranış Analizi**\n\nDAVRANIŞ PUANLAMA SİSTEMİ:\n\n• Mesai dışı erişim: +15 puan\n• Büyük dosya transferi: +20 puan\n• Yeni cihazdan erişim: +10 puan\n• Çoklu başarısız giriş: +25 puan\n• Normal dışı saat aktivitesi: +15 puan\n\n📈 Bu puanlar gerçek zamanlı olarak hesaplanır ve dashboardda gösterilir.',
    ],
    data: [
        '🔒 **Veri Koruma Stratejileri**\n\nEtkili veri koruma için öneriler:\n\n1. **Veri Sınıflandırma** — Hassasiyet seviyelerine göre etiketleme\n2. **Erişim Kontrolü** — En az ayrıcalık ilkesi\n3. **Şifreleme** — Hem dinlenme hem de transfer sırasında\n4. **DLP Politikaları** — İçerik tabanlı filtreleme kuralları\n5. **Kullanıcı Eğitimi** — Güvenlik farkındalık programları\n\n💡 RADAR, veri sızıntısını gerçek zamanlı olarak tespit eder ve engeller.',
    ],
    report: [
        '📈 **Raporlama ve Analytics**\n\nRADAR raporlama özellikleri:\n\n• **Anlık Raporlar** — Gerçek zamanlı dashboard görünümü\n• **Scheduled Reports** — Otomatik periyodik raporlar\n• **Özel Raporlar** — İhtiyaca göre özelleştirilebilir\n• **Export Seçenekleri** — PDF, Excel, CSV formatları\n\nRapor almak için üst menüdeki **Reports** bölümüne gidin.\n\n📊 Aylık trend raporları için "Analytics" menüsünü kullanın.',
    ],
}

function generateResponse(userMessage: string): string {
    const msg = userMessage.toLowerCase()

    // Greeting detection
    if (/^(merhaba|selam|hi|hello|hey|günaydın|iyi günler)/.test(msg)) {
        return '👋 **Merhaba! RADAR Güvenlik Asistanı\'na hoş geldiniz.**\n\nSize şu konularda yardımcı olabilirim:\n\n• 🔴 Risk analizi ve yüksek riskli kullanıcılar\n• 🛡️ DLP politikaları ve ihlal yönetimi\n• 🔍 Güvenlik olayı soruşturma\n• ⚡ Otomatik düzeltme aksiyonları\n• 📊 Raporlama ve analytics\n\nNasıl yardımcı olabilirim?'
    }

    // Farewell detection
    if (/^(güle güle|hoşça kal|bye|tamam teşekkür|teşekkürler|sağol|görüşürüz)/.test(msg)) {
        return '👋 **Görüşmek üzere!**\n\nHerhangi bir güvenlik sorunuz olduğunda buradayım. RADAR sistemini güvende tutun! 🛡️'
    }

    // Help/what can you do
    if (/ne yapabilir|neler yapabilir|yardım|help|nasıl kullan/.test(msg)) {
        return '🤖 **RADAR Güvenlik Asistanı Yetenekleri**\n\nSize şu konularda rehberlik edebilirim:\n\n1. **Risk Yönetimi** — Kullanıcı ve sistem risk skorları\n2. **DLP Politikaları** — İhlal analizi ve politika yönetimi\n3. **Olay Soruşturma** — Adım adım soruşturma rehberi\n4. **Otomatik Düzeltme** — Güvenlik aksiyonları\n5. **Raporlama** — Dashboard ve raporlar\n6. **Veri Koruma** — En iyi uygulamalar\n\n💡 Aşağıdaki hızlı önerileri veya kendi sorunuzu kullanabilirsiniz.'
    }

    // Risk-related queries
    if (/risk|puan|skor|score/.test(msg)) {
        const responses = DLP_KNOWLEDGE.risk
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // DLP policy queries
    if (/dlp|politika|ihlal|kural|rule|policy/.test(msg)) {
        const responses = DLP_KNOWLEDGE.dlp
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // Investigation queries
    if (/soruştur|incele|analiz|investigate|olay|alert|uyarı/.test(msg)) {
        const responses = DLP_KNOWLEDGE.investigation
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // Remediation queries
    if (/düzeltme|remediat|engel|kapat|kilit|otomatik/.test(msg)) {
        const responses = DLP_KNOWLEDGE.remediation
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // User queries
    if (/kullanıcı|user|kişi|çalışan|employee|davranış|behavior/.test(msg)) {
        const responses = DLP_KNOWLEDGE.user
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // Data protection queries
    if (/veri|data|sızıntı|koruma|leak|protect|şifrele|encrypt/.test(msg)) {
        const responses = DLP_KNOWLEDGE.data
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // Report queries
    if (/rapor|report|analitik|analytic|istatistik|statistic/.test(msg)) {
        const responses = DLP_KNOWLEDGE.report
        return responses[Math.floor(Math.random() * responses.length)]
    }

    // Default response with suggestions
    return `🤔 Sorunuzu anlamaya çalışıyorum...\n\n"**${userMessage}**" hakkında doğrudan bilgim olmayabilir, ancak şu konularda yardımcı olabilirim:\n\n• Risk skoru analizi için **"risk"** yazın\n• DLP ihlalleri için **"dlp politika"** yazın\n• Olay soruşturması için **"soruşturma"** yazın\n• Veri koruma için **"veri koruma"** yazın\n\n💡 Veya aşağıdaki hızlı önerilerden birini seçebilirsiniz.`
}

export default function ChatBot() {
    const [isOpen, setIsOpen] = useState(false)
    const [isMinimized, setIsMinimized] = useState(false)
    const [messages, setMessages] = useState<Message[]>([
        {
            id: '1',
            role: 'assistant',
            content: '👋 **RADAR Güvenlik Asistanı**\n\nMerhaba! DLP güvenlik sisteminiz hakkında sorularınızı yanıtlamak için buradayım.\n\nRisk analizi, politika ihlalleri, olay soruşturma veya veri koruma konularında yardımcı olabilirim.',
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

        // Simulate thinking time (800ms - 2s)
        const thinkTime = 800 + Math.random() * 1200

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

            if (!isOpen || isMinimized) {
                setHasNewMessage(true)
            }
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
                content: '🔄 **Sohbet temizlendi.**\n\nYeni bir konuşma başlatabilirsiniz. Size nasıl yardımcı olabilirim?',
                timestamp: new Date(),
            },
        ])
    }

    const formatContent = (content: string) => {
        // Simple markdown-like rendering
        return content
            .split('\n')
            .map((line, i) => {
                // Bold text
                line = line.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
                // Headers with ##
                if (line.startsWith('## ')) {
                    return `<div class="chat-heading">${line.slice(3)}</div>`
                }
                // Bullet points
                if (line.startsWith('• ')) {
                    return `<div class="chat-bullet">${line}</div>`
                }
                // Numbered items
                if (/^\d+\. /.test(line)) {
                    return `<div class="chat-numbered">${line}</div>`
                }
                // Table rows
                if (line.startsWith('|')) {
                    return `<div class="chat-table-row">${line}</div>`
                }
                // Empty line
                if (line === '') {
                    return '<div class="chat-spacer"></div>'
                }
                return `<div>${line}</div>`
            })
            .join('')
    }

    const timeLabel = (date: Date) => {
        return date.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
    }

    return (
        <>
            {/* Chat Window */}
            {isOpen && (
                <div
                    style={{
                        position: 'fixed',
                        bottom: isMinimized ? '90px' : '90px',
                        right: '24px',
                        width: '380px',
                        height: isMinimized ? '60px' : '580px',
                        background: 'linear-gradient(135deg, #0d1117 0%, #161b27 100%)',
                        border: '1px solid rgba(0, 168, 232, 0.3)',
                        borderRadius: '16px',
                        boxShadow: '0 25px 60px rgba(0,0,0,0.6), 0 0 0 1px rgba(0,168,232,0.1), inset 0 1px 0 rgba(255,255,255,0.05)',
                        display: 'flex',
                        flexDirection: 'column',
                        zIndex: 9999,
                        overflow: 'hidden',
                        transition: 'height 0.3s cubic-bezier(0.4,0,0.2,1), box-shadow 0.3s ease',
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
                        {/* Avatar */}
                        <div
                            style={{
                                width: '36px',
                                height: '36px',
                                borderRadius: '50%',
                                background: 'linear-gradient(135deg, #00a8e8, #0066cc)',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                flexShrink: 0,
                                boxShadow: '0 0 12px rgba(0,168,232,0.5)',
                            }}
                        >
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="white">
                                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" />
                            </svg>
                        </div>

                        <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontSize: '13px', fontWeight: 700, color: '#e8eaed', letterSpacing: '0.3px' }}>
                                RADAR Güvenlik Asistanı
                            </div>
                            <div style={{ fontSize: '11px', color: '#00a8e8', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '1px' }}>
                                <span style={{
                                    width: '6px',
                                    height: '6px',
                                    borderRadius: '50%',
                                    background: '#00d4aa',
                                    display: 'inline-block',
                                    boxShadow: '0 0 6px #00d4aa',
                                    animation: 'pulse 2s infinite',
                                }} />
                                Çevrimiçi
                            </div>
                        </div>

                        {/* Action buttons */}
                        <div style={{ display: 'flex', gap: '4px' }} onClick={e => e.stopPropagation()}>
                            <button
                                onClick={clearChat}
                                title="Sohbeti temizle"
                                style={{
                                    background: 'rgba(255,255,255,0.05)',
                                    border: '1px solid rgba(255,255,255,0.1)',
                                    borderRadius: '6px',
                                    color: '#8a9199',
                                    cursor: 'pointer',
                                    padding: '4px 6px',
                                    fontSize: '11px',
                                    transition: 'all 0.2s',
                                    display: 'flex',
                                    alignItems: 'center',
                                }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.1)'; e.currentTarget.style.color = '#e8eaed' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; e.currentTarget.style.color = '#8a9199' }}
                            >
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                                    <path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z" />
                                </svg>
                            </button>
                            <button
                                onClick={() => setIsMinimized(!isMinimized)}
                                style={{
                                    background: 'rgba(255,255,255,0.05)',
                                    border: '1px solid rgba(255,255,255,0.1)',
                                    borderRadius: '6px',
                                    color: '#8a9199',
                                    cursor: 'pointer',
                                    padding: '4px 6px',
                                    fontSize: '11px',
                                    transition: 'all 0.2s',
                                    display: 'flex',
                                    alignItems: 'center',
                                }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.1)'; e.currentTarget.style.color = '#e8eaed' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; e.currentTarget.style.color = '#8a9199' }}
                            >
                                {isMinimized ? (
                                    <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14l5-5 5 5h-10z" /></svg>
                                ) : (
                                    <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M7 10l5 5 5-5h-10z" /></svg>
                                )}
                            </button>
                            <button
                                onClick={() => setIsOpen(false)}
                                style={{
                                    background: 'rgba(255,255,255,0.05)',
                                    border: '1px solid rgba(255,255,255,0.1)',
                                    borderRadius: '6px',
                                    color: '#8a9199',
                                    cursor: 'pointer',
                                    padding: '4px 6px',
                                    fontSize: '11px',
                                    transition: 'all 0.2s',
                                    display: 'flex',
                                    alignItems: 'center',
                                }}
                                onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(220,50,50,0.2)'; e.currentTarget.style.color = '#ff6b6b' }}
                                onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => { e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; e.currentTarget.style.color = '#8a9199' }}
                            >
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" /></svg>
                            </button>
                        </div>
                    </div>

                    {!isMinimized && (
                        <>
                            {/* Messages Area */}
                            <div
                                style={{
                                    flex: 1,
                                    overflowY: 'auto',
                                    padding: '16px 12px',
                                    display: 'flex',
                                    flexDirection: 'column',
                                    gap: '10px',
                                    scrollBehavior: 'smooth',
                                }}
                                className="chatbot-messages"
                            >
                                {messages.map((message) => (
                                    <div
                                        key={message.id}
                                        style={{
                                            display: 'flex',
                                            flexDirection: message.role === 'user' ? 'row-reverse' : 'row',
                                            gap: '8px',
                                            alignItems: 'flex-start',
                                            animation: 'msgFadeIn 0.25s ease',
                                        }}
                                    >
                                        {/* Avatar */}
                                        {message.role === 'assistant' && (
                                            <div style={{
                                                width: '28px',
                                                height: '28px',
                                                borderRadius: '50%',
                                                background: 'linear-gradient(135deg, #00a8e8, #0066cc)',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'center',
                                                flexShrink: 0,
                                                marginTop: '2px',
                                                boxShadow: '0 0 8px rgba(0,168,232,0.4)',
                                            }}>
                                                <svg width="14" height="14" viewBox="0 0 24 24" fill="white">
                                                    <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" />
                                                </svg>
                                            </div>
                                        )}

                                        <div style={{ maxWidth: '80%', display: 'flex', flexDirection: 'column', gap: '3px', alignItems: message.role === 'user' ? 'flex-end' : 'flex-start' }}>
                                            <div
                                                style={{
                                                    padding: '10px 13px',
                                                    borderRadius: message.role === 'user' ? '14px 14px 4px 14px' : '4px 14px 14px 14px',
                                                    background: message.role === 'user'
                                                        ? 'linear-gradient(135deg, #00a8e8, #0066cc)'
                                                        : 'rgba(255,255,255,0.06)',
                                                    border: message.role === 'user' ? 'none' : '1px solid rgba(255,255,255,0.08)',
                                                    color: '#e8eaed',
                                                    fontSize: '12.5px',
                                                    lineHeight: '1.5',
                                                    boxShadow: message.role === 'user' ? '0 4px 12px rgba(0,168,232,0.3)' : 'none',
                                                }}
                                                dangerouslySetInnerHTML={{ __html: formatContent(message.content) }}
                                            />
                                            <span style={{ fontSize: '10px', color: '#5a6170', paddingLeft: '2px', paddingRight: '2px' }}>
                                                {timeLabel(message.timestamp)}
                                            </span>
                                        </div>

                                        {/* User avatar */}
                                        {message.role === 'user' && (
                                            <div style={{
                                                width: '28px',
                                                height: '28px',
                                                borderRadius: '50%',
                                                background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'center',
                                                flexShrink: 0,
                                                marginTop: '2px',
                                            }}>
                                                <svg width="14" height="14" viewBox="0 0 24 24" fill="white">
                                                    <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" />
                                                </svg>
                                            </div>
                                        )}
                                    </div>
                                ))}

                                {/* Typing indicator */}
                                {isTyping && (
                                    <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-start', animation: 'msgFadeIn 0.25s ease' }}>
                                        <div style={{
                                            width: '28px', height: '28px', borderRadius: '50%',
                                            background: 'linear-gradient(135deg, #00a8e8, #0066cc)',
                                            display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
                                            boxShadow: '0 0 8px rgba(0,168,232,0.4)',
                                        }}>
                                            <svg width="14" height="14" viewBox="0 0 24 24" fill="white">
                                                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" />
                                            </svg>
                                        </div>
                                        <div style={{
                                            padding: '12px 16px',
                                            borderRadius: '4px 14px 14px 14px',
                                            background: 'rgba(255,255,255,0.06)',
                                            border: '1px solid rgba(255,255,255,0.08)',
                                            display: 'flex',
                                            gap: '4px',
                                            alignItems: 'center',
                                        }}>
                                            {[0, 1, 2].map(i => (
                                                <div key={i} style={{
                                                    width: '6px',
                                                    height: '6px',
                                                    borderRadius: '50%',
                                                    background: '#00a8e8',
                                                    animation: `typingDot 1.2s ${i * 0.2}s infinite`,
                                                }} />
                                            ))}
                                        </div>
                                    </div>
                                )}

                                <div ref={messagesEndRef} />
                            </div>

                            {/* Quick Suggestions */}
                            <div style={{
                                padding: '8px 12px 4px',
                                borderTop: '1px solid rgba(255,255,255,0.06)',
                                display: 'flex',
                                flexWrap: 'wrap',
                                gap: '5px',
                                flexShrink: 0,
                            }}>
                                {QUICK_SUGGESTIONS.map((s, i) => (
                                    <button
                                        key={i}
                                        onClick={() => sendMessage(s.query)}
                                        style={{
                                            background: 'rgba(0,168,232,0.08)',
                                            border: '1px solid rgba(0,168,232,0.2)',
                                            borderRadius: '20px',
                                            color: '#7dd3f9',
                                            fontSize: '10.5px',
                                            padding: '4px 10px',
                                            cursor: 'pointer',
                                            transition: 'all 0.2s',
                                            whiteSpace: 'nowrap',
                                        }}
                                        onMouseEnter={(e: React.MouseEvent<HTMLButtonElement>) => {
                                            const el = e.currentTarget
                                            el.style.background = 'rgba(0,168,232,0.2)'
                                            el.style.borderColor = 'rgba(0,168,232,0.5)'
                                            el.style.color = '#00a8e8'
                                        }}
                                        onMouseLeave={(e: React.MouseEvent<HTMLButtonElement>) => {
                                            const el = e.currentTarget
                                            el.style.background = 'rgba(0,168,232,0.08)'
                                            el.style.borderColor = 'rgba(0,168,232,0.2)'
                                            el.style.color = '#7dd3f9'
                                        }}
                                    >
                                        {s.label}
                                    </button>
                                ))}
                            </div>

                            {/* Input Area */}
                            <div style={{
                                padding: '10px 12px 14px',
                                borderTop: '1px solid rgba(255,255,255,0.06)',
                                display: 'flex',
                                gap: '8px',
                                alignItems: 'center',
                                flexShrink: 0,
                            }}>
                                <input
                                    ref={inputRef}
                                    type="text"
                                    value={inputValue}
                                    onChange={e => setInputValue(e.target.value)}
                                    onKeyDown={handleKeyDown}
                                    placeholder="Güvenlik sorunuzu yazın..."
                                    style={{
                                        flex: 1,
                                        background: 'rgba(255,255,255,0.05)',
                                        border: '1px solid rgba(255,255,255,0.1)',
                                        borderRadius: '10px',
                                        padding: '9px 13px',
                                        color: '#e8eaed',
                                        fontSize: '12.5px',
                                        outline: 'none',
                                        transition: 'all 0.2s',
                                    }}
                                    onFocus={(e: React.FocusEvent<HTMLInputElement>) => { e.target.style.borderColor = 'rgba(0,168,232,0.5)'; e.target.style.boxShadow = '0 0 0 2px rgba(0,168,232,0.1)' }}
                                    onBlur={(e: React.FocusEvent<HTMLInputElement>) => { e.target.style.borderColor = 'rgba(255,255,255,0.1)'; e.target.style.boxShadow = 'none' }}
                                />
                                <button
                                    onClick={() => sendMessage()}
                                    disabled={!inputValue.trim() || isTyping}
                                    style={{
                                        width: '36px',
                                        height: '36px',
                                        borderRadius: '10px',
                                        background: inputValue.trim() && !isTyping
                                            ? 'linear-gradient(135deg, #00a8e8, #0066cc)'
                                            : 'rgba(255,255,255,0.05)',
                                        border: 'none',
                                        cursor: inputValue.trim() && !isTyping ? 'pointer' : 'default',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        flexShrink: 0,
                                        transition: 'all 0.2s',
                                        boxShadow: inputValue.trim() && !isTyping ? '0 4px 12px rgba(0,168,232,0.4)' : 'none',
                                    }}
                                >
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
                onClick={() => {
                    setIsOpen(!isOpen)
                    setIsMinimized(false)
                    setHasNewMessage(false)
                }}
                title="RADAR Güvenlik Asistanı"
                style={{
                    position: 'fixed',
                    bottom: '24px',
                    right: '24px',
                    width: '56px',
                    height: '56px',
                    borderRadius: '16px',
                    background: isOpen
                        ? 'linear-gradient(135deg, #1a1f2e, #222839)'
                        : 'linear-gradient(135deg, #00a8e8, #0066cc)',
                    border: isOpen ? '1px solid rgba(0,168,232,0.4)' : 'none',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxShadow: isOpen
                        ? '0 8px 25px rgba(0,0,0,0.5), 0 0 0 1px rgba(0,168,232,0.3)'
                        : '0 8px 25px rgba(0,168,232,0.5), 0 4px 10px rgba(0,0,0,0.3)',
                    zIndex: 9998,
                    transition: 'all 0.3s cubic-bezier(0.4,0,0.2,1)',
                    transform: 'scale(1)',
                }}
                onMouseEnter={e => { e.currentTarget.style.transform = 'scale(1.1)' }}
                onMouseLeave={e => { e.currentTarget.style.transform = 'scale(1)' }}
            >
                {isOpen ? (
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="#00a8e8">
                        <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
                    </svg>
                ) : (
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="white">
                        <path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z" />
                    </svg>
                )}

                {/* Notification badge */}
                {hasNewMessage && !isOpen && (
                    <div style={{
                        position: 'absolute',
                        top: '-4px',
                        right: '-4px',
                        width: '16px',
                        height: '16px',
                        borderRadius: '50%',
                        background: '#ef4444',
                        border: '2px solid #0d1117',
                        boxShadow: '0 0 8px rgba(239,68,68,0.6)',
                        animation: 'pulse 1.5s infinite',
                    }} />
                )}

                {/* Ripple effect when not open */}
                {!isOpen && (
                    <div style={{
                        position: 'absolute',
                        inset: '-6px',
                        borderRadius: '22px',
                        border: '2px solid rgba(0,168,232,0.3)',
                        animation: 'ripple 2s infinite',
                        pointerEvents: 'none',
                    }} />
                )}
            </button>

            {/* CSS Animations */}
            <style>{`
        @keyframes chatSlideUp {
          from { opacity: 0; transform: translateY(20px) scale(0.95); }
          to { opacity: 1; transform: translateY(0) scale(1); }
        }

        @keyframes msgFadeIn {
          from { opacity: 0; transform: translateY(8px); }
          to { opacity: 1; transform: translateY(0); }
        }

        @keyframes typingDot {
          0%, 60%, 100% { transform: translateY(0); opacity: 0.4; }
          30% { transform: translateY(-6px); opacity: 1; }
        }

        @keyframes ripple {
          0% { transform: scale(1); opacity: 0.6; }
          100% { transform: scale(1.4); opacity: 0; }
        }

        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.5; }
        }

        .chatbot-messages::-webkit-scrollbar {
          width: 4px;
        }
        .chatbot-messages::-webkit-scrollbar-track {
          background: transparent;
        }
        .chatbot-messages::-webkit-scrollbar-thumb {
          background: rgba(0,168,232,0.3);
          border-radius: 2px;
        }
        .chatbot-messages::-webkit-scrollbar-thumb:hover {
          background: rgba(0,168,232,0.5);
        }

        .chat-heading {
          font-weight: 700;
          color: #00a8e8;
          font-size: 13px;
          margin-bottom: 4px;
        }

        .chat-bullet {
          padding-left: 4px;
          margin: 1px 0;
        }

        .chat-numbered {
          padding-left: 4px;
          margin: 1px 0;
        }

        .chat-table-row {
          font-family: monospace;
          font-size: 11px;
          color: #b0b8c4;
          margin: 1px 0;
        }

        .chat-spacer {
          height: 4px;
        }
      `}</style>
        </>
    )
}
