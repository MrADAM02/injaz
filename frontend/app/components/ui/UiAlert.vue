<script setup lang="ts">
const props = withDefaults(defineProps<{ tone?: 'error' | 'info' | 'warning' }>(), { tone: 'error' })

const tones = {
  error: { icon: 'alert', class: 'border-danger/30 bg-danger-soft text-danger' },
  warning: { icon: 'alert', class: 'border-amber-500/30 bg-amber-50 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300' },
  info: { icon: 'info', class: 'border-accent/30 bg-accent-soft text-accent-strong' },
} as const
</script>

<template>
  <div class="flex items-start gap-2.5 rounded-xl border px-3.5 py-3 text-sm" :class="tones[props.tone].class" :role="props.tone === 'error' ? 'alert' : 'status'">
    <UiIcon :name="tones[props.tone].icon" :size="18" class="mt-0.5" />
    <div class="min-w-0 flex-1"><slot /></div>
  </div>
</template>
