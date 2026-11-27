import { Link, useLocation } from 'react-router'
import { Notice } from '../../components/Messages'
import { AuthCard } from './AuthCard'
import { useAuthOptions } from './authQueries'
import { ExternalProviderButtons } from './ExternalProviderButtons'
import { LoginForm } from './LoginForm'
import type { AuthLocationState } from './routeGuards'

export function LoginPage() {
  const state = useLocation().state as AuthLocationState | null
  const options = useAuthOptions()

  // After a successful login, GuestOnly redirects to the page the user was going to.
  return (
    <AuthCard
      title="Log in"
      footer={
        <>
          <Link to="/forgot-password" className="text-primary hover:underline">
            Forgot your password?
          </Link>
          {options.data?.allowRegistration !== false && (
            <p>
              No account?{' '}
              <Link to="/register" className="text-primary hover:underline">
                Register
              </Link>
            </p>
          )}
        </>
      }
    >
      {state?.notice && <Notice>{state.notice}</Notice>}
      <LoginForm />
      <ExternalProviderButtons />
    </AuthCard>
  )
}
