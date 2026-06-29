'use client'

import { useEffect, useRef } from 'react'
import { usePathname } from 'next/navigation'
import { useAuth } from './AuthProvider'
import apiClient from '@/lib/axios'

export function ActivityTracker() {
  const pathname = usePathname()
  const { username } = useAuth()
  const currentActivityId = useRef<number | null>(null)
  const lastPath = useRef<string | null>(null)

  useEffect(() => {
    if (!username || !pathname) return

    // Prevent duplicate triggers for the same path
    if (lastPath.current === pathname) return
    lastPath.current = pathname

    // If there was a previous activity, log its departure
    if (currentActivityId.current) {
      apiClient.post('/api/activity/page-leave', {
        activityId: currentActivityId.current,
        durationSeconds: 0 // Will be handled accurately on server-side using current time if needed, 
                           // or frontend could track Date.now() differences. Let's just do a ping for now.
      }).catch(console.error)
    }

    // Determine page title
    let pageTitle = document.title
    if (pathname === '/') pageTitle = 'Dashboard'
    else if (pathname.includes('/exceptions')) pageTitle = 'Exceptions'
    else if (pathname.includes('/settings')) pageTitle = 'Settings'
    // etc...

    // Log the new page visit
    apiClient.post('/api/activity/track', {
      userName: username,
      authSource: 'Local', // Can be dynamic when LDAP is added
      activityType: 'PageVisit',
      pagePath: pathname,
      pageTitle: pageTitle,
      actionDetail: `Navigated to ${pathname}`
    }).then(response => {
      if (response.data && response.data.id) {
        currentActivityId.current = response.data.id
      }
    }).catch(console.error)

    const handleBeforeUnload = () => {
      if (currentActivityId.current) {
        // Use keepalive for page unloads or Beacon API
        navigator.sendBeacon('/api/activity/page-leave', JSON.stringify({
          activityId: currentActivityId.current,
          durationSeconds: 0
        }))
      }
    }

    window.addEventListener('beforeunload', handleBeforeUnload)

    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload)
    }
  }, [pathname, username])

  return null // Invisible component
}
