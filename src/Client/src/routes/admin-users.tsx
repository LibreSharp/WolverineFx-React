import type { PageHandle } from '../components/PageMeta'
import { AdminPage } from '../features/admin/AdminPage'

export const handle: PageHandle = { title: 'Users · Admin' }

export default function AdminUsers() {
  return <AdminPage />
}
