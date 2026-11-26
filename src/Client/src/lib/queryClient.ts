import { QueryClient } from '@tanstack/react-query'

// One cache for the whole app. Server data is refreshed when it's invalidated (after a
// change here, or a SignalR "todo-changed" message from another tab), so it can stay
// fresh for a while without refetching on every render.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1 },
  },
})
