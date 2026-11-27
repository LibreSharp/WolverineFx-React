import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api/client'

export const billingKeys = {
  summary: ['billing'] as const,
}

// The current organisation's plan, the features it includes and how much of each limit is used
// (GET /api/billing). The API enforces all of it; the client uses it to show or hide features and
// to offer an upgrade.
export function useBilling() {
  return useQuery({
    queryKey: billingKeys.summary,
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
