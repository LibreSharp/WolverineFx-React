import type { RouteObject } from 'react-router'
import { describe, expect, it } from 'vitest'
import { routes } from './router'
import type { PageHandle } from './components/PageMeta'
import { clientRoutes } from './routes'

// Every page in the router, with whether its handle lets search engines index it.
function pages(list: RouteObject[], parent = ''): { path: string; index: boolean }[] {
  return list.flatMap((route) => {
    const path = route.index ? parent || '/' : route.path ? `${parent}/${route.path}`.replace(/\/+/g, '/') : parent
    const own = route.index || (route.path && route.path !== '*')
      ? [{ path, index: (route.handle as PageHandle | undefined)?.index === true }]
      : []
    return [...own, ...pages(route.children ?? [], path === '/' ? '' : path)]
  })
}

describe('routes.ts', () => {
  it('lists exactly the pages in App.tsx, with the same index flags', () => {
    const sort = (list: { path: string; index: boolean }[]) => [...list].sort((a, b) => a.path.localeCompare(b.path))
    expect(sort(clientRoutes)).toEqual(sort(pages(routes)))
  })
})
