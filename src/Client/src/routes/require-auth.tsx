import { RequireAuth } from '../features/auth/routeGuards'

// Pages that need a login: a signed-out user is sent to /login and brought back.
export default RequireAuth
