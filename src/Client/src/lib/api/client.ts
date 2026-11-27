import createClient from 'openapi-fetch'
import type { paths } from './schema'
import { getTokens, setTokens } from './authTokens'

// The page's own origin: the same as a relative URL in the browser, and resolvable in tests.
// (Empty when the build pre-renders a page in Node, which makes no requests.)
// fetch is looked up on each call (openapi-fetch would otherwise keep the one that existed when
// this module loaded), so the unit tests' fake API (MSW) sees the requests.
export const api = createClient<paths>({
  baseUrl: typeof window === 'undefined' ? '' : window.location.origin,
  fetch: (request) => globalThis.fetch(request),
})

let refreshPromise: Promise<boolean> | null = null

async function refreshTokens(): Promise<boolean> {
  const current = getTokens()
  if (!current) return false

  if (!refreshPromise) {
    refreshPromise = api
      .POST('/api/auth/refresh', { body: { refreshToken: current.refreshToken } })
      .then(({ data }) => {
        if (!data) {
          setTokens(null)
          return false
        }
        setTokens(data)
        return true
      })
      .catch(() => {
        setTokens(null)
        return false
      })
      .finally(() => {
        refreshPromise = null
      })
  }

  return refreshPromise
}

api.use({
  onRequest({ request }) {
    const tokens = getTokens()
    if (tokens) {
      request.headers.set('Authorization', `Bearer ${tokens.accessToken}`)
    }
    return request
  },
  async onResponse({ request, response }) {
    // A refused refresh token must not trigger another refresh: that would wait on itself.
    const isRefresh = new URL(request.url).pathname === '/api/auth/refresh'
    if (response.status === 401 && !isRefresh && !request.headers.has('X-Retried-After-Refresh')) {
      const refreshed = await refreshTokens()
      if (refreshed) {
        const retryRequest = new Request(request)
        retryRequest.headers.set('X-Retried-After-Refresh', '1')
        const tokens = getTokens()
        if (tokens) {
          retryRequest.headers.set('Authorization', `Bearer ${tokens.accessToken}`)
        }
        return fetch(retryRequest)
      }
    }
    return response
  },
})
