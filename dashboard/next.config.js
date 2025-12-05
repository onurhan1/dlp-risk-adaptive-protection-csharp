/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  output: 'standalone',
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001',
  },
  // Offline mode: Optimize package imports for offline use
  experimental: {
    optimizePackageImports: ['react-plotly.js', 'plotly.js'],
  },
}

module.exports = nextConfig
