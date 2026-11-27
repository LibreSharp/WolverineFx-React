import { http, HttpResponse } from 'msw'
import type { RouteObject } from 'react-router'
import { setTokens } from '../../lib/api/authTokens'
import { renderRoutes } from '../../test/render'
import { AuthProvider } from './AuthContext'

// Helpers for the Identity screens' unit tests.

export const testTokens = { accessToken: 'access-1', refreshToken: 'refresh-1', expiresAtUtc: '2030-01-01T00:00:00Z' }

export const signIn = () => setTokens(testTokens)

export function renderWithAuth(routes: RouteObject[], url = '/') {
  return renderRoutes(routes, url, { wrapper: AuthProvider })
}

export function meHandler(me: Partial<{ userId: string; email: string; displayName: string | null; roles: string[] }> = {}) {
  return http.get('*/api/auth/me', () =>
    HttpResponse.json({ userId: 'user-1', email: 'ada@example.com', displayName: null, roles: [], ...me }),
  )
}

export function authOptionsHandler(options: Partial<{ allowRegistration: boolean; externalProviders: { name: string; displayName: string }[] }> = {}) {
  return http.get('*/api/auth/options', () =>
    HttpResponse.json({ allowRegistration: true, externalProviders: [], ...options }),
  )
}

// Pretends the browser supports passkeys (jsdom doesn't): PublicKeyCredential's JSON helpers pass
// options through, and navigator.credentials uses the given create/get. Undo with
// vi.unstubAllGlobals() and removePasskeyBrowser().
export function stubPasskeyBrowser(credentials: { create?: () => Promise<unknown>; get?: () => Promise<unknown> }) {
  Object.assign(globalThis, {
    PublicKeyCredential: {
      parseCreationOptionsFromJSON: (json: unknown) => json,
      parseRequestOptionsFromJSON: (json: unknown) => json,
    },
  })
  Object.defineProperty(navigator, 'credentials', { value: credentials, configurable: true })
}

export function removePasskeyBrowser() {
  delete (globalThis as { PublicKeyCredential?: unknown }).PublicKeyCredential
  delete (navigator as { credentials?: unknown }).credentials
}
