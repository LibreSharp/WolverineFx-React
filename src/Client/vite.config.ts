/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5133',
        changeOrigin: true,
        // Also proxy the SignalR WebSocket (/api/hub) used for live updates.
        ws: true,
      },
    },
  },
  // Unit tests (npm test): Vitest in a simulated browser (jsdom), with the API faked by MSW.
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
