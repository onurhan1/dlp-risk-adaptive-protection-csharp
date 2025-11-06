import './globals.css'
import type { Metadata } from 'next'
import { Inter } from 'next/font/google'
import { AuthProvider } from '@/components/AuthProvider'
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
        <AuthProvider>
          <AuthLayoutClient>
            {children}
          </AuthLayoutClient>
        </AuthProvider>
      </body>
    </html>
  )
}
