import { useEffect, useRef, useSyncExternalStore } from 'react'
import { Link } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { getOrganisationId, subscribeTokens } from '../../lib/api/authTokens'
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
    </div>
  )
}
