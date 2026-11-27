import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { api } from '../../lib/api/client'
import { setTokens } from '../../lib/api/authTokens'
import { problemMessage } from '../../lib/api/problemDetails'

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
      <div className="mx-auto flex max-w-sm flex-col gap-3 p-8">
        <h1 className="text-2xl font-semibold">Accept your invitation</h1>
        <p role="alert" className="text-sm text-red-600">This invitation link is incomplete. Open the link from the email again.</p>
      </div>
    )
  }

  return (
    <form onSubmit={submit} className="mx-auto flex max-w-sm flex-col gap-3 p-8">
      <h1 className="text-2xl font-semibold">Accept your invitation</h1>
      {email && <p className="text-sm text-gray-600">Choose a password for {email}.</p>}
      <input
        type="password"
        required
        autoComplete="new-password"
        placeholder="Password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        className="rounded border border-gray-300 px-3 py-2"
      />
      <input
        type="password"
        required
        autoComplete="new-password"
        placeholder="Confirm password"
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
        Create my account
      </button>
    </form>
  )
}
