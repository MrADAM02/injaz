import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import type { DueBucket } from '~/types/api'
import { hasFilters, type CardFilters } from '~/utils/cards'

const dueBuckets: readonly DueBucket[] = ['overdue', 'today', 'week', 'none']

function list(value: LocationQuery[string] | undefined): string[] {
  const raw = Array.isArray(value) ? value.join(',') : (value ?? '')
  return raw.split(',').filter(Boolean)
}

/**
 * Board filters live in the URL (`?q=&labels=&members=&due=`): shareable, kept on reload, and the card
 * dialog (a child route) keeps them by passing the query along.
 */
export function useBoardFilters() {
  const route = useRoute()
  const router = useRouter()

  const filters = computed<CardFilters>(() => {
    const due = typeof route.query.due === 'string' && (dueBuckets as readonly string[]).includes(route.query.due) ? (route.query.due as DueBucket) : null
    return {
      q: typeof route.query.q === 'string' ? route.query.q : '',
      labelIds: list(route.query.labels),
      assigneeIds: list(route.query.members),
      due,
    }
  })

  const active = computed(() => hasFilters(filters.value))
  const count = computed(
    () => (filters.value.q.trim() ? 1 : 0) + filters.value.labelIds.length + filters.value.assigneeIds.length + (filters.value.due ? 1 : 0),
  )

  function set(next: Partial<CardFilters>) {
    const merged = { ...filters.value, ...next }
    const query: LocationQueryRaw = { ...route.query }
    const assign = (key: string, value: string | null) => {
      if (value) query[key] = value
      else delete query[key]
    }
    assign('q', merged.q || null)
    assign('labels', merged.labelIds.join(',') || null)
    assign('members', merged.assigneeIds.join(',') || null)
    assign('due', merged.due)
    void router.replace({ query })
  }

  function toggle(key: 'labelIds' | 'assigneeIds', value: string) {
    const current = filters.value[key]
    set({ [key]: current.includes(value) ? current.filter((x) => x !== value) : [...current, value] })
  }

  function clear() {
    set({ q: '', labelIds: [], assigneeIds: [], due: null })
  }

  /** The route query without filters (for links that should drop them). */
  const query = computed(() => route.query)

  return { filters, active, count, set, toggle, clear, query }
}
