/**
 * MOCK API SERVER — Dummy data for all dashboard pages
 * Run: node mock-server.js
 * Provides data on port 5001 (matches next.config.js proxy target)
 * DELETE THIS FILE after testing is complete
 */

const http = require('http')
const url = require('url')

const PORT = 5001

// ─── Dummy Data ────────────────────────────────────────────────────────────────

const dummyUsers = [
    { user_email: 'ahmet.yilmaz@company.com', login_name: 'Ahmet Yılmaz', department: 'IT', risk_score: 87, risk_level: 'Critical', total_alerts: 45, total_incidents: 45, days_with_activity: 18, total_blocks: 12 },
    { user_email: 'ayse.kaya@company.com', login_name: 'Ayşe Kaya', department: 'Finance', risk_score: 72, risk_level: 'High', total_alerts: 32, total_incidents: 32, days_with_activity: 14, total_blocks: 8 },
    { user_email: 'mehmet.demir@company.com', login_name: 'Mehmet Demir', department: 'HR', risk_score: 65, risk_level: 'High', total_alerts: 28, total_incidents: 28, days_with_activity: 12, total_blocks: 5 },
    { user_email: 'fatma.celik@company.com', login_name: 'Fatma Çelik', department: 'Engineering', risk_score: 55, risk_level: 'Medium', total_alerts: 19, total_incidents: 19, days_with_activity: 10, total_blocks: 3 },
    { user_email: 'ali.ozturk@company.com', login_name: 'Ali Öztürk', department: 'Sales', risk_score: 48, risk_level: 'Medium', total_alerts: 15, total_incidents: 15, days_with_activity: 8, total_blocks: 2 },
    { user_email: 'zeynep.arslan@company.com', login_name: 'Zeynep Arslan', department: 'Marketing', risk_score: 42, risk_level: 'Medium', total_alerts: 12, total_incidents: 12, days_with_activity: 6, total_blocks: 1 },
    { user_email: 'mustafa.sahin@company.com', login_name: 'Mustafa Şahin', department: 'Legal', risk_score: 35, risk_level: 'Low', total_alerts: 8, total_incidents: 8, days_with_activity: 5, total_blocks: 0 },
    { user_email: 'elif.yildiz@company.com', login_name: 'Elif Yıldız', department: 'Operations', risk_score: 28, risk_level: 'Low', total_alerts: 5, total_incidents: 5, days_with_activity: 3, total_blocks: 0 },
    { user_email: 'emre.acar@company.com', login_name: 'Emre Acar', department: 'Engineering', risk_score: 22, risk_level: 'Low', total_alerts: 3, total_incidents: 3, days_with_activity: 2, total_blocks: 0 },
    { user_email: 'selin.bas@company.com', login_name: 'Selin Baş', department: 'IT', risk_score: 15, risk_level: 'Low', total_alerts: 2, total_incidents: 2, days_with_activity: 1, total_blocks: 0 },
]

const channels = ['Email', 'Removable Storage', 'Cloud Upload', 'Web Browser', 'Printer', 'Network Share']
const actions = ['Authorized', 'Block', 'Quarantine', 'Released']
const severities = ['Low', 'Medium', 'High', 'Critical']
const dataTypes = ['PII', 'PCI', 'CCN', 'PHI', 'Confidential']
const policies = ['NEO IoB-1 Data Exfiltration Prevention', 'NEO IoB-2 Insider Threat Detection', 'NEO IoB-3 Sensitive Data Policy', 'PCI Compliance Rule', 'KVKK Personal Data Protection']
const teams = ['Bilgi Teknolojileri', 'Muhasebe', 'Saha Operasyonlari', 'Insan Kaynaklari', 'Hukuk', 'Satis', 'Pazarlama', 'Uretim', 'Lojistik', 'Kalite Kontrol']
const domains = ['gmail.com', 'outlook.com', 'wetransfer.com', 'dropbox.com', 'drive.google.com', 'onedrive.com', 'company.com', 'yandex.com', 'icloud.com', 'protonmail.com', 'hotmail.com', 'yahoo.com']

function randomFrom(arr) { return arr[Math.floor(Math.random() * arr.length)] }
function randomInt(min, max) { return Math.floor(Math.random() * (max - min + 1)) + min }

function generateDate(daysAgo) {
    const d = new Date()
    d.setDate(d.getDate() - daysAgo)
    return d.toISOString()
}

function formatDate(d) {
    return d.toISOString().split('T')[0]
}

// Generate daily summary data (30 days)
function generateDailySummary(days = 30) {
    const data = []
    for (let i = days; i >= 0; i--) {
        const d = new Date()
        d.setDate(d.getDate() - i)
        data.push({
            date: formatDate(d),
            total_incidents: randomInt(5, 50),
            critical: randomInt(0, 5),
            high: randomInt(2, 15),
            medium: randomInt(3, 20),
            low: randomInt(1, 15),
            authorized: randomInt(2, 20),
            blocked: randomInt(1, 10),
            quarantined: randomInt(0, 5),
            avg_risk_score: randomInt(15, 85),
        })
    }
    return data
}

// Generate incidents
function generateIncidents(count = 200) {
    const incidents = []
    for (let i = 1; i <= count; i++) {
        const user = dummyUsers[randomInt(0, dummyUsers.length - 1)]
        const severity = randomInt(1, 5)
        const destDomain = randomFrom(domains)
        const recipient = `${randomFrom(['john.doe', 'jane.smith', 'bob.jones', 'alice.wonder', 'mark.twain'])}@${destDomain}`
        incidents.push({
            id: i,
            timestamp: generateDate(randomInt(0, 30)),
            userEmail: user.user_email,
            loginName: user.login_name,
            fullName: user.login_name,
            department: user.department,
            team: randomFrom(teams),
            channel: randomFrom(channels),
            action: randomFrom(actions),
            severity: severity,
            riskLevel: severity >= 4 ? 'Critical' : severity >= 3 ? 'High' : severity >= 2 ? 'Medium' : 'Low',
            riskScore: randomInt(10, 95),
            dataType: randomFrom(dataTypes),
            policy: randomFrom(policies),
            destination: recipient,
            domain: destDomain,
            maxMatches: randomInt(0, 25),
            violationTriggers: JSON.stringify([{
                PolicyName: randomFrom(policies),
                RuleName: `Rule-${randomInt(1, 20)}`,
                Classifiers: [`Classifier-${randomInt(1, 10)}`],
                MatchCount: randomInt(1, 15)
            }]),
            ioBs: [`IoB-${randomInt(1, 10)}`],
            data_type: randomFrom(dataTypes),
            source_application: randomFrom(['Outlook', 'Chrome', 'Explorer', 'Teams', 'OneDrive']),
            email_subject: `Report Q${randomInt(1, 4)} - ${randomFrom(['Confidential', 'Internal', 'Draft'])}`,
            recipients: recipient,
            files: [
                { name: `report_${randomInt(1, 100)}.xlsx`, size: `${randomInt(100, 5000)} KB`, protected: Math.random() > 0.5, classification: [randomFrom(dataTypes)] },
                { name: `data_${randomInt(1, 100)}.pdf`, size: `${randomInt(50, 2000)} KB`, protected: Math.random() > 0.7, classification: [] }
            ]
        })
    }
    return incidents.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp))
}

const allIncidents = generateIncidents(100)
const dailySummary = generateDailySummary(30)

// ─── AI Behavioral Data ────────────────────────────────────────────────────────

const aiOverview = {
    totalAnalyzed: 156,
    highAnomalyCount: 12,
    mediumAnomalyCount: 34,
    lowAnomalyCount: 110,
    userAnomalies: dummyUsers.slice(0, 5).map(u => ({
        entityType: 'user',
        entityId: u.user_email,
        riskScore: u.risk_score,
        anomalyLevel: u.risk_score >= 70 ? 'high' : u.risk_score >= 40 ? 'medium' : 'low',
        aiExplanation: `User ${u.login_name} showed unusual data transfer patterns in ${u.department} department. Elevated file copy activity detected outside normal working hours.`,
        aiRecommendation: u.risk_score >= 70 ? 'Immediate review recommended. Consider restricting removable storage access.' : 'Monitor and review during next assessment cycle.',
        referenceIncidentIds: [randomInt(1, 100), randomInt(1, 100), randomInt(1, 100)],
        analysisMetadata: { model: 'anomaly-detect-v2', confidence: 0.87 },
        analysisDate: generateDate(randomInt(0, 7)),
    })),
    channelAnomalies: channels.slice(0, 4).map(ch => ({
        entityType: 'channel',
        entityId: ch,
        riskScore: randomInt(30, 85),
        anomalyLevel: randomFrom(['low', 'medium', 'high']),
        aiExplanation: `Channel ${ch} showed ${randomInt(20, 200)}% increase in data volume compared to baseline.`,
        aiRecommendation: 'Review channel policies and audit recent transfers.',
        referenceIncidentIds: [randomInt(1, 100)],
        analysisMetadata: {},
        analysisDate: generateDate(1),
    })),
    uniqueChannels: channels,
    uniqueDepartments: ['IT', 'Finance', 'HR', 'Engineering', 'Sales', 'Marketing', 'Legal', 'Operations'],
    uniqueDestinations: ['external-1.domain.com', 'cloud-5.domain.com', 'internal-3.domain.com'],
    uniqueRules: policies,
    topAnomalies: dummyUsers.slice(0, 3).map(u => ({
        entityType: 'user',
        entityId: u.user_email,
        riskScore: u.risk_score,
        anomalyLevel: 'high',
        aiExplanation: `Critical anomaly: ${u.login_name} transferred ${randomInt(50, 500)} files in a single session.`,
        aiRecommendation: 'Escalate to security team immediately.',
        referenceIncidentIds: [randomInt(1, 50)],
        analysisMetadata: {},
        analysisDate: generateDate(0),
    })),
    anomalyByChannel: { Email: 45, 'Removable Storage': 32, 'Cloud Upload': 28, 'Web Browser': 15 },
    anomalyByDepartment: { IT: 35, Finance: 28, HR: 18, Engineering: 22, Sales: 12 },
}

// ─── Settings ──────────────────────────────────────────────────────────────────

const settingsData = {
    splunk_url: 'https://splunk.company.com:8089',
    splunk_token: 'mock-token-xxxx',
    dlp_api_url: 'https://compliance.company.com/api',
    dlp_api_key: 'mock-dlp-key-xxxx',
    email_host: 'smtp.company.com',
    email_port: 587,
    email_username: 'alerts@company.com',
    email_use_ssl: true,
    auto_remediation: false,
    auto_remediation_threshold: 80,
    notification_email: 'soc@company.com',
    ai_provider: 'azure',
    ai_model: 'gpt-4o',
    ai_endpoint: 'https://ai.company.com/openai',
    ai_api_key: 'mock-ai-key-xxxx',
}

const mockImapSettings = {
    enabled: true,
    host: 'imap.company.com',
    port: 993,
    enable_ssl: true,
    username: 'dlp-workflow@company.com',
    password_set: true,
    folder: 'INBOX',
    unread_only: false,
    lookback_days: 7,
    max_messages: 500,
    is_configured: true,
    updated_at: new Date().toISOString(),
}

const mockImapMessages = [
    {
        id: '101',
        from: 'analyst@company.com',
        subject: 'RE: DLP sorgu cevabi - yuksek match',
        date: generateDate(0),
        unread: true,
        size: 18432,
    },
    {
        id: '100',
        from: 'manager@company.com',
        subject: 'Permit incident kullanici aciklamasi',
        date: generateDate(1),
        unread: false,
        size: 9216,
    },
    {
        id: '99',
        from: 'security@company.com',
        subject: 'Haftalik inceleme listesi',
        date: generateDate(2),
        unread: false,
        size: 12780,
    },
]

// ─── Domain Features ───────────────────────────────────────────────────────────

const domainColumns = [
    { id: 1, displayName: 'Domain', fieldName: 'domain' },
    { id: 2, displayName: 'Category', fieldName: 'category' },
    { id: 3, displayName: 'Risk Level', fieldName: 'risk_level' },
    { id: 4, displayName: 'Whitelisted', fieldName: 'whitelisted' },
]

const domainFeatures = [
    { id: 1, domain: 'gmail.com', category: 'Email', risk_level: 'Medium', whitelisted: false },
    { id: 2, domain: 'company.com', category: 'Internal', risk_level: 'Low', whitelisted: true },
    { id: 3, domain: 'dropbox.com', category: 'Cloud Storage', risk_level: 'High', whitelisted: false },
    { id: 4, domain: 'drive.google.com', category: 'Cloud Storage', risk_level: 'Medium', whitelisted: false },
    { id: 5, domain: 'wetransfer.com', category: 'File Transfer', risk_level: 'Critical', whitelisted: false },
    { id: 6, domain: 'onedrive.com', category: 'Cloud Storage', risk_level: 'Low', whitelisted: true },
    { id: 7, domain: 'slack.com', category: 'Messaging', risk_level: 'Low', whitelisted: true },
    { id: 8, domain: 'pastebin.com', category: 'Web', risk_level: 'Critical', whitelisted: false },
]

// ─── Mercek Data ───────────────────────────────────────────────────────────────

const mercekData = allIncidents.slice(0, 20).map((inc, i) => ({
    ...inc,
    id: i + 1,
    status: randomFrom(['pending', 'reviewed', 'resolved', 'escalated']),
    reviewedBy: randomFrom([null, 'admin', 'analyst1']),
    notes: Math.random() > 0.5 ? 'Reviewed - looks like false positive' : null,
}))

// ─── Users Management ──────────────────────────────────────────────────────────

const managementUsers = [
    { id: 1, username: 'admin', email: 'admin@company.com', role: 'admin', createdAt: '2024-01-01', lastLogin: generateDate(0) },
    { id: 2, username: 'analyst1', email: 'analyst1@company.com', role: 'analyst', createdAt: '2024-03-15', lastLogin: generateDate(1) },
    { id: 3, username: 'viewer1', email: 'viewer1@company.com', role: 'viewer', createdAt: '2024-06-01', lastLogin: generateDate(5) },
]

// ─── Logs Data ─────────────────────────────────────────────────────────────────

function generateLogs(count = 30) {
    const eventTypes = ['LOGIN', 'LOGOUT', 'SETTINGS_CHANGE', 'USER_CREATE', 'INCIDENT_REVIEW', 'REPORT_GENERATE', 'POLICY_UPDATE']
    return Array.from({ length: count }, (_, i) => ({
        id: i + 1,
        timestamp: generateDate(randomInt(0, 30)),
        eventType: randomFrom(eventTypes),
        user: randomFrom(['admin', 'analyst1', 'viewer1']),
        details: `${randomFrom(eventTypes)} action performed`,
        ipAddress: `192.168.1.${randomInt(1, 254)}`,
        success: Math.random() > 0.1,
    }))
}

// ─── Azure AI Users ────────────────────────────────────────────────────────────

const azureAIUsers = dummyUsers.slice(0, 5).map(u => ({
    userId: u.user_email,
    userName: u.login_name,
    department: u.department,
    riskScore: u.risk_score,
    lastAnalysis: generateDate(randomInt(0, 3)),
    analysisCount: randomInt(1, 10),
    anomalyLevel: u.risk_score >= 70 ? 'high' : u.risk_score >= 40 ? 'medium' : 'low',
}))

// ─── Route Handler ─────────────────────────────────────────────────────────────

function handleRequest(pathname, query, method, body) {
    // Auth
    if (pathname === '/api/auth/login' && method === 'POST') {
        return { token: 'mock-jwt-token-xxx', username: body?.username || 'admin', role: 'admin' }
    }

    // Dashboard - Daily Summary
    if (pathname === '/api/risk-trends/daily-summary') {
        return dailySummary
    }

    // Dashboard - Top Users
    if (pathname === '/api/risk-trends/top-users') {
        const limit = parseInt(query.limit) || 10
        return dummyUsers.slice(0, limit)
    }

    // Dashboard - Top Rules
    if (pathname === '/api/risk-trends/top-rules') {
        return policies.map((p, i) => ({
            rule_name: p,
            total_alerts: randomInt(10, 80),
            avg_risk_score: randomInt(30, 85),
            unique_users: randomInt(3, 15),
        }))
    }

    // Dashboard - High Impact Alerts (Data Exfiltration)
    if (pathname === '/api/risk-trends/high-impact-alerts') {
        const page = parseInt(query.page) || 1
        const pageSize = parseInt(query.pageSize) || 20
        const allAlerts = dummyUsers.slice(0, 6).map((u, idx) => ({
            user_email: u.user_email,
            full_name: u.login_name,
            team: randomFrom(teams),
            impact_score: randomInt(60, 100),
            max_max_matches: randomInt(100, 500),
            highest_risk_date: formatDate(new Date(Date.now() - idx * 86400000)),
            daily_risk_score: randomInt(80, 100),
            incident_count: randomInt(5, 30),
            block_count: randomInt(1, 10),
            quarantine_count: randomInt(0, 5),
            days_with_activity: randomInt(1, 10),
            total_incidents_in_period: randomInt(10, 50),
            is_single_day_event: Math.random() > 0.5,
            severity_level: idx < 2 ? 'Critical' : idx < 4 ? 'High' : 'Medium',
            incident_details: allIncidents.filter(i => i.userEmail === u.user_email).slice(0, 5).map(i => ({
                file_name: i.files?.[0]?.name || 'report.xlsx',
                destination: i.destination,
                channel: i.channel,
                action: i.action,
                policy: i.policy,
                max_matches: randomInt(10, 200),
                timestamp: i.timestamp
            })),
        }))
        const totalCount = allAlerts.length
        const totalPages = Math.ceil(totalCount / pageSize)
        const start = (page - 1) * pageSize
        const paginatedData = allAlerts.slice(start, start + pageSize)
        return {
            data: paginatedData,
            pagination: {
                page,
                pageSize,
                totalCount,
                totalPages
            }
        }
    }

    // Dashboard - User Comprehensive Report
    if (pathname.match(/\/api\/risk-trends\/user\/.*\/comprehensive/)) {
        const userEmail = decodeURIComponent(pathname.split('/')[4])
        const user = dummyUsers.find(u => u.user_email === userEmail) || dummyUsers[0]
        return {
            user: user,
            totalIncidents: user.total_alerts,
            channelBreakdown: { Email: randomInt(5, 20), 'Removable Storage': randomInt(2, 10), 'Cloud Upload': randomInt(1, 8) },
            timelineData: dailySummary.slice(0, 14),
            topPolicies: policies.slice(0, 3).map(p => ({ policy: p, count: randomInt(1, 10) })),
            riskTrend: dailySummary.slice(0, 7).map(d => ({ date: d.date, score: randomInt(20, 90) })),
        }
    }

    // Dashboard - Action Summary
    if (pathname === '/api/risk/action-summary') {
        const a = randomInt(50, 200), b = randomInt(10, 60), q = randomInt(5, 30), r = randomInt(3, 20)
        return { authorized: a, block: b, quarantine: q, released: r, total: a + b + q + r }
    }

    // Dashboard - Department Summary
    if (pathname === '/api/risk/department-summary') {
        return [
            { department: 'IT', total_incidents: 45, risk_score: 78 },
            { department: 'Finance', total_incidents: 32, risk_score: 65 },
            { department: 'HR', total_incidents: 18, risk_score: 52 },
            { department: 'Engineering', total_incidents: 28, risk_score: 60 },
            { department: 'Sales', total_incidents: 15, risk_score: 40 },
        ]
    }

    // Reports
    if (pathname === '/api/reports/summary' || pathname === '/api/reports/daily-summary') {
        return {
            totalIncidents: randomInt(100, 500),
            criticalAlerts: randomInt(5, 25),
            usersAffected: randomInt(20, 80),
            topChannel: 'Email',
            summary: dailySummary,
        }
    }
    if (pathname === '/api/risk-trends/users/report') {
        return dummyUsers
    }
    if (pathname === '/api/reports/daily-summary/pdf') {
        return { message: 'PDF generation not available in mock mode' }
    }

    // Investigation - User List
    if (pathname === '/api/risk/user-list') {
        return dummyUsers
    }

    // Investigation - Incidents
    if (pathname === '/api/incidents') {
        const limit = parseInt(query.limit) || 50
        const user = query.user
        let filtered = allIncidents
        if (user) {
            filtered = filtered.filter(i => i.userEmail.includes(user))
        }
        return filtered.slice(0, limit)
    }

    // Incidents by action
    if (pathname === '/api/risk/incidents/by-action') {
        const action = query.action || 'Block'
        return {
            items: allIncidents.filter(i => i.action.toLowerCase() === action.toLowerCase()).slice(0, 20),
            total: randomInt(10, 50),
            page: 1,
            pageSize: 20,
        }
    }

    // Filter options
    if (pathname === '/api/risk/incidents/filter-options') {
        const now = new Date()
        const thirtyDaysAgo = new Date(now)
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30)
        return {
            channels: channels,
            actions: actions,
            severities: severities,
            dataTypes: dataTypes,
            departments: ['IT', 'Finance', 'HR', 'Engineering', 'Sales', 'Marketing', 'Legal', 'Operations'],
            dateRange: { minDate: formatDate(thirtyDaysAgo), maxDate: formatDate(now) },
        }
    }

    // Risk Daily Summary (for RiskTimelineChart)
    if (pathname === '/api/risk/daily-summary') {
        return dailySummary
    }

    // Top users daily
    if (pathname === '/api/risk/top-users-daily') {
        return dummyUsers.slice(0, parseInt(query.limit) || 10)
    }

    // Top rules daily
    if (pathname === '/api/risk/top-rules-daily') {
        return policies.map(p => ({
            rule_name: p,
            total_alerts: randomInt(5, 40),
            avg_risk_score: randomInt(30, 75),
            unique_users: randomInt(2, 12),
        }))
    }

    // High risk users
    if (pathname === '/api/risk/high-risk-users') {
        return dummyUsers.filter(u => u.risk_score >= 50)
    }

    // Channel Activity
    if (pathname === '/api/risk/channel-activity') {
        return channels.map(ch => ({
            channel: ch,
            total: randomInt(10, 100),
            trend: randomFrom(['up', 'down', 'stable']),
            percentage: randomInt(5, 35),
        }))
    }

    // Anomaly Detections
    if (pathname === '/api/risk/anomaly/detections') {
        return {
            total: randomInt(5, 20),
            detections: dummyUsers.slice(0, 5).map(u => ({
                userId: u.user_email,
                userName: u.login_name,
                anomalyScore: randomInt(60, 95),
                detectedAt: generateDate(randomInt(0, 3)),
                type: randomFrom(['volume_spike', 'off_hours', 'new_destination', 'bulk_transfer']),
            }))
        }
    }

    // Remediate
    if (pathname.match(/\/api\/incidents\/\d+\/remediate/)) {
        return { success: true, message: 'Incident remediated successfully' }
    }

    // Policy Recommendations
    if (pathname === '/api/policies/recommendations') {
        return {
            recommendations: [
                { policy: 'Block USB transfers for high-risk users', confidence: 0.92, impact: 'High' },
                { policy: 'Enable MFA for external file sharing', confidence: 0.88, impact: 'Medium' },
                { policy: 'Restrict cloud uploads from Finance dept', confidence: 0.85, impact: 'High' },
            ]
        }
    }

    // AI Behavioral
    if (pathname === '/api/ai-behavioral/overview') {
        return aiOverview
    }
    if (pathname.match(/\/api\/ai-behavioral\/entity\/.+\/detail/)) {
        const parts = pathname.split('/')
        const entityType = parts[4]
        const entityId = decodeURIComponent(parts[5])
        const user = dummyUsers.find(u => u.user_email === entityId) || dummyUsers[0]
        return {
            entityType,
            entityId,
            fullName: user.login_name,
            riskScore: user.risk_score,
            anomalyLevel: user.risk_score >= 70 ? 'high' : user.risk_score >= 40 ? 'medium' : 'low',
            aiExplanation: `Comprehensive analysis of ${user.login_name}: Detected ${randomInt(3, 15)} anomalous activities over the past 30 days.`,
            aiRecommendation: 'Continue monitoring. Consider scheduling a review meeting with the user\'s manager.',
            referenceIncidentIds: [randomInt(1, 100), randomInt(1, 100)],
            analysisMetadata: { model: 'v2', lastUpdated: generateDate(0) },
            analysisDate: generateDate(0),
            timeline: dailySummary.slice(0, 14).map(d => ({ date: d.date, anomalyScore: randomInt(10, 90) })),
            incidents: allIncidents.filter(i => i.userEmail === user.user_email).slice(0, 10),
        }
    }
    if (pathname.match(/\/api\/ai-behavioral\/entity\/.+/)) {
        const parts = pathname.split('/')
        const entityId = decodeURIComponent(parts[5])
        const user = dummyUsers.find(u => u.user_email === entityId) || dummyUsers[0]
        return {
            entityType: parts[4],
            entityId,
            riskScore: user.risk_score,
            anomalyLevel: user.risk_score >= 70 ? 'high' : 'medium',
            aiExplanation: `Analysis completed for ${user.login_name}.`,
            aiRecommendation: 'Review recommended.',
            referenceIncidentIds: [randomInt(1, 50)],
            analysisMetadata: {},
            analysisDate: generateDate(0),
        }
    }
    if (pathname === '/api/ai-behavioral/analyze') {
        return { success: true, message: 'Analysis started', taskId: 'mock-task-123' }
    }
    if (pathname === '/api/azure-ai/users-with-analysis') {
        return azureAIUsers
    }

    // Settings
    if (pathname === '/api/settings') {
        return settingsData
    }
    if (pathname === '/api/settings/splunk') {
        if (method === 'POST') return { success: true, message: 'Splunk settings saved' }
        return { url: settingsData.splunk_url, token: settingsData.splunk_token }
    }
    if (pathname === '/api/settings/splunk/test') {
        return { success: true, message: 'Splunk connection successful' }
    }
    if (pathname === '/api/settings/dlp') {
        if (method === 'POST') return { success: true, message: 'DLP settings saved' }
        return { apiUrl: settingsData.dlp_api_url, apiKey: settingsData.dlp_api_key }
    }
    if (pathname === '/api/settings/dlp/test') {
        return { success: true, message: 'DLP connection successful' }
    }
    if (pathname === '/api/settings/email') {
        if (method === 'POST') return { success: true, message: 'Email settings saved' }
        return { host: settingsData.email_host, port: settingsData.email_port, username: settingsData.email_username, useSsl: settingsData.email_use_ssl }
    }
    if (pathname === '/api/settings/email/test') {
        return { success: true, message: 'Email connection successful' }
    }
    if (pathname === '/api/settings/send-test-email') {
        return { success: true, message: 'Test email sent' }
    }
    if (pathname === '/api/settings/imap') {
        if (method === 'POST') {
            Object.assign(mockImapSettings, {
                ...body,
                password: undefined,
                password_set: true,
                is_configured: true,
                updated_at: new Date().toISOString(),
            })
            return { success: true, settings: mockImapSettings }
        }
        return mockImapSettings
    }
    if (pathname === '/api/settings/imap/test') {
        return { success: true, message: 'IMAP baglantisi ve kimlik dogrulama basarili', tested_at: new Date().toISOString() }
    }
    if (pathname === '/api/settings/imap/inbox') {
        const requestedFolder = body?.folder || mockImapSettings.folder
        const messages = body?.unread_only
            ? mockImapMessages.filter(mail => mail.unread)
            : mockImapMessages
        return {
            success: true,
            message: `${requestedFolder} klasorunden ${messages.length} mail listelendi`,
            folder: requestedFolder,
            total_messages: mockImapMessages.length,
            returned_messages: messages.length,
            messages,
            tested_at: new Date().toISOString(),
        }
    }
    if (pathname === '/api/settings/imap/message' || pathname.match(/^\/api\/settings\/imap\/messages\/[^/]+$/)) {
        const pathMessageId = pathname.split('/').pop()
        const mail = mockImapMessages.find(item => item.id === String(body?.message_id || pathMessageId)) || mockImapMessages[0]
        return {
            success: true,
            message: 'Mail icerigi alindi',
            id: mail.id,
            from: mail.from,
            subject: mail.subject,
            date: mail.date,
            content_type: 'text/plain',
            body_text: [
                'Merhaba,',
                '',
                'Ilgili DLP sorgu maili icin kullanici aciklamasi asagidadir.',
                'Gonderim tek seferde yuksek match sayisina ulasmistir ve yonetici onayi beklenmektedir.',
                '',
                'Bu kayit mock IMAP endpoint tarafindan uretilmistir.',
            ].join('\n'),
            truncated: false,
            tested_at: new Date().toISOString(),
        }
    }
    if (pathname === '/api/settings/ai') {
        if (method === 'POST') return { success: true, message: 'AI settings saved' }
        return { provider: settingsData.ai_provider, model: settingsData.ai_model, endpoint: settingsData.ai_endpoint, apiKey: settingsData.ai_api_key }
    }
    if (pathname === '/api/settings/ai/test') {
        return { success: true, message: 'AI connection successful', response: 'Hello! I am a mock AI assistant.' }
    }

    // Users Management
    if (pathname === '/api/users') {
        if (method === 'POST') return { id: randomInt(10, 100), ...body, createdAt: new Date().toISOString() }
        return managementUsers
    }
    if (pathname.match(/\/api\/users\/\d+/)) {
        if (method === 'DELETE') return { success: true }
        if (method === 'PUT') return { success: true }
        return managementUsers[0]
    }

    // Logs
    if (pathname === '/api/logs/audit/event-types') {
        return ['LOGIN', 'LOGOUT', 'SETTINGS_CHANGE', 'USER_CREATE', 'INCIDENT_REVIEW', 'REPORT_GENERATE', 'POLICY_UPDATE']
    }
    if (pathname === '/api/logs/audit') {
        return generateLogs(30)
    }
    if (pathname === '/api/logs/application') {
        return generateLogs(20).map(l => ({ ...l, level: randomFrom(['INFO', 'WARN', 'ERROR']), source: randomFrom(['API', 'Worker', 'Scheduler']) }))
    }

    // Domain Features
    if (pathname === '/api/domain-features/columns') {
        if (method === 'POST') return { id: randomInt(10, 100), ...body }
        return domainColumns
    }
    if (pathname === '/api/domain-features') {
        return domainFeatures
    }
    if (pathname === '/api/domain-features/top') {
        return domainFeatures.slice(0, 5)
    }
    if (pathname === '/api/domain-features/bulk-save') {
        return { success: true, updated: randomInt(1, 10) }
    }
    if (pathname === '/api/domain-features/extract-from-incidents') {
        return { success: true, extracted: randomInt(5, 20), message: 'Domains extracted from incidents' }
    }
    if (pathname.match(/\/api\/domain-features\/columns\/\d+/)) {
        return { success: true }
    }

    // Mercek
    if (pathname === '/api/mercek') {
        return mercekData
    }
    if (pathname === '/api/mercek/filters') {
        return { channels, actions, dataTypes, departments: ['IT', 'Finance', 'HR', 'Engineering'] }
    }
    if (pathname === '/api/mercek/statistics') {
        return {
            total: mercekData.length,
            pending: randomInt(3, 10),
            reviewed: randomInt(5, 12),
            resolved: randomInt(2, 8),
            escalated: randomInt(1, 4),
        }
    }

    // Released incidents
    if (pathname === '/api/released-incidents') {
        return allIncidents.filter(i => i.action === 'Released').slice(0, 10)
    }

    // Policy Exceptions
    if (pathname === '/api/policy-exceptions') {
        const exceptionNames = [
            'Muhasebe Departmanı Email İstisnası',
            'IT Yönetici USB İstisnası',
            'Saha Ekibi Cloud Erişim İzni',
            'Hukuk Departmanı Dosya Paylaşım İzni',
            'CEO Asistanı Tam Yetki',
            'Dış Denetçi Geçici Erişim',
            'VPN Kullanıcıları Geçici İstisna',
            'Yedekleme Sistemi İstisnası',
            'Test Ortamı Genel İstisna',
            'Legacy Sistem Entegrasyon İstisnası',
            'Satış Ekibi CRM Erişimi',
            'Pazarlama Email Kampanya İzni',
        ]
        const data = policies.slice(0, 3).map((policyName, pIdx) => ({
            policyName,
            rules: Array.from({ length: randomInt(2, 4) }, (_, rIdx) => ({
                ruleName: `Rule-${pIdx * 10 + rIdx + 1}`,
                exceptions: exceptionNames.slice(pIdx * 4 + rIdx, pIdx * 4 + rIdx + randomInt(1, 3))
            }))
        }))
        return {
            success: true,
            totalExceptions: exceptionNames.length,
            totalPolicies: data.length,
            lastSyncedAt: generateDate(randomInt(0, 2)),
            data
        }
    }
    if (pathname === '/api/policy-exceptions/sync') {
        return { success: true, message: 'Sync completed: 12 exceptions saved', syncedCount: 12, syncedAt: new Date().toISOString() }
    }

    // Policy Inventory
    if (pathname === '/api/policy-inventory/stats') {
        return { totalPolicies: 12, totalRules: 28, totalExceptions: 156, activeExceptionsPercentage: 87 };
    }
    if (pathname === '/api/policy-inventory/export/excel' || pathname === '/api/policy-inventory/export/json') {
        return { success: true, url: '/dummy-download-url' };
    }
    if (pathname === '/api/policy-inventory/import') {
        return { success: true, message: 'Import completed', parsedPolicies: 45, parsedRules: 128, parsedExceptions: 6179 };
    }
    if (pathname.startsWith('/api/policy-inventory')) {
        if (method === 'GET') {
            return {
                success: true,
                data: [
                    {
                        id: 1,
                        policy_name: 'Örnek Politika 1',
                        rules: [
                            {
                                id: 10,
                                rule_name: 'Rule-1',
                                parts_count_type: 'CROSS_COUNT',
                                condition_relation_type: 'AND',
                                classifiers: [{ id: 101, classifier_name: 'XXX', threshold_type: 'CHECK_GREATER_THAN', threshold_value_from: 10, threshold_calculate_type: 'UNIQUE' }],
                                severity_actions: [{ id: 201, selected: 'true', number_of_matches: 0, severity_type: 'MEDIUM', action_plan: 'Audit Only' }],
                                sources: [{ id: 301, resource_name: 'AD Group-1', resource_type: 'DIRECTORY_ENTRY_GROUP', include: 'true' }],
                                destinations: [{ id: 401, channel_type: 'EMAIL', channel_enabled: 'true', resources: [] }],
                                exceptions: [
                                    {
                                        id: 100,
                                        exception_rule_name: 'Exception-A',
                                        enabled: 'true',
                                        description: 'Test exception',
                                        severity_actions: [{ id: 501, severity_type: 'MEDIUM', action_plan: 'Audit Only' }]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };
        } else {
            return { success: true, id: Math.floor(Math.random() * 1000) };
        }
    }

    // Fallback
    return { message: 'Mock endpoint not found', path: pathname }
}

// ─── Server ────────────────────────────────────────────────────────────────────

const server = http.createServer((req, res) => {
    const parsedUrl = url.parse(req.url, true)
    const pathname = parsedUrl.pathname
    const query = parsedUrl.query
    const method = req.method

    // CORS headers
    res.setHeader('Access-Control-Allow-Origin', '*')
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS')
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization')

    if (method === 'OPTIONS') {
        res.writeHead(204)
        res.end()
        return
    }

    // Parse body for POST/PUT
    let body = ''
    req.on('data', chunk => { body += chunk })
    req.on('end', () => {
        let parsedBody = null
        try { parsedBody = body ? JSON.parse(body) : null } catch (e) { }

        const result = handleRequest(pathname, query, method, parsedBody)

        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(result))

        // Log request
        const timestamp = new Date().toLocaleTimeString()
        console.log(`[${timestamp}] ${method} ${pathname} → 200`)
    })
})

server.listen(PORT, () => {
    console.log('')
    console.log('╔══════════════════════════════════════════════════════╗')
    console.log('║          RADAR Mock API Server                      ║')
    console.log(`║          Running on http://localhost:${PORT}            ║`)
    console.log('║                                                      ║')
    console.log('║   ⚠ DELETE THIS FILE after testing is complete       ║')
    console.log('╚══════════════════════════════════════════════════════╝')
    console.log('')
    console.log('Endpoints available:')
    console.log('  /api/auth/login          - Authentication')
    console.log('  /api/risk-trends/*       - Dashboard data')
    console.log('  /api/risk/*              - Risk & incidents')
    console.log('  /api/incidents           - Incident list')
    console.log('  /api/ai-behavioral/*     - AI analysis')
    console.log('  /api/settings/*          - Settings')
    console.log('  /api/users               - User management')
    console.log('  /api/logs/*              - Audit logs')
    console.log('  /api/domain-features/*   - Domain features')
    console.log('  /api/mercek/*            - Mercek analysis')
    console.log('  /api/policy-exceptions   - Policy exceptions')
    console.log('  /api/policy-inventory    - Policy inventory')
    console.log('')
})
