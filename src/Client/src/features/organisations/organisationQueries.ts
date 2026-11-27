import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, refreshTokens } from '../../lib/api/client'
import { getTokens, setTokens } from '../../lib/api/authTokens'
import { problemMessage } from '../../lib/api/problemDetails'

export const roles = ['Owner', 'Admin', 'Member'] as const
export type OrganisationRole = (typeof roles)[number]

export const organisationKeys = {
  mine: ['organisations'] as const,
  current: ['organisation'] as const,
  members: ['organisation', 'members'] as const,
  invitations: ['organisation', 'invitations'] as const,
  join: (token: string) => ['join', token] as const,
}

export const canManageMembers = (role?: string) => role === 'Owner' || role === 'Admin'

// The user's organisations; isCurrent marks the one they're working in.
export function useOrganisations() {
  return useQuery({
    queryKey: organisationKeys.mine,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisations')
      if (!response.ok || !data) throw new Error('Could not load your organisations.')
      return data
    },
  })
}

export function useCurrentOrganisation() {
  return useQuery({
    queryKey: organisationKeys.current,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation')
      if (!response.ok || !data) throw new Error('Could not load the organisation.')
      return data
    },
  })
}

export function useMembers() {
  return useQuery({
    queryKey: organisationKeys.members,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/members')
      if (!response.ok || !data) throw new Error('Could not load the members.')
      return data
    },
  })
}

export function useInvitations(enabled: boolean) {
  return useQuery({
    queryKey: organisationKeys.invitations,
    enabled,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/invitations')
      if (!response.ok || !data) throw new Error('Could not load the invitations.')
      return data
    },
  })
}

// New tokens for another organisation. The organisation in the token changes, so cached data is
// dropped (OrganisationSwitcher) and the realtime connection reconnects (useTodoRealtime).
export async function switchOrganisation(organisationId: string) {
  const refreshToken = getTokens()?.refreshToken
  if (!refreshToken) throw new Error('You are signed out.')
  const { data, error, response } = await api.POST('/api/organisations/switch', { body: { organisationId, refreshToken } })
  if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not switch organisation.'))
  setTokens(data)
}

export function useSwitchOrganisation() {
  return useMutation({ mutationFn: switchOrganisation })
}

// Creates an organisation and switches to it.
export function useCreateOrganisation() {
  return useMutation({
    mutationFn: async (name: string) => {
      const { data, error, response } = await api.POST('/api/organisations', { body: { name } })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not create the organisation.'))
      await switchOrganisation(data.id)
    },
  })
}

export function useRenameOrganisation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (name: string) => {
      const { error, response } = await api.PUT('/api/organisation', { body: { name } })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not rename the organisation.'))
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organisationKeys.current }),
  })
}

// After leaving or deleting the current organisation the token no longer works for it: refreshing
// moves the session to another of the user's organisations (or a new one of their own).
async function moveToAnotherOrganisation() {
  if (!(await refreshTokens())) setTokens(null)
}

export function useDeleteOrganisation() {
  return useMutation({
    mutationFn: async () => {
      const { error, response } = await api.DELETE('/api/organisation')
      if (!response.ok) throw new Error(problemMessage(error, 'Could not delete the organisation.'))
      await moveToAnotherOrganisation()
    },
  })
}

export function useChangeRole() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, role }: { userId: string; role: OrganisationRole }) => {
      const { error, response } = await api.PUT('/api/organisation/members/{userId}/role', {
        params: { path: { userId } },
        body: { role },
      })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not change the role.'))
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organisationKeys.current }),
  })
}

// Removes a member; with your own user id, leaves the organisation.
export function useRemoveMember() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, leaving }: { userId: string; leaving: boolean }) => {
      const { error, response } = await api.DELETE('/api/organisation/members/{userId}', { params: { path: { userId } } })
      if (!response.ok) throw new Error(problemMessage(error, leaving ? 'Could not leave the organisation.' : 'Could not remove the member.'))
      if (leaving) await moveToAnotherOrganisation()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organisationKeys.members }),
  })
}

export function useInvite() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (invitation: { email: string; role: OrganisationRole }) => {
      const { error, response } = await api.POST('/api/organisation/invitations', { body: invitation })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not send the invitation.'))
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organisationKeys.invitations }),
  })
}

export function useRevokeInvitation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      const { error, response } = await api.DELETE('/api/organisation/invitations/{id}', { params: { path: { id } } })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not revoke the invitation.'))
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organisationKeys.invitations }),
  })
}

// The invitation behind a /join link.
export function useInvitation(token: string) {
  return useQuery({
    queryKey: organisationKeys.join(token),
    enabled: token !== '',
    retry: false,
    queryFn: async () => {
      // POST, with the token in the body: a URL would be recorded in traces and logs.
      const { data, response } = await api.POST('/api/join/details', { body: { token } })
      if (response.status === 404) return null
      if (!response.ok || !data) throw new Error('Could not load the invitation.')
      return data
    },
  })
}

// Signed in with the invited email: join, and switch to the organisation.
export function useAcceptInvitation() {
  return useMutation({
    mutationFn: async (token: string) => {
      const { data, error, response } = await api.POST('/api/join', { body: { token } })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not accept the invitation.'))
      await switchOrganisation(data.organisationId)
    },
  })
}

// No account yet: create one from the invitation, which signs in to the organisation.
export function useJoinWithNewAccount() {
  return useMutation({
    mutationFn: async ({ token, password }: { token: string; password: string }) => {
      const { data, error, response } = await api.POST('/api/join/register', { body: { token, password } })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not create your account.'))
      setTokens(data)
    },
  })
}
