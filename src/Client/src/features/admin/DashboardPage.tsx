import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { ErrorText } from '../../components/Messages'
import { formatPrice } from '../billing/plans'
import { useDashboard } from './backOfficeQueries'

function Stat({ label, value, note, to }: { label: string; value: ReactNode; note?: string; to?: string }) {
  const content = (
    <Card className="h-full">
      <CardHeader>
        <CardDescription>{label}</CardDescription>
        <CardTitle className="text-2xl tabular-nums">{value}</CardTitle>
      </CardHeader>
      {note && <CardContent className="text-xs text-muted-foreground">{note}</CardContent>}
    </Card>
  )
  return to ? (
    <Link to={to} className="rounded-xl focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none">
      {content}
    </Link>
  ) : (
    content
  )
}

// /admin: the back office's front page.
export function DashboardPage() {
  const dashboard = useDashboard()
  if (dashboard.isPending) return <p className="text-muted-foreground">Loading…</p>
  if (dashboard.isError) return <ErrorText error={dashboard.error} />

  const d = dashboard.data
  const revenue = d.monthlyRevenue.length === 0 ? formatPrice(0, 'USD') : d.monthlyRevenue.map((r) => formatPrice(r.monthlyAmount, r.currency)).join(' + ')
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Stat label="Monthly recurring revenue" value={revenue} note={`${d.payingOrganisations} paying organisations`} />
      <Stat label="Organisations" value={d.organisations} note={`${d.newOrganisations} new in the last 30 days`} to="/admin/organisations" />
      <Stat label="Active organisations" value={d.activeOrganisations} note="Used in the last 30 days" />
      <Stat label="Trials ending" value={d.trialsEnding} note="In the next 7 days" />
      <Stat label="Failed payments" value={d.failedPayments} note="Stripe is retrying" />
      <Stat label="Failed messages" value={d.failedMessages} note="Waiting to be replayed" to="/admin/failed-messages" />
    </div>
  )
}
