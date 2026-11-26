import { useCallback, useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { getTokens } from '../../lib/api/authTokens'
import { todoKeys } from './todoQueries'

export interface TodoReminder {
  todoId: string
  title: string
}

// Live updates over SignalR (AppHub at /api/hub), one connection per tab:
// - "todo-changed": a Todo was created or completed, here or in another tab. It marks the
//   cached Todo list stale, and TanStack Query refetches it.
// - "todo-reminder": a "remind me" reminder is due. It's added to `reminders` to show.
export function useTodoRealtime() {
  const queryClient = useQueryClient()
  const [reminders, setReminders] = useState<TodoReminder[]>([])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/api/hub', {
        // Read on every (re)connect, so a refreshed access token is picked up. With no
        // login (Identity off) there is no token and the hub doesn't require one.
        accessTokenFactory: () => getTokens()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    // Wolverine sends each message as CloudEvents JSON text: { type, data, ... }.
    connection.on('ReceiveMessage', (json: string) => {
      const message = JSON.parse(json) as { type?: string; data?: TodoReminder }
      if (message.type === 'todo-changed') {
        void queryClient.invalidateQueries({ queryKey: todoKeys.all })
      } else if (message.type === 'todo-reminder' && message.data) {
        const reminder = message.data
        setReminders((current) => [...current.filter((r) => r.todoId !== reminder.todoId), reminder])
      }
    })

    // Changes may have been missed while disconnected.
    connection.onreconnected(() => void queryClient.invalidateQueries({ queryKey: todoKeys.all }))

    const started = connection.start().catch((error: unknown) => {
      // React's StrictMode mounts twice in development, stopping the first connection
      // mid-start; that and a hub that is briefly unavailable are not worth surfacing.
      console.debug('Realtime updates unavailable', error)
    })

    return () => {
      void started.then(() => connection.stop())
    }
  }, [queryClient])

  const dismiss = useCallback(
    (todoId: string) => setReminders((current) => current.filter((r) => r.todoId !== todoId)),
    [],
  )

  return { reminders, dismiss }
}
