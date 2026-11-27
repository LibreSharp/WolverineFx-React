import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { setTokens } from '../../lib/api/authTokens'
import { problemMessage } from '../../lib/api/problemDetails'

export const accountKeys = {
  me: ['me'] as const,
  externalLogins: ['external-logins'] as const,
}

// The signed-in user's profile (id, email, display name).
export function useMe() {
  return useQuery({
    queryKey: accountKeys.me,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/auth/me')
      if (!response.ok || !data) throw new Error('Could not load your account.')
      return data
    },
  })
}

// Saves the display name straight away. A new email is only used once the link sent to it is
// opened, so the result says where that link went (confirmationSentTo).
export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (profile: { email: string; displayName: string | null }) => {
      const { data, error, response } = await api.PUT('/api/account/profile', { body: profile })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not save your profile.'))
      return data
    },
    onSuccess: ({ profile }) => queryClient.setQueryData(accountKeys.me, profile),
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: async (passwords: { currentPassword: string; newPassword: string }) => {
      const { data, error, response } = await api.POST('/api/account/password', { body: passwords })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not change your password.'))
      return data
    },
    // Every other session is signed out; this one continues with the new tokens.
    onSuccess: (tokens) => setTokens(tokens),
  })
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: async (password: string) => {
      const { error, response } = await api.POST('/api/account/delete', { body: { password } })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not delete your account.'))
    },
  })
}

// The Google/GitHub logins linked to this account.
export function useExternalLogins() {
  return useQuery({
    queryKey: accountKeys.externalLogins,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/account/external-logins')
      if (!response.ok || !data) throw new Error('Could not load your sign-in methods.')
      return data
    },
  })
}

export function useRemoveExternalLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (provider: string) => {
      const { error, response } = await api.DELETE('/api/account/external-logins/{provider}', {
        params: { path: { provider } },
      })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not remove it.'))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: accountKeys.externalLogins }),
  })
}

// Leaves the app to link a provider: a full-page navigation can't send the access token, so ask
// for a short-lived ticket first (see ExternalLinkTickets in the API). The provider sends the
// browser back to /settings#linked=... or /settings#error=...
export async function startLinkingExternalLogin(provider: string) {
  const { data } = await api.POST('/api/account/external-logins/link-ticket')
  if (!data) throw new Error('Could not start linking.')
  window.location.assign(`/api/auth/external/${encodeURIComponent(provider)}?link=${encodeURIComponent(data.ticket)}`)
}
