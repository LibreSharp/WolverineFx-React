/// <reference types="vitest/config" />
import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { clientRoutes } from './src/routes.ts'

// Writes the app's page paths (src/routes.ts) into the build as client-routes.json. The API reads
// it to answer 404 for any other path, and to list the indexable pages in sitemap.xml.
function clientRoutesFile(): Plugin {
  return {
    name: 'client-routes',
    apply: 'build',
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: 'client-routes.json', source: JSON.stringify(clientRoutes) })
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss(), clientRoutesFile()],
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
