<script setup lang="ts">
/**
 * Text that turns into a field when activated (list and card titles). Enter or leaving the field saves,
 * Esc cancels. An empty or unchanged value just closes the editor. Read-only text when not `editable`.
 */
const props = withDefaults(
  defineProps<{
    value: string
    label: string
    editable?: boolean
    multiline?: boolean
    maxlength?: number
    /** Classes for the text and the field, so both look the same. */
    textClass?: string
    /** Field classes when they must differ (inputs stay at 16px or iOS zooms in). Default: `textClass`. */
    fieldClass?: string
    tag?: string
  }>(),
  { tag: 'span', textClass: '' },
)
const emit = defineEmits<{ save: [value: string] }>()

const editing = ref(false)
const draft = ref('')
const field = useTemplateRef<HTMLInputElement | HTMLTextAreaElement>('field')

async function start() {
  if (!props.editable) return
  draft.value = props.value
  editing.value = true
  await nextTick()
  field.value?.focus()
  field.value?.select()
  if (field.value instanceof HTMLTextAreaElement) resize()
}

function commit() {
  if (!editing.value) return
  editing.value = false
  const next = draft.value.trim()
  if (next && next !== props.value) emit('save', next)
}

function cancel() {
  editing.value = false
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault()
    commit()
  } else if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    cancel()
  }
}

function resize() {
  const el = field.value
  if (!(el instanceof HTMLTextAreaElement)) return
  el.style.height = 'auto'
  el.style.height = `${el.scrollHeight}px`
}

defineExpose({ start })
</script>

<template>
  <!-- Display text sits in <bdi>: it keeps its own direction while alignment follows the page's. -->
  <textarea
    v-if="editing && props.multiline"
    ref="field"
    v-model="draft"
    rows="1"
    :aria-label="props.label"
    :maxlength="props.maxlength"
    dir="auto"
    class="block w-full resize-none overflow-hidden rounded-lg border border-accent bg-surface px-2 py-1 focus:outline-none focus:ring-3 focus:ring-accent/20"
    :class="props.fieldClass ?? props.textClass"
    @input="resize"
    @keydown="onKeydown"
    @blur="commit"
  />
  <input
    v-else-if="editing"
    ref="field"
    v-model="draft"
    :aria-label="props.label"
    :maxlength="props.maxlength"
    dir="auto"
    class="block w-full min-w-0 rounded-lg border border-accent bg-surface px-2 py-1 focus:outline-none focus:ring-3 focus:ring-accent/20"
    :class="props.fieldClass ?? props.textClass"
    @keydown="onKeydown"
    @blur="commit"
  >
  <button
    v-else-if="props.editable"
    type="button"
    class="block w-full min-w-0 rounded-lg px-2 py-1 text-start hover:bg-subtle"
    :class="props.textClass"
    :aria-label="`${props.label}: ${props.value}`"
    @click="start"
  ><bdi>{{ props.value }}</bdi></button>
  <component :is="props.tag" v-else class="block min-w-0 px-2 py-1" :class="props.textClass"><bdi>{{ props.value }}</bdi></component>
</template>
