<script setup lang="ts">
import type { IconName } from '~/utils/icons'

/** Desktop sidebar; the same content slides in as the drawer on phones. */
const auth = useAuthStore()
const workspaces = useWorkspacesStore()
const ui = useUiStore()
const route = useRoute()
const { t } = useI18n()

const nav = computed<{ to: string; label: string; icon: IconName; active: boolean }[]>(() => [
  { to: '/', label: t('nav.home'), icon: 'home', active: route.path === '/' },
  { to: '/my-tasks', label: t('nav.myTasks'), icon: 'tasks', active: route.path.startsWith('/my-tasks') },
])

const board = useBoardStore()
// On a board, its workspace is the active one.
const activeWorkspaceId = computed(() =>
  typeof route.params.workspaceId === 'string' ? route.params.workspaceId : route.params.boardId ? board.board?.workspaceId : undefined,
)
</script>

<template>
  <div class="flex h-full flex-col">
    <div class="flex h-16 shrink-0 items-center justify-between gap-2 px-4">
      <LayoutBrand />
      <slot name="close" />
    </div>

    <nav :aria-label="t('nav.primary')" class="flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto px-3 pb-4">
      <ul class="flex flex-col gap-0.5">
        <li v-for="item in nav" :key="item.to">
          <NuxtLink
            :to="item.to"
            class="flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm font-medium transition-colors"
            :class="item.active ? 'bg-accent-soft text-accent-strong' : 'text-ink hover:bg-subtle'"
            :aria-current="item.active ? 'page' : undefined"
          >
            <UiIcon :name="item.icon" />
            {{ item.label }}
          </NuxtLink>
        </li>
      </ul>

      <section>
        <div class="mb-1 flex items-center justify-between ps-3">
          <h2 class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('nav.workspaces') }}</h2>
          <UiButton variant="ghost" size="sm" icon="plus" :label="t('nav.newWorkspace')" @click="ui.createWorkspaceOpen = true" />
        </div>
        <ul class="flex flex-col gap-0.5">
          <li v-for="workspace in workspaces.items" :key="workspace.id">
            <NuxtLink
              :to="`/w/${workspace.id}`"
              class="flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm transition-colors"
              :class="workspace.id === activeWorkspaceId ? 'bg-subtle font-semibold' : 'hover:bg-subtle'"
              :aria-current="workspace.id === activeWorkspaceId ? 'page' : undefined"
            >
              <span class="flex size-7 shrink-0 items-center justify-center rounded-lg bg-accent text-xs font-bold text-on-accent">
                {{ Array.from(workspace.name)[0] }}
              </span>
              <span class="truncate">{{ workspace.name }}</span>
            </NuxtLink>
          </li>
        </ul>
      </section>
    </nav>

    <div v-if="auth.user" class="shrink-0 border-t border-line p-3 pb-safe">
      <NuxtLink to="/profile" class="flex min-h-11 items-center gap-3 rounded-xl px-2 py-1.5 hover:bg-subtle">
        <UiAvatar :name="auth.user.displayName" :src="auth.user.avatarUrl" size="sm" />
        <span class="min-w-0 flex-1">
          <span class="block truncate text-sm font-medium">{{ auth.user.displayName }}</span>
          <span class="block truncate text-xs text-muted" dir="ltr">{{ auth.user.email }}</span>
        </span>
      </NuxtLink>
      <div class="mt-1 flex items-center gap-1">
        <LayoutLocaleSwitcher class="flex-1" />
        <UiButton variant="ghost" size="sm" icon="logout" :label="t('nav.signOut')" :title="t('nav.signOut')" @click="auth.logout()" />
      </div>
    </div>
  </div>
</template>
