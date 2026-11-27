import { Link, useNavigate } from 'react-router'
import { RegisterForm } from './RegisterForm'
import type { AuthLocationState } from './routeGuards'

export function RegisterPage() {
  const navigate = useNavigate()

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
