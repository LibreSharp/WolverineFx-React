import { useState } from 'react'
import { useAuth } from './AuthContext'

export function RegisterForm({ onSuccess }: { onSuccess?: () => void }) {
  const { register } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)

    const result = await register(email, password)
    setSubmitting(false)

    if (!result.ok) {
      setError(result.error)
      return
    }

    onSuccess?.()
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto flex max-w-sm flex-col gap-3 p-8">
      <h1 className="text-2xl font-semibold">Create an account</h1>

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

      {error && <p className="text-sm text-red-600">{error}</p>}

      <button
        type="submit"
        disabled={submitting}
        className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        {submitting ? 'Creating account…' : 'Create account'}
      </button>
    </form>
  )
}
