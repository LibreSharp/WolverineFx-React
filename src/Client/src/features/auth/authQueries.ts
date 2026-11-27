import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api/client'

// What the API offers for signing in and up (for example, whether open registration is on).
export function useAuthOptions() {
  return useQuery({
    queryKey: ['auth-options'],
    queryFn: async () => {
      const { data, response } = await api.GET('/api/auth/options')
      if (!response.ok || !data) throw new Error('Could not load the sign-in options.')
      return data
    },
    staleTime: Infinity,
  })
}
