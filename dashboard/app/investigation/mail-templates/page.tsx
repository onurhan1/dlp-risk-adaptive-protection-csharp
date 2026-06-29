'use client'

import MailTemplateManager from '@/components/investigation/MailTemplateManager'

export default function MailTemplatesPage() {
  return (
    <div className="dashboard-page">
      <div className="dashboard-header">
        <div>
          <h1>Mail Şablonları</h1>
          <p className="text-muted">Soruşturma mailleri için yeniden kullanılabilir şablonlar oluşturun ve yönetin</p>
        </div>
      </div>

      <MailTemplateManager />
    </div>
  )
}
