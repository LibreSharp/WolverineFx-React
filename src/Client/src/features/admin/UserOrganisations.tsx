import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { useUserOrganisations } from './backOfficeQueries'

// A user's organisations, on the back office's users list: loaded when asked for.
export function UserOrganisations({ userId }: { userId: string }) {
  const [open, setOpen] = useState(false)
  const organisations = useUserOrganisations(userId, open)
  if (!open) {
    return (
      <Button variant="link" size="sm" className="h-auto px-0" onClick={() => setOpen(true)}>
        Organisations
      </Button>
    )
  }

  if (organisations.isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (organisations.isError) return <p role="alert" className="text-sm text-destructive">{organisations.error.message}</p>
  return (
    <ul className="text-sm">
      {organisations.data.length === 0 && <li className="text-muted-foreground">No organisations.</li>}
      {organisations.data.map((organisation) => (
        <li key={organisation.id}>
          <Link to={`/admin/organisations/${organisation.id}`} className="text-primary hover:underline">
            {organisation.name}
          </Link>{' '}
          <span className="text-muted-foreground">({organisation.role})</span>
        </li>
      ))}
    </ul>
  )
}
