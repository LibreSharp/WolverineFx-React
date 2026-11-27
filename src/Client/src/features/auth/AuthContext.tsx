import { useCallback, useEffect, useRef, useSyncExternalStore, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { getTokens, setTokens, subscribeTokens } from '../../lib/api/authTokens'
import { problemMessage } from '../../lib/api/problemDetails'
import { authOptionsKey } from './authQueries'
import { AuthContext, type LoginRefusal, type LoginResult } from './useAuth'

const signedOut = () => null

export function AuthProvider({ children }: { children: ReactNode }) {
  // Signed out while the build pre-renders public pages, and when the app first starts (tokens
  // are kept in memory only).
  const tokens = useSyncExternalStore(subscribeTokens, getTokens, signedOut)
  const queryClient = useQueryClient()

  // Just signed out (logout, or a refresh token that was rejected): drop the user's cached data,
  // so the next person to log in on this browser never sees the previous user's Todos. Only on
  // a change from signed in, and keeping the public sign-in options, which pages that are still
  // showing (the header, the login page) depend on.
  const wasSignedIn = useRef(tokens !== null)
  useEffect(() => {
    if (wasSignedIn.current && !tokens) {
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== authOptionsKey[0] })
    }
    wasSignedIn.current = tokens !== null
  }, [tokens, queryClient])

  const login = useCallback(async (email: string, password: string): Promise<LoginResult> => {
    const { data, error, response } = await api.POST('/api/auth/login', { body: { email, password } })
    if (!data) {
      if (response.status === 401) return { ok: false, error: 'Invalid email or password' }
      // 403: the password was right, but the account can't log in yet; the API says why.
      const reason = (error as { reason?: LoginRefusal } | undefined)?.reason
      return { ok: false, error: problemMessage(error, 'Login failed'), reason }
    }
    setTokens(data)
    return { ok: true }
  }, [])

  const register = useCallback(async (email: string, password: string) => {
    const { error, response } = await api.POST('/api/auth/register', { body: { email, password } })
    if (!response.ok) {
      return { ok: false as const, error: problemMessage(error, 'Registration failed') }
    }
    return { ok: true as const }
  }, [])

  const logout = useCallback(async () => {
    const current = getTokens()
    if (current) {
      await api.POST('/api/auth/logout', { body: { refreshToken: current.refreshToken } })
    }
    setTokens(null)
  }, [])

  return (
    <AuthContext.Provider value={{ isAuthenticated: tokens !== null, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
