import { useCallback, useEffect, useSyncExternalStore, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { getTokens, setTokens, subscribeTokens } from '../../lib/api/authTokens'
import { AuthContext } from './useAuth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const tokens = useSyncExternalStore(subscribeTokens, getTokens)
  const queryClient = useQueryClient()

  // Signed out (logout, or a refresh token that was rejected): drop all cached data, so the
  // next person to log in on this browser never sees the previous user's Todos.
  useEffect(() => {
    if (!tokens) queryClient.clear()
  }, [tokens, queryClient])

  const login = useCallback(async (email: string, password: string) => {
    const { data, response } = await api.POST('/api/auth/login', { body: { email, password } })
    if (!data) {
      return { ok: false as const, error: response.status === 401 ? 'Invalid email or password' : 'Login failed' }
    }
    setTokens(data)
    return { ok: true as const }
  }, [])

  const register = useCallback(async (email: string, password: string) => {
    const { response } = await api.POST('/api/auth/register', { body: { email, password } })
    if (!response.ok) {
      return { ok: false as const, error: 'Registration failed' }
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
