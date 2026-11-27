import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { getTokens, setTokens } from '../../lib/api/authTokens'
import { server } from '../../test/server'
import { meHandler, renderWithAuth, signIn } from '../auth/testing'
import { billingHandler } from '../billing/testing'
import { JoinPage } from './JoinPage'
import { OrganisationPage } from './OrganisationPage'
import { OrganisationSwitcher } from './OrganisationSwitcher'

const organisations = [
  { id: 'org-1', name: 'Personal', role: 'Owner', isCurrent: true },
  { id: 'org-2', name: 'Team', role: 'Member', isCurrent: false },
]

const member = (userId: string, email: string, role: string) => ({ userId, email, displayName: null, role, joinedAt: '2026-01-01T00:00:00Z' })

describe('OrganisationSwitcher', () => {
  beforeEach(signIn)

  it('switches to another organisation with the session refresh token', async () => {
    let switchedWith: unknown
    server.use(
      http.get('*/api/organisations', () => HttpResponse.json(organisations)),
      http.post('*/api/organisations/switch', async ({ request }) => {
        switchedWith = await request.json()
        return HttpResponse.json({ accessToken: 'access-2', refreshToken: 'refresh-2', expiresAtUtc: '2030-01-01T00:00:00Z' })
      }),
    )
    renderWithAuth([{ path: '/', element: <OrganisationSwitcher /> }])

    await userEvent.selectOptions(await screen.findByLabelText('Organisation'), 'Team')

    await waitFor(() => expect(getTokens()?.refreshToken).toBe('refresh-2'))
    expect(switchedWith).toEqual({ organisationId: 'org-2', refreshToken: 'refresh-1' })
  })
})

describe('OrganisationPage', () => {
  beforeEach(() => {
    signIn()
    server.use(
      meHandler({ userId: 'me' }),
      http.get('*/api/organisation/members', () =>
        HttpResponse.json([member('me', 'me@example.com', 'Admin'), member('o', 'owner@example.com', 'Owner'), member('m', 'm@example.com', 'Member')]),
      ),
      http.get('*/api/organisation/invitations', () => HttpResponse.json([])),
      billingHandler(),
    )
  })

  it('lets an admin manage members but not owners, and not invite owners', async () => {
    server.use(http.get('*/api/organisation', () => HttpResponse.json({ id: 'org-2', name: 'Team', role: 'Admin' })))
    renderWithAuth([{ path: '/', element: <OrganisationPage /> }])

    expect(await screen.findByLabelText('Role of m@example.com')).toBeInTheDocument()
    expect(screen.queryByLabelText('Role of owner@example.com')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Invite people' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'Owner' })).not.toBeInTheDocument()
    // Only owners rename or delete.
    expect(screen.queryByRole('button', { name: 'Rename' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete organisation' })).not.toBeInTheDocument()
  })

  it('sends an invitation', async () => {
    let invited: unknown
    server.use(
      http.get('*/api/organisation', () => HttpResponse.json({ id: 'org-2', name: 'Team', role: 'Owner' })),
      http.post('*/api/organisation/invitations', async ({ request }) => {
        invited = await request.json()
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderWithAuth([{ path: '/', element: <OrganisationPage /> }])

    await userEvent.type(await screen.findByLabelText('Email to invite'), 'new@example.com')
    await userEvent.selectOptions(screen.getByLabelText('Role'), 'Admin')
    await userEvent.click(screen.getByRole('button', { name: 'Invite' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Invitation sent.')
    expect(invited).toEqual({ email: 'new@example.com', role: 'Admin' })
  })
})

describe('JoinPage', () => {
  const details = (accountExists: boolean) =>
    http.post('*/api/join/details', () => HttpResponse.json({ organisationName: 'Team', email: 'new@example.com', role: 'Member', accountExists }))

  it('creates an account from the invitation and signs in', async () => {
    server.use(
      details(false),
      http.post('*/api/join/register', () => HttpResponse.json({ accessToken: 'a', refreshToken: 'r', expiresAtUtc: '2030-01-01T00:00:00Z' })),
    )
    const { router } = renderWithAuth([{ path: '/join', element: <JoinPage /> }, { path: '/todos', element: <p>Todos</p> }], '/join?token=t1')

    await userEvent.type(await screen.findByLabelText('Choose a password'), 'P@ssword123!')
    await userEvent.click(screen.getByRole('button', { name: 'Create account and join' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/todos'))
    expect(getTokens()?.refreshToken).toBe('r')
  })

  it('asks someone with an account to log in first', async () => {
    server.use(details(true))
    renderWithAuth([{ path: '/join', element: <JoinPage /> }], '/join?token=t1')

    expect(await screen.findByRole('link', { name: 'Log in to accept' })).toBeInTheDocument()
  })

  it('says when the link is no longer valid', async () => {
    setTokens(null)
    server.use(http.post('*/api/join/details', () => new HttpResponse(null, { status: 404 })))
    renderWithAuth([{ path: '/join', element: <JoinPage /> }], '/join?token=old')

    expect(await screen.findByRole('alert')).toHaveTextContent('invalid or has expired')
  })
})
