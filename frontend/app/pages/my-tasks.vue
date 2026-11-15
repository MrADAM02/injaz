<script setup lang="ts">
// Placeholder until phase 7b (the API side — GET /api/me/tasks — already exists).
const { t } = useI18n()
usePageTitle(() => t('myTasks.title'))
</script>

<template>
  <div>
    <h1 class="mb-6 hidden text-2xl font-bold md:block">{{ t('myTasks.title') }}</h1>
    <UiEmptyState icon="tasks" :title="t('myTasks.title')" :body="t('myTasks.placeholder')" />
  </div>
</template>
