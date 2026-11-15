<script setup lang="ts">
import type { ChecklistItem } from '~/types/api'
import { compareRanked, neighbours } from '~/utils/ordering'
import { limits } from '~/utils/validate'

/** The card's checklist: tick, rename, delete, reorder (drag the handle) and add items. */
const props = defineProps<{ cardId: string; items: readonly ChecklistItem[]; editable: boolean }>()
const emit = defineEmits<{ change: [items: ChecklistItem[]] }>()

const { t } = useI18n()
const api = useApi()
const toast = useToast()
const problem = useProblem()
const id = useId()

const sorted = computed(() => [...props.items].sort(compareRanked))
const done = computed(() => props.items.filter((item) => item.isDone).length)
const percent = computed(() => (props.items.length ? Math.round((done.value / props.items.length) * 100) : 0))

const adding = ref(false)
const draft = ref('')
const addError = ref<string | null>(null)
const pendingAdd = ref(false)
const addField = useTemplateRef<HTMLInputElement>('addField')

function replace(item: ChecklistItem) {
  emit('change', props.items.map((existing) => (existing.id === item.id ? item : existing)))
}

async function toggle(item: ChecklistItem) {
  replace({ ...item, isDone: !item.isDone }) // optimistic
  try {
    replace(await api.patch<ChecklistItem>(`/api/checklist-items/${item.id}`, { isDone: !item.isDone }))
  } catch (e) {
    replace(item)
    toast.error(e)
  }
}

async function rename(item: ChecklistItem, text: string) {
  try {
    replace(await api.patch<ChecklistItem>(`/api/checklist-items/${item.id}`, { text }))
  } catch (e) {
    toast.error(e)
  }
}

async function remove(item: ChecklistItem) {
  try {
    await api.delete(`/api/checklist-items/${item.id}`)
    emit('change', props.items.filter((existing) => existing.id !== item.id))
  } catch (e) {
    toast.error(e)
  }
}

async function startAdding() {
  adding.value = true
  await nextTick()
  addField.value?.focus()
}

async function add() {
  const text = draft.value.trim()
  if (!text) {
    adding.value = false
    return
  }
  if (pendingAdd.value) return
  pendingAdd.value = true
  addError.value = null
  try {
    const item = await api.post<ChecklistItem>(`/api/cards/${props.cardId}/checklist`, { text })
    emit('change', [...props.items, item])
    draft.value = ''
    await nextTick()
    addField.value?.focus()
  } catch (e) {
    addError.value = problem.fields(e).text ?? problem.message(e)
  } finally {
    pendingAdd.value = false
  }
}

const list = useTemplateRef<HTMLElement>('list')
useSortable(list, {
  group: `checklist-${props.cardId}`,
  handle: '.checklist-handle',
  disabled: () => !props.editable,
  onDrop: async (drop) => {
    const before = props.items
    const order = sorted.value.filter((item) => item.id !== drop.id)
    const moved = sorted.value.find((item) => item.id === drop.id)
    if (!moved) return
    order.splice(drop.newIndex, 0, moved)
    try {
      const updated = await api.post<ChecklistItem>(`/api/checklist-items/${drop.id}/move`, neighbours(order, drop.newIndex))
      replace(updated)
    } catch (e) {
      emit('change', [...before])
      toast.error(e)
    }
  },
})
</script>

<template>
  <section :aria-labelledby="`${id}-title`">
    <div class="mb-2 flex items-center gap-2">
      <UiIcon name="checklist" :size="18" class="text-muted" />
      <h3 :id="`${id}-title`" class="flex-1 text-sm font-semibold">{{ t('card.checklist') }}</h3>
      <span v-if="props.items.length" class="text-xs text-muted" dir="ltr">{{ done }}/{{ props.items.length }}</span>
    </div>

    <div v-if="props.items.length" class="mb-3 h-1.5 overflow-hidden rounded-full bg-subtle" role="progressbar" :aria-valuenow="percent" aria-valuemin="0" aria-valuemax="100" :aria-label="t('card.checklistProgress', { percent })">
      <div class="h-full rounded-full transition-[width]" :class="percent === 100 ? 'bg-success' : 'bg-accent'" :style="{ width: `${percent}%` }" />
    </div>

    <ul ref="list" class="flex flex-col" data-sortable-container="checklist">
      <li v-for="item in sorted" :key="item.id" :data-sortable-id="item.id" class="group flex items-start gap-1 rounded-lg hover:bg-subtle/60">
        <span v-if="props.editable" class="checklist-handle no-callout flex min-h-10 cursor-grab touch-none items-center px-1 text-muted opacity-60 group-hover:opacity-100" aria-hidden="true">
          <UiIcon name="grip" :size="14" />
        </span>
        <label class="flex min-h-10 shrink-0 cursor-pointer items-center px-1">
          <input
            type="checkbox"
            class="size-5 cursor-pointer rounded accent-(--color-accent)"
            :checked="item.isDone"
            :disabled="!props.editable"
            :aria-label="item.text"
            @change="toggle(item)"
          >
        </label>
        <div class="min-w-0 flex-1 py-1" :class="{ 'text-muted line-through': item.isDone }">
          <UiInlineEdit
            :value="item.text"
            :label="t('card.checklistItem')"
            :editable="props.editable"
            :maxlength="limits.checklistText"
            multiline
            text-class="text-sm"
            field-class=""
            @save="(text) => rename(item, text)"
          />
        </div>
        <UiButton v-if="props.editable" variant="ghost" size="sm" icon="trash" :label="t('card.deleteItem', { text: item.text })" class="opacity-60 group-hover:opacity-100" @click="remove(item)" />
      </li>
    </ul>

    <form v-if="adding" class="mt-2 flex flex-col gap-2" novalidate @submit.prevent="add" @keydown.esc.stop.prevent="adding = false">
      <label :for="`${id}-add`" class="sr-only">{{ t('card.checklistItem') }}</label>
      <input
        :id="`${id}-add`"
        ref="addField"
        v-model="draft"
        :maxlength="limits.checklistText"
        :placeholder="t('card.checklistPlaceholder')"
        autocomplete="off"
        dir="auto"
        class="block min-h-11 w-full rounded-xl border border-line bg-surface px-3 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
      >
      <p v-if="addError" class="text-xs font-medium text-danger">{{ addError }}</p>
      <div class="flex gap-2">
        <UiButton type="submit" size="sm" :loading="pendingAdd">{{ t('common.add') }}</UiButton>
        <UiButton variant="ghost" size="sm" @click="adding = false">{{ t('common.cancel') }}</UiButton>
      </div>
    </form>
    <UiButton v-else-if="props.editable" variant="secondary" size="sm" icon="plus" class="mt-2" @click="startAdding">{{ t('card.addItem') }}</UiButton>
  </section>
</template>
