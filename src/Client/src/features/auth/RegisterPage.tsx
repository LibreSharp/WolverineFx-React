import { Link, useNavigate } from 'react-router'
import { RegisterForm } from './RegisterForm'
import type { AuthLocationState } from './routeGuards'

export function RegisterPage() {
  const navigate = useNavigate()

  return (
    <div>
      <RegisterForm
        onSuccess={() => navigate('/login', { state: { registered: true } satisfies AuthLocationState })}
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
