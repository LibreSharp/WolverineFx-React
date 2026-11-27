import { useCallback, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useRealtime } from '../../lib/realtime'
import { todoKeys } from './todoQueries'

export { freshAccessToken } from '../../lib/realtime'

export interface TodoReminder {
  todoId: string
  title: string
}

// Live updates for the Todos, over the tab's realtime connection (lib/realtime.ts):
// - "todo-changed": a Todo was created or completed, here or in another tab. It marks the
//   cached Todo list stale, and TanStack Query refetches it.
// - "todo-reminder": a "remind me" reminder is due. It's added to `reminders` to show.
export function useTodoRealtime() {
  const queryClient = useQueryClient()
  const [reminders, setReminders] = useState<TodoReminder[]>([])

  useRealtime(
    (message) => {
      if (message.type === 'todo-changed') {
        void queryClient.invalidateQueries({ queryKey: todoKeys.all })
      } else if (message.type === 'todo-reminder' && message.data) {
        const reminder = message.data as TodoReminder
        setReminders((current) => [...current.filter((r) => r.todoId !== reminder.todoId), reminder])
      }
    },
    // Changes may have been missed while disconnected, or in the organisation switched to.
    () => void queryClient.invalidateQueries({ queryKey: todoKeys.all }),
  )

  const dismiss = useCallback(
    (todoId: string) => setReminders((current) => current.filter((r) => r.todoId !== todoId)),
    [],
  )

  return { reminders, dismiss }
}
