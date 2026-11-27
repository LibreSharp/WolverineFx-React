import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

export const adminKeys = {
  users: ['admin', 'users'] as const,
  userPage: (search: string, page: number) => ['admin', 'users', { search, page }] as const,
}

// One page of users, filtered by part of the email. Keeps showing the previous page while the
// next one loads, so the table doesn't flicker when typing in the search box.
export function useAdminUsers(search: string, page: number) {
  return useQuery({
    queryKey: adminKeys.userPage(search, page),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/users', { params: { query: { search, page } } })
      if (!response.ok || !data) throw new Error('Could not load the users.')
      return data
    },
    placeholderData: keepPreviousData,
  })
}

// Runs an admin action, then refetches the user list.
function useAdminMutation<TVariables>(action: (variables: TVariables) => Promise<{ error?: unknown; response: Response }>, fallback: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (variables: TVariables) => {
      const { error, response } = await action(variables)
      if (!response.ok) throw new Error(problemMessage(error, fallback))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: adminKeys.users }),
  })
}

export function useLockUser() {
  return useAdminMutation(
    ({ id, lock }: { id: string; lock: boolean }) =>
      lock
        ? api.POST('/api/admin/users/{id}/lock', { params: { path: { id } } })
        : api.POST('/api/admin/users/{id}/unlock', { params: { path: { id } } }),
    'Could not change the lock.',
  )
}

export function useSetRoles() {
  return useAdminMutation(
    ({ id, roles }: { id: string; roles: string[] }) =>
      api.PUT('/api/admin/users/{id}/roles', { params: { path: { id } }, body: { roles } }),
    'Could not change the roles.',
  )
}

export function useInviteUser() {
  return useAdminMutation(
    (invitation: { email: string; roles: string[] }) => api.POST('/api/admin/invitations', { body: invitation }),
    'Could not send the invitation.',
  )
}
