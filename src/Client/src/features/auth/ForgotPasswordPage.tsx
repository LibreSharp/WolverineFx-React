import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

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
    <div className="mx-auto flex max-w-sm flex-col gap-3 p-8">
      <h1 className="text-2xl font-semibold">Reset your password</h1>
      {sent ? (
        // The API answers the same whether or not the email has an account.
        <p role="status" className="text-sm text-gray-700">
          If an account exists for <strong>{email}</strong>, we've sent it a link to reset the
          password. The link expires in an hour.
        </p>
      ) : (
        <form onSubmit={submit} className="flex flex-col gap-3">
          <p className="text-sm text-gray-600">Enter your email and we'll send you a link to choose a new password.</p>
          <input
            type="email"
            required
            placeholder="Email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="rounded border border-gray-300 px-3 py-2"
          />
          {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
          <button
            type="submit"
            disabled={submitting}
            className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            Send reset link
          </button>
        </form>
      )}
      <Link to="/login" className="text-center text-sm text-indigo-600 hover:underline">
        Back to log in
      </Link>
    </div>
  )
}
