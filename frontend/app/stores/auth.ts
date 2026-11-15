import { defineStore } from 'pinia'
import type { RouteLocationRaw } from 'vue-router'
import type { AuthSession, Locale, User } from '~/types/api'
import { isApiError } from '~/utils/api-client'
import { singleFlight } from '~/utils/single-flight'

/** Refresh this long before the access token expires, so in-flight requests never carry a stale token. */
const refreshMargin = 30_000

/**
 * The session. The access token lives only in memory; the refresh token is an httpOnly cookie the browser
 * sends to /api/auth/*. A page load therefore starts signed out and restores the session with one refresh.
 */
export const useAuthStore = defineStore('auth', () => {
  const { $api } = useNuxtApp()
  const router = useRouter()

  const user = ref<User | null>(null)
  const accessToken = shallowRef<string | null>(null)
  const expiresAt = ref(0)
  const initialized = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  function apply(session: AuthSession) {
    accessToken.value = session.accessToken
    expiresAt.value = Date.parse(session.accessTokenExpiresAt)
    user.value = session.user
  }

  function clear() {
    accessToken.value = null
    expiresAt.value = 0
    user.value = null
  }

  /**
   * Rotates the refresh cookie for a new access token. Single-flight: concurrent callers (boot, parallel 401s)
   * share one request. `auth.refreshSuperseded` means another tab rotated the cookie a moment ago; the browser
   * already holds the successor, so retry once. Network failures throw (the session may still be fine).
   */
  const refresh = singleFlight(async (): Promise<boolean> => {
    for (let attempt = 0; ; attempt++) {
      try {
        apply(await $api.post<AuthSession>('/api/auth/refresh', undefined, { auth: false }))
        return true
      } catch (error) {
        if (attempt === 0 && isApiError(error, 'auth.refreshSuperseded')) {
          await new Promise((resolve) => setTimeout(resolve, 250))
          continue
        }
        if (isApiError(error) && error.status === 401) {
          clear()
          return false
        }
        throw error
      }
    }
  })

  /** Restores the session from the cookie once per page load. Offline counts as signed out. */
  const ensureInitialized = singleFlight(async () => {
    if (initialized.value) return
    try {
      await refresh()
    } catch {
      clear()
    } finally {
      initialized.value = true
    }
  })

  async function getAccessToken(): Promise<string | null> {
    if (!accessToken.value) return null
    if (Date.now() >= expiresAt.value - refreshMargin) await refresh()
    return accessToken.value
  }

  async function login(email: string, password: string) {
    apply(await $api.post<AuthSession>('/api/auth/login', { email, password }, { auth: false }))
    broadcast('login')
  }

  async function register(input: { displayName: string; email: string; password: string; locale: Locale }) {
    apply(await $api.post<AuthSession>('/api/auth/register', input, { auth: false }))
    broadcast('login')
  }

  /** Ends this session on the server (best effort) and locally, then tells other tabs. */
  async function logout(next: RouteLocationRaw = '/login') {
    try {
      await $api.post('/api/auth/logout', undefined, { auth: false })
    } catch {
      // Already gone or offline: the local session is cleared either way.
    }
    signedOut()
    broadcast('logout')
    await router.push(next)
  }

  /** The password change signs out other sessions and returns a fresh one for this tab. */
  async function changePassword(currentPassword: string, newPassword: string) {
    apply(await $api.put<AuthSession>('/api/me/password', { currentPassword, newPassword }))
  }

  function setUser(next: User) {
    user.value = next
  }

  /** A request stayed unauthorized after refreshing: the session is over. */
  function expire() {
    if (!user.value) return
    signedOut()
    useToastStore().push('auth.sessionExpired', 'info', true)
    const current = router.currentRoute.value
    void router.push({ path: '/login', query: current.meta.public ? {} : { redirect: current.fullPath } })
  }

  function signedOut() {
    clear()
    useWorkspacesStore().reset()
  }

  // ----- other tabs -----
  const channel = import.meta.client && 'BroadcastChannel' in window ? new BroadcastChannel('injaz-auth') : null

  function broadcast(type: 'login' | 'logout') {
    channel?.postMessage({ type })
  }

  channel?.addEventListener('message', (event: MessageEvent<{ type?: string }>) => {
    if (event.data?.type === 'logout' && user.value) {
      signedOut()
      void router.push('/login')
    } else if (event.data?.type === 'login' && !user.value) {
      // Another tab signed in; the shared cookie lets this one pick up a session too.
      refresh().then(
        (ok) => {
          if (ok && router.currentRoute.value.meta.guestOnly) void router.push('/')
        },
        () => {},
      )
    }
  })

  return {
    user,
    initialized,
    isAuthenticated,
    refresh,
    ensureInitialized,
    getAccessToken,
    login,
    register,
    logout,
    changePassword,
    setUser,
    expire,
  }
})
