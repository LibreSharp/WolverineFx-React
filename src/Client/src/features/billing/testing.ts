import { http, HttpResponse } from 'msw'
import type { components } from '../../lib/api/schema'

type BillingSummary = components['schemas']['BillingSummary']

// GET /api/billing for unit tests: a Pro trial with room in every limit, unless overridden.
export function billingHandler(summary: Partial<BillingSummary> = {}) {
  return http.get('*/api/billing', () =>
    HttpResponse.json({
      planId: 'pro',
      planVersion: 1,
      planName: 'Pro',
      status: 'Trial',
      trialEndsAt: new Date(Date.now() + 10 * 86_400_000).toISOString(),
      periodStart: '2026-01-01T00:00:00Z',
      periodEnd: '2026-02-01T00:00:00Z',
      cancelAtPeriodEnd: false,
      features: ['TodoReminders'],
      limits: [
        { name: 'OpenTodos', kind: 'Count', description: 'Open Todos', limit: null, used: 1, nearLimit: false, reached: false },
        { name: 'Members', kind: 'Count', description: 'Members', limit: 50, used: 1, nearLimit: false, reached: false },
        { name: 'Reminders', kind: 'PerPeriod', description: 'Reminders a month', limit: 1000, used: 0, nearLimit: false, reached: false },
      ],
      canManage: true,
      ...summary,
    } satisfies BillingSummary),
  )
}
