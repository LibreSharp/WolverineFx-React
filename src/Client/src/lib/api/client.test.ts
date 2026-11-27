import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { getTokens, setTokens } from './authTokens'
import { api, onOrganisationLost } from './client'

const oldTokens = { accessToken: 'old-access', refreshToken: 'old-refresh', expiresAtUtc: '2030-01-01T00:00:00Z' }
const newTokens = { accessToken: 'new-access', refreshToken: 'new-refresh', expiresAtUtc: '2030-01-01T00:15:00Z' }

// An access token for an organisation (only its "org" claim is read).
const tokenFor = (organisationId: string) => `header.${btoa(JSON.stringify({ org: organisationId }))}.signature`

describe('api client', () => {
  it('sends the access token', async () => {
    let authorization: string | null = null
    server.use(
      http.get('*/api/todos', ({ request }) => {
        authorization = request.headers.get('Authorization')
        return HttpResponse.json([])
      }),
    )
    setTokens(oldTokens)

    await api.GET('/api/todos')

    expect(authorization).toBe('Bearer old-access')
  })

  it('refreshes an expired access token once and retries the request', async () => {
    const seen: (string | null)[] = []
    let refreshes = 0
    server.use(
      http.get('*/api/todos', ({ request }) => {
        seen.push(request.headers.get('Authorization'))
        return request.headers.get('Authorization') === 'Bearer new-access'
          ? HttpResponse.json([{ id: '1', title: 'Hi', isComplete: false }])
          : new HttpResponse(null, { status: 401 })
      }),
      http.post('*/api/auth/refresh', async ({ request }) => {
        refreshes++
        expect(await request.json()).toEqual({ refreshToken: 'old-refresh' })
        return HttpResponse.json(newTokens)
      }),
    )
    setTokens(oldTokens)

    // Two requests at once share one refresh (refresh tokens are single-use).
    const [first, second] = await Promise.all([api.GET('/api/todos'), api.GET('/api/todos')])

    expect(first.response.status).toBe(200)
    expect(second.response.status).toBe(200)
    expect(refreshes).toBe(1)
    expect(getTokens()).toEqual(newTokens)
    expect(seen.filter((h) => h === 'Bearer new-access')).toHaveLength(2)
  })

  it('signs out when the refresh token is refused', async () => {
    server.use(
      http.get('*/api/todos', () => new HttpResponse(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => new HttpResponse(null, { status: 401 })),
    )
    setTokens(oldTokens)

    const { response } = await api.GET('/api/todos')

    expect(response.status).toBe(401)
    expect(getTokens()).toBeNull()
  })

  it("doesn't repeat a request in another organisation after losing access to the first", async () => {
    const created: string[] = []
    server.use(
      http.post('*/api/todos', ({ request }) => {
        if (request.headers.get('Authorization') === `Bearer ${tokenFor('team')}`) {
          return new HttpResponse(null, { status: 401 })
        }
        created.push(request.headers.get('Authorization') ?? '')
        return HttpResponse.json({ id: '1', url: '/api/todos/1' }, { status: 201 })
      }),
      // Removed from "team": refreshing moves the session to the user's own organisation.
      http.post('*/api/auth/refresh', () => HttpResponse.json({ ...newTokens, accessToken: tokenFor('own') })),
    )
    setTokens({ ...oldTokens, accessToken: tokenFor('team') })
    let lost = 0
    const unsubscribe = onOrganisationLost(() => lost++)

    const { response } = await api.POST('/api/todos', { body: { title: 'For the team' } })
    unsubscribe()

    expect(response.status).toBe(401)
    expect(created).toEqual([])
    expect(lost).toBe(1)
    expect(getTokens()?.accessToken).toBe(tokenFor('own'))
  })

  it("doesn't try to refresh when signed out", async () => {
    server.use(http.get('*/api/todos', () => new HttpResponse(null, { status: 401 })))

    const { response } = await api.GET('/api/todos')

    expect(response.status).toBe(401)
  })
})
