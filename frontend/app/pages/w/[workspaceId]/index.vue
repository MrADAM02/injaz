<script setup lang="ts">
import type { BoardSummary } from '~/types/api'

const { t } = useI18n()
const api = useApi()
const problem = useProblem()
const { id, workspace, allows } = useCurrentWorkspace()
usePageTitle(() => workspace.value?.name)

const boards = ref<BoardSummary[]>([])
const loading = ref(true)
const error = ref<unknown>(null)
const showArchived = ref(false)
const createOpen = ref(false)

const visible = computed(() => boards.value.filter((b) => showArchived.value || !b.isArchived))
const hasArchived = computed(() => boards.value.some((b) => b.isArchived))

async function load() {
  loading.value = true
  error.value = null
  try {
    boards.value = await api.get<BoardSummary[]>(`/api/workspaces/${id.value}/boards`, { includeArchived: true })
  } catch (e) {
    error.value = e
  } finally {
    loading.value = false
  }
}

watch(id, load, { immediate: true })

function onCreated(board: BoardSummary) {
  boards.value = [...boards.value, board]
}
</script>

<template>
  <div>
    <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
      <label v-if="hasArchived" class="inline-flex min-h-11 cursor-pointer items-center gap-2 text-sm text-muted">
        <input v-model="showArchived" type="checkbox" class="size-4 accent-accent">
        {{ t('boards.showArchived') }}
      </label>
      <span v-else />
      <UiButton v-if="allows('editContent') && visible.length > 0" icon="plus" @click="createOpen = true">{{ t('boards.new') }}</UiButton>
    </div>

    <div v-if="loading" class="flex justify-center py-16 text-muted">
      <UiSpinner :size="28" :label="t('common.loading')" />
    </div>

    <UiEmptyState v-else-if="error" icon="alert" :title="problem.message(error)">
      <UiButton variant="secondary" @click="load">{{ t('common.retry') }}</UiButton>
    </UiEmptyState>

    <UiEmptyState
      v-else-if="visible.length === 0"
      icon="board"
      :title="t('boards.empty.title')"
      :body="allows('editContent') ? t('boards.empty.body') : t('boards.empty.viewer')"
    >
      <UiButton v-if="allows('editContent')" icon="plus" @click="createOpen = true">{{ t('boards.create') }}</UiButton>
    </UiEmptyState>

    <ul v-else class="grid grid-cols-1 gap-3 min-[420px]:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      <li v-for="board in visible" :key="board.id">
        <WorkspaceBoardTile :board="board" />
      </li>
    </ul>

    <WorkspaceCreateBoardDialog v-model:open="createOpen" :workspace-id="id" @created="onCreated" />
  </div>
</template>
