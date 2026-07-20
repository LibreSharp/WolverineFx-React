import { createContext, useCallback, useContext, useSyncExternalStore, type ReactNode } from 'react'
import { api } from '../../lib/api/client'
import { getTokens, setTokens, subscribeTokens } from '../../lib/api/authTokens'

interface AuthContextValue {
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<{ ok: true } | { ok: false; error: string }>
  register: (email: string, password: string) => Promise<{ ok: true } | { ok: false; error: string }>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const tokens = useSyncExternalStore(subscribeTokens, getTokens)

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

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
