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

export function subscribeTokens(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
