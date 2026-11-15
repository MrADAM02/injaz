<script setup lang="ts" generic="T extends string">
/** Native <select>: the best picker on phones (system wheel / sheet), styled to match the inputs. */
const model = defineModel<T>({ required: true })

const props = defineProps<{
  label: string
  options: readonly { value: T; label: string }[]
  error?: string | null
  hint?: string
  disabled?: boolean
  hideLabel?: boolean
  size?: 'md' | 'sm'
}>()

const id = useId()
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <label :for="id" class="text-sm font-medium" :class="{ 'sr-only': props.hideLabel }">{{ props.label }}</label>
    <div class="relative">
      <select
        :id="id"
        v-model="model"
        :disabled="props.disabled"
        :aria-invalid="props.error ? 'true' : undefined"
        class="block w-full appearance-none rounded-xl border bg-surface ps-3.5 pe-10 text-ink focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20 disabled:opacity-60"
        :class="[props.error ? 'border-danger' : 'border-line', props.size === 'sm' ? 'min-h-9 py-1 text-sm' : 'min-h-11 py-2']"
      >
        <option v-for="option in props.options" :key="option.value" :value="option.value">{{ option.label }}</option>
      </select>
      <UiIcon name="chevronDown" :size="16" class="pointer-events-none absolute inset-e-3.5 top-1/2 -translate-y-1/2 text-muted" />
    </div>
    <p v-if="props.hint && !props.error" class="text-xs text-muted">{{ props.hint }}</p>
    <p v-if="props.error" class="text-xs font-medium text-danger">{{ props.error }}</p>
  </div>
</template>
