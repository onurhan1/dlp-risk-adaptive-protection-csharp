'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'

export default function AIBehavioralOverviewRedirect() {
  const router = useRouter()
  useEffect(() => {
    router.replace('/ai-behavioral')
  }, [router])
  return null
}
