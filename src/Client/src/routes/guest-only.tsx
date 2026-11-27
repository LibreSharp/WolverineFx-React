import { GuestOnly } from '../features/auth/routeGuards'

// Signed-out pages: a signed-in user is sent on to the Todos.
export default GuestOnly
