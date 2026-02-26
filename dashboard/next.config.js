/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  output: 'standalone',
  // Proxy /api requests to the .NET backend so the browser never talks to port 5001 directly.
  // This eliminates CORS, mixed-content, and firewall issues.
  async rewrites() {
    const API_BACKEND = process.env.API_BACKEND_URL || 'http://127.0.0.1:5001';
    return [
      {
        source: '/api/:path*',
        destination: `${API_BACKEND}/api/:path*`,
      },
    ];
  },
  // Offline mode: Optimize package imports for offline use
  experimental: {
    optimizePackageImports: ['react-plotly.js', 'plotly.js'],
  },
  // Ensure all dependencies are included in standalone build
  outputFileTracingIncludes: {
    '/': ['./node_modules/react-plotly.js/**/*', './node_modules/plotly.js/**/*'],
  },
  // Fix workspace root warning
  turbopack: {
    root: process.cwd(),
  },
}

module.exports = nextConfig
