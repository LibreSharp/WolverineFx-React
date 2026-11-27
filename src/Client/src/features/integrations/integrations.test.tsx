import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { renderPage } from '../../test/render'
import { signIn } from '../auth/testing'
import { IntegrationsPage } from './IntegrationsPage'

const organisation = (role: string) => http.get('*/api/organisation', () => HttpResponse.json({ id: 'org-1', name: 'Acme', role }))

const apiKey = { id: 'k1', name: 'Deploys', hint: 'sk_AbCd1234', scope: 'write', createdAt: '2026-11-01T00:00:00Z', expiresAt: null, lastUsedAt: null, expired: false }
const webhook = { id: 'w1', url: 'https://example.com/hooks', description: '', events: [], enabled: true, createdAt: '2026-11-01T00:00:00Z' }

describe('IntegrationsPage', () => {
  beforeEach(() => {
    signIn()
    server.use(
      http.get('*/api/organisation/api-keys', () => HttpResponse.json([])),
      http.get('*/api/organisation/webhooks', () => HttpResponse.json([])),
      http.get('*/api/organisation/webhooks/events', () => HttpResponse.json([{ type: 'todo.created', description: 'A Todo was added.' }])),
      http.get('*/api/organisation/secrets', () => HttpResponse.json([])),
    )
  })

  it('creates an API key and shows it once', async () => {
    let created: unknown
    server.use(
      organisation('Owner'),
      http.post('*/api/organisation/api-keys', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json({ apiKey: { ...apiKey, scope: 'read' }, key: 'sk_AbCd1234secret' })
      }),
    )
    renderPage(<IntegrationsPage />)

    await userEvent.type(await screen.findByLabelText('Key name'), 'Deploys')
    await userEvent.click(screen.getByRole('button', { name: 'Create key' }))

    expect(await screen.findByLabelText(/Your new key “Deploys”/)).toHaveTextContent('sk_AbCd1234secret')
    expect(created).toEqual({ name: 'Deploys', scope: 'read', expiresInDays: null })
  })

  it('rotates and revokes keys', async () => {
    let revoked = false
    server.use(
      organisation('Admin'),
      http.get('*/api/organisation/api-keys', () => HttpResponse.json(revoked ? [] : [apiKey])),
      http.post('*/api/organisation/api-keys/k1/rotate', () => HttpResponse.json({ apiKey, key: 'sk_rotated' })),
      http.delete('*/api/organisation/api-keys/k1', () => {
        revoked = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderPage(<IntegrationsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Rotate' }))
    expect(await screen.findByLabelText(/Your new key/)).toHaveTextContent('sk_rotated')

    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }))
    await waitFor(() => expect(screen.queryByText('Deploys')).not.toBeInTheDocument())
  })

  it('adds a webhook for chosen events and shows its signing secret', async () => {
    let added: unknown
    server.use(
      organisation('Owner'),
      http.post('*/api/organisation/webhooks', async ({ request }) => {
        added = await request.json()
        return HttpResponse.json({ webhook, secret: 'whsec_shown' })
      }),
    )
    renderPage(<IntegrationsPage />)

    await userEvent.type(await screen.findByLabelText('Webhook URL'), 'https://example.com/hooks')
    await userEvent.click(await screen.findByRole('checkbox', { name: /A Todo was added/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Add webhook' }))

    expect(await screen.findByLabelText(/signing secret/)).toHaveTextContent('whsec_shown')
    expect(added).toEqual({ url: 'https://example.com/hooks', description: '', events: ['todo.created'], enabled: true })
  })

  it('shows failed deliveries and resends them', async () => {
    let resent = false
    server.use(
      organisation('Owner'),
      http.get('*/api/organisation/webhooks', () => HttpResponse.json([webhook])),
      http.get('*/api/organisation/webhooks/w1/deliveries', () =>
        HttpResponse.json([{ id: 'd1', eventId: 'e1', eventType: 'todo.created', createdAt: '2026-11-28T09:00:00Z', attempts: 2, lastAttemptAt: '2026-11-28T09:01:00Z', statusCode: 500, response: 'down', deliveredAt: null, payload: '{}' }]),
      ),
      http.post('*/api/organisation/webhooks/deliveries/d1/resend', () => {
        resent = true
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderPage(<IntegrationsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Deliveries' }))
    const item = (await screen.findByText('Failed (500), 2 tries')).closest('li')!
    await userEvent.click(within(item).getByRole('button', { name: 'Resend' }))

    await waitFor(() => expect(resent).toBe(true))
  })

  it('keeps secrets to owners, and integrations to owners and admins', async () => {
    server.use(organisation('Admin'))
    const { unmount } = renderPage(<IntegrationsPage />)
    expect(await screen.findByRole('heading', { name: 'API keys' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Secrets' })).not.toBeInTheDocument()
    unmount()

    server.use(organisation('Member'))
    renderPage(<IntegrationsPage />)
    expect(await screen.findByText('Only owners and admins can manage integrations.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'API keys' })).not.toBeInTheDocument()
  })
})
