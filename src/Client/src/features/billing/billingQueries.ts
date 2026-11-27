import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

export const billingKeys = {
  summary: ['billing'] as const,
}

// The current organisation's plan, the features it includes and how much of each limit is used
// (GET /api/billing). The API enforces all of it; the client uses it to show or hide features and
// to offer an upgrade.
// waitingForPayment: just back from checkout, so check every two seconds until the webhook has
// moved the organisation onto its new plan.
export function useBilling({ waitingForPayment = false }: { waitingForPayment?: boolean } = {}) {
  return useQuery({
    queryKey: billingKeys.summary,
    refetchInterval: (query) => (waitingForPayment && query.state.data?.status !== 'Active' ? 2000 : false),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/billing')
      if (!response.ok || !data) throw new Error('Could not load the plan.')
      return data
    },
  })
}

export function daysLeft(until: string | null | undefined, now = Date.now()): number {
  return until ? Math.max(0, Math.ceil((new Date(until).getTime() - now) / 86_400_000)) : 0
}

// Sends the browser to the payment provider's page: checkout for a plan, or the customer portal.
// The plan changes when the provider's webhook arrives, a moment after the user comes back.
async function goToPaymentPage(request: Promise<{ data?: { url: string }; error?: unknown; response: Response }>, fallback: string) {
  const { data, error, response } = await request
  if (!response.ok || !data) throw new Error(problemMessage(error, fallback))
  window.location.assign(data.url)
}

export function useCheckout() {
  return useMutation({
    mutationFn: ({ planId, currency }: { planId: string; currency?: string }) =>
      goToPaymentPage(api.POST('/api/billing/checkout', { body: { planId, currency: currency ?? null } }), 'Could not start the checkout.'),
  })
}

export function usePortal() {
  return useMutation({
    mutationFn: () => goToPaymentPage(api.POST('/api/billing/portal'), 'Could not open billing.'),
  })
}
