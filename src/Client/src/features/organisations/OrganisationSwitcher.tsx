import { useEffect, useRef, useState, useSyncExternalStore } from 'react'
import { Link } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { getOrganisationId, subscribeTokens } from '../../lib/api/authTokens'
import { onOrganisationLost } from '../../lib/api/client'
import { useAuth } from '../auth/useAuth'
import { useOrganisations, useSwitchOrganisation } from './organisationQueries'

// The header's organisation picker, for signed-in users. Every page shows the current
// organisation's data, so when the organisation in the tokens changes (switching here, joining
// one, or leaving one) the cached data is dropped and refetched.
export function OrganisationSwitcher() {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return null
  return <SignedInSwitcher />
}

function SignedInSwitcher() {
  const queryClient = useQueryClient()
  const organisations = useOrganisations()
  const switchTo = useSwitchOrganisation()
  const organisationId = useSyncExternalStore(subscribeTokens, getOrganisationId, () => null)

  const previous = useRef(organisationId)
  useEffect(() => {
    if (previous.current !== organisationId) {
      previous.current = organisationId
      void queryClient.resetQueries()
    }
  }, [organisationId, queryClient])

  // Removed from the organisation (or it was deleted) mid-session: the session moved to another
  // of the user's organisations, and what they were doing wasn't done (lib/api/client.ts).
  const [lost, setLost] = useState(false)
  useEffect(() => onOrganisationLost(() => setLost(true)), [])

  const current = organisations.data?.find((o) => o.isCurrent)
  if (!organisations.data) return null

  return (
    <div className="flex items-center gap-2 text-sm">
      <label className="sr-only" htmlFor="organisation">
        Organisation
      </label>
      <select
        id="organisation"
        value={current?.id ?? ''}
        disabled={switchTo.isPending}
        onChange={(e) => switchTo.mutate(e.target.value)}
        className="rounded border border-gray-300 px-2 py-1"
      >
        {organisations.data.map((o) => (
          <option key={o.id} value={o.id}>
            {o.name}
          </option>
        ))}
      </select>
      <Link to="/organisation" className="text-gray-600 hover:text-gray-900">
        Manage
      </Link>
      {lost && (
        <p role="alert" className="flex items-center gap-2 text-amber-800">
          You no longer have access to the organisation you were working in, and your last change
          there wasn&apos;t saved. You&apos;re now in {current?.name ?? 'another organisation'}.
          <button type="button" onClick={() => setLost(false)} className="underline">
            Dismiss
          </button>
        </p>
      )}
    </div>
  )
}
