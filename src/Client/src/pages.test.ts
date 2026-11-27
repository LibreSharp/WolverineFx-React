import type { RouteConfigEntry } from '@react-router/dev/routes'
import { describe, expect, it } from 'vitest'
import type { PageHandle } from './components/PageMeta'
import { pages, type Page } from './pages'
import routes from './routes'

const routeModules = import.meta.glob<{ handle?: PageHandle }>('./routes/*.tsx')

// Every page in routes.ts, with whether its route module's handle lets search engines index it.
async function pagesIn(entries: RouteConfigEntry[], parent = ''): Promise<Page[]> {
  const found = await Promise.all(
    entries.map(async (entry) => {
      const path = entry.index ? parent || '/' : entry.path ? `${parent}/${entry.path}`.replace(/\/+/g, '/') : parent
      const own: Page[] = []
      if (entry.index || (entry.path && entry.path !== '*')) {
        const module = await routeModules[`./${entry.file}`]()
        own.push({ path, index: module.handle?.index === true })
      }
      return [...own, ...(await pagesIn(entry.children ?? [], path === '/' ? '' : path))]
    }),
  )
  return found.flat()
}

describe('pages.ts', () => {
  it('lists exactly the pages in routes.ts, with the same index flags', async () => {
    const sort = (list: Page[]) => [...list].sort((a, b) => a.path.localeCompare(b.path))
    expect(sort(pages)).toEqual(sort(await pagesIn(routes)))
  })
})
