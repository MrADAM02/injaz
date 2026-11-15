import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { defineStore } from 'pinia'
import type { BoardEvent } from '~/types/api'

export type RealtimeStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting'

export interface BoardSubscription {
  onEvent: (event: BoardEvent) => void
  /** After a reconnect: events may have been missed, so reload the board. */
  onResync: () => void
  /** The caller was removed from the board's workspace. */
  onAccessRevoked: (workspaceId: string) => void
  /** JoinBoard was refused (e.g. `board.notFound`). */
  onJoinFailed?: (code: string) => void
}

/** Retry forever while signed in: quickly at first, then every 30 s (a phone coming back online, a redeploy). */
const retryDelays = [0, 1000, 3000, 10_000]
const retryPolicy = { nextRetryDelayInMilliseconds: ({ previousRetryCount }: { previousRetryCount: number }) => retryDelays[previousRetryCount] ?? 30_000 }

/**
 * One SignalR connection per tab (`/hubs/boards`), shared by whichever board is on screen. REST calls send its
 * `connectionId` as `X-Connection-Id`, so the server skips echoing our own changes back to us.
 */
export const useRealtimeStore = defineStore('realtime', () => {
  const config = useRuntimeConfig()

  const status = ref<RealtimeStatus>('idle')
  const connectionId = ref<string | null>(null)

  let connection: HubConnection | null = null
  let starting: Promise<void> | null = null
  let restartTimer: ReturnType<typeof setTimeout> | undefined
  let current: { boardId: string; subscription: BoardSubscription } | null = null

  function build(): HubConnection {
    const base = String(config.public.apiBase ?? '').replace(/\/+$/, '')
    const hub = new HubConnectionBuilder()
      .withUrl(`${base}/hubs/boards`, {
        // Called for every (re)connect, so an expired token is refreshed first.
        accessTokenFactory: async () => (await useAuthStore().getAccessToken()) ?? '',
        withCredentials: false,
      })
      .withAutomaticReconnect(retryPolicy)
      .configureLogging(import.meta.dev ? LogLevel.Warning : LogLevel.Error)
      .build()

    hub.on('boardEvent', (event: BoardEvent) => {
      if (current && event.boardId === current.boardId) current.subscription.onEvent(event)
    })
    hub.on('accessRevoked', (payload: { workspaceId: string }) => {
      current?.subscription.onAccessRevoked(payload.workspaceId)
    })
    hub.onreconnecting(() => {
      status.value = 'reconnecting'
      connectionId.value = null
    })
    hub.onreconnected(async (id) => {
      status.value = 'connected'
      connectionId.value = id ?? null
      if (current) {
        await join(current.boardId)
        current?.subscription.onResync()
      }
    })
    hub.onclose(() => {
      connectionId.value = null
      // Closed for good (the retry policy never gives up, so this is a failed start or a stop()).
      if (status.value !== 'idle') scheduleRestart()
    })
    return hub
  }

  async function ensureStarted(): Promise<void> {
    connection ??= build()
    if (connection.state === HubConnectionState.Connected) return
    if (starting) return starting
    if (connection.state !== HubConnectionState.Disconnected) return // (re)connecting already

    status.value = 'connecting'
    starting = connection
      .start()
      .then(() => {
        status.value = 'connected'
        connectionId.value = connection?.connectionId ?? null
      })
      .catch((error: unknown) => {
        scheduleRestart()
        throw error
      })
      .finally(() => {
        starting = null
      })
    return starting
  }

  function scheduleRestart() {
    status.value = 'reconnecting'
    clearTimeout(restartTimer)
    restartTimer = setTimeout(async () => {
      if (!current || !useAuthStore().isAuthenticated) return
      try {
        await ensureStarted()
        await join(current.boardId)
        current?.subscription.onResync()
      } catch {
        // ensureStarted already scheduled the next attempt.
      }
    }, 5000)
  }

  async function join(boardId: string) {
    try {
      await connection?.invoke('JoinBoard', boardId)
    } catch (error) {
      // HubException messages carry the API error code (e.g. "…: board.notFound").
      const message = error instanceof Error ? error.message : ''
      const code = /([a-z]+\.[a-zA-Z]+)\s*$/.exec(message)?.[1]
      if (code && current?.boardId === boardId) current.subscription.onJoinFailed?.(code)
      else throw error
    }
  }

  /** Follows one board: joins its group now and after every reconnect. Returns the unsubscribe function. */
  async function subscribe(boardId: string, subscription: BoardSubscription): Promise<() => void> {
    const previous = current
    current = { boardId, subscription }
    const unsubscribe = () => {
      if (current?.boardId !== boardId) return
      current = null
      if (connection?.state === HubConnectionState.Connected) void connection.invoke('LeaveBoard', boardId).catch(() => {})
    }

    try {
      await ensureStarted()
      if (previous && previous.boardId !== boardId) await connection?.invoke('LeaveBoard', previous.boardId).catch(() => {})
      if (current?.boardId === boardId) await join(boardId)
    } catch {
      // Offline or the API is down: the board still works over REST; the restart loop joins later.
    }
    return unsubscribe
  }

  /** Sign-out: drop the connection (its token belongs to the old session). */
  async function stop() {
    current = null
    status.value = 'idle'
    clearTimeout(restartTimer)
    const old = connection
    connection = null
    connectionId.value = null
    await old?.stop().catch(() => {})
  }

  return { status, connectionId, subscribe, stop }
})
