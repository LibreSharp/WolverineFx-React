import { createBrowserRouter, RouterProvider } from 'react-router'
import { Layout } from './components/Layout'
import { NotFound } from './components/NotFound'
import { AuthProvider } from './features/auth/AuthContext'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { GuestOnly, RequireAuth } from './features/auth/routeGuards'
import { useAuth } from './features/auth/useAuth'
import { TodoList } from './features/todos/TodoList'

const router = createBrowserRouter([
  {
    element: <Layout actions={<AccountActions />} />,
    children: [
      {
        element: <GuestOnly />,
        children: [
          { path: 'login', element: <LoginPage /> },
          { path: 'register', element: <RegisterPage /> },
        ],
      },
      {
        element: <RequireAuth />,
        children: [{ index: true, element: <TodoList /> }],
      },
      { path: '*', element: <NotFound /> },
    ],
  },
])

function AccountActions() {
  const { isAuthenticated, logout } = useAuth()
  if (!isAuthenticated) return null

  // Logging out clears the tokens; RequireAuth then redirects to /login.
  return (
    <button type="button" onClick={() => void logout()} className="text-sm text-gray-500 hover:underline">
      Log out
    </button>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}
