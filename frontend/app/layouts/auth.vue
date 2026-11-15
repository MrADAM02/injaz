<script setup lang="ts">
const { t } = useI18n()
</script>

<template>
  <div class="flex min-h-dvh flex-col pt-safe pb-safe">
    <header class="flex items-center justify-between px-4 py-3 sm:px-6">
      <LayoutBrand />
      <LayoutLocaleSwitcher />
    </header>
    <main class="flex flex-1 items-start justify-center px-4 pb-10 pt-4 sm:items-center sm:pt-0">
      <div class="w-full max-w-md">
        <slot />
      </div>
    </main>
    <footer class="px-4 pb-4 text-center text-xs text-muted">{{ t('app.tagline') }}</footer>
  </div>
</template>
