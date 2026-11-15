<script setup lang="ts">
import type { IconName } from '~/utils/icons'

const props = defineProps<{ icon?: IconName; title: string; body?: string }>()
</script>

<template>
  <div class="flex flex-col items-center px-6 py-12 text-center">
    <div v-if="props.icon" class="mb-4 flex size-14 items-center justify-center rounded-2xl bg-accent-soft text-accent">
      <UiIcon :name="props.icon" :size="26" />
    </div>
    <h2 class="text-lg font-semibold">{{ props.title }}</h2>
    <p v-if="props.body" class="mt-1.5 max-w-sm text-sm text-muted">{{ props.body }}</p>
    <div v-if="$slots.default" class="mt-6 flex flex-wrap justify-center gap-2">
      <slot />
    </div>
  </div>
</template>
