import { Link, useSearchParams } from 'react-router'
import { ErrorText, Notice } from '../../components/Messages'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { cn } from '@/lib/utils'
import { daysLeft, useBilling, useCheckout, usePortal } from './billingQueries'
import { formatLimit, formatPrice, planCatalogue } from './plans'

const statusText: Record<string, string> = {
  Trial: 'Trial',
  Active: 'Active',
  PastDue: 'Payment failed',
  Free: 'Free',
}

// The organisation's plan on /organisation: the plan, any trial, how much of each limit is used,
// and (for owners) upgrading or managing the subscription on the payment provider's pages.
export function BillingSection() {
  const [params] = useSearchParams()
  const backFromCheckout = params.get('billing') === 'success'
  const billing = useBilling({ waitingForPayment: backFromCheckout })
  if (billing.isPending) return null
  if (billing.isError) return <p role="alert" className="text-sm text-destructive">{billing.error.message}</p>

  const { planName, status, trialEndsAt, periodEnd, cancelAtPeriodEnd, limits, canManage, paymentsEnabled, hasCustomer } = billing.data
  const subscribed = status === 'Active' || status === 'PastDue'
  return (
    <Card aria-labelledby="billing-heading" role="region">
      <CardHeader>
        <CardTitle id="billing-heading" className="flex items-center gap-2 text-lg">
          {planName} plan
          <Badge variant={status === 'PastDue' ? 'destructive' : 'secondary'}>{statusText[status] ?? status}</Badge>
        </CardTitle>
        {status === 'Trial' && (
          <CardDescription>
            {daysLeft(trialEndsAt)} days of the trial left. Then this organisation moves to the Free plan; nothing is deleted.
          </CardDescription>
        )}
        {status === 'PastDue' && <CardDescription>Update your payment details to keep this plan.</CardDescription>}
        {cancelAtPeriodEnd && <CardDescription>Cancelled: ends on {new Date(periodEnd).toLocaleDateString()}.</CardDescription>}
      </CardHeader>
      <CardContent>
        <ul className="space-y-3">
          {limits.map((limit) => {
            const max = limit.limit === null ? null : Number(limit.limit)
            const used = Number(limit.used)
            return (
              <li key={limit.name} className="text-sm">
                <div className="flex justify-between">
                  <span>{limit.description}</span>
                  <span className={cn(limit.reached ? 'text-destructive' : limit.nearLimit ? 'text-amber-700 dark:text-amber-300' : 'text-muted-foreground')}>
                    {used.toLocaleString()} of {formatLimit(limit.limit)}
                  </span>
                </div>
                {max !== null && max > 0 && (
                  <Progress
                    aria-label={limit.description}
                    value={Math.min(100, (used / max) * 100)}
                    className={cn('mt-1', limit.reached ? '[&>*]:bg-destructive' : limit.nearLimit && '[&>*]:bg-amber-500')}
                  />
                )}
              </li>
            )
          })}
        </ul>
      </CardContent>
      <CardFooter className="flex-col items-start gap-3">
        {backFromCheckout && (
          <Notice className="w-full">
            {subscribed ? `Thank you: you're on the ${planName} plan.` : 'Thank you. Your plan changes as soon as the payment is confirmed…'}
          </Notice>
        )}
        {canManage && paymentsEnabled && (subscribed && hasCustomer ? <ManageBilling /> : <Upgrade currentPlanId={billing.data.planId} status={status} />)}
        {!canManage && <p className="text-sm text-muted-foreground">Only the organisation's owners can change its plan.</p>}
        <Button asChild variant="link" className="h-auto px-0">
          <Link to="/pricing">Compare plans</Link>
        </Button>
      </CardFooter>
    </Card>
  )
}

// A button for each paid plan, which opens the payment provider's checkout.
function Upgrade({ currentPlanId, status }: { currentPlanId: string; status: string }) {
  const checkout = useCheckout()
  const paid = planCatalogue.plans.filter((plan) => !plan.isFree && plan.prices.length > 0)
  return (
    <div className="flex flex-wrap items-center gap-2">
      {paid.map((plan) => {
        const price = plan.prices.find((p) => p.currency === planCatalogue.currency) ?? plan.prices[0]
        const label = status === 'Trial' && plan.id === currentPlanId ? `Keep ${plan.name}` : `Upgrade to ${plan.name}`
        return (
          <Button key={plan.id} disabled={checkout.isPending} onClick={() => checkout.mutate({ planId: plan.id, currency: price.currency })}>
            {label} · {formatPrice(price.monthlyAmount, price.currency)} a month
          </Button>
        )
      })}
      <ErrorText error={checkout.error} className="w-full" />
    </div>
  )
}

// The payment provider's portal: change plan or payment details, see invoices, or cancel.
function ManageBilling() {
  const portal = usePortal()
  return (
    <div className="flex flex-col gap-1">
      <Button variant="outline" disabled={portal.isPending} onClick={() => portal.mutate()}>
        Manage billing
      </Button>
      <ErrorText error={portal.error} />
    </div>
  )
}
