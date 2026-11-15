import { createApiClient } from '~/utils/api-client'

/** The API client every store and page uses (`useApi()`). Auth hooks resolve the store lazily. */
export default defineNuxtPlugin({
  name: 'injaz:api',
  setup(nuxtApp) {
    const config = useRuntimeConfig()
    const auth = () => useAuthStore(nuxtApp.$pinia)

    const api = createApiClient({
      baseURL: String(config.public.apiBase ?? '').replace(/\/+$/, ''),
      getAccessToken: () => auth().getAccessToken(),
      refreshAccessToken: () => auth().refresh(),
      onSessionExpired: () => auth().expire(),
      // Lets the server skip echoing our own changes back over the real-time connection.
      headers: (): Record<string, string> => {
        const id = useRealtimeStore(nuxtApp.$pinia).connectionId
        return id ? { 'X-Connection-Id': id } : {}
      },
    })

    return { provide: { api } }
  },
})
