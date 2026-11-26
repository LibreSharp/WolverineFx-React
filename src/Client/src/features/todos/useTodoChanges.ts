import { useEffect, useRef } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { getTokens } from '../../lib/api/authTokens'

// Calls onChange whenever the API reports that a Todo was created or completed, including
// from another tab. The API pushes "todo-changed" messages over SignalR (AppHub at /api/hub);
// the caller simply refetches, which keeps this in sync without merging changes by hand.
export function useTodoChanges(onChange: () => void) {
  const onChangeRef = useRef(onChange)
  useEffect(() => {
    onChangeRef.current = onChange
  })

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
        onChangeRef.current()
      }
    })

    // Changes may have been missed while disconnected.
    connection.onreconnected(() => onChangeRef.current())

    const started = connection.start().catch((error: unknown) => {
      // React's StrictMode mounts twice in development, stopping the first connection
      // mid-start; that and a hub that is briefly unavailable are not worth surfacing.
      console.debug('Realtime updates unavailable', error)
    })

    return () => {
      void started.then(() => connection.stop())
    }
  }, [])
}
