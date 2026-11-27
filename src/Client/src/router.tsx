import type { RouteObject } from 'react-router'
import { HomePage } from './components/HomePage'
import { Layout, NavItem } from './components/Layout'
import { NotFound } from './components/NotFound'
import type { PageHandle } from './components/PageMeta'

// Each route's `handle` gives its page title and whether search engines may index it (see
// components/PageMeta.tsx). Pages other than the home page load when first visited, so the
// first page view downloads less JavaScript. When you add a route, add its path to routes.ts too.
export const routes: RouteObject[] = [
  {
    element: <Layout nav={<NavItem to="/todos">Todos</NavItem>} />,
    children: [
      { index: true, element: <HomePage />, handle: { index: true } satisfies PageHandle },
      {
        path: 'todos',
        handle: { title: 'Todos' } satisfies PageHandle,
        lazy: async () => ({ Component: (await import('./features/todos/TodoList')).TodoList }),
      },
      { path: '*', element: <NotFound />, handle: { title: 'Page not found' } satisfies PageHandle },
    ],
  },
]

