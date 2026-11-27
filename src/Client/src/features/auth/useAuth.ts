import { createContext, useContext } from 'react'

// Why the API refused a login with 403 (see LoginProblems in the API).
export type LoginRefusal = 'EmailNotConfirmed' | 'LockedOut'

export type LoginResult = { ok: true } | { ok: false; error: string; reason?: LoginRefusal }

export interface AuthContextValue {
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<LoginResult>
  register: (email: string, password: string) => Promise<{ ok: true } | { ok: false; error: string }>
  logout: () => Promise<void>
}

// Kept out of AuthContext.tsx so that file exports only a component (React fast refresh).
export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
