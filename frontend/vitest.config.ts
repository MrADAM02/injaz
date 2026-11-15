import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

// Unit tests for framework-free modules (app/utils, i18n). They run in Node without booting Nuxt.
export default defineConfig({
  resolve: {
    alias: {
      '~': fileURLToPath(new URL('./app', import.meta.url)),
    },
  },
  test: {
    include: ['tests/**/*.test.ts'],
    environment: 'node',
  },
})
