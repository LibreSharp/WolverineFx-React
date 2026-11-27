import { useState } from 'react'
import { Link } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { formatLimit, formatPrice, planCatalogue, planName } from './plans'

// The page is pre-rendered at build time, so its text can't depend on the browser's language:
// prices and numbers are formatted for this locale, on the server and in the browser alike.
const locale = 'en'

// /pricing: public and pre-rendered, from the plan catalogue (plans.json).
export function PricingPage() {
  const { isAuthenticated } = useAuth()
  const currencies = [...new Set(planCatalogue.plans.flatMap((plan) => plan.prices.map((price) => price.currency)))]
  const [currency, setCurrency] = useState(planCatalogue.currency)

  return (
    <div className="mx-auto max-w-3xl px-6 py-16">
      <h1 className="mb-3 text-3xl font-semibold">Pricing</h1>
      {Number(planCatalogue.trialDays) > 0 && (
        <p className="mb-6 text-gray-600">
          Every new organisation gets {planName(planCatalogue.trialPlanId)} free for {planCatalogue.trialDays} days, then
          moves to Free unless you upgrade. Nothing is deleted when it does.
        </p>
      )}
      {currencies.length > 1 && (
        <label className="mb-6 flex items-center gap-2 text-sm text-gray-600">
          Currency
          <select value={currency} onChange={(event) => setCurrency(event.target.value)} className="rounded border border-gray-300 px-2 py-1">
            {currencies.map((code) => (
              <option key={code}>{code}</option>
            ))}
          </select>
        </label>
      )}
      <div className="grid gap-6 sm:grid-cols-2">
        {planCatalogue.plans.map((plan) => {
          const price = plan.prices.find((p) => p.currency === currency) ?? plan.prices[0]
          return (
            <section key={plan.id} aria-labelledby={`plan-${plan.id}`} className="flex flex-col rounded border border-gray-200 bg-white p-6">
              <h2 id={`plan-${plan.id}`} className="text-xl font-semibold">{plan.name}</h2>
              <p className="mb-4 text-sm text-gray-600">{plan.description}</p>
              <p className="mb-4 text-2xl font-semibold">
                {plan.isFree || !price ? 'Free' : <>{formatPrice(price.monthlyAmount, price.currency, locale)}<span className="text-base font-normal text-gray-500"> a month</span></>}
              </p>
              <ul className="mb-6 flex-1 space-y-1 text-sm">
                {plan.limits.map((limit) => (
                  <li key={limit.name}>
                    {limit.description}: {formatLimit(limit.limit, locale)}
                  </li>
                ))}
                {plan.features.map((feature) => (
                  <li key={feature.name} className={feature.included ? '' : 'text-gray-400 line-through'}>
                    {feature.description}
                  </li>
                ))}
              </ul>
              <Link
                to={isAuthenticated ? '/organisation' : '/register'}
                className="rounded bg-indigo-600 px-4 py-2 text-center text-white hover:bg-indigo-700"
              >
                {isAuthenticated ? 'Choose in your organisation' : plan.isFree ? 'Get started' : 'Start your free trial'}
              </Link>
            </section>
          )
        })}
      </div>
    </div>
  )
}
