import type { ReactNode } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'

// Renders routes at a URL, with a fresh query cache (no retries, so errors show at once).
// Returns the router too, to check where a page navigated: router.state.location.
export function renderRoutes(
  routes: RouteObject[],
  url = '/',
  { wrapper: Wrapper }: { wrapper?: (props: { children: ReactNode }) => ReactNode } = {},
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [url] })
  const content = <RouterProvider router={router} />
  const result = render(
    <QueryClientProvider client={queryClient}>{Wrapper ? <Wrapper>{content}</Wrapper> : content}</QueryClientProvider>,
  )
  return { ...result, router, queryClient }
}

// A single page at a URL, plus a stand-in for each page it may navigate to.
export function renderPage(element: ReactNode, url = '/', otherPaths: string[] = []) {
  return renderRoutes(
    [
      { path: new URL(url, 'http://localhost').pathname, element },
      ...otherPaths.map((path) => ({ path, element: <p>Page {path}</p> })),
    ],
    url,
  )
}
