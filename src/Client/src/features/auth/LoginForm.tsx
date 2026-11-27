import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { api } from '../../lib/api/client'
import { setTokens } from '../../lib/api/authTokens'
import { isCancelled, loginWithPasskey, passkeysSupported } from '../../lib/passkeys'
import { useAuth, type LoginRefusal } from './useAuth'

// The demo login the API creates in Development (see .env.development). Both values are
// undefined in a production build, so nothing is pre-filled there.
const devEmail: string = import.meta.env.DEV ? (import.meta.env.VITE_DEV_LOGIN_EMAIL ?? '') : ''
const devPassword: string = import.meta.env.DEV ? (import.meta.env.VITE_DEV_LOGIN_PASSWORD ?? '') : ''

export function LoginForm({ onSuccess }: { onSuccess?: () => void }) {
  const { login } = useAuth()
  const [email, setEmail] = useState(devEmail)
  const [password, setPassword] = useState(devPassword)
  const [error, setError] = useState<string | null>(null)
  const [refusal, setRefusal] = useState<LoginRefusal | null>(null)
  const [resent, setResent] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    setRefusal(null)
    setResent(false)

    const result = await login(email, password)
    setSubmitting(false)

    if (!result.ok) {
      setError(result.error)
      setRefusal(result.reason ?? null)
      return
    }

    onSuccess?.()
  }

  async function handlePasskey() {
    setError(null)
    setRefusal(null)
    try {
      setTokens(await loginWithPasskey())
      onSuccess?.()
    } catch (e) {
      if (!isCancelled(e)) setError(e instanceof Error ? e.message : 'Passkey login failed')
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-3">
      {devEmail && (
        <p className="text-sm text-muted-foreground">
          Development: pre-filled with the demo user <strong>{devEmail}</strong>.
        </p>
      )}

      <Input
        type="email"
        required
        aria-label="Email"
        placeholder="Email"
        autoComplete="email"
        value={email}
        onChange={(event) => setEmail(event.target.value)}
      />
      <Input
        type="password"
        required
        aria-label="Password"
        placeholder="Password"
        autoComplete="current-password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
      />

      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      {refusal === 'LockedOut' && (
        <Link to="/forgot-password" className="text-sm text-primary hover:underline">
          Reset your password
        </Link>
      )}
      {refusal === 'EmailNotConfirmed' &&
        (resent ? (
          <p role="status" className="text-sm text-emerald-700 dark:text-emerald-300">We've sent a new confirmation link to {email}.</p>
        ) : (
          <Button
            type="button"
            variant="link"
            className="h-auto justify-start px-0"
            onClick={async () => {
              await api.POST('/api/auth/resend-confirmation', { body: { email } })
              setResent(true)
            }}
          >
            Send the confirmation email again
          </Button>
        ))}

      <Button type="submit" disabled={submitting}>
        {submitting ? 'Logging in…' : 'Log in'}
      </Button>

      {passkeysSupported() && (
        <Button type="button" variant="outline" onClick={handlePasskey}>
          Log in with a passkey
        </Button>
      )}
    </form>
  )
}
