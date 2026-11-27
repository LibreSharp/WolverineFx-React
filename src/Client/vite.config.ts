/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import { reactRouter } from '@react-router/dev/vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  // React Router's plugin builds the app (react-router.config.ts); the unit tests render
  // components on their own, with the plain React plugin.
  plugins: [tailwindcss(), process.env.VITEST ? react() : reactRouter()],
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
