import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  use: {
    baseURL: 'https://localhost:5113',
    ignoreHTTPSErrors: true,
  },
})
