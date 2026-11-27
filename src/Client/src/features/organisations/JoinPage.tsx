import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import type { AuthLocationState } from '../auth/routeGuards'
import { useAuth } from '../auth/useAuth'
import { useAcceptInvitation, useInvitation, useJoinWithNewAccount } from './organisationQueries'

// /join?token=...: the link in an invitation email. Signed in, accept it; with no account yet,
// choose a password to create one (the link proves the email); with an account, log in first and
// come back here.
export function JoinPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const invitation = useInvitation(token)
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  return (
    <div className="mx-auto max-w-md px-6 py-10">
      <h1 className="mb-4 text-2xl font-semibold">Join an organisation</h1>
      {invitation.isPending && token !== '' && <p className="text-gray-500">Loading…</p>}
      {(token === '' || invitation.data === null) && (
        <p role="alert" className="text-red-600">This invitation link is invalid or has expired. Ask for a new one.</p>
      )}
      {invitation.isError && <p role="alert" className="text-red-600">{invitation.error.message}</p>}
      {invitation.data && (
        <>
          <p className="mb-6 text-gray-700">
            You're invited to join <strong>{invitation.data.organisationName}</strong> as {invitation.data.role.toLowerCase()} ({invitation.data.email}).
          </p>
          {isAuthenticated ? (
            <Accept token={token} />
          ) : invitation.data.accountExists ? (
            <Link
              to="/login"
              state={{ from: location } satisfies AuthLocationState}
              className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700"
            >
              Log in to accept
            </Link>
          ) : (
            <CreateAccount token={token} />
          )}
        </>
      )}
    </div>
  )
}

function Accept({ token }: { token: string }) {
  const accept = useAcceptInvitation()
  const navigate = useNavigate()
  return (
    <>
      <button
        type="button"
        disabled={accept.isPending}
        onClick={() => accept.mutate(token, { onSuccess: () => void navigate('/todos') })}
        className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        Accept and join
      </button>
      {accept.error && <p role="alert" className="mt-3 text-sm text-red-600">{accept.error.message}</p>}
    </>
  )
}

function CreateAccount({ token }: { token: string }) {
  const join = useJoinWithNewAccount()
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  return (
    <form
      onSubmit={async (e: FormEvent) => {
        e.preventDefault()
        // Awaited rather than onSuccess: signing in replaces this form before that would run.
        try {
          await join.mutateAsync({ token, password })
          await navigate('/todos')
        } catch {
          // Shown from join.error.
        }
      }}
      className="space-y-4"
    >
      <label className="block text-sm">
        <span className="mb-1 block text-gray-700">Choose a password</span>
        <input type="password" required autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} className="w-full rounded border border-gray-300 px-3 py-2" />
      </label>
      <button type="submit" disabled={join.isPending} className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">
        Create account and join
      </button>
      {join.error && <p role="alert" className="text-sm text-red-600">{join.error.message}</p>}
    </form>
  )
}
