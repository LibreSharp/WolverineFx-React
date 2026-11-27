import type { PageHandle } from '../components/PageMeta'
import { AdminLayout } from '../features/admin/AdminLayout'

// The back office: its sections are the routes inside it (routes.ts).
export const handle: PageHandle = { title: 'Admin' }

export default AdminLayout
