'use client'

import React, { Suspense } from 'react'
import DomainFeaturesManager from '@/components/DomainFeaturesManager'
import { useRouter } from 'next/navigation'

function DomainFeaturesContent() {
  const router = useRouter()

  return (
    <div style={{ minHeight: '100vh', background: 'var(--background)', padding: '24px' }}>
      <div style={{ maxWidth: '100%', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <h1 style={{ fontSize: '20px', fontWeight: '600', color: 'var(--text-primary)', margin: 0 }}>Domain Features</h1>
        </div>
        <DomainFeaturesManager onClose={() => router.push('/exceptions')} />
      </div>
    </div>
  )
}

export default function DomainFeaturesPage() {
  return (
    <Suspense fallback={<div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Loading...</div>}>
      <DomainFeaturesContent />
    </Suspense>
  )
}
