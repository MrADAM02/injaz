<script setup lang="ts">
/** Workspace settings: General / Members / Invitations. The tab lives in `?tab=` so it survives reloads and Back. */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const { id, workspace, allows } = useCurrentWorkspace()
usePageTitle(() => (workspace.value ? `${t('nav.settings')} · ${workspace.value.name}` : undefined))

type Tab = 'general' | 'members' | 'invitations'

const tabs = computed<Tab[]>(() => (allows('manageMembers') ? ['general', 'members', 'invitations'] : ['general', 'members']))
const tab = computed<Tab>({
  get: () => {
    const requested = route.query.tab as Tab
    return tabs.value.includes(requested) ? requested : 'general'
  },
  set: (value) => {
    void router.replace({ query: { ...route.query, tab: value === 'general' ? undefined : value } })
  },
})

const members = useMembers(id)
watch(id, () => members.load(), { immediate: true })
</script>

<template>
  <div v-if="workspace">
    <div class="mb-6 inline-flex w-full rounded-xl bg-subtle p-1 sm:w-auto" role="tablist">
      <button
        v-for="name in tabs"
        :key="name"
        type="button"
        role="tab"
        :aria-selected="tab === name"
        class="min-h-10 flex-1 rounded-lg px-4 text-sm font-medium transition-colors sm:flex-none"
        :class="tab === name ? 'bg-surface text-ink shadow-sm' : 'text-muted hover:text-ink'"
        @click="tab = name"
      >
        {{ t(`settings.tabs.${name}`) }}
      </button>
    </div>

    <div role="tabpanel" class="max-w-3xl">
      <WorkspaceGeneralPanel v-if="tab === 'general'" :workspace="workspace" :members="members.members.value" @members-changed="members.load()" />
      <WorkspaceMembersPanel v-else-if="tab === 'members'" :workspace="workspace" :state="members" />
      <WorkspaceInvitationsPanel v-else :workspace="workspace" />
    </div>
  </div>
</template>
