import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { setTokens } from '../../lib/api/authTokens'
import { freshAccessToken } from './useTodoRealtime'

// The hub closes a connection when its access token expires, so reconnecting needs a fresh one.
describe('freshAccessToken', () => {
  const inMinutes = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString()

  it('uses the current access token while it has time left', async () => {
    setTokens({ accessToken: 'current', refreshToken: 'refresh', expiresAtUtc: inMinutes(10) })

    expect(await freshAccessToken()).toBe('current')
  })

  it('refreshes an access token that has expired or is about to', async () => {
    server.use(
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json({ accessToken: 'refreshed', refreshToken: 'refresh-2', expiresAtUtc: inMinutes(15) }),
      ),
    )
    setTokens({ accessToken: 'expiring', refreshToken: 'refresh', expiresAtUtc: inMinutes(0.1) })

    expect(await freshAccessToken()).toBe('refreshed')
  })

  it('connects without a token when signed out', async () => {
    setTokens(null)

    expect(await freshAccessToken()).toBe('')
  })
})
