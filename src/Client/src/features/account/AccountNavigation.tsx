import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { NavItem } from '../../components/Layout'
import { useAuthOptions } from '../auth/authQueries'
import { useAuth } from '../auth/useAuth'
import { NotificationBell } from '../notifications/NotificationBell'
import { useMe } from './accountQueries'
import { UserMenu } from './UserMenu'

// The header's navigation and account actions: links for signed-in users (Admin only for
// admins), and the notifications and user menu, or "Log in"/"Register".
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
  if (isAuthenticated)
    return (
      <div className="flex items-center gap-1">
        <NotificationBell />
        <UserMenu />
      </div>
    )

  return (
    <div className="flex items-center gap-3 text-sm">
      <Button asChild variant="ghost" size="sm">
        <Link to="/login">Log in</Link>
      </Button>
      {options.data?.allowRegistration !== false && (
        <Button asChild size="sm">
          <Link to="/register">Register</Link>
        </Button>
      )}
    </div>
  )
}
