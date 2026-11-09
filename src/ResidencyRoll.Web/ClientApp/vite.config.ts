import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

const webHost = {
  target: 'https://localhost:5113',
  changeOrigin: true,
  secure: false,
}

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': webHost,
      '/auth': webHost,
      '/bff': webHost,
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
