import tailwindcss from '@tailwindcss/vite'

// The API the dev server proxies to (`/api` → API). Only used by `nuxt dev`.
const devApiTarget = process.env.INJAZ_DEV_API ?? 'http://localhost:8080'

export default defineNuxtConfig({
  compatibilityDate: '2026-11-01',

  // A static SPA (Cloudflare Pages). All data comes from the API at runtime.
  ssr: false,

  modules: ['@pinia/nuxt', '@nuxtjs/i18n'],

  css: [
    '@fontsource/ibm-plex-sans-arabic/400.css',
    '@fontsource/ibm-plex-sans-arabic/500.css',
    '@fontsource/ibm-plex-sans-arabic/600.css',
    '@fontsource/ibm-plex-sans-arabic/700.css',
    '~/assets/css/main.css',
  ],

  vite: {
    plugins: [tailwindcss()],
  },

  runtimeConfig: {
    public: {
      // Empty = same origin (the dev proxy below, or a reverse proxy that serves both).
      // Production on Cloudflare Pages: NUXT_PUBLIC_API_BASE=https://api.<domain> at build time.
      apiBase: '',
    },
  },

  nitro: {
    devProxy: {
      '/api': { target: `${devApiTarget}/api`, changeOrigin: true },
      '/hubs': { target: `${devApiTarget}/hubs`, changeOrigin: true, ws: true },
    },
  },

  app: {
    head: {
      // Arabic is the default; app.vue switches lang/dir with the active locale.
      htmlAttrs: { lang: 'ar', dir: 'rtl' },
      title: 'إنجاز',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#0f766e' },
        { name: 'color-scheme', content: 'light dark' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
      ],
      link: [
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'manifest', href: '/manifest.webmanifest' },
      ],
    },
  },

  i18n: {
    strategy: 'no_prefix', // the locale is a user preference, not part of the URL
    defaultLocale: 'ar',
    detectBrowserLanguage: false, // Arabic unless the user chose otherwise (see plugins/locale.client.ts)
    locales: [
      { code: 'ar', language: 'ar', dir: 'rtl', name: 'العربية', file: 'ar.json' },
      { code: 'en', language: 'en', dir: 'ltr', name: 'English', file: 'en.json' },
    ],
  },

  typescript: {
    strict: true,
  },
})
