<script setup lang="ts">
/** Shared header for a workspace's pages (boards, settings), plus the "not found / no access" state. */
const { t } = useI18n()
const route = useRoute()
const workspaces = useWorkspacesStore()
const { id, workspace, missing } = useCurrentWorkspace()

watch(
  workspace,
  (current) => {
    if (current) workspaces.remember(current.id)
  },
  { immediate: true },
)

const tabs = computed(() => [
  { to: `/w/${id.value}`, label: t('workspace.boards'), icon: 'board' as const, active: !route.path.endsWith('/settings') },
  { to: `/w/${id.value}/settings`, label: t('nav.settings'), icon: 'settings' as const, active: route.path.endsWith('/settings') },
])
</script>

<template>
  <div>
    <UiEmptyState v-if="missing" icon="alert" :title="t('errors.workspace.notFound')">
      <UiButton to="/" variant="secondary">{{ t('invite.goHome') }}</UiButton>
    </UiEmptyState>

    <template v-else-if="workspace">
      <header class="mb-6">
        <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
          <h1 class="min-w-0 truncate text-2xl font-bold sm:text-3xl">{{ workspace.name }}</h1>
          <WorkspaceRoleBadge :role="workspace.myRole" />
        </div>
        <p class="mt-1 text-sm text-muted">{{ t('workspace.memberCount', workspace.memberCount) }}</p>

        <nav class="-mx-4 mt-4 flex gap-1 overflow-x-auto border-b border-line px-4 sm:mx-0 sm:px-0" :aria-label="t('workspace.settings')">
          <NuxtLink
            v-for="tab in tabs"
            :key="tab.to"
            :to="tab.to"
            class="-mb-px flex min-h-11 shrink-0 items-center gap-2 border-b-2 px-3 text-sm font-medium transition-colors"
            :class="tab.active ? 'border-accent text-accent-strong' : 'border-transparent text-muted hover:text-ink'"
            :aria-current="tab.active ? 'page' : undefined"
          >
            <UiIcon :name="tab.icon" :size="18" />
            {{ tab.label }}
          </NuxtLink>
        </nav>
      </header>

      <NuxtPage />
    </template>

    <div v-else class="flex justify-center py-16 text-muted">
      <UiSpinner :size="28" :label="t('common.loading')" />
    </div>
  </div>
</template>
