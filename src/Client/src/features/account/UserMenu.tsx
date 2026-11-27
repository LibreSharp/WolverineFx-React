import { Link } from 'react-router'
import { ChevronDownIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAuth } from '../auth/useAuth'
import { useMe } from './accountQueries'

// The signed-in user's name in the header, opening a menu with Settings and Log out.
export function UserMenu() {
  const { logout } = useAuth()
  const me = useMe()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm">
          {me.data?.displayName || me.data?.email || 'Account'}
          <ChevronDownIcon aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-40">
        <DropdownMenuItem asChild>
          <Link to="/settings">Settings</Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        {/* Clears the tokens; RequireAuth then redirects to /login. */}
        <DropdownMenuItem onSelect={() => void logout()}>Log out</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
