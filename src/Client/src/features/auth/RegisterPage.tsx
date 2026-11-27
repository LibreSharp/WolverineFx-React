import { Link, useNavigate } from 'react-router'
import { useAuthOptions } from './authQueries'
import { RegisterForm } from './RegisterForm'
import type { AuthLocationState } from './routeGuards'

export function RegisterPage() {
  const navigate = useNavigate()
  const options = useAuthOptions()

  if (options.data?.allowRegistration === false) {
    return (
      <div className="mx-auto flex max-w-sm flex-col gap-3 p-8">
        <h1 className="text-2xl font-semibold">Create an account</h1>
        <p className="text-sm text-gray-600">Accounts are by invitation only. Ask an administrator to invite you.</p>
        <Link to="/login" className="text-sm text-indigo-600 hover:underline">
          Log in
        </Link>
      </div>
    )
  }

  return (
    <div>
      <RegisterForm
        onSuccess={(email) =>
          navigate('/login', {
            state: {
              notice: `Account created. We've sent a confirmation link to ${email}: open it, then log in.`,
            } satisfies AuthLocationState,
          })
        }
      />
      <p className="text-center text-sm text-gray-500">
        Already have an account?{' '}
        <Link to="/login" className="text-indigo-600 hover:underline">
          Log in
        </Link>
      </p>
    </div>
  )
}
