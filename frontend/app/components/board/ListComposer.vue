<script setup lang="ts">
import { limits } from '~/utils/validate'

/** The "Add a list" column at the end of the board. Stays open after adding, for the next list. */
const { t } = useI18n()
const board = useBoardStore()
const toast = useToast()
const problem = useProblem()

const open = ref(false)
const title = ref('')
const pending = ref(false)
const error = ref<string | null>(null)
const field = useTemplateRef<HTMLInputElement>('field')

async function start() {
  open.value = true
  await nextTick()
  field.value?.focus()
  field.value?.scrollIntoView({ inline: 'nearest', block: 'nearest' })
}

async function submit() {
  const value = title.value.trim()
  if (!value) {
    open.value = false
    return
  }
  if (pending.value) return
  pending.value = true
  error.value = null
  try {
    await board.createList(value)
    title.value = ''
    await nextTick()
    field.value?.focus()
    field.value?.scrollIntoView({ inline: 'nearest', block: 'nearest' })
  } catch (e) {
    const fields = problem.fields(e)
    if (fields.title) error.value = fields.title
    else toast.error(e)
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <div class="w-[85vw] max-w-80 shrink-0 snap-center sm:w-72">
    <form v-if="open" class="flex flex-col gap-2 rounded-2xl bg-subtle/80 p-2" novalidate @submit.prevent="submit" @keydown.esc.stop="open = false">
      <label for="new-list-title" class="sr-only">{{ t('list.title') }}</label>
      <input
        id="new-list-title"
        ref="field"
        v-model="title"
        :maxlength="limits.listTitle"
        :placeholder="t('list.titlePlaceholder')"
        :aria-invalid="error ? 'true' : undefined"
        autocomplete="off"
        dir="auto"
        class="block min-h-11 w-full rounded-xl border border-line bg-surface px-3 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
      >
      <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
      <div class="flex items-center gap-2">
        <UiButton type="submit" size="sm" :loading="pending">{{ t('list.add') }}</UiButton>
        <UiButton variant="ghost" size="sm" icon="close" :label="t('common.cancel')" @click="open = false" />
      </div>
    </form>
    <button
      v-else
      type="button"
      class="flex min-h-12 w-full items-center gap-2 rounded-2xl border-2 border-dashed border-line px-4 text-sm font-medium text-muted transition-colors hover:border-accent/50 hover:text-ink"
      @click="start"
    >
      <UiIcon name="plus" :size="18" />
      {{ t('list.add') }}
    </button>
  </div>
</template>
