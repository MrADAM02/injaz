<script setup lang="ts">
import type { LocationQueryRaw } from 'vue-router'
import type { Card, CardPriority, DueBucket, MyTask, Page } from '~/types/api'
import { browserTimeZone, dueState, dueTone, priorities, priorityTone } from '~/utils/cards'
import { paletteFill } from '~/utils/palette'

/**
 * Everything assigned to me across my workspaces, soonest due first. Filters live in the URL. Ticking a task
 * completes it in place (it stays visible, struck through, so a mis-tap is easy to undo).
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const api = useApi()
const workspaces = useWorkspacesStore()
const toast = useToast()
const problem = useProblem()
const format = useFormat()
usePageTitle(() => t('myTasks.title'))

const dueBuckets: readonly DueBucket[] = ['overdue', 'today', 'week', 'none']

const due = computed(() => (dueBuckets as readonly string[]).includes(String(route.query.due)) ? (route.query.due as DueBucket) : null)
const priority = computed(() => (priorities as readonly string[]).includes(String(route.query.priority)) ? (route.query.priority as CardPriority) : null)
const workspaceId = computed(() => (typeof route.query.workspace === 'string' ? route.query.workspace : null))
const includeCompleted = computed(() => route.query.done === '1')

function setQuery(patch: Record<string, string | null>) {
  const query: LocationQueryRaw = { ...route.query }
  for (const [key, value] of Object.entries(patch)) {
    if (value) query[key] = value
    else delete query[key]
  }
  void router.replace({ query })
}

const tz = browserTimeZone()
const tasks = usePaged<MyTask>((cursor) =>
  api.get<Page<MyTask>>('/api/me/tasks', {
    due: due.value,
    priority: priority.value,
    workspaceId: workspaceId.value,
    includeCompleted: includeCompleted.value || undefined,
    tz,
    cursor,
    limit: 30,
  }),
)
watch([due, priority, workspaceId, includeCompleted], () => void tasks.load(), { immediate: true })

const priorityModel = computed({
  get: () => priority.value ?? '',
  set: (value: string) => setQuery({ priority: value || null }),
})
const priorityOptions = computed(() => [{ value: '', label: t('myTasks.anyPriority') }, ...priorities.filter((p) => p !== 'none').map((p) => ({ value: p, label: t(`priority.${p}`) }))])

const workspaceModel = computed({
  get: () => workspaceId.value ?? '',
  set: (value: string) => setQuery({ workspace: value || null }),
})
const workspaceOptions = computed(() => [{ value: '', label: t('myTasks.allWorkspaces') }, ...workspaces.items.map((w) => ({ value: w.id, label: w.name }))])

const toggling = ref<string | null>(null)

async function toggle(task: MyTask) {
  const done = task.card.completedAt === null
  toggling.value = task.card.id
  try {
    const card = await api.patch<Card>(`/api/cards/${task.card.id}`, { isCompleted: done })
    tasks.items.value = tasks.items.value.map((item) => (item.card.id === card.id ? { ...item, card: { ...item.card, completedAt: card.completedAt } } : item))
    if (done) toast.success(t('myTasks.completed'))
  } catch (e) {
    toast.error(e)
  } finally {
    toggling.value = null
  }
}
</script>

<template>
  <div>
    <h1 class="mb-4 hidden text-2xl font-bold md:block">{{ t('myTasks.title') }}</h1>

    <div class="mb-4 flex flex-col gap-3">
      <div class="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:px-0" role="radiogroup" :aria-label="t('filters.due')">
        <button
          v-for="option in [null, ...dueBuckets]"
          :key="option ?? 'all'"
          type="button"
          role="radio"
          :aria-checked="due === option"
          class="min-h-10 shrink-0 rounded-full border px-4 text-sm transition-colors"
          :class="due === option ? 'border-accent bg-accent text-on-accent' : 'border-line bg-surface hover:bg-subtle'"
          @click="setQuery({ due: option })"
        >
          {{ option ? t(`due.bucket.${option}`) : t('myTasks.all') }}
        </button>
      </div>

      <div class="flex flex-wrap items-end gap-3">
        <UiSelect v-model="priorityModel" :label="t('card.priority')" :options="priorityOptions" size="sm" class="min-w-36" />
        <UiSelect v-if="workspaces.items.length > 1" v-model="workspaceModel" :label="t('nav.workspaces')" :options="workspaceOptions" size="sm" class="min-w-44" />
        <label class="flex min-h-9 cursor-pointer items-center gap-2 text-sm">
          <input type="checkbox" class="size-5 rounded accent-accent" :checked="includeCompleted" @change="setQuery({ done: includeCompleted ? null : '1' })">
          {{ t('myTasks.showCompleted') }}
        </label>
      </div>
    </div>

    <div v-if="tasks.loading.value && !tasks.loaded.value" class="flex justify-center py-16 text-muted"><UiSpinner :size="28" :label="t('common.loading')" /></div>
    <UiAlert v-else-if="tasks.error.value && !tasks.loaded.value">
      {{ problem.message(tasks.error.value) }}
      <button type="button" class="ms-2 font-semibold underline" @click="tasks.load()">{{ t('common.retry') }}</button>
    </UiAlert>
    <UiEmptyState
      v-else-if="tasks.items.value.length === 0"
      icon="tasks"
      :title="due || priority || workspaceId ? t('myTasks.noneFiltered') : t('myTasks.none')"
      :body="due || priority || workspaceId ? undefined : t('myTasks.noneBody')"
    />

    <ul v-else class="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface" :class="{ 'opacity-60': tasks.loading.value }">
      <li v-for="task in tasks.items.value" :key="task.card.id" class="flex items-start gap-2 px-2 py-2 sm:px-3">
        <button
          type="button"
          class="flex size-11 shrink-0 items-center justify-center rounded-full transition-colors"
          :class="task.card.completedAt ? 'text-success' : 'text-muted hover:text-ink'"
          :aria-pressed="task.card.completedAt !== null"
          :aria-label="task.card.completedAt ? t('card.reopen') : t('card.complete')"
          :disabled="toggling === task.card.id"
          @click="toggle(task)"
        >
          <UiSpinner v-if="toggling === task.card.id" :size="18" />
          <UiIcon v-else :name="task.card.completedAt ? 'checkCircle' : 'circle'" :size="22" />
        </button>
        <NuxtLink :to="`/b/${task.boardId}/c/${task.card.id}`" class="min-w-0 flex-1 rounded-lg py-1.5">
          <span class="block wrap-break-word text-sm font-medium" :class="{ 'text-muted line-through': task.card.completedAt }" dir="auto">{{ task.card.title }}</span>
          <span class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
            <span class="inline-flex min-w-0 items-center gap-1.5">
              <span class="size-2.5 shrink-0 rounded-full" :class="paletteFill(task.boardColor)" aria-hidden="true" />
              <span class="truncate" dir="auto">{{ task.boardTitle }}</span>
            </span>
            <UiIcon name="chevronForward" :size="12" />
            <span class="truncate" dir="auto">{{ task.listTitle }}</span>
            <span v-if="workspaces.items.length > 1" class="truncate">· {{ task.workspaceName }}</span>
          </span>
        </NuxtLink>
        <span class="flex shrink-0 flex-col items-end gap-1 py-1.5 text-xs">
          <span v-if="task.card.dueAt" class="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 font-medium" :class="dueTone[dueState(task.card.dueAt, task.card.completedAt)!]">
            <UiIcon name="clock" :size="13" />
            {{ format.dateShort(task.card.dueAt) }}
          </span>
          <span v-if="task.card.priority !== 'none'" class="rounded-md px-1.5 py-0.5 font-medium" :class="priorityTone[task.card.priority]">{{ t(`priority.${task.card.priority}`) }}</span>
        </span>
      </li>
    </ul>

    <div v-if="tasks.hasMore.value" class="mt-4 flex justify-center">
      <UiButton variant="secondary" :loading="tasks.loadingMore.value" @click="tasks.more()">{{ t('common.loadMore') }}</UiButton>
    </div>
  </div>
</template>
