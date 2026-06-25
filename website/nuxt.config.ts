import { resolve } from 'node:path'

export default defineNuxtConfig({
  compatibilityDate: '2024-12-01',
  devtools: { enabled: false },

  experimental: {
    appManifest: false,
    checkOutdatedBuildInterval: false,
  },

  modules: [
    '@nuxtjs/google-fonts',
  ],

  css: [
    '~/assets/css/main.css',
  ],

  postcss: {
    plugins: {
      'tailwindcss/nesting': {},
      tailwindcss: {
        config: './tailwind.config.cjs',
      },
      autoprefixer: {},
    },
  },

  googleFonts: {
    families: {
      'Noto Sans SC': [400, 500, 600, 700],
    },
    display: 'swap',
  },

  runtimeConfig: {
    adminUsername: '',
    adminPassword: '',
    adminToken: '',
    adminSessionSecret: '',
    adminSessionSecure: false,
    adminSessionMaxAge: 60 * 60 * 12,
    announcementDataFile: resolve('./data/announcements.json'),
    releaseDataFile: resolve('./data/releases.json'),
    public: {
      downloadUrl: '/downloads/LuaShengIME.apk',
      groupChatUrl: 'https://qm.qq.com/q/wGNYz3qppg',
    },
  },

  app: {
    head: {
      htmlAttrs: { lang: 'zh-CN' },
      title: 'LuaShengIME | 中文输入更快更准的输入法',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        {
          name: 'description',
          content: 'LuaShengIME 是面向安卓的中文输入法，内置 Rime 引擎，候选精准、词库可管理，提升输入效率。',
        },
        {
          name: 'keywords',
          content: '中文输入法,Rime,安卓输入法,拼音输入,词库管理,LuaShengIME',
        },
      ],
      link: [
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
      ],
    },
  },
})
