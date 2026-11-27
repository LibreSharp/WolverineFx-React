import type { PageHandle } from '../components/PageMeta'
import { AdminPage } from '../features/admin/AdminPage'
import { UserOrganisations } from '../features/admin/UserOrganisations'

export const handle: PageHandle = { title: 'Users · Admin' }

// With organisations: each user's organisations too.
export default function AdminUsers() {
  return <AdminPage details={(user) => <UserOrganisations userId={user.id} />} />
}
