import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { ErrorText, Notice } from '../../components/Messages'
import { Section } from '../../components/Section'
import type { components } from '../../lib/api/schema'
import { formatLimit, formatPrice, planCatalogue } from '../billing/plans'
import { AuditLog } from './AuditLogPage'
import { useAdminOrganisation, useDeleteOrganisation, useGrantPlan, useSaveNotes, useSetFeature, useSuspend } from './backOfficeQueries'
import { OverrideSelect } from './OverrideSelect'

type Detail = components['schemas']['AdminOrganisationDetail']

const day = (date: string | null | undefined) => (date ? new Date(date).toLocaleDateString() : '')

// /admin/organisations/:id: one organisation, and supporting it.
export function OrganisationDetailPage() {
  const { id = '' } = useParams()
  const organisation = useAdminOrganisation(id)
  if (organisation.isPending) return <p className="text-muted-foreground">Loading…</p>
  if (organisation.isError) return <ErrorText error={organisation.error} />

  const o = organisation.data
  return (
    <div className="space-y-6">
      <div>
        <h2 className="flex items-center gap-2 text-xl font-semibold">
          {o.name}
          {o.suspendedAt && <Badge variant="destructive">Suspended</Badge>}
        </h2>
        <p className="text-sm text-muted-foreground">Created {day(o.createdAt)}</p>
        {o.suspendedAt && (
          <Notice tone="warning" className="mt-2">
            Suspended on {day(o.suspendedAt)}: {o.suspensionReason}
          </Notice>
        )}
      </div>
      <BillingSection organisation={o} />
      <UsageSection organisation={o} />
      <MembersSection organisation={o} />
      <InvoicesSection organisation={o} />
      <FeaturesSection organisation={o} />
      <NotesSection organisation={o} />
      <AuditLog targetId={o.id} title="History" />
      <SuspendOrDeleteSection organisation={o} />
    </div>
  )
}

function BillingSection({ organisation }: { organisation: Detail }) {
  const b = organisation.billing
  const grant = useGrantPlan(organisation.id)
  const subscribed = b.status === 'Active' || b.status === 'PastDue'
  const [planId, setPlanId] = useState(planCatalogue.plans.find((plan) => !plan.isFree)?.id ?? b.planId)
  const [until, setUntil] = useState(() => new Date(Date.now() + 30 * 86_400_000).toISOString().slice(0, 10))

  return (
    <Section title="Plan">
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
        <dt className="text-muted-foreground">Plan</dt>
        <dd>
          {b.planName} (version {b.planVersion}) · {b.status}
          {b.cancelAtPeriodEnd && ' · cancelled, ends with the period'}
        </dd>
        {b.trialEndsAt && (
          <>
            <dt className="text-muted-foreground">Trial ends</dt>
            <dd>{day(b.trialEndsAt)}</dd>
          </>
        )}
        <dt className="text-muted-foreground">Billing period</dt>
        <dd>
          {day(b.periodStart)} to {day(b.periodEnd)}
        </dd>
        {(b.customerUrl || b.subscriptionUrl) && (
          <>
            <dt className="text-muted-foreground">Payment provider</dt>
            <dd className="flex gap-3">
              {b.customerUrl && (
                <a href={b.customerUrl} target="_blank" rel="noreferrer" className="text-primary hover:underline">
                  Customer
                </a>
              )}
              {b.subscriptionUrl && (
                <a href={b.subscriptionUrl} target="_blank" rel="noreferrer" className="text-primary hover:underline">
                  Subscription
                </a>
              )}
            </dd>
          </>
        )}
      </dl>
      {subscribed ? (
        <p className="mt-4 text-sm text-muted-foreground">It has a subscription: change its plan, or refund it, with the payment provider.</p>
      ) : (
        <form
          className="mt-4 flex flex-wrap items-end gap-2"
          onSubmit={(event: FormEvent) => {
            event.preventDefault()
            grant.mutate({ planId, until: new Date(`${until}T23:59:59Z`).toISOString() })
          }}
        >
          <div className="space-y-1">
            <Label htmlFor="grant-plan">Give plan</Label>
            <NativeSelect id="grant-plan" value={planId} onChange={(event) => setPlanId(event.target.value)}>
              {planCatalogue.plans.map((plan) => (
                <NativeSelectOption key={plan.id} value={plan.id}>
                  {plan.name}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>
          <div className="space-y-1">
            <Label htmlFor="grant-until">Until</Label>
            <Input id="grant-until" type="date" required value={until} onChange={(event) => setUntil(event.target.value)} />
          </div>
          <Button type="submit" disabled={grant.isPending}>
            {b.status === 'Trial' ? 'Change the trial' : 'Give the plan'}
          </Button>
          <ErrorText error={grant.error} className="w-full" />
          {grant.isSuccess && <Notice className="w-full">Done: {planCatalogue.plans.find((p) => p.id === planId)?.name} until {until}.</Notice>}
        </form>
      )}
    </Section>
  )
}

function UsageSection({ organisation }: { organisation: Detail }) {
  return (
    <Section title="Usage">
      <ul className="space-y-1 text-sm">
        {organisation.limits.map((limit) => (
          <li key={limit.name} className="flex justify-between">
            <span>{limit.description}</span>
            <span className={limit.reached ? 'text-destructive' : 'text-muted-foreground'}>
              {Number(limit.used).toLocaleString()} of {formatLimit(limit.limit)}
            </span>
          </li>
        ))}
      </ul>
    </Section>
  )
}

function MembersSection({ organisation }: { organisation: Detail }) {
  return (
    <Section title="Members">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Email</TableHead>
            <TableHead>Role</TableHead>
            <TableHead>Joined</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {organisation.members.map((member) => (
            <TableRow key={member.userId}>
              <TableCell>
                {member.email}
                {member.displayName && <span className="text-muted-foreground"> · {member.displayName}</span>}
              </TableCell>
              <TableCell>{member.role}</TableCell>
              <TableCell>{day(member.joinedAt)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Section>
  )
}

function InvoicesSection({ organisation }: { organisation: Detail }) {
  return (
    <Section title="Invoices">
      {organisation.invoices.length === 0 ? (
        <p className="text-sm text-muted-foreground">None yet.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Date</TableHead>
              <TableHead>Number</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Total</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {organisation.invoices.map((invoice) => (
              <TableRow key={invoice.id}>
                <TableCell>{day(invoice.createdAt)}</TableCell>
                <TableCell>
                  {invoice.dashboardUrl ? (
                    <a href={invoice.dashboardUrl} target="_blank" rel="noreferrer" className="text-primary hover:underline">
                      {invoice.number ?? invoice.id}
                    </a>
                  ) : (
                    (invoice.number ?? invoice.id)
                  )}
                </TableCell>
                <TableCell>
                  <Badge variant={invoice.status === 'paid' ? 'secondary' : invoice.status === 'open' ? 'outline' : 'destructive'}>{invoice.status}</Badge>
                </TableCell>
                <TableCell className="text-right tabular-nums">{formatPrice(invoice.total, invoice.currency)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Section>
  )
}

function FeaturesSection({ organisation }: { organisation: Detail }) {
  const setFeature = useSetFeature()
  return (
    <Section title="Features" description="Turn a feature on or off for this organisation only, whatever its plan.">
      <ul className="space-y-2">
        {organisation.features.map((feature) => (
          <li key={feature.name} className="flex items-center justify-between gap-4 text-sm">
            <span>
              {feature.description} <span className="text-muted-foreground">· {feature.enabled ? 'on' : 'off'} now</span>
            </span>
            <OverrideSelect
              label={`${feature.description} for ${organisation.name}`}
              value={feature.override}
              defaultLabel="as for everyone"
              disabled={setFeature.isPending}
              onChange={(enabled) => setFeature.mutate({ feature: feature.name, scope: 'Organisation', scopeKey: organisation.id, enabled })}
            />
          </li>
        ))}
      </ul>
      <ErrorText error={setFeature.error} />
    </Section>
  )
}

function NotesSection({ organisation }: { organisation: Detail }) {
  const save = useSaveNotes(organisation.id)
  const [notes, setNotes] = useState(organisation.notes ?? '')
  return (
    <Section title="Notes" description="For admins only: the organisation's members never see them.">
      <form
        className="space-y-2"
        onSubmit={(event: FormEvent) => {
          event.preventDefault()
          save.mutate(notes)
        }}
      >
        <Label htmlFor="admin-notes" className="sr-only">
          Notes
        </Label>
        <Textarea id="admin-notes" rows={4} maxLength={10_000} value={notes} onChange={(event) => setNotes(event.target.value)} />
        <Button type="submit" variant="outline" disabled={save.isPending}>
          Save notes
        </Button>
        <ErrorText error={save.error} />
        {save.isSuccess && <p role="status" className="text-sm text-muted-foreground">Saved.</p>}
      </form>
    </Section>
  )
}

function SuspendOrDeleteSection({ organisation }: { organisation: Detail }) {
  const navigate = useNavigate()
  const suspend = useSuspend(organisation.id)
  const remove = useDeleteOrganisation(organisation.id)
  const [reason, setReason] = useState('')
  const [confirm, setConfirm] = useState('')

  return (
    <Section title="Suspend or delete" tone="danger">
      {organisation.suspendedAt ? (
        <Button variant="outline" disabled={suspend.isPending} onClick={() => suspend.mutate(null)}>
          Unsuspend
        </Button>
      ) : (
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(event: FormEvent) => {
            event.preventDefault()
            suspend.mutate(reason)
          }}
        >
          <div className="flex-1 space-y-1">
            <Label htmlFor="suspend-reason">Reason</Label>
            <Input id="suspend-reason" required maxLength={500} value={reason} onChange={(event) => setReason(event.target.value)} />
          </div>
          <Button type="submit" variant="destructive" disabled={suspend.isPending}>
            Suspend
          </Button>
        </form>
      )}
      <p className="mt-1 text-sm text-muted-foreground">Nobody can work in a suspended organisation. Nothing is deleted.</p>
      <ErrorText error={suspend.error} />

      <form
        className="mt-6 flex flex-wrap items-end gap-2"
        onSubmit={(event: FormEvent) => {
          event.preventDefault()
          remove.mutate(undefined, { onSuccess: () => navigate('/admin/organisations') })
        }}
      >
        <div className="flex-1 space-y-1">
          <Label htmlFor="confirm-delete">Type the organisation's name to delete it, with all its data</Label>
          <Input id="confirm-delete" value={confirm} onChange={(event) => setConfirm(event.target.value)} />
        </div>
        <Button type="submit" variant="destructive" disabled={confirm !== organisation.name || remove.isPending}>
          Delete
        </Button>
      </form>
      <ErrorText error={remove.error} />
    </Section>
  )
}
