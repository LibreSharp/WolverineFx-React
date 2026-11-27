import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

export const adminKeys = {
  all: ['admin'] as const,
  users: ['admin', 'users'] as const,
  userPage: (search: string, page: number) => ['admin', 'users', { search, page }] as const,
  audit: (page: number, targetId?: string) => ['admin', 'audit', { page, targetId }] as const,
  failedMessages: (page: number) => ['admin', 'failed-messages', { page }] as const,
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

// Runs an admin action, then refetches the back office's data (lists, and the audit log).
export function useAdminMutation<TVariables>(action: (variables: TVariables) => Promise<{ error?: unknown; response: Response }>, fallback: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (variables: TVariables) => {
      const { error, response } = await action(variables)
      if (!response.ok) throw new Error(problemMessage(error, fallback))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: adminKeys.all }),
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

export function useEndSessions() {
  return useAdminMutation((id: string) => api.POST('/api/admin/users/{id}/end-sessions', { params: { path: { id } } }), 'Could not end the sessions.')
}

export function useResendConfirmation() {
  return useAdminMutation(
    (id: string) => api.POST('/api/admin/users/{id}/resend-confirmation', { params: { path: { id } } }),
    'Could not send the email.',
  )
}

// Newest first; targetId: only the entries about one user, organisation or feature.
export function useAuditLog(page: number, targetId?: string) {
  return useQuery({
    queryKey: adminKeys.audit(page, targetId),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/audit', { params: { query: { page, targetId } } })
      if (!response.ok || !data) throw new Error('Could not load the audit log.')
      return data
    },
    placeholderData: keepPreviousData,
  })
}

export function useFailedMessages(page: number) {
  return useQuery({
    queryKey: adminKeys.failedMessages(page),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/failed-messages', { params: { query: { page } } })
      if (!response.ok || !data) throw new Error('Could not load the failed messages.')
      return data
    },
    placeholderData: keepPreviousData,
  })
}

export function useReplayMessages() {
  return useAdminMutation((ids: string[]) => api.POST('/api/admin/failed-messages/replay', { body: { ids } }), 'Could not replay the messages.')
}

export function useDiscardMessages() {
  return useAdminMutation((ids: string[]) => api.POST('/api/admin/failed-messages/discard', { body: { ids } }), 'Could not discard the messages.')
}
