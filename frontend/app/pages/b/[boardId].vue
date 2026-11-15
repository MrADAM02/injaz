<script setup lang="ts">
import type { MenuEntry } from '~/types/ui'
import type { RouteLocationRaw } from 'vue-router'
import type { SortableDrop } from '~/composables/useSortable'
import { matchesFilters } from '~/utils/cards'
import { paletteFill } from '~/utils/palette'
import { limits } from '~/utils/validate'

/**
 * The board: columns scroll sideways (one column per swipe on phones), cards drag between them (long-press on
 * touch), and other people's changes arrive live. The card dialog is the nested route `c/[cardId]`.
 */
definePageMeta({ fullBleed: true })

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const board = useBoardStore()
const realtime = useRealtimeStore()
const workspaces = useWorkspacesStore()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()
const { filters, active: filtering, count: filterCount, clear: clearFilters } = useBoardFilters()

const boardId = computed(() => String(route.params.boardId))
const summary = computed(() => board.board)
usePageTitle(() => summary.value?.title)

const canEdit = computed(() => board.allows('editContent'))
const canDelete = computed(() => board.allows('deleteContent'))
const loading = computed(() => !summary.value && !board.error)

// ---------- loading & live updates ----------

let unsubscribe: (() => void) | null = null

async function open(id: string) {
  unsubscribe?.()
  unsubscribe = null
  try {
    await board.load(id)
  } catch {
    return // board.error is shown
  }
  if (board.id !== id) return
  const stop = await realtime.subscribe(id, {
    onEvent: (event) => board.receive(event),
    onResync: () => void board.reload(),
    onAccessRevoked: (workspaceId) => {
      if (workspaceId === board.board?.workspaceId) board.closed = 'revoked'
    },
    onJoinFailed: () => void board.reload(),
  })
  if (board.id === id) unsubscribe = stop
  else stop()
}

watch(boardId, (id) => void open(id), { immediate: true })
onBeforeUnmount(() => {
  unsubscribe?.()
  board.reset()
})

watch(
  () => board.closed,
  (reason) => {
    if (!reason) return
    const workspaceId = board.board?.workspaceId
    if (reason === 'revoked' && workspaceId) workspaces.drop(workspaceId)
    toast.info(t(`board.closed.${reason}`))
    void router.replace(reason === 'revoked' || !workspaceId ? '/' : `/w/${workspaceId}`)
  },
)

// ---------- filtering ----------

const visibleCards = computed(() => {
  const now = new Date()
  return new Map(board.lists.map((list) => [list.id, filtering.value ? list.cards.filter((card) => matchesFilters(card, filters.value, locale.value, now)) : list.cards]))
})
const matchCount = computed(() => [...visibleCards.value.values()].reduce((sum, cards) => sum + cards.length, 0))

function cardLink(cardId: string): RouteLocationRaw {
  return { path: `/b/${boardId.value}/c/${cardId}`, query: route.query }
}

// ---------- drag & drop ----------

/** Cards drag only when everything is visible: with filters on, the neighbours of a drop would be guesses. */
const cardsDraggable = computed(() => canEdit.value && !filtering.value)
const dragging = ref(false)
let releaseDrag: (() => void) | null = null

function onDragStart() {
  dragging.value = true
  releaseDrag = board.hold()
}

function onDragEnd() {
  dragging.value = false
  releaseDrag?.()
  releaseDrag = null
}

async function onCardDrop(drop: SortableDrop) {
  try {
    await board.moveCard(drop.id, drop.toContainer, drop.newIndex)
  } catch (e) {
    toast.error(e)
  }
}

async function moveList(listId: string, toIndex: number) {
  try {
    await board.moveList(listId, toIndex)
  } catch (e) {
    toast.error(e)
  }
}

const columns = useTemplateRef<HTMLElement>('columns')
useSortable(columns, {
  group: 'lists',
  handle: '.list-handle',
  disabled: () => !canEdit.value,
  onStart: onDragStart,
  onEnd: onDragEnd,
  onDrop: (drop) => void moveList(drop.id, drop.newIndex),
})

// ---------- phones: which column is on screen ----------

const scroller = useTemplateRef<HTMLElement>('scroller')
const activeColumn = ref(0)
let frame = 0

function onScroll() {
  cancelAnimationFrame(frame)
  frame = requestAnimationFrame(() => {
    const el = scroller.value
    const items = columns.value?.children
    if (!el || !items) return
    const center = el.getBoundingClientRect().x + el.clientWidth / 2
    let best = 0
    let distance = Infinity
    Array.from(items).forEach((item, index) => {
      const rect = item.getBoundingClientRect()
      const d = Math.abs(rect.x + rect.width / 2 - center)
      if (d < distance) {
        distance = d
        best = index
      }
    })
    activeColumn.value = best
  })
}

function showColumn(index: number) {
  const item = columns.value?.children[index] as HTMLElement | undefined
  item?.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' })
}

// ---------- board actions ----------

const filterOpen = ref(false)
const searchOpen = ref(false)
const searchArchived = ref(false)
const activityOpen = ref(false)
const labelsOpen = ref(false)
const archivedListsOpen = ref(false)
const editOpen = ref(false)

function openSearch(archived: boolean) {
  filterOpen.value = false
  searchArchived.value = archived
  searchOpen.value = true
}

async function rename(title: string) {
  try {
    await board.updateBoard({ title })
  } catch (e) {
    toast.error(e)
  }
}

async function setArchived(isArchived: boolean) {
  if (isArchived) {
    const ok = await confirm({ title: t('board.archiveConfirm', { title: summary.value?.title ?? '' }), body: t('board.archiveBody'), confirmLabel: t('board.archive') })
    if (!ok) return
  }
  try {
    const updated = await board.updateBoard({ isArchived })
    if (updated.isArchived) {
      toast.success(t('board.archived'))
      void router.push(`/w/${updated.workspaceId}`)
    } else {
      toast.success(t('board.restored'))
    }
  } catch (e) {
    toast.error(e)
  }
}

async function remove() {
  const current = summary.value
  if (!current) return
  const ok = await confirm({
    title: t('board.deleteConfirm', { title: current.title }),
    body: t('board.deleteBody'),
    confirmLabel: t('board.delete'),
    danger: true,
    requireText: current.title,
    requireTextLabel: t('board.typeTitle'),
  })
  if (!ok) return
  try {
    await board.deleteBoard()
    toast.success(t('board.deleted'))
    void router.push(`/w/${current.workspaceId}`)
  } catch (e) {
    toast.error(e)
  }
}

const menu = computed<MenuEntry[]>(() => [
  { label: t('board.activity'), icon: 'activity' as const, action: () => (activityOpen.value = true) },
  { label: t('board.archivedCards'), icon: 'archive' as const, action: () => openSearch(true) },
  { label: t('board.archivedLists'), icon: 'archive' as const, action: () => (archivedListsOpen.value = true) },
  { label: t('labels.title'), icon: 'tag' as const, action: () => (labelsOpen.value = true) },
  canEdit.value && { label: t('board.edit'), icon: 'edit' as const, action: () => (editOpen.value = true) },
  canEdit.value && !summary.value?.isArchived && { label: t('board.archive'), icon: 'archive' as const, action: () => setArchived(true) },
  canDelete.value && { label: t('board.delete'), icon: 'trash' as const, danger: true, action: remove },
])
</script>

<template>
  <div class="flex h-full min-h-0 flex-col">
    <UiEmptyState v-if="board.error && !summary" icon="alert" :title="problem.message(board.error)" class="px-4">
      <UiButton variant="secondary" @click="open(boardId)">{{ t('common.retry') }}</UiButton>
      <UiButton to="/" variant="ghost">{{ t('invite.goHome') }}</UiButton>
    </UiEmptyState>

    <div v-else-if="loading" class="flex flex-1 items-center justify-center text-muted">
      <UiSpinner :size="28" :label="t('common.loading')" />
    </div>

    <template v-else-if="summary">
      <header class="flex shrink-0 items-center gap-1 px-2 py-2 sm:gap-2 sm:px-4 md:px-6 md:pt-5">
        <UiButton :to="`/w/${summary.workspaceId}`" variant="ghost" size="sm" icon="arrowBack" :label="t('board.backToWorkspace')" :title="t('board.backToWorkspace')" />
        <span class="hidden size-3 shrink-0 rounded-full sm:block" :class="paletteFill(summary.color)" aria-hidden="true" />
        <h1 class="hidden min-w-0 flex-1 sm:block">
          <UiInlineEdit
            :value="summary.title"
            :label="t('boards.title')"
            :editable="canEdit"
            :maxlength="limits.boardTitle"
            text-class="text-xl font-bold truncate"
            field-class="text-lg font-bold"
            @save="rename"
          />
        </h1>
        <span class="flex-1 sm:hidden" />

        <UiButton
          :variant="filtering ? 'primary' : 'secondary'"
          size="sm"
          icon="filter"
          :aria-label="filtering ? t('filters.activeCount', filterCount) : t('filters.title')"
          @click="filterOpen = true"
        >
          <span class="hidden sm:inline">{{ t('filters.title') }}</span>
          <span v-if="filtering" class="rounded-full bg-on-accent/20 px-1.5 text-xs">{{ filterCount }}</span>
        </UiButton>
        <UiButton variant="ghost" size="sm" icon="search" :label="t('search.title')" :title="t('search.title')" @click="openSearch(false)" />
        <UiMenu :items="menu" :label="t('board.actions')" :title="summary.title" />
      </header>

      <div v-if="realtime.status === 'reconnecting'" class="mx-2 mb-2 flex items-center gap-2 rounded-xl bg-amber-100 px-3 py-2 text-sm text-amber-900 sm:mx-4 md:mx-6 dark:bg-amber-950 dark:text-amber-200" role="status">
        <UiIcon name="wifiOff" :size="18" />
        {{ t('board.reconnecting') }}
      </div>

      <div v-if="summary.isArchived" class="mx-2 mb-2 flex flex-wrap items-center gap-2 rounded-xl bg-subtle px-3 py-2 text-sm sm:mx-4 md:mx-6" role="status">
        <UiIcon name="archive" :size="18" />
        <span class="flex-1">{{ t('board.isArchived') }}</span>
        <UiButton v-if="canEdit" size="sm" variant="secondary" icon="restore" @click="setArchived(false)">{{ t('board.restore') }}</UiButton>
      </div>

      <div v-if="filtering" class="mx-2 mb-2 flex flex-wrap items-center gap-2 rounded-xl bg-accent-soft px-3 py-2 text-sm text-accent-strong sm:mx-4 md:mx-6" role="status">
        <UiIcon name="filter" :size="16" />
        <span class="flex-1">{{ t('filters.showing', matchCount) }}<template v-if="canEdit"> · {{ t('filters.dragPaused') }}</template></span>
        <button type="button" class="min-h-9 font-semibold underline-offset-2 hover:underline" @click="clearFilters">{{ t('filters.clear') }}</button>
      </div>

      <!-- `relative` matters: without it, absolutely positioned descendants (sr-only text) escape the
           overflow clip, widen the page on phones and make the browser zoom out. -->
      <div
        ref="scroller"
        class="board-scroller relative min-h-0 flex-1 snap-x snap-mandatory overflow-x-auto overflow-y-hidden overscroll-x-contain scroll-px-4 sm:snap-none"
        :class="{ 'is-dragging': dragging }"
        @scroll.passive="onScroll"
      >
        <div class="flex h-full items-start gap-3 px-4 pb-3 md:px-6">
          <div ref="columns" class="flex h-full items-start gap-3">
            <BoardColumn
              v-for="(list, index) in board.lists"
              :key="list.id"
              :list="list"
              :cards="visibleCards.get(list.id) ?? []"
              :index="index"
              :count="board.lists.length"
              :cards-draggable="cardsDraggable"
              :filtered="filtering"
              :card-link="cardLink"
              @drag-start="onDragStart"
              @drag-end="onDragEnd"
              @drop="onCardDrop"
              @move-list="(to) => moveList(list.id, to)"
            />
          </div>
          <BoardListComposer v-if="canEdit" />
          <UiEmptyState v-else-if="board.lists.length === 0" icon="board" :title="t('board.emptyViewer')" class="w-full" />
          <span class="w-1 shrink-0" aria-hidden="true" />
        </div>
      </div>

      <nav v-if="board.lists.length > 1" class="flex shrink-0 justify-center gap-1.5 pb-2 sm:hidden" :aria-label="t('board.columns')">
        <button
          v-for="(list, index) in board.lists"
          :key="list.id"
          type="button"
          class="flex size-6 items-center justify-center"
          :aria-label="list.title"
          :aria-current="index === activeColumn ? 'true' : undefined"
          @click="showColumn(index)"
        >
          <span class="block h-2 rounded-full transition-all" :class="index === activeColumn ? 'w-4 bg-accent' : 'w-2 bg-line'" />
        </button>
      </nav>

      <BoardFilterDialog v-model:open="filterOpen" @search-archived="openSearch(false)" />
      <BoardSearchDialog v-model:open="searchOpen" :card-link="cardLink" :initial-archived="searchArchived" />
      <UiModal v-model:open="activityOpen" :title="t('board.activity')" size="lg">
        <BoardActivityFeed :source="{ boardId }" />
      </UiModal>
      <BoardLabelsDialog v-model:open="labelsOpen" />
      <BoardArchivedListsDialog v-model:open="archivedListsOpen" />
      <BoardEditDialog v-model:open="editOpen" />

      <NuxtPage />
    </template>
  </div>
</template>
