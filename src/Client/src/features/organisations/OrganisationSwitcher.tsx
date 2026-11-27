import { useEffect, useRef, useState, useSyncExternalStore } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Notice } from '../../components/Messages'
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
      <NativeSelect
        id="organisation"
        size="sm"
        value={current?.id ?? ''}
        disabled={switchTo.isPending}
        onChange={(e) => switchTo.mutate(e.target.value)}
      >
        {organisations.data.map((o) => (
          <NativeSelectOption key={o.id} value={o.id}>
            {o.name}
          </NativeSelectOption>
        ))}
      </NativeSelect>
      <Button asChild variant="ghost" size="sm">
        <Link to="/organisation">Manage</Link>
      </Button>
      {lost && (
        <Notice tone="warning" className="absolute top-14 right-6 z-10 max-w-sm">
          <span role="alert">
            You no longer have access to the organisation you were working in, and your last change
            there wasn&apos;t saved. You&apos;re now in {current?.name ?? 'another organisation'}.
          </span>{' '}
          <Button variant="link" size="sm" className="h-auto px-0" onClick={() => setLost(false)}>
            Dismiss
          </Button>
        </Notice>
      )}
    </div>
  )
}
