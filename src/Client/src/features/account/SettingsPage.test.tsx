import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getTokens } from '../../lib/api/authTokens'
import { server } from '../../test/server'
import { authOptionsHandler, meHandler, removePasskeyBrowser, renderWithAuth, signIn, stubPasskeyBrowser, testTokens } from '../auth/testing'
import { SettingsPage } from './SettingsPage'

const newTokens = { ...testTokens, accessToken: 'access-2', refreshToken: 'refresh-2' }

function renderSettings() {
  return renderWithAuth(
    [
      { path: '/settings', element: <SettingsPage /> },
      { path: '/', element: <p>Home page</p> },
    ],
    '/settings',
  )
}

describe('SettingsPage', () => {
  beforeEach(() => {
    signIn()
    server.use(
      meHandler({ email: 'ada@example.com', displayName: 'Ada' }),
      authOptionsHandler(),
      http.get('*/api/account/external-logins', () => HttpResponse.json([])),
    )
  })
  afterEach(removePasskeyBrowser)

  describe('profile', () => {
    it('saves the display name', async () => {
      let saved: unknown
      server.use(
        http.put('*/api/account/profile', async ({ request }) => {
          saved = await request.json()
          return HttpResponse.json({ profile: { userId: 'user-1', email: 'ada@example.com', displayName: 'Ada L', roles: [] }, confirmationSentTo: null })
        }),
      )
      renderSettings()

      const name = await screen.findByLabelText('Display name (optional)')
      expect(name).toHaveValue('Ada')
      await userEvent.type(name, ' L')
      await userEvent.click(screen.getByRole('button', { name: 'Save profile' }))

      expect(await screen.findByText('Profile saved.')).toBeInTheDocument()
      expect(saved).toEqual({ email: 'ada@example.com', displayName: 'Ada L' })
    })

    it('explains that a new email needs confirming, and keeps showing the current one', async () => {
      server.use(
        http.put('*/api/account/profile', () =>
          HttpResponse.json({ profile: { userId: 'user-1', email: 'ada@example.com', displayName: 'Ada', roles: [] }, confirmationSentTo: 'new@example.com' }),
        ),
      )
      renderSettings()

      const email = await screen.findByLabelText('Email (you log in with this)')
      await userEvent.clear(email)
      await userEvent.type(email, 'new@example.com')
      await userEvent.click(screen.getByRole('button', { name: 'Save profile' }))

      expect(await screen.findByText(/open the link we've sent to new@example.com/)).toBeInTheDocument()
      expect(email).toHaveValue('ada@example.com')
    })
  })

  describe('password', () => {
    it('changes the password and continues with the new tokens', async () => {
      server.use(http.post('*/api/account/password', () => HttpResponse.json(newTokens)))
      renderSettings()

      await userEvent.type(await screen.findByLabelText('Current password'), 'old')
      await userEvent.type(screen.getByLabelText('New password'), 'N3w-P@ssword!')
      await userEvent.type(screen.getByLabelText('Confirm new password'), 'N3w-P@ssword!')
      await userEvent.click(screen.getByRole('button', { name: 'Change password' }))

      expect(await screen.findByText(/Password changed/)).toBeInTheDocument()
      expect(getTokens()).toEqual(newTokens)
    })

    it('shows why a change was refused', async () => {
      server.use(
        http.post('*/api/account/password', () =>
          HttpResponse.json({ errors: { PasswordMismatch: ['Incorrect password.'] } }, { status: 400 }),
        ),
      )
      renderSettings()

      await userEvent.type(await screen.findByLabelText('Current password'), 'wrong')
      await userEvent.type(screen.getByLabelText('New password'), 'N3w-P@ssword!')
      await userEvent.type(screen.getByLabelText('Confirm new password'), 'N3w-P@ssword!')
      await userEvent.click(screen.getByRole('button', { name: 'Change password' }))

      expect(await screen.findByText('Incorrect password.')).toBeInTheDocument()
      expect(getTokens()).toEqual(testTokens)
    })
  })

  describe('deleting the account', () => {
    it('signs out and goes home with a notice', async () => {
      server.use(http.post('*/api/account/delete', () => new HttpResponse(null, { status: 204 })))
      const { router } = renderSettings()

      await userEvent.type(await screen.findByLabelText('Enter your password to confirm'), 'P@ssword123!')
      await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }))

      expect(await screen.findByText('Home page')).toBeInTheDocument()
      expect(router.state.location.state).toEqual({ notice: 'Your account has been deleted.' })
      expect(getTokens()).toBeNull()
    })

    it('needs the right password', async () => {
      server.use(
        http.post('*/api/account/delete', () => HttpResponse.json({ errors: { Password: ['The password is incorrect.'] } }, { status: 400 })),
      )
      renderSettings()

      await userEvent.type(await screen.findByLabelText('Enter your password to confirm'), 'wrong')
      await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }))

      expect(await screen.findByText('The password is incorrect.')).toBeInTheDocument()
      expect(getTokens()).toEqual(testTokens)
    })
  })

  describe('passkeys', () => {
    it("aren't offered where the browser can't use them", async () => {
      renderSettings()
      await screen.findByLabelText('Current password')
      expect(screen.queryByRole('heading', { name: 'Passkeys' })).not.toBeInTheDocument()
    })

    it('lists, adds and renames passkeys', async () => {
      let passkeys = [{ id: 'pk1', name: 'Laptop', createdAt: '2026-11-01T00:00:00Z' }]
      let added: unknown
      stubPasskeyBrowser({ create: vi.fn().mockResolvedValue({ toJSON: () => ({ id: 'pk2' }) }) })
      server.use(
        http.get('*/api/account/passkeys', () => HttpResponse.json(passkeys)),
        http.post('*/api/account/passkeys/options', () => HttpResponse.json({ optionsJson: '{}', state: 'state-1' })),
        http.post('*/api/account/passkeys', async ({ request }) => {
          added = await request.json()
          passkeys = [...passkeys, { id: 'pk2', name: 'Phone', createdAt: '2026-11-02T00:00:00Z' }]
          return new HttpResponse(null, { status: 201 })
        }),
        http.put('*/api/account/passkeys/:id', async ({ params, request }) => {
          const { name } = (await request.json()) as { name: string }
          passkeys = passkeys.map((p) => (p.id === params.id ? { ...p, name } : p))
          return new HttpResponse(null, { status: 204 })
        }),
      )
      renderSettings()

      expect(await screen.findByText('Laptop')).toBeInTheDocument()
      await userEvent.type(screen.getByPlaceholderText('Name, e.g. Laptop'), 'Phone')
      await userEvent.click(screen.getByRole('button', { name: 'Add a passkey' }))
      expect(await screen.findByText('Passkey added.')).toBeInTheDocument()
      expect(await screen.findByText('Phone')).toBeInTheDocument()
      expect(added).toEqual({ credentialJson: '{"id":"pk2"}', state: 'state-1', name: 'Phone' })

      await userEvent.click(screen.getAllByRole('button', { name: 'Rename' })[0])
      const name = screen.getByDisplayValue('Laptop')
      await userEvent.clear(name)
      await userEvent.type(name, 'Work laptop')
      await userEvent.click(screen.getByRole('button', { name: 'Save' }))
      expect(await screen.findByText('Work laptop')).toBeInTheDocument()
    })

    it('explains why the last way to sign in cannot be removed', async () => {
      stubPasskeyBrowser({})
      server.use(
        http.get('*/api/account/passkeys', () => HttpResponse.json([{ id: 'pk1', name: 'Laptop', createdAt: '2026-11-01T00:00:00Z' }])),
        http.delete('*/api/account/passkeys/:id', () =>
          HttpResponse.json({ errors: { Passkey: ['This is the only way you can sign in.'] } }, { status: 400 }),
        ),
      )
      renderSettings()

      await screen.findByText('Laptop')
      await userEvent.click(screen.getByRole('button', { name: 'Remove' }))

      expect(await screen.findByText('This is the only way you can sign in.')).toBeInTheDocument()
    })
  })

  describe('sign-in methods', () => {
    it("aren't shown when no provider is configured or linked", async () => {
      renderSettings()
      await screen.findByLabelText('Current password')
      expect(screen.queryByRole('heading', { name: 'Sign-in methods' })).not.toBeInTheDocument()
    })

    it('starts linking a configured provider with a ticket', async () => {
      let ticketRequested = false
      server.use(
        authOptionsHandler({ externalProviders: [{ name: 'Google', displayName: 'Google' }] }),
        http.post('*/api/account/external-logins/link-ticket', () => {
          ticketRequested = true
          return HttpResponse.json({ ticket: 'ticket-1' })
        }),
      )
      renderSettings()

      await userEvent.click(await screen.findByRole('button', { name: 'Link Google' }))

      await waitFor(() => expect(ticketRequested).toBe(true))
    })

    it('shows the result of linking, then removes it from the address bar', async () => {
      server.use(
        authOptionsHandler({ externalProviders: [{ name: 'Google', displayName: 'Google' }] }),
        http.get('*/api/account/external-logins', () => HttpResponse.json([{ provider: 'Google', displayName: 'Google' }])),
      )
      window.history.replaceState(null, '', '/settings#linked=Google')
      renderSettings()

      expect(await screen.findByText(/Google is now linked/)).toBeInTheDocument()
      expect(window.location.hash).toBe('')
      expect(screen.queryByRole('button', { name: 'Link Google' })).not.toBeInTheDocument()
    })

    it('removes a linked provider', async () => {
      let linked = [{ provider: 'Google', displayName: 'Google' }]
      server.use(
        authOptionsHandler({ externalProviders: [{ name: 'Google', displayName: 'Google' }] }),
        http.get('*/api/account/external-logins', () => HttpResponse.json(linked)),
        http.delete('*/api/account/external-logins/:provider', () => {
          linked = []
          return new HttpResponse(null, { status: 204 })
        }),
      )
      renderSettings()

      await userEvent.click(await screen.findByRole('button', { name: 'Remove' }))

      expect(await screen.findByText('No providers linked.')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Link Google' })).toBeInTheDocument()
    })
  })
})
