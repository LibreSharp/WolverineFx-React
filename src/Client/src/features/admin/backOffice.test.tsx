import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { renderRoutes } from '../../test/render'
import { DashboardPage } from './DashboardPage'
import { FeatureFlagsPage } from './FeatureFlagsPage'
import { OrganisationDetailPage } from './OrganisationDetailPage'

const organisationId = '0193f2d4-0000-7000-8000-0000000000aa'

function detail(billing: Record<string, unknown> = {}) {
  return {
    id: organisationId,
    name: 'Acme',
    createdAt: '2026-11-01T00:00:00Z',
    suspendedAt: null,
    suspensionReason: null,
    notes: null,
    members: [{ userId: 'u', email: 'owner@example.com', displayName: null, role: 'Owner', joinedAt: '2026-11-01T00:00:00Z' }],
    billing: {
      planId: 'free',
      planName: 'Free',
      planVersion: 1,
      status: 'Free',
      trialEndsAt: null,
      periodStart: '2026-11-01T00:00:00Z',
      periodEnd: '2026-12-01T00:00:00Z',
      cancelAtPeriodEnd: false,
      currency: null,
      customerUrl: null,
      subscriptionUrl: null,
      ...billing,
    },
    limits: [{ name: 'Members', kind: 'Count', description: 'Members', limit: 3, used: 1, nearLimit: false, reached: false }],
    invoices: [],
    features: [{ name: 'TodoReminders', description: 'Todo reminders', enabled: false, override: null }],
  }
}

function renderDetail() {
  server.use(http.get('*/api/admin/audit', () => HttpResponse.json({ entries: [], total: 0, page: 1, pageSize: 50 })))
  return renderRoutes([{ path: '/admin/organisations/:id', element: <OrganisationDetailPage /> }], `/admin/organisations/${organisationId}`)
}

describe('DashboardPage', () => {
  it('shows revenue in each currency and the counts', async () => {
    server.use(
      http.get('*/api/admin/dashboard', () =>
        HttpResponse.json({
          organisations: 40,
          newOrganisations: 5,
          activeOrganisations: 12,
          payingOrganisations: 3,
          monthlyRevenue: [
            { currency: 'EUR', monthlyAmount: 1100 },
            { currency: 'USD', monthlyAmount: 2400 },
          ],
          trialsEnding: 2,
          failedPayments: 1,
          failedMessages: 0,
        }),
      ),
    )
    renderRoutes([{ path: '/', element: <DashboardPage /> }])

    expect(await screen.findByText(/€11.*\+.*\$24/)).toBeInTheDocument()
    expect(screen.getByText('5 new in the last 30 days')).toBeInTheDocument()
  })
})

describe('OrganisationDetailPage', () => {
  it('gives a plan until a date, and overrides a feature for the organisation', async () => {
    let granted: unknown
    let feature: unknown
    server.use(
      http.get('*/api/admin/organisations/:id', () => HttpResponse.json(detail())),
      http.post('*/api/admin/organisations/:id/grant', async ({ request }) => {
        granted = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
      http.put('*/api/admin/features/:feature', async ({ request, params }) => {
        feature = { name: params.feature, ...((await request.json()) as object) }
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderDetail()

    const until = await screen.findByLabelText('Until')
    await userEvent.clear(until)
    await userEvent.type(until, '2027-01-31')
    await userEvent.click(screen.getByRole('button', { name: 'Give the plan' }))
    await waitFor(() => expect(granted).toEqual({ planId: 'pro', until: '2027-01-31T23:59:59.000Z' }))

    await userEvent.selectOptions(screen.getByLabelText('Todo reminders for Acme'), 'On')
    await waitFor(() => expect(feature).toEqual({ name: 'TodoReminders', scope: 'Organisation', scopeKey: organisationId, enabled: true }))
  })

  it("leaves a subscription's plan to the payment provider", async () => {
    server.use(http.get('*/api/admin/organisations/:id', () => HttpResponse.json(detail({ planId: 'pro', planName: 'Pro', status: 'Active', customerUrl: 'https://dashboard.stripe.com/customers/cus_1' }))))
    renderDetail()

    expect(await screen.findByText(/change its plan, or refund it, with the payment provider/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Give the plan/ })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Customer' })).toHaveAttribute('href', 'https://dashboard.stripe.com/customers/cus_1')
  })

  it('deletes only once the name is typed', async () => {
    server.use(http.get('*/api/admin/organisations/:id', () => HttpResponse.json(detail())))
    renderDetail()

    const remove = await screen.findByRole('button', { name: 'Delete' })
    expect(remove).toBeDisabled()
    await userEvent.type(screen.getByLabelText(/Type the organisation's name/), 'Acme')
    expect(remove).toBeEnabled()
  })
})

describe('FeatureFlagsPage', () => {
  it('turns a feature off for everyone', async () => {
    let body: unknown
    server.use(
      http.get('*/api/admin/features', () =>
        HttpResponse.json([
          {
            name: 'TodoReminders',
            description: 'Todo reminders',
            global: null,
            plans: [
              { planId: 'free', planName: 'Free', included: false, override: null },
              { planId: 'pro', planName: 'Pro', included: true, override: null },
            ],
            organisationOverrides: 0,
          },
        ]),
      ),
      http.put('*/api/admin/features/:feature', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutes([{ path: '/', element: <FeatureFlagsPage /> }])

    await userEvent.selectOptions(await screen.findByLabelText('Todo reminders for everyone'), 'Off')

    await waitFor(() => expect(body).toEqual({ scope: 'Global', scopeKey: null, enabled: false }))
    expect(screen.getByLabelText('Todo reminders on Pro')).toHaveDisplayValue('Default (on)')
  })
})
