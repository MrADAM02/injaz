<script setup lang="ts">
import { ApiError } from '~/utils/api-client'
import { limits, maxLength, required, validateFields } from '~/utils/validate'

/**
 * "Add a card" inline form. Enter adds and keeps the form open for the next card (Shift+Enter: new line);
 * Esc or Cancel closes it.
 */
const props = defineProps<{ listId: string; position: 'top' | 'bottom' }>()
const emit = defineEmits<{ close: []; created: [] }>()

const { t } = useI18n()
const board = useBoardStore()
const toast = useToast()
const problem = useProblem()

const title = ref('')
const pending = ref(false)
const error = ref<string | null>(null)
const field = useTemplateRef<HTMLTextAreaElement>('field')

onMounted(() => {
  field.value?.focus()
  field.value?.scrollIntoView({ block: 'nearest' })
})

async function submit() {
  if (pending.value) return
  const local = validateFields({ title: [title.value, required, maxLength(limits.cardTitle)] })
  if (local.title) {
    error.value = problem.fields(new ApiError(400, { errors: local })).title ?? null
    if (!title.value.trim()) emit('close')
    return
  }
  pending.value = true
  error.value = null
  try {
    await board.createCard(props.listId, title.value.trim(), props.position)
    title.value = ''
    emit('created')
    await nextTick()
    field.value?.focus()
    field.value?.scrollIntoView({ block: 'nearest' })
  } catch (e) {
    const fields = problem.fields(e)
    if (fields.title) error.value = fields.title
    else toast.error(e)
  } finally {
    pending.value = false
  }
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault()
    void submit()
  } else if (event.key === 'Escape') {
    event.stopPropagation()
    emit('close')
  }
}
</script>

<template>
  <form class="flex flex-col gap-2" novalidate @submit.prevent="submit">
    <label class="sr-only" :for="`composer-${props.listId}-${props.position}`">{{ t('card.titleLabel') }}</label>
    <textarea
      :id="`composer-${props.listId}-${props.position}`"
      ref="field"
      v-model="title"
      rows="2"
      :maxlength="limits.cardTitle"
      :placeholder="t('card.composerPlaceholder')"
      :aria-invalid="error ? 'true' : undefined"
      dir="auto"
      class="block w-full resize-none rounded-xl border border-line bg-surface px-3 py-2 shadow-xs focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
      @keydown="onKeydown"
    />
    <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
    <div class="flex items-center gap-2">
      <UiButton type="submit" size="sm" :loading="pending">{{ t('card.add') }}</UiButton>
      <UiButton variant="ghost" size="sm" icon="close" :label="t('common.cancel')" @click="emit('close')" />
    </div>
  </form>
</template>
