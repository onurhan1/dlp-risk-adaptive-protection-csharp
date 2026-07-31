'use client'

import { useMemo } from 'react'
import type { CSSProperties } from 'react'
import { Settings2 } from 'lucide-react'
import { MailTemplate, TEMPLATE_PLACEHOLDERS, applyPlaceholders, toEmailHtml } from '../types'
import type { WeeklyFlagUser } from '../types'
import {
  WEEKLY_FLAG_CRITERIA,
  INCIDENT_METRICS,
  BREAKDOWN_DIMENSIONS,
  METRIC_PLACEHOLDERS,
  buildCron,
  countMetricFilters,
  isValidCron,
  isValidEmail,
  nodeDefinition,
  type PlaybookNode,
} from './types'
import { fieldGroupStyle, hintStyle, inputStyle, labelStyle } from './formStyles'

interface Props {
  node: PlaybookNode | null
  templates: MailTemplate[]
  /**
   * True when this node sits downstream of an incident metric source. A metric mail describes the
   * whole organisation, so it uses a different token set and requires a fixed recipient.
   */
  inMetricFlow?: boolean
  onChange: (node: PlaybookNode) => void
}

/** Stand-in user used for the mail preview, so the analyst sees where placeholders land. */
const PREVIEW_USER: WeeklyFlagUser = {
  user_email: 'ornek.kullanici@firma.com',
  full_name: 'Örnek Kullanıcı',
  team: 'Bilgi Teknolojileri',
  contact_email: 'ornek.kullanici@firma.com',
  trigger_count: 4,
  first_seen: new Date().toISOString(),
  last_seen: new Date().toISOString(),
  sample_incidents: [
    {
      timestamp: new Date().toISOString(),
      policy: 'KT Şüpheli Kullanıcı Aktivitesi',
      max_matches: 620,
      destination: 'ornek@gmail.com',
      channel: 'Email',
    },
  ],
}

const DAYS = [
  { value: 1, label: 'Pazartesi' },
  { value: 2, label: 'Salı' },
  { value: 3, label: 'Çarşamba' },
  { value: 4, label: 'Perşembe' },
  { value: 5, label: 'Cuma' },
  { value: 6, label: 'Cumartesi' },
  { value: 0, label: 'Pazar' },
]

export default function NodeInspector({ node, templates, inMetricFlow = false, onChange }: Props) {
  const definition = node ? nodeDefinition(node.type) : undefined

  const setConfig = (patch: Record<string, any>) => {
    if (!node) return
    onChange({ ...node, config: { ...node.config, ...patch } })
  }

  const setLabel = (label: string) => {
    if (!node) return
    onChange({ ...node, label })
  }

  if (!node || !definition) {
    return (
      <aside style={panelStyle}>
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            height: '100%',
            gap: '10px',
            color: 'var(--text-muted)',
            textAlign: 'center',
            padding: '24px',
          }}
        >
          <Settings2 size={26} />
          <div style={{ fontSize: '13px' }}>Ayarlarını görmek için bir node seçin</div>
        </div>
      </aside>
    )
  }

  const Icon = definition.icon

  return (
    <aside style={panelStyle}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '16px' }}>
        <div
          style={{
            width: '32px',
            height: '32px',
            borderRadius: '9px',
            background: definition.color,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: 'white',
            flexShrink: 0,
          }}
        >
          <Icon size={17} />
        </div>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-primary)' }}>{definition.label}</div>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{node.type}</div>
        </div>
      </div>

      <div style={fieldGroupStyle}>
        <div>
          <label style={labelStyle}>Node Adı</label>
          <input style={inputStyle} value={node.label} onChange={e => setLabel(e.target.value)} />
        </div>

        {node.type === 'trigger.schedule' && <ScheduleForm node={node} setConfig={setConfig} />}
        {node.type === 'trigger.manual' && (
          <p style={hintStyle}>
            Bu akış yalnızca "Şimdi Çalıştır" ile başlar. Zamanlanmış çalıştırma için Zamanlama
            tetikleyicisini kullanın.
          </p>
        )}
        {node.type === 'source.weeklyFlags' && <WeeklyFlagsForm node={node} setConfig={setConfig} />}
        {node.type === 'source.incidentMetric' && <IncidentMetricForm node={node} setConfig={setConfig} />}
        {node.type === 'transform.filter' && <FilterForm node={node} setConfig={setConfig} />}
        {node.type === 'logic.condition' && <ConditionForm node={node} setConfig={setConfig} />}
        {node.type === 'logic.metricThreshold' && <MetricThresholdForm node={node} setConfig={setConfig} />}
        {node.type === 'action.sendMail' && (
          <SendMailForm node={node} setConfig={setConfig} templates={templates} inMetricFlow={inMetricFlow} />
        )}
        {node.type === 'output.report' && (
          <div>
            <label style={labelStyle}>Rapor Başlığı</label>
            <input
              style={inputStyle}
              value={node.config.title ?? ''}
              onChange={e => setConfig({ title: e.target.value })}
              placeholder="Haftalık Sorgu Raporu"
            />
            <p style={hintStyle}>
              Bu adıma ulaşan gönderimler tarih, konu, alıcı ve durum bilgisiyle rapora yazılır ve
              CSV / Excel / PDF olarak dışa aktarılabilir.
            </p>
          </div>
        )}
      </div>
    </aside>
  )
}

// ── Per-type forms ─────────────────────────────────────────────────────────

function ScheduleForm({ node, setConfig }: { node: PlaybookNode; setConfig: (p: Record<string, any>) => void }) {
  const frequency = node.config.frequency ?? 'weekly'
  const cron = buildCron(node.config)
  const cronValid = isValidCron(cron)

  // Keep the stored cron in step with the preset fields so the backend and UI agree.
  const update = (patch: Record<string, any>) => {
    const merged = { ...node.config, ...patch }
    setConfig({ ...patch, cron: patch.cron !== undefined ? patch.cron : buildCron(merged) })
  }

  return (
    <>
      <div>
        <label style={labelStyle}>Sıklık</label>
        <select style={inputStyle} value={frequency} onChange={e => update({ frequency: e.target.value })}>
          <option value="weekly">Haftalık</option>
          <option value="daily">Günlük</option>
          <option value="hourly">Saatlik</option>
          <option value="cron">Cron ifadesi</option>
        </select>
      </div>

      {frequency === 'weekly' && (
        <div>
          <label style={labelStyle}>Gün</label>
          <select
            style={inputStyle}
            value={node.config.day_of_week ?? 1}
            onChange={e => update({ day_of_week: Number(e.target.value) })}
          >
            {DAYS.map(d => (
              <option key={d.value} value={d.value}>{d.label}</option>
            ))}
          </select>
        </div>
      )}

      {frequency !== 'cron' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
          {frequency !== 'hourly' && (
            <div>
              <label style={labelStyle}>Saat</label>
              <input
                type="number"
                min={0}
                max={23}
                style={inputStyle}
                value={node.config.hour ?? 9}
                onChange={e => update({ hour: Number(e.target.value) })}
              />
            </div>
          )}
          <div>
            <label style={labelStyle}>Dakika</label>
            <input
              type="number"
              min={0}
              max={59}
              style={inputStyle}
              value={node.config.minute ?? 0}
              onChange={e => update({ minute: Number(e.target.value) })}
            />
          </div>
        </div>
      )}

      {frequency === 'cron' && (
        <div>
          <label style={labelStyle}>Cron İfadesi</label>
          <input
            style={{ ...inputStyle, fontFamily: 'monospace' }}
            value={node.config.cron ?? ''}
            onChange={e => update({ cron: e.target.value })}
            placeholder="0 9 * * 1"
          />
          <p style={hintStyle}>
            5 alan: dakika saat ayın-günü ay haftanın-günü. Örnek: <code>0 9 * * 1</code> → her Pazartesi 09:00.
          </p>
        </div>
      )}

      <div
        style={{
          padding: '9px 11px',
          borderRadius: '6px',
          background: cronValid ? 'var(--surface-hover)' : '#fee2e2',
          border: `1px solid ${cronValid ? 'var(--border)' : '#fca5a5'}`,
          fontSize: '12px',
          color: cronValid ? 'var(--text-secondary)' : '#991b1b',
        }}
      >
        {cronValid ? (
          <>
            Cron: <code style={{ fontFamily: 'monospace' }}>{cron}</code>
          </>
        ) : (
          'Cron ifadesi geçersiz — 5 alan bekleniyor.'
        )}
      </div>

      <p style={hintStyle}>
        Saatler sunucu saatine (UTC) göre değerlendirilir.
      </p>
    </>
  )
}

function WeeklyFlagsForm({ node, setConfig }: { node: PlaybookNode; setConfig: (p: Record<string, any>) => void }) {
  const criteria: string[] = Array.isArray(node.config.criteria) ? node.config.criteria : []

  const toggle = (value: string) => {
    setConfig({
      criteria: criteria.includes(value) ? criteria.filter(c => c !== value) : [...criteria, value],
    })
  }

  return (
    <>
      <div>
        <label style={labelStyle}>Geriye Dönük Gün Sayısı</label>
        <input
          type="number"
          min={1}
          max={90}
          style={inputStyle}
          value={node.config.days ?? 7}
          onChange={e => setConfig({ days: Number(e.target.value) })}
        />
      </div>

      <div>
        <label style={labelStyle}>Kriterler</label>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {WEEKLY_FLAG_CRITERIA.map(criterion => (
            <label
              key={criterion.value}
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: '8px',
                fontSize: '12px',
                color: 'var(--text-primary)',
                cursor: 'pointer',
                lineHeight: 1.4,
              }}
            >
              <input
                type="checkbox"
                checked={criteria.includes(criterion.value)}
                onChange={() => toggle(criterion.value)}
                style={{ marginTop: '2px', flexShrink: 0 }}
              />
              <span>{criterion.label}</span>
            </label>
          ))}
        </div>
        {criteria.length === 0 && (
          <p style={{ ...hintStyle, color: '#991b1b' }}>En az bir kriter seçilmeli.</p>
        )}
        <p style={hintStyle}>
          Aynı kişi birden fazla kritere takılırsa yalnızca bir kez listelenir (olay sayısı yüksek olan kriterle).
        </p>
      </div>
    </>
  )
}

function IncidentMetricForm({ node, setConfig }: { node: PlaybookNode; setConfig: (p: Record<string, any>) => void }) {
  const filterCount = countMetricFilters(node.config || {})

  return (
    <>
      <div>
        <label style={labelStyle}>Ölçülecek Metrik</label>
        <select
          style={inputStyle}
          value={node.config.metric ?? 'total_incidents'}
          onChange={e => setConfig({ metric: e.target.value })}
        >
          {INCIDENT_METRICS.map(m => (
            <option key={m.value} value={m.value}>{m.label}</option>
          ))}
        </select>
      </div>

      <div>
        <label style={labelStyle}>Geriye Dönük Gün Sayısı</label>
        <input
          type="number"
          min={1}
          max={365}
          style={inputStyle}
          value={node.config.days ?? 7}
          onChange={e => setConfig({ days: Number(e.target.value) })}
        />
        <p style={hintStyle}>Haftalık kontrol için 7. Ölçüm her çalıştırmada bu pencereyi yeniden hesaplar.</p>
      </div>

      <div>
        <label style={labelStyle}>Kırılım</label>
        <select
          style={inputStyle}
          value={node.config.breakdown_by ?? 'channel'}
          onChange={e => setConfig({ breakdown_by: e.target.value })}
        >
          {BREAKDOWN_DIMENSIONS.map(d => (
            <option key={d.value} value={d.value}>{d.label}</option>
          ))}
        </select>
        <p style={hintStyle}>
          Mail içeriğinde <code>{'{{ozet}}'}</code> ile bu kırılımın dökümünü basabilirsin.
        </p>
      </div>

      <div
        style={{
          padding: '8px 11px',
          borderRadius: '6px',
          background: 'var(--surface-hover)',
          border: '1px solid var(--border)',
          fontSize: '12px',
          color: 'var(--text-secondary)',
        }}
      >
        {filterCount === 0
          ? 'Filtre yok — tüm incident\'lar sayılıyor.'
          : `${filterCount} filtre aktif.`}
      </div>

      <div style={{ borderTop: '1px solid var(--border)', paddingTop: '12px' }}>
        <div style={{ fontSize: '11px', fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase', color: 'var(--text-muted)', marginBottom: '10px' }}>
          Filtreler (opsiyonel)
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div>
            <label style={labelStyle}>Kanal</label>
            <input
              style={inputStyle}
              value={listToText(node.config.channels)}
              onChange={e => setConfig({ channels: textToList(e.target.value) })}
              placeholder="Email, Removable Storage, Print"
            />
            <p style={hintStyle}>Virgülle ayırın. Boşsa tüm kanallar sayılır.</p>
          </div>

          <div>
            <label style={labelStyle}>Veri Tipi</label>
            <input
              style={inputStyle}
              value={listToText(node.config.data_types)}
              onChange={e => setConfig({ data_types: textToList(e.target.value) })}
              placeholder="PII, PCI, CCN"
            />
          </div>

          <div>
            <label style={labelStyle}>Aksiyon</label>
            <input
              style={inputStyle}
              value={listToText(node.config.actions)}
              onChange={e => setConfig({ actions: textToList(e.target.value) })}
              placeholder="BLOCK, QUARANTINE, AUTHORIZED"
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
            <div>
              <label style={labelStyle}>Min Şiddet</label>
              <input
                type="number"
                min={1}
                max={5}
                style={inputStyle}
                value={node.config.min_severity ?? ''}
                onChange={e => setConfig({ min_severity: e.target.value === '' ? null : Number(e.target.value) })}
                placeholder="1-5"
              />
            </div>
            <div>
              <label style={labelStyle}>Min Risk Skoru</label>
              <input
                type="number"
                min={0}
                max={100}
                style={inputStyle}
                value={node.config.min_risk_score ?? ''}
                onChange={e => setConfig({ min_risk_score: e.target.value === '' ? null : Number(e.target.value) })}
                placeholder="0-100"
              />
            </div>
          </div>

          <div>
            <label style={labelStyle}>Min Eşleşme Sayısı</label>
            <input
              type="number"
              min={0}
              style={inputStyle}
              value={node.config.min_matches ?? ''}
              onChange={e => setConfig({ min_matches: e.target.value === '' ? null : Number(e.target.value) })}
              placeholder="Örn. 500"
            />
          </div>

          <div>
            <label style={labelStyle}>Politika Adı İçerir</label>
            <input
              style={inputStyle}
              value={node.config.policy_contains ?? ''}
              onChange={e => setConfig({ policy_contains: e.target.value })}
              placeholder="KT Şüpheli Kullanıcı Aktivitesi"
            />
          </div>

          <div>
            <label style={labelStyle}>Takım / Departman İçerir</label>
            <input
              style={inputStyle}
              value={node.config.team_contains ?? ''}
              onChange={e => setConfig({ team_contains: e.target.value })}
              placeholder="Örn. Finans"
            />
          </div>

          <div>
            <label style={labelStyle}>Hedef İçerir</label>
            <input
              style={inputStyle}
              value={node.config.destination_contains ?? ''}
              onChange={e => setConfig({ destination_contains: e.target.value })}
              placeholder="gmail.com"
            />
          </div>
        </div>
      </div>
    </>
  )
}

function MetricThresholdForm({ node, setConfig }: { node: PlaybookNode; setConfig: (p: Record<string, any>) => void }) {
  return (
    <>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
        <div>
          <label style={labelStyle}>Koşul</label>
          <select style={inputStyle} value={node.config.op ?? 'gt'} onChange={e => setConfig({ op: e.target.value })}>
            <option value="gt">&gt; büyük</option>
            <option value="gte">≥ büyük eşit</option>
            <option value="lt">&lt; küçük</option>
            <option value="lte">≤ küçük eşit</option>
            <option value="eq">= eşit</option>
          </select>
        </div>
        <div>
          <label style={labelStyle}>Eşik Değeri</label>
          <input
            type="number"
            style={inputStyle}
            value={node.config.value ?? 0}
            onChange={e => setConfig({ value: Number(e.target.value) })}
          />
        </div>
      </div>

      <p style={hintStyle}>
        Ölçülen metrik bu koşulu sağlıyorsa akış <strong>Aşıldı</strong> kolundan devam eder ve mail gider.
        Sağlamıyorsa <strong>Aşılmadı</strong> kolundan çıkar — o kola hiçbir şey bağlamazsan akış sessizce durur,
        yani eşik aşılmadığında mail gönderilmez.
      </p>

      <div
        style={{
          padding: '9px 11px',
          borderRadius: '6px',
          background: 'var(--surface-hover)',
          border: '1px solid var(--border)',
          fontSize: '12px',
          color: 'var(--text-secondary)',
        }}
      >
        Bu node yalnızca <strong>Incident Metriği</strong> girdisiyle çalışır; kullanıcı listesi taşıyan
        bir kaynağa bağlanamaz. Kullanıcı bazlı eşik için <strong>Koşul</strong> node'unu kullan.
      </div>
    </>
  )
}

function FilterForm({ node, setConfig }: { node: PlaybookNode; setConfig: (p: Record<string, any>) => void }) {
  return (
    <>
      <div>
        <label style={labelStyle}>Minimum Olay Sayısı</label>
        <input
          type="number"
          min={0}
          style={inputStyle}
          value={node.config.min_trigger_count ?? ''}
          onChange={e => setConfig({ min_trigger_count: e.target.value === '' ? null : Number(e.target.value) })}
          placeholder="Sınır yok"
        />
      </div>

      <div>
        <label style={labelStyle}>Takım / Departman İçerir</label>
        <input
          style={inputStyle}
          value={node.config.team_contains ?? ''}
          onChange={e => setConfig({ team_contains: e.target.value })}
          placeholder="Örn. Finans"
        />
      </div>

      <div>
        <label style={labelStyle}>Yalnızca Bu Alan Adları</label>
        <input
          style={inputStyle}
          value={listToText(node.config.email_domain_in)}
          onChange={e => setConfig({ email_domain_in: textToList(e.target.value) })}
          placeholder="firma.com, firma.com.tr"
        />
        <p style={hintStyle}>Virgülle ayırın. Boş bırakılırsa alan adı filtresi uygulanmaz.</p>
      </div>

      <div>
        <label style={labelStyle}>Muaf Kullanıcılar</label>
        <textarea
          style={{ ...inputStyle, minHeight: '70px', resize: 'vertical' }}
          value={listToText(node.config.exclude_users)}
          onChange={e => setConfig({ exclude_users: textToList(e.target.value) })}
          placeholder="ahmet@firma.com, mehmet@firma.com"
        />
        <p style={hintStyle}>Bu adreslere hiçbir zaman mail gönderilmez.</p>
      </div>
    </>
  )
}

function ConditionForm({ node, setConfig }: { node: PlaybookNode; setConfig: (p: Record<string, any>) => void }) {
  return (
    <>
      <div>
        <label style={labelStyle}>Karşılaştırılan Alan</label>
        <select
          style={inputStyle}
          value={node.config.field ?? 'triggerCount'}
          onChange={e => setConfig({ field: e.target.value })}
        >
          <option value="triggerCount">Olay sayısı</option>
          <option value="maxMatches">En yüksek eşleşme sayısı</option>
        </select>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
        <div>
          <label style={labelStyle}>Koşul</label>
          <select style={inputStyle} value={node.config.op ?? 'gte'} onChange={e => setConfig({ op: e.target.value })}>
            <option value="gte">≥ büyük eşit</option>
            <option value="gt">&gt; büyük</option>
            <option value="lte">≤ küçük eşit</option>
            <option value="lt">&lt; küçük</option>
            <option value="eq">= eşit</option>
          </select>
        </div>
        <div>
          <label style={labelStyle}>Değer</label>
          <input
            type="number"
            style={inputStyle}
            value={node.config.value ?? 0}
            onChange={e => setConfig({ value: Number(e.target.value) })}
          />
        </div>
      </div>

      <p style={hintStyle}>
        Koşulu sağlayanlar <strong>Evet</strong> kolundan, sağlamayanlar <strong>Hayır</strong> kolundan çıkar.
        Her kola ayrı bir mail adımı bağlayabilirsiniz.
      </p>
    </>
  )
}

function SendMailForm({
  node,
  setConfig,
  templates,
  inMetricFlow,
}: {
  node: PlaybookNode
  setConfig: (p: Record<string, any>) => void
  templates: MailTemplate[]
  inMetricFlow: boolean
}) {
  const templateId = node.config.template_id ? String(node.config.template_id) : ''
  const template = templates.find(t => String(t.id) === templateId)
  const recipientMode = node.config.recipient_mode ?? 'user'

  const effectiveSubject = node.config.subject_override?.trim() || template?.subject || ''
  const effectiveBody = node.config.body_override?.trim() || template?.body || ''

  // Metric mails have no user to preview against, so their tokens are shown as-is.
  const previewSubject = useMemo(
    () => (inMetricFlow ? effectiveSubject : applyPlaceholders(effectiveSubject, PREVIEW_USER)),
    [effectiveSubject, inMetricFlow]
  )
  const previewBody = useMemo(
    () => toEmailHtml(inMetricFlow ? effectiveBody : applyPlaceholders(effectiveBody, PREVIEW_USER)),
    [effectiveBody, inMetricFlow]
  )

  const ccEmail = String(node.config.cc_email ?? '').trim()

  return (
    <>
      {inMetricFlow && (
        <div
          style={{
            padding: '9px 11px',
            borderRadius: '6px',
            background: 'rgba(8,145,178,0.10)',
            border: '1px solid rgba(8,145,178,0.35)',
            fontSize: '12px',
            color: 'var(--text-primary)',
            lineHeight: 1.45,
          }}
        >
          Bu mail bir <strong>Incident Metriği</strong> akışında. Kurum toplamı için tek bir özet maili
          gönderilir; kişi başına mail yoktur. Bu yüzden <strong>Alıcı sabit bir adres olmalı</strong> ve
          aşağıdaki metrik alanları kullanılır.
        </div>
      )}

      {inMetricFlow && recipientMode !== 'fixed' && (
        <div
          style={{
            padding: '9px 11px',
            borderRadius: '6px',
            background: '#fee2e2',
            border: '1px solid #fca5a5',
            fontSize: '12px',
            color: '#991b1b',
          }}
        >
          Alıcı'yı "Sabit bir adres" yapmadan bu akış kaydedilemez ve çalıştırılamaz.
        </div>
      )}

      <div>
        <label style={labelStyle}>Mail Şablonu</label>
        <select style={inputStyle} value={templateId} onChange={e => setConfig({ template_id: e.target.value ? Number(e.target.value) : null })}>
          <option value="">— Şablon seçin —</option>
          {templates.map(t => (
            <option key={t.id} value={String(t.id)}>{t.name}</option>
          ))}
        </select>
        {templates.length === 0 && (
          <p style={hintStyle}>Kayıtlı şablon yok. Mail Şablonları sayfasından bir şablon oluşturun.</p>
        )}
      </div>

      <div>
        <label style={labelStyle}>Alıcı</label>
        <select
          style={inputStyle}
          value={recipientMode}
          onChange={e => setConfig({ recipient_mode: e.target.value })}
        >
          <option value="user">İşaretlenen kullanıcının kendisi</option>
          <option value="fixed">Sabit bir adres</option>
        </select>
      </div>

      {recipientMode === 'fixed' && (
        <div>
          <label style={labelStyle}>Sabit Alıcı Adresi</label>
          <input
            type="email"
            style={inputStyle}
            value={node.config.fixed_recipient ?? ''}
            onChange={e => setConfig({ fixed_recipient: e.target.value })}
            placeholder="yonetici@firma.com"
          />
          {!isValidEmail(node.config.fixed_recipient) && (
            <p style={{ ...hintStyle, color: '#991b1b' }}>Geçerli bir e-posta adresi girin.</p>
          )}
          <p style={hintStyle}>
            Kullanıcı listesi yine bu adrese ayrı ayrı mail olarak gider; özet için Koşul + sabit alıcı
            birleşimini kullanın.
          </p>
        </div>
      )}

      <div>
        <label style={labelStyle}>CC <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>(opsiyonel)</span></label>
        <input
          type="email"
          style={inputStyle}
          value={node.config.cc_email ?? ''}
          onChange={e => setConfig({ cc_email: e.target.value })}
          placeholder="dlp-ekip@firma.com"
        />
        {ccEmail !== '' && !isValidEmail(ccEmail) && (
          <p style={{ ...hintStyle, color: '#991b1b' }}>Geçerli bir CC adresi girin.</p>
        )}
      </div>

      <div>
        <label style={labelStyle}>Konu <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>(şablonu ezer)</span></label>
        <input
          style={inputStyle}
          value={node.config.subject_override ?? ''}
          onChange={e => setConfig({ subject_override: e.target.value })}
          placeholder={template?.subject || 'Şablondaki konu kullanılır'}
        />
      </div>

      <div>
        <label style={labelStyle}>İçerik <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>(şablonu ezer)</span></label>
        <textarea
          style={{ ...inputStyle, minHeight: '110px', resize: 'vertical' }}
          value={node.config.body_override ?? ''}
          onChange={e => setConfig({ body_override: e.target.value })}
          placeholder={template ? 'Şablondaki içerik kullanılır' : 'Mail içeriği'}
        />
      </div>

      <div>
        <label style={labelStyle}>
          Kullanılabilir Alanlar
          {inMetricFlow && <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}> (metrik)</span>}
        </label>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '5px' }}>
          {(inMetricFlow ? METRIC_PLACEHOLDERS : TEMPLATE_PLACEHOLDERS).map(placeholder => (
            <span
              key={placeholder.token}
              title={placeholder.desc}
              style={{
                fontSize: '10px',
                fontFamily: 'monospace',
                padding: '3px 6px',
                borderRadius: '4px',
                background: 'var(--surface-hover)',
                border: '1px solid var(--border)',
                color: 'var(--text-secondary)',
              }}
            >
              {placeholder.token}
            </span>
          ))}
        </div>
      </div>

      {effectiveSubject && (
        <div>
          <label style={labelStyle}>
            Önizleme{' '}
            <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>
              {inMetricFlow ? '(alanlar çalıştırmada dolar)' : '(örnek kullanıcıyla)'}
            </span>
          </label>
          <div style={{ border: '1px solid var(--border)', borderRadius: '6px', overflow: 'hidden' }}>
            <div
              style={{
                padding: '8px 11px',
                borderBottom: '1px solid var(--border)',
                background: 'var(--surface-hover)',
                fontSize: '12px',
                fontWeight: 600,
                color: 'var(--text-primary)',
                wordBreak: 'break-word',
              }}
            >
              {previewSubject}
            </div>
            <div
              style={{
                padding: '10px 12px',
                background: 'white',
                color: '#0f172a',
                fontSize: '12px',
                maxHeight: '180px',
                overflowY: 'auto',
                wordBreak: 'break-word',
              }}
              dangerouslySetInnerHTML={{ __html: previewBody || '<em style="color:#94a3b8">İçerik boş</em>' }}
            />
          </div>
        </div>
      )}
    </>
  )
}

// ── Helpers ────────────────────────────────────────────────────────────────

function listToText(value: any): string {
  if (Array.isArray(value)) return value.join(', ')
  return String(value ?? '')
}

function textToList(text: string): string[] {
  return text.split(/[,;\n\r]/).map(s => s.trim()).filter(Boolean)
}

const panelStyle: CSSProperties = {
  width: '320px',
  flexShrink: 0,
  borderLeft: '1px solid var(--border)',
  background: 'var(--surface)',
  overflowY: 'auto',
  padding: '14px',
}
