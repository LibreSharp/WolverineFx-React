import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { GuestOnly, RequireAdmin, RequireAuth } from './routeGuards'
import { meHandler, renderWithAuth, signIn } from './testing'

const routes = [
  { path: '/login', element: <p>Login page</p> },
  { path: '/todos', element: <p>Todos page</p> },
  {
    element: <RequireAuth />,
    children: [
      { path: '/settings', element: <p>Settings page</p> },
      { element: <RequireAdmin />, children: [{ path: '/admin', element: <p>Admin page</p> }] },
    ],
  },
  { element: <GuestOnly />, children: [{ path: '/register', element: <p>Register page</p> }] },
]

describe('route guards', () => {
  it('sends signed-out users to the login page', async () => {
    const { router } = renderWithAuth(routes, '/settings')

    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/settings' } })
  })

  it('lets signed-in users through', async () => {
    signIn()
    renderWithAuth(routes, '/settings')
    expect(await screen.findByText('Settings page')).toBeInTheDocument()
  })

  it('sends signed-in users away from the signed-out pages', async () => {
    signIn()
    renderWithAuth(routes, '/register')
    expect(await screen.findByText('Todos page')).toBeInTheDocument()
  })

  it('shows the admin page only to admins', async () => {
    signIn()
    server.use(meHandler({ roles: ['Admin'] }))
    renderWithAuth(routes, '/admin')
    expect(await screen.findByText('Admin page')).toBeInTheDocument()
  })

  it('shows not found to everyone else', async () => {
    signIn()
    server.use(meHandler({ roles: [] }))
    renderWithAuth(routes, '/admin')
    expect(await screen.findByText(/not found/i)).toBeInTheDocument()
    expect(screen.queryByText('Admin page')).not.toBeInTheDocument()
  })
})
