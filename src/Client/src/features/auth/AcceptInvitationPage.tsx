import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { api } from '../../lib/api/client'
import { setTokens } from '../../lib/api/authTokens'
import { problemMessage } from '../../lib/api/problemDetails'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { AuthCard } from './AuthCard'

// Opened from the invitation email an admin sent: /accept-invitation?userId=...&email=...&token=...
// Choosing a password finishes the account and signs the person in.
export function AcceptInvitationPage() {
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const mismatch = confirmPassword.length > 0 && confirmPassword !== password

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (mismatch) return
    setSubmitting(true)
    setError(null)
    const { data, error } = await api.POST('/api/auth/accept-invitation', { body: { userId, token, password } })
    setSubmitting(false)
    if (!data) {
      setError(problemMessage(error, 'Could not accept the invitation.'))
      return
    }
    setTokens(data)
    navigate('/todos', { replace: true })
  }

  if (!userId || !token) {
    return (
      <AuthCard title="Accept your invitation">
        <p role="alert" className="text-sm text-destructive">This invitation link is incomplete. Open the link from the email again.</p>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Accept your invitation">
      <form onSubmit={submit} className="flex flex-col gap-3">
        {email && <p className="text-sm text-muted-foreground">Choose a password for {email}.</p>}
        <Input
          type="password"
          required
          autoComplete="new-password"
          placeholder="Password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
        <Input
          type="password"
          required
          autoComplete="new-password"
          placeholder="Confirm password"
          value={confirmPassword}
          onChange={(event) => setConfirmPassword(event.target.value)}
        />
        {mismatch && <p role="alert" className="text-sm text-destructive">The passwords don't match.</p>}
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <Button type="submit" disabled={submitting || mismatch}>
          Create my account
        </Button>
      </form>
    </AuthCard>
  )
}
