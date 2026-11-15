<script setup lang="ts">
/** Home: go straight to the last-used workspace; first-run users get the "create a workspace" prompt. */
const { t } = useI18n()
const workspaces = useWorkspacesStore()
const ui = useUiStore()
const problem = useProblem()
usePageTitle(() => t('nav.home'))

const error = ref<unknown>(null)

async function load() {
  error.value = null
  try {
    await workspaces.ensureLoaded()
  } catch (e) {
    error.value = e
    return
  }
  const last = workspaces.lastWorkspaceId()
  const target = workspaces.byId(last ?? undefined) ?? workspaces.items[0]
  if (target) await navigateTo(`/w/${target.id}`, { replace: true })
}

onMounted(load)
</script>

<template>
  <div>
    <UiEmptyState v-if="error" icon="alert" :title="problem.message(error)">
      <UiButton variant="secondary" @click="load">{{ t('common.retry') }}</UiButton>
    </UiEmptyState>
    <UiEmptyState v-else-if="workspaces.loaded && workspaces.items.length === 0" icon="users" :title="t('home.empty.title')" :body="t('home.empty.body')">
      <UiButton icon="plus" @click="ui.createWorkspaceOpen = true">{{ t('workspace.create') }}</UiButton>
    </UiEmptyState>
    <div v-else class="flex justify-center py-16 text-muted">
      <UiSpinner :size="28" :label="t('common.loading')" />
    </div>
  </div>
</template>
