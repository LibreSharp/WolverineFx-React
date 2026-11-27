import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { cn } from '@/lib/utils'
import { daysLeft, useBilling } from './billingQueries'
import { formatLimit } from './plans'

const statusText: Record<string, string> = {
  Trial: 'Trial',
  Active: 'Active',
  PastDue: 'Payment failed',
  Free: 'Free',
}

// The organisation's plan on /organisation: the plan, any trial, and how much of each limit is used.
export function BillingSection() {
  const billing = useBilling()
  if (billing.isPending) return null
  if (billing.isError) return <p role="alert" className="text-sm text-destructive">{billing.error.message}</p>

  const { planName, status, trialEndsAt, periodEnd, cancelAtPeriodEnd, limits } = billing.data
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
      <CardFooter>
        <Button asChild variant="link" className="px-0">
          <Link to="/pricing">Compare plans</Link>
        </Button>
      </CardFooter>
    </Card>
  )
}
