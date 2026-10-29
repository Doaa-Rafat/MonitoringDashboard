/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // The HTTPS port is in src/WebAPI/Properties/launchSettings.json. Override it in client/.env.local.
  const target = env.API_TARGET || 'https://localhost:7000'

  return {
    plugins: [react(), tailwindcss()],
    resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
    server: {
      port: 5173,
      proxy: {
        '/api': { target, changeOrigin: true, secure: false },
        '/hubs': { target, changeOrigin: true, secure: false, ws: true },
        '/hangfire': { target, changeOrigin: true, secure: false },
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: './src/test/setup.ts',
      css: false,
    },
  }
})