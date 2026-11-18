<script setup lang="ts">
import type { ArchivedList } from '~/types/api'

/** The board's archived lists: restore one (it returns to its old place, with its cards) or delete it for good. */
const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const api = useApi()
const board = useBoardStore()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()

const items = ref<ArchivedList[]>([])
const loading = ref(false)
const error = ref<unknown>(null)
const busy = ref<string | null>(null)

const canEdit = computed(() => board.allows('editContent'))
const canDelete = computed(() => board.allows('deleteContent'))

async function load() {
  loading.value = true
  error.value = null
  try {
    items.value = await api.get<ArchivedList[]>(`/api/boards/${board.id}/lists/archived`)
  } catch (e) {
    error.value = e
  } finally {
    loading.value = false
  }
}

watch(open, (value) => {
  if (value) void load()
})

async function restore(list: ArchivedList) {
  busy.value = list.id
  try {
    await board.restoreList(list.id)
    items.value = items.value.filter((l) => l.id !== list.id)
    toast.success(t('list.restored'))
  } catch (e) {
    toast.error(e)
  } finally {
    busy.value = null
  }
}

async function remove(list: ArchivedList) {
  const ok = await confirm({
    title: t('list.deleteConfirm', { title: list.title }),
    body: t('list.deleteBody', list.cardCount),
    confirmLabel: t('common.delete'),
    danger: true,
  })
  if (!ok) return
  busy.value = list.id
  try {
    await api.delete(`/api/lists/${list.id}`)
    items.value = items.value.filter((l) => l.id !== list.id)
    toast.success(t('list.deleted'))
  } catch (e) {
    toast.error(e)
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('board.archivedLists')" :description="canEdit ? t('list.restoreHint') : undefined">
    <div v-if="loading" class="flex justify-center py-8 text-muted"><UiSpinner :label="t('common.loading')" /></div>
    <UiAlert v-else-if="error">{{ problem.message(error) }}</UiAlert>
    <p v-else-if="items.length === 0" class="py-6 text-center text-sm text-muted">{{ t('list.noneArchived') }}</p>

    <ul v-else class="-mx-5 divide-y divide-line">
      <li v-for="list in items" :key="list.id" class="flex flex-wrap items-center gap-x-3 gap-y-2 px-5 py-3">
        <div class="min-w-0 flex-1">
          <p class="truncate text-match-parent text-sm font-medium" dir="auto">{{ list.title }}</p>
          <p class="text-xs text-muted">{{ t('list.cardCount', list.cardCount) }}</p>
        </div>
        <div v-if="canEdit || canDelete" class="flex items-center gap-1">
          <UiButton v-if="canEdit" variant="secondary" size="sm" icon="restore" :loading="busy === list.id" :disabled="busy !== null && busy !== list.id" @click="restore(list)">
            {{ t('list.restore') }}
          </UiButton>
          <UiButton
            v-if="canDelete"
            variant="ghost"
            size="sm"
            icon="trash"
            class="text-danger"
            :label="t('list.deleteNamed', { title: list.title })"
            :disabled="busy !== null"
            @click="remove(list)"
          />
        </div>
      </li>
    </ul>
  </UiModal>
</template>
