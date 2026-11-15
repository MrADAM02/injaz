<script setup lang="ts">
import type { BoardSummary } from '~/types/api'

// Placeholder until phase 7b (lists, cards, drag & drop, live updates). Shows the board header so links work.
const { t } = useI18n()
const route = useRoute()
const api = useApi()
const problem = useProblem()

const board = ref<BoardSummary | null>(null)
const error = ref<unknown>(null)
usePageTitle(() => board.value?.title)

onMounted(async () => {
  try {
    board.value = await api.get<BoardSummary>(`/api/boards/${String(route.params.boardId)}`)
  } catch (e) {
    error.value = e
  }
})
</script>

<template>
  <div>
    <UiEmptyState v-if="error" icon="alert" :title="problem.message(error)">
      <UiButton to="/" variant="secondary">{{ t('invite.goHome') }}</UiButton>
    </UiEmptyState>
    <template v-else-if="board">
      <UiButton :to="`/w/${board.workspaceId}`" variant="ghost" size="sm" icon="arrowBack" class="-ms-2 mb-2">{{ t('common.back') }}</UiButton>
      <h1 class="text-2xl font-bold">{{ board.title }}</h1>
      <UiEmptyState icon="board" :title="t('boards.placeholder')" />
    </template>
    <div v-else class="flex justify-center py-16 text-muted">
      <UiSpinner :size="28" :label="t('common.loading')" />
    </div>
  </div>
</template>
