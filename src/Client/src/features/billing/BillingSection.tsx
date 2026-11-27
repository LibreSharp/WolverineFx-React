import { Link } from 'react-router'
import { daysLeft, useBilling } from './billingQueries'
import { formatLimit } from './plans'

const statusText: Record<string, string> = {
  Trial: 'Trial',
  Active: 'Active',
  PastDue: 'Payment failed: update your payment details to keep this plan',
  Free: 'Free',
}

// The organisation's plan on /organisation: the plan, any trial, and how much of each limit is used.
export function BillingSection() {
  const billing = useBilling()
  if (billing.isPending) return null
  if (billing.isError) return <p role="alert" className="text-sm text-red-600">{billing.error.message}</p>

  const { planName, status, trialEndsAt, periodEnd, cancelAtPeriodEnd, limits } = billing.data
  return (
    <section aria-labelledby="billing-heading" className="rounded border border-gray-200 bg-white p-6">
      <h2 id="billing-heading" className="mb-4 text-lg font-semibold">Plan</h2>
      <p className="mb-1">
        <span className="font-medium">{planName}</span> · {statusText[status] ?? status}
      </p>
      {status === 'Trial' && (
        <p className="mb-1 text-sm text-gray-600">
          {daysLeft(trialEndsAt)} days of the trial left. Then this organisation moves to the Free plan; nothing is deleted.
        </p>
      )}
      {cancelAtPeriodEnd && (
        <p className="mb-1 text-sm text-gray-600">Cancelled: ends on {new Date(periodEnd).toLocaleDateString()}.</p>
      )}
      <ul className="my-4 space-y-3">
        {limits.map((limit) => {
          const max = limit.limit === null ? null : Number(limit.limit)
          const used = Number(limit.used)
          return (
            <li key={limit.name} className="text-sm">
              <div className="flex justify-between">
                <span>{limit.description}</span>
                <span className={limit.reached ? 'text-red-600' : limit.nearLimit ? 'text-amber-700' : 'text-gray-600'}>
                  {used.toLocaleString()} of {formatLimit(limit.limit)}
                </span>
              </div>
              {max !== null && max > 0 && (
                <div className="mt-1 h-1.5 rounded bg-gray-100">
                  <div
                    className={`h-1.5 rounded ${limit.reached ? 'bg-red-500' : limit.nearLimit ? 'bg-amber-500' : 'bg-indigo-500'}`}
                    style={{ width: `${Math.min(100, (used / max) * 100)}%` }}
                  />
                </div>
              )}
            </li>
          )
        })}
      </ul>
      <Link to="/pricing" className="text-sm text-indigo-600 hover:underline">
        Compare plans
      </Link>
    </section>
  )
}
