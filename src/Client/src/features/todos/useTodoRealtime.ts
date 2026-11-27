import { useCallback, useEffect, useState, useSyncExternalStore } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { getOrganisationId, getTokens, subscribeTokens } from '../../lib/api/authTokens'
import { refreshTokens } from '../../lib/api/client'
import { todoKeys } from './todoQueries'

// The access token for (re)connecting, refreshed first if it expires within 30 seconds.
export async function freshAccessToken(): Promise<string> {
  const tokens = getTokens()
  if (tokens && Date.parse(tokens.expiresAtUtc) - Date.now() < 30_000) {
    await refreshTokens()
  }
  return getTokens()?.accessToken ?? ''
}

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
  // With organisations, the hub sends each connection its organisation's updates: reconnect when
  // the user switches.
  const organisationId = useSyncExternalStore(subscribeTokens, getOrganisationId, () => null)

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/api/hub', {
        // Read on every (re)connect. The server closes the connection when its access token
        // expires, so refresh a token that has expired or is about to before reconnecting. With
        // no login (Identity off) there is no token and the hub doesn't require one.
        accessTokenFactory: freshAccessToken,
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

    const start = () =>
      connection.start().then(
        () => true,
        (error: unknown) => {
          // React's StrictMode mounts twice in development, stopping the first connection
          // mid-start; that and a hub that is briefly unavailable are not worth surfacing.
          console.debug('Realtime updates unavailable', error)
          return false
        },
      )
    let started = start()

    // Closed for good: by the server when the access token expired (which automatic reconnect
    // doesn't retry), or after automatic reconnect gave up. Start again a little later, until it
    // works, and refetch what may have been missed.
    let stopped = false
    let restart: ReturnType<typeof setTimeout> | undefined
    const restartLater = () => {
      if (stopped) return
      restart = setTimeout(() => {
        started = start()
        void started.then((ok) => (ok ? queryClient.invalidateQueries({ queryKey: todoKeys.all }) : restartLater()))
      }, 5_000)
    }
    connection.onclose(restartLater)

    return () => {
      stopped = true
      clearTimeout(restart)
      void started.then(() => connection.stop())
    }
  }, [queryClient, organisationId])

  const dismiss = useCallback(
    (todoId: string) => setReminders((current) => current.filter((r) => r.todoId !== todoId)),
    [],
  )

  return { reminders, dismiss }
}
