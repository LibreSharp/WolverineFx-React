import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'
import { accountKeys } from '../account/accountQueries'
import { useAuth } from './useAuth'

type Status = { state: 'working' } | { state: 'done' } | { state: 'failed'; error: string }

// Sends the link's token to the API once, when the page opens. (A ref, not state, guards it: in
// development React runs effects twice, and an email change token works only once.)
function useConfirmOnce(confirm: () => Promise<{ ok: boolean; error?: unknown }>, fallback: string): Status {
  const [status, setStatus] = useState<Status>({ state: 'working' })
  const started = useRef(false)
  useEffect(() => {
    if (started.current) return
    started.current = true
    void confirm().then(({ ok, error }) =>
      setStatus(ok ? { state: 'done' } : { state: 'failed', error: problemMessage(error, fallback) }),
    )
  }, [confirm, fallback])
  return status
}

function ConfirmationResult({ title, status, success }: { title: string; status: Status; success: ReactNode }) {
  return (
    <div className="mx-auto flex max-w-sm flex-col gap-3 p-8">
      <h1 className="text-2xl font-semibold">{title}</h1>
      {status.state === 'working' && <p className="text-gray-500">Confirming…</p>}
      {status.state === 'done' && success}
      {status.state === 'failed' && (
        <p role="alert" className="text-sm text-red-600">
          {status.error}
        </p>
      )}
    </div>
  )
}

// Opened from the link in the email sent on registration: /confirm-email?userId=...&token=...
export function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const [confirm] = useState(() => async () => {
    const { error, response } = await api.POST('/api/auth/confirm-email', {
      body: { userId: params.get('userId') ?? '', token: params.get('token') ?? '' },
    })
    return { ok: response.ok, error }
  })
  const status = useConfirmOnce(confirm, 'This confirmation link is invalid or has expired.')

  return (
    <ConfirmationResult
      title="Confirm your email"
      status={status}
      success={
        <>
          <p role="status" className="text-sm text-green-700">Your email address is confirmed.</p>
          <Link to="/login" className="text-sm text-indigo-600 hover:underline">
            Log in
          </Link>
        </>
      }
    />
  )
}

// Opened from the link sent to a new address after changing it in Settings:
// /confirm-email-change?userId=...&email=...&token=...
export function ConfirmEmailChangePage() {
  const [params] = useSearchParams()
  const { isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const email = params.get('email') ?? ''
  const [confirm] = useState(() => async () => {
    const { error, response } = await api.POST('/api/auth/confirm-email-change', {
      body: { userId: params.get('userId') ?? '', email, token: params.get('token') ?? '' },
    })
    // Signed in on this browser: show the new email in the header and Settings.
    if (response.ok) void queryClient.invalidateQueries({ queryKey: accountKeys.me })
    return { ok: response.ok, error }
  })
  const status = useConfirmOnce(confirm, 'This confirmation link is invalid or has expired.')

  return (
    <ConfirmationResult
      title="Confirm your new email"
      status={status}
      success={
        <>
          <p role="status" className="text-sm text-green-700">
            Your email address is now {email}. Use it to log in.
          </p>
          <Link to={isAuthenticated ? '/settings' : '/login'} className="text-sm text-indigo-600 hover:underline">
            {isAuthenticated ? 'Back to settings' : 'Log in'}
          </Link>
        </>
      }
    />
  )
}
