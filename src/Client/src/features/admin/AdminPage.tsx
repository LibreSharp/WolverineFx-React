import { useState, type FormEvent } from 'react'
import type { components } from '../../lib/api/schema'
import { useMe } from '../account/accountQueries'
import { useAdminUsers, useInviteUser, useLockUser, useSetRoles } from './adminQueries'

type AdminUser = components['schemas']['AdminUser']

const inputClass = 'rounded border border-gray-300 px-3 py-2 text-sm'
const buttonClass = 'rounded bg-indigo-600 px-3 py-2 text-sm text-white hover:bg-indigo-700 disabled:opacity-50'

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
    <section className="rounded border border-gray-200 bg-white p-6">
      <h2 className="mb-1 text-lg font-semibold">Invite someone</h2>
      <p className="mb-4 text-sm text-gray-600">They get an email with a link to choose a password.</p>
      <form onSubmit={submit} className="space-y-3">
        <div className="flex gap-2">
          <input
            type="email"
            required
            placeholder="Email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className={`${inputClass} flex-1`}
          />
          <button type="submit" disabled={invite.isPending} className={buttonClass}>
            Send invitation
          </button>
        </div>
        <RoleCheckboxes allRoles={allRoles} selected={roles} onChange={setRoles} />
        {invite.error && <p role="alert" className="text-sm text-red-600">{invite.error.message}</p>}
        {invite.isSuccess && sentTo && <p role="status" className="text-sm text-green-700">Invitation sent to {sentTo}.</p>}
      </form>
    </section>
  )
}

function Status({ user }: { user: AdminUser }) {
  const [label, tone] = user.lockedByAdmin
    ? ['Locked', 'bg-red-100 text-red-800']
    : user.lockedUntil
      ? ['Locked out (failed logins)', 'bg-amber-100 text-amber-800']
      : user.invitationPending
        ? ['Invited', 'bg-blue-100 text-blue-800']
        : !user.emailConfirmed
          ? ['Unconfirmed', 'bg-gray-100 text-gray-700']
          : ['Active', 'bg-green-100 text-green-800']
  return <span className={`rounded px-2 py-0.5 text-xs ${tone}`}>{label}</span>
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
        {user.displayName && <div className="truncate text-sm text-gray-500">{user.displayName}</div>}
        {error && <p role="alert" className="text-sm text-red-600">{error.message}</p>}
      </div>
      <Status user={user} />
      <RoleCheckboxes
        allRoles={allRoles}
        selected={user.roles}
        disabled={setRoles.isPending}
        onChange={(roles) => setRoles.mutate({ id: user.id, roles })}
      />
      <button
        type="button"
        disabled={isSelf || lock.isPending}
        title={isSelf ? "You can't lock your own account" : undefined}
        onClick={() => lock.mutate({ id: user.id, lock: !locked })}
        className="w-20 rounded border border-gray-300 px-2 py-1 text-sm hover:bg-gray-100 disabled:opacity-50"
      >
        {locked ? 'Unlock' : 'Lock'}
      </button>
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
      <section className="rounded border border-gray-200 bg-white p-6">
        <h2 className="mb-4 text-lg font-semibold">Users</h2>
        <input
          type="search"
          placeholder="Search by email"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
          className={`${inputClass} mb-2 w-full`}
        />
        {users.isPending && <p className="text-gray-500">Loading…</p>}
        {users.isError && <p role="alert" className="text-sm text-red-600">{users.error.message}</p>}
        {users.data && (
          <>
            <ul className="divide-y divide-gray-100">
              {users.data.users.map((user) => (
                <UserRow key={user.id} user={user} allRoles={allRoles} isSelf={user.id === me.data?.userId} />
              ))}
            </ul>
            {users.data.users.length === 0 && <p className="py-3 text-sm text-gray-500">No users found.</p>}
            <div className="mt-4 flex items-center justify-between text-sm text-gray-600">
              <span>
                {total} user{total === 1 ? '' : 's'} · page {page} of {pageCount}
              </span>
              <div className="flex gap-2">
                <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)} className="rounded border border-gray-300 px-2 py-1 disabled:opacity-50">
                  Previous
                </button>
                <button type="button" disabled={page >= pageCount} onClick={() => setPage(page + 1)} className="rounded border border-gray-300 px-2 py-1 disabled:opacity-50">
                  Next
                </button>
              </div>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
