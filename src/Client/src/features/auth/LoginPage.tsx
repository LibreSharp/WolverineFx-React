import { Link, useLocation } from 'react-router'
import { LoginForm } from './LoginForm'
import type { AuthLocationState } from './routeGuards'

export function LoginPage() {
  const state = useLocation().state as AuthLocationState | null

  // After a successful login, GuestOnly redirects to the page the user was going to.
  return (
    <div>
      {state?.registered && (
        <p role="status" className="mx-auto max-w-sm px-8 pt-8 text-sm text-green-700">
          Account created. Log in to continue.
        </p>
      )}
      <LoginForm />
      <p className="text-center text-sm text-gray-500">
        No account?{' '}
        <Link to="/register" className="text-indigo-600 hover:underline">
          Register
        </Link>
      </p>
    </div>
  )
}
