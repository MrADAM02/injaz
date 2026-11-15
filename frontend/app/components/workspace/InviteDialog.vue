<script setup lang="ts">
import type { CreatedInvitation, Workspace, WorkspaceRole } from '~/types/api'
import { isolateLtr } from '~/utils/bidi'
import { assignableRoles } from '~/utils/roles'
import { email as emailRule, limits, maxLength, required, validateFields } from '~/utils/validate'

/**
 * Two steps: pick email + role, then show the one-time accept link to copy or share. There is no email
 * delivery yet (the API only logs it), so the link is the way to reach people.
 */
const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ workspace: Workspace }>()
const emit = defineEmits<{ created: [invitation: CreatedInvitation] }>()

const { t } = useI18n()
const api = useApi()
const format = useFormat()
const form = useFormState(['email', 'role'] as const)

const email = ref('')
const role = ref<WorkspaceRole>('member')
const created = ref<CreatedInvitation | null>(null)
const copied = ref(false)
const canShare = import.meta.client && typeof navigator.share === 'function'

const roleOptions = computed(() => assignableRoles.map((r) => ({ value: r, label: `${t(`roles.${r}`)} — ${t(`roles.hint.${r}`)}` })))

watch(open, (value) => {
  if (!value) return
  email.value = ''
  role.value = 'member'
  created.value = null
  copied.value = false
  form.reset()
})

async function submit() {
  const result = await form.submit(
    () => api.post<CreatedInvitation>(`/api/workspaces/${props.workspace.id}/invitations`, { email: email.value.trim(), role: role.value }),
    () => validateFields({ email: [email.value, required, emailRule, maxLength(limits.email)] }),
  )
  if (result) {
    created.value = result
    emit('created', result)
  }
}

async function copy() {
  if (!created.value) return
  try {
    await navigator.clipboard.writeText(created.value.acceptUrl)
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch {
    // Clipboard blocked: the link field is selectable as a fallback.
    document.getElementById('invite-link')?.focus()
  }
}

async function share() {
  if (!created.value) return
  try {
    await navigator.share({ title: t('invite.title'), text: t('invitations.inviteTitle', { name: props.workspace.name }), url: created.value.acceptUrl })
  } catch {
    // Dismissed by the user.
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="created ? t('invitations.readyTitle') : t('invitations.inviteTitle', { name: props.workspace.name })">
    <form v-if="!created" novalidate class="flex flex-col gap-4" @submit.prevent="submit">
      <UiAlert v-if="form.formError.value">{{ form.formError.value }}</UiAlert>
      <UiInput
        v-model="email"
        type="email"
        inputmode="email"
        autocomplete="off"
        :label="t('invitations.email')"
        :error="form.errors.value.email"
        required
        autofocus
      />
      <UiSelect v-model="role" :label="t('invitations.role')" :options="roleOptions" :error="form.errors.value.role" />
      <UiDialogActions>
        <UiButton variant="secondary" @click="open = false">{{ t('common.cancel') }}</UiButton>
        <UiButton type="submit" icon="link" :loading="form.pending.value">{{ t('invitations.send') }}</UiButton>
      </UiDialogActions>
    </form>

    <div v-else class="flex flex-col gap-4">
      <p class="text-sm">
        {{ t('invitations.readyBody', { email: isolateLtr(created.invitation.email), date: format.date(created.invitation.expiresAt) }) }}
      </p>
      <div class="flex gap-2">
        <input
          id="invite-link"
          :value="created.acceptUrl"
          readonly
          dir="ltr"
          :aria-label="t('invitations.readyTitle')"
          class="min-h-11 min-w-0 flex-1 rounded-xl border border-line bg-subtle px-3 text-sm text-ink"
          @focus="($event.target as HTMLInputElement).select()"
        >
        <UiButton variant="secondary" :icon="copied ? 'check' : 'copy'" @click="copy">{{ copied ? t('common.copied') : t('common.copy') }}</UiButton>
      </div>
      <UiAlert tone="warning">{{ t('invitations.readyWarning') }}</UiAlert>
      <UiDialogActions>
        <UiButton v-if="canShare" variant="secondary" icon="link" @click="share">{{ t('common.share') }}</UiButton>
        <UiButton @click="open = false">{{ t('common.close') }}</UiButton>
      </UiDialogActions>
    </div>
  </UiModal>
</template>
