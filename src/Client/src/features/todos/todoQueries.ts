import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api/client'
import { problemMessage } from '../../lib/api/problemDetails'

// Cache key for the Todo list. Anything that changes Todos invalidates it, which refetches
// the list wherever it's shown.
export const todoKeys = {
  all: ['todos'] as const,
}

export function useTodos() {
  return useQuery({
    queryKey: todoKeys.all,
    queryFn: async () => {
      const { data, response } = await api.GET('/api/todos')
      if (!response.ok || !data) throw new Error('Could not load your todos.')
      return data
    },
  })
}

export function useCreateTodo() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (title: string) => {
      const { error, response } = await api.POST('/api/todos', { body: { title } })
      // The API validates with FluentValidation and answers 400 with ProblemDetails.
      if (!response.ok) throw new Error(problemMessage(error, 'Could not add the todo.'))
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: todoKeys.all }),
  })
}

export function useCompleteTodo() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      const { response } = await api.POST('/api/todos/{id}/complete', { params: { path: { id } } })
      if (!response.ok) throw new Error('Could not complete the todo.')
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: todoKeys.all }),
  })
}
