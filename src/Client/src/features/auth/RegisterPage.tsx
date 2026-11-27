import { Link, useNavigate } from 'react-router'
import { AuthCard } from './AuthCard'
import { useAuthOptions } from './authQueries'
import { ExternalProviderButtons } from './ExternalProviderButtons'
import { RegisterForm } from './RegisterForm'
import type { AuthLocationState } from './routeGuards'

export function RegisterPage() {
  const navigate = useNavigate()
  const options = useAuthOptions()

  if (options.data?.allowRegistration === false) {
    return (
      <AuthCard
        title="Create an account"
        footer={
          <Link to="/login" className="text-primary hover:underline">
            Log in
          </Link>
        }
      >
        <p className="text-sm text-muted-foreground">Accounts are by invitation only. Ask an administrator to invite you.</p>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      title="Create an account"
      footer={
        <p>
          Already have an account?{' '}
          <Link to="/login" className="text-primary hover:underline">
            Log in
          </Link>
        </p>
      }
    >
      <RegisterForm
        onSuccess={(email) =>
          navigate('/login', {
            state: {
              notice: `Account created. We've sent a confirmation link to ${email}: open it, then log in.`,
            } satisfies AuthLocationState,
          })
        }
      />
      <ExternalProviderButtons />
    </AuthCard>
  )
}
