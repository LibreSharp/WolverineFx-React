import { act, screen } from '@testing-library/react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { getTokens, setTokens } from '../../lib/api/authTokens'
import { server } from '../../test/server'
import { authOptionsKey } from './authQueries'
import { authOptionsHandler, renderWithAuth, signIn, testTokens } from './testing'
import { useAuth } from './useAuth'

function Probe() {
  const { isAuthenticated, logout } = useAuth()
  const queryClient = useQueryClient()
  useQuery({ queryKey: ['todos'], queryFn: () => ['private'], enabled: isAuthenticated })
  return (
    <>
      <p>{isAuthenticated ? 'signed in' : 'signed out'}</p>
      <p>todos cached: {String(queryClient.getQueryData(['todos']) !== undefined)}</p>
      <button type="button" onClick={() => void logout()}>Log out</button>
    </>
  )
}

describe('AuthProvider', () => {
  it('logging out revokes the refresh token and forgets the user, but keeps the public sign-in options', async () => {
    let revoked = ''
    server.use(
      authOptionsHandler(),
      http.post('*/api/auth/logout', async ({ request }) => {
        revoked = ((await request.json()) as { refreshToken: string }).refreshToken
        return new HttpResponse(null, { status: 204 })
      }),
    )
    signIn()
    const { queryClient } = renderWithAuth([{ path: '/', element: <Probe /> }])
    await queryClient.fetchQuery({ queryKey: authOptionsKey, queryFn: () => ({ allowRegistration: true, externalProviders: [] }) })
    expect(await screen.findByText('todos cached: true')).toBeInTheDocument()

    await act(async () => screen.getByRole('button', { name: 'Log out' }).click())

    expect(await screen.findByText('signed out')).toBeInTheDocument()
    expect(revoked).toBe(testTokens.refreshToken)
    expect(getTokens()).toBeNull()
    expect(queryClient.getQueryData(['todos'])).toBeUndefined()
    expect(queryClient.getQueryData(authOptionsKey)).toBeDefined()
  })

  it('keeps cached data when the page loads signed out', async () => {
    const { queryClient } = renderWithAuth([{ path: '/', element: <Probe /> }])
    queryClient.setQueryData(authOptionsKey, { allowRegistration: true, externalProviders: [] })
    act(() => setTokens(null))
    expect(queryClient.getQueryData(authOptionsKey)).toBeDefined()
  })
})
