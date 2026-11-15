<script setup lang="ts">
import type { IconName } from '~/utils/icons'

/** Phone-only tab bar for the three top-level destinations. */
const route = useRoute()
const { t } = useI18n()

const tabs = computed<{ to: string; label: string; icon: IconName; active: boolean }[]>(() => [
  { to: '/', label: t('nav.home'), icon: 'home', active: route.path === '/' || route.path.startsWith('/w/') || route.path.startsWith('/b/') },
  { to: '/my-tasks', label: t('nav.myTasks'), icon: 'tasks', active: route.path.startsWith('/my-tasks') },
  { to: '/profile', label: t('nav.profile'), icon: 'user', active: route.path.startsWith('/profile') },
])
</script>

<template>
  <nav :aria-label="t('nav.primary')" class="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface/95 pb-safe backdrop-blur md:hidden">
    <ul class="grid h-16 grid-cols-3">
      <li v-for="tab in tabs" :key="tab.to">
        <NuxtLink
          :to="tab.to"
          class="flex h-full flex-col items-center justify-center gap-0.5 text-[11px] font-medium"
          :class="tab.active ? 'text-accent' : 'text-muted'"
          :aria-current="tab.active ? 'page' : undefined"
        >
          <UiIcon :name="tab.icon" :size="22" />
          {{ tab.label }}
        </NuxtLink>
      </li>
    </ul>
  </nav>
</template>
