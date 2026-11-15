import type { ApiClient } from '~/utils/api-client'

export function useApi(): ApiClient {
  return useNuxtApp().$api
}
