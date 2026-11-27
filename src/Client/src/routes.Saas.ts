import { index, layout, route, type RouteConfig } from '@react-router/dev/routes'

// The app's pages: each URL and the route module (in routes/) that renders it. A route module
// default-exports the page's component, and its `handle` gives the page title and whether search
// engines may index it (components/PageMeta.tsx). Each page's code loads when it's first visited.
// When you add a page, add its path to pages.ts too.
export default [
  index('routes/home.tsx'),
  // Opened from links in emails, signed in or not.
  route('confirm-email', 'routes/confirm-email.tsx'),
  route('confirm-email-change', 'routes/confirm-email-change.tsx'),
  route('accept-invitation', 'routes/accept-invitation.tsx'),
  route('external-login', 'routes/external-login.tsx'),
  // An organisation invitation's link: accept it, or create an account from it.
  route('join', 'routes/join.tsx'),
  // Signed-out pages; a signed-in user is sent on to the Todos.
  layout('routes/guest-only.tsx', [
    route('login', 'routes/login.tsx'),
    route('register', 'routes/register.tsx'),
    route('forgot-password', 'routes/forgot-password.tsx'),
    route('reset-password', 'routes/reset-password.tsx'),
  ]),
  // Pages that need a login; a signed-out user is sent to /login and brought back.
  layout('routes/require-auth.tsx', [
    route('todos', 'routes/todos.tsx'),
    route('settings', 'routes/settings.tsx'),
    route('organisation', 'routes/organisation.tsx'),
    layout('routes/require-admin.tsx', [route('admin', 'routes/admin.tsx')]),
  ]),
  route('*', 'routes/not-found.tsx'),
] satisfies RouteConfig
