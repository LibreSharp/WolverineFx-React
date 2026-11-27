import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { AuthCard } from './AuthCard'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    const { error, response } = await api.POST('/api/auth/forgot-password', { body: { email } })
    setSubmitting(false)
    if (!response.ok) {
      setError(problemMessage(error, 'Something went wrong. Try again.'))
      return
    }
    setSent(true)
  }

  return (
    <AuthCard title="Reset your password">
      {sent ? (
        // The API answers the same whether or not the email has an account.
        <p role="status" className="text-sm text-muted-foreground">
          If an account exists for <strong>{email}</strong>, we've sent it a link to reset the
          password. The link expires in an hour.
        </p>
      ) : (
        <form onSubmit={submit} className="flex flex-col gap-3">
          <p className="text-sm text-muted-foreground">Enter your email and we'll send you a link to choose a new password.</p>
          <Input
            type="email"
            required
            placeholder="Email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
          {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
          <Button type="submit" disabled={submitting}>
            Send reset link
          </Button>
        </form>
      )}
      <Link to="/login" className="text-center text-sm text-primary hover:underline">
        Back to log in
      </Link>
    </AuthCard>
  )
}
