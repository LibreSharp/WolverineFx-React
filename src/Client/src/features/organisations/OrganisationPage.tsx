import { useState, type FormEvent } from 'react'
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
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Input } from '@/components/ui/input'
import { Section } from '../../components/Section'

function Problem({ error }: { error: Error | null }) {
  return error ? <p role="alert" className="text-sm text-destructive">{error.message}</p> : null
}

// /organisation: the current organisation's name, members, roles and invitations, plus creating
// another organisation. What each role may do is enforced by the API; the page only hides what
// the user can't do.
export function OrganisationPage() {
  const organisation = useCurrentOrganisation()
  if (organisation.isPending) return <p className="mx-auto max-w-3xl px-6 py-10 text-muted-foreground">Loading…</p>
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
        <Input id="organisation-name" required maxLength={100} value={value} onChange={(e) => setValue(e.target.value)} />
        <Button type="submit" disabled={rename.isPending}>Rename</Button>
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
      {members.isPending && <p className="text-muted-foreground">Loading…</p>}
      <Problem error={members.error ?? changeRole.error ?? (remove.variables?.leaving ? null : remove.error)} />
      <ul className="divide-y divide-border">
        {members.data?.map((member) => (
          <li key={member.userId} className="flex items-center justify-between gap-4 py-2 text-sm">
            <span>
              {member.displayName || member.email}
              {member.userId === me.data?.userId && <span className="text-muted-foreground"> (you)</span>}
            </span>
            <span className="flex items-center gap-3">
              {canManage(member.role) && member.userId !== me.data?.userId ? (
                <>
                  <label className="sr-only" htmlFor={`role-${member.userId}`}>Role of {member.email}</label>
                  <NativeSelect
                    id={`role-${member.userId}`}
                    value={member.role}
                    onChange={(e) => changeRole.mutate({ userId: member.userId, role: e.target.value as OrganisationRole })}
                  >
                    {assignable.map((r) => <NativeSelectOption key={r}>{r}</NativeSelectOption>)}
                  </NativeSelect>
                  <Button variant="link" size="sm" type="button" className="h-auto px-0" onClick={() => remove.mutate({ userId: member.userId, leaving: false })}>
                    Remove
                  </Button>
                </>
              ) : (
                <span className="text-muted-foreground">{member.role}</span>
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
        <Input id="invite-email" type="email" required placeholder="name@example.com" value={email} onChange={(e) => setEmail(e.target.value)} className="flex-1" />
        <label className="sr-only" htmlFor="invite-role">Role</label>
        <NativeSelect id="invite-role" value={role} onChange={(e) => setRole(e.target.value as OrganisationRole)}>
          {roles.filter((r) => r !== 'Owner' || myRole === 'Owner').map((r) => <NativeSelectOption key={r}>{r}</NativeSelectOption>)}
        </NativeSelect>
        <Button type="submit" disabled={invite.isPending}>Invite</Button>
      </form>
      {invite.isSuccess && <p role="status" className="mt-2 text-sm text-emerald-700 dark:text-emerald-300">Invitation sent.</p>}
      <Problem error={invite.error ?? revoke.error} />
      {invitations.data && invitations.data.length > 0 && (
        <>
          <h3 className="mt-6 mb-2 text-sm font-semibold text-muted-foreground">Waiting to join</h3>
          <ul className="divide-y divide-border">
            {invitations.data.map((invitation) => (
              <li key={invitation.id} className="flex items-center justify-between py-2 text-sm">
                <span>
                  {invitation.email} <span className="text-muted-foreground">({invitation.role})</span>
                </span>
                <Button variant="link" size="sm" type="button" className="h-auto px-0" onClick={() => revoke.mutate(invitation.id)}>
                  Revoke
                </Button>
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
        <Input id="new-organisation" required maxLength={100} placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} />
        <Button type="submit" disabled={create.isPending}>Create</Button>
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
        <Button variant="outline"
          type="button"
          disabled={!me.data || leave.isPending}
          onClick={() => me.data && leave.mutate({ userId: me.data.userId, leaving: true }, done)}
          className="text-destructive text-destructive"
        >
          Leave {name}
        </Button>
        <Problem error={leave.error} />
        {isOwner && (
          <form
            onSubmit={(e: FormEvent) => {
              e.preventDefault()
              remove.mutate(undefined, done)
            }}
            className="space-y-2"
          >
            <label className="block text-sm text-muted-foreground" htmlFor="confirm-delete">
              To delete {name} and all its data for everyone, type its name:
            </label>
            <Input id="confirm-delete" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
            <Button variant="destructive" type="submit" disabled={confirm !== name || remove.isPending}>
              Delete organisation
            </Button>
            <Problem error={remove.error} />
          </form>
        )}
      </div>
    </Section>
  )
}
