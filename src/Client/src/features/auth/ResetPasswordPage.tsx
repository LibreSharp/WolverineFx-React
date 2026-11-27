import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'
import type { AuthLocationState } from './routeGuards'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { AuthCard } from './AuthCard'

// Opened from the link in the password reset email: /reset-password?email=...&token=...
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const mismatch = confirmPassword.length > 0 && confirmPassword !== newPassword

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (mismatch) return
    setSubmitting(true)
    setError(null)
    const { error, response } = await api.POST('/api/auth/reset-password', { body: { email, token, newPassword } })
    setSubmitting(false)
    if (!response.ok) {
      setError(problemMessage(error, 'Could not reset your password.'))
      return
    }
    navigate('/login', {
      replace: true,
      state: { notice: 'Your password has been reset. Log in with your new password.' } satisfies AuthLocationState,
    })
  }

  if (!email || !token) {
    return (
      <AuthCard title="Reset your password">
        <p role="alert" className="text-sm text-destructive">This reset link is incomplete. Open the link from the email again.</p>
        <Link to="/forgot-password" className="text-sm text-primary hover:underline">Request a new link</Link>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Choose a new password">
      <form onSubmit={submit} className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">For {email}</p>
        <Input
          type="password"
          required
          autoComplete="new-password"
          placeholder="New password"
          value={newPassword}
          onChange={(event) => setNewPassword(event.target.value)}
        />
        <Input
          type="password"
          required
          autoComplete="new-password"
          placeholder="Confirm new password"
          value={confirmPassword}
          onChange={(event) => setConfirmPassword(event.target.value)}
        />
        {mismatch && <p role="alert" className="text-sm text-destructive">The passwords don't match.</p>}
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <Button type="submit" disabled={submitting || mismatch}>
          Set new password
        </Button>
        {error && (
          <Link to="/forgot-password" className="text-center text-sm text-primary hover:underline">
            Request a new link
          </Link>
        )}
      </form>
    </AuthCard>
  )
}
