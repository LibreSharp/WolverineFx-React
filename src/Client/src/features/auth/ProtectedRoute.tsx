import type { ReactNode } from 'react'
import { useAuth } from './useAuth'

// Not tied to a routing library (none is included in this template) - just a conditional
// render guard for the login/register/protected-content demo.
export function ProtectedRoute({ children, fallback }: { children: ReactNode; fallback: ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <>{children}</> : <>{fallback}</>
}
