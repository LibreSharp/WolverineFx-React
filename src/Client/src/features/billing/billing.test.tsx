import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { server } from '../../test/server'
import { renderPage } from '../../test/render'
import { renderWithAuth } from '../auth/testing'
import { BillingSection } from './BillingSection'
import { PlanTodoList } from './PlanTodoList'
import { PricingPage } from './PricingPage'
import { formatPrice } from './plans'
import { billingHandler } from './testing'

vi.mock('../todos/useTodoRealtime', () => ({ useTodoRealtime: () => ({ reminders: [], dismiss: vi.fn() }) }))

describe('formatPrice', () => {
  it('turns the smallest unit into the currency amount', () => {
    expect(formatPrice(1200, 'USD', 'en-US')).toBe('$12')
    expect(formatPrice(1250, 'USD', 'en-US')).toBe('$12.50')
    expect(formatPrice(500, 'JPY', 'en-US')).toBe('¥500')
  })
})

describe('PricingPage', () => {
  it('lists the plans from the catalogue, with the trial', () => {
    renderWithAuth([{ path: '/', element: <PricingPage /> }])

    const pro = screen.getByRole('region', { name: 'Pro' })
    expect(pro).toHaveTextContent('$12 a month')
    expect(within(pro).getByText('Todo reminders')).not.toHaveClass('line-through')
    expect(within(screen.getByRole('region', { name: 'Free' })).getByText('Todo reminders')).toHaveClass('line-through')
    expect(screen.getByText(/free for 14 days/)).toBeInTheDocument()
    expect(within(pro).getByRole('link', { name: 'Start your free trial' })).toHaveAttribute('href', '/register')
  })
})

describe('BillingSection', () => {
  it('shows the trial and how much of each limit is used', async () => {
    server.use(billingHandler())
    renderPage(<BillingSection />)

    expect(await screen.findByText(/days of the trial left/)).toBeInTheDocument()
    expect(screen.getByText('1 of 50')).toBeInTheDocument()
    expect(screen.getByText('1 of Unlimited')).toBeInTheDocument()
  })
})

describe('PlanTodoList', () => {
  beforeEach(() => {
    server.use(http.get('*/api/todos', () => HttpResponse.json([{ id: '1', title: 'Write tests', isComplete: false }])))
  })

  it('offers reminders when the plan includes them', async () => {
    server.use(billingHandler())
    renderPage(<PlanTodoList />, '/todos')

    expect(await screen.findByLabelText('Remind me about Write tests')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('offers an upgrade instead of reminders, and warns near the open Todos limit', async () => {
    server.use(
      billingHandler({
        planId: 'free',
        planName: 'Free',
        status: 'Free',
        trialEndsAt: null,
        features: [],
        limits: [{ name: 'OpenTodos', kind: 'Count', description: 'Open Todos', limit: 25, used: 21, nearLimit: true, reached: false }],
      }),
    )
    renderPage(<PlanTodoList />, '/todos')

    expect(await screen.findByRole('link', { name: 'Reminders: upgrade' })).toHaveAttribute('href', '/pricing')
    expect(screen.queryByLabelText('Remind me about Write tests')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent("21 of the Free plan's 25 open Todos used")
  })
})
