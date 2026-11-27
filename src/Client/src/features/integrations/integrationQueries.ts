import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

export const integrationKeys = {
  apiKeys: ['organisation', 'api-keys'] as const,
  webhooks: ['organisation', 'webhooks'] as const,
  webhookEvents: ['organisation', 'webhook-events'] as const,
  deliveries: (id: string) => ['organisation', 'webhooks', id, 'deliveries'] as const,
  secrets: ['organisation', 'secrets'] as const,
}

export const scopes = ['read', 'write'] as const
export type Scope = (typeof scopes)[number]

// API keys: owners and admins of the current organisation.
export function useApiKeys() {
  return useQuery({
    queryKey: integrationKeys.apiKeys,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/api-keys')
      if (!response.ok || !data) throw new Error('Could not load the API keys.')
      return data
    },
  })
}

// Creating or rotating a key answers with the key itself, shown this once.
export function useCreateApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (key: { name: string; scope: Scope; expiresInDays: number | null }) => {
      const { data, error, response } = await api.POST('/api/organisation/api-keys', { body: key })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not create the key.'))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.apiKeys }),
  })
}

export function useRotateApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      const { data, error, response } = await api.POST('/api/organisation/api-keys/{id}/rotate', { params: { path: { id } } })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not rotate the key.'))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.apiKeys }),
  })
}

export function useRevokeApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      const { response } = await api.DELETE('/api/organisation/api-keys/{id}', { params: { path: { id } } })
      if (!response.ok) throw new Error('Could not revoke the key.')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.apiKeys }),
  })
}

export function useWebhooks() {
  return useQuery({
    queryKey: integrationKeys.webhooks,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/webhooks')
      if (!response.ok || !data) throw new Error('Could not load the webhooks.')
      return data
    },
  })
}

export function useWebhookEvents() {
  return useQuery({
    queryKey: integrationKeys.webhookEvents,
    staleTime: Infinity,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/webhooks/events')
      if (!response.ok || !data) throw new Error('Could not load the event types.')
      return data
    },
  })
}

export interface WebhookInput {
  url: string
  description: string
  // Empty for every event.
  events: string[]
  enabled: boolean
}

export function useCreateWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (webhook: WebhookInput) => {
      const { data, error, response } = await api.POST('/api/organisation/webhooks', { body: webhook })
      if (!response.ok || !data) throw new Error(problemMessage(error, 'Could not add the webhook.'))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.webhooks }),
  })
}

export function useUpdateWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...webhook }: WebhookInput & { id: string }) => {
      const { error, response } = await api.PUT('/api/organisation/webhooks/{id}', { params: { path: { id } }, body: webhook })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not save the webhook.'))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.webhooks }),
  })
}

export function useDeleteWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      const { response } = await api.DELETE('/api/organisation/webhooks/{id}', { params: { path: { id } } })
      if (!response.ok) throw new Error('Could not delete the webhook.')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.webhooks }),
  })
}

export function useWebhookSecret() {
  return useMutation({
    mutationFn: async (id: string) => {
      const { data, response } = await api.GET('/api/organisation/webhooks/{id}/secret', { params: { path: { id } } })
      if (!response.ok || !data) throw new Error('Could not load the signing secret.')
      return data.secret
    },
  })
}

export function useDeliveries(id: string) {
  return useQuery({
    queryKey: integrationKeys.deliveries(id),
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/webhooks/{id}/deliveries', { params: { path: { id } } })
      if (!response.ok || !data) throw new Error('Could not load the deliveries.')
      return data
    },
  })
}

// Sending happens in the background: refresh the deliveries shortly after.
function refreshDeliveriesSoon(queryClient: ReturnType<typeof useQueryClient>, id: string) {
  setTimeout(() => void queryClient.invalidateQueries({ queryKey: integrationKeys.deliveries(id) }), 1500)
}

export function useSendTestEvent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      const { response } = await api.POST('/api/organisation/webhooks/{id}/test', { params: { path: { id } } })
      if (!response.ok) throw new Error('Could not send the test event.')
    },
    onSuccess: (_, id) => refreshDeliveriesSoon(queryClient, id),
  })
}

export function useResendDelivery(webhookId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (deliveryId: string) => {
      const { response } = await api.POST('/api/organisation/webhooks/deliveries/{deliveryId}/resend', { params: { path: { deliveryId } } })
      if (!response.ok) throw new Error('Could not resend it.')
    },
    onSuccess: () => refreshDeliveriesSoon(queryClient, webhookId),
  })
}

// Secrets: owners only. Values are never sent back.
export function useSecrets(enabled: boolean) {
  return useQuery({
    queryKey: integrationKeys.secrets,
    enabled,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/organisation/secrets')
      if (!response.ok || !data) throw new Error('Could not load the secrets.')
      return data
    },
  })
}

export function useSetSecret() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ name, value }: { name: string; value: string }) => {
      const { error, response } = await api.PUT('/api/organisation/secrets/{name}', { params: { path: { name } }, body: { value } })
      if (!response.ok) throw new Error(problemMessage(error, 'Could not save the secret.'))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.secrets }),
  })
}

export function useDeleteSecret() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (name: string) => {
      const { response } = await api.DELETE('/api/organisation/secrets/{name}', { params: { path: { name } } })
      if (!response.ok) throw new Error('Could not delete the secret.')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: integrationKeys.secrets }),
  })
}
