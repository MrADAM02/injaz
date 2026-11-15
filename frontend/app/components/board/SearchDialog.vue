<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'
import type { Card, CardSearchResult, Page } from '~/types/api'

/**
 * Server-side search: what the loaded board can't answer (text inside descriptions, archived cards).
 * Archived results can be restored from here.
 */
const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ cardLink: (cardId: string) => RouteLocationRaw; initialArchived?: boolean }>()

const { t } = useI18n()
const api = useApi()
const board = useBoardStore()
const toast = useToast()
const problem = useProblem()
const format = useFormat()

const q = ref('')
const archived = ref(false)
const restoring = ref<string | null>(null)

const results = usePaged<CardSearchResult>((cursor) =>
  api.get<Page<CardSearchResult>>(`/api/boards/${board.id}/cards`, { q: q.value.trim() || undefined, archived: archived.value || undefined, cursor, limit: 20 }),
)

let timer: ReturnType<typeof setTimeout> | undefined
watch(q, () => {
  clearTimeout(timer)
  timer = setTimeout(() => void results.load(), 300)
})
watch(archived, () => void results.load())
watch(open, (value) => {
  if (!value) return
  archived.value = props.initialArchived ?? false
  void results.load()
})
onBeforeUnmount(() => clearTimeout(timer))

async function restore(result: CardSearchResult) {
  restoring.value = result.card.id
  try {
    const card = await api.patch<Card>(`/api/cards/${result.card.id}`, { isArchived: false })
    board.syncCard(card)
    results.items.value = results.items.value.filter((r) => r.card.id !== result.card.id)
    toast.success(t('card.restored'))
  } catch (e) {
    toast.error(e)
  } finally {
    restoring.value = null
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('search.title')" size="lg">
    <div class="flex flex-col gap-4">
      <UiInput v-model="q" type="search" :label="t('search.query')" :placeholder="t('search.placeholder')" autocomplete="off" inputmode="search" autofocus />

      <div class="inline-flex self-start rounded-xl bg-subtle p-1" role="radiogroup" :aria-label="t('search.scope')">
        <button
          v-for="option in [false, true]"
          :key="String(option)"
          type="button"
          role="radio"
          :aria-checked="archived === option"
          class="min-h-9 rounded-lg px-3 text-sm font-medium transition-colors"
          :class="archived === option ? 'bg-surface shadow-xs' : 'text-muted hover:text-ink'"
          @click="archived = option"
        >
          {{ option ? t('search.archived') : t('search.open') }}
        </button>
      </div>

      <div v-if="results.loading.value" class="flex justify-center py-8 text-muted"><UiSpinner :label="t('common.loading')" /></div>
      <UiAlert v-else-if="results.error.value">{{ problem.message(results.error.value) }}</UiAlert>
      <p v-else-if="results.items.value.length === 0" class="py-6 text-center text-sm text-muted">{{ t('search.none') }}</p>

      <ul v-else class="-mx-5 divide-y divide-line">
        <li v-for="result in results.items.value" :key="result.card.id" class="flex items-center gap-3 px-5 py-2.5">
          <NuxtLink :to="props.cardLink(result.card.id)" class="min-w-0 flex-1 rounded-lg py-1 hover:text-accent-strong" @click="open = false">
            <span class="block truncate text-sm font-medium" dir="auto">{{ result.card.title }}</span>
            <span class="block truncate text-xs text-muted">
              {{ t('search.inList', { list: result.listTitle }) }} · {{ format.relative(result.updatedAt) }}
            </span>
          </NuxtLink>
          <UiButton
            v-if="result.isArchived && board.allows('editContent')"
            variant="secondary"
            size="sm"
            icon="restore"
            :loading="restoring === result.card.id"
            @click="restore(result)"
          >
            {{ t('card.restore') }}
          </UiButton>
        </li>
      </ul>

      <UiButton v-if="results.hasMore.value" variant="secondary" :loading="results.loadingMore.value" class="self-center" @click="results.more()">
        {{ t('common.loadMore') }}
      </UiButton>
    </div>
  </UiModal>
</template>
