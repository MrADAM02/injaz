<script setup lang="ts">
import type { Activity, Page } from '~/types/api'
import { activityText } from '~/utils/activity'

/** Board or card history, newest first, paginated. Reloads when `revision` changes (live updates). */
const props = defineProps<{ source: { boardId: string } | { cardId: string }; revision?: number }>()

const { t, te } = useI18n()
const api = useApi()
const format = useFormat()
const problem = useProblem()

const inCard = computed(() => 'cardId' in props.source)
const path = computed(() => ('cardId' in props.source ? `/api/cards/${props.source.cardId}/activity` : `/api/boards/${props.source.boardId}/activity`))

const feed = usePaged<Activity>((cursor) => api.get<Page<Activity>>(path.value, { cursor, limit: 25 }))
watch([path, () => props.revision], () => void feed.load(), { immediate: true })

const ctx = computed(() => ({
  t: (key: string, params?: Record<string, unknown>) => t(key, params ?? {}),
  te: (key: string) => te(key),
  formatDate: (iso: string) => format.dateTime(iso),
  inCard: inCard.value,
}))
</script>

<template>
  <div>
    <div v-if="feed.loading.value && !feed.loaded.value" class="flex justify-center py-6 text-muted"><UiSpinner :label="t('common.loading')" /></div>
    <UiAlert v-else-if="feed.error.value && !feed.loaded.value">
      {{ problem.message(feed.error.value) }}
      <button type="button" class="ms-2 font-semibold underline" @click="feed.load()">{{ t('common.retry') }}</button>
    </UiAlert>
    <p v-else-if="feed.items.value.length === 0" class="py-4 text-sm text-muted">{{ t('activity.none') }}</p>

    <ol v-else class="flex flex-col gap-3">
      <li v-for="entry in feed.items.value" :key="entry.id" class="flex gap-3">
        <UiAvatar :name="entry.actor.displayName" :src="entry.actor.avatarUrl" size="sm" />
        <div class="min-w-0 flex-1 text-sm">
          <p class="break-words">
            <span class="font-semibold" dir="auto">{{ entry.actor.displayName }}</span>
            {{ ' ' }}
            <span>{{ activityText(entry, ctx) }}</span>
          </p>
          <time class="text-xs text-muted" :datetime="entry.createdAt" :title="format.dateTime(entry.createdAt)">{{ format.relative(entry.createdAt) }}</time>
        </div>
      </li>
    </ol>

    <div v-if="feed.hasMore.value" class="mt-4 flex justify-center">
      <UiButton variant="secondary" size="sm" :loading="feed.loadingMore.value" @click="feed.more()">{{ t('common.loadMore') }}</UiButton>
    </div>
  </div>
</template>
