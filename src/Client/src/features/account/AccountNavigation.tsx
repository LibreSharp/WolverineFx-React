import { Link } from 'react-router'
import { NavItem } from '../../components/Layout'
import { useAuthOptions } from '../auth/authQueries'
import { useAuth } from '../auth/useAuth'
import { useMe } from './accountQueries'
import { UserMenu } from './UserMenu'

// The header's navigation and account actions: links for signed-in users (Admin only for
// admins), and the user menu or "Log in"/"Register".
export function Navigation() {
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

export function AccountActions() {
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
