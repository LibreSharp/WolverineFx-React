import { useState, type FormEvent } from 'react'
import type { components } from '../../lib/api/schema'
import { useMe } from '../account/accountQueries'
import { useAdminUsers, useInviteUser, useLockUser, useSetRoles } from './adminQueries'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Section } from '../../components/Section'

type AdminUser = components['schemas']['AdminUser']

function RoleCheckboxes({
  allRoles,
  selected,
  onChange,
  disabled,
}: {
  allRoles: string[]
  selected: string[]
  onChange: (roles: string[]) => void
  disabled?: boolean
}) {
  return (
    <div className="flex flex-wrap gap-3">
      {allRoles.map((role) => (
        <label key={role} className="flex items-center gap-1 text-sm">
          <input
            type="checkbox"
            checked={selected.includes(role)}
            disabled={disabled}
            onChange={(event) =>
              onChange(event.target.checked ? [...selected, role] : selected.filter((r) => r !== role))
            }
          />
          {role}
        </label>
      ))}
    </div>
  )
}

function InviteForm({ allRoles }: { allRoles: string[] }) {
  const invite = useInviteUser()
  const [email, setEmail] = useState('')
  const [roles, setRoles] = useState<string[]>([])
  const [sentTo, setSentTo] = useState<string | null>(null)

  function submit(event: FormEvent) {
    event.preventDefault()
    invite.mutate(
      { email, roles },
      {
        onSuccess: () => {
          setSentTo(email)
          setEmail('')
          setRoles([])
        },
      },
    )
  }

  return (
    <Section title="Invite someone" description="They get an email with a link to choose a password.">
      <form onSubmit={submit} className="space-y-3">
        <div className="flex gap-2">
          <Input
            type="email"
            required
            placeholder="Email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="flex-1"
          />
          <Button type="submit" disabled={invite.isPending}>
            Send invitation
          </Button>
        </div>
        <RoleCheckboxes allRoles={allRoles} selected={roles} onChange={setRoles} />
        {invite.error && <p role="alert" className="text-sm text-destructive">{invite.error.message}</p>}
        {invite.isSuccess && sentTo && <p role="status" className="text-sm text-emerald-700 dark:text-emerald-300">Invitation sent to {sentTo}.</p>}
      </form>
    </Section>
  )
}

function Status({ user }: { user: AdminUser }) {
  const [label, variant] = user.lockedByAdmin
    ? (['Locked', 'destructive'] as const)
    : user.lockedUntil
      ? (['Locked out (failed logins)', 'destructive'] as const)
      : user.invitationPending
        ? (['Invited', 'outline'] as const)
        : !user.emailConfirmed
          ? (['Unconfirmed', 'secondary'] as const)
          : (['Active', 'default'] as const)
  return <Badge variant={variant}>{label}</Badge>
}

function UserRow({ user, allRoles, isSelf }: { user: AdminUser; allRoles: string[]; isSelf: boolean }) {
  const lock = useLockUser()
  const setRoles = useSetRoles()
  const locked = user.lockedUntil !== null
  const error = lock.error ?? setRoles.error

  return (
    <li className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:gap-4">
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium">{user.email}</div>
        {user.displayName && <div className="truncate text-sm text-muted-foreground">{user.displayName}</div>}
        {error && <p role="alert" className="text-sm text-destructive">{error.message}</p>}
      </div>
      <Status user={user} />
      <RoleCheckboxes
        allRoles={allRoles}
        selected={user.roles}
        disabled={setRoles.isPending}
        onChange={(roles) => setRoles.mutate({ id: user.id, roles })}
      />
      <Button variant="outline" size="sm"
        type="button"
        disabled={isSelf || lock.isPending}
        title={isSelf ? "You can't lock your own account" : undefined}
        onClick={() => lock.mutate({ id: user.id, lock: !locked })}
        className="w-20"
      >
        {locked ? 'Unlock' : 'Lock'}
      </Button>
    </li>
  )
}

// /admin: users, their roles and locks, and invitations. Only for the Admin role (RequireAdmin
// in routes.ts); the API checks the role on every request too.
export function AdminPage() {
  const me = useMe()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const users = useAdminUsers(search, page)
  const allRoles = users.data?.allRoles ?? []
  // The OpenAPI document allows numbers as strings, so convert them.
  const total = Number(users.data?.total ?? 0)
  const pageCount = users.data ? Math.max(1, Math.ceil(total / Number(users.data.pageSize))) : 1

  return (
    <div className="mx-auto max-w-3xl space-y-6 px-6 py-10">
      <h1 className="text-2xl font-semibold">Admin</h1>
      <InviteForm allRoles={allRoles} />
      <Section title="Users">
        <Input
          type="search"
          placeholder="Search by email"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
          className="mb-2 w-full"
        />
        {users.isPending && <p className="text-muted-foreground">Loading…</p>}
        {users.isError && <p role="alert" className="text-sm text-destructive">{users.error.message}</p>}
        {users.data && (
          <>
            <ul className="divide-y divide-border">
              {users.data.users.map((user) => (
                <UserRow key={user.id} user={user} allRoles={allRoles} isSelf={user.id === me.data?.userId} />
              ))}
            </ul>
            {users.data.users.length === 0 && <p className="py-3 text-sm text-muted-foreground">No users found.</p>}
            <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
              <span>
                {total} user{total === 1 ? '' : 's'} · page {page} of {pageCount}
              </span>
              <div className="flex gap-2">
                <Button variant="outline" type="button" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  Previous
                </Button>
                <Button variant="outline" type="button" disabled={page >= pageCount} onClick={() => setPage(page + 1)}>
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </Section>
    </div>
  )
}
