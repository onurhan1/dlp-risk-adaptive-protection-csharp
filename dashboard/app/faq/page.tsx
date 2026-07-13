'use client'

import { BookOpen, Lock, Check, X } from 'lucide-react'
import { useTranslation } from '@/components/LanguageProvider'

export default function FAQPage() {
    const { locale, t } = useTranslation()

    return (
        <div style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
            {/* Header with Language Toggle */}
            <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '32px',
                paddingBottom: '16px',
                borderBottom: '1px solid var(--border)'
            }}>
                <h1 style={{ fontSize: '28px', fontWeight: '700', color: 'var(--text-primary)', margin: 0 }}>
                    <BookOpen size={22} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '8px' }} />
                    {t('faq.title')}
                </h1>
            </div>

            {/* Content */}
            <div style={{
                background: 'var(--background-secondary)',
                borderRadius: '12px',
                padding: '32px',
                lineHeight: '1.7'
            }}>
                {locale === 'tr' ? <TurkishContent /> : <EnglishContent />}
            </div>
        </div>
    )
}

function SectionTitle({ children }: { children: React.ReactNode }) {
    return (
        <h2 style={{
            fontSize: '22px',
            fontWeight: '700',
            color: 'var(--text-primary)',
            marginTop: '32px',
            marginBottom: '16px',
            paddingBottom: '8px',
            borderBottom: '2px solid var(--primary)'
        }}>
            {children}
        </h2>
    )
}

function SubSection({ title, children }: { title: string; children: React.ReactNode }) {
    return (
        <div style={{ marginBottom: '24px' }}>
            <h3 style={{
                fontSize: '16px',
                fontWeight: '600',
                color: 'var(--primary)',
                marginBottom: '12px'
            }}>
                {title}
            </h3>
            {children}
        </div>
    )
}

function Table({ headers, rows }: { headers: string[]; rows: string[][] }) {
    return (
        <div style={{ overflowX: 'auto', marginBottom: '16px' }}>
            <table style={{
                width: '100%',
                borderCollapse: 'collapse',
                fontSize: '14px',
                background: 'var(--background)'
            }}>
                <thead>
                    <tr>
                        {headers.map((h, i) => (
                            <th key={i} style={{
                                padding: '12px 16px',
                                textAlign: 'left',
                                background: 'var(--surface-hover)',
                                fontWeight: '600',
                                color: 'var(--text-primary)',
                                borderBottom: '2px solid var(--border)'
                            }}>
                                {h}
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {rows.map((row, i) => (
                        <tr key={i}>
                            {row.map((cell, j) => (
                                <td key={j} style={{
                                    padding: '12px 16px',
                                    borderBottom: '1px solid var(--border)',
                                    color: 'var(--text-secondary)'
                                }}>
                                    {cell}
                                </td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}

function QA({ q, a }: { q: string; a: string }) {
    return (
        <div style={{
            marginBottom: '16px',
            padding: '16px',
            background: 'var(--background)',
            borderRadius: '8px',
            borderLeft: '4px solid var(--primary)'
        }}>
            <div style={{ fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>
                S: {q}
            </div>
            <div style={{ color: 'var(--text-secondary)' }}>
                {a}
            </div>
        </div>
    )
}

function QAen({ q, a }: { q: string; a: string }) {
    return (
        <div style={{
            marginBottom: '16px',
            padding: '16px',
            background: 'var(--background)',
            borderRadius: '8px',
            borderLeft: '4px solid var(--primary)'
        }}>
            <div style={{ fontWeight: '600', color: 'var(--text-primary)', marginBottom: '8px' }}>
                Q: {q}
            </div>
            <div style={{ color: 'var(--text-secondary)' }}>
                {a}
            </div>
        </div>
    )
}

function TurkishContent() {
    return (
        <>
            {/* Dashboard */}
            <SectionTitle>1. Dashboard (Ana Sayfa)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Bileşen', 'Açıklama']}
                    rows={[
                        ['Günlük Özet Kartları', 'Toplam olay sayısı, yüksek riskli olay sayısı, ortalama risk skoru, etkilenen departman sayısı'],
                        ['Aksiyon Özeti', 'AUTHORIZED, BLOCKED, QUARANTINED, RELEASED eylemlerinin günlük dağılımı'],
                        ['Günlük Trend Grafiği', 'Son 7-30 günlük olay eğilimi'],
                        ['Yüksek Etkili Uyarılar', 'En yüksek etkiye sahip kullanıcılar ve olayları'],
                        ['En Riskli Kullanıcılar', 'Günlük bazda en yüksek risk skoruna sahip kullanıcılar'],
                        ['En Çok Tetiklenen Kurallar', 'Hangi DLP kurallarının en sık tetiklendiği'],
                    ]}
                />
            </SubSection>
            <SubSection title="Sık Sorulan Sorular">
                <QA q="BLOCKED, QUARANTINED gibi kartlara tıkladığımda ne olur?" a="Tıkladığınızda o aksiyona ait tüm olayların detaylı listesini gösteren bir modal açılır." />
                <QA q="Rapor indirebilir miyim?" a="Evet, sidebar'daki Reports sayfasına giderek tarih aralığına göre PDF formatında detaylı rapor oluşturabilirsiniz." />
            </SubSection>

            {/* Investigation */}
            <SectionTitle>2. Investigation (Soruşturma)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Bileşen', 'Açıklama']}
                    rows={[
                        ['Kullanıcı Listesi', 'Tüm riskli kullanıcıların risk skoru ile birlikte listesi'],
                        ['Timeline', 'Seçili kullanıcının tüm olaylarının kronolojik görünümü'],
                        ['Alert Details', 'Seçili olayın tam detayları (Channel, Action, Matched Policy, Matched Rules)'],
                        ['AI Behavioral Analysis', 'Yapay zeka destekli davranış analizi özeti'],
                        ['User Insights Modal', 'Kullanıcı hakkında derinlemesine analiz'],
                    ]}
                />
            </SubSection>
            <SubSection title="Sık Sorulan Sorular">
                <QA q="Bir kullanıcının tüm olaylarını nasıl görebilirim?" a="Sol paneldeki kullanıcı listesinden ilgili kullanıcıya tıklayın. Timeline otomatik olarak o kullanıcının tüm olaylarını gösterecektir." />
                <QA q="Remediate butonu ne yapar?" a="Bu buton ile olayı 'çözüldü' olarak işaretleyebilir, alınan aksiyonu ve notları kaydedebilirsiniz." />
            </SubSection>

            {/* Reports */}
            <SectionTitle>3. Reports (Raporlar)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Bileşen', 'Açıklama']}
                    rows={[
                        ['Tarih Seçici', 'Rapor için tarih aralığı belirleme'],
                        ['Aksiyon Özet Kartları', 'AUTHORIZED, BLOCK, QUARANTINE, RELEASED sayıları'],
                        ['En Riskli Kullanıcılar', 'Risk skoruna göre sıralı kullanıcılar'],
                        ['Kanal Dağılımı', 'EMAIL, HTTPS, ENDPOINT gibi kanalların yüzdelik dağılımı'],
                        ['Geçmiş Raporlar', 'Daha önce oluşturulmuş raporların listesi'],
                    ]}
                />
            </SubSection>
            <SubSection title="Sık Sorulan Sorular">
                <QA q="PDF rapor nasıl oluşturulur?" a="'PDF Oluştur' butonuna tıklayın. Tarih aralığına göre detaylı bir rapor oluşturulur ve indirilir." />
            </SubSection>

            {/* Users */}
            <SectionTitle>4. Users (Kullanıcılar)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Bileşen', 'Açıklama']}
                    rows={[
                        ['Kullanıcı Listesi', 'Sistemdeki tüm kullanıcıların tablosu'],
                        ['Kullanıcı Bilgileri', 'Email, rol (Admin/User), durum (Aktif/Pasif)'],
                        ['Aksiyonlar', 'Düzenle, Sil butonları'],
                    ]}
                />
            </SubSection>
            <SubSection title="Sık Sorulan Sorular">
                <QA q="Kullanıcı rolü ne işe yarar?" a="Admin: Tüm sayfalara erişim. User: Sadece Dashboard sayfasına erişim." />
            </SubSection>

            {/* AI Behavioral */}
            <SectionTitle>5. AI Behavioral (Yapay Zeka Davranış Analizi)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Bileşen', 'Açıklama']}
                    rows={[
                        ['Günlük AI Risk Skoru', 'Her kullanıcı için son 7 günlük davranış skoru'],
                        ['Kişisel Baseline', '7 günlük pencerenin öncesindeki tüm kullanıcı geçmişi'],
                        ['Kullanıcı Riskleri', 'Skor, durum ve skoru etkileyen ilk üç davranış'],
                        ['Kural Tabanlı Analiz', 'Seçilebilir dönem için eşik ve Z-Score tabanlı ayrı değerlendirme'],
                    ]}
                />
            </SubSection>
            <SubSection title="Sık Sorulan Sorular">
                <QA q="AI risk skoru nasıl belirleniyor?" a="Isolation Forest modeli her gün son 7 günlük davranışı skorlar; kişinin tüm önceki davranışını kişisel norm, aynı dönemdeki ekip davranışını ise akran karşılaştırması olarak kullanır." />
            </SubSection>

            {/* Analytics */}
            <SectionTitle>6. Analytics (Analitik)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Bileşen', 'Açıklama']}
                    rows={[
                        ['Incident Heatmap', 'Gün ve saat bazlı olay yoğunluğu ısı haritası'],
                        ['Domain Features Manager', 'Domain bazlı özellik yönetimi tablosu'],
                        ['Filtreleme Seçenekleri', 'Policy, Channel, Severity, Department filtreleri'],
                    ]}
                />
            </SubSection>
            <SubSection title="Sık Sorulan Sorular">
                <QA q="Heatmap nasıl okunur?" a="X ekseni saatleri (0-23), Y ekseni günleri gösterir. Koyu renkler yüksek olay yoğunluğunu ifade eder." />
            </SubSection>

            {/* Settings */}
            <SectionTitle>7. Settings (Ayarlar)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Tab', 'İçerik']}
                    rows={[
                        ['Genel Ayarlar', 'Email bildirimi, günlük rapor saati, risk eşikleri'],
                        ['DLP API', 'Symantec DLP Manager bağlantı ayarları'],
                        ['Email', 'SMTP sunucu ayarları, test email gönderimi'],
                        ['Splunk SIEM', 'Splunk HEC entegrasyon ayarları'],
                    ]}
                />
            </SubSection>

            {/* AI Settings */}
            <SectionTitle>8. AI Settings (Yapay Zeka Ayarları)</SectionTitle>
            <SubSection title="Bu sayfada ne görebilirim?">
                <Table
                    headers={['Ayar', 'Açıklama']}
                    rows={[
                        ['Model Provider', 'OpenAI, Azure OpenAI veya Copilot seçimi'],
                        ['API Key', 'Seçilen provider için API anahtarı'],
                        ['Model Name', 'Kullanılacak model (gpt-4o, gpt-4-turbo vb.)'],
                        ['Temperature', 'Yanıt yaratıcılık seviyesi (0.0 - 1.0)'],
                    ]}
                />
            </SubSection>

            {/* Access Rights */}
            <SectionTitle><Lock size={18} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '6px' }} /> Erişim Yetkileri Özeti</SectionTitle>
            <Table
                headers={['Sayfa', 'Admin', 'User']}
                rows={[
                    ['Dashboard', '✓', '✓'],
                    ['Investigation', '✓', '✗'],
                    ['Reports', '✓', '✗'],
                    ['Users', '✓', '✗'],
                    ['AI Behavioral', '✓', '✗'],
                    ['Analytics', '✓', '✗'],
                    ['Settings', '✓', '✗'],
                    ['AI Settings', '✓', '✗'],
                    ['Logs', '✓', '✗'],
                ]}
            />
        </>
    )
}

function EnglishContent() {
    return (
        <>
            {/* Dashboard */}
            <SectionTitle>1. Dashboard</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Component', 'Description']}
                    rows={[
                        ['Daily Summary Cards', 'Total incidents, high-risk incidents, average risk score, affected departments'],
                        ['Action Summary', 'Daily distribution of AUTHORIZED, BLOCKED, QUARANTINED, RELEASED actions'],
                        ['Daily Trend Chart', 'Incident trends over the last 7-30 days'],
                        ['High Impact Alerts', 'Users and incidents with the highest impact'],
                        ['Top Risky Users', 'Users with the highest daily risk scores'],
                        ['Most Triggered Rules', 'Which DLP rules are triggered most frequently'],
                    ]}
                />
            </SubSection>
            <SubSection title="Frequently Asked Questions">
                <QAen q="What happens when I click on BLOCKED, QUARANTINED cards?" a="Clicking opens a modal showing a detailed list of all incidents for that action." />
                <QAen q="Can I download a report?" a="Yes, go to the Reports page in the sidebar to create and download a detailed PDF report based on your selected date range." />
            </SubSection>

            {/* Investigation */}
            <SectionTitle>2. Investigation</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Component', 'Description']}
                    rows={[
                        ['User List', 'List of all risky users with their risk scores'],
                        ['Timeline', 'Chronological view of all incidents for the selected user'],
                        ['Alert Details', 'Complete details of selected incident (Channel, Action, Matched Policy, Matched Rules)'],
                        ['AI Behavioral Analysis', 'AI-powered behavioral analysis summary'],
                        ['User Insights Modal', 'In-depth analysis about the user'],
                    ]}
                />
            </SubSection>
            <SubSection title="Frequently Asked Questions">
                <QAen q="How can I see all incidents for a user?" a="Click on the user in the left panel. The timeline will automatically show all incidents for that user." />
                <QAen q="What does the Remediate button do?" a="This button allows you to mark the incident as 'resolved' and save the action taken and notes." />
            </SubSection>

            {/* Reports */}
            <SectionTitle>3. Reports</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Component', 'Description']}
                    rows={[
                        ['Date Selector', 'Set date range for the report'],
                        ['Action Summary Cards', 'AUTHORIZED, BLOCK, QUARANTINE, RELEASED counts'],
                        ['Top Risky Users', 'Users sorted by risk score'],
                        ['Channel Distribution', 'Percentage distribution of channels like EMAIL, HTTPS, ENDPOINT'],
                        ['Past Reports', 'List of previously generated reports'],
                    ]}
                />
            </SubSection>
            <SubSection title="Frequently Asked Questions">
                <QAen q="How do I create a PDF report?" a="Click the 'Create PDF' button. A detailed report based on the date range will be generated and downloaded." />
            </SubSection>

            {/* Users */}
            <SectionTitle>4. Users</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Component', 'Description']}
                    rows={[
                        ['User List', 'Table of all users in the system'],
                        ['User Information', 'Email, role (Admin/User), status (Active/Inactive)'],
                        ['Actions', 'Edit, Delete buttons'],
                    ]}
                />
            </SubSection>
            <SubSection title="Frequently Asked Questions">
                <QAen q="What does the user role do?" a="Admin: Access to all pages. User: Access only to the Dashboard page." />
            </SubSection>

            {/* AI Behavioral */}
            <SectionTitle>5. AI Behavioral Analysis</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Component', 'Description']}
                    rows={[
                        ['Daily AI Risk Score', 'The latest seven-day behavior score for each user'],
                        ['Personal Baseline', 'All user history before the seven-day scoring window'],
                        ['User Risks', 'Score, status, and the top three contributing behaviors'],
                        ['Rule-Based Analysis', 'A separate threshold and Z-Score assessment for a selectable period'],
                    ]}
                />
            </SubSection>
            <SubSection title="Frequently Asked Questions">
                <QAen q="How is the AI risk score determined?" a="Each day, the Isolation Forest model scores the latest seven days, using all earlier user activity as the personal baseline and the same-period team activity as the peer comparison." />
            </SubSection>

            {/* Analytics */}
            <SectionTitle>6. Analytics</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Component', 'Description']}
                    rows={[
                        ['Incident Heatmap', 'Heatmap showing incident density by day and hour'],
                        ['Domain Features Manager', 'Table for managing domain-based features'],
                        ['Filtering Options', 'Filters for Policy, Channel, Severity, Department'],
                    ]}
                />
            </SubSection>
            <SubSection title="Frequently Asked Questions">
                <QAen q="How do I read the heatmap?" a="X-axis shows hours (0-23), Y-axis shows days. Darker colors indicate higher incident density." />
            </SubSection>

            {/* Settings */}
            <SectionTitle>7. Settings</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Tab', 'Content']}
                    rows={[
                        ['General Settings', 'Email notifications, daily report time, risk thresholds'],
                        ['DLP API', 'Symantec DLP Manager connection settings'],
                        ['Email', 'SMTP server settings, test email sending'],
                        ['Splunk SIEM', 'Splunk HEC integration settings'],
                    ]}
                />
            </SubSection>

            {/* AI Settings */}
            <SectionTitle>8. AI Settings</SectionTitle>
            <SubSection title="What can I see on this page?">
                <Table
                    headers={['Setting', 'Description']}
                    rows={[
                        ['Model Provider', 'Choose between OpenAI, Azure OpenAI, or Copilot'],
                        ['API Key', 'API key for the selected provider'],
                        ['Model Name', 'Model to use (gpt-4o, gpt-4-turbo, etc.)'],
                        ['Temperature', 'Response creativity level (0.0 - 1.0)'],
                    ]}
                />
            </SubSection>

            {/* Access Rights */}
            <SectionTitle><Lock size={18} style={{ display: 'inline', verticalAlign: 'middle', marginRight: '6px' }} /> Access Rights Summary</SectionTitle>
            <Table
                headers={['Page', 'Admin', 'User']}
                rows={[
                    ['Dashboard', '✓', '✓'],
                    ['Investigation', '✓', '✗'],
                    ['Reports', '✓', '✗'],
                    ['Users', '✓', '✗'],
                    ['AI Behavioral', '✓', '✗'],
                    ['Analytics', '✓', '✗'],
                    ['Settings', '✓', '✗'],
                    ['AI Settings', '✓', '✗'],
                    ['Logs', '✓', '✗'],
                ]}
            />
        </>
    )
}
