import createClient from 'openapi-fetch'
import type { paths } from './schema'
import { getTokens, setTokens } from './authTokens'

export const api = createClient<paths>({ baseUrl: '/' })

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
    if (response.status === 401 && !request.headers.has('X-Retried-After-Refresh')) {
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
