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
    })

    return { provide: { api } }
  },
})
