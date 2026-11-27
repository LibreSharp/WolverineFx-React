import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { useAdminMutation } from './adminQueries'

// The back office's organisations, dashboard and feature flags (with --Auth saas). Every change
// refetches the back office's data (useAdminMutation).
const keys = {
  dashboard: ['admin', 'dashboard'] as const,
  organisations: (search: string, page: number) => ['admin', 'organisations', { search, page }] as const,
  organisation: (id: string) => ['admin', 'organisation', id] as const,
  features: ['admin', 'features'] as const,
  userOrganisations: (id: string) => ['admin', 'users', id, 'organisations'] as const,
}

export function useDashboard() {
  return useQuery({
    queryKey: keys.dashboard,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/dashboard')
      if (!response.ok || !data) throw new Error('Could not load the dashboard.')
      return data
    },
  })
}

export function useAdminOrganisations(search: string, page: number) {
  return useQuery({
    queryKey: keys.organisations(search, page),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/organisations', { params: { query: { search, page } } })
      if (!response.ok || !data) throw new Error('Could not load the organisations.')
      return data
    },
    placeholderData: keepPreviousData,
  })
}

export function useAdminOrganisation(id: string) {
  return useQuery({
    queryKey: keys.organisation(id),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/organisations/{id}', { params: { path: { id } } })
      if (response.status === 404) throw new Error('There is no such organisation.')
      if (!response.ok || !data) throw new Error('Could not load the organisation.')
      return data
    },
  })
}

export function useGrantPlan(id: string) {
  return useAdminMutation(
    ({ planId, until }: { planId: string; until: string }) =>
      api.POST('/api/admin/organisations/{id}/grant', { params: { path: { id } }, body: { planId, until } }),
    'Could not change the plan.',
  )
}

export function useSuspend(id: string) {
  return useAdminMutation(
    (reason: string | null) =>
      reason === null
        ? api.POST('/api/admin/organisations/{id}/unsuspend', { params: { path: { id } } })
        : api.POST('/api/admin/organisations/{id}/suspend', { params: { path: { id } }, body: { reason } }),
    'Could not change the suspension.',
  )
}

export function useSaveNotes(id: string) {
  return useAdminMutation(
    (notes: string) => api.PUT('/api/admin/organisations/{id}/notes', { params: { path: { id } }, body: { notes } }),
    'Could not save the notes.',
  )
}

export function useDeleteOrganisation(id: string) {
  return useAdminMutation(() => api.DELETE('/api/admin/organisations/{id}', { params: { path: { id } } }), 'Could not delete the organisation.')
}

export function useFeatures() {
  return useQuery({
    queryKey: keys.features,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/features')
      if (!response.ok || !data) throw new Error('Could not load the feature flags.')
      return data
    },
  })
}

// enabled: on, off, or null for what the plans say.
export function useSetFeature() {
  return useAdminMutation(
    ({ feature, scope, scopeKey, enabled }: { feature: string; scope: 'Global' | 'Plan' | 'Organisation'; scopeKey?: string; enabled: boolean | null }) =>
      api.PUT('/api/admin/features/{feature}', { params: { path: { feature } }, body: { scope, scopeKey: scopeKey ?? null, enabled } }),
    'Could not change the feature flag.',
  )
}

export function useUserOrganisations(userId: string, enabled: boolean) {
  return useQuery({
    queryKey: keys.userOrganisations(userId),
    enabled,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/admin/users/{id}/organisations', { params: { path: { id: userId } } })
      if (!response.ok || !data) throw new Error("Could not load the user's organisations.")
      return data
    },
  })
}
