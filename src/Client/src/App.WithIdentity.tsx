import { createBrowserRouter, Link, RouterProvider } from 'react-router'
import { HomePage } from './components/HomePage'
import { Layout, NavItem } from './components/Layout'
import { NotFound } from './components/NotFound'
import { AdminPage } from './features/admin/AdminPage'
import { useMe } from './features/account/accountQueries'
import { SettingsPage } from './features/account/SettingsPage'
import { UserMenu } from './features/account/UserMenu'
import { AcceptInvitationPage } from './features/auth/AcceptInvitationPage'
import { useAuthOptions } from './features/auth/authQueries'
import { AuthProvider } from './features/auth/AuthContext'
import { ConfirmEmailChangePage, ConfirmEmailPage } from './features/auth/ConfirmEmailPage'
import { ForgotPasswordPage } from './features/auth/ForgotPasswordPage'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { ResetPasswordPage } from './features/auth/ResetPasswordPage'
import { GuestOnly, RequireAdmin, RequireAuth } from './features/auth/routeGuards'
import { useAuth } from './features/auth/useAuth'
import { TodoList } from './features/todos/TodoList'

const router = createBrowserRouter([
  {
    element: <Layout nav={<Navigation />} actions={<AccountActions />} />,
    children: [
      { index: true, element: <HomePage /> },
      // Opened from links in emails, signed in or not.
      { path: 'confirm-email', element: <ConfirmEmailPage /> },
      { path: 'confirm-email-change', element: <ConfirmEmailChangePage /> },
      { path: 'accept-invitation', element: <AcceptInvitationPage /> },
      {
        // Signed-out pages; a signed-in user is sent on to the Todos.
        element: <GuestOnly />,
        children: [
          { path: 'login', element: <LoginPage /> },
          { path: 'register', element: <RegisterPage /> },
          { path: 'forgot-password', element: <ForgotPasswordPage /> },
          { path: 'reset-password', element: <ResetPasswordPage /> },
        ],
      },
      {
        // Pages that need a login; a signed-out user is sent to /login and brought back.
        element: <RequireAuth />,
        children: [
          { path: 'todos', element: <TodoList /> },
          { path: 'settings', element: <SettingsPage /> },
          { element: <RequireAdmin />, children: [{ path: 'admin', element: <AdminPage /> }] },
        ],
      },
      { path: '*', element: <NotFound /> },
    ],
  },
])

function Navigation() {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return null
  return <SignedInNavigation />
}

function SignedInNavigation() {
  const me = useMe()
  return (
    <>
      <NavItem to="/todos">Todos</NavItem>
      <NavItem to="/settings">Settings</NavItem>
      {me.data?.roles.includes('Admin') && <NavItem to="/admin">Admin</NavItem>}
    </>
  )
}

function AccountActions() {
  const { isAuthenticated } = useAuth()
  const options = useAuthOptions()
  if (isAuthenticated) return <UserMenu />

  return (
    <div className="flex items-center gap-3 text-sm">
      <Link to="/login" className="text-gray-600 hover:text-gray-900">
        Log in
      </Link>
      {options.data?.allowRegistration !== false && (
        <Link to="/register" className="rounded bg-indigo-600 px-3 py-1.5 text-white hover:bg-indigo-700">
          Register
        </Link>
      )}
    </div>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}
