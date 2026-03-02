'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

export default function AISettingsPage() {
    const router = useRouter()

    useEffect(() => {
        router.replace('/ai-behavioral')
    }, [router])

    return (
        <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Redirecting...
        </div>
    )
}
