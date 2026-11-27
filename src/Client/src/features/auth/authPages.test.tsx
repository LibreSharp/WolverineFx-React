import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { getTokens } from '../../lib/api/authTokens'
import { server } from '../../test/server'
import { AcceptInvitationPage } from './AcceptInvitationPage'
import { ConfirmEmailChangePage, ConfirmEmailPage } from './ConfirmEmailPage'
import { ExternalLoginPage } from './ExternalLoginPage'
import { ForgotPasswordPage } from './ForgotPasswordPage'
import { RegisterPage } from './RegisterPage'
import { ResetPasswordPage } from './ResetPasswordPage'
import { authOptionsHandler, renderWithAuth, testTokens } from './testing'

const elsewhere = ['/login', '/todos', '/settings'].map((path) => ({ path, element: <p>Page {path}</p> }))

describe('RegisterPage', () => {
  it('shows the API validation messages', async () => {
    server.use(
      authOptionsHandler(),
      http.post('*/api/auth/register', () =>
        HttpResponse.json({ errors: { PasswordTooShort: ['Passwords must be at least 6 characters.'] } }, { status: 400 }),
      ),
    )
    renderWithAuth([{ path: '/register', element: <RegisterPage /> }, ...elsewhere], '/register')

    await userEvent.type(screen.getByPlaceholderText('Email'), 'ada@example.com')
    await userEvent.type(screen.getByPlaceholderText('Password'), 'x')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Passwords must be at least 6 characters.')
  })

  it('goes to login with a "check your email" notice', async () => {
    server.use(authOptionsHandler(), http.post('*/api/auth/register', () => new HttpResponse(null, { status: 201 })))
    const { router } = renderWithAuth([{ path: '/register', element: <RegisterPage /> }, ...elsewhere], '/register')

    await userEvent.type(screen.getByPlaceholderText('Email'), 'ada@example.com')
    await userEvent.type(screen.getByPlaceholderText('Password'), 'P@ssword123!')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Page /login')).toBeInTheDocument()
    expect(router.state.location.state).toMatchObject({ notice: expect.stringContaining('confirmation link to ada@example.com') })
  })

  it('says registration is closed when it is', async () => {
    server.use(authOptionsHandler({ allowRegistration: false }))
    renderWithAuth([{ path: '/register', element: <RegisterPage /> }], '/register')

    expect(await screen.findByText(/by invitation only/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Create account' })).not.toBeInTheDocument()
  })

  it('offers the configured external providers', async () => {
    server.use(authOptionsHandler({ externalProviders: [{ name: 'Google', displayName: 'Google' }] }))
    renderWithAuth([{ path: '/register', element: <RegisterPage /> }], '/register')

    expect(await screen.findByRole('link', { name: 'Continue with Google' })).toHaveAttribute('href', '/api/auth/external/Google')
  })
})

describe('email links', () => {
  it('confirms the email once, from the link', async () => {
    let calls = 0
    server.use(
      http.post('*/api/auth/confirm-email', async ({ request }) => {
        calls++
        expect(await request.json()).toEqual({ userId: 'u1', token: 't1' })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderWithAuth([{ path: '/confirm-email', element: <ConfirmEmailPage /> }], '/confirm-email?userId=u1&token=t1')

    expect(await screen.findByRole('status')).toHaveTextContent('Your email address is confirmed.')
    expect(calls).toBe(1)
  })

  it('explains an invalid confirmation link', async () => {
    server.use(
      http.post('*/api/auth/confirm-email', () =>
        HttpResponse.json({ errors: { Token: ['This confirmation link is invalid or has expired.'] } }, { status: 400 }),
      ),
    )
    renderWithAuth([{ path: '/confirm-email', element: <ConfirmEmailPage /> }], '/confirm-email?userId=u1&token=bad')

    expect(await screen.findByRole('alert')).toHaveTextContent('invalid or has expired')
  })

  it('confirms a new email address', async () => {
    server.use(http.post('*/api/auth/confirm-email-change', () => new HttpResponse(null, { status: 204 })))
    renderWithAuth(
      [{ path: '/confirm-email-change', element: <ConfirmEmailChangePage /> }],
      '/confirm-email-change?userId=u1&email=new%40example.com&token=t1',
    )

    expect(await screen.findByRole('status')).toHaveTextContent('Your email address is now new@example.com')
  })

  it('sends a password reset link, without saying whether the account exists', async () => {
    server.use(http.post('*/api/auth/forgot-password', () => new HttpResponse(null, { status: 202 })))
    renderWithAuth([{ path: '/forgot-password', element: <ForgotPasswordPage /> }], '/forgot-password')

    await userEvent.type(screen.getByPlaceholderText('Email'), 'ada@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(await screen.findByRole('status')).toHaveTextContent('If an account exists for ada@example.com')
  })

  it('resets the password from the link and goes to login', async () => {
    let body: unknown
    server.use(
      http.post('*/api/auth/reset-password', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router } = renderWithAuth(
      [{ path: '/reset-password', element: <ResetPasswordPage /> }, ...elsewhere],
      '/reset-password?email=ada%40example.com&token=t1',
    )

    await userEvent.type(screen.getByPlaceholderText('New password'), 'N3w-P@ssword!')
    await userEvent.type(screen.getByPlaceholderText('Confirm new password'), 'N3w-P@ssword!')
    await userEvent.click(screen.getByRole('button', { name: 'Set new password' }))

    expect(await screen.findByText('Page /login')).toBeInTheDocument()
    expect(body).toEqual({ email: 'ada@example.com', token: 't1', newPassword: 'N3w-P@ssword!' })
    expect(router.state.location.state).toMatchObject({ notice: expect.stringContaining('password has been reset') })
  })

  it('stops a reset when the passwords differ', async () => {
    renderWithAuth([{ path: '/reset-password', element: <ResetPasswordPage /> }], '/reset-password?email=a%40b.c&token=t1')

    await userEvent.type(screen.getByPlaceholderText('New password'), 'one')
    await userEvent.type(screen.getByPlaceholderText('Confirm new password'), 'two')

    expect(screen.getByRole('alert')).toHaveTextContent("The passwords don't match.")
    expect(screen.getByRole('button', { name: 'Set new password' })).toBeDisabled()
  })
})

describe('AcceptInvitationPage', () => {
  it('sets the password, signs in and opens the todos', async () => {
    server.use(http.post('*/api/auth/accept-invitation', () => HttpResponse.json(testTokens)))
    renderWithAuth(
      [{ path: '/accept-invitation', element: <AcceptInvitationPage /> }, ...elsewhere],
      '/accept-invitation?userId=u1&email=ada%40example.com&token=t1',
    )

    expect(screen.getByText('Choose a password for ada@example.com.')).toBeInTheDocument()
    await userEvent.type(screen.getByPlaceholderText('Password'), 'P@ssword123!')
    await userEvent.type(screen.getByPlaceholderText('Confirm password'), 'P@ssword123!')
    await userEvent.click(screen.getByRole('button', { name: 'Create my account' }))

    expect(await screen.findByText('Page /todos')).toBeInTheDocument()
    expect(getTokens()).toEqual(testTokens)
  })

  it('explains an incomplete link', () => {
    renderWithAuth([{ path: '/accept-invitation', element: <AcceptInvitationPage /> }], '/accept-invitation')
    expect(screen.getByRole('alert')).toHaveTextContent('incomplete')
  })
})

describe('ExternalLoginPage', () => {
  it('swaps the refresh token from the URL for new tokens, and removes it from the URL', async () => {
    let swapped = ''
    server.use(
      http.post('*/api/auth/refresh', async ({ request }) => {
        swapped = ((await request.json()) as { refreshToken: string }).refreshToken
        return HttpResponse.json(testTokens)
      }),
    )
    window.history.replaceState(null, '', '/external-login#refreshToken=from-provider')
    renderWithAuth([{ path: '/external-login', element: <ExternalLoginPage /> }, ...elsewhere], '/external-login')

    expect(await screen.findByText('Page /todos')).toBeInTheDocument()
    expect(swapped).toBe('from-provider')
    expect(getTokens()).toEqual(testTokens)
    expect(window.location.hash).toBe('')
  })

  it('shows the error the API sent back', async () => {
    window.history.replaceState(null, '', '/external-login#error=Registration%20is%20closed.')
    renderWithAuth([{ path: '/external-login', element: <ExternalLoginPage /> }], '/external-login')

    expect(await screen.findByRole('alert')).toHaveTextContent('Registration is closed.')
    await waitFor(() => expect(window.location.hash).toBe(''))
  })
})
