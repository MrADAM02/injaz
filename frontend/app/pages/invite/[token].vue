<script setup lang="ts">
import type { InvitationPreview, Workspace } from '~/types/api'
import { isApiError } from '~/utils/api-client'
import { isolateLtr } from '~/utils/bidi'

/**
 * The link from an invitation. Public: it previews the invitation for signed-out visitors and routes them
 * through sign-in / sign-up (with the invited email pre-filled), then back here to accept.
 */
definePageMeta({ layout: 'auth', public: true })

const { t } = useI18n()
const route = useRoute()
const api = useApi()
const auth = useAuthStore()
const workspaces = useWorkspacesStore()
const toast = useToast()
const problem = useProblem()
const format = useFormat()
usePageTitle(() => t('invite.title'))

const token = computed(() => String(route.params.token ?? ''))
const preview = ref<InvitationPreview | null>(null)
const loadError = ref<unknown>(null)
const loading = ref(true)
const accepting = ref(false)
const acceptError = ref<string | null>(null)

const emailMatches = computed(
  () => !!auth.user && !!preview.value && auth.user.email.toLowerCase() === preview.value.email.toLowerCase(),
)
const authQuery = computed(() => ({ redirect: route.fullPath, email: preview.value?.email }))

async function load() {
  loading.value = true
  loadError.value = null
  try {
    preview.value = await api.get<InvitationPreview>(`/api/invitations/${encodeURIComponent(token.value)}`, undefined, { auth: false })
  } catch (error) {
    loadError.value = error
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function accept() {
  accepting.value = true
  acceptError.value = null
  try {
    const workspace = await api.post<Workspace>(`/api/invitations/${encodeURIComponent(token.value)}/accept`)
    workspaces.upsert(workspace)
    toast.success(t('invite.joined', { name: workspace.name }))
    await navigateTo(`/w/${workspace.id}`, { replace: true })
  } catch (error) {
    acceptError.value = problem.message(error)
    if (isApiError(error) && error.status === 422) await load() // status changed (used, revoked, expired)
  } finally {
    accepting.value = false
  }
}

function switchAccount() {
  void auth.logout({ path: '/login', query: authQuery.value })
}
</script>

<template>
  <div class="rounded-2xl border border-line bg-surface p-6 shadow-sm sm:p-8">
    <div v-if="loading" class="flex justify-center py-10 text-muted">
      <UiSpinner :size="28" :label="t('common.loading')" />
    </div>

    <UiEmptyState
      v-else-if="loadError || !preview"
      icon="link"
      :title="isApiError(loadError, 'invitation.notFound') ? t('invite.notFound') : problem.message(loadError)"
    >
      <UiButton v-if="!isApiError(loadError, 'invitation.notFound')" variant="secondary" @click="load">{{ t('common.retry') }}</UiButton>
      <UiButton v-else to="/" variant="secondary">{{ t('invite.goHome') }}</UiButton>
    </UiEmptyState>

    <template v-else>
      <div class="flex flex-col items-center text-center">
        <div class="mb-4 flex size-14 items-center justify-center rounded-2xl bg-accent-soft text-accent">
          <UiIcon name="users" :size="26" />
        </div>
        <h1 class="text-2xl font-bold">{{ t('invite.title') }}</h1>
        <p class="mt-2 text-muted">
          <i18n-t keypath="invite.body" scope="global">
            <template #inviter><strong class="text-ink">{{ preview.invitedByName }}</strong></template>
            <template #workspace><strong class="text-ink">{{ preview.workspaceName }}</strong></template>
            <template #role><strong class="text-ink">{{ t(`roles.${preview.role}`) }}</strong></template>
          </i18n-t>
        </p>
        <p class="mt-1 text-sm text-muted">
          {{ t('invite.forEmail', { email: isolateLtr(preview.email) }) }}
        </p>
      </div>

      <div class="mt-6 flex flex-col gap-3">
        <UiAlert v-if="preview.status !== 'pending'" tone="warning">{{ t(`invite.${preview.status}`) }}</UiAlert>
        <UiAlert v-else-if="acceptError">{{ acceptError }}</UiAlert>

        <template v-if="preview.status !== 'pending'">
          <UiButton :to="auth.isAuthenticated ? '/' : '/login'" variant="secondary" block>{{ t('invite.goHome') }}</UiButton>
        </template>

        <template v-else-if="!auth.isAuthenticated">
          <UiButton :to="{ path: '/register', query: authQuery }" block>{{ t('invite.register') }}</UiButton>
          <UiButton :to="{ path: '/login', query: authQuery }" variant="secondary" block>{{ t('invite.signIn') }}</UiButton>
        </template>

        <template v-else-if="emailMatches">
          <UiButton block :loading="accepting" @click="accept">{{ t('invite.accept') }}</UiButton>
          <p class="text-center text-xs text-muted">{{ t('invitations.expires', { date: format.date(preview.expiresAt) }) }}</p>
        </template>

        <template v-else>
          <UiAlert tone="warning">{{ t('invite.wrongAccount', { current: isolateLtr(auth.user?.email), email: isolateLtr(preview.email) }) }}</UiAlert>
          <UiButton variant="secondary" block @click="switchAccount">{{ t('invite.switchAccount') }}</UiButton>
        </template>
      </div>
    </template>
  </div>
</template>
