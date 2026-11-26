import { useRef } from 'react'
import { Link } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { useMe } from './accountQueries'

// The signed-in user's name in the header, opening a menu with Settings and Log out.
// A <details> element gives keyboard and screen reader support without extra code.
export function UserMenu() {
  const { logout } = useAuth()
  const me = useMe()
  const menu = useRef<HTMLDetailsElement>(null)
  const close = () => menu.current?.removeAttribute('open')

  return (
    <details ref={menu} className="relative">
      <summary className="cursor-pointer list-none rounded px-2 py-1 text-sm text-gray-700 hover:bg-gray-100">
        {me.data?.displayName || me.data?.email || 'Account'} ▾
      </summary>
      <div className="absolute right-0 z-10 mt-1 w-40 rounded border border-gray-200 bg-white py-1 shadow-md">
        <Link to="/settings" onClick={close} className="block px-3 py-2 text-sm hover:bg-gray-100">
          Settings
        </Link>
        <button
          type="button"
          onClick={() => {
            close()
            // Clears the tokens; RequireAuth then redirects to /login.
            void logout()
          }}
          className="block w-full px-3 py-2 text-left text-sm hover:bg-gray-100"
        >
          Log out
        </button>
      </div>
    </details>
  )
}
