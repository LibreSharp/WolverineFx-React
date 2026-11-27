import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getTokens } from '../../lib/api/authTokens'
import { server } from '../../test/server'
import { LoginForm } from './LoginForm'
import { removePasskeyBrowser, renderWithAuth, stubPasskeyBrowser, testTokens } from './testing'

function renderLogin() {
  const onSuccess = vi.fn()
  renderWithAuth([{ path: '/login', element: <LoginForm onSuccess={onSuccess} /> }, { path: '/forgot-password', element: <p>Forgot</p> }], '/login')
  return { onSuccess }
}

async function submit(email = 'ada@example.com', password = 'secret') {
  await userEvent.type(screen.getByPlaceholderText('Email'), email)
  await userEvent.type(screen.getByPlaceholderText('Password'), password)
  await userEvent.click(screen.getByRole('button', { name: 'Log in' }))
}

describe('LoginForm', () => {
  afterEach(removePasskeyBrowser)

  it('stores the tokens on success', async () => {
    server.use(http.post('*/api/auth/login', () => HttpResponse.json(testTokens)))
    const { onSuccess } = renderLogin()

    await submit()

    expect(onSuccess).toHaveBeenCalled()
    expect(getTokens()).toEqual(testTokens)
  })

  it('says the email or password is wrong on 401', async () => {
    server.use(http.post('*/api/auth/login', () => new HttpResponse(null, { status: 401 })))
    const { onSuccess } = renderLogin()

    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password')
    expect(onSuccess).not.toHaveBeenCalled()
    expect(getTokens()).toBeNull()
  })

  it('offers to resend the confirmation email when the email is unconfirmed', async () => {
    let resentTo = ''
    server.use(
      http.post('*/api/auth/login', () =>
        HttpResponse.json({ title: 'Confirm your email address first.', reason: 'EmailNotConfirmed' }, { status: 403 }),
      ),
      http.post('*/api/auth/resend-confirmation', async ({ request }) => {
        resentTo = ((await request.json()) as { email: string }).email
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderLogin()

    await submit()
    expect(await screen.findByRole('alert')).toHaveTextContent('Confirm your email address first.')
    await userEvent.click(screen.getByRole('button', { name: 'Send the confirmation email again' }))

    expect(await screen.findByRole('status')).toHaveTextContent('sent a new confirmation link to ada@example.com')
    expect(resentTo).toBe('ada@example.com')
  })

  it('links to password reset when the account is locked', async () => {
    server.use(
      http.post('*/api/auth/login', () =>
        HttpResponse.json({ title: 'This account is locked.', reason: 'LockedOut' }, { status: 403 }),
      ),
    )
    renderLogin()

    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('This account is locked.')
    expect(screen.getByRole('link', { name: 'Reset your password' })).toHaveAttribute('href', '/forgot-password')
  })

  it('hides the passkey button where the browser has no passkey support', () => {
    renderLogin()
    expect(screen.queryByRole('button', { name: 'Log in with a passkey' })).not.toBeInTheDocument()
  })

  it('logs in with a passkey', async () => {
    let sent: { credentialJson: string; state: string } | undefined
    stubPasskeyBrowser({ get: vi.fn().mockResolvedValue({ toJSON: () => ({ id: 'credential-1' }) }) })
    server.use(
      http.post('*/api/auth/passkey/options', () => HttpResponse.json({ optionsJson: '{"challenge":"abc"}', state: 'state-1' })),
      http.post('*/api/auth/passkey/login', async ({ request }) => {
        sent = (await request.json()) as typeof sent
        return HttpResponse.json(testTokens)
      }),
    )
    const { onSuccess } = renderLogin()

    await userEvent.click(screen.getByRole('button', { name: 'Log in with a passkey' }))

    await vi.waitFor(() => expect(onSuccess).toHaveBeenCalled())
    expect(sent).toEqual({ credentialJson: '{"id":"credential-1"}', state: 'state-1' })
    expect(getTokens()).toEqual(testTokens)
  })

  it('stays quiet when the passkey dialog is cancelled', async () => {
    stubPasskeyBrowser({ get: vi.fn().mockRejectedValue(new DOMException('cancelled', 'NotAllowedError')) })
    server.use(http.post('*/api/auth/passkey/options', () => HttpResponse.json({ optionsJson: '{}', state: 's' })))
    renderLogin()

    await userEvent.click(screen.getByRole('button', { name: 'Log in with a passkey' }))

    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
