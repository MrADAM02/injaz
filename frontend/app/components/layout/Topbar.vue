<script setup lang="ts">
/** Phone-only compact header: drawer toggle, the page title, and the account shortcut. */
const ui = useUiStore()
const auth = useAuthStore()
const title = useCurrentPageTitle()
const { t } = useI18n()
</script>

<template>
  <header class="sticky top-0 z-30 border-b border-line bg-surface/95 pt-safe backdrop-blur md:hidden">
    <div class="flex h-14 items-center gap-2 px-2">
      <UiButton variant="ghost" icon="menu" :label="t('nav.openMenu')" :aria-expanded="ui.drawerOpen" @click="ui.drawerOpen = true" />
      <p class="min-w-0 flex-1 truncate text-base font-semibold">{{ title || t('app.name') }}</p>
      <NuxtLink v-if="auth.user" to="/profile" class="flex size-11 items-center justify-center rounded-full" :aria-label="t('nav.profile')">
        <UiAvatar :name="auth.user.displayName" :src="auth.user.avatarUrl" size="sm" />
      </NuxtLink>
    </div>
  </header>
</template>
