import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import type { AuthLocationState } from '../auth/routeGuards'
import { useAuth } from '../auth/useAuth'
import { useAcceptInvitation, useInvitation, useJoinWithNewAccount } from './organisationQueries'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { AuthCard } from '../auth/AuthCard'

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
    <AuthCard title="Join an organisation">
      {invitation.isPending && token !== '' && <p className="text-muted-foreground">Loading…</p>}
      {(token === '' || invitation.data === null) && (
        <p role="alert" className="text-destructive">This invitation link is invalid or has expired. Ask for a new one.</p>
      )}
      {invitation.isError && <p role="alert" className="text-destructive">{invitation.error.message}</p>}
      {invitation.data && (
        <>
          <p className="mb-6 text-muted-foreground">
            You're invited to join <strong>{invitation.data.organisationName}</strong> as {invitation.data.role.toLowerCase()} ({invitation.data.email}).
          </p>
          {isAuthenticated ? (
            <Accept token={token} />
          ) : invitation.data.accountExists ? (
            <Button asChild>
              <Link to="/login" state={{ from: location } satisfies AuthLocationState}>
                Log in to accept
              </Link>
            </Button>
          ) : (
            <CreateAccount token={token} />
          )}
        </>
      )}
    </AuthCard>
  )
}

function Accept({ token }: { token: string }) {
  const accept = useAcceptInvitation()
  const navigate = useNavigate()
  return (
    <>
      <Button
        type="button"
        disabled={accept.isPending}
        onClick={() => accept.mutate(token, { onSuccess: () => void navigate('/todos') })}
      >
        Accept and join
      </Button>
      {accept.error && <p role="alert" className="mt-3 text-sm text-destructive">{accept.error.message}</p>}
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
        <span className="mb-1 block text-muted-foreground">Choose a password</span>
        <Input type="password" required autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </label>
      <Button type="submit" disabled={join.isPending}>
        Create account and join
      </Button>
      {join.error && <p role="alert" className="text-sm text-destructive">{join.error.message}</p>}
    </form>
  )
}
