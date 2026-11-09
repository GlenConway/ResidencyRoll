import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'

const webHost = {
  target: 'https://localhost:5113',
  changeOrigin: true,
  secure: false,
}

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  server: {
    fs: {
      allow: [fileURLToPath(new URL('.', import.meta.url)), fileURLToPath(new URL('../..', import.meta.url))],
    },
    proxy: {
      '/api': webHost,
      '/auth': webHost,
      '/bff': webHost,
      '/mcp': webHost,
      '/.well-known': webHost,
      '/signin-oidc': webHost,
      '/signout-callback-oidc': webHost,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
  },
})
