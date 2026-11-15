<script setup lang="ts">
import { limits } from '~/utils/validate'

/** Markdown description: rendered when idle, a textarea while editing (Ctrl/⌘+Enter saves, Esc cancels). */
const props = defineProps<{ value: string | null; editable: boolean; save: (value: string | null) => Promise<boolean> }>()

const { t } = useI18n()
const editing = ref(false)
const draft = ref('')
const pending = ref(false)
const field = useTemplateRef<HTMLTextAreaElement>('field')
const id = useId()

async function start() {
  if (!props.editable) return
  draft.value = props.value ?? ''
  editing.value = true
  await nextTick()
  field.value?.focus()
}

async function submit() {
  if (pending.value) return
  pending.value = true
  const next = draft.value.trim() ? draft.value : null
  try {
    if (next === props.value || (await props.save(next))) editing.value = false
  } finally {
    pending.value = false
  }
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    void submit()
  } else if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    editing.value = false
  }
}
</script>

<template>
  <section :aria-labelledby="`${id}-title`">
    <div class="mb-2 flex items-center gap-2">
      <UiIcon name="description" :size="18" class="text-muted" />
      <h3 :id="`${id}-title`" class="flex-1 text-sm font-semibold">{{ t('card.description') }}</h3>
      <UiButton v-if="props.editable && props.value && !editing" variant="ghost" size="sm" icon="edit" @click="start">{{ t('common.edit') }}</UiButton>
    </div>

    <form v-if="editing" class="flex flex-col gap-2" @submit.prevent="submit">
      <label :for="`${id}-field`" class="sr-only">{{ t('card.description') }}</label>
      <textarea
        :id="`${id}-field`"
        ref="field"
        v-model="draft"
        rows="8"
        :maxlength="limits.cardDescription"
        :placeholder="t('card.descriptionPlaceholder')"
        dir="auto"
        class="block w-full resize-y rounded-xl border border-line bg-surface px-3.5 py-2.5 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
        @keydown="onKeydown"
      />
      <div class="flex flex-wrap items-center gap-2">
        <UiButton type="submit" size="sm" :loading="pending">{{ t('common.save') }}</UiButton>
        <UiButton variant="ghost" size="sm" @click="editing = false">{{ t('common.cancel') }}</UiButton>
        <span class="ms-auto text-xs text-muted">{{ t('card.markdownHint') }}</span>
      </div>
    </form>

    <UiMarkdown v-else-if="props.value" :source="props.value" class="rounded-xl px-1" />
    <button
      v-else-if="props.editable"
      type="button"
      class="flex min-h-16 w-full items-start rounded-xl bg-subtle px-3.5 py-3 text-start text-sm text-muted hover:bg-line/60"
      @click="start"
    >
      {{ t('card.addDescription') }}
    </button>
    <p v-else class="text-sm text-muted">{{ t('card.noDescription') }}</p>
  </section>
</template>
