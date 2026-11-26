import { Link, useLocation } from 'react-router'
import { LoginForm } from './LoginForm'
import type { AuthLocationState } from './routeGuards'

export function LoginPage() {
  const state = useLocation().state as AuthLocationState | null

  // After a successful login, GuestOnly redirects to the page the user was going to.
  return (
    <div>
      {state?.notice && (
        <p role="status" className="mx-auto max-w-sm px-8 pt-8 text-sm text-green-700">
          {state.notice}
        </p>
      )}
      <LoginForm />
      <p className="text-center text-sm text-gray-500">
        <Link to="/forgot-password" className="text-indigo-600 hover:underline">
          Forgot your password?
        </Link>
      </p>
      <p className="mt-2 text-center text-sm text-gray-500">
        No account?{' '}
        <Link to="/register" className="text-indigo-600 hover:underline">
          Register
        </Link>
      </p>
    </div>
  )
}
