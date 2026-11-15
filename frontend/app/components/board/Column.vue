<script setup lang="ts">
import type { MenuEntry } from '~/types/ui'
import type { RouteLocationRaw } from 'vue-router'
import type { CardSummary } from '~/types/api'
import type { ListState } from '~/stores/board'
import type { SortableDrop } from '~/composables/useSortable'
import { limits } from '~/utils/validate'

/** One list: header (rename, menu, drag handle for the column), its cards (sortable), and the composer. */
const props = defineProps<{
  list: ListState
  /** The cards to show (the filtered subset while filters are on). */
  cards: readonly CardSummary[]
  index: number
  count: number
  cardsDraggable: boolean
  filtered: boolean
  cardLink: (cardId: string) => RouteLocationRaw
}>()
const emit = defineEmits<{ dragStart: []; dragEnd: []; drop: [drop: SortableDrop]; moveList: [toIndex: number] }>()

const { t } = useI18n()
const board = useBoardStore()
const toast = useToast()
const confirm = useConfirm()

const canEdit = computed(() => board.allows('editContent'))
const canDelete = computed(() => board.allows('deleteContent'))
const composer = ref<'top' | 'bottom' | null>(null)
const title = useTemplateRef<{ start: () => void }>('title')

const container = useTemplateRef<HTMLElement>('container')
useSortable(container, {
  group: 'cards',
  disabled: () => !props.cardsDraggable,
  onStart: () => emit('dragStart'),
  onEnd: () => emit('dragEnd'),
  onDrop: (drop) => emit('drop', drop),
})

async function rename(value: string) {
  try {
    await board.updateList(props.list.id, { title: value })
  } catch (e) {
    toast.error(e)
  }
}

async function archive() {
  const ok = await confirm({
    title: t('list.archiveConfirm', { title: props.list.title }),
    body: t('list.archiveBody'),
    confirmLabel: t('list.archive'),
  })
  if (!ok) return
  try {
    await board.updateList(props.list.id, { isArchived: true })
    toast.success(t('list.archived'))
  } catch (e) {
    toast.error(e)
  }
}

async function remove() {
  const ok = await confirm({
    title: t('list.deleteConfirm', { title: props.list.title }),
    body: t('list.deleteBody', props.list.cards.length),
    confirmLabel: t('common.delete'),
    danger: true,
  })
  if (!ok) return
  try {
    await board.deleteList(props.list.id)
    toast.success(t('list.deleted'))
  } catch (e) {
    toast.error(e)
  }
}

const menu = computed<MenuEntry[]>(() => [
  canEdit.value && { label: t('card.addTop'), icon: 'plus' as const, action: () => (composer.value = 'top') },
  canEdit.value && { label: t('list.rename'), icon: 'edit' as const, action: () => title.value?.start() },
  canEdit.value && props.index > 0 && { label: t('list.moveEarlier'), icon: 'arrowBack' as const, action: () => emit('moveList', props.index - 1) },
  canEdit.value && props.index < props.count - 1 && { label: t('list.moveLater'), icon: 'chevronForward' as const, action: () => emit('moveList', props.index + 1) },
  canEdit.value && { label: t('list.archive'), icon: 'archive' as const, action: archive },
  canDelete.value && { label: t('list.delete'), icon: 'trash' as const, danger: true, action: remove },
])
</script>

<template>
  <section
    class="flex max-h-full w-[85vw] max-w-80 shrink-0 snap-center flex-col rounded-2xl bg-subtle/80 sm:w-72"
    :aria-label="props.list.title"
    :data-sortable-id="props.list.id"
  >
    <header class="flex shrink-0 items-start gap-1 p-2 pb-1">
      <span
        v-if="canEdit"
        class="list-handle no-callout flex min-h-9 cursor-grab touch-none items-center px-1 text-muted active:cursor-grabbing"
        :title="t('list.dragHint')"
        aria-hidden="true"
      >
        <UiIcon name="grip" :size="16" />
      </span>
      <h2 class="min-w-0 flex-1">
        <UiInlineEdit
          ref="title"
          :value="props.list.title"
          :label="t('list.title')"
          :editable="canEdit"
          :maxlength="limits.listTitle"
          text-class="text-sm font-semibold min-h-9"
          field-class="font-semibold min-h-9"
          @save="rename"
        />
      </h2>
      <span class="flex min-h-9 items-center px-1 text-xs text-muted" :title="t('list.cardCount', props.list.cards.length)">
        {{ props.filtered ? `${props.cards.length}/${props.list.cards.length}` : props.list.cards.length }}
      </span>
      <UiMenu v-if="canEdit" :items="menu" :label="t('list.actions', { title: props.list.title })" :title="props.list.title" />
    </header>

    <div v-if="composer === 'top'" class="px-2 pb-2">
      <BoardCardComposer :list-id="props.list.id" position="top" @close="composer = null" />
    </div>

    <ol
      ref="container"
      class="relative flex min-h-12 flex-1 flex-col gap-2 overflow-y-auto overscroll-y-contain px-2 pb-1"
      :data-sortable-container="props.list.id"
      :aria-label="t('list.cardsIn', { title: props.list.title })"
    >
      <li v-for="card in props.cards" :key="card.id" :data-sortable-id="card.id">
        <BoardCardTile :card="card" :to="props.cardLink(card.id)" :draggable="props.cardsDraggable" />
      </li>
    </ol>

    <footer v-if="canEdit" class="shrink-0 p-2 pt-1">
      <BoardCardComposer v-if="composer === 'bottom'" :list-id="props.list.id" position="bottom" @close="composer = null" />
      <button
        v-else
        type="button"
        class="flex min-h-11 w-full items-center gap-2 rounded-xl px-3 text-sm font-medium text-muted transition-colors hover:bg-surface hover:text-ink"
        @click="composer = 'bottom'"
      >
        <UiIcon name="plus" :size="18" />
        {{ t('card.add') }}
      </button>
    </footer>
  </section>
</template>
