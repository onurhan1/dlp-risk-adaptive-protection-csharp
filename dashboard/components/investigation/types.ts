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
  { token: '{{tam_ad}}', desc: 'Kullanıcı adı (username)' },
  { token: '{{takim}}', desc: 'Takım / departman' },
  { token: '{{tarih}}', desc: 'Bugünün tarihi' },
  { token: '{{olaylar}}', desc: 'Örnek olay (incident) özeti' },
]

export function applyPlaceholders(text: string, user: WeeklyFlagUser | null): string {
  if (!text) return ''
  if (!user) return text
  const incidentsSummary = (user.sample_incidents || [])
    .map(i => `- ${new Date(i.timestamp).toLocaleString('tr-TR')} | ${i.policy ?? '-'} | ${i.max_matches} eşleşme | ${i.destination ?? '-'}`)
    .join('\n')
  return text
    .replaceAll('{{kullanici}}', user.contact_email || user.user_email)
    .replaceAll('{{tam_ad}}', user.user_email)
    .replaceAll('{{takim}}', user.team || '-')
    .replaceAll('{{tarih}}', new Date().toLocaleDateString('tr-TR'))
    .replaceAll('{{olaylar}}', incidentsSummary || '-')
}
