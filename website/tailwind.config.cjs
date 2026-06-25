/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './components/**/*.vue',
    './pages/**/*.vue',
    './app.vue',
  ],
  theme: {
    extend: {
      colors: {
        primary: '#0D9488',
        secondary: '#14B8A6',
        cta: '#F97316',
        'cta-hover': '#EA580C',
        surface: '#F0FDFA',
        body: '#134E4A',
      },
      fontFamily: {
        sans: ['"Noto Sans SC"', 'system-ui', 'sans-serif'],
      },
      transitionDuration: {
        DEFAULT: '150ms',
      },
    },
  },
  plugins: [],
}
