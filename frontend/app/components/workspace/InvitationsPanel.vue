<script setup lang="ts">
import type { CreatedInvitation, Invitation, Workspace } from '~/types/api'
import { isolateLtr } from '~/utils/bidi'

const props = defineProps<{ workspace: Workspace }>()

const { t } = useI18n()
const api = useApi()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()
const format = useFormat()

const invitations = ref<Invitation[]>([])
const loading = ref(true)
const error = ref<unknown>(null)
const inviteOpen = ref(false)
const revoking = ref<string | null>(null)

const base = computed(() => `/api/workspaces/${props.workspace.id}/invitations`)

async function load() {
  loading.value = true
  error.value = null
  try {
    invitations.value = await api.get<Invitation[]>(base.value)
  } catch (e) {
    error.value = e
  } finally {
    loading.value = false
  }
}

watch(() => props.workspace.id, load, { immediate: true })

function onCreated(created: CreatedInvitation) {
  // Inviting the same email again revokes its previous link on the server.
  invitations.value = [created.invitation, ...invitations.value.filter((i) => i.email !== created.invitation.email)]
}

async function revoke(invitation: Invitation) {
  const ok = await confirm({
    title: t('invitations.revokeConfirm', { email: isolateLtr(invitation.email) }),
    body: t('invitations.revokeBody'),
    confirmLabel: t('invitations.revoke'),
    danger: true,
  })
  if (!ok) return
  revoking.value = invitation.id
  try {
    await api.delete(`${base.value}/${invitation.id}`)
    invitations.value = invitations.value.filter((i) => i.id !== invitation.id)
    toast.success(t('invitations.revoked'))
  } catch (e) {
    toast.error(e)
  } finally {
    revoking.value = null
  }
}
</script>

<template>
  <UiCard :title="t('invitations.pending')" :description="t('invitations.replaceHint')">
    <template #actions>
      <UiButton icon="mail" @click="inviteOpen = true">{{ t('invitations.invite') }}</UiButton>
    </template>

    <div v-if="loading" class="flex justify-center py-8 text-muted">
      <UiSpinner :label="t('common.loading')" />
    </div>
    <UiAlert v-else-if="error">
      {{ problem.message(error) }}
      <button type="button" class="ms-2 font-semibold underline" @click="load">{{ t('common.retry') }}</button>
    </UiAlert>
    <p v-else-if="invitations.length === 0" class="py-4 text-sm text-muted">{{ t('invitations.none') }}</p>

    <ul v-else class="-mx-4 divide-y divide-line sm:-mx-6">
      <li v-for="invitation in invitations" :key="invitation.id" class="flex items-center gap-3 px-4 py-3 sm:px-6">
        <span class="flex size-9 shrink-0 items-center justify-center rounded-full bg-subtle text-muted">
          <UiIcon name="mail" :size="18" />
        </span>
        <div class="min-w-0 flex-1">
          <p class="flex flex-wrap items-center gap-2 text-sm font-medium">
            <span class="truncate" dir="ltr">{{ invitation.email }}</span>
            <WorkspaceRoleBadge :role="invitation.role" />
          </p>
          <p class="truncate text-xs text-muted">
            {{ t('invitations.invitedBy', { name: invitation.invitedByName }) }} ·
            {{ t('invitations.expires', { date: format.date(invitation.expiresAt) }) }}
          </p>
        </div>
        <UiButton variant="danger-ghost" size="sm" :loading="revoking === invitation.id" @click="revoke(invitation)">
          {{ t('invitations.revoke') }}
        </UiButton>
      </li>
    </ul>

    <WorkspaceInviteDialog v-model:open="inviteOpen" :workspace="props.workspace" @created="onCreated" />
  </UiCard>
</template>
