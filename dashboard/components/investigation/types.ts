export interface MailTemplate {
  id: number
  name: string
  subject: string
  body: string
  created_at?: string
  updated_at?: string
}

export interface WeeklyFlagIncident {
  timestamp: string
  policy: string | null
  max_matches: number
  destination: string | null
  channel: string | null
}

export interface WeeklyFlagUser {
  user_email: string
  full_name: string | null
  team: string | null
  contact_email: string
  gender?: string | null
  trigger_count: number
  first_seen: string
  last_seen: string
  sample_incidents: WeeklyFlagIncident[]
}

export interface WeeklyFlagsResult {
  personal_email_senders: WeeklyFlagUser[]
  high_volume: WeeklyFlagUser[]
  massive_matches: WeeklyFlagUser[]
}

/** Placeholders supported in mail templates, filled from the flagged user. */
export const TEMPLATE_PLACEHOLDERS = [
  { token: '{{kullanici}}', desc: 'Kullanıcı e-postası' },
  { token: '{{tam_ad}}', desc: 'LDAP adıyla hitap' },
  { token: '{{ad_soyad}}', desc: 'LDAP ad soyad' },
  { token: '{{full_name}}', desc: 'LDAP ad soyad' },
  { token: '{{hitap}}', desc: 'Bey / Hanım' },
  { token: '{{takim}}', desc: 'Takım / departman' },
  { token: '{{tarih}}', desc: 'Ilgili olay kaydinin tarihi' },
  { token: '{{olay_tarihi}}', desc: 'Ilgili olay kaydinin tarih ve saati' },
  { token: '{{olay_saati}}', desc: 'Ilgili olay kaydinin saati' },
  { token: '{{destination}}', desc: 'En yüksek eşleşmeli olayın hedefi' },
  { token: '{{hedef}}', desc: 'En yüksek eşleşmeli olayın hedefi' },
  { token: '{{kanal}}', desc: 'En yüksek eşleşmeli olayın kanalı' },
  { token: '{{channel}}', desc: 'En yüksek eşleşmeli olayın kanalı' },
  { token: '{{policy}}', desc: 'En yüksek eşleşmeli olayın policy/rule bilgisi' },
  { token: '{{kural}}', desc: 'En yüksek eşleşmeli olayın policy/rule bilgisi' },
  { token: '{{max_match}}', desc: 'En yüksek eşleşme sayısı' },
  { token: '{{max_matches}}', desc: 'En yüksek eşleşme sayısı' },
  { token: '{{olaylar}}', desc: 'Örnek olay (incident) özeti' },
]

function normalizeGender(value?: string | null): 'male' | 'female' | null {
  const normalized = (value || '').trim().toLocaleLowerCase('tr-TR')
  if (!normalized) return null
  if (normalized === 'bayan') return 'female'
  if (['m', 'male', 'man', 'erkek', 'bay', 'e'].includes(normalized)) return 'male'
  if (['f', 'female', 'woman', 'kadin', 'kadın', 'k'].includes(normalized)) return 'female'
  return null
}

function firstNamePart(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length <= 1) return fullName.trim()
  return parts.slice(0, -1).join(' ')
}

function removeDirectorySuffix(fullName: string): string {
  return fullName.split('/')[0].trim()
}

function salutationName(user: WeeklyFlagUser): string {
  const fullName = (user.full_name || '').trim()
  if (!fullName) return user.user_email

  const gender = normalizeGender(user.gender)
  const firstNames = firstNamePart(removeDirectorySuffix(fullName))
  if (gender === 'male') return `${firstNames} Bey`
  if (gender === 'female') return `${firstNames} Hanım`
  return firstNames
}

function honorific(user: WeeklyFlagUser): string {
  const gender = normalizeGender(user.gender)
  if (gender === 'male') return 'Bey'
  if (gender === 'female') return 'Hanım'
  return ''
}

export function applyPlaceholders(text: string, user: WeeklyFlagUser | null): string {
  if (!text) return ''
  if (!user) return text
  const incidentsSummary = (user.sample_incidents || [])
    .map(i => `- ${new Date(i.timestamp).toLocaleString('tr-TR')} | ${i.policy ?? '-'} | ${i.max_matches} eşleşme | ${i.destination ?? '-'}`)
    .join('\n')
  const primaryIncident = (user.sample_incidents || [])[0]
  const incidentDate = primaryIncident?.timestamp ? new Date(primaryIncident.timestamp) : new Date(user.last_seen || Date.now())
  const fullName = user.full_name || user.user_email
  return text
    .replaceAll('{{kullanici}}', user.contact_email || user.user_email)
    .replaceAll('{{tam_ad}}', salutationName(user))
    .replaceAll('{{ad_soyad}}', fullName)
    .replaceAll('{{full_name}}', fullName)
    .replaceAll('{{hitap}}', honorific(user))
    .replaceAll('{{takim}}', user.team || '-')
    .replaceAll('{{tarih}}', incidentDate.toLocaleDateString('tr-TR'))
    .replaceAll('{{olay_tarihi}}', incidentDate.toLocaleString('tr-TR'))
    .replaceAll('{{olay_saati}}', incidentDate.toLocaleTimeString('tr-TR'))
    .replaceAll('{{destination}}', primaryIncident?.destination || '-')
    .replaceAll('{{hedef}}', primaryIncident?.destination || '-')
    .replaceAll('{{kanal}}', primaryIncident?.channel || '-')
    .replaceAll('{{channel}}', primaryIncident?.channel || '-')
    .replaceAll('{{policy}}', primaryIncident?.policy || '-')
    .replaceAll('{{kural}}', primaryIncident?.policy || '-')
    .replaceAll('{{max_match}}', String(primaryIncident?.max_matches ?? '-'))
    .replaceAll('{{max_matches}}', String(primaryIncident?.max_matches ?? '-'))
    .replaceAll('{{olaylar}}', incidentsSummary || '-')
}

/** Şablon gövdesinde HTML kullanıldığını gösteren blok seviyesi etiketler. */
const BLOCK_LEVEL_HTML = /<(p|div|br|table|tr|td|th|ul|ol|li|h[1-6]|html|body|blockquote|pre|section)\b[^>]*>/i

function escapeHtml(text: string): string {
  return text
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
}

/**
 * Şablonlar düz bir textarea'da yazıldığı için gövde çoğunlukla satır sonları olan
 * düz metindir. Mail IsBodyHtml=true ile gönderildiğinden bu satır sonları alıcıda
 * yok sayılır ve içerik tek bir blok halinde, biçimsiz gelir. Burada düz metni HTML'e
 * çevirip okunabilir bir kapsayıcıya alıyoruz; böylece giden mail önizlemeyle birebir olur.
 * Zaten HTML yazılmış şablonlar olduğu gibi korunur.
 */
export function toEmailHtml(body: string): string {
  const content = (body || '').trim()
  if (!content) return ''

  // Tam bir HTML dokümanı verilmişse kendi <head>/<style> kurgusu bozulmasın.
  if (/<html[\s>]/i.test(content)) return content

  const inner = BLOCK_LEVEL_HTML.test(content)
    ? content
    : escapeHtml(content).replace(/\r\n|\r|\n/g, '<br />')

  return `<div style="font-family: Arial, Helvetica, sans-serif; font-size: 14px; line-height: 1.6; color: #0f172a;">${inner}</div>`
}
