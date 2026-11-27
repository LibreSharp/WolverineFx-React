import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { meHandler, renderWithAuth, signIn } from '../auth/testing'
import { AdminPage } from './AdminPage'

type User = {
  id: string
  email: string
  displayName: string | null
  emailConfirmed: boolean
  invitationPending: boolean
  roles: string[]
  lockedUntil: string | null
  lockedByAdmin: boolean
}

const user = (overrides: Partial<User>): User => ({
  id: 'u',
  email: 'u@example.com',
  displayName: null,
  emailConfirmed: true,
  invitationPending: false,
  roles: [],
  lockedUntil: null,
  lockedByAdmin: false,
  ...overrides,
})

describe('AdminPage', () => {
  let users: User[]
  let lastSearch: string | null

  beforeEach(() => {
    signIn()
    lastSearch = null
    users = [
      user({ id: 'admin-1', email: 'admin@example.com', roles: ['Admin'] }),
      user({ id: 'u1', email: 'ada@example.com', displayName: 'Ada' }),
      user({ id: 'u2', email: 'invited@example.com', emailConfirmed: false, invitationPending: true }),
      user({ id: 'u3', email: 'locked@example.com', lockedUntil: '9999-12-31T23:59:59Z', lockedByAdmin: true }),
      user({ id: 'u4', email: 'guesser@example.com', lockedUntil: '2030-01-01T00:00:00Z' }),
    ]
    server.use(
      meHandler({ userId: 'admin-1', email: 'admin@example.com', roles: ['Admin'] }),
      http.get('*/api/admin/users', ({ request }) => {
        lastSearch = new URL(request.url).searchParams.get('search')
        const shown = users.filter((u) => !lastSearch || u.email.includes(lastSearch))
        return HttpResponse.json({ users: shown, total: shown.length, page: 1, pageSize: 25, allRoles: ['Admin'] })
      }),
    )
  })

  const row = async (email: string) => (await screen.findByText(email)).closest('li') as HTMLElement

  it('lists users with their status', async () => {
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')

    expect(within(await row('ada@example.com')).getByText('Active')).toBeInTheDocument()
    expect(within(await row('invited@example.com')).getByText('Invited')).toBeInTheDocument()
    expect(within(await row('locked@example.com')).getByText('Locked')).toBeInTheDocument()
    expect(within(await row('guesser@example.com')).getByText('Locked out (failed logins)')).toBeInTheDocument()
    expect(screen.getByText(/5 users · page 1 of 1/)).toBeInTheDocument()
  })

  it("doesn't let admins lock themselves", async () => {
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')
    expect(within(await row('admin@example.com')).getByRole('button', { name: 'Lock' })).toBeDisabled()
  })

  it('searches by email', async () => {
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')
    await row('ada@example.com')

    await userEvent.type(screen.getByPlaceholderText('Search by email'), 'ada')

    await waitFor(() => expect(screen.queryByText('invited@example.com')).not.toBeInTheDocument())
    expect(lastSearch).toBe('ada')
  })

  it('locks and unlocks accounts', async () => {
    const calls: string[] = []
    server.use(
      http.post('*/api/admin/users/:id/:action', ({ params }) => {
        calls.push(`${params.action} ${params.id}`)
        users = users.map((u) =>
          u.id === params.id
            ? params.action === 'lock'
              ? { ...u, lockedUntil: '9999-12-31T23:59:59Z', lockedByAdmin: true }
              : { ...u, lockedUntil: null, lockedByAdmin: false }
            : u,
        )
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')

    await userEvent.click(within(await row('ada@example.com')).getByRole('button', { name: 'Lock' }))
    await waitFor(() => expect(within(screen.getByText('ada@example.com').closest('li')!).getByText('Locked')).toBeInTheDocument())

    await userEvent.click(within(await row('locked@example.com')).getByRole('button', { name: 'Unlock' }))

    await waitFor(() => expect(calls).toEqual(['lock u1', 'unlock u3']))
  })

  it('changes roles, and shows why a change was refused', async () => {
    let saved: unknown
    server.use(
      http.put('*/api/admin/users/:id/roles', async ({ params, request }) => {
        if (params.id === 'admin-1') {
          return HttpResponse.json({ errors: { Roles: ["You can't remove your own Admin role."] } }, { status: 400 })
        }
        saved = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')

    await userEvent.click(within(await row('ada@example.com')).getByRole('checkbox', { name: 'Admin' }))
    await waitFor(() => expect(saved).toEqual({ roles: ['Admin'] }))

    await userEvent.click(within(await row('admin@example.com')).getByRole('checkbox', { name: 'Admin' }))
    expect(await screen.findByText("You can't remove your own Admin role.")).toBeInTheDocument()
  })

  it('invites someone with roles', async () => {
    let invitation: unknown
    server.use(
      http.post('*/api/admin/invitations', async ({ request }) => {
        invitation = await request.json()
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')
    await row('ada@example.com')

    const form = screen.getByRole('heading', { name: 'Invite someone' }).closest('section') as HTMLElement
    await userEvent.type(within(form).getByPlaceholderText('Email'), 'new@example.com')
    await userEvent.click(within(form).getByRole('checkbox', { name: 'Admin' }))
    await userEvent.click(within(form).getByRole('button', { name: 'Send invitation' }))

    expect(await screen.findByText('Invitation sent to new@example.com.')).toBeInTheDocument()
    expect(invitation).toEqual({ email: 'new@example.com', roles: ['Admin'] })
  })

  it('shows why an invitation was refused', async () => {
    server.use(
      http.post('*/api/admin/invitations', () =>
        HttpResponse.json({ errors: { Email: ['Someone with this email already has an account.'] } }, { status: 400 }),
      ),
    )
    renderWithAuth([{ path: '/admin', element: <AdminPage /> }], '/admin')
    await row('ada@example.com')

    const form = screen.getByRole('heading', { name: 'Invite someone' }).closest('section') as HTMLElement
    await userEvent.type(within(form).getByPlaceholderText('Email'), 'ada@example.com')
    await userEvent.click(within(form).getByRole('button', { name: 'Send invitation' }))

    expect(await screen.findByText('Someone with this email already has an account.')).toBeInTheDocument()
  })
})
