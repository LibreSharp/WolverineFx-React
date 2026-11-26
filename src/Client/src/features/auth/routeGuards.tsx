import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './useAuth'

// What the login page remembers: where to go after logging in, and whether the user just
// registered.
export interface AuthLocationState {
  from?: { pathname: string }
  registered?: boolean
}

// Layout route for pages that need a login: sends signed-out users to /login, remembering
// where they were going. Also runs on logout, which therefore lands on /login.
export function RequireAuth() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location } satisfies AuthLocationState} />
  }
  return <Outlet />
}

// Layout route for /login and /register: once signed in, continue to the page the user was
// going to (or the home page).
export function GuestOnly() {
  const { isAuthenticated } = useAuth()
  const state = useLocation().state as AuthLocationState | null
  if (isAuthenticated) {
    return <Navigate to={state?.from?.pathname ?? '/'} replace />
  }
  return <Outlet />
}
