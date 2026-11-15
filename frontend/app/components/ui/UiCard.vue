<script setup lang="ts">
const props = defineProps<{ title?: string; description?: string; tone?: 'danger' }>()
</script>

<template>
  <section class="rounded-2xl border bg-surface p-4 sm:p-6" :class="props.tone === 'danger' ? 'border-danger/40' : 'border-line'">
    <header v-if="props.title || $slots.actions" class="mb-4 flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0">
        <h2 class="text-base font-semibold" :class="{ 'text-danger': props.tone === 'danger' }">{{ props.title }}</h2>
        <p v-if="props.description" class="mt-1 text-sm text-muted">{{ props.description }}</p>
      </div>
      <slot name="actions" />
    </header>
    <slot />
  </section>
</template>
