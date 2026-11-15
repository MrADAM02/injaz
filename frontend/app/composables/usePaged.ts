import type { Page } from '~/types/api'

/**
 * A keyset-paginated list (comments, activity, search, My Tasks): `load()` starts over, `more()` appends the
 * next page. A newer `load()` wins over a slower, older one.
 */
export function usePaged<T>(fetchPage: (cursor: string | null) => Promise<Page<T>>) {
  const items = ref<T[]>([]) as Ref<T[]>
  const nextCursor = ref<string | null>(null)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<unknown>(null)
  const loaded = ref(false)
  let generation = 0

  async function load() {
    const mine = ++generation
    loading.value = true
    error.value = null
    try {
      const page = await fetchPage(null)
      if (mine !== generation) return
      items.value = page.items
      nextCursor.value = page.nextCursor
      loaded.value = true
    } catch (e) {
      if (mine === generation) error.value = e
    } finally {
      if (mine === generation) loading.value = false
    }
  }

  async function more() {
    if (!nextCursor.value || loadingMore.value) return
    const mine = generation
    loadingMore.value = true
    try {
      const page = await fetchPage(nextCursor.value)
      if (mine !== generation) return
      items.value = [...items.value, ...page.items]
      nextCursor.value = page.nextCursor
    } catch (e) {
      if (mine === generation) error.value = e
    } finally {
      loadingMore.value = false
    }
  }

  const hasMore = computed(() => nextCursor.value !== null)

  return { items, nextCursor, loading, loadingMore, error, loaded, hasMore, load, more }
}
