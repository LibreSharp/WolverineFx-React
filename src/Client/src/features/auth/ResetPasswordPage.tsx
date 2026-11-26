import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'
import type { AuthLocationState } from './routeGuards'

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
      <div className="mx-auto flex max-w-sm flex-col gap-3 p-8">
        <h1 className="text-2xl font-semibold">Reset your password</h1>
        <p role="alert" className="text-sm text-red-600">This reset link is incomplete. Open the link from the email again.</p>
        <Link to="/forgot-password" className="text-sm text-indigo-600 hover:underline">Request a new link</Link>
      </div>
    )
  }

  return (
    <form onSubmit={submit} className="mx-auto flex max-w-sm flex-col gap-3 p-8">
      <h1 className="text-2xl font-semibold">Choose a new password</h1>
      <p className="text-sm text-gray-600">For {email}</p>
      <input
        type="password"
        required
        autoComplete="new-password"
        placeholder="New password"
        value={newPassword}
        onChange={(event) => setNewPassword(event.target.value)}
        className="rounded border border-gray-300 px-3 py-2"
      />
      <input
        type="password"
        required
        autoComplete="new-password"
        placeholder="Confirm new password"
        value={confirmPassword}
        onChange={(event) => setConfirmPassword(event.target.value)}
        className="rounded border border-gray-300 px-3 py-2"
      />
      {mismatch && <p role="alert" className="text-sm text-red-600">The passwords don't match.</p>}
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      <button
        type="submit"
        disabled={submitting || mismatch}
        className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        Set new password
      </button>
      {error && (
        <Link to="/forgot-password" className="text-center text-sm text-indigo-600 hover:underline">
          Request a new link
        </Link>
      )}
    </form>
  )
}
