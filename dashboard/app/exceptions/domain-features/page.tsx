'use client'

import React from 'react'
import DomainFeaturesManager from '@/components/DomainFeaturesManager'
import { useRouter } from 'next/navigation'
import { Globe } from 'lucide-react'

export default function DomainFeaturesPage() {
  const router = useRouter()

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div style={{
              width: '38px',
              height: '38px',
              borderRadius: '10px',
              background: 'linear-gradient(135deg, #3b82f6, #06b6d4)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 4px 12px rgba(59, 130, 246, 0.3)'
            }}>
              <Globe size={20} color="#fff" />
            </div>
            <h1 style={{ fontSize: '24px', fontWeight: '700', margin: 0, background: 'linear-gradient(135deg, #3b82f6, #06b6d4)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>Domain Features</h1>
          </div>
        </div>
        <DomainFeaturesManager onClose={() => router.push('/exceptions')} />
      </div>
    </div>
  )
}
