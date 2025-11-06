/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './pages/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
    './app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        'forcepoint-green': '#14b8a6',
        'forcepoint-green-dark': '#0d9488',
        'forcepoint-green-light': '#5eead4',
      },
    },
  },
  plugins: [],
}

