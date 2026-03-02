/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './pages/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
    './app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
      },
      colors: {
        slate: {
          50: '#F8FAFC',
          100: '#F1F5F9',
          200: '#E2E8F0',
          300: '#CBD5E1',
          400: '#94A3B8',
          500: '#64748B',
          600: '#475569',
          700: '#334155',
          800: '#1E293B',
          900: '#0F172A',
          950: '#020617',
        },
        // Semantic colors
        'figma-bg-light': '#F8FAFC',
        'figma-bg-dark': '#0F172A',
        'figma-card-light': '#FFFFFF',
        'figma-card-dark': '#1E293B',
        'figma-text-light': '#0F172A',
        'figma-text-dark': '#F1F5F9',
        'figma-muted-light': '#64748B',
        'figma-muted-dark': '#94A3B8',
        'figma-border-light': '#E2E8F0',
        'figma-border-dark': '#334155',
        // Action colors
        'action-authorized': '#10B981',
        'action-block': '#EF4444',
        'action-quarantine': '#8B5CF6',
        'action-released': '#F59E0B',
      },
      borderRadius: {
        '2xl': '16px',
      },
      boxShadow: {
        'card': '0 4px 20px rgba(15, 23, 42, 0.06)',
        'card-hover': '0 4px 12px -1px rgba(15, 23, 42, 0.08)',
        'dropdown': '0 4px 16px rgba(15, 23, 42, 0.1)',
      },
    },
  },
  plugins: [],
}
