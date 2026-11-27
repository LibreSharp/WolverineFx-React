import { useCallback, useEffect, useSyncExternalStore, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { getTokens, setTokens, subscribeTokens } from '../../lib/api/authTokens'
import { problemMessage } from '../../lib/api/problemDetails'
import { AuthContext, type LoginRefusal, type LoginResult } from './useAuth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const tokens = useSyncExternalStore(subscribeTokens, getTokens)
  const queryClient = useQueryClient()

  // Signed out (logout, or a refresh token that was rejected): drop all cached data, so the
  // next person to log in on this browser never sees the previous user's Todos.
  useEffect(() => {
    if (!tokens) queryClient.clear()
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
