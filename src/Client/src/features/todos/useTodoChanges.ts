import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { getTokens } from '../../lib/api/authTokens'
import { todoKeys } from './todoQueries'

// Keeps the cached Todo list current when it changes elsewhere, including in another tab.
// The API pushes "todo-changed" messages over SignalR (AppHub at /api/hub); each one marks the
// list stale, and TanStack Query refetches it wherever it's shown.
export function useTodoChanges() {
  const queryClient = useQueryClient()

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
      const message = JSON.parse(json) as { type?: string }
      if (message.type === 'todo-changed') {
        void queryClient.invalidateQueries({ queryKey: todoKeys.all })
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
}
