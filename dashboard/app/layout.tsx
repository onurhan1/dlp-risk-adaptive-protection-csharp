import './globals.css'
import type { Metadata } from 'next'
import { Inter } from 'next/font/google'
import { Providers } from '@/components/Providers'
import AuthLayoutClient from '@/components/AuthLayoutClient'

const inter = Inter({ subsets: ['latin'] })

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
      <body className={inter.className}>
        <Providers>
          <AuthLayoutClient>
            {children}
          </AuthLayoutClient>
        </Providers>
      </body>
    </html>
  )
}
