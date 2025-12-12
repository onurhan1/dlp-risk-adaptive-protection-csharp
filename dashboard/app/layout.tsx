import './globals.css'
import type { Metadata } from 'next'
import { Providers } from '@/components/Providers'
import AuthLayoutClient from '@/components/AuthLayoutClient'

export const metadata: Metadata = {
  title: 'Forcepoint RAP - Risk Adaptive Protection',
  description: 'Real-time DLP risk analysis and reporting dashboard',
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <body>
        <Providers>
          <AuthLayoutClient>
            {children}
          </AuthLayoutClient>
        </Providers>
      </body>
    </html>
  )
}
