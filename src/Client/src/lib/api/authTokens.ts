export interface AuthTokens {
  accessToken: string
  refreshToken: string
  expiresAtUtc: string
}

// Held only in memory (module state, not sessionStorage/localStorage) so nothing survives a hard
// page reload and nothing is readable by an injected script via Web Storage. Trade-off: a reload
// forces re-login. See README for the sessionStorage alternative if reload-survival matters more
// than that XSS exposure.
let tokens: AuthTokens | null = null
const listeners = new Set<() => void>()

export function getTokens(): AuthTokens | null {
  return tokens
}

export function setTokens(next: AuthTokens | null): void {
  tokens = next
  listeners.forEach((listener) => listener())
}

// The organisation the access token is for (its "org" claim), or null without organisations or
// tokens. It changes when the user switches organisation, which is when the realtime connection
// reconnects and cached data is dropped.
let scopeFor: { token: string; organisationId: string | null } | null = null
export function getOrganisationId(): string | null {
  const token = tokens?.accessToken
  if (!token) return null
  if (scopeFor?.token !== token) {
    let organisationId: string | null = null
    try {
      const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
      organisationId = (JSON.parse(atob(payload)) as { org?: string }).org ?? null
    } catch {
      organisationId = null
    }
    scopeFor = { token, organisationId }
  }
  return scopeFor.organisationId
}

export function subscribeTokens(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
