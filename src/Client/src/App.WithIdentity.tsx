import { useState } from 'react'
import { AuthProvider } from './features/auth/AuthContext'
import { useAuth } from './features/auth/useAuth'
import { LoginForm } from './features/auth/LoginForm'
import { RegisterForm } from './features/auth/RegisterForm'
import { ProtectedRoute } from './features/auth/ProtectedRoute'
import { TodoList } from './features/todos/TodoList'

function AuthGate() {
  const { logout } = useAuth()
  const [mode, setMode] = useState<'login' | 'register'>('login')

  return (
    <ProtectedRoute
      fallback={
        <div>
          {mode === 'login' ? <LoginForm /> : <RegisterForm onSuccess={() => setMode('login')} />}
          <p className="text-center text-sm text-gray-500">
            {mode === 'login' ? (
              <>
                No account?{' '}
                <button
                  type="button"
                  className="text-indigo-600 hover:underline"
                  onClick={() => setMode('register')}
                >
                  Register
                </button>
              </>
            ) : (
              <>
                Already have an account?{' '}
                <button type="button" className="text-indigo-600 hover:underline" onClick={() => setMode('login')}>
                  Log in
                </button>
              </>
            )}
          </p>
        </div>
      }
    >
      <div>
        <div className="flex justify-end p-4">
          <button type="button" onClick={() => void logout()} className="text-sm text-gray-500 hover:underline">
            Log out
          </button>
        </div>
        <TodoList />
      </div>
    </ProtectedRoute>
  )
}

function App() {
  return (
    <AuthProvider>
      <AuthGate />
    </AuthProvider>
  )
}

export default App
