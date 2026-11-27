import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

export const notificationKeys = {
  list: ['notifications'] as const,
  preferences: ['email-preferences'] as const,
}

// The signed-in user's latest notifications and how many are unread. NotificationBell refetches it
// when the server says one was added.
export function useNotifications() {
  return useQuery({
    queryKey: notificationKeys.list,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/notifications')
      if (!response.ok || !data) throw new Error('Could not load your notifications.')
      return data
    },
  })
}

// Marks one notification (or, with no id, all of them) read.
export function useMarkRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id?: string) => {
      const { response } = id
        ? await api.POST('/api/notifications/{id}/read', { params: { path: { id } } })
        : await api.POST('/api/notifications/read-all')
      if (!response.ok) throw new Error('Could not mark the notifications read.')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: notificationKeys.list }),
  })
}

export function useEmailPreferences() {
  return useQuery({
    queryKey: notificationKeys.preferences,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/account/email-preferences')
      if (!response.ok || !data) throw new Error('Could not load your email choices.')
      return data
    },
  })
}

export function useUpdateEmailPreferences() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (choice: { category: string; email: boolean }) => {
      const { data, error, response } = await api.PUT('/api/account/email-preferences', {
        body: { categories: [{ category: choice.category, description: '', email: choice.email }] },
      })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not save your email choices.'))
      return data
    },
    onSuccess: (data) => queryClient.setQueryData(notificationKeys.preferences, data),
  })
}

// The link in a notification email: turns that kind of email off, with no login needed.
export function useUnsubscribe() {
  return useMutation({
    mutationFn: async (token: string) => {
      const { data, response } = await api.POST('/api/notifications/unsubscribe', { params: { query: { token } } })
      if (!response.ok || !data) throw new Error('This unsubscribe link is not valid. Change which emails you get in Settings.')
      return data
    },
  })
}
