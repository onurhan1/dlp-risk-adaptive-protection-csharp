'use client'

import dynamic from 'next/dynamic'

const UsersTab = dynamic(() => import('@/components/settings/UsersTab'), { ssr: false })

export default function UserManagementPage() {
  return (
    <div style={{ padding: '0' }}>
      <UsersTab />
    </div>
  )
}
