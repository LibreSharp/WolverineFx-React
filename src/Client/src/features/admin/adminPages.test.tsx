import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { renderRoutes } from '../../test/render'
import { AdminLayout } from './AdminLayout'
import { adminSections } from './adminSections'
import { AuditLogPage } from './AuditLogPage'
import { FailedMessagesPage } from './FailedMessagesPage'

const failed = {
  id: '0193f2d4-0000-7000-8000-000000000001',
  messageType: 'ApiHost.Identity.Email.SendEmailConfirmation',
  exceptionType: 'System.Net.Sockets.SocketException',
  exceptionMessage: 'Connection refused',
  sentAt: '2026-11-27T10:00:00Z',
  failedAt: '2026-11-27T10:00:05Z',
  source: null,
  replayable: true,
  replayCount: 0,
}

describe('AdminLayout', () => {
  it("shows the back office's sections as tabs around the page", () => {
    renderRoutes([{ path: '/admin', element: <AdminLayout />, children: [{ index: true, element: <p>Section page</p> }] }], '/admin')

    const tabs = within(screen.getByRole('navigation', { name: 'Admin' })).getAllByRole('link')
    expect(tabs.map((tab) => tab.textContent)).toEqual(adminSections.map((section) => section.label))
    expect(screen.getByText('Section page')).toBeInTheDocument()
  })
})

describe('FailedMessagesPage', () => {
  it('lists failed messages and replays one', async () => {
    let replayed: unknown
    server.use(
      http.get('*/api/admin/failed-messages', () => HttpResponse.json({ messages: [failed], total: 1, page: 1, pageSize: 25 })),
      http.post('*/api/admin/failed-messages/replay', async ({ request }) => {
        replayed = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutes([{ path: '/', element: <FailedMessagesPage /> }])

    expect(await screen.findByText('SendEmailConfirmation')).toBeInTheDocument()
    expect(screen.getByText(/SocketException: Connection refused/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Replay' }))

    await waitFor(() => expect(replayed).toEqual({ ids: [failed.id] }))
    expect(await screen.findByRole('status')).toHaveTextContent('Sent again')
  })
})

describe('AuditLogPage', () => {
  it('shows who did what', async () => {
    server.use(
      http.get('*/api/admin/audit', () =>
        HttpResponse.json({
          entries: [{ id: '1', at: '2026-11-27T10:00:00Z', actorId: 'a', actorEmail: 'admin@example.com', action: 'user.lock', targetType: 'User', targetId: 'u', details: 'ada@example.com' }],
          total: 1,
          page: 1,
          pageSize: 50,
        }),
      ),
    )
    renderRoutes([{ path: '/', element: <AuditLogPage /> }])

    const row = (await screen.findByText('user.lock')).closest('tr') as HTMLElement
    expect(within(row).getByText('admin@example.com')).toBeInTheDocument()
    expect(within(row).getByText('ada@example.com')).toBeInTheDocument()
  })
})
