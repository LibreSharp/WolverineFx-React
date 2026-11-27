import type { components } from '../../lib/api/schema'
import catalogue from './plans.json'

export type PlanCatalogue = components['schemas']['PlanCatalogueResponse']
export type PlanOffer = components['schemas']['PlanOffer']
export type LimitUsage = components['schemas']['LimitUsage']

// The plans on offer, from the API's plan catalogue (Billing/Plans.cs). A copy, so the pricing
// page is pre-rendered with them; the API tests fail if it no longer matches (see docs/billing.md).
export const planCatalogue = catalogue as PlanCatalogue

// A price in the currency's smallest unit (cents), as the API gives it, for display: "$12", or
// "$12.50" when there are cents.
export function formatPrice(amount: number | string, currency: string, locale?: string): string {
  const digits = new Intl.NumberFormat(locale, { style: 'currency', currency }).resolvedOptions().maximumFractionDigits ?? 2
  const value = Number(amount) / 10 ** digits
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: Number.isInteger(value) ? 0 : digits,
  }).format(value)
}

export function formatLimit(limit: number | string | null | undefined, locale?: string): string {
  return limit === null || limit === undefined ? 'Unlimited' : Number(limit).toLocaleString(locale)
}

export const planName = (id: string) => planCatalogue.plans.find((plan) => plan.id === id)?.name ?? id
