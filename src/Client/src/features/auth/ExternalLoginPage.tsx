import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { api } from '../../lib/api/client'
import { setTokens } from '../../lib/api/authTokens'
import { AuthCard } from './AuthCard'

// Where the API sends the browser back after signing in with Google or GitHub:
// /external-login#refreshToken=... or /external-login#error=...
// The refresh token is swapped straight away for new tokens, which also makes the one in the URL
// useless (refresh tokens rotate). A ref makes sure that happens once, even when React runs
// effects twice in development.
export function ExternalLoginPage() {
  const navigate = useNavigate()
  // Read the fragment during the first render (not in the effect, which clears it).
  const [fragment] = useState(() => new URLSearchParams(window.location.hash.slice(1)))
  const refreshToken = fragment.get('refreshToken')
  const [error, setError] = useState<string | null>(() =>
    refreshToken ? null : (fragment.get('error') ?? "Signing in didn't work. Please try again."),
  )
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true
    // Take the token out of the address bar and browser history.
    window.history.replaceState(null, '', window.location.pathname)
    if (!refreshToken) return

    void api.POST('/api/auth/refresh', { body: { refreshToken } }).then(({ data }) => {
      if (!data) {
        setError("Signing in didn't work. Please try again.")
        return
      }
      setTokens(data)
      navigate('/todos', { replace: true })
    })
  }, [navigate, refreshToken])

  return (
    <AuthCard title="Signing in">
      {error ? (
        <>
          <p role="alert" className="text-sm text-destructive">{error}</p>
          <Link to="/login" className="text-sm text-primary hover:underline">Back to log in</Link>
        </>
      ) : (
        <p className="text-muted-foreground">One moment…</p>
      )}
    </AuthCard>
  )
}
