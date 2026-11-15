<script setup lang="ts">
const { t } = useI18n()
</script>

<template>
  <NuxtLink to="/" class="inline-flex items-center gap-2.5 rounded-lg font-bold">
    <img src="/favicon.svg" alt="" class="size-8" width="32" height="32">
    <span class="text-lg tracking-tight">{{ t('app.name') }}</span>
  </NuxtLink>
</template>
