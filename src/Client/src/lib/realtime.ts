import { useEffect, useRef } from 'react'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { getOrganisationId, getTokens, subscribeTokens } from './api/authTokens'
import { refreshTokens } from './api/client'

// The access token for (re)connecting, refreshed first if it expires within 30 seconds.
export async function freshAccessToken(): Promise<string> {
  const tokens = getTokens()
  if (tokens && Date.parse(tokens.expiresAtUtc) - Date.now() < 30_000) {
    await refreshTokens()
  }
  return getTokens()?.accessToken ?? ''
}

// A message from the server: Wolverine sends each one as CloudEvents JSON, { type, data, ... },
// where type is the message's stable name (such as "todo-changed").
export interface RealtimeMessage {
  type?: string
  data?: unknown
}

interface Listener {
  onMessage: (message: RealtimeMessage) => void
  // After a reconnect: messages may have been missed, so refetch what they would have changed.
  onResync?: () => void
}

// One SignalR connection per tab (AppHub at /api/hub), shared by every part of the page that
// listens, and open while at least one does. With organisations, the hub sends each connection its
// organisation's updates, so it reconnects when the user switches.
const listeners = new Set<Listener>()
let connection: HubConnection | null = null
let started: Promise<boolean> = Promise.resolve(false)
let restart: ReturnType<typeof setTimeout> | undefined
let organisationId: string | null = null
let unsubscribeTokens: (() => void) | null = null

const resync = () => listeners.forEach((l) => l.onResync?.())

function connect() {
  organisationId = getOrganisationId()
  const current = new HubConnectionBuilder()
    .withUrl('/api/hub', {
      // Read on every (re)connect. The server closes the connection when its access token expires,
      // so refresh a token that has expired or is about to before reconnecting. With no login
      // (Identity off) there is no token and the hub doesn't require one.
      accessTokenFactory: freshAccessToken,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
  connection = current

  current.on('ReceiveMessage', (json: string) => {
    const message = JSON.parse(json) as RealtimeMessage
    listeners.forEach((l) => l.onMessage(message))
  })
  current.onreconnected(resync)

  const start = () =>
    current.start().then(
      () => true,
      (error: unknown) => {
        // React's StrictMode mounts twice in development, stopping the first connection mid-start;
        // that and a hub that is briefly unavailable are not worth surfacing.
        console.debug('Realtime updates unavailable', error)
        return false
      },
    )
  started = start()

  // Closed for good: by the server when the access token expired (which automatic reconnect
  // doesn't retry), or after automatic reconnect gave up. Start again a little later, until it
  // works, and refetch what may have been missed.
  const restartLater = () => {
    if (connection !== current) return
    restart = setTimeout(() => {
      started = start()
      void started.then((ok) => (ok ? resync() : restartLater()))
    }, 5_000)
  }
  current.onclose(restartLater)
}

function disconnect() {
  clearTimeout(restart)
  const current = connection
  connection = null
  if (current) void started.then(() => current.stop())
}

function add(listener: Listener) {
  listeners.add(listener)
  if (listeners.size === 1) {
    connect()
    unsubscribeTokens = subscribeTokens(() => {
      if (getOrganisationId() !== organisationId && connection) {
        disconnect()
        connect()
        resync()
      }
    })
  }
  return () => {
    listeners.delete(listener)
    if (listeners.size === 0) {
      unsubscribeTokens?.()
      unsubscribeTokens = null
      disconnect()
    }
  }
}

// Calls onMessage for every realtime message while the component is mounted, and onResync after a
// reconnect. The callbacks may change between renders.
export function useRealtime(onMessage: (message: RealtimeMessage) => void, onResync?: () => void) {
  const latest = useRef({ onMessage, onResync })
  useEffect(() => {
    latest.current = { onMessage, onResync }
  })
  useEffect(
    () =>
      add({
        onMessage: (message) => latest.current.onMessage(message),
        onResync: () => latest.current.onResync?.(),
      }),
    [],
  )
}
