import { useState } from 'react'
import { api } from '../../lib/api/client'
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

  return (
    <form onSubmit={handleSubmit} className="mx-auto flex max-w-sm flex-col gap-3 p-8">
      <h1 className="text-2xl font-semibold">Log in</h1>

      {devEmail && (
        <p className="text-sm text-gray-500">
          Development: pre-filled with the demo user <strong>{devEmail}</strong>.
        </p>
      )}

      <input
        type="email"
        required
        placeholder="Email"
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        className="rounded border border-gray-300 px-3 py-2"
      />
      <input
        type="password"
        required
        placeholder="Password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        className="rounded border border-gray-300 px-3 py-2"
      />

      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      {refusal === 'EmailNotConfirmed' &&
        (resent ? (
          <p role="status" className="text-sm text-green-700">We've sent a new confirmation link to {email}.</p>
        ) : (
          <button
            type="button"
            onClick={async () => {
              await api.POST('/api/auth/resend-confirmation', { body: { email } })
              setResent(true)
            }}
            className="text-left text-sm text-indigo-600 hover:underline"
          >
            Send the confirmation email again
          </button>
        ))}

      <button
        type="submit"
        disabled={submitting}
        className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        {submitting ? 'Logging in…' : 'Log in'}
      </button>
    </form>
  )
}
