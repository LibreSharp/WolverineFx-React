import { writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import type { Config } from '@react-router/dev/config'
import { pages } from './src/pages.ts'

// React Router's framework mode, as a single-page app (no rendering server): the build writes
// real HTML for the public pages (`index: true` in src/pages.ts), which the API serves as static
// files and a CDN can cache, and a fallback page that starts the app for every other page.
export default {
  appDirectory: 'src',
  buildDirectory: 'dist',
  ssr: false,
  prerender: pages.filter((page) => page.index).map((page) => page.path),
  // The API reads the list of pages (client-routes.json) to answer 404 for any other path, and to
  // list the public ones in sitemap.xml.
  async buildEnd({ reactRouterConfig }) {
    await writeFile(join(reactRouterConfig.buildDirectory, 'client', 'client-routes.json'), JSON.stringify(pages))
  },
} satisfies Config
