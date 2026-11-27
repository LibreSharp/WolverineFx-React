import type { RouteObject } from 'react-router'
import { HomePage } from './components/HomePage'
import { Layout } from './components/Layout'
import { AccountActions, Navigation } from './features/account/AccountNavigation'
import { NotFound } from './components/NotFound'
import type { PageHandle } from './components/PageMeta'
import { GuestOnly, RequireAdmin, RequireAuth } from './features/auth/routeGuards'

// A page that loads when first visited, so the first page view downloads less JavaScript.
function page(title: string, load: () => Promise<React.ComponentType>): RouteObject {
  return { handle: { title } satisfies PageHandle, lazy: async () => ({ Component: await load() }) }
}

// Each route's `handle` gives its page title and whether search engines may index it (see
// components/PageMeta.tsx). When you add a route, add its path to routes.ts too.
export const routes: RouteObject[] = [
  {
    element: <Layout nav={<Navigation />} actions={<AccountActions />} />,
    children: [
      { index: true, element: <HomePage />, handle: { index: true } satisfies PageHandle },
      // Opened from links in emails, signed in or not.
      {
        path: 'confirm-email',
        ...page('Confirm your email', async () => (await import('./features/auth/ConfirmEmailPage')).ConfirmEmailPage),
      },
      {
        path: 'confirm-email-change',
        ...page('Confirm your new email', async () => (await import('./features/auth/ConfirmEmailPage')).ConfirmEmailChangePage),
      },
      {
        path: 'accept-invitation',
        ...page('Accept your invitation', async () => (await import('./features/auth/AcceptInvitationPage')).AcceptInvitationPage),
      },
      { path: 'external-login', ...page('Signing in', async () => (await import('./features/auth/ExternalLoginPage')).ExternalLoginPage) },
      {
        // Signed-out pages; a signed-in user is sent on to the Todos.
        element: <GuestOnly />,
        children: [
          { path: 'login', ...page('Log in', async () => (await import('./features/auth/LoginPage')).LoginPage) },
          { path: 'register', ...page('Create an account', async () => (await import('./features/auth/RegisterPage')).RegisterPage) },
          {
            path: 'forgot-password',
            ...page('Reset your password', async () => (await import('./features/auth/ForgotPasswordPage')).ForgotPasswordPage),
          },
          {
            path: 'reset-password',
            ...page('Choose a new password', async () => (await import('./features/auth/ResetPasswordPage')).ResetPasswordPage),
          },
        ],
      },
      {
        // Pages that need a login; a signed-out user is sent to /login and brought back.
        element: <RequireAuth />,
        children: [
          { path: 'todos', ...page('Todos', async () => (await import('./features/todos/TodoList')).TodoList) },
          { path: 'settings', ...page('Settings', async () => (await import('./features/account/SettingsPage')).SettingsPage) },
          {
            element: <RequireAdmin />,
            children: [{ path: 'admin', ...page('Admin', async () => (await import('./features/admin/AdminPage')).AdminPage) }],
          },
        ],
      },
      { path: '*', element: <NotFound />, handle: { title: 'Page not found' } satisfies PageHandle },
    ],
  },
]
