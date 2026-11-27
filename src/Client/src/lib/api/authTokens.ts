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

// The organisation an access token is for (its "org" claim), or null without organisations or a
// readable token.
export function organisationIdOf(accessToken: string | null | undefined): string | null {
  if (!accessToken) return null
  try {
    const payload = accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    return (JSON.parse(atob(payload)) as { org?: string }).org ?? null
  } catch {
    return null
  }
}

// The organisation the current access token is for. It changes when the user switches
// organisation, which is when the realtime connection reconnects and cached data is dropped.
let scopeFor: { token: string; organisationId: string | null } | null = null
export function getOrganisationId(): string | null {
  const token = tokens?.accessToken
  if (!token) return null
  if (scopeFor?.token !== token) {
    scopeFor = { token, organisationId: organisationIdOf(token) }
  }
  return scopeFor.organisationId
}

export function subscribeTokens(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
