import { useState, type FormEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useMe } from '../account/accountQueries'
import { BillingSection } from '../billing/BillingSection'
import {
  canManageMembers,
  roles,
  useChangeRole,
  useCreateOrganisation,
  useCurrentOrganisation,
  useDeleteOrganisation,
  useInvitations,
  useInvite,
  useMembers,
  useRemoveMember,
  useRenameOrganisation,
  useRevokeInvitation,
  type OrganisationRole,
} from './organisationQueries'

const inputClass = 'w-full rounded border border-gray-300 px-3 py-2'
const buttonClass = 'rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50'
const linkButtonClass = 'text-sm text-indigo-600 hover:underline disabled:opacity-50'

function Section({ title, children, tone }: { title: string; children: ReactNode; tone?: 'danger' }) {
  return (
    <section className={`rounded border bg-white p-6 ${tone === 'danger' ? 'border-red-200' : 'border-gray-200'}`}>
      <h2 className="mb-4 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}

function Problem({ error }: { error: Error | null }) {
  return error ? <p role="alert" className="text-sm text-red-600">{error.message}</p> : null
}

// /organisation: the current organisation's name, members, roles and invitations, plus creating
// another organisation. What each role may do is enforced by the API; the page only hides what
// the user can't do.
export function OrganisationPage() {
  const organisation = useCurrentOrganisation()
  if (organisation.isPending) return <p className="mx-auto max-w-3xl px-6 py-10 text-gray-500">Loading…</p>
  if (organisation.isError) return <div className="mx-auto max-w-3xl px-6 py-10"><Problem error={organisation.error} /></div>

  const role = organisation.data.role
  return (
    // Keyed by organisation, so the forms start afresh after switching.
    <div key={organisation.data.id} className="mx-auto max-w-3xl space-y-6 px-6 py-10">
      <h1 className="text-2xl font-semibold">{organisation.data.name}</h1>
      {role === 'Owner' && <RenameSection name={organisation.data.name} />}
      <BillingSection />
      <MembersSection myRole={role} />
      {canManageMembers(role) && <InvitationsSection myRole={role} />}
      <CreateSection />
      <LeaveOrDeleteSection isOwner={role === 'Owner'} name={organisation.data.name} />
    </div>
  )
}

function RenameSection({ name }: { name: string }) {
  const rename = useRenameOrganisation()
  const [value, setValue] = useState(name)
  return (
    <Section title="Name">
      <form
        onSubmit={(e: FormEvent) => {
          e.preventDefault()
          rename.mutate(value)
        }}
        className="flex gap-2"
      >
        <label className="sr-only" htmlFor="organisation-name">Organisation name</label>
        <input id="organisation-name" required maxLength={100} value={value} onChange={(e) => setValue(e.target.value)} className={inputClass} />
        <button type="submit" disabled={rename.isPending} className={buttonClass}>Rename</button>
      </form>
      <Problem error={rename.error} />
    </Section>
  )
}

function MembersSection({ myRole }: { myRole: string }) {
  const members = useMembers()
  const me = useMe()
  const changeRole = useChangeRole()
  const remove = useRemoveMember()

  // Admins manage admins and members; only owners manage owners (and make owners).
  const canManage = (role: string) => canManageMembers(myRole) && (role !== 'Owner' || myRole === 'Owner')
  const assignable = roles.filter((r) => r !== 'Owner' || myRole === 'Owner')

  return (
    <Section title="Members">
      {members.isPending && <p className="text-gray-500">Loading…</p>}
      <Problem error={members.error ?? changeRole.error ?? (remove.variables?.leaving ? null : remove.error)} />
      <ul className="divide-y divide-gray-100">
        {members.data?.map((member) => (
          <li key={member.userId} className="flex items-center justify-between gap-4 py-2 text-sm">
            <span>
              {member.displayName || member.email}
              {member.userId === me.data?.userId && <span className="text-gray-500"> (you)</span>}
            </span>
            <span className="flex items-center gap-3">
              {canManage(member.role) && member.userId !== me.data?.userId ? (
                <>
                  <label className="sr-only" htmlFor={`role-${member.userId}`}>Role of {member.email}</label>
                  <select
                    id={`role-${member.userId}`}
                    value={member.role}
                    onChange={(e) => changeRole.mutate({ userId: member.userId, role: e.target.value as OrganisationRole })}
                    className="rounded border border-gray-300 px-2 py-1"
                  >
                    {assignable.map((r) => <option key={r}>{r}</option>)}
                  </select>
                  <button type="button" className={linkButtonClass} onClick={() => remove.mutate({ userId: member.userId, leaving: false })}>
                    Remove
                  </button>
                </>
              ) : (
                <span className="text-gray-600">{member.role}</span>
              )}
            </span>
          </li>
        ))}
      </ul>
    </Section>
  )
}

function InvitationsSection({ myRole }: { myRole: string }) {
  const invitations = useInvitations(true)
  const invite = useInvite()
  const revoke = useRevokeInvitation()
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<OrganisationRole>('Member')

  function send(event: FormEvent) {
    event.preventDefault()
    invite.mutate({ email, role }, { onSuccess: () => setEmail('') })
  }

  return (
    <Section title="Invite people">
      <form onSubmit={send} className="flex flex-wrap gap-2">
        <label className="sr-only" htmlFor="invite-email">Email to invite</label>
        <input id="invite-email" type="email" required placeholder="name@example.com" value={email} onChange={(e) => setEmail(e.target.value)} className={`${inputClass} flex-1`} />
        <label className="sr-only" htmlFor="invite-role">Role</label>
        <select id="invite-role" value={role} onChange={(e) => setRole(e.target.value as OrganisationRole)} className="rounded border border-gray-300 px-2 py-2">
          {roles.filter((r) => r !== 'Owner' || myRole === 'Owner').map((r) => <option key={r}>{r}</option>)}
        </select>
        <button type="submit" disabled={invite.isPending} className={buttonClass}>Invite</button>
      </form>
      {invite.isSuccess && <p role="status" className="mt-2 text-sm text-green-700">Invitation sent.</p>}
      <Problem error={invite.error ?? revoke.error} />
      {invitations.data && invitations.data.length > 0 && (
        <>
          <h3 className="mt-6 mb-2 text-sm font-semibold text-gray-700">Waiting to join</h3>
          <ul className="divide-y divide-gray-100">
            {invitations.data.map((invitation) => (
              <li key={invitation.id} className="flex items-center justify-between py-2 text-sm">
                <span>
                  {invitation.email} <span className="text-gray-500">({invitation.role})</span>
                </span>
                <button type="button" className={linkButtonClass} onClick={() => revoke.mutate(invitation.id)}>
                  Revoke
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </Section>
  )
}

function CreateSection() {
  const create = useCreateOrganisation()
  const navigate = useNavigate()
  const [name, setName] = useState('')
  return (
    <Section title="New organisation">
      <form
        onSubmit={(e: FormEvent) => {
          e.preventDefault()
          create.mutate(name, { onSuccess: () => { setName(''); void navigate('/organisation') } })
        }}
        className="flex gap-2"
      >
        <label className="sr-only" htmlFor="new-organisation">New organisation name</label>
        <input id="new-organisation" required maxLength={100} placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} className={inputClass} />
        <button type="submit" disabled={create.isPending} className={buttonClass}>Create</button>
      </form>
      <Problem error={create.error} />
    </Section>
  )
}

function LeaveOrDeleteSection({ isOwner, name }: { isOwner: boolean; name: string }) {
  const me = useMe()
  const leave = useRemoveMember()
  const remove = useDeleteOrganisation()
  const navigate = useNavigate()
  const [confirm, setConfirm] = useState('')
  const done = { onSuccess: () => void navigate('/todos') }

  return (
    <Section title="Leave or delete" tone="danger">
      <div className="space-y-4">
        <button
          type="button"
          disabled={!me.data || leave.isPending}
          onClick={() => me.data && leave.mutate({ userId: me.data.userId, leaving: true }, done)}
          className="rounded border border-red-300 px-4 py-2 text-red-700 hover:bg-red-50 disabled:opacity-50"
        >
          Leave {name}
        </button>
        <Problem error={leave.error} />
        {isOwner && (
          <form
            onSubmit={(e: FormEvent) => {
              e.preventDefault()
              remove.mutate(undefined, done)
            }}
            className="space-y-2"
          >
            <label className="block text-sm text-gray-700" htmlFor="confirm-delete">
              To delete {name} and all its data for everyone, type its name:
            </label>
            <input id="confirm-delete" value={confirm} onChange={(e) => setConfirm(e.target.value)} className={inputClass} />
            <button type="submit" disabled={confirm !== name || remove.isPending} className="rounded bg-red-600 px-4 py-2 text-white hover:bg-red-700 disabled:opacity-50">
              Delete organisation
            </button>
            <Problem error={remove.error} />
          </form>
        )}
      </div>
    </Section>
  )
}
