<script setup lang="ts">
import type { Member, Workspace } from '~/types/api'
import { can } from '~/utils/roles'
import { limits, maxLength, required, validateFields } from '~/utils/validate'

const props = defineProps<{ workspace: Workspace; members: Member[] }>()
const emit = defineEmits<{ membersChanged: [] }>()

const { t } = useI18n()
const auth = useAuthStore()
const workspaces = useWorkspacesStore()
const toast = useToast()
const confirm = useConfirm()

const canRename = computed(() => can(props.workspace.myRole, 'manageWorkspace'))
const isOwner = computed(() => props.workspace.myRole === 'owner')

// ----- rename -----
const name = ref(props.workspace.name)
watch(
  () => props.workspace.name,
  (value) => {
    name.value = value
  },
)
const renameForm = useFormState(['name'] as const)
const renameDirty = computed(() => name.value.trim() !== props.workspace.name)

async function rename() {
  const ok = await renameForm.submit(
    async () => {
      await workspaces.rename(props.workspace.id, name.value.trim())
      return true
    },
    () => validateFields({ name: [name.value, required, maxLength(limits.workspaceName)] }),
  )
  if (ok) toast.success(t('settings.renamed'))
}

// ----- transfer ownership -----
const candidates = computed(() => props.members.filter((m) => m.userId !== auth.user?.id))
const newOwnerId = ref('')
watch(
  candidates,
  (list) => {
    if (!list.some((m) => m.userId === newOwnerId.value)) newOwnerId.value = list[0]?.userId ?? ''
  },
  { immediate: true },
)
const transferring = ref(false)

async function transfer() {
  const target = candidates.value.find((m) => m.userId === newOwnerId.value)
  if (!target) return
  const ok = await confirm({
    title: t('settings.transfer.confirm', { name: target.displayName }),
    body: t('settings.transfer.body'),
    confirmLabel: t('settings.transfer.action'),
    danger: true,
  })
  if (!ok) return
  transferring.value = true
  try {
    await workspaces.transferOwnership(props.workspace.id, target.userId)
    toast.success(t('settings.transfer.done', { name: target.displayName }))
    emit('membersChanged')
  } catch (error) {
    toast.error(error)
  } finally {
    transferring.value = false
  }
}

// ----- leave / delete -----
const busy = ref(false)

async function leave() {
  if (!auth.user) return
  const ok = await confirm({
    title: t('settings.leave.confirm', { name: props.workspace.name }),
    body: t('settings.leave.body'),
    confirmLabel: t('settings.leave.action'),
    danger: true,
  })
  if (!ok) return
  busy.value = true
  try {
    const name = props.workspace.name
    await workspaces.leave(props.workspace.id, auth.user.id)
    toast.success(t('settings.leave.done', { name }))
    await navigateTo('/', { replace: true })
  } catch (error) {
    toast.error(error)
  } finally {
    busy.value = false
  }
}

async function remove() {
  const ok = await confirm({
    title: t('settings.delete.confirm', { name: props.workspace.name }),
    body: t('settings.delete.body'),
    confirmLabel: t('settings.delete.action'),
    danger: true,
    requireText: props.workspace.name,
    requireTextLabel: t('settings.delete.typeName'),
  })
  if (!ok) return
  busy.value = true
  try {
    await workspaces.remove(props.workspace.id)
    toast.success(t('settings.delete.done'))
    await navigateTo('/', { replace: true })
  } catch (error) {
    toast.error(error)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <UiCard :title="t('workspace.name')">
      <form novalidate class="flex flex-col gap-3 sm:flex-row sm:items-start" @submit.prevent="rename">
        <UiInput
          v-model="name"
          class="flex-1"
          :label="t('workspace.name')"
          hide-label
          :readonly="!canRename"
          :error="renameForm.errors.value.name ?? renameForm.formError.value"
          :maxlength="limits.workspaceName"
          autocomplete="off"
        />
        <UiButton v-if="canRename" type="submit" :disabled="!renameDirty" :loading="renameForm.pending.value">{{ t('common.save') }}</UiButton>
      </form>
    </UiCard>

    <UiCard :title="t('settings.dangerZone')" tone="danger">
      <div class="flex flex-col divide-y divide-line">
        <!-- Leave (everyone but the owner) -->
        <div class="flex flex-col gap-3 pb-5 sm:flex-row sm:items-center sm:justify-between">
          <div class="min-w-0">
            <h3 class="text-sm font-semibold">{{ t('settings.leave.title') }}</h3>
            <p class="mt-0.5 text-sm text-muted">{{ isOwner ? t('settings.leave.owner') : t('settings.leave.body') }}</p>
          </div>
          <UiButton variant="secondary" icon="logout" :disabled="isOwner || busy" class="shrink-0" @click="leave">{{ t('settings.leave.action') }}</UiButton>
        </div>

        <template v-if="isOwner">
          <!-- Transfer ownership -->
          <div class="flex flex-col gap-3 py-5">
            <div>
              <h3 class="text-sm font-semibold">{{ t('settings.transfer.title') }}</h3>
              <p class="mt-0.5 text-sm text-muted">{{ t('settings.transfer.body') }}</p>
            </div>
            <p v-if="candidates.length === 0" class="text-sm text-muted">{{ t('settings.transfer.noCandidates') }}</p>
            <div v-else class="flex flex-col gap-3 sm:flex-row sm:items-end">
              <UiSelect
                v-model="newOwnerId"
                class="flex-1"
                :label="t('settings.transfer.select')"
                :options="candidates.map((m) => ({ value: m.userId, label: `${m.displayName} — ${t(`roles.${m.role}`)}` }))"
              />
              <UiButton variant="secondary" icon="crown" :loading="transferring" class="shrink-0" @click="transfer">{{ t('settings.transfer.action') }}</UiButton>
            </div>
          </div>

          <!-- Delete -->
          <div class="flex flex-col gap-3 pt-5 sm:flex-row sm:items-center sm:justify-between">
            <div class="min-w-0">
              <h3 class="text-sm font-semibold">{{ t('settings.delete.title') }}</h3>
              <p class="mt-0.5 text-sm text-muted">{{ t('settings.delete.body') }}</p>
            </div>
            <UiButton variant="danger" icon="trash" :disabled="busy" class="shrink-0" @click="remove">{{ t('settings.delete.action') }}</UiButton>
          </div>
        </template>
      </div>
    </UiCard>
  </div>
</template>
