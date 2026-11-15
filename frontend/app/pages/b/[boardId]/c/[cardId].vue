<script setup lang="ts">
import type { MenuEntry } from '~/types/ui'
import type { Card, CardPriority } from '~/types/api'
import { isApiError } from '~/utils/api-client'
import { dueState, dueTone, priorities, priorityTone } from '~/utils/cards'
import { limits } from '~/utils/validate'

/**
 * The card dialog, a child route of the board (`/b/:boardId/c/:cardId`): deep-linkable, and the back
 * gesture closes it. Full screen on phones, a wide dialog from `sm` up.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const api = useApi()
const board = useBoardStore()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()
const format = useFormat()

const boardId = computed(() => String(route.params.boardId))
const cardId = computed(() => String(route.params.cardId))

const card = ref<Card | null>(null)
const error = ref<unknown>(null)
/** Bumped after our own changes so the activity tab refreshes too (remote ones come via the board store). */
const localRevision = ref(0)
const remoteRevision = computed(() => board.cardRevisions[cardId.value] ?? 0)
const revision = computed(() => localRevision.value + remoteRevision.value)

const canEdit = computed(() => board.allows('editContent'))
const canDelete = computed(() => board.allows('deleteContent'))
const editable = computed(() => canEdit.value && !card.value?.isArchived)

usePageTitle(() => card.value?.title ?? board.board?.title)

async function load() {
  error.value = null
  try {
    const loaded = await api.get<Card>(`/api/cards/${cardId.value}`)
    if (loaded.id !== cardId.value) return
    if (loaded.boardId !== boardId.value) {
      // A link with the wrong board: go to the card's real board.
      void router.replace({ path: `/b/${loaded.boardId}/c/${loaded.id}` })
      return
    }
    card.value = loaded
  } catch (e) {
    error.value = e
  }
}

watch(cardId, () => {
  card.value = null
  void load()
}, { immediate: true })

// Someone else changed (or removed) this card: re-read it.
watch(remoteRevision, () => void load())

// ---------- closing ----------

const open = computed({
  get: () => true,
  set: (value) => {
    if (!value) close()
  },
})

function close() {
  const back = (window.history.state as { back?: unknown } | null)?.back
  // Opened from the board: going back keeps history tidy (and matches the back gesture). Deep link: replace.
  if (typeof back === 'string' && back.startsWith(`/b/${boardId.value}`) && !back.includes('/c/')) router.back()
  else void router.replace({ path: `/b/${boardId.value}`, query: route.query })
}

// ---------- updates ----------

function applied(next: Card) {
  card.value = next
  board.syncCard(next)
  localRevision.value++
}

/** PATCH with merge semantics. Returns false (after a toast) on failure; a concurrent edit reloads the card. */
async function patch(body: Partial<Pick<Card, 'title' | 'description' | 'dueAt' | 'priority' | 'isArchived'>> & { isCompleted?: boolean }): Promise<boolean> {
  try {
    applied(await api.patch<Card>(`/api/cards/${cardId.value}`, body))
    return true
  } catch (e) {
    toast.error(e)
    if (isApiError(e, 'conflict.concurrency')) void load()
    return false
  }
}

const priorityOptions = computed(() => priorities.map((p) => ({ value: p, label: t(`priority.${p}`) })))
const priority = computed({
  get: () => card.value?.priority ?? 'none',
  set: (value: CardPriority) => void patch({ priority: value }),
})

const completed = computed(() => card.value?.completedAt != null)
const due = computed(() => (card.value ? dueState(card.value.dueAt, card.value.completedAt) : null))
const listTitle = computed(() => board.lists.find((l) => l.id === card.value?.listId)?.title)

const cardLabels = computed(() => (card.value?.labelIds ?? []).map((id) => board.labelById.get(id)).filter((l) => l !== undefined))
const assignees = computed(() => (card.value?.assigneeIds ?? []).map((id) => board.memberById.get(id)).filter((m) => m !== undefined))

const picker = ref<'members' | 'labels' | null>(null)
const pickerOpen = computed({
  get: () => picker.value !== null,
  set: (value) => {
    if (!value) picker.value = null
  },
})
const pickerKind = ref<'members' | 'labels'>('members')
const pending = ref<string | null>(null)

function openPicker(kind: 'members' | 'labels') {
  pickerKind.value = kind
  picker.value = kind
}

async function toggle(id: string, on: boolean) {
  const current = card.value
  if (!current) return
  const kind = pickerKind.value
  const path = kind === 'members' ? `/api/cards/${current.id}/assignees/${id}` : `/api/cards/${current.id}/labels/${id}`
  pending.value = id
  try {
    if (on) await api.put(path)
    else await api.delete(path)
    const key = kind === 'members' ? 'assigneeIds' : 'labelIds'
    const ids = on ? [...new Set([...current[key], id])] : current[key].filter((x) => x !== id)
    applied({ ...current, [key]: ids })
  } catch (e) {
    toast.error(e)
  } finally {
    pending.value = null
  }
}

const labelsOpen = ref(false)
function manageLabels() {
  picker.value = null
  labelsOpen.value = true
}

const dueOpen = ref(false)
const moveOpen = ref(false)
const tab = ref<'comments' | 'activity'>('comments')

function onChecklist(items: Card['checklist']) {
  if (!card.value) return
  applied({ ...card.value, checklist: items })
}

function onCommentCount(delta: number) {
  if (!card.value) return
  const next = { ...card.value, commentCount: Math.max(0, card.value.commentCount + delta) }
  card.value = next
  board.patchCard(next.id, () => ({ commentCount: next.commentCount }))
  localRevision.value++
}

async function onMoved(listId: string) {
  if (!card.value) return
  const tile = board.findCard(card.value.id)?.card
  card.value = { ...card.value, listId, rank: tile?.rank ?? card.value.rank }
  localRevision.value++
}

async function setArchived(isArchived: boolean) {
  if (await patch({ isArchived })) toast.success(t(isArchived ? 'card.archived' : 'card.restored'))
}

async function remove() {
  const current = card.value
  if (!current) return
  const ok = await confirm({ title: t('card.deleteConfirm', { title: current.title }), body: t('card.deleteBody'), confirmLabel: t('common.delete'), danger: true })
  if (!ok) return
  try {
    await api.delete(`/api/cards/${current.id}`)
    board.dropCard(current.id)
    toast.success(t('card.deleted'))
    close()
  } catch (e) {
    toast.error(e)
  }
}

const actions = computed<MenuEntry[]>(() => {
  const c = card.value
  if (!c) return []
  return [
    editable.value && { label: t('card.move'), icon: 'move' as const, action: () => (moveOpen.value = true) },
    canEdit.value && !c.isArchived && { label: t('card.archive'), icon: 'archive' as const, action: () => setArchived(true) },
    canEdit.value && c.isArchived && { label: t('card.restore'), icon: 'restore' as const, action: () => setArchived(false) },
    canDelete.value && { label: t('common.delete'), icon: 'trash' as const, danger: true, action: remove },
  ]
})
</script>

<template>
  <UiModal v-model:open="open" :title="card?.title ?? t('common.loading')" size="xl" fullscreen hide-title>
    <template #header>
      <div class="flex items-center gap-1 text-sm text-muted">
        <UiIcon name="board" :size="16" />
        <span v-if="listTitle" class="truncate">{{ t('card.inList', { list: listTitle }) }}</span>
        <span v-else-if="card?.isArchived" class="truncate">{{ t('card.archivedLabel') }}</span>
        <span class="flex-1" />
        <UiMenu v-if="actions.some(Boolean)" :items="actions" :label="t('card.actions')" :title="card?.title" />
      </div>
    </template>

    <div v-if="error && !card" class="py-8">
      <UiEmptyState icon="alert" :title="problem.message(error)">
        <UiButton variant="secondary" @click="close">{{ t('common.close') }}</UiButton>
      </UiEmptyState>
    </div>
    <div v-else-if="!card" class="flex justify-center py-16 text-muted"><UiSpinner :size="28" :label="t('common.loading')" /></div>

    <div v-else class="flex flex-col gap-6 pb-2">
      <div v-if="card.isArchived" class="flex flex-wrap items-center gap-2 rounded-xl bg-amber-100 px-3 py-2 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200" role="status">
        <UiIcon name="archive" :size="18" />
        <span class="flex-1">{{ t('card.isArchived') }}</span>
        <UiButton v-if="canEdit" size="sm" variant="secondary" icon="restore" @click="setArchived(false)">{{ t('card.restore') }}</UiButton>
      </div>

      <div class="-mx-2 -mt-2 flex items-start gap-2">
        <button
          v-if="editable"
          type="button"
          class="mt-1 flex size-9 shrink-0 items-center justify-center rounded-full transition-colors"
          :class="completed ? 'text-success hover:bg-green-100 dark:hover:bg-green-950' : 'text-muted hover:bg-subtle hover:text-ink'"
          :aria-pressed="completed"
          :aria-label="completed ? t('card.reopen') : t('card.complete')"
          :title="completed ? t('card.reopen') : t('card.complete')"
          @click="patch({ isCompleted: !completed })"
        >
          <UiIcon :name="completed ? 'checkCircle' : 'circle'" :size="24" />
        </button>
        <h2 class="min-w-0 flex-1">
          <UiInlineEdit
            :value="card.title"
            :label="t('card.titleLabel')"
            :editable="editable"
            :maxlength="limits.cardTitle"
            multiline
            text-class="text-xl font-bold leading-snug"
            field-class="text-lg font-bold leading-snug"
            :class="{ 'line-through decoration-2 text-muted': completed }"
            @save="(title) => patch({ title })"
          />
        </h2>
      </div>

      <!-- Properties: a wrapping row of chips on phones, a grid on larger screens. -->
      <dl class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <dt class="mb-1.5 text-xs font-semibold text-muted">{{ t('card.members') }}</dt>
          <dd class="flex flex-wrap items-center gap-1.5">
            <span v-for="person in assignees" :key="person.userId" class="inline-flex items-center gap-1.5 rounded-full bg-subtle py-0.5 pe-2.5 ps-0.5 text-sm">
              <UiAvatar :name="person.displayName" :src="person.avatarUrl" size="xs" />
              <span dir="auto">{{ person.displayName }}</span>
            </span>
            <UiButton v-if="editable" variant="secondary" size="sm" :icon="assignees.length ? 'edit' : 'plus'" :label="assignees.length ? t('card.editMembers') : undefined" @click="openPicker('members')">
              <template v-if="!assignees.length">{{ t('card.assign') }}</template>
            </UiButton>
            <span v-else-if="!assignees.length" class="text-sm text-muted">{{ t('card.nobody') }}</span>
          </dd>
        </div>

        <div>
          <dt class="mb-1.5 text-xs font-semibold text-muted">{{ t('card.labels') }}</dt>
          <dd class="flex flex-wrap items-center gap-1.5">
            <BoardLabelChip v-for="label in cardLabels" :key="label.id" :label="{ ...label, name: label.name || t(`colors.${label.color}`) }" class="min-h-7 text-sm" />
            <UiButton v-if="editable" variant="secondary" size="sm" :icon="cardLabels.length ? 'edit' : 'plus'" :label="cardLabels.length ? t('card.editLabels') : undefined" @click="openPicker('labels')">
              <template v-if="!cardLabels.length">{{ t('card.addLabel') }}</template>
            </UiButton>
            <span v-else-if="!cardLabels.length" class="text-sm text-muted">{{ t('card.noLabels') }}</span>
          </dd>
        </div>

        <div>
          <dt class="mb-1.5 text-xs font-semibold text-muted">{{ t('card.dueDate') }}</dt>
          <dd>
            <button
              v-if="editable"
              type="button"
              class="inline-flex min-h-9 items-center gap-2 rounded-lg px-3 text-sm font-medium"
              :class="due ? dueTone[due] : 'border border-line hover:bg-subtle'"
              @click="dueOpen = true"
            >
              <UiIcon name="calendar" :size="16" />
              {{ card.dueAt ? format.dateTime(card.dueAt) : t('card.setDue') }}
              <span v-if="due === 'overdue'" class="text-xs">· {{ t('due.state.overdue') }}</span>
            </button>
            <span v-else-if="card.dueAt" class="inline-flex min-h-9 items-center gap-2 rounded-lg px-3 text-sm font-medium" :class="due ? dueTone[due] : ''">
              <UiIcon name="calendar" :size="16" />
              {{ format.dateTime(card.dueAt) }}
            </span>
            <span v-else class="text-sm text-muted">{{ t('card.noDue') }}</span>
          </dd>
        </div>

        <div>
          <dt class="mb-1.5 text-xs font-semibold text-muted">{{ t('card.priority') }}</dt>
          <dd>
            <UiSelect v-if="editable" v-model="priority" :label="t('card.priority')" :options="priorityOptions" hide-label size="sm" class="max-w-48" />
            <span v-else-if="card.priority !== 'none'" class="inline-flex rounded-md px-2 py-0.5 text-sm font-medium" :class="priorityTone[card.priority]">{{ t(`priority.${card.priority}`) }}</span>
            <span v-else class="text-sm text-muted">{{ t('priority.none') }}</span>
          </dd>
        </div>
      </dl>

      <CardDescription :value="card.description" :editable="editable" :save="(description) => patch({ description })" />

      <CardChecklist :card-id="card.id" :items="card.checklist" :editable="editable" @change="onChecklist" />

      <section>
        <div class="mb-3 flex gap-1 border-b border-line" role="tablist">
          <button
            v-for="option in ['comments', 'activity'] as const"
            :key="option"
            type="button"
            role="tab"
            :aria-selected="tab === option"
            class="-mb-px flex min-h-11 items-center gap-2 border-b-2 px-3 text-sm font-medium"
            :class="tab === option ? 'border-accent text-accent-strong' : 'border-transparent text-muted hover:text-ink'"
            @click="tab = option"
          >
            <UiIcon :name="option === 'comments' ? 'comment' : 'activity'" :size="16" />
            {{ option === 'comments' ? t('card.comments') : t('card.activity') }}
            <span v-if="option === 'comments' && card.commentCount" class="rounded-full bg-subtle px-1.5 text-xs">{{ card.commentCount }}</span>
          </button>
        </div>
        <CardComments
          v-if="tab === 'comments'"
          :card-id="card.id"
          :can-comment="canEdit"
          :can-moderate="canDelete"
          :revision="remoteRevision"
          @count-change="onCommentCount"
        />
        <BoardActivityFeed v-else :source="{ cardId: card.id }" :revision="revision" />
      </section>
    </div>

    <CardPickers v-model:open="pickerOpen" :kind="pickerKind" :selected="pickerKind === 'members' ? (card?.assigneeIds ?? []) : (card?.labelIds ?? [])" :pending="pending" @toggle="toggle" @manage-labels="manageLabels" />
    <CardDueDialog v-if="card" v-model:open="dueOpen" :due-at="card.dueAt" :save="(dueAt) => patch({ dueAt })" />
    <CardMoveDialog v-if="card" v-model:open="moveOpen" :card-id="card.id" :list-id="card.listId" @moved="onMoved" />
    <BoardLabelsDialog v-model:open="labelsOpen" />
  </UiModal>
</template>
