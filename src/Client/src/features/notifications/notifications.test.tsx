import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { server } from '../../test/server'
import { renderPage } from '../../test/render'
import { signIn } from '../auth/testing'
import { EmailPreferencesSection } from './EmailPreferencesSection'
import { NotificationBell } from './NotificationBell'
import { UnsubscribePage } from './UnsubscribePage'

// No realtime connection in unit tests.
vi.mock('../../lib/realtime', () => ({ useRealtime: vi.fn() }))

const notification = (id: string, title: string, read: boolean) => ({
  id,
  category: 'reminders',
  title,
  body: null,
  link: '/todos',
  createdAt: '2026-11-28T09:00:00Z',
  read,
})

describe('NotificationBell', () => {
  beforeEach(signIn)

  it('shows how many are unread, opens one at its page and marks it read', async () => {
    let markedRead: string | undefined
    server.use(
      http.get('*/api/notifications', () =>
        HttpResponse.json({ notifications: [notification('n1', 'Reminder: call the bank', false), notification('n2', 'Older one', true)], unread: 1 }),
      ),
      http.post('*/api/notifications/:id/read', ({ params }) => {
        markedRead = params.id as string
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router } = renderPage(<NotificationBell />, '/', ['/todos'])

    await userEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }))
    expect(screen.getByText('Older one')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('menuitem', { name: /Reminder: call the bank/ }))

    await waitFor(() => expect(markedRead).toBe('n1'))
    expect(router.state.location.pathname).toBe('/todos')
  })

  it('marks them all read', async () => {
    let allRead = false
    server.use(
      http.get('*/api/notifications', () => HttpResponse.json({ notifications: [notification('n1', 'New', false)], unread: 1 })),
      http.post('*/api/notifications/read-all', () => {
        allRead = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderPage(<NotificationBell />)

    await userEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }))
    await userEvent.click(screen.getByRole('button', { name: 'Mark all read' }))

    await waitFor(() => expect(allRead).toBe(true))
  })
})

describe('EmailPreferencesSection', () => {
  beforeEach(signIn)

  it('turns a kind of email off', async () => {
    let saved: unknown
    server.use(
      http.get('*/api/account/email-preferences', () =>
        HttpResponse.json({ categories: [{ category: 'reminders', description: 'Todo reminders you set', email: true }] }),
      ),
      http.put('*/api/account/email-preferences', async ({ request }) => {
        saved = await request.json()
        return HttpResponse.json({ categories: [{ category: 'reminders', description: 'Todo reminders you set', email: false }] })
      }),
    )
    renderPage(<EmailPreferencesSection />)

    const checkbox = await screen.findByRole('checkbox', { name: 'Todo reminders you set' })
    expect(checkbox).toBeChecked()
    await userEvent.click(checkbox)

    await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Todo reminders you set' })).not.toBeChecked())
    expect(saved).toEqual({ categories: [{ category: 'reminders', description: '', email: false }] })
  })
})

describe('UnsubscribePage', () => {
  it('unsubscribes with the link’s token, after asking', async () => {
    let token: string | null = null
    server.use(
      http.post('*/api/notifications/unsubscribe', ({ request }) => {
        token = new URL(request.url).searchParams.get('token')
        return HttpResponse.json({ category: 'reminders', description: 'Todo reminders you set' })
      }),
    )
    renderPage(<UnsubscribePage />, '/unsubscribe?token=abc', ['/settings'])

    await userEvent.click(screen.getByRole('button', { name: 'Unsubscribe' }))

    expect(await screen.findByRole('status')).toHaveTextContent('todo reminders you set')
    expect(token).toBe('abc')
  })

  it('says when the link is not valid', async () => {
    server.use(http.post('*/api/notifications/unsubscribe', () => new HttpResponse(null, { status: 404 })))
    renderPage(<UnsubscribePage />, '/unsubscribe?token=nope')

    await userEvent.click(screen.getByRole('button', { name: 'Unsubscribe' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('not valid')
  })
})
