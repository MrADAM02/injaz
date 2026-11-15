<script setup lang="ts">
const model = defineModel<string>({ default: '' })

const props = withDefaults(
  defineProps<{ label: string; error?: string | null; hint?: string; placeholder?: string; rows?: number; maxlength?: number }>(),
  { rows: 3 },
)

const id = useId()
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <label :for="id" class="text-sm font-medium">{{ props.label }}</label>
    <textarea
      :id="id"
      v-model="model"
      :rows="props.rows"
      :placeholder="props.placeholder"
      :maxlength="props.maxlength"
      :aria-invalid="props.error ? 'true' : undefined"
      :aria-describedby="props.error ? `${id}-error` : props.hint ? `${id}-hint` : undefined"
      class="block w-full resize-y rounded-xl border bg-surface px-3.5 py-2.5 text-ink placeholder:text-muted/70 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
      :class="props.error ? 'border-danger' : 'border-line'"
    />
    <p v-if="props.hint && !props.error" :id="`${id}-hint`" class="text-xs text-muted">{{ props.hint }}</p>
    <p v-if="props.error" :id="`${id}-error`" class="text-xs font-medium text-danger">{{ props.error }}</p>
  </div>
</template>
